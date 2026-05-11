using System.Numerics;

namespace SpaceManagers.Core;

public static class SimulationMath
{
    public static Vector2 ForwardFromRotation(float rotation) => new(MathF.Sin(rotation), -MathF.Cos(rotation));

    public static Vector2 RightFromRotation(float rotation) => new(MathF.Cos(rotation), MathF.Sin(rotation));

    public static Vector2 SafeNormalize(Vector2 value, Vector2 fallback)
    {
        if (value.LengthSquared() < 0.0001f)
        {
            return fallback;
        }

        return Vector2.Normalize(value);
    }

    public static Vector2 ClampLength(Vector2 value, float maxLength)
    {
        var lengthSquared = value.LengthSquared();
        var maxSquared = maxLength * maxLength;
        if (lengthSquared <= maxSquared || lengthSquared <= 0f)
        {
            return value;
        }

        return value * (maxLength / MathF.Sqrt(lengthSquared));
    }

    public static float Approach(float value, float target, float delta)
    {
        if (value < target)
        {
            return MathF.Min(value + delta, target);
        }

        return MathF.Max(value - delta, target);
    }
}

