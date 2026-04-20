using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Domain;

namespace VictoriaLike.Core.Application.Commands;

public sealed class MakePeaceCommandHandler : ICommandHandler
{
    public string CommandType => "MakePeace";

    public CommandResult Handle(CommandEnvelope envelope, WorldState world, ActorId actor)
    {
        if (!MilitaryCommandHelpers.TryGetString(envelope.Payload, "targetCountryId", out var targetCountryId))
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload, "Missing targetCountryId in payload");

        targetCountryId = MilitaryCommandHelpers.NormalizeGuidString(targetCountryId);

        if (!world.Countries.ContainsKey(targetCountryId))
            return CommandResult.Reject(CommandRejectionReason.CountryNotFound, $"Country {targetCountryId} not found");

        if (!CommandAuthorizer.TryResolveActorAccount(actor, world, out var account, out var authFailure))
            return authFailure!;

        var requesterCountryId = account!.ControlledCountry.Value.ToString();
        var activeWars = world.Wars.Values
            .Where(war => war.IsActive && war.IsBetween(requesterCountryId, targetCountryId))
            .ToList();

        if (activeWars.Count == 0)
            return CommandResult.Reject(CommandRejectionReason.NotAtWar, "Countries are not at war");

        if (activeWars.Count > 1)
            return CommandResult.Reject(CommandRejectionReason.WarStateConflict, "Contradictory active war state detected");

        var war = activeWars[0];
        war.IsActive = false;
        war.EndedOn = world.Date.Value;
        world.EventLog.Add($"peace-made:{war.Id}:{requesterCountryId}<->{targetCountryId}:{world.Date.Value:yyyy-MM-dd}");
        return CommandResult.Success();
    }
}
