using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VictoriaLike.Core.Application.Logging;
using VictoriaLike.Core.Application.Profiling;
using VictoriaLike.Core.Core.Common;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Simulation.TickPipeline;

namespace VictoriaLike.Core.Simulation;

public sealed class SimulationOrchestrator
{
    private readonly IReadOnlyList<ISimulationStage> _stages;

    public SimulationOrchestrator(IEnumerable<ISimulationStage> stages)
    {
        _stages = stages.ToList();
    }

    public void RunTick(SimulationContext context)
    {
        foreach (var stage in _stages)
        {
            var stopwatch = Stopwatch.StartNew();
            stage.Execute(context);
            stopwatch.Stop();
            context.Profile.StageDurations[stage.Name] = stopwatch.Elapsed;
        }
    }

    public void RunTick(WorldState world)
    {
        var context = new SimulationContext
        {
            World = world,
            Random = new SeededRandom(world.Seed + world.Date.Value.DayNumber),
            Log = new SimulationLog(),
            Profile = new TickProfile()
        };
        RunTick(context);
    }
}
