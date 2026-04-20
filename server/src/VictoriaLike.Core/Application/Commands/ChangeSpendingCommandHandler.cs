using System.Text.Json;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Domain;

namespace VictoriaLike.Core.Application.Commands;

/// Sets education/military/administration spending intensity on the controlled
/// country. Levels are normalized to 0..1.
public class ChangeSpendingCommandHandler : ICommandHandler
{
    public string CommandType => "ChangeSpending";

    public CommandResult Handle(CommandEnvelope envelope, WorldState world, ActorId actor)
    {
        if (!envelope.Payload.TryGetValue("countryId", out var countryIdObj) ||
            !envelope.Payload.TryGetValue("category", out var categoryObj) ||
            !envelope.Payload.TryGetValue("level", out var levelObj))
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload,
                "Missing countryId, category or level in payload");

        if (countryIdObj is not JsonElement countryIdEl ||
            categoryObj is not JsonElement categoryEl ||
            levelObj is not JsonElement levelEl)
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload, "Invalid payload format");

        if (countryIdEl.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(countryIdEl.GetString(), out var countryGuid))
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload, "Invalid countryId format");

        var category = categoryEl.GetString()?.Trim().ToLowerInvariant();
        if (category != "education" && category != "military" && category != "administration")
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload,
                $"Invalid category '{category}'. Expected education|military|administration");

        if (!TryReadDecimal(levelEl, out var level))
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload, "Invalid level format");

        if (level > 1m && level <= 100m)
            level /= 100m;
        if (level < 0m || level > 1m)
            return CommandResult.Reject(CommandRejectionReason.InvalidParameterRange,
                $"Spending level must be 0..1 or 0..100, got {level}");

        var countryIdString = countryGuid.ToString();
        if (!world.Countries.TryGetValue(countryIdString, out var country))
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload,
                $"Country {countryIdString} not found");

        if (!CommandAuthorizer.TryResolveActorAccount(actor, world, out var account, out var authFailure))
            return authFailure!;
        if (!CommandAuthorizer.TryCheckCountryOwnership(account!, countryIdString, out authFailure))
            return authFailure!;

        switch (category)
        {
            case "education": country.EducationSpending = level; break;
            case "military": country.MilitarySpending = level; break;
            case "administration": country.AdministrationSpending = level; break;
        }

        return CommandResult.Success();
    }

    private static bool TryReadDecimal(JsonElement element, out decimal value)
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
