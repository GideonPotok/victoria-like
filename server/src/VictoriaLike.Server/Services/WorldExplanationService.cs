using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VictoriaLike.Core.Core.Economy;
using VictoriaLike.Core.Domain;
using VictoriaLike.Server.Api;
using VictoriaLike.Server.Api.Dtos;
using VictoriaLike.Server.Data;

namespace VictoriaLike.Server.Services;

public interface IWorldExplanationService
{
    Task<ExplanationDto?> ExplainGoodAsync(string goodId, CancellationToken cancellationToken = default);
    Task<ExplanationDto?> ExplainPopNeedsAsync(string popId, CancellationToken cancellationToken = default);
    Task<ExplanationDto?> ExplainProvinceEmploymentAsync(string provinceId, CancellationToken cancellationToken = default);
    Task<ExplanationDto?> ExplainCountryBudgetAsync(string countryId, CancellationToken cancellationToken = default);
    Task<ExplanationDto?> ExplainWarAsync(string warId, CancellationToken cancellationToken = default);
    Task<ExplanationDto?> ExplainBattleAsync(string battleId, CancellationToken cancellationToken = default);
}

public sealed class WorldExplanationService : IWorldExplanationService
{
    private readonly IWorldStateDatabase _worldDatabase;
    private readonly IGoodsService _goodsService;
    private readonly IMarketHistoryService _marketHistory;

    public WorldExplanationService(
        IWorldStateDatabase worldDatabase,
        IGoodsService goodsService,
        IMarketHistoryService marketHistory)
    {
        _worldDatabase = worldDatabase;
        _goodsService = goodsService;
        _marketHistory = marketHistory;
    }

    public async Task<ExplanationDto?> ExplainGoodAsync(string goodId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(goodId))
            return null;

        var world = await _worldDatabase.LoadWorldAsync(cancellationToken);
        if (world == null || world.Markets.Count == 0)
            return null;

        var market = world.Markets[0];
        if (!market.GoodPrices.TryGetValue(goodId, out var price))
            return null;

        var definition = _goodsService.All.FirstOrDefault(g => string.Equals(g.Id, goodId, StringComparison.OrdinalIgnoreCase));
        var history = _marketHistory.GetHistory(20);
        var previousPrice = history
            .Reverse()
            .Skip(1)
            .Select(tick => tick.Prices.GetValueOrDefault(goodId))
            .FirstOrDefault(value => value > 0m);
        if (previousPrice <= 0m)
            previousPrice = price;

        var explained = AdminEconomyExplainer.ExplainGood(
            goodId,
            definition?.DisplayName ?? goodId,
            definition,
            price,
            previousPrice,
            market.GoodSupply.GetValueOrDefault(goodId),
            market.GoodDemand.GetValueOrDefault(goodId),
            world,
            history);

        var dto = Create("good", goodId, $"{explained.Name} price", $"{explained.Name} is £{explained.Price:F2}.");
        dto.Metrics["price"] = explained.Price;
        dto.Metrics["previous_price"] = explained.PreviousPrice;
        dto.Metrics["price_delta"] = explained.PriceDelta;
        dto.Metrics["supply"] = explained.Supply;
        dto.Metrics["demand"] = explained.Demand;
        dto.Metrics["fulfillment_rate"] = explained.FulfillmentRate;
        dto.Metrics["pressure"] = explained.TargetPressure;

        AddFactor(dto, "Supply and demand", $"Demand {explained.Demand:F2} vs supply {explained.Supply:F2}.", explained.Demand > explained.Supply ? "negative" : "positive");
        AddFactor(dto, "Price pressure", $"Target pressure is {explained.TargetPressure:F2}x base price.", explained.TargetPressure > 1.1m ? "negative" : "info");
        AddFactor(dto, "Weekly clamp", explained.ClampApplied ? "Price movement hit a clamp or bound this tick." : "Price movement stayed within normal bounds.", explained.ClampApplied ? "info" : "positive");
        AddFactor(dto, "Largest producer", explained.LargestProducer ?? "No producer attribution available.", "info");
        AddFactor(dto, "Largest consumer", explained.LargestConsumer ?? "No consumer attribution available.", "info");
        return dto;
    }

    public async Task<ExplanationDto?> ExplainPopNeedsAsync(string popId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(popId, out var popGuid))
            return null;

        var world = await _worldDatabase.LoadWorldAsync(cancellationToken);
        if (world == null)
            return null;

        var province = world.Provinces.FirstOrDefault(p => p.PopGroups.Any(pop => pop.Id == popGuid));
        var pop = province?.PopGroups.FirstOrDefault(p => p.Id == popGuid);
        if (province == null || pop == null)
            return null;

        var country = world.Countries.FirstOrDefault(c => c.Id.Equals(province.OwnerId));
        var taxRate = country == null ? 0m : TaxRateForPop(country, pop);
        var unemployment = pop.Size > 0 ? (decimal)pop.UnemployedCount / pop.Size : 0m;

        var dto = Create("pop_needs", popId, $"{Capitalize(pop.PopType)} needs", $"{pop.PopType} in {province.Name} have {pop.LifeNeedsFulfillment:P0} life-needs fulfillment.");
        dto.Metrics["life_needs_fulfillment"] = pop.LifeNeedsFulfillment;
        dto.Metrics["everyday_needs_fulfillment"] = pop.EverydayNeedsFulfillment;
        dto.Metrics["luxury_needs_fulfillment"] = pop.LuxuryNeedsFulfillment;
        dto.Metrics["cash"] = pop.Cash;
        dto.Metrics["unemployment_share"] = unemployment;
        dto.Metrics["tax_rate"] = taxRate;
        dto.Metrics["militancy"] = pop.Militancy;

        AddFactor(dto, "Life needs", $"Life needs are {pop.LifeNeedsFulfillment:P0}.", pop.LifeNeedsFulfillment < 0.85m ? "negative" : "positive");
        AddFactor(dto, "Employment", $"{pop.UnemployedCount} of {pop.Size} POP members are unemployed.", unemployment > 0.10m ? "negative" : "info");
        AddFactor(dto, "Taxes", $"{pop.Strata} tax rate is {taxRate:P0}, reducing take-home pay.", taxRate >= 0.5m ? "negative" : "info");
        AddFactor(dto, "Cash reserve", $"Cash reserve is £{pop.Cash:F2}.", pop.Cash < 1m ? "negative" : "positive");
        AddRelated(dto, "province", province.Id.Value.ToString(), province.Name);
        if (country != null)
            AddRelated(dto, "country", country.Id.Value.ToString(), country.Name);
        return dto;
    }

    public async Task<ExplanationDto?> ExplainProvinceEmploymentAsync(string provinceId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(provinceId, out var provinceGuid))
            return null;

        var world = await _worldDatabase.LoadWorldAsync(cancellationToken);
        if (world == null)
            return null;

        var province = world.Provinces.FirstOrDefault(p => p.Id.Value == provinceGuid);
        if (province == null)
            return null;

        var workforce = province.PopGroups.Sum(pop => pop.EmployedCount + pop.UnemployedCount);
        var unemployed = province.PopGroups.Sum(pop => pop.UnemployedCount);
        var unemployment = workforce > 0 ? (decimal)unemployed / workforce : 0m;
        var factories = world.Factories.Where(factory => factory.ProvinceId?.Value == provinceGuid).ToList();

        var dto = Create("province_employment", provinceId, $"{province.Name} employment", $"{province.Name} unemployment is {unemployment:P1}.");
        dto.Metrics["workforce"] = workforce;
        dto.Metrics["employed"] = Math.Max(0, workforce - unemployed);
        dto.Metrics["unemployed"] = unemployed;
        dto.Metrics["unemployment_share"] = unemployment;
        dto.Metrics["factory_count"] = factories.Count;

        AddFactor(dto, "RGO", $"{province.RgoType} provides local primary employment.", "info");
        AddFactor(dto, "Factories", factories.Count == 0 ? "No factories are present." : $"{factories.Count} factories employ {factories.Sum(f => f.EmployedCraftsmen + f.EmployedClerks)} workers.", factories.Count == 0 ? "negative" : "positive");
        AddFactor(dto, "Unemployment", $"{unemployed} workers are unemployed.", unemployment > 0.15m ? "negative" : "positive");
        AddFactor(dto, "Needs pressure", $"Province needs fulfillment is {province.NeedsFulfillment:P0}.", province.NeedsFulfillment < 0.85m ? "negative" : "info");
        return dto;
    }

    public async Task<ExplanationDto?> ExplainCountryBudgetAsync(string countryId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(countryId, out var countryGuid))
            return null;

        var world = await _worldDatabase.LoadWorldAsync(cancellationToken);
        if (world == null)
            return null;

        var country = world.Countries.FirstOrDefault(c => c.Id.Value == countryGuid);
        if (country == null)
            return null;

        var provinces = world.Provinces.Where(p => p.OwnerId.Value == countryGuid).ToList();
        var population = provinces.Sum(p => p.PopGroups.Count > 0 ? p.PopGroups.Sum(pop => pop.Size) : p.Population);
        var weeklySpend = EstimateWeeklySpendingCost(population, NormalizeUnitValue(country.EducationSpending), NormalizeUnitValue(country.MilitarySpending), NormalizeUnitValue(country.AdministrationSpending));

        var dto = Create("country_budget", countryId, $"{country.Name} budget", $"{country.Name} treasury is £{country.Treasury:F2}.");
        dto.Metrics["treasury"] = country.Treasury;
        dto.Metrics["tax_rate"] = country.TaxRate / 100m;
        dto.Metrics["poor_tax_rate"] = NormalizeTaxOverride(country.PoorTaxRate, country.TaxRate);
        dto.Metrics["middle_tax_rate"] = NormalizeTaxOverride(country.MiddleTaxRate, country.TaxRate);
        dto.Metrics["rich_tax_rate"] = NormalizeTaxOverride(country.RichTaxRate, country.TaxRate);
        dto.Metrics["estimated_weekly_spending"] = weeklySpend;
        dto.Metrics["population"] = population;

        AddFactor(dto, "Treasury", $"Treasury is £{country.Treasury:F2}.", country.Treasury < 1_000m ? "negative" : "positive");
        AddFactor(dto, "Tax rates", $"Poor {dto.Metrics["poor_tax_rate"]:P0}, middle {dto.Metrics["middle_tax_rate"]:P0}, rich {dto.Metrics["rich_tax_rate"]:P0}.", "info");
        AddFactor(dto, "Spending", $"Estimated weekly state spending is £{weeklySpend:F2}.", weeklySpend > country.Treasury ? "negative" : "info");
        AddFactor(dto, "Policy mix", $"Education {NormalizeUnitValue(country.EducationSpending):P0}, military {NormalizeUnitValue(country.MilitarySpending):P0}, administration {NormalizeUnitValue(country.AdministrationSpending):P0}.", "info");
        return dto;
    }

    public async Task<ExplanationDto?> ExplainWarAsync(string warId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(warId, out var warGuid))
            return null;

        var world = await _worldDatabase.LoadWorldAsync(cancellationToken);
        if (world == null)
            return null;

        var war = world.Wars.FirstOrDefault(w => w.Id == warGuid);
        if (war == null)
            return null;

        var attacker = world.Countries.FirstOrDefault(c => c.Id.Equals(war.AttackerCountryId));
        var defender = world.Countries.FirstOrDefault(c => c.Id.Equals(war.DefenderCountryId));
        var battles = world.BattleReports.Where(b => b.WarId == war.Id).ToList();
        var attackerSoldiers = world.Armies.Where(a => a.CountryId.Equals(war.AttackerCountryId)).Sum(a => a.SoldierCount);
        var defenderSoldiers = world.Armies.Where(a => a.CountryId.Equals(war.DefenderCountryId)).Sum(a => a.SoldierCount);

        var dto = Create("war", warId, $"{attacker?.Name ?? "Attacker"} vs {defender?.Name ?? "Defender"}", war.IsActive ? "War is active." : "War has ended.");
        dto.Metrics["battle_count"] = battles.Count;
        dto.Metrics["attacker_soldiers"] = attackerSoldiers;
        dto.Metrics["defender_soldiers"] = defenderSoldiers;
        dto.Metrics["attacker_casualties"] = battles.Where(b => b.LoserCountryId == war.AttackerCountryId.Value).Sum(b => b.LoserCasualties) + battles.Where(b => b.WinnerCountryId == war.AttackerCountryId.Value).Sum(b => b.WinnerCasualties);
        dto.Metrics["defender_casualties"] = battles.Where(b => b.LoserCountryId == war.DefenderCountryId.Value).Sum(b => b.LoserCasualties) + battles.Where(b => b.WinnerCountryId == war.DefenderCountryId.Value).Sum(b => b.WinnerCasualties);

        AddFactor(dto, "Status", war.IsActive ? "The war is active and armies can enter enemy provinces." : $"Peace was made on {war.EndedAt:yyyy-MM-dd}.", war.IsActive ? "negative" : "positive");
        AddFactor(dto, "Battles", battles.Count == 0 ? "No battles have been recorded." : $"{battles.Count} battle(s) have been recorded.", battles.Count == 0 ? "info" : "negative");
        AddFactor(dto, "Army strength", $"{attacker?.Name ?? "Attacker"} has {attackerSoldiers} soldiers; {defender?.Name ?? "Defender"} has {defenderSoldiers}.", "info");
        return dto;
    }

    public async Task<ExplanationDto?> ExplainBattleAsync(string battleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(battleId))
            return null;

        var world = await _worldDatabase.LoadWorldAsync(cancellationToken);
        if (world == null)
            return null;

        var battle = world.BattleReports.FirstOrDefault(b => string.Equals(b.Id, battleId, StringComparison.Ordinal));
        if (battle == null)
            return null;

        var province = world.Provinces.FirstOrDefault(p => p.Id.Value == battle.ProvinceId);
        var winner = world.Countries.FirstOrDefault(c => c.Id.Value == battle.WinnerCountryId);
        var loser = world.Countries.FirstOrDefault(c => c.Id.Value == battle.LoserCountryId);

        var dto = Create("battle", battleId, $"Battle of {province?.Name ?? battle.ProvinceId.ToString()}", $"{winner?.Name ?? "Winner"} defeated {loser?.Name ?? "loser"}.");
        dto.Metrics["winner_casualties"] = battle.WinnerCasualties;
        dto.Metrics["loser_casualties"] = battle.LoserCasualties;
        dto.Metrics["winner_morale_after"] = battle.WinnerMoraleAfter;
        dto.Metrics["loser_morale_after"] = battle.LoserMoraleAfter;

        AddFactor(dto, "Outcome", $"{winner?.Name ?? battle.WinnerCountryId.ToString()} won the battle.", "positive");
        AddFactor(dto, "Casualties", $"Winner losses {battle.WinnerCasualties}; loser losses {battle.LoserCasualties}.", "negative");
        AddFactor(dto, "Morale", $"Winner morale {battle.WinnerMoraleAfter:P0}; loser morale {battle.LoserMoraleAfter:P0}.", battle.LoserMoraleAfter < 0.3m ? "negative" : "info");
        AddRelated(dto, "war", battle.WarId.ToString(), "War");
        if (province != null)
            AddRelated(dto, "province", province.Id.Value.ToString(), province.Name);
        return dto;
    }

    private static ExplanationDto Create(string subjectType, string subjectId, string title, string summary) =>
        new()
        {
            SubjectType = subjectType,
            SubjectId = subjectId,
            Title = title,
            Summary = summary,
            GeneratedAt = DateTime.UtcNow
        };

    private static void AddFactor(ExplanationDto dto, string label, string detail, string impact) =>
        dto.Factors.Add(new ExplanationFactorDto { Label = label, Detail = detail, Impact = impact });

    private static void AddRelated(ExplanationDto dto, string type, string id, string label) =>
        dto.Related.Add(new ExplanationLinkDto { Type = type, Id = id, Label = label });

    private static decimal TaxRateForPop(Country country, PopGroup pop) =>
        pop.Strata.ToLowerInvariant() switch
        {
            "middle" => NormalizeTaxOverride(country.MiddleTaxRate, country.TaxRate),
            "rich" => NormalizeTaxOverride(country.RichTaxRate, country.TaxRate),
            _ => NormalizeTaxOverride(country.PoorTaxRate, country.TaxRate)
        };

    private static decimal NormalizeTaxOverride(decimal overrideRate, int fallbackTaxRate) =>
        overrideRate < 0m ? Math.Clamp(fallbackTaxRate / 100m, 0m, 1m) : NormalizeUnitValue(overrideRate);

    private static decimal NormalizeUnitValue(decimal value) =>
        value > 1m ? Math.Clamp(value / 100m, 0m, 1m) : Math.Clamp(value, 0m, 1m);

    private static decimal EstimateWeeklySpendingCost(int population, decimal education, decimal military, decimal administration)
    {
        var populationScale = population / 1000m;
        return populationScale * ((education * 0.10m) + (military * 0.12m) + (administration * 0.08m));
    }

    private static string Capitalize(string value) =>
        string.IsNullOrWhiteSpace(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];
}
