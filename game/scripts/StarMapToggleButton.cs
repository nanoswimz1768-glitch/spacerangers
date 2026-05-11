using Godot;

namespace SpaceManagersPrototype;

public partial class StarMapToggleButton : Control
{
    private readonly Vector2[] _diamond = new Vector2[4];
    private bool _hovered;

    public event Action? ToggleRequested;

    public bool Active { get; set; }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(42f, 42f);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 20;
        TooltipText = "Star map";
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            ToggleRequested?.Invoke();
            AcceptEvent();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseEnter)
        {
            _hovered = true;
            QueueRedraw();
        }
        else if (what == NotificationMouseExit)
        {
            _hovered = false;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var size = Size;
        var center = size * 0.5f;
        var radius = MathF.Min(size.X, size.Y) * 0.42f;
        var glow = Active ? 0.44f : _hovered ? 0.32f : 0.20f;
        var line = Active
            ? new Color(0.72f, 1f, 0.94f, 0.96f)
            : new Color(0.18f, 0.88f, 1f, 0.84f);

        DrawCircle(center, radius + 5f, new Color(0.02f, 0.64f, 0.88f, glow * 0.32f));
        DrawCircle(center, radius, new Color(0f, 0.14f, 0.20f, 0.88f));
        DrawArc(center, radius, 0f, MathF.Tau, 48, new Color(0.14f, 0.86f, 1f, 0.78f), 1.6f, true);
        DrawArc(center, radius - 5f, -0.45f, MathF.Tau * 0.64f, 32, new Color(0.30f, 1f, 0.88f, 0.35f + glow), 1.1f, true);

        _diamond[0] = center + new Vector2(0f, -radius * 0.48f);
        _diamond[1] = center + new Vector2(radius * 0.46f, 0f);
        _diamond[2] = center + new Vector2(0f, radius * 0.48f);
        _diamond[3] = center + new Vector2(-radius * 0.46f, 0f);
        DrawPolyline(_diamond, new Color(0f, 0.04f, 0.06f, 0.92f), 5.2f, true);
        DrawPolyline(_diamond, line, 2f, true);
        DrawLine(_diamond[0], _diamond[2], WithAlpha(line, 0.65f), 1.0f, true);
        DrawLine(_diamond[1], _diamond[3], WithAlpha(line, 0.55f), 1.0f, true);
        DrawCircle(center, 2.8f, new Color(1f, 0.92f, 0.46f, 0.96f));
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, Math.Clamp(alpha, 0f, 1f));
    }
}
