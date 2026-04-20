using VictoriaLike.Core.Application.Logging;
using VictoriaLike.Core.Application.Profiling;
using VictoriaLike.Core.Core.Common;
using VictoriaLike.Core.Core.Countries;
using VictoriaLike.Core.Core.Pops;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Simulation.Systems;
using VictoriaLike.Core.Simulation.TickPipeline;
using Xunit;

namespace VictoriaLike.Core.Tests;

public sealed class BudgetStageTests
{
    [Fact]
    public void Execute_SpendingAffectsTreasuryAndFundedPops()
    {
        var world = CreateWorld();
        var country = world.Countries["albion"];
        var clergy = world.Pops["clergy"];
        var soldiers = world.Pops["soldiers"];
        var bureaucrats = world.Pops["bureaucrats"];
        var treasuryBefore = country.Treasury;

        new BudgetStage().Execute(CreateContext(world));

        Assert.True(country.Treasury < treasuryBefore);
        Assert.True(clergy.CashReserve > 1m);
        Assert.True(clergy.Literacy > 0.5m);
        Assert.True(soldiers.CashReserve > 1m);
        Assert.True(soldiers.Militancy < 1m);
        Assert.True(bureaucrats.CashReserve > 1m);
        Assert.True(bureaucrats.Consciousness > 0.2m);
        Assert.True(world.Metrics.TreasuryDeltaByCountry["albion"] < 0m);
    }

    private static SimulationContext CreateContext(WorldState world) =>
        new()
        {
            World = world,
            Random = new SeededRandom(74),
            Log = new SimulationLog(),
            Profile = new TickProfile()
        };

    private static WorldState CreateWorld() =>
        new()
        {
            Seed = 74,
            Date = new GameDate(new DateOnly(1836, 1, 8)),
            Countries = new Dictionary<string, CountryState>
            {
                ["albion"] = new()
                {
                    Id = "albion",
                    DisplayName = "Albion",
                    ProvinceIds = ["capital"],
                    Treasury = 1_000m,
                    TaxRate = 0.2m,
                    TariffRate = 0m,
                    EducationSpending = 1m,
                    MilitarySpending = 1m,
                    AdministrationSpending = 1m,
                    IsPlayable = true
                }
            },
            Provinces = new Dictionary<string, ProvinceState>
            {
                ["capital"] = new()
                {
                    Id = "capital",
                    DisplayName = "Capital",
                    OwnerId = "albion",
                    PopulationIds = ["clergy", "soldiers", "bureaucrats"]
                }
            },
            Pops = new Dictionary<string, PopState>
            {
                ["clergy"] = CreatePop("clergy", "clergy", literacy: 0.5m, militancy: 0.1m, consciousness: 0.2m),
                ["soldiers"] = CreatePop("soldiers", "soldiers", literacy: 0.2m, militancy: 1m, consciousness: 0.1m),
                ["bureaucrats"] = CreatePop("bureaucrats", "bureaucrats", literacy: 0.4m, militancy: 0.1m, consciousness: 0.2m)
            }
        };

    private static PopState CreatePop(
        string id,
        string popClass,
        decimal literacy,
        decimal militancy,
        decimal consciousness) =>
        new()
        {
            Id = id,
            ProvinceId = "capital",
            PopClass = popClass,
            Size = 1_000,
            CashReserve = 1m,
            Literacy = literacy,
            Militancy = militancy,
            Consciousness = consciousness,
            Needs = new PopNeedProfile()
        };
}
