namespace VictoriaLike.Core.Data.Loaders;

public sealed class BalanceCsvLoader
{
    public IReadOnlyDictionary<string, decimal> LoadKeyValueTable(string path)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(path).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = line.Split(',', StringSplitOptions.TrimEntries);
            if (columns.Length < 2)
            {
                continue;
            }

            result[columns[0]] = decimal.Parse(columns[1]);
        }

        return result;
    }
}
