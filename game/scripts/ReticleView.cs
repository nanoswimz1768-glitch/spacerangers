using Godot;
using SpaceManagers.Core;

namespace SpaceManagersPrototype;

public partial class ReticleView : Node2D
{
    private float _phase;

    public ShipMode Mode { get; set; } = ShipMode.Navigation;

    public override void _Ready()
    {
        ZIndex = 40;
    }

    public override void _Process(double delta)
    {
        _phase += (float)delta * 4.5f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Mode == ShipMode.Combat)
        {
            DrawCombatReticle();
            return;
        }

        DrawNavigationReticle();
    }

    private void DrawNavigationReticle()
    {
        var pulse = 1f + MathF.Sin(_phase) * 0.08f;
        var color = new Color(0.36f, 0.96f, 1f, 0.62f);
        var soft = new Color(0.34f, 0.86f, 1f, 0.18f);
        DrawArc(Vector2.Zero, 22f * pulse, MathF.PI * 0.15f, MathF.PI * 0.85f, 36, color, 1.2f, true);
        DrawArc(Vector2.Zero, 22f * pulse, MathF.PI * 1.15f, MathF.PI * 1.85f, 36, color, 1.2f, true);
        DrawArc(Vector2.Zero, 10f, 0f, MathF.Tau, 48, soft, 1.1f, true);
        DrawLine(new Vector2(-28f, 0f), new Vector2(-17f, 0f), color, 1.0f, true);
        DrawLine(new Vector2(17f, 0f), new Vector2(28f, 0f), color, 1.0f, true);
        DrawCircle(Vector2.Zero, 1.6f, new Color(0.78f, 1f, 0.96f, 0.72f));
    }

    private void DrawCombatReticle()
    {
        var pulse = 1f + MathF.Sin(_phase * 1.6f) * 0.07f;
        var color = new Color(1f, 0.42f, 0.22f, 0.96f);
        var hot = new Color(1f, 0.88f, 0.38f, 0.92f);
        DrawArc(Vector2.Zero, 18f * pulse, 0f, MathF.Tau, 64, color, 1.7f, true);
        DrawArc(Vector2.Zero, 28f, -0.55f, 0.55f, 24, hot, 1.5f, true);
        DrawArc(Vector2.Zero, 28f, MathF.PI - 0.55f, MathF.PI + 0.55f, 24, hot, 1.5f, true);
        DrawLine(new Vector2(-36f, 0f), new Vector2(-12f, 0f), color, 1.7f, true);
        DrawLine(new Vector2(12f, 0f), new Vector2(36f, 0f), color, 1.7f, true);
        DrawLine(new Vector2(0f, -36f), new Vector2(0f, -12f), color, 1.7f, true);
        DrawLine(new Vector2(0f, 12f), new Vector2(0f, 36f), color, 1.7f, true);
        DrawLine(new Vector2(-7f, -7f), new Vector2(7f, 7f), hot, 1.2f, true);
        DrawLine(new Vector2(7f, -7f), new Vector2(-7f, 7f), hot, 1.2f, true);
        DrawCircle(Vector2.Zero, 2.4f, hot);
    }
}
