using System.Numerics;

namespace SpaceRangers.Core;

public static class AsteroidPhysics
{
    public const float ReferenceStarSize = 1000f;
    public const float SunVisualWorldSize = 1320f;

    public static float ReferenceDiameterToWorld(float referenceDiameter)
    {
        return SunVisualWorldSize * referenceDiameter / ReferenceStarSize;
    }

    public static float StarScale(SimulationConfig config)
    {
        return MathF.Max(0.1f, config.StarVisualWorldSize / SunVisualWorldSize);
    }

    public static float ShipSunBurnDamageRadius(SimulationConfig config)
    {
        return MathF.Max(0f, config.SunBurnDamageRadius) * StarScale(config);
    }

    public static float AsteroidSunBurnRadius(SimulationConfig config)
    {
        return MathF.Max(0f, config.AsteroidSunBurnRadius) * StarScale(config);
    }

    public static float AsteroidHeatRadius(SimulationConfig config)
    {
        var burnRadius = AsteroidSunBurnRadius(config);
        var heatRadius = MathF.Max(0f, config.AsteroidHeatRadius) * StarScale(config);
        return MathF.Max(heatRadius, burnRadius + 1f);
    }

    public static float AsteroidGravityMinDistance(SimulationConfig config)
    {
        return MathF.Max(0f, config.AsteroidGravityMinDistance) * StarScale(config);
    }

    public static Vector2 SolarGravity(Vector2 position, SimulationConfig config)
    {
        var toSun = -position;
        var distanceSquared = toSun.LengthSquared();
        if (distanceSquared <= 0.0001f || config.AsteroidSunGravity <= 0f)
        {
            return Vector2.Zero;
        }

        var distance = MathF.Sqrt(distanceSquared);
        var normalizedDistance = MathF.Max(AsteroidGravityMinDistance(config), distance) / 1000f;
        var acceleration = config.AsteroidSunGravity / (normalizedDistance * normalizedDistance);
        return toSun / distance * acceleration;
    }

    public static float HeatRatio(Vector2 position, SimulationConfig config)
    {
        var burnRadius = AsteroidSunBurnRadius(config);
        var heatRadius = AsteroidHeatRadius(config);
        if (heatRadius <= burnRadius)
        {
            return 0f;
        }

        var distance = position.Length();
        var ratio = (heatRadius - distance) / (heatRadius - burnRadius);
        return Math.Clamp(ratio, 0f, 1f);
    }

    public static bool IsInsideSunBurnZone(Vector2 position, float radius, SimulationConfig config)
    {
        return position.Length() <= AsteroidSunBurnRadius(config) + radius * 0.35f;
    }

    public static float ShipSunBurnDamagePerSecond(Vector2 position, float shipRadius, SimulationConfig config)
    {
        var maxDamage = MathF.Max(0f, config.SunBurnDamageMaxPerSecond);
        var damageRadius = ShipSunBurnDamageRadius(config);
        if (maxDamage <= 0f || damageRadius <= 0f)
        {
            return 0f;
        }

        var minDamage = Math.Clamp(config.SunBurnDamageMinPerSecond, 0f, maxDamage);
        var effectiveRadius = damageRadius + MathF.Max(0f, shipRadius) * 0.35f;
        if (effectiveRadius <= 0.001f)
        {
            return 0f;
        }

        var distance = position.Length();
        if (distance > effectiveRadius)
        {
            return 0f;
        }

        var proximity = 1f - Math.Clamp(distance / effectiveRadius, 0f, 1f);
        return minDamage + (maxDamage - minDamage) * proximity;
    }

    public static bool IsOutsideRemovalBounds(Vector2 position, float radius, SimulationConfig config)
    {
        var margin = config.AsteroidRemovalMargin + radius;
        return position.X < -config.Bounds.HalfWidth - margin
            || position.X > config.Bounds.HalfWidth + margin
            || position.Y < -config.Bounds.HalfHeight - margin
            || position.Y > config.Bounds.HalfHeight + margin;
    }
}
