using VictoriaLike.Core.Application.Logging;
using VictoriaLike.Core.Application.Profiling;
using VictoriaLike.Core.Core.Common;
using VictoriaLike.Core.Core.World;

namespace VictoriaLike.Core.Simulation.TickPipeline;

public sealed class SimulationContext
{
    public required WorldState World { get; init; }
    public required SeededRandom Random { get; init; }
    public required SimulationLog Log { get; init; }
    public required TickProfile Profile { get; init; }
}
