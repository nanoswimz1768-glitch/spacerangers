using Godot;
using SpaceManagers.Core;

namespace SpaceManagersPrototype;

public partial class TargetLockLayer : Node2D
{
    private ShipState _target;
    private Color _color = new(0.78f, 1f, 1f, 1f);
    private bool _hasTarget;
    private bool _hostile;
    private float _time;

    public override void _Ready()
    {
        ZIndex = 27;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (!_hasTarget)
        {
            return;
        }

        _time += Math.Max(0f, (float)delta);
        QueueRedraw();
    }

    public void SetTarget(ShipState target, bool hostile, Color color)
    {
        _target = target;
        _hostile = hostile;
        _color = color;
        _hasTarget = !target.IsDestroyed;
        Visible = _hasTarget;
        QueueRedraw();
    }

    public void ClearTarget()
    {
        _hasTarget = false;
        Visible = false;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_hasTarget || _target.IsDestroyed)
        {
            return;
        }

        var center = _target.Hitbox.WorldCenter(_target.Position, _target.Rotation).ToGodot();
        var radius = Math.Clamp(_target.Hitbox.BoundingRadius + 18f, 44f, 150f);
        var pulse = 0.5f + 0.5f * MathF.Sin(_time * 4.8f);
        var spin = _time * (_hostile ? 0.62f : 0.42f);
        var line = WithAlpha(_color, _hostile ? 0.92f : 0.82f);
        var glow = WithAlpha(_color, 0.12f + pulse * 0.07f);
        var faint = WithAlpha(_color, 0.22f + pulse * 0.08f);

        DrawCircle(center, radius + 12f + pulse * 4f, glow);
        DrawCircle(center, radius + 4f, WithAlpha(_color, 0.045f));

        for (var segment = 0; segment < 4; segment++)
        {
            var start = segment * MathF.PI * 0.5f + 0.22f + spin;
            DrawArc(center, radius, start, start + 0.72f, 18, line, 2.3f, true);
            DrawArc(center, radius * 0.76f, start + 0.18f, start + 0.50f, 10, faint, 1.15f, true);
        }

        DrawTargetTicks(center, radius, spin, line);

        if (_hostile)
        {
            DrawArc(center, radius + 8f, -0.36f - spin * 0.55f, 0.36f - spin * 0.55f, 12, WithAlpha(_color, 0.70f), 1.8f, true);
            DrawArc(center, radius + 8f, MathF.PI - 0.36f - spin * 0.55f, MathF.PI + 0.36f - spin * 0.55f, 12, WithAlpha(_color, 0.70f), 1.8f, true);
        }
    }

    private void DrawTargetTicks(Vector2 center, float radius, float spin, Color color)
    {
        for (var index = 0; index < 4; index++)
        {
            var angle = index * MathF.PI * 0.5f + spin * 0.32f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var tangent = new Vector2(-direction.Y, direction.X);
            var outer = center + direction * (radius + 11f);
            var inner = center + direction * (radius - 6f);
            DrawLine(inner, outer, color, 1.85f, true);
            DrawLine(outer - tangent * 6f, outer + tangent * 6f, WithAlpha(color, 0.72f), 1.45f, true);
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, Math.Clamp(alpha, 0f, 1f));
    }
}
