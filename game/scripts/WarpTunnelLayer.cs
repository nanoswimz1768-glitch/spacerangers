using Godot;

namespace SpaceManagersPrototype;

public partial class WarpTunnelLayer : Node2D
{
    private float _phase;
    private float _progress;
    private bool _arriving;
    private Color _outerColor = new(0.1f, 0.85f, 1f, 1f);
    private Color _coreColor = new(0.78f, 1f, 1f, 1f);

    public bool Active { get; private set; }

    public override void _Ready()
    {
        ZIndex = 60;
        Visible = false;
        var material = new CanvasItemMaterial
        {
            BlendMode = CanvasItemMaterial.BlendModeEnum.Add
        };
        Material = material;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (!Active)
        {
            return;
        }

        _phase += (float)delta;
        QueueRedraw();
    }

    public void Start(Color outerColor, Color coreColor, bool arriving)
    {
        _outerColor = Saturated(outerColor);
        _coreColor = Saturated(coreColor);
        _arriving = arriving;
        _progress = 0f;
        _phase = 0f;
        Active = true;
        Visible = true;
        QueueRedraw();
    }

    public void SetProgress(float progress)
    {
        _progress = Math.Clamp(progress, 0f, 1f);
        if (Active)
        {
            QueueRedraw();
        }
    }

    public void Stop()
    {
        Active = false;
        Visible = false;
        _progress = 0f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Active)
        {
            return;
        }

        var localCenter = new Vector2(0f, -520f);
        var open = SmoothStep(0f, 0.22f, _progress);
        var close = 1f - SmoothStep(0.76f, 1f, _progress);
        var intensity = Math.Clamp(open * close, 0f, 1f);
        if (_arriving)
        {
            intensity = Math.Clamp((0.35f + SmoothStep(0f, 0.28f, _progress) * 0.65f) * close, 0f, 1f);
        }

        DrawNebulaVortex(localCenter, intensity);
        DrawStreakField(localCenter, intensity);
        DrawTunnelRings(localCenter, intensity);
        DrawMouth(localCenter, intensity);
    }

    private void DrawStreakField(Vector2 center, float intensity)
    {
        var count = _arriving ? 168 : 142;
        var speed = _arriving ? -1.9f : 2.25f;
        for (var i = 0; i < count; i++)
        {
            var h0 = Hash01(i * 13.17f + 1.9f);
            var h1 = Hash01(i * 31.73f + 7.1f);
            var h2 = Hash01(i * 57.11f + 3.4f);
            var angle = h0 * MathF.Tau + MathF.Sin(_phase * 0.7f + h1 * 9f) * 0.03f;
            var depth = Fract(h1 + _phase * speed * (0.18f + h2 * 0.18f));
            var radius = 80f + depth * depth * 1900f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var tangent = new Vector2(-direction.Y, direction.X);
            var swirl = MathF.Sin(depth * 8.4f + _phase * 2.0f + h2 * 4f) * (22f + depth * 110f);
            var end = center + direction * radius + tangent * swirl;
            var length = 42f + depth * 220f;
            var start = end - direction * length * (_arriving ? -0.62f : 1f);
            var alpha = intensity * (0.08f + h2 * 0.24f) * (1f - SmoothStep(0.92f, 1f, depth));
            var width = 0.9f + h2 * 2.2f + depth * 1.1f;
            var color = Mix(_coreColor, _outerColor, 0.35f + h1 * 0.55f);
            DrawLine(start, end, WithAlpha(color, alpha), width, true);
            if (h2 > 0.72f)
            {
                DrawLine(
                    start.Lerp(end, 0.35f),
                    end,
                    WithAlpha(Colors.White, alpha * 0.45f),
                    Math.Max(0.6f, width * 0.34f),
                    true);
            }
        }
    }

    private void DrawTunnelRings(Vector2 center, float intensity)
    {
        for (var i = 0; i < 9; i++)
        {
            var t = Fract(i / 9f + _phase * (_arriving ? -0.28f : 0.34f));
            var radius = 70f + t * t * 720f;
            var alpha = intensity * (1f - t) * 0.26f;
            var rotation = _phase * 1.2f + i * 0.71f;
            var color = Mix(_outerColor, _coreColor, 0.25f + Hash01(i * 17.3f) * 0.36f);
            DrawArc(center, radius, rotation, rotation + MathF.Tau * 0.70f, 96, WithAlpha(color, alpha), 2.2f + t * 2.2f, true);
            DrawArc(center, radius * 0.82f, -rotation * 0.72f, -rotation * 0.72f + MathF.Tau * 0.28f, 64, WithAlpha(_coreColor, alpha * 0.7f), 1.2f, true);
        }
    }

    private void DrawNebulaVortex(Vector2 center, float intensity)
    {
        for (var i = 0; i < 18; i++)
        {
            var h = Hash01(i * 41.9f + 2.0f);
            var angle = i / 18f * MathF.Tau + _phase * (0.26f + h * 0.16f);
            var arm = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var radius = 110f + h * 660f;
            var width = 18f + h * 42f;
            var start = center + arm * (radius * 0.30f);
            var end = center + arm * radius + new Vector2(-arm.Y, arm.X) * (120f + h * 240f);
            DrawLine(start, end, WithAlpha(_outerColor, intensity * (0.018f + h * 0.045f)), width, true);
        }
    }

    private void DrawMouth(Vector2 center, float intensity)
    {
        var pulse = 0.5f + 0.5f * MathF.Sin(_phase * 7.6f);
        DrawCircle(center, 116f + pulse * 16f, WithAlpha(_outerColor, intensity * 0.13f));
        DrawCircle(center, 74f + pulse * 8f, WithAlpha(Mix(_outerColor, _coreColor, 0.54f), intensity * 0.22f));
        DrawCircle(center, 30f + pulse * 5f, WithAlpha(_coreColor, intensity * 0.55f));
        DrawCircle(center, 18f, WithAlpha(Colors.White, intensity * 0.34f));
    }

    private static Color Saturated(Color color)
    {
        var max = Math.Max(color.R, Math.Max(color.G, color.B));
        if (max <= 0.001f)
        {
            return new Color(0.1f, 0.85f, 1f, 1f);
        }

        return new Color(
            Math.Clamp(color.R / max, 0.05f, 1f),
            Math.Clamp(color.G / max, 0.05f, 1f),
            Math.Clamp(color.B / max, 0.05f, 1f),
            1f);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, Math.Clamp(alpha, 0f, 1f));
    }

    private static Color Mix(Color from, Color to, float amount)
    {
        return from.Lerp(to, Math.Clamp(amount, 0f, 1f));
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        if (Math.Abs(edge1 - edge0) <= 0.0001f)
        {
            return value < edge0 ? 0f : 1f;
        }

        var x = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return x * x * (3f - 2f * x);
    }

    private static float Fract(float value)
    {
        return value - MathF.Floor(value);
    }

    private static float Hash01(float value)
    {
        var hashed = MathF.Sin(value) * 43758.5453f;
        return hashed - MathF.Floor(hashed);
    }
}
