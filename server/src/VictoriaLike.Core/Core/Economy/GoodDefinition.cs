namespace VictoriaLike.Core.Core.Economy;

public sealed record GoodDefinition(
    string Id,
    string DisplayName,
    decimal BasePrice,
    string Category);
