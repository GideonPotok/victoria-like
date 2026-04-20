using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using VictoriaLike.Core.Core.Economy;

namespace VictoriaLike.Server.Services;

public interface IGoodsService
{
    IReadOnlyList<GoodDefinition> All { get; }
}

public sealed class GoodsService : IGoodsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IReadOnlyList<GoodDefinition> _goods;

    public GoodsService(IConfiguration configuration)
    {
        var path = configuration.GetValue<string>("Content:GoodsPath")
            ?? Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "content", "goods.json"));

        if (!File.Exists(path))
            throw new FileNotFoundException($"Goods file not found at: {path}");

        var json = File.ReadAllText(path);
        _goods = JsonSerializer.Deserialize<List<GoodDefinition>>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse goods.json");
    }

    public IReadOnlyList<GoodDefinition> All => _goods;
}
