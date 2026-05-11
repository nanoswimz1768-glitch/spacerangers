using Godot;

namespace SpaceManagersPrototype;

public partial class WarpScreenLayer : Control
{
    private static readonly Color DefaultOuter = new(0.08f, 0.82f, 1f, 1f);
    private static readonly Color DefaultCore = new(0.82f, 1f, 1f, 1f);

    private bool _active;
    private bool _arriving;
    private float _phase;
    private float _progress;
    private Vector2 _focus;
    private Color _outerColor = DefaultOuter;
    private Color _coreColor = DefaultCore;
    private float _flashAge = 99f;
    private Vector2 _flashFocus;
    private Color _flashOuter = DefaultOuter;
    private Color _flashCore = DefaultCore;
    private float _afterglowAge = 99f;
    private Vector2 _afterglowFocus;
    private Color _afterglowOuter = DefaultOuter;
    private Color _afterglowCore = DefaultCore;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Visible = true;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _phase += MathF.Min(0.05f, Math.Max(0f, (float)delta));

        if (_flashAge < 2f)
        {
            _flashAge += (float)delta;
        }

        if (_afterglowAge < 3f)
        {
            _afterglowAge += (float)delta;
        }

        if (_active || _flashAge < 0.62f || _afterglowAge < 1.65f)
        {
            QueueRedraw();
        }
    }

    public void SetWarpState(bool active, Vector2 focus, Color outerColor, Color coreColor, float progress, bool arriving)
    {
        var started = active && !_active;
        var changedPhase = active && _active && arriving != _arriving;

        _active = active;
        _focus = focus;
        _outerColor = Mix(DefaultOuter, Saturated(outerColor), 0.60f);
        _coreColor = Mix(DefaultCore, Saturated(coreColor), 0.38f);
        _progress = Math.Clamp(progress, 0f, 1f);
        _arriving = arriving;

        if (started || changedPhase)
        {
            TriggerFlash(focus, outerColor, coreColor);
        }
    }

    public void TriggerFlash(Vector2 focus, Color outerColor, Color coreColor)
    {
        _flashAge = 0f;
        _flashFocus = focus;
        _flashOuter = Mix(DefaultOuter, Saturated(outerColor), 0.62f);
        _flashCore = Mix(DefaultCore, Saturated(coreColor), 0.42f);
        QueueRedraw();
    }

    public void StartAfterglow(Vector2 focus, Color outerColor, Color coreColor)
    {
        _afterglowAge = 0f;
        _afterglowFocus = focus;
        _afterglowOuter = Mix(DefaultOuter, Saturated(outerColor), 0.60f);
        _afterglowCore = Mix(DefaultCore, Saturated(coreColor), 0.38f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = Size;
        if (size.X <= 1f || size.Y <= 1f)
        {
            size = GetViewportRect().Size;
        }

        if (size.X <= 1f || size.Y <= 1f)
        {
            return;
        }

        if (_active)
        {
            var open = SmoothStep(0.02f, 0.22f, _progress);
            var close = 1f - SmoothStep(0.88f, 1f, _progress);
            var intensity = Math.Clamp(open * close, 0f, 1f);
            DrawTransitSpace(size, _focus, _outerColor, _coreColor, intensity);
        }

        DrawAfterglow(size);
        DrawFlash(size);
    }

    private void DrawTransitSpace(Vector2 size, Vector2 focus, Color outerColor, Color coreColor, float intensity)
    {
        if (intensity <= 0.01f)
        {
            return;
        }

        var tunnelPull = _arriving
            ? 0.72f + (1f - _progress) * 0.18f
            : 0.58f + _progress * 0.34f;
        var shadeAlpha = intensity * (0.11f + tunnelPull * 0.10f);
        DrawRect(new Rect2(Vector2.Zero, size), new Color(0f, 0.006f, 0.012f, shadeAlpha), true);

        var maxRadius = MathF.Sqrt(size.X * size.X + size.Y * size.Y);
        DrawRadialStreaks(size, focus, outerColor, coreColor, intensity, maxRadius);
        DrawScreenVortex(focus, outerColor, coreColor, intensity, maxRadius);

        var corePulse = 0.5f + 0.5f * MathF.Sin(_phase * 8.6f);
        DrawCircle(focus, 170f + corePulse * 38f, WithAlpha(outerColor, intensity * 0.025f));
        DrawCircle(focus, 58f + corePulse * 12f, WithAlpha(coreColor, intensity * 0.045f));
    }

    private void DrawRadialStreaks(Vector2 size, Vector2 focus, Color outerColor, Color coreColor, float intensity, float maxRadius)
    {
        var count = 146;
        var flow = _arriving ? -0.82f : 1.0f;
        for (var i = 0; i < count; i++)
        {
            var h0 = Hash01(i * 12.989f + 0.4f);
            var h1 = Hash01(i * 78.233f + 6.1f);
            var h2 = Hash01(i * 37.719f + 1.7f);
            var angle = h0 * MathF.Tau + MathF.Sin(_phase * 0.18f + h1 * 9.0f) * 0.06f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var t = Fract(h1 + _phase * flow * (0.22f + h2 * 0.23f));
            var radius = maxRadius * Lerp(0.18f, 0.88f, t);
            var length = maxRadius * (0.032f + h2 * 0.065f) * (0.75f + intensity * 1.15f);
            var lateral = new Vector2(-direction.Y, direction.X) * MathF.Sin(_phase * 1.8f + h1 * 11.0f) * (3f + h2 * 14f);
            var outer = focus + direction * radius + lateral;
            var inner = focus + direction * MathF.Max(22f, radius - length) + lateral * 0.45f;
            var alpha = intensity * (0.035f + h2 * 0.105f) * SmoothStep(0f, 0.12f, t) * (1f - SmoothStep(0.92f, 1f, t));
            var color = Mix(coreColor, outerColor, 0.22f + h1 * 0.62f);
            DrawLine(outer, inner, WithAlpha(color, alpha), 0.75f + h2 * 1.95f, true);
            if (h2 > 0.84f)
            {
                DrawLine(outer.Lerp(inner, 0.38f), inner, WithAlpha(Colors.White, alpha * 0.24f), 0.65f, true);
            }
        }
    }

    private void DrawScreenVortex(Vector2 focus, Color outerColor, Color coreColor, float intensity, float maxRadius)
    {
        var bandCount = 6;
        for (var band = 0; band < bandCount; band++)
        {
            var seed = band * 17.31f + 2.4f;
            var color = Mix(outerColor, coreColor, 0.16f + Hash01(seed) * 0.56f);
            var width = 2.4f + band * 0.24f + Hash01(seed + 8f) * 2.8f;
            var turns = 1.42f + Hash01(seed + 3f) * 1.1f;
            var rotation = _phase * (0.18f + Hash01(seed + 6f) * 0.12f) * (band % 2 == 0 ? 1f : -1f);
            var previous = SpiralPoint(focus, maxRadius * 0.075f, maxRadius * 0.58f, turns, rotation + band * MathF.Tau / bandCount, 0f);

            for (var segment = 1; segment <= 48; segment++)
            {
                var t = segment / 48f;
                var eased = MathF.Pow(t, 0.84f);
                var point = SpiralPoint(focus, maxRadius * 0.075f, maxRadius * 0.58f, turns, rotation + band * MathF.Tau / bandCount, eased);
                var fade = SmoothStep(0f, 0.18f, t) * (1f - SmoothStep(0.72f, 1f, t));
                var alpha = intensity * fade * (0.012f + Hash01(seed + segment * 1.17f) * 0.018f);
                DrawLine(previous, point, WithAlpha(color, alpha), width * (1f - t * 0.44f), true);
                previous = point;
            }
        }
    }

    private void DrawFlash(Vector2 size)
    {
        const float duration = 0.58f;
        if (_flashAge >= duration)
        {
            return;
        }

        var t = Math.Clamp(_flashAge / duration, 0f, 1f);
        var fade = MathF.Pow(1f - t, 2.15f);
        var maxRadius = MathF.Sqrt(size.X * size.X + size.Y * size.Y);
        var radius = Lerp(48f, maxRadius * 0.62f, SmoothStep(0f, 1f, t));
        DrawRect(new Rect2(Vector2.Zero, size), WithAlpha(Mix(_flashOuter, Colors.White, 0.25f), fade * 0.045f), true);
        DrawArc(_flashFocus, radius, 0f, MathF.Tau, 112, WithAlpha(_flashCore, fade * 0.62f), 2.6f + fade * 6.0f, true);
        DrawArc(_flashFocus, radius * 0.62f, 0f, MathF.Tau, 88, WithAlpha(_flashOuter, fade * 0.38f), 1.8f + fade * 3.5f, true);
        DrawCircle(_flashFocus, 42f + fade * 64f, WithAlpha(_flashCore, fade * 0.095f));
    }

    private void DrawAfterglow(Vector2 size)
    {
        const float duration = 1.55f;
        if (_afterglowAge >= duration)
        {
            return;
        }

        var t = Math.Clamp(_afterglowAge / duration, 0f, 1f);
        var fade = MathF.Pow(1f - t, 1.75f);
        var maxRadius = MathF.Sqrt(size.X * size.X + size.Y * size.Y);
        DrawScreenVortex(_afterglowFocus, _afterglowOuter, _afterglowCore, fade * 0.48f, maxRadius);
        for (var i = 0; i < 38; i++)
        {
            var h0 = Hash01(i * 29.19f + 1.0f);
            var h1 = Hash01(i * 47.37f + 3.0f);
            var angle = h0 * MathF.Tau + _phase * 0.12f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var start = _afterglowFocus + direction * (42f + h1 * 220f);
            var end = start + direction * (40f + h0 * 130f) * fade;
            DrawLine(start, end, WithAlpha(Mix(_afterglowOuter, _afterglowCore, h1), fade * (0.045f + h1 * 0.07f)), 0.8f + h1 * 1.7f, true);
        }
    }

    private static Vector2 SpiralPoint(Vector2 focus, float innerRadius, float outerRadius, float turns, float rotation, float t)
    {
        var radius = Lerp(innerRadius, outerRadius, t);
        var angle = rotation + t * MathF.Tau * turns;
        return focus + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
    }

    private static Color Saturated(Color color)
    {
        var max = Math.Max(color.R, Math.Max(color.G, color.B));
        if (max <= 0.001f)
        {
            return DefaultOuter;
        }

        return new Color(
            Math.Clamp(color.R / max, 0.04f, 1f),
            Math.Clamp(color.G / max, 0.04f, 1f),
            Math.Clamp(color.B / max, 0.04f, 1f),
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
        return Fract(MathF.Sin(value) * 43758.5453f);
    }
}
