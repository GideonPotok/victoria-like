using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VictoriaLike.Core.Application.Commands;
using VictoriaLike.Core.Application.Logging;
using VictoriaLike.Core.Domain;
using VictoriaLike.Core.Simulation.TickPipeline;

namespace VictoriaLike.Core.Simulation.Systems;

public interface ICommandOutcomeRecorder
{
    Task RecordOutcomeAsync(
        CommandId commandId,
        ActorId actorId,
        string commandType,
        string outcomeStatus,
        string? reason,
        long appliedTick,
        CommandRejectionReason? rejectionReasonCode = null);
}

public class CommandProcessingStage : ISimulationStage
{
    public string Name => "CommandProcessing";

    private readonly Dictionary<string, ICommandHandler> _handlers;
    private readonly ICommandOutcomeRecorder? _outcomeRecorder;

    public CommandProcessingStage(IEnumerable<ICommandHandler> handlers, ICommandOutcomeRecorder? outcomeRecorder = null)
    {
        _handlers = handlers.ToDictionary(h => h.CommandType);
        _outcomeRecorder = outcomeRecorder;
    }

    public void Execute(SimulationContext context)
    {
        // placeholder — server calls ProcessCommandsAsync directly
    }

    public async Task ProcessCommandsAsync(List<CommandEnvelope> commands, SimulationContext context, long currentTick)
    {
        var seenCommandIds = new HashSet<CommandId>();
        foreach (var command in commands
            .OrderBy(c => c.SubmittedTick)
            .ThenBy(c => c.ReceivedAt)
            .ThenBy(c => c.Id.Value))
        {
            if (!seenCommandIds.Add(command.Id))
            {
                await RecordRejectedAsync(command, currentTick, CommandRejectionReason.DuplicateCommand,
                    "Duplicate command id already processed in this batch");
                context.Log.LogCommandFailure(command.Id.ToString(), "Duplicate command id already processed in this batch");
                continue;
            }

            if (command.ExpectedWorldTick.HasValue && command.ExpectedWorldTick.Value < currentTick - 1)
            {
                var staleReason =
                    $"Stale client state: expected tick {command.ExpectedWorldTick.Value}, executing tick {currentTick}";
                await RecordRejectedAsync(command, currentTick, CommandRejectionReason.StaleClientState, staleReason);
                context.Log.LogCommandFailure(command.Id.ToString(), staleReason);
                continue;
            }

            if (!_handlers.TryGetValue(command.CommandType, out var handler))
            {
                context.Log.LogCommandFailure(command.Id.ToString(), "Unknown command type");
                if (_outcomeRecorder != null)
                    await _outcomeRecorder.RecordOutcomeAsync(command.Id, command.ActorId, command.CommandType, "rejected", "Unknown command type", currentTick);
                continue;
            }

            try
            {
                var result = handler.Handle(command, context.World, command.ActorId);

                if (result.IsSuccess)
                    context.Log.LogCommandSuccess(command.Id.ToString(), command.CommandType);
                else
                    context.Log.LogCommandFailure(command.Id.ToString(), result.Message ?? "Unknown error");

                if (_outcomeRecorder != null)
                    await _outcomeRecorder.RecordOutcomeAsync(command.Id, command.ActorId, command.CommandType, result.OutcomeStatus, result.Message, currentTick, result.RejectionReason);
            }
            catch (Exception ex)
            {
                var reason = $"Command pipeline error: {ex.Message}";
                context.Log.LogCommandFailure(command.Id.ToString(), reason);
                if (_outcomeRecorder != null)
                    await _outcomeRecorder.RecordOutcomeAsync(command.Id, command.ActorId, command.CommandType, "failed", reason, currentTick);
            }
        }
    }

    private async Task RecordRejectedAsync(
        CommandEnvelope command,
        long currentTick,
        CommandRejectionReason reasonCode,
        string reason)
    {
        if (_outcomeRecorder != null)
            await _outcomeRecorder.RecordOutcomeAsync(command.Id, command.ActorId, command.CommandType, "rejected", reason, currentTick, reasonCode);
    }
}
