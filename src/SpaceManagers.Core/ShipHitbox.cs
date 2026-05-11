using System.Numerics;

namespace SpaceManagers.Core;

public readonly record struct ShipHitbox(Vector2 LocalCenter, Vector2 Size)
{
    public static ShipHitbox Default { get; } = new(Vector2.Zero, new Vector2(84f, 84f));

    public float HalfWidth => MathF.Max(0f, Size.X) * 0.5f;
    public float HalfHeight => MathF.Max(0f, Size.Y) * 0.5f;
    public float BoundingRadius => LocalCenter.Length() + MathF.Sqrt(HalfWidth * HalfWidth + HalfHeight * HalfHeight);
    public float ForwardExtent => MathF.Max(0f, HalfHeight - LocalCenter.Y);

    public Vector2 WorldCenter(Vector2 shipPosition, float shipRotation)
    {
        return shipPosition + Rotate(LocalCenter, shipRotation);
    }

    public bool ContainsWorldPoint(Vector2 shipPosition, float shipRotation, Vector2 point)
    {
        var local = WorldPointToLocal(shipPosition, shipRotation, point);
        return MathF.Abs(local.X) <= HalfWidth && MathF.Abs(local.Y) <= HalfHeight;
    }

    public bool TryIntersectWorldSegment(Vector2 shipPosition, float shipRotation, Vector2 start, Vector2 end, out Vector2 impactPoint)
    {
        var localStart = WorldPointToLocal(shipPosition, shipRotation, start);
        var localEnd = WorldPointToLocal(shipPosition, shipRotation, end);
        var direction = localEnd - localStart;

        var minT = 0f;
        var maxT = 1f;
        var hit = ClipAxis(localStart.X, direction.X, -HalfWidth, HalfWidth, ref minT, ref maxT)
            && ClipAxis(localStart.Y, direction.Y, -HalfHeight, HalfHeight, ref minT, ref maxT);
        if (!hit)
        {
            impactPoint = Vector2.Zero;
            return false;
        }

        var localImpact = localStart + direction * Math.Clamp(minT, 0f, 1f);
        impactPoint = shipPosition + Rotate(localImpact + LocalCenter, shipRotation);
        return true;
    }

    public bool IntersectsWorldSegment(Vector2 shipPosition, float shipRotation, Vector2 start, Vector2 end)
    {
        var localStart = WorldPointToLocal(shipPosition, shipRotation, start);
        var localEnd = WorldPointToLocal(shipPosition, shipRotation, end);
        var direction = localEnd - localStart;

        var minT = 0f;
        var maxT = 1f;
        return ClipAxis(localStart.X, direction.X, -HalfWidth, HalfWidth, ref minT, ref maxT)
            && ClipAxis(localStart.Y, direction.Y, -HalfHeight, HalfHeight, ref minT, ref maxT);
    }

    private Vector2 WorldPointToLocal(Vector2 shipPosition, float shipRotation, Vector2 point)
    {
        return Rotate(point - shipPosition, -shipRotation) - LocalCenter;
    }

    private static bool ClipAxis(float start, float direction, float min, float max, ref float minT, ref float maxT)
    {
        if (MathF.Abs(direction) < 0.00001f)
        {
            return start >= min && start <= max;
        }

        var t1 = (min - start) / direction;
        var t2 = (max - start) / direction;
        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
        }

        minT = MathF.Max(minT, t1);
        maxT = MathF.Min(maxT, t2);
        return minT <= maxT;
    }

    private static Vector2 Rotate(Vector2 value, float radians)
    {
        var sin = MathF.Sin(radians);
        var cos = MathF.Cos(radians);
        return new Vector2(
            value.X * cos - value.Y * sin,
            value.X * sin + value.Y * cos);
    }
}
