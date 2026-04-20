using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VictoriaLike.Core.Application.Commands;
using VictoriaLike.Core.Domain;
using VictoriaLike.Core.Simulation.Systems;
using VictoriaLike.Server.Data;

namespace VictoriaLike.Server.Services;

public class CommandOutcomeRecorder : ICommandOutcomeRecorder
{
    private readonly ICommandRepository _repository;
    private readonly IWorldWebSocketHub _hub;
    private readonly ILogger<CommandOutcomeRecorder> _logger;

    public CommandOutcomeRecorder(ICommandRepository repository, IWorldWebSocketHub hub, ILogger<CommandOutcomeRecorder> logger)
    {
        _repository = repository;
        _hub = hub;
        _logger = logger;
    }

    public async Task RecordOutcomeAsync(
        CommandId commandId,
        ActorId actorId,
        string commandType,
        string outcomeStatus,
        string? reason,
        long appliedTick,
        CommandRejectionReason? rejectionReasonCode = null)
    {
        await _repository.UpdateCommandOutcomeAsync(commandId, outcomeStatus, reason, appliedTick, rejectionReasonCode);
        try
        {
            await _hub.SendCommandResultAsync(
                actorId.ToString(),
                commandId.ToString(),
                commandType,
                outcomeStatus,
                reason,
                rejectionReasonCode?.ToString(),
                retryAfterTicks: null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send command_result via WebSocket for command {CommandId}", commandId);
        }
    }
}
