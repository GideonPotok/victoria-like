namespace VictoriaLike.Core.Core.Common;

public static class ScalarMath
{
    public static decimal Clamp(decimal value, decimal min, decimal max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}
