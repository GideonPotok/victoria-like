using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VictoriaLike.Core.Domain;
using VictoriaLike.Server.Data;

namespace VictoriaLike.Server.Services;

public interface ICommandQueueService
{
    Task<CommandEnqueueResult> EnqueueAsync(CommandEnvelope command);
    Task<List<CommandEnvelope>> DequeueAllAsync();
    int PendingCount { get; }
}

public sealed record CommandEnqueueResult(CommandEnvelope Command, string Status, bool IsDuplicate);

public class CommandQueueService : ICommandQueueService
{
    private readonly Queue<CommandEnvelope> _queue = new();
    private readonly ILogger<CommandQueueService> _logger;
    private readonly ICommandRepository _repository;
    private readonly object _lockObject = new();

    public int PendingCount
    {
        get
        {
            lock (_lockObject)
            {
                return _queue.Count;
            }
        }
    }

    public CommandQueueService(ILogger<CommandQueueService> logger, ICommandRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    public async Task<CommandEnqueueResult> EnqueueAsync(CommandEnvelope command)
    {
        var saveResult = await _repository.SaveCommandAsync(command);

        if (saveResult.Inserted)
        {
            lock (_lockObject)
            {
                _queue.Enqueue(saveResult.Command);
            }

            _logger.LogInformation("Command enqueued: {CommandId} type={CommandType} actor={ActorId} submitted_tick={SubmittedTick}",
                saveResult.Command.Id, saveResult.Command.CommandType, saveResult.Command.ActorId, saveResult.Command.SubmittedTick);
        }

        return new CommandEnqueueResult(saveResult.Command, saveResult.Status, !saveResult.Inserted);
    }

    public Task<List<CommandEnvelope>> DequeueAllAsync()
    {
        lock (_lockObject)
        {
            var commands = new List<CommandEnvelope>(_queue.Count);
            while (_queue.TryDequeue(out var cmd))
            {
                commands.Add(cmd);
            }
            commands.Sort(static (left, right) =>
            {
                var tick = left.SubmittedTick.CompareTo(right.SubmittedTick);
                if (tick != 0) return tick;

                var received = left.ReceivedAt.CompareTo(right.ReceivedAt);
                if (received != 0) return received;

                return left.Id.Value.CompareTo(right.Id.Value);
            });

            if (commands.Count > 0)
            {
                _logger.LogInformation("Dequeued {Count} commands for tick processing", commands.Count);
            }
            return Task.FromResult(commands);
        }
    }
}
