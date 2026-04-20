using System.Text.Json;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Domain;

namespace VictoriaLike.Core.Application.Commands;

public class ChangeTaxRateCommandHandler : ICommandHandler
{
    public string CommandType => "ChangeTaxRate";

    public CommandResult Handle(CommandEnvelope envelope, WorldState world, ActorId actor)
    {
        if (!envelope.Payload.TryGetValue("countryId", out var countryIdObj) ||
            !envelope.Payload.TryGetValue("newTaxRate", out var taxRateObj))
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload,
                "Missing countryId or newTaxRate in payload");

        if (countryIdObj is not JsonElement countryIdElement || taxRateObj is not JsonElement taxRateElement)
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload, "Invalid payload format");

        if (!TryReadCountryId(countryIdElement, out var countryGuid))
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload, "Invalid countryId format");

        if (!TryReadTaxRate(taxRateElement, out var newTaxRate))
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload, "Invalid newTaxRate format");

        if (newTaxRate < 0 || newTaxRate > 100)
            return CommandResult.Reject(CommandRejectionReason.InvalidParameterRange,
                $"Tax rate must be 0–100, got {newTaxRate}");

        var countryIdString = countryGuid.ToString();

        if (!world.Countries.TryGetValue(countryIdString, out var country))
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload,
                $"Country {countryIdString} not found");

        if (!CommandAuthorizer.TryResolveActorAccount(actor, world, out var account, out var authFailure))
            return authFailure!;

        if (!CommandAuthorizer.TryCheckCountryOwnership(account!, countryIdString, out authFailure))
            return authFailure!;

        country.TaxRate = newTaxRate;
        return CommandResult.Success();
    }

    private static bool TryReadCountryId(JsonElement element, out Guid countryGuid)
    {
        countryGuid = Guid.Empty;
        if (element.ValueKind != JsonValueKind.String) return false;
        var value = element.GetString();
        return !string.IsNullOrWhiteSpace(value) && Guid.TryParse(value, out countryGuid);
    }

    private static bool TryReadTaxRate(JsonElement element, out int taxRate)
    {
        taxRate = 0;
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt32(out taxRate),
            JsonValueKind.String => int.TryParse(element.GetString(), out taxRate),
            _ => false
        };
    }
}
