using VictoriaLike.Core.Core.Buildings;
using VictoriaLike.Core.Core.Countries;
using VictoriaLike.Core.Core.Economy;
using VictoriaLike.Core.Core.Pops;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Data.Validation;
using VictoriaLike.Core.Domain;
using Xunit;

namespace VictoriaLike.Core.Tests;

public sealed class WorldInvariantCheckerTests
{
    [Fact]
    public void Check_AcceptsValidWorld()
    {
        var report = new WorldInvariantChecker().Check(CreateValidWorld());

        Assert.True(report.IsValid);
        Assert.Empty(report.Violations);
    }

    [Fact]
    public void Check_RejectsNegativeMarketQuantitiesAndOutOfClampPrices()
    {
        var world = CreateValidWorld();
        world.Market.SupplyLastTick["grain"] = -1m;
        world.Market.Prices["grain"] = 99m;

        var report = new WorldInvariantChecker().Check(world);

        Assert.Contains(report.Violations, violation => violation.Code == "market_supply_negative");
        Assert.Contains(report.Violations, violation => violation.Code == "market_price_out_of_bounds");
    }

    [Fact]
    public void Check_RejectsInvalidProvinceOwnerAndPlayerMapping()
    {
        var world = CreateValidWorld();
        world.Provinces.Values.Single().OwnerId = "missing-country";
        world.PlayerAccounts["actor-1"] = new PlayerAccount(ActorId.New(), "broken-player", CountryId.New());

        var report = new WorldInvariantChecker().Check(world);

        Assert.Contains(report.Violations, violation => violation.Code == "province_missing_owner");
        Assert.Contains(report.Violations, violation => violation.Code == "account_missing_country");
    }

    [Fact]
    public void Check_RejectsDuplicateProvinceConstructionAndBadTicks()
    {
        var world = CreateValidWorld();
        var provinceId = world.Provinces.Keys.Single();
        var countryId = world.Countries.Keys.Single();
        world.BuildingQueue.Add(new BuildingQueueEntry
        {
            Id = "queue-2",
            ProvinceId = provinceId,
            CountryId = countryId,
            BuildingType = "farm",
            TicksRemaining = -1,
            QueuedAt = DateTime.UtcNow
        });

        var report = new WorldInvariantChecker().Check(world);

        Assert.Contains(report.Violations, violation => violation.Code == "building_queue_duplicate_province");
        Assert.Contains(report.Violations, violation => violation.Code == "building_queue_ticks_out_of_bounds");
    }

    private static WorldState CreateValidWorld()
    {
        var actorId = ActorId.New();
        var countryId = Guid.Parse("11111111-1111-1111-1111-111111111111").ToString();
        var provinceId = Guid.Parse("22222222-2222-2222-2222-222222222222").ToString();

        return new WorldState
        {
            Seed = 1,
            Date = new GameDate(new DateOnly(1800, 1, 1)),
            Countries = new Dictionary<string, CountryState>
            {
                [countryId] = new()
                {
                    Id = countryId,
                    DisplayName = "Albion",
                    ProvinceIds = [provinceId],
                    Treasury = 100m,
                    TaxRate = 10m,
                    TariffRate = 0m,
                    IsPlayable = true,
                    Stockpile = { ["grain"] = 1m }
                }
            },
            Provinces = new Dictionary<string, ProvinceState>
            {
                [provinceId] = new()
                {
                    Id = provinceId,
                    DisplayName = "Albionshire",
                    OwnerId = countryId,
                    PopulationIds = ["pop-1"],
                    Stockpile = { ["grain"] = 1m },
                    OutputsPerTick = { ["grain"] = 0.5m }
                }
            },
            PlayerAccounts = new Dictionary<string, PlayerAccount>
            {
                ["actor-1"] = new(actorId, "albion-player", new CountryId(Guid.Parse("11111111-1111-1111-1111-111111111111")))
            },
            Pops = new Dictionary<string, PopState>
            {
                ["pop-1"] = new()
                {
                    Id = "pop-1",
                    ProvinceId = provinceId,
                    PopClass = "farmers",
                    Size = 1_000,
                    NeedsFulfillment = 1m,
                    Needs = new PopNeedProfile()
                }
            },
            Goods = new Dictionary<string, GoodDefinition>
            {
                ["grain"] = new("grain", "Grain", 2m, "food")
            },
            Market = new MarketState
            {
                Prices = { ["grain"] = 2m },
                SupplyLastTick = { ["grain"] = 1m },
                DemandLastTick = { ["grain"] = 1m }
            },
            BuildingQueue =
            [
                new BuildingQueueEntry
                {
                    Id = "queue-1",
                    ProvinceId = provinceId,
                    CountryId = countryId,
                    BuildingType = "farm",
                    TicksRemaining = 12,
                    QueuedAt = DateTime.UtcNow
                }
            ]
        };
    }
}
