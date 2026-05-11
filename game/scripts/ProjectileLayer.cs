using Godot;
using SpaceRangers.Core;

namespace SpaceRangersPrototype;

public partial class ProjectileLayer : Node2D
{
    public IReadOnlyList<ProjectileState> Projectiles { get; set; } = Array.Empty<ProjectileState>();
    public bool UseCulling { get; set; }
    public Rect2 VisibleWorldRect { get; set; }

    public override void _Ready()
    {
        ZIndex = 20;
    }

    public override void _Draw()
    {
        foreach (var projectile in Projectiles)
        {
            var position = projectile.Position.ToGodot();
            if (UseCulling && !VisibleWorldRect.HasPoint(position))
            {
                continue;
            }

            var direction = projectile.Velocity.ToGodot().Normalized();
            var hot = new Color(0.85f, 1f, 0.9f, 1f);
            var glow = new Color(0.1f, 0.95f, 1f, 0.45f);
            DrawLine(position - direction * 20f, position, glow, 5.5f, true);
            DrawLine(position - direction * 13f, position + direction * 5f, hot, 1.6f, true);
            DrawCircle(position, 1.9f, hot);
        }
    }
}
