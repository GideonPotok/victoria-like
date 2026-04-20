namespace VictoriaLike.Core.Application.Logging;

public sealed class SimulationLog
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries => _entries;

    public void Info(string message)
    {
        _entries.Add(message);
    }

    public void LogCommandSuccess(string commandId, string commandType)
    {
        _entries.Add($"[Command] {commandType} ({commandId}) applied successfully");
    }

    public void LogCommandFailure(string commandId, string reason)
    {
        _entries.Add($"[Command] {commandId} failed: {reason}");
    }
}
