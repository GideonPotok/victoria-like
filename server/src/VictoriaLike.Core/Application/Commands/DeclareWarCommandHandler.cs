using VictoriaLike.Core.Core.Military;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Domain;

namespace VictoriaLike.Core.Application.Commands;

public sealed class DeclareWarCommandHandler : ICommandHandler
{
    public string CommandType => "DeclareWar";

    public CommandResult Handle(CommandEnvelope envelope, WorldState world, ActorId actor)
    {
        if (!MilitaryCommandHelpers.TryGetString(envelope.Payload, "targetCountryId", out var targetCountryId))
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload, "Missing targetCountryId in payload");

        targetCountryId = MilitaryCommandHelpers.NormalizeGuidString(targetCountryId);

        if (!world.Countries.ContainsKey(targetCountryId))
            return CommandResult.Reject(CommandRejectionReason.CountryNotFound, $"Country {targetCountryId} not found");

        if (!CommandAuthorizer.TryResolveActorAccount(actor, world, out var account, out var authFailure))
            return authFailure!;

        var attackerCountryId = account!.ControlledCountry.Value.ToString();
        if (string.Equals(attackerCountryId, targetCountryId, StringComparison.Ordinal))
            return CommandResult.Reject(CommandRejectionReason.InvalidParameterRange, "A country cannot declare war on itself");

        if (!world.Countries.ContainsKey(attackerCountryId))
            return CommandResult.Fail($"Actor controls missing country {attackerCountryId}");

        if (world.Wars.Values.Any(war => war.IsActive && war.IsBetween(attackerCountryId, targetCountryId)))
            return CommandResult.Reject(CommandRejectionReason.AlreadyAtWar, "Countries are already at war");

        var war = new WarState
        {
            Id = Guid.NewGuid().ToString(),
            AttackerCountryId = attackerCountryId,
            DefenderCountryId = targetCountryId,
            StartedOn = world.Date.Value,
            IsActive = true
        };

        world.Wars[war.Id] = war;
        world.EventLog.Add($"war-declared:{war.Id}:{attackerCountryId}->{targetCountryId}:{world.Date.Value:yyyy-MM-dd}");
        return CommandResult.Success();
    }
}
