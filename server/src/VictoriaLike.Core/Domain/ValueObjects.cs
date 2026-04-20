namespace VictoriaLike.Core.Domain;

/// Strongly-typed IDs to prevent mixing up identifiers.

public readonly struct CountryId : IEquatable<CountryId>
{
    public Guid Value { get; }

    public CountryId(Guid value) => Value = value;
    public static CountryId New() => new(Guid.NewGuid());
    public static CountryId Parse(string value) => new(Guid.Parse(value));

    public bool Equals(CountryId other) => Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is CountryId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value.ToString();
}

public readonly struct ProvinceId : IEquatable<ProvinceId>
{
    public Guid Value { get; }

    public ProvinceId(Guid value) => Value = value;
    public static ProvinceId New() => new(Guid.NewGuid());
    public static ProvinceId Parse(string value) => new(Guid.Parse(value));

    public bool Equals(ProvinceId other) => Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is ProvinceId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value.ToString();
}

public readonly struct MarketId : IEquatable<MarketId>
{
    public Guid Value { get; }

    public MarketId(Guid value) => Value = value;
    public static MarketId New() => new(Guid.NewGuid());
    public static MarketId Parse(string value) => new(Guid.Parse(value));

    public bool Equals(MarketId other) => Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is MarketId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value.ToString();
}

public readonly struct ActorId : IEquatable<ActorId>
{
    public Guid Value { get; }

    public ActorId(Guid value) => Value = value;
    public static ActorId New() => new(Guid.NewGuid());
    public static ActorId Parse(string value) => new(Guid.Parse(value));

    public bool Equals(ActorId other) => Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is ActorId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value.ToString();
}

public readonly struct CommandId : IEquatable<CommandId>
{
    public Guid Value { get; }

    public CommandId(Guid value) => Value = value;
    public static CommandId New() => new(Guid.NewGuid());
    public static CommandId Parse(string value) => new(Guid.Parse(value));

    public bool Equals(CommandId other) => Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is CommandId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value.ToString();
}
