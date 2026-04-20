namespace VictoriaLike.Core.Core.World;

public readonly record struct GameDate(DateOnly Value)
{
    public GameDate AdvanceWeeks(int weeks = 1) => new(Value.AddDays(weeks * 7));

    public override string ToString() => Value.ToString("yyyy-MM-dd");
}
