using Godot;

namespace SpaceManagersPrototype;

public partial class WarpTunnelLayer : Node2D
{
    private const string StripShaderPath = "res://shaders/warp_tunnel_strip.gdshader";
    private const string SheathShaderPath = "res://shaders/warp_tunnel_sheath.gdshader";
    private const string PortalShaderPath = "res://shaders/warp_portal_mouth.gdshader";

    private Polygon2D _sheath = null!;
    private Polygon2D _strip = null!;
    private Polygon2D _mouth = null!;
    private ImageTexture _whiteTexture = null!;
    private ShaderMaterial? _sheathMaterial;
    private ShaderMaterial? _stripMaterial;
    private ShaderMaterial? _mouthMaterial;
    private float _phase;
    private float _progress;
    private float _fade = 1f;
    private bool _arriving;
    private bool _residualActive;
    private float _residualAge;
    private float _residualDuration = 1.4f;
    private Color _outerColor = new(0.16f, 0.72f, 1f, 1f);
    private Color _coreColor = new(0.86f, 1f, 1f, 1f);

    public bool Active { get; private set; }
    public Vector2 MouthOffset { get; set; } = new(0f, -560f);

    public override void _Ready()
    {
        ZIndex = 14;
        Visible = false;

        _sheath = new Polygon2D
        {
            Antialiased = true,
            Color = Colors.White,
            ZIndex = 0
        };
        AddChild(_sheath);

        _strip = new Polygon2D
        {
            Antialiased = true,
            Color = Colors.White,
            ZIndex = 1
        };
        AddChild(_strip);

        _mouth = new Polygon2D
        {
            Antialiased = true,
            Color = Colors.White,
            ZIndex = 3
        };
        AddChild(_mouth);

        var image = Image.CreateEmpty(256, 256, false, Image.Format.Rgba8);
        image.Fill(Colors.White);
        _whiteTexture = ImageTexture.CreateFromImage(image);
        _sheath.Texture = _whiteTexture;
        _strip.Texture = _whiteTexture;
        _mouth.Texture = _whiteTexture;

        var sheathShader = ResourceLoader.Load<Shader>(SheathShaderPath);
        if (sheathShader is not null)
        {
            _sheathMaterial = new ShaderMaterial { Shader = sheathShader };
            _sheath.Material = _sheathMaterial;
        }

        var stripShader = ResourceLoader.Load<Shader>(StripShaderPath);
        if (stripShader is not null)
        {
            _stripMaterial = new ShaderMaterial { Shader = stripShader };
            _strip.Material = _stripMaterial;
        }

        var portalShader = ResourceLoader.Load<Shader>(PortalShaderPath);
        if (portalShader is not null)
        {
            _mouthMaterial = new ShaderMaterial { Shader = portalShader };
            _mouth.Material = _mouthMaterial;
        }

        UpdateGeometry();
        ApplyMaterial();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (!Active)
        {
            return;
        }

        var step = MathF.Min(0.05f, Math.Max(0f, (float)delta));
        _phase += step;
        if (_residualActive)
        {
            _residualAge += step;
            _progress = Math.Clamp(_progress + step * 0.20f, 0f, 1f);
            _fade = MathF.Pow(1f - SmoothStep(0f, _residualDuration, _residualAge), 1.45f);
            if (_residualAge >= _residualDuration)
            {
                Stop();
                return;
            }
        }
        else
        {
            _fade = MoveToward(_fade, 1f, step * 5f);
        }

        UpdateGeometry();
        ApplyMaterial();
    }

    public void Start(Color outerColor, Color coreColor, bool arriving)
    {
        _outerColor = Saturated(outerColor).Lerp(new Color(0.16f, 0.72f, 1f, 1f), 0.18f);
        _coreColor = Saturated(coreColor).Lerp(new Color(0.86f, 1f, 1f, 1f), 0.16f);
        _arriving = arriving;
        _progress = 0f;
        _phase = 0f;
        _fade = 1f;
        _residualActive = false;
        _residualAge = 0f;
        Active = true;
        Visible = true;
        UpdateGeometry();
        ApplyMaterial();
    }

    public void SetProgress(float progress)
    {
        _progress = Math.Clamp(progress, 0f, 1f);
        if (Active)
        {
            ApplyMaterial();
        }
    }

    public void BeginResidual(Color outerColor, Color coreColor, bool arriving, float duration)
    {
        _outerColor = Saturated(outerColor).Lerp(new Color(0.16f, 0.72f, 1f, 1f), 0.18f);
        _coreColor = Saturated(coreColor).Lerp(new Color(0.86f, 1f, 1f, 1f), 0.16f);
        _arriving = arriving;
        _progress = 0.70f;
        _phase = 0f;
        _fade = 0.82f;
        _residualActive = true;
        _residualAge = 0f;
        _residualDuration = Math.Max(0.1f, duration);
        Active = true;
        Visible = true;
        UpdateGeometry();
        ApplyMaterial();
    }

    public void Stop()
    {
        Active = false;
        Visible = false;
        _progress = 0f;
        _fade = 0f;
        _residualActive = false;
        _residualAge = 0f;
        ApplyMaterial();
    }

    private void UpdateGeometry()
    {
        var end = MouthOffset;
        if (end.LengthSquared() < 1f)
        {
            end = new Vector2(0f, -560f);
        }

        var direction = end.Normalized();
        var right = new Vector2(-direction.Y, direction.X);
        var length = end.Length();
        var nearWidth = Math.Clamp(132f + length * 0.020f, 130f, 176f);
        var farWidth = Math.Clamp(320f + length * 0.15f, 360f, 500f);
        var start = direction * 38f;
        var portalCenter = end;

        const int textureUvMax = 256;
        const int stripSegments = 14;
        var stripPolygon = new Vector2[(stripSegments + 1) * 2];
        var stripUv = new Vector2[(stripSegments + 1) * 2];
        var stripLength = Math.Max(80f, length - 76f);
        for (var i = 0; i <= stripSegments; i++)
        {
            var t = i / (float)stripSegments;
            var eased = SmoothStep(0f, 1f, t);
            var center = start + direction * (stripLength * t);
            var width = nearWidth + (farWidth - nearWidth) * eased;
            width *= 0.92f + MathF.Sin(t * MathF.PI) * 0.12f;

            stripPolygon[i] = center - right * width * 0.5f;
            stripUv[i] = new Vector2(0f, t * textureUvMax);
            var rightIndex = stripPolygon.Length - 1 - i;
            stripPolygon[rightIndex] = center + right * width * 0.5f;
            stripUv[rightIndex] = new Vector2(textureUvMax, t * textureUvMax);
        }

        _strip.Polygon = stripPolygon;
        _strip.UV = stripUv;
        _sheath.Polygon = stripPolygon;
        _sheath.UV = stripUv;

        var radius = Math.Clamp(farWidth * 0.56f, 210f, 310f);
        const int mouthSegments = 72;
        var mouthPolygon = new Vector2[mouthSegments];
        var mouthUv = new Vector2[mouthSegments];
        for (var i = 0; i < mouthSegments; i++)
        {
            var angle = i / (float)mouthSegments * MathF.Tau;
            var unit = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            mouthPolygon[i] = portalCenter + unit * radius;
            mouthUv[i] = new Vector2(
                (0.5f + unit.X * 0.5f) * textureUvMax,
                (0.5f + unit.Y * 0.5f) * textureUvMax);
        }

        _mouth.Polygon = mouthPolygon;
        _mouth.UV = mouthUv;
    }

    private void ApplyMaterial()
    {
        ApplyMaterial(_stripMaterial);
        ApplyMaterial(_sheathMaterial);
        ApplyMaterial(_mouthMaterial);
    }

    private void ApplyMaterial(ShaderMaterial? material)
    {
        if (material is null)
        {
            return;
        }

        material.SetShaderParameter("time_seconds", _phase);
        material.SetShaderParameter("progress", _progress);
        material.SetShaderParameter("fade", _fade);
        material.SetShaderParameter("arriving", _arriving ? 1f : 0f);
        material.SetShaderParameter("outer_color", _outerColor);
        material.SetShaderParameter("core_color", _coreColor);
        material.SetShaderParameter("secondary_color", SecondaryWarpColor(_outerColor));
    }

    private static Color Saturated(Color color)
    {
        var max = Math.Max(color.R, Math.Max(color.G, color.B));
        if (max <= 0.001f)
        {
            return new Color(0.16f, 0.72f, 1f, 1f);
        }

        return new Color(
            Math.Clamp(color.R / max, 0.04f, 1f),
            Math.Clamp(color.G / max, 0.04f, 1f),
            Math.Clamp(color.B / max, 0.04f, 1f),
            1f);
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

    private static float MoveToward(float from, float to, float delta)
    {
        if (from < to)
        {
            return Math.Min(from + Math.Max(0f, delta), to);
        }

        return Math.Max(from - Math.Max(0f, delta), to);
    }

    private static Color SecondaryWarpColor(Color primary)
    {
        var blueBias = new Color(0.54f, 0.18f, 1f, 1f);
        var warmBias = new Color(1f, 0.62f, 0.14f, 1f);
        var bias = primary.R > primary.B + 0.18f ? warmBias : blueBias;
        return Saturated(primary.Lerp(bias, 0.54f));
    }
}
