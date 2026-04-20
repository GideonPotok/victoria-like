using VictoriaLike.Core.Application.Logging;
using VictoriaLike.Core.Application.Profiling;
using VictoriaLike.Core.Core.Common;
using VictoriaLike.Core.Core.Countries;
using VictoriaLike.Core.Core.Economy;
using VictoriaLike.Core.Core.Pops;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Data.Loaders;
using VictoriaLike.Core.Simulation.Systems;
using VictoriaLike.Core.Simulation.TickPipeline;
using Xunit;

namespace VictoriaLike.Core.Tests;

public sealed class PopNeedProfileCatalogTests
{
    [Theory]
    [InlineData("farmers", "grain", "liquor", "luxury_clothes")]
    [InlineData("clerks", "clothes", "furniture", "luxury_furniture")]
    [InlineData("capitalists", "clothes", "furniture", "luxury_furniture")]
    public void ForPopClass_ReturnsLifeEverydayAndLuxuryNeeds(string popClass, string lifeGood, string everydayGood, string luxuryGood)
    {
        var profile = PopNeedProfileCatalog.ForPopClass(popClass);

        Assert.Contains(lifeGood, profile.Life.Keys);
        Assert.Contains(everydayGood, profile.Everyday.Keys);
        Assert.Contains(luxuryGood, profile.Luxury.Keys);
        Assert.All(profile.Life.Values.Concat(profile.Everyday.Values).Concat(profile.Luxury.Values), amount => Assert.True(amount > 0m));
    }

    [Fact]
    public void ApplyScenarioOverrides_UsesDefaultsForOmittedCategories()
    {
        var profile = PopNeedProfileCatalog.ApplyScenarioOverrides(
            "farmers",
            new Dictionary<string, decimal> { ["fish"] = 0.2m },
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>());

        Assert.Equal(0.2m, profile.Life["fish"]);
        Assert.DoesNotContain("grain", profile.Life.Keys);
        Assert.Contains("tools", profile.Everyday.Keys);
        Assert.Contains("luxury_clothes", profile.Luxury.Keys);
    }

    [Fact]
    public void JsonContentLoader_AssignsDefaultNeedsWhenScenarioOmitsNeeds()
    {
        var scenarioPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(scenarioPath, """
            {
              "seed": 71,
              "startDate": "1836-01-01",
              "countries": [
                {
                  "id": "albion",
                  "displayName": "Albion",
                  "provinceIds": ["northshire"],
                  "treasury": 1000,
                  "taxRate": 0.2,
                  "tariffRate": 0,
                  "isPlayable": true
                }
              ],
              "provinces": [
                {
                  "id": "northshire",
                  "displayName": "Northshire",
                  "ownerId": "albion",
                  "populationIds": ["northshire-farmers"]
                }
              ],
              "pops": [
                {
                  "id": "northshire-farmers",
                  "provinceId": "northshire",
                  "popClass": "farmers",
                  "size": 1000,
                  "cashReserve": 5,
                  "militancy": 0,
                  "consciousness": 0,
                  "literacy": 0.2
                }
              ]
            }
            """);
            var goods = new[]
            {
                new GoodDefinition("grain", "Grain", 1m, "food"),
                new GoodDefinition("clothes", "Clothes", 3.2m, "consumer"),
                new GoodDefinition("liquor", "Liquor", 2.8m, "consumer"),
                new GoodDefinition("tools", "Tools", 4.5m, "industrial"),
                new GoodDefinition("luxury_clothes", "Luxury Clothes", 8m, "luxury")
            };

            var world = new JsonContentLoader().LoadScenario(scenarioPath, goods);
            var pop = world.Pops["northshire-farmers"];

            Assert.Contains("grain", pop.Needs.Life.Keys);
            Assert.Contains("tools", pop.Needs.Everyday.Keys);
            Assert.Contains("luxury_clothes", pop.Needs.Luxury.Keys);
        }
        finally
        {
            File.Delete(scenarioPath);
        }
    }

    [Fact]
    public void MarketPricingStage_CountsLifeEverydayAndLuxuryNeedDemand()
    {
        var world = new WorldState
        {
            Seed = 71,
            Date = new GameDate(new DateOnly(1836, 1, 1)),
            Countries = new Dictionary<string, CountryState>
            {
                ["albion"] = new()
                {
                    Id = "albion",
                    DisplayName = "Albion",
                    ProvinceIds = ["northshire"],
                    Treasury = 1000m,
                    TaxRate = 0.2m,
                    TariffRate = 0m,
                    IsPlayable = true
                }
            },
            Provinces = new Dictionary<string, ProvinceState>
            {
                ["northshire"] = new()
                {
                    Id = "northshire",
                    DisplayName = "Northshire",
                    OwnerId = "albion",
                    PopulationIds = ["farmers"],
                    Stockpile = { ["grain"] = 10m, ["tools"] = 10m, ["luxury_clothes"] = 10m }
                }
            },
            Pops = new Dictionary<string, PopState>
            {
                ["farmers"] = new()
                {
                    Id = "farmers",
                    ProvinceId = "northshire",
                    PopClass = "farmers",
                    Size = 2_000,
                    CashReserve = 5m,
                    Literacy = 0.2m,
                    Militancy = 0m,
                    Consciousness = 0m,
                    Needs = new PopNeedProfile
                    {
                        Life = { ["grain"] = 0.5m },
                        Everyday = { ["tools"] = 0.25m },
                        Luxury = { ["luxury_clothes"] = 0.1m }
                    }
                }
            },
            Goods = new Dictionary<string, GoodDefinition>
            {
                ["grain"] = new("grain", "Grain", 1m, "food"),
                ["tools"] = new("tools", "Tools", 4.5m, "industrial"),
                ["luxury_clothes"] = new("luxury_clothes", "Luxury Clothes", 8m, "luxury")
            },
            Market = new MarketState
            {
                Prices =
                {
                    ["grain"] = 1m,
                    ["tools"] = 4.5m,
                    ["luxury_clothes"] = 8m
                }
            }
        };

        new MarketPricingStage().Execute(new SimulationContext
        {
            World = world,
            Random = new SeededRandom(71),
            Log = new SimulationLog(),
            Profile = new TickProfile()
        });

        Assert.Equal(1m, world.Market.DemandLastTick["grain"]);
        Assert.Equal(0.5m, world.Market.DemandLastTick["tools"]);
        Assert.Equal(0.2m, world.Market.DemandLastTick["luxury_clothes"]);
    }
}
