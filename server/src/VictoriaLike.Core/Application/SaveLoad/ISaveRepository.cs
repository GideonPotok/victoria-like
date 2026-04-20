using VictoriaLike.Core.Core.World;

namespace VictoriaLike.Core.Application.SaveLoad;

public interface ISaveRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task SaveSnapshotAsync(string slotName, WorldState world, CancellationToken cancellationToken = default);
}
