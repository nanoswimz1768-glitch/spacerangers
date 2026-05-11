using Godot;

namespace SpaceManagersPrototype;

public partial class WarpTunnelLayer : Node2D
{
    private const string PortalRingTexturePath = "res://assets/effects/effectblocks/effectblocks_circle_1.png";
    private const string SparkleTexturePath = "res://assets/effects/effectblocks/effectblocks_sparkle.png";

    private float _phase;
    private float _progress;
    private bool _arriving;
    private Texture2D? _portalRingTexture;
    private Texture2D? _sparkleTexture;
    private static readonly Color WarpBaseOuter = new(0.08f, 0.85f, 1f, 1f);
    private static readonly Color WarpBaseCore = new(0.82f, 1f, 1f, 1f);

    private Color _outerColor = WarpBaseOuter;
    private Color _coreColor = new(0.78f, 1f, 1f, 1f);

    public bool Active { get; private set; }
    public Vector2 MouthOffset { get; set; } = new(0f, -540f);

    public override void _Ready()
    {
        ZIndex = 60;
        Visible = false;
        _portalRingTexture = LoadTexture(PortalRingTexturePath);
        _sparkleTexture = LoadTexture(SparkleTexturePath);
        Material = new CanvasItemMaterial
        {
            BlendMode = CanvasItemMaterial.BlendModeEnum.Add
        };
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
        _outerColor = Mix(WarpBaseOuter, Saturated(outerColor), 0.42f);
        _coreColor = Mix(WarpBaseCore, Saturated(coreColor), 0.28f);
        _arriving = arriving;
        _progress = 0f;
        _phase = 0f;
        MouthOffset = new Vector2(0f, -540f);
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

        var mouth = MouthOffset;
        var open = SmoothStep(0f, 0.18f, _progress);
        var close = 1f - SmoothStep(0.80f, 1f, _progress);
        var intensity = Math.Clamp(open * close, 0f, 1f);
        var corridor = _arriving
            ? Math.Clamp((0.55f + SmoothStep(0f, 0.22f, _progress) * 0.45f) * close, 0f, 1f)
            : intensity;

        DrawWarpSleeve(mouth, corridor);
        DrawCorridorRings(mouth, corridor);
        DrawSpeedStreaks(mouth, corridor);
        DrawNebulaVortex(mouth, intensity);
        DrawMouth(mouth, intensity);
        DrawShipEndGlow(mouth, corridor);
    }

    private void DrawWarpSleeve(Vector2 mouth, float intensity)
    {
        var build = _arriving ? 1f : SmoothStep(0.02f, 0.34f, _progress);
        var collapse = _arriving ? 1f - SmoothStep(0.68f, 1f, _progress) : 1f;
        var sleeve = intensity * build * collapse;
        if (sleeve <= 0.01f)
        {
            return;
        }

        var nearY = _arriving ? -104f : -48f;
        for (var i = 0; i < 10; i++)
        {
            var h0 = Hash01(i * 17.13f + 0.4f);
            var h1 = Hash01(i * 29.71f + 3.2f);
            var side = i % 2 == 0 ? -1f : 1f;
            var startX = side * (54f + h0 * 104f);
            var startY = nearY + (h1 - 0.5f) * 66f;
            var pull = _arriving ? 1f - SmoothStep(0.06f, 0.86f, _progress) : SmoothStep(0.12f, 0.92f, _progress);
            var wake = _arriving
                ? 0.55f + (1f - pull) * 0.65f
                : 0.55f + pull * 0.65f;
            var color = Mix(_outerColor, _coreColor, 0.18f + h1 * 0.50f);
            var previous = new Vector2(startX, startY);
            for (var segment = 1; segment <= 18; segment++)
            {
                var t = segment / 18f;
                var eased = SmoothStep(0f, 1f, t);
                var y = Lerp(startY, mouth.Y + 26f + h1 * 18f, eased);
                var swirl = MathF.Sin(t * MathF.Tau * (0.78f + h0 * 0.42f) + _phase * (1.18f + h1)) * (26f * (1f - t) + 5f);
                var x = Lerp(startX, side * (18f + h0 * 28f), eased) + swirl;
                var point = new Vector2(x, y);
                var alpha = sleeve * wake * (0.024f + h1 * 0.040f) * (1f - t * 0.24f);
                var width = (5.4f * (1f - t) + 1.3f) * (0.64f + h0 * 0.52f);
                DrawLine(previous, point, WithAlpha(color, alpha), width, true);
                DrawLine(previous, point, WithAlpha(_coreColor, alpha * 0.35f), Math.Max(1.2f, width * 0.18f), true);
                previous = point;
            }
        }

        DrawLine(new Vector2(0f, -24f), mouth, WithAlpha(_coreColor, sleeve * 0.11f), 1.8f, true);
        DrawLine(new Vector2(-38f, -18f), mouth + new Vector2(-14f, 18f), WithAlpha(_outerColor, sleeve * 0.070f), 2.2f, true);
        DrawLine(new Vector2(38f, -18f), mouth + new Vector2(14f, 18f), WithAlpha(_outerColor, sleeve * 0.070f), 2.2f, true);
    }

    private void DrawCorridorRings(Vector2 mouth, float intensity)
    {
        if (intensity <= 0.01f)
        {
            return;
        }

        var flow = _arriving ? -0.28f : 0.36f;
        for (var i = 0; i < 11; i++)
        {
            var t = Fract(i / 11f + _phase * flow);
            var y = Lerp(-76f, mouth.Y + 46f, t);
            var widthScale = 1f - t;
            var rx = 36f + widthScale * 142f;
            var ry = 8f + widthScale * 28f;
            var alpha = intensity * (0.12f + widthScale * 0.05f) * (1f - SmoothStep(0.88f, 1f, t));
            var phase = _phase * (1.1f + i * 0.03f) + i * 0.52f;
            var color = Mix(_outerColor, _coreColor, 0.25f + Hash01(i * 7.3f) * 0.42f);
            DrawEllipseArc(new Vector2(0f, y), rx, ry, phase, phase + MathF.Tau * 0.58f, 64, WithAlpha(color, alpha * 0.50f), 2.4f);
            DrawEllipseArc(new Vector2(0f, y), rx * 0.72f, ry * 0.72f, -phase * 0.72f, -phase * 0.72f + MathF.Tau * 0.22f, 40, WithAlpha(_coreColor, alpha * 0.34f), 1.2f);
        }
    }

    private void DrawSpeedStreaks(Vector2 mouth, float intensity)
    {
        var count = _arriving ? 132 : 118;
        var speed = _arriving ? -2.3f : 2.55f;
        for (var i = 0; i < count; i++)
        {
            var h0 = Hash01(i * 13.17f + 1.9f);
            var h1 = Hash01(i * 31.73f + 7.1f);
            var h2 = Hash01(i * 57.11f + 3.4f);
            var side = h0 < 0.5f ? -1f : 1f;
            var t = Fract(h1 + _phase * speed * (0.20f + h2 * 0.22f));
            var y = Lerp(16f, mouth.Y - 115f, t);
            var spread = 38f + (1f - t) * 430f;
            var x = side * spread * MathF.Pow(Math.Abs(h0 * 2f - 1f), 0.58f);
            var direction = (mouth - new Vector2(x, y)).Normalized();
            var tangent = new Vector2(-direction.Y, direction.X);
            var swirl = MathF.Sin(t * 7.7f + _phase * 2.4f + h2 * 4f) * (8f + (1f - t) * 74f);
            var end = new Vector2(x, y) + tangent * swirl;
            var length = 42f + (1f - t) * 170f;
            var start = end - direction * length * (_arriving ? -0.72f : 1f);
            var alpha = intensity * (0.055f + h2 * 0.16f) * SmoothStep(0f, 0.16f, t) * (1f - SmoothStep(0.94f, 1f, t));
            var width = 0.65f + h2 * 1.8f + (1f - t) * 0.8f;
            var color = Mix(_coreColor, _outerColor, 0.25f + h1 * 0.55f);
            DrawLine(start, end, WithAlpha(color, alpha), width, true);
            if (h2 > 0.78f)
            {
                DrawLine(start.Lerp(end, 0.42f), end, WithAlpha(Colors.White, alpha * 0.36f), Math.Max(0.55f, width * 0.32f), true);
            }
        }
    }

    private void DrawNebulaVortex(Vector2 mouth, float intensity)
    {
        for (var i = 0; i < 18; i++)
        {
            var h = Hash01(i * 41.9f + 2.0f);
            var angle = i / 18f * MathF.Tau + _phase * (0.32f + h * 0.20f);
            var arm = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var radius = 50f + h * 210f;
            var width = 1.1f + h * 3.6f;
            var start = mouth + arm * (radius * 0.16f);
            var end = mouth + arm * radius + new Vector2(-arm.Y, arm.X) * (72f + h * 145f);
            DrawLine(start, end, WithAlpha(_outerColor, intensity * (0.045f + h * 0.10f)), width, true);
        }
    }

    private void DrawMouth(Vector2 mouth, float intensity)
    {
        var pulse = 0.5f + 0.5f * MathF.Sin(_phase * 8.4f);
        var openRadius = 82f + pulse * 12f;
        DrawCircle(mouth, openRadius * 1.42f, WithAlpha(_outerColor, intensity * 0.10f));
        DrawCircle(mouth, openRadius * 0.92f, WithAlpha(Mix(_outerColor, _coreColor, 0.42f), intensity * 0.18f));

        if (_portalRingTexture is not null)
        {
            for (var i = 0; i < 3; i++)
            {
                var size = (openRadius * (2.0f + i * 0.56f)) * (1f + pulse * 0.035f);
                DrawSetTransform(mouth, _phase * (i % 2 == 0 ? 0.55f : -0.42f) + i * 0.47f, Vector2.One);
                DrawTextureRect(_portalRingTexture, new Rect2(new Vector2(-size * 0.5f, -size * 0.5f), new Vector2(size, size)), false, WithAlpha(i == 0 ? _coreColor : _outerColor, intensity * (0.28f - i * 0.055f)));
                DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            }
        }

        DrawCircle(mouth, 58f + pulse * 9f, WithAlpha(_coreColor, intensity * 0.45f));
        DrawCircle(mouth, 24f + pulse * 5f, WithAlpha(Colors.White, intensity * 0.32f));
        DrawSparkles(mouth, intensity);
    }

    private void DrawSparkles(Vector2 mouth, float intensity)
    {
        if (_sparkleTexture is null)
        {
            return;
        }

        for (var i = 0; i < 18; i++)
        {
            var h0 = Hash01(i * 9.7f + 1.0f);
            var h1 = Hash01(i * 18.3f + 2.0f);
            var h2 = Hash01(i * 33.1f + 5.0f);
            var orbit = Fract(h0 + _phase * (0.10f + h2 * 0.08f));
            var angle = orbit * MathF.Tau;
            var radius = 84f + h1 * 190f;
            var position = mouth + new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.55f) * radius;
            var size = 7f + h2 * 16f;
            var alpha = intensity * (0.045f + h1 * 0.11f);
            var rect = new Rect2(position - new Vector2(size * 0.5f, size * 0.5f), new Vector2(size, size));
            DrawTextureRect(_sparkleTexture, rect, false, WithAlpha(Mix(_coreColor, Colors.White, 0.28f), alpha));
        }
    }

    private void DrawShipEndGlow(Vector2 mouth, float intensity)
    {
        var nearGlow = _arriving
            ? 1f - SmoothStep(0.64f, 1f, _progress)
            : SmoothStep(0.28f, 0.94f, _progress);
        var alpha = intensity * nearGlow;
        if (alpha <= 0.01f)
        {
            return;
        }

        DrawCircle(Vector2.Zero, 120f + nearGlow * 52f, WithAlpha(_outerColor, alpha * 0.055f));
        DrawLine(Vector2.Zero, mouth, WithAlpha(_coreColor, alpha * 0.13f), 2.2f + nearGlow * 2.8f, true);
    }

    private void DrawEllipseArc(Vector2 center, float radiusX, float radiusY, float startAngle, float endAngle, int points, Color color, float width)
    {
        if (points < 2 || color.A <= 0f || width <= 0f)
        {
            return;
        }

        var previous = center + new Vector2(MathF.Cos(startAngle) * radiusX, MathF.Sin(startAngle) * radiusY);
        for (var i = 1; i <= points; i++)
        {
            var t = i / (float)points;
            var angle = Lerp(startAngle, endAngle, t);
            var current = center + new Vector2(MathF.Cos(angle) * radiusX, MathF.Sin(angle) * radiusY);
            DrawLine(previous, current, color, width, true);
            previous = current;
        }
    }

    private static Texture2D? LoadTexture(string path)
    {
        return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
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

    private static float Lerp(float from, float to, float amount)
    {
        return from + (to - from) * amount;
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
