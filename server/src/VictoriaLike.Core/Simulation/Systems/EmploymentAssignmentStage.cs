using VictoriaLike.Core.Core.Buildings;
using VictoriaLike.Core.Core.Pops;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Simulation.TickPipeline;

namespace VictoriaLike.Core.Simulation.Systems;

public sealed class EmploymentAssignmentStage : ISimulationStage
{
    private const int RgoBaseCapacity = 4_000;
    private const int CraftsmenJobsPerFactoryLevel = 1_000;
    private const int ClerkJobsPerFactoryLevel = 250;

    public string Name => "employment-assignment";

    public void Execute(SimulationContext context)
    {
        foreach (var pop in context.World.Pops.Values)
        {
            pop.EmployedCount = 0;
            pop.UnemployedCount = Math.Max(0, pop.Size);
        }

        foreach (var factory in context.World.Factories.Values)
        {
            factory.EmployedCraftsmen = 0;
            factory.EmployedClerks = 0;
        }

        AssignRgoWorkers(context);
        AssignFactoryWorkers(context);
        AssignSelfAndStateEmployedPops(context);
    }

    private static void AssignRgoWorkers(SimulationContext context)
    {
        foreach (var province in context.World.Provinces.Values.OrderBy(province => province.Id, StringComparer.Ordinal))
        {
            var workerType = RgoWorkerType(province.RgoType);
            var capacity = RgoCapacity(province);
            AssignPopGroupJobs(context, province.PopulationIds, workerType, capacity);
        }
    }

    private static void AssignFactoryWorkers(SimulationContext context)
    {
        foreach (var factory in context.World.Factories.Values.OrderBy(factory => factory.Id, StringComparer.Ordinal))
        {
            var candidates = CandidatePopulationIds(context, factory).ToList();

            factory.EmployedCraftsmen = AssignPopGroupJobs(
                context,
                candidates,
                "craftsmen",
                Math.Max(1, factory.Level) * CraftsmenJobsPerFactoryLevel);

            factory.EmployedClerks = AssignPopGroupJobs(
                context,
                candidates,
                "clerks",
                Math.Max(1, factory.Level) * ClerkJobsPerFactoryLevel);
        }
    }

    private static IEnumerable<string> CandidatePopulationIds(SimulationContext context, FactoryState factory)
    {
        if (!string.IsNullOrWhiteSpace(factory.ProvinceId) &&
            context.World.Provinces.TryGetValue(factory.ProvinceId, out var province))
        {
            return province.PopulationIds;
        }

        return context.World.Provinces.Values
            .Where(province => string.Equals(province.OwnerId, factory.CountryId, StringComparison.Ordinal))
            .OrderBy(province => province.Id, StringComparer.Ordinal)
            .SelectMany(province => province.PopulationIds);
    }

    private static void AssignSelfAndStateEmployedPops(SimulationContext context)
    {
        foreach (var pop in context.World.Pops.Values.OrderBy(pop => pop.Id, StringComparer.Ordinal))
        {
            if (!IsSelfOrStateEmployed(pop.PopClass))
            {
                continue;
            }

            var available = Math.Max(0, pop.Size - pop.EmployedCount);
            pop.EmployedCount += available;
            pop.UnemployedCount = Math.Max(0, pop.Size - pop.EmployedCount);
        }
    }

    private static int AssignPopGroupJobs(
        SimulationContext context,
        IEnumerable<string> populationIds,
        string popClass,
        int capacity)
    {
        var remaining = Math.Max(0, capacity);
        var assigned = 0;

        foreach (var popId in populationIds.Order(StringComparer.Ordinal))
        {
            if (remaining <= 0 || !context.World.Pops.TryGetValue(popId, out var pop))
            {
                continue;
            }

            if (!string.Equals(pop.PopClass, popClass, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var available = Math.Max(0, pop.Size - pop.EmployedCount);
            var hired = Math.Min(available, remaining);
            pop.EmployedCount += hired;
            pop.UnemployedCount = Math.Max(0, pop.Size - pop.EmployedCount);
            remaining -= hired;
            assigned += hired;
        }

        return assigned;
    }

    private static int RgoCapacity(ProvinceState province)
    {
        var infrastructureModifier = 1m + Math.Max(0m, province.Infrastructure) * 0.25m;
        return Math.Max(0, (int)Math.Floor(RgoBaseCapacity * infrastructureModifier));
    }

    private static string RgoWorkerType(string rgoType) =>
        rgoType.Contains("farm", StringComparison.OrdinalIgnoreCase) ? "farmers" : "laborers";

    private static bool IsSelfOrStateEmployed(string popClass)
    {
        var normalized = popClass.Trim().ToLowerInvariant();
        return normalized is "artisans" or "soldiers" or "clergy" or "bureaucrats" or "aristocrats" or "capitalists";
    }
}
