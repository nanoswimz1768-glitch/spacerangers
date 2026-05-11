using Godot;
using SpaceRangers.Core;

namespace SpaceRangersPrototype;

public partial class DebugHitboxLayer : Node2D
{
    public bool ShowHitboxes { get; set; }
    public SimulationConfig Config { get; set; } = new();
    public IReadOnlyList<AsteroidState> Asteroids { get; set; } = Array.Empty<AsteroidState>();
    public bool UseCulling { get; set; }
    public Rect2 VisibleWorldRect { get; set; }

    public override void _Ready()
    {
        ZIndex = 80;
    }

    public override void _Draw()
    {
        if (!ShowHitboxes)
        {
            return;
        }

        DrawSunHitbox();
        DrawAsteroidHitboxes();
    }

    private void DrawSunHitbox()
    {
        var sunRadius = MathF.Max(1f, AsteroidPhysics.ShipSunBurnDamageRadius(Config));
        if (UseCulling && !CircleIntersectsRect(Vector2.Zero, sunRadius, VisibleWorldRect))
        {
            return;
        }

        DrawCircle(Vector2.Zero, sunRadius, new Color(1f, 0.12f, 0.02f, 0.032f));
        DrawArc(Vector2.Zero, sunRadius, 0f, MathF.Tau, 192, new Color(1f, 0.22f, 0.04f, 0.96f), 2.8f, true);
        DrawLine(new Vector2(-18f, 0f), new Vector2(18f, 0f), new Color(1f, 0.78f, 0.18f, 0.9f), 1.4f, true);
        DrawLine(new Vector2(0f, -18f), new Vector2(0f, 18f), new Color(1f, 0.78f, 0.18f, 0.9f), 1.4f, true);

        var asteroidBurnRadius = AsteroidPhysics.AsteroidSunBurnRadius(Config);
        if (MathF.Abs(asteroidBurnRadius - sunRadius) > 1f)
        {
            DrawArc(Vector2.Zero, asteroidBurnRadius, 0f, MathF.Tau, 192, new Color(1f, 0.72f, 0.12f, 0.52f), 1.4f, true);
        }
    }

    private void DrawAsteroidHitboxes()
    {
        foreach (var asteroid in Asteroids)
        {
            var position = asteroid.Position.ToGodot();
            var radius = asteroid.Radius;
            if (UseCulling && !CircleIntersectsRect(position, radius, VisibleWorldRect))
            {
                continue;
            }

            DrawCircle(position, radius, new Color(1f, 0.78f, 0.10f, 0.045f));
            DrawArc(position, radius, 0f, MathF.Tau, 72, new Color(1f, 0.86f, 0.18f, 0.92f), 1.8f, true);
            var mark = Math.Clamp(radius * 0.12f, 5f, 14f);
            DrawLine(position + new Vector2(-mark, 0f), position + new Vector2(mark, 0f), new Color(1f, 0.95f, 0.45f, 0.8f), 1f, true);
            DrawLine(position + new Vector2(0f, -mark), position + new Vector2(0f, mark), new Color(1f, 0.95f, 0.45f, 0.8f), 1f, true);
        }
    }

    private static bool CircleIntersectsRect(Vector2 center, float radius, Rect2 rect)
    {
        var max = rect.Position + rect.Size;
        var nearestX = Math.Clamp(center.X, rect.Position.X, max.X);
        var nearestY = Math.Clamp(center.Y, rect.Position.Y, max.Y);
        return center.DistanceSquaredTo(new Vector2(nearestX, nearestY)) <= radius * radius;
    }
}
