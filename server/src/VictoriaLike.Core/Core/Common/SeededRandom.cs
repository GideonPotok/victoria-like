namespace VictoriaLike.Core.Core.Common;

public sealed class SeededRandom
{
    private uint _state;

    public SeededRandom(int seed)
    {
        _state = unchecked((uint)seed);
    }

    public decimal NextDecimal(decimal min, decimal max)
    {
        _state = 1664525u * _state + 1013904223u;
        var sample = _state / (decimal)uint.MaxValue;
        return min + ((max - min) * sample);
    }
}
