using Godot;

namespace SpaceRangersPrototype;

public partial class StabilizedSunView : Node2D
{
    private const float DefaultWorldSize = 1320f;
    private SunDiskLayer? _disk;
    private SunCoronaLayer? _corona;
    private IReadOnlyList<Texture2D> _frames = Array.Empty<Texture2D>();

    public float TimeSeconds { get; set; }
    public float WorldSize { get; set; } = DefaultWorldSize;
    public Color DiskTint { get; set; } = Colors.White;
    public Color CoronaColor { get; set; } = new(1f, 0.34f, 0.02f, 1f);
    public float CoronaIntensity { get; set; } = 1f;
    public float AnimationSpeed { get; set; } = 1f;
    public float TintStrength { get; set; }
    public bool IsAvailable => _disk?.IsAvailable == true;

    public override void _Ready()
    {
        ZIndex = 5;

        _disk = new SunDiskLayer { WorldSize = WorldSize, TimeSeconds = TimeSeconds, DiskTint = DiskTint, AnimationSpeed = AnimationSpeed, TintStrength = TintStrength };
        _disk.SetFrames(_frames);
        AddChild(_disk);

        _corona = new SunCoronaLayer { WorldSize = WorldSize, TimeSeconds = TimeSeconds, CoronaColor = CoronaColor, CoronaIntensity = CoronaIntensity, AnimationSpeed = AnimationSpeed };
        AddChild(_corona);
    }

    public void SetFrames(IReadOnlyList<Texture2D> frames)
    {
        _frames = frames;
        _disk?.SetFrames(frames);
    }

    public void QueueSunRedraw()
    {
        if (_disk is null || _corona is null)
        {
            return;
        }

        _disk.WorldSize = WorldSize;
        _disk.TimeSeconds = TimeSeconds;
        _disk.DiskTint = DiskTint;
        _disk.AnimationSpeed = AnimationSpeed;
        _disk.TintStrength = TintStrength;
        _disk.QueueRedraw();

        _corona.WorldSize = WorldSize;
        _corona.TimeSeconds = TimeSeconds;
        _corona.CoronaColor = CoronaColor;
        _corona.CoronaIntensity = CoronaIntensity;
        _corona.AnimationSpeed = AnimationSpeed;
        _corona.QueueRedraw();
    }
}

public partial class SunDiskLayer : Node2D
{
    private const float FramesPerSecond = 18f;
    private ShaderMaterial? _material;
    private IReadOnlyList<Texture2D> _frames = Array.Empty<Texture2D>();

    public float TimeSeconds { get; set; }
    public float WorldSize { get; set; } = 1320f;
    public Color DiskTint { get; set; } = Colors.White;
    public float AnimationSpeed { get; set; } = 1f;
    public float TintStrength { get; set; }
    public bool IsAvailable => _material is not null && _frames.Count > 0;

    public override void _Ready()
    {
        ZIndex = 0;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;

        var shader = ResourceLoader.Load<Shader>("res://shaders/sun_stabilized.gdshader");
        if (shader is not null)
        {
            _material = new ShaderMaterial { Shader = shader };
            Material = _material;
        }
    }

    public void SetFrames(IReadOnlyList<Texture2D> frames)
    {
        _frames = frames;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_material is null || _frames.Count == 0)
        {
            return;
        }

        var frameProgress = TimeSeconds * FramesPerSecond * AnimationSpeed;
        var current = PositiveModulo((int)MathF.Floor(frameProgress), _frames.Count);
        var next = (current + 1) % _frames.Count;
        var previous = PositiveModulo(current - 1, _frames.Count);
        var afterNext = (next + 1) % _frames.Count;
        var blend = SmoothStep(frameProgress - MathF.Floor(frameProgress));

        _material.SetShaderParameter("frame_prev", _frames[previous]);
        _material.SetShaderParameter("frame_current", _frames[current]);
        _material.SetShaderParameter("frame_next", _frames[next]);
        _material.SetShaderParameter("frame_after_next", _frames[afterNext]);
        _material.SetShaderParameter("frame_blend", blend);
        _material.SetShaderParameter("disk_tint", DiskTint);
        _material.SetShaderParameter("tint_strength", TintStrength);

        var size = new Vector2(WorldSize, WorldSize);
        DrawRect(new Rect2(-size * 0.5f, size), Colors.White, true);
    }

    private static int PositiveModulo(int value, int modulo)
    {
        return ((value % modulo) + modulo) % modulo;
    }

    private static float SmoothStep(float value)
    {
        var t = Math.Clamp(value, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}

public partial class SunCoronaLayer : Node2D
{
    private const int CoronaArcSegments = 72;
    private const float ReferenceWorldSize = 1320f;

    public float TimeSeconds { get; set; }
    public float WorldSize { get; set; } = ReferenceWorldSize;
    public Color CoronaColor { get; set; } = new(1f, 0.34f, 0.02f, 1f);
    public float CoronaIntensity { get; set; } = 1f;
    public float AnimationSpeed { get; set; } = 1f;

    public override void _Ready()
    {
        ZIndex = 1;
    }

    public override void _Draw()
    {
        var scale = WorldSize / ReferenceWorldSize;
        var slow = TimeSeconds * 0.26f * AnimationSpeed;
        for (var i = 0; i < 8; i++)
        {
            var radius = (480f + i * 18f + MathF.Sin(TimeSeconds * 0.28f * AnimationSpeed + i) * 2.8f) * scale;
            var start = slow * (i % 2 == 0 ? 1f : -0.7f) + i * 0.8f;
            DrawArc(
                Vector2.Zero,
                radius,
                start,
                start + 1.1f,
                CoronaArcSegments,
                new Color(CoronaColor.R, CoronaColor.G, CoronaColor.B, 0.045f * CoronaIntensity),
                3.2f * scale,
                true);
        }
    }
}
