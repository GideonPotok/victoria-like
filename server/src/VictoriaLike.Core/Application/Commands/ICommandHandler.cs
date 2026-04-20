using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Domain;

namespace VictoriaLike.Core.Application.Commands;

public enum CommandOutcome { Applied, Rejected, Failed }

public enum CommandRejectionReason
{
    UnknownCommandType,
    MalformedPayload,
    NoPlayerAccount,
    NotCountryOwner,
    InsufficientTreasury,
    ProvinceNotOwned,
    InvalidParameterRange,
    DuplicateCommand,
    StaleClientState,
    ActiveConstructionConflict,
    ArmyNotFound,
    ArmyAlreadyMoving,
    InvalidMovementTarget,
    CountryNotFound,
    AlreadyAtWar,
    NotAtWar,
    WarStateConflict
}

public sealed class CommandResult
{
    public CommandOutcome Outcome { get; private init; }
    public string? Message { get; private init; }
    public string? ErrorMessage => Message;
    public CommandRejectionReason? RejectionReason { get; private init; }

    public bool IsSuccess => Outcome == CommandOutcome.Applied;

    public string OutcomeStatus => Outcome switch
    {
        CommandOutcome.Applied => "applied",
        CommandOutcome.Rejected => "rejected",
        _ => "failed"
    };

    public static CommandResult Success() => new() { Outcome = CommandOutcome.Applied };

    public static CommandResult Reject(CommandRejectionReason reason, string message) =>
        new() { Outcome = CommandOutcome.Rejected, RejectionReason = reason, Message = message };

    public static CommandResult Fail(string message) =>
        new() { Outcome = CommandOutcome.Failed, Message = message };

    // Back-compat alias
    public static CommandResult Failure(string message) => Fail(message);
}

public interface ICommandHandler
{
    string CommandType { get; }
    CommandResult Handle(CommandEnvelope envelope, WorldState world, ActorId actor);
}
