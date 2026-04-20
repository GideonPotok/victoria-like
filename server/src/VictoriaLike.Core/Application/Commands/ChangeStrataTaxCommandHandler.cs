using System.Text.Json;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Domain;

namespace VictoriaLike.Core.Application.Commands;

/// Sets a per-strata tax rate override on the controlled country. A rate of -1
/// (or below 0) clears the override and falls back to the flat TaxRate.
public class ChangeStrataTaxCommandHandler : ICommandHandler
{
    public string CommandType => "ChangeStrataTax";

    public CommandResult Handle(CommandEnvelope envelope, WorldState world, ActorId actor)
    {
        if (!envelope.Payload.TryGetValue("countryId", out var countryIdObj) ||
            !envelope.Payload.TryGetValue("strata", out var strataObj) ||
            !envelope.Payload.TryGetValue("rate", out var rateObj))
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload,
                "Missing countryId, strata or rate in payload");

        if (countryIdObj is not JsonElement countryIdEl ||
            strataObj is not JsonElement strataEl ||
            rateObj is not JsonElement rateEl)
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload, "Invalid payload format");

        if (countryIdEl.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(countryIdEl.GetString(), out var countryGuid))
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload, "Invalid countryId format");

        var strata = strataEl.GetString()?.Trim().ToLowerInvariant();
        if (strata != "poor" && strata != "middle" && strata != "rich")
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload,
                $"Invalid strata '{strata}'. Expected poor|middle|rich");

        if (!TryReadRate(rateEl, out var rate))
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload, "Invalid rate format");

        // Accept either fraction (0..1) or percent (0..100); -1 means clear override.
        if (rate > 1m && rate <= 100m)
            rate /= 100m;
        if (rate < 0m)
            rate = -1m;
        else if (rate > 1m)
            return CommandResult.Reject(CommandRejectionReason.InvalidParameterRange,
                $"Tax rate must be 0..1 or 0..100 (or -1 to clear), got {rate}");

        var countryIdString = countryGuid.ToString();
        if (!world.Countries.TryGetValue(countryIdString, out var country))
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload,
                $"Country {countryIdString} not found");

        if (!CommandAuthorizer.TryResolveActorAccount(actor, world, out var account, out var authFailure))
            return authFailure!;
        if (!CommandAuthorizer.TryCheckCountryOwnership(account!, countryIdString, out authFailure))
            return authFailure!;

        switch (strata)
        {
            case "poor": country.PoorTaxRate = rate; break;
            case "middle": country.MiddleTaxRate = rate; break;
            case "rich": country.RichTaxRate = rate; break;
        }

        return CommandResult.Success();
    }

    private static bool TryReadRate(JsonElement element, out decimal value)
    {
        value = 0m;
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetDecimal(out value),
            JsonValueKind.String => decimal.TryParse(element.GetString(), out value),
            _ => false
        };
    }
}
