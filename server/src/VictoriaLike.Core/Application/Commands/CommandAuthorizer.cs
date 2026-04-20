using VictoriaLike.Core.Core.Countries;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Domain;

namespace VictoriaLike.Core.Application.Commands;

/// Shared authorization helpers for command handlers. Call these to enforce ownership and
/// resource checks consistently rather than reimplementing them per handler.
public static class CommandAuthorizer
{
    public static bool TryResolveActorAccount(
        ActorId actor,
        WorldState world,
        out PlayerAccount? account,
        out CommandResult? failure)
    {
        if (!world.PlayerAccounts.TryGetValue(actor.ToString(), out account))
        {
            failure = CommandResult.Reject(CommandRejectionReason.NoPlayerAccount,
                $"Actor {actor} has no player account");
            return false;
        }
        failure = null;
        return true;
    }

    public static bool TryCheckCountryOwnership(
        PlayerAccount account,
        string countryId,
        out CommandResult? failure)
    {
        if (account.ControlledCountry.Value.ToString() != countryId)
        {
            failure = CommandResult.Reject(CommandRejectionReason.NotCountryOwner,
                $"Actor {account.Id} does not control country {countryId}");
            return false;
        }
        failure = null;
        return true;
    }

    public static bool TryCheckProvinceOwnership(
        PlayerAccount account,
        ProvinceState province,
        out CommandResult? failure)
    {
        var controlledCountryId = account.ControlledCountry.Value.ToString();
        if (province.OwnerId != controlledCountryId)
        {
            failure = CommandResult.Reject(CommandRejectionReason.ProvinceNotOwned,
                $"Actor {account.Id} does not control province {province.Id}");
            return false;
        }
        failure = null;
        return true;
    }

    public static bool TryCheckTreasury(
        CountryState country,
        decimal required,
        out CommandResult? failure)
    {
        if (country.Treasury < required)
        {
            failure = CommandResult.Reject(CommandRejectionReason.InsufficientTreasury,
                $"Insufficient treasury: need {required:F0}, have {country.Treasury:F2}");
            return false;
        }
        failure = null;
        return true;
    }
}
