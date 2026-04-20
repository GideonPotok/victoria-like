namespace VictoriaLike.Core.Core.Pops;

public static class PopNeedProfileCatalog
{
    private static readonly PopNeedProfile Poor = new()
    {
        Life =
        {
            ["grain"] = 0.75m,
            ["clothes"] = 0.05m
        },
        Everyday =
        {
            ["liquor"] = 0.03m,
            ["tools"] = 0.02m
        },
        Luxury =
        {
            ["luxury_clothes"] = 0.003m
        }
    };

    private static readonly PopNeedProfile Middle = new()
    {
        Life =
        {
            ["grain"] = 0.55m,
            ["clothes"] = 0.08m
        },
        Everyday =
        {
            ["furniture"] = 0.04m,
            ["liquor"] = 0.03m,
            ["tools"] = 0.03m
        },
        Luxury =
        {
            ["luxury_clothes"] = 0.008m,
            ["luxury_furniture"] = 0.004m
        }
    };

    private static readonly PopNeedProfile Rich = new()
    {
        Life =
        {
            ["grain"] = 0.35m,
            ["clothes"] = 0.10m
        },
        Everyday =
        {
            ["furniture"] = 0.08m,
            ["liquor"] = 0.04m,
            ["tools"] = 0.02m
        },
        Luxury =
        {
            ["luxury_clothes"] = 0.02m,
            ["luxury_furniture"] = 0.015m
        }
    };

    public static PopNeedProfile ForPopClass(string popClass)
    {
        var profile = StrataFor(popClass) switch
        {
            "middle" => Middle,
            "rich" => Rich,
            _ => Poor
        };

        return Clone(profile);
    }

    public static PopNeedProfile ApplyScenarioOverrides(
        string popClass,
        IReadOnlyDictionary<string, decimal> life,
        IReadOnlyDictionary<string, decimal> everyday,
        IReadOnlyDictionary<string, decimal> luxury)
    {
        var defaults = ForPopClass(popClass);
        return new PopNeedProfile
        {
            Life = life.Count == 0 ? defaults.Life : CopyPositive(life),
            Everyday = everyday.Count == 0 ? defaults.Everyday : CopyPositive(everyday),
            Luxury = luxury.Count == 0 ? defaults.Luxury : CopyPositive(luxury)
        };
    }

    private static string StrataFor(string popClass)
    {
        return popClass.Trim().ToLowerInvariant() switch
        {
            "clerks" or "clergy" or "bureaucrats" or "artisans" => "middle",
            "aristocrats" or "capitalists" => "rich",
            _ => "poor"
        };
    }

    private static PopNeedProfile Clone(PopNeedProfile profile) =>
        new()
        {
            Life = new Dictionary<string, decimal>(profile.Life),
            Everyday = new Dictionary<string, decimal>(profile.Everyday),
            Luxury = new Dictionary<string, decimal>(profile.Luxury)
        };

    private static Dictionary<string, decimal> CopyPositive(IReadOnlyDictionary<string, decimal> source) =>
        source
            .Where(entry => entry.Value > 0m)
            .ToDictionary(entry => entry.Key.Trim().ToLowerInvariant(), entry => entry.Value);
}
