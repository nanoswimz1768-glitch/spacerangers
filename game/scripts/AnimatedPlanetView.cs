using Godot;

namespace SpaceRangersPrototype;

public partial class AnimatedPlanetView : Node2D
{
    private PlanetRingLayer? _ringBack;
    private PlanetSphereLayer? _sphere;
    private PlanetRingLayer? _ringFront;

    public float TextureWorldSize { get; set; } = 300f;
    public float BodyDiameter { get; set; } = 300f;
    public float TimeSeconds { get; set; }
    public float Daylight { get; set; } = 1f;
    public float RotationSpeed { get; set; } = 0.012f;
    public float FlowStrength { get; set; } = 0.006f;
    public float AtmosphereStrength { get; set; } = 0.18f;
    public float Contrast { get; set; } = 1f;
    public float Saturation { get; set; } = 1f;
    public float GlowStrength { get; set; } = 1f;
    public Color AtmosphereColor { get; set; } = new(0.35f, 0.7f, 1f, 1f);
    public Texture2D? SurfaceTexture { get; set; }
    public bool HasRings { get; set; }
    public float RingInnerRadiusFactor { get; set; } = 0.54f;
    public float RingOuterRadiusFactor { get; set; } = 0.94f;
    public float RingFlattening { get; set; } = 0.30f;
    public float RingRotation { get; set; } = -0.24f;
    public float RingAlpha { get; set; } = 0.78f;
    public Color RingColor { get; set; } = new(0.78f, 0.70f, 0.58f, 1f);
    public Color RingAccentColor { get; set; } = new(1.00f, 0.92f, 0.72f, 1f);

    public bool IsAvailable => SurfaceTexture is not null && _sphere?.IsAvailable == true;

    public override void _Ready()
    {
        ZIndex = 1;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;

        _ringBack = new PlanetRingLayer { Front = false, ZIndex = -1 };
        _sphere = new PlanetSphereLayer { ZIndex = 0 };
        _ringFront = new PlanetRingLayer { Front = true, ZIndex = 1 };

        AddChild(_ringBack);
        AddChild(_sphere);
        AddChild(_ringFront);
        ApplyVisualState();
    }

    public override void _Draw()
    {
        if (SurfaceTexture is null)
        {
            return;
        }

        var glowRadius = BodyDiameter * 0.5f;
        var daylight = Math.Clamp(Daylight, 0.52f, 1.0f);
        for (var i = 5; i >= 1; i--)
        {
            var radius = glowRadius + BodyDiameter * (0.035f * i);
            var alpha = AtmosphereStrength * GlowStrength * daylight * 0.030f / i;
            DrawCircle(Vector2.Zero, radius, new Color(AtmosphereColor.R, AtmosphereColor.G, AtmosphereColor.B, alpha));
        }

        var pulse = 0.5f + MathF.Sin(TimeSeconds * 0.75f + BodyDiameter * 0.01f) * 0.5f;
        DrawCircle(Vector2.Zero, glowRadius * (1.015f + pulse * 0.012f), new Color(AtmosphereColor.R, AtmosphereColor.G, AtmosphereColor.B, AtmosphereStrength * GlowStrength * 0.026f));
    }

    public void ApplyVisualState()
    {
        if (_sphere is not null)
        {
            _sphere.Diameter = BodyDiameter;
            _sphere.TimeSeconds = TimeSeconds;
            _sphere.Daylight = Daylight;
            _sphere.RotationSpeed = RotationSpeed;
            _sphere.FlowStrength = FlowStrength;
            _sphere.AtmosphereStrength = AtmosphereStrength;
            _sphere.Contrast = Contrast;
            _sphere.Saturation = Saturation;
            _sphere.AtmosphereColor = AtmosphereColor;
            _sphere.SurfaceTexture = SurfaceTexture;
            _sphere.QueueRedraw();
        }

        ApplyRingState(_ringBack);
        ApplyRingState(_ringFront);
        QueueRedraw();
    }

    private void ApplyRingState(PlanetRingLayer? ring)
    {
        if (ring is null)
        {
            return;
        }

        ring.ApplyVisualState(
            HasRings,
            BodyDiameter,
            Daylight,
            RingInnerRadiusFactor,
            RingOuterRadiusFactor,
            RingFlattening,
            RingRotation,
            RingAlpha,
            RingColor,
            RingAccentColor);
    }
}

public partial class PlanetSphereLayer : Node2D
{
    private ShaderMaterial? _material;

    public float Diameter { get; set; } = 300f;
    public float TimeSeconds { get; set; }
    public float Daylight { get; set; } = 1f;
    public float RotationSpeed { get; set; } = 0.012f;
    public float FlowStrength { get; set; }
    public float AtmosphereStrength { get; set; } = 0.18f;
    public float Contrast { get; set; } = 1f;
    public float Saturation { get; set; } = 1f;
    public Color AtmosphereColor { get; set; } = new(0.35f, 0.7f, 1f, 1f);
    public Texture2D? SurfaceTexture { get; set; }

    public bool IsAvailable => _material is not null && SurfaceTexture is not null;

    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;

        var shader = ResourceLoader.Load<Shader>("res://shaders/animated_planet.gdshader");
        if (shader is not null)
        {
            _material = new ShaderMaterial { Shader = shader };
            Material = _material;
        }
    }

    public override void _Draw()
    {
        var material = _material;
        var surface = SurfaceTexture;
        if (material is null || surface is null)
        {
            return;
        }

        material.SetShaderParameter("surface_map", surface);
        material.SetShaderParameter("time_seconds", TimeSeconds);
        material.SetShaderParameter("daylight", Daylight);
        material.SetShaderParameter("rotation_speed", RotationSpeed);
        material.SetShaderParameter("flow_strength", FlowStrength);
        material.SetShaderParameter("atmosphere_strength", AtmosphereStrength);
        material.SetShaderParameter("contrast", Contrast);
        material.SetShaderParameter("saturation", Saturation);
        material.SetShaderParameter("atmosphere_color", AtmosphereColor);

        var size = new Vector2(Diameter, Diameter);
        DrawRect(new Rect2(-size * 0.5f, size), Colors.White, true);
    }
}

public partial class PlanetRingLayer : Node2D
{
    private static readonly RingBand[] Bands =
    {
        new(0.00f, 0.11f, 0.22f, 0.15f),
        new(0.13f, 0.27f, 0.48f, 0.34f),
        new(0.30f, 0.50f, 0.32f, 0.12f),
        new(0.55f, 0.69f, 0.64f, 0.52f),
        new(0.72f, 0.88f, 0.40f, 0.26f),
        new(0.90f, 1.00f, 0.18f, 0.62f),
    };

    public bool Front { get; set; }
    public float BodyDiameter { get; set; } = 300f;
    public float TimeSeconds { get; set; }
    public float Daylight { get; set; } = 1f;
    public float InnerRadiusFactor { get; set; } = 0.54f;
    public float OuterRadiusFactor { get; set; } = 0.94f;
    public float Flattening { get; set; } = 0.30f;
    public float RingRotationAngle { get; set; } = -0.24f;
    public float Alpha { get; set; } = 0.78f;
    public Color RingColor { get; set; } = new(0.78f, 0.70f, 0.58f, 1f);
    public Color AccentColor { get; set; } = new(1.00f, 0.92f, 0.72f, 1f);

    private bool _hasState;
    private float _lastBodyDiameter = -1f;
    private float _lastDaylight = -1f;
    private float _lastInnerRadiusFactor = -1f;
    private float _lastOuterRadiusFactor = -1f;
    private float _lastFlattening = -1f;
    private float _lastRingRotationAngle = -100f;
    private float _lastAlpha = -1f;
    private Color _lastRingColor;
    private Color _lastAccentColor;

    public void ApplyVisualState(
        bool visible,
        float bodyDiameter,
        float daylight,
        float innerRadiusFactor,
        float outerRadiusFactor,
        float flattening,
        float ringRotationAngle,
        float alpha,
        Color ringColor,
        Color accentColor)
    {
        Visible = visible;
        if (!visible)
        {
            return;
        }

        // Quantize daylight so rings do not rebuild geometry every frame while orbit lighting changes imperceptibly.
        daylight = MathF.Round(daylight * 40f) / 40f;
        var changed = !_hasState
            || MathF.Abs(_lastBodyDiameter - bodyDiameter) > 0.1f
            || MathF.Abs(_lastDaylight - daylight) > 0.001f
            || MathF.Abs(_lastInnerRadiusFactor - innerRadiusFactor) > 0.0001f
            || MathF.Abs(_lastOuterRadiusFactor - outerRadiusFactor) > 0.0001f
            || MathF.Abs(_lastFlattening - flattening) > 0.0001f
            || MathF.Abs(_lastRingRotationAngle - ringRotationAngle) > 0.0001f
            || MathF.Abs(_lastAlpha - alpha) > 0.0001f
            || _lastRingColor != ringColor
            || _lastAccentColor != accentColor;

        if (!changed)
        {
            return;
        }

        _hasState = true;
        _lastBodyDiameter = bodyDiameter;
        _lastDaylight = daylight;
        _lastInnerRadiusFactor = innerRadiusFactor;
        _lastOuterRadiusFactor = outerRadiusFactor;
        _lastFlattening = flattening;
        _lastRingRotationAngle = ringRotationAngle;
        _lastAlpha = alpha;
        _lastRingColor = ringColor;
        _lastAccentColor = accentColor;

        BodyDiameter = bodyDiameter;
        Daylight = daylight;
        InnerRadiusFactor = innerRadiusFactor;
        OuterRadiusFactor = outerRadiusFactor;
        Flattening = flattening;
        RingRotationAngle = ringRotationAngle;
        Alpha = alpha;
        RingColor = ringColor;
        AccentColor = accentColor;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var start = Front ? 0f : MathF.PI;
        var end = Front ? MathF.PI : MathF.Tau;
        var passAlpha = Alpha * (Front ? 0.88f : 0.54f) * Math.Clamp(Daylight, 0.54f, 1.0f);
        var bodyRadius = BodyDiameter * 0.5f;
        var inner = BodyDiameter * InnerRadiusFactor;
        var outer = BodyDiameter * OuterRadiusFactor;

        DrawEllipseBand(inner, outer, start, end, WithAlpha(RingColor, passAlpha * 0.13f));

        foreach (var band in Bands)
        {
            var bandInner = Lerp(inner, outer, band.Inner);
            var bandOuter = Lerp(inner, outer, band.Outer);
            var color = Mix(RingColor, AccentColor, band.AccentMix);
            DrawEllipseBand(bandInner, bandOuter, start, end, WithAlpha(color, passAlpha * band.Alpha));
        }

        DrawEllipseBand(Lerp(inner, outer, 0.515f), Lerp(inner, outer, 0.565f), start, end, new Color(0.0f, 0.0f, 0.0f, passAlpha * 0.32f));
        DrawEllipseBand(Lerp(inner, outer, 0.585f), Lerp(inner, outer, 0.610f), start, end, WithAlpha(AccentColor, passAlpha * 0.34f));

        for (var line = 0; line < 9; line++)
        {
            var t = 0.07f + line * 0.105f;
            var radius = Lerp(inner, outer, t);
            var lineAlpha = passAlpha * (line % 3 == 0 ? 0.34f : 0.18f);
            DrawEllipseArc(radius, start, end, Math.Max(1.0f, BodyDiameter * 0.0028f), WithAlpha(Mix(RingColor, AccentColor, 0.55f), lineAlpha));
        }

        DrawEllipseArc(inner, start, end, Math.Max(1.0f, BodyDiameter * 0.0032f), WithAlpha(AccentColor, passAlpha * 0.28f));
        DrawEllipseArc(outer, start, end, Math.Max(1.0f, BodyDiameter * 0.0032f), WithAlpha(AccentColor, passAlpha * 0.36f));
        DrawPlanetShadowOnBackHalf(start, end, bodyRadius, inner, outer);
    }

    private void DrawEllipseBand(float innerRadius, float outerRadius, float start, float end, Color color)
    {
        const int Steps = 72;
        var points = new Vector2[(Steps + 1) * 2];
        var cos = MathF.Cos(RingRotationAngle);
        var sin = MathF.Sin(RingRotationAngle);

        for (var i = 0; i <= Steps; i++)
        {
            var t = start + (end - start) * i / Steps;
            points[i] = EllipsePoint(t, outerRadius, cos, sin);
            points[points.Length - 1 - i] = EllipsePoint(t, innerRadius, cos, sin);
        }

        DrawColoredPolygon(points, color);
    }

    private void DrawEllipseArc(float radius, float start, float end, float width, Color color)
    {
        const int Steps = 72;
        var points = new Vector2[Steps + 1];
        var cos = MathF.Cos(RingRotationAngle);
        var sin = MathF.Sin(RingRotationAngle);

        for (var i = 0; i <= Steps; i++)
        {
            var t = start + (end - start) * i / Steps;
            points[i] = EllipsePoint(t, radius, cos, sin);
        }

        DrawPolyline(points, color, width, true);
    }

    private Vector2 EllipsePoint(float angle, float radius, float cos, float sin)
    {
        var x = MathF.Cos(angle) * radius;
        var y = MathF.Sin(angle) * radius * Flattening;
        return new Vector2(x * cos - y * sin, x * sin + y * cos);
    }

    private void DrawPlanetShadowOnBackHalf(float start, float end, float bodyRadius, float inner, float outer)
    {
        if (Front)
        {
            return;
        }

        var shadowInner = Math.Max(inner, bodyRadius * 0.92f);
        var shadowOuter = Math.Min(outer, bodyRadius * 1.34f);
        if (shadowInner >= shadowOuter)
        {
            return;
        }

        DrawEllipseBand(shadowInner, shadowOuter, start, end, new Color(0.0f, 0.0f, 0.0f, Alpha * 0.13f));
    }

    private static float Lerp(float from, float to, float amount)
    {
        return from + (to - from) * amount;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, Math.Clamp(alpha, 0f, 1f));
    }

    private static Color Mix(Color from, Color to, float amount)
    {
        return new Color(
            from.R + (to.R - from.R) * amount,
            from.G + (to.G - from.G) * amount,
            from.B + (to.B - from.B) * amount,
            from.A + (to.A - from.A) * amount);
    }

    private readonly record struct RingBand(float Inner, float Outer, float Alpha, float AccentMix);
}
