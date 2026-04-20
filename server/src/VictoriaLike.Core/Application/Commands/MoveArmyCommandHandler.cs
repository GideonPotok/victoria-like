using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Domain;

namespace VictoriaLike.Core.Application.Commands;

public sealed class MoveArmyCommandHandler : ICommandHandler
{
    public string CommandType => "MoveArmy";

    public CommandResult Handle(CommandEnvelope envelope, WorldState world, ActorId actor)
    {
        if (!MilitaryCommandHelpers.TryGetString(envelope.Payload, "armyId", out var armyId) ||
            !MilitaryCommandHelpers.TryGetString(envelope.Payload, "destinationProvinceId", out var destinationProvinceId))
        {
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload,
                "Missing armyId or destinationProvinceId in payload");
        }

        armyId = MilitaryCommandHelpers.NormalizeGuidString(armyId);
        destinationProvinceId = MilitaryCommandHelpers.NormalizeGuidString(destinationProvinceId);

        if (!world.Armies.TryGetValue(armyId, out var army))
            return CommandResult.Reject(CommandRejectionReason.ArmyNotFound, $"Army {armyId} not found");

        if (!world.Provinces.TryGetValue(destinationProvinceId, out var destination))
            return CommandResult.Reject(CommandRejectionReason.InvalidMovementTarget,
                $"Province {destinationProvinceId} not found");

        if (!CommandAuthorizer.TryResolveActorAccount(actor, world, out var account, out var authFailure))
            return authFailure!;

        if (!CommandAuthorizer.TryCheckCountryOwnership(account!, army.CountryId, out authFailure))
            return authFailure!;

        if (army.SoldierCount <= 0)
            return CommandResult.Reject(CommandRejectionReason.InvalidMovementTarget, "Army has no soldiers");

        if (army.IsMoving)
            return CommandResult.Reject(CommandRejectionReason.ArmyAlreadyMoving,
                $"Army {armyId} is already moving to {army.DestinationProvinceId}");

        if (string.Equals(army.LocationProvinceId, destinationProvinceId, StringComparison.Ordinal))
            return CommandResult.Reject(CommandRejectionReason.InvalidMovementTarget,
                "Army is already in the destination province");

        var isOwnProvince = string.Equals(destination.OwnerId, army.CountryId, StringComparison.Ordinal);
        var isWarTarget = world.Wars.Values.Any(war =>
            war.IsActive &&
            war.IsBetween(army.CountryId, destination.OwnerId));
        if (!isOwnProvince && !isWarTarget)
        {
            return CommandResult.Reject(CommandRejectionReason.InvalidMovementTarget,
                "Armies may only move to owned provinces or enemy provinces during an active war");
        }

        army.DestinationProvinceId = destinationProvinceId;
        army.MovementTicksRemaining = 2;
        world.EventLog.Add($"army-move-queued:{army.Id}:{army.LocationProvinceId}->{destinationProvinceId}:eta=2");
        return CommandResult.Success();
    }
}
