using System.Text.Json;

namespace VictoriaLike.Core.Application.Commands;

internal static class MilitaryCommandHelpers
{
    public static bool TryGetString(
        IReadOnlyDictionary<string, object> payload,
        string key,
        out string value)
    {
        value = string.Empty;
        if (!payload.TryGetValue(key, out var raw))
            return false;

        if (raw is JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.String)
                return false;

            value = element.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        value = raw?.ToString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    public static string NormalizeGuidString(string value) =>
        Guid.TryParse(value, out var guid) ? guid.ToString() : value;

    public static (string First, string Second) NormalizePair(string first, string second) =>
        string.CompareOrdinal(first, second) <= 0 ? (first, second) : (second, first);
}
