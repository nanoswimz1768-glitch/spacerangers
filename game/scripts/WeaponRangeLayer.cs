using Godot;
using SpaceManagers.Core;

namespace SpaceManagersPrototype;

public partial class WeaponRangeLayer : Node2D
{
    private const int CircleSegments = 144;
    private const int ConeSegments = 48;
    private readonly Vector2[] _circlePoints = new Vector2[CircleSegments + 1];
    private readonly Vector2[] _coneFillPoints = new Vector2[ConeSegments + 2];
    private readonly Vector2[] _coneArcPoints = new Vector2[ConeSegments + 1];
    private ShipState _ship;
    private WeaponDefinition _weapon = WeaponCatalog.Default;
    private bool _hasState;

    public override void _Ready()
    {
        ZIndex = 12;
    }

    public void SetState(ShipState ship, WeaponDefinition weapon, bool visible)
    {
        _ship = ship;
        _weapon = weapon;
        _hasState = visible;
        Visible = visible;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_hasState || _ship.IsDestroyed)
        {
            return;
        }

        var range = _weapon.EffectiveRange;
        if (range <= 1f)
        {
            return;
        }

        var center = _ship.Position.ToGodot();
        var pulse = 0.5f + 0.5f * MathF.Sin((float)Time.GetTicksMsec() * 0.0024f);
        var ring = new Color(0.36f, 1f, 0.96f, 0.16f + pulse * 0.04f);
        var ringDim = new Color(0.20f, 0.72f, 0.98f, 0.055f);
        var cone = new Color(0.30f, 1f, 0.92f, 0.035f);
        var coneEdge = new Color(0.52f, 1f, 0.92f, 0.16f);

        BuildCircle(center, range);
        DrawPolyline(_circlePoints, ringDim, 3.2f, true);
        DrawPolyline(_circlePoints, ring, 1.15f, true);

        if (_weapon.FireMode != WeaponFireMode.Manual)
        {
            return;
        }

        var halfCone = MathF.Max(0f, _weapon.ManualConeDegrees) * MathF.PI / 360f;
        BuildCone(center, range, _ship.Rotation, halfCone);
        DrawColoredPolygon(_coneFillPoints, cone);
        DrawPolyline(_coneArcPoints, coneEdge, 1.45f, true);
        DrawLine(center, _coneFillPoints[1], coneEdge, 1.1f, true);
        DrawLine(center, _coneFillPoints[^1], coneEdge, 1.1f, true);
    }

    private void BuildCircle(Vector2 center, float radius)
    {
        for (var index = 0; index <= CircleSegments; index++)
        {
            var angle = MathF.Tau * index / CircleSegments;
            _circlePoints[index] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }
    }

    private void BuildCone(Vector2 center, float radius, float rotation, float halfCone)
    {
        _coneFillPoints[0] = center;
        for (var index = 0; index <= ConeSegments; index++)
        {
            var t = ConeSegments <= 0 ? 0f : index / (float)ConeSegments;
            var angle = -halfCone + halfCone * 2f * t;
            var point = center + Vector2.Up.Rotated(rotation + angle) * radius;
            _coneFillPoints[index + 1] = point;
            _coneArcPoints[index] = point;
        }
    }
}
