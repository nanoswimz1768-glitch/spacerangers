using Godot;

namespace SpaceManagersPrototype;

public partial class WarpTunnelLayer : Node2D
{
    private const int TextureUvMax = 256;
    private const string StripShaderPath = "res://shaders/warp_tunnel_strip.gdshader";
    private const string SheathShaderPath = "res://shaders/warp_tunnel_sheath.gdshader";
    private const string StarsShaderPath = "res://shaders/warp_tunnel_stars.gdshader";
    private const string PortalShaderPath = "res://shaders/warp_portal_mouth.gdshader";
    private const string LensShaderPath = "res://shaders/warp_portal_lens.gdshader";
    private const string ApertureShaderPath = "res://shaders/warp_portal_aperture.gdshader";
    private const string ResidualShaderPath = "res://shaders/warp_residual_rift.gdshader";
    private const string ShockwaveShaderPath = "res://shaders/warp_shockwave.gdshader";

    private Polygon2D _lens = null!;
    private Polygon2D _sheath = null!;
    private Polygon2D _stars = null!;
    private Polygon2D _strip = null!;
    private Polygon2D _mouth = null!;
    private Polygon2D _aperture = null!;
    private Polygon2D _residualRift = null!;
    private Polygon2D _shipShockwave = null!;
    private Polygon2D _mouthShockwave = null!;
    private ImageTexture _whiteTexture = null!;
    private ShaderMaterial? _lensMaterial;
    private ShaderMaterial? _sheathMaterial;
    private ShaderMaterial? _starsMaterial;
    private ShaderMaterial? _stripMaterial;
    private ShaderMaterial? _mouthMaterial;
    private ShaderMaterial? _apertureMaterial;
    private ShaderMaterial? _residualRiftMaterial;
    private ShaderMaterial? _shipShockwaveMaterial;
    private ShaderMaterial? _mouthShockwaveMaterial;
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

        _lens = new Polygon2D
        {
            Antialiased = true,
            Color = Colors.White,
            ZIndex = -1
        };
        AddChild(_lens);

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
            ZIndex = 2
        };
        AddChild(_strip);

        _stars = new Polygon2D
        {
            Antialiased = true,
            Color = Colors.White,
            ZIndex = 3
        };
        AddChild(_stars);

        _mouth = new Polygon2D
        {
            Antialiased = true,
            Color = Colors.White,
            ZIndex = 4
        };
        AddChild(_mouth);

        _aperture = new Polygon2D
        {
            Antialiased = true,
            Color = Colors.White,
            ZIndex = 7
        };
        AddChild(_aperture);

        _residualRift = new Polygon2D
        {
            Antialiased = true,
            Color = Colors.White,
            ZIndex = 8
        };
        AddChild(_residualRift);

        _shipShockwave = new Polygon2D
        {
            Antialiased = true,
            Color = Colors.White,
            ZIndex = 9
        };
        AddChild(_shipShockwave);

        _mouthShockwave = new Polygon2D
        {
            Antialiased = true,
            Color = Colors.White,
            ZIndex = 10
        };
        AddChild(_mouthShockwave);

        var image = Image.CreateEmpty(TextureUvMax, TextureUvMax, false, Image.Format.Rgba8);
        image.Fill(Colors.White);
        _whiteTexture = ImageTexture.CreateFromImage(image);
        _lens.Texture = _whiteTexture;
        _sheath.Texture = _whiteTexture;
        _stars.Texture = _whiteTexture;
        _strip.Texture = _whiteTexture;
        _mouth.Texture = _whiteTexture;
        _aperture.Texture = _whiteTexture;
        _residualRift.Texture = _whiteTexture;
        _shipShockwave.Texture = _whiteTexture;
        _mouthShockwave.Texture = _whiteTexture;

        var lensShader = ResourceLoader.Load<Shader>(LensShaderPath);
        if (lensShader is not null)
        {
            _lensMaterial = new ShaderMaterial { Shader = lensShader };
            _lens.Material = _lensMaterial;
        }

        var sheathShader = ResourceLoader.Load<Shader>(SheathShaderPath);
        if (sheathShader is not null)
        {
            _sheathMaterial = new ShaderMaterial { Shader = sheathShader };
            _sheath.Material = _sheathMaterial;
        }

        var starsShader = ResourceLoader.Load<Shader>(StarsShaderPath);
        if (starsShader is not null)
        {
            _starsMaterial = new ShaderMaterial { Shader = starsShader };
            _stars.Material = _starsMaterial;
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

        var apertureShader = ResourceLoader.Load<Shader>(ApertureShaderPath);
        if (apertureShader is not null)
        {
            _apertureMaterial = new ShaderMaterial { Shader = apertureShader };
            _aperture.Material = _apertureMaterial;
        }

        var residualShader = ResourceLoader.Load<Shader>(ResidualShaderPath);
        if (residualShader is not null)
        {
            _residualRiftMaterial = new ShaderMaterial { Shader = residualShader };
            _residualRift.Material = _residualRiftMaterial;
        }

        var shockwaveShader = ResourceLoader.Load<Shader>(ShockwaveShaderPath);
        if (shockwaveShader is not null)
        {
            _shipShockwaveMaterial = new ShaderMaterial { Shader = shockwaveShader };
            _shipShockwave.Material = _shipShockwaveMaterial;
            _mouthShockwaveMaterial = new ShaderMaterial { Shader = shockwaveShader };
            _mouthShockwave.Material = _mouthShockwaveMaterial;
        }

        UpdateGeometry();
        ApplyMaterial();
        SetProcess(false);
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
        SetProcess(true);
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
        SetProcess(true);
        UpdateGeometry();
        ApplyMaterial();
    }

    public void Stop()
    {
        Active = false;
        Visible = false;
        SetProcess(false);
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
            stripUv[i] = new Vector2(0f, t * TextureUvMax);
            var rightIndex = stripPolygon.Length - 1 - i;
            stripPolygon[rightIndex] = center + right * width * 0.5f;
            stripUv[rightIndex] = new Vector2(TextureUvMax, t * TextureUvMax);
        }

        _strip.Polygon = stripPolygon;
        _strip.UV = stripUv;
        _sheath.Polygon = stripPolygon;
        _sheath.UV = stripUv;
        _stars.Polygon = stripPolygon;
        _stars.UV = stripUv;

        var radius = Math.Clamp(farWidth * 0.56f, 210f, 310f);
        SetCirclePolygon(_lens, portalCenter, Math.Clamp(radius * 1.72f, 390f, 560f), 96);
        SetCirclePolygon(_mouth, portalCenter, radius, 72);
        SetCirclePolygon(_aperture, portalCenter, Math.Clamp(radius * 0.88f, 190f, 290f), 72);
        SetCirclePolygon(_residualRift, portalCenter, Math.Clamp(radius * 0.76f, 170f, 250f), 72);
        SetCirclePolygon(_shipShockwave, Vector2.Zero, Math.Clamp(nearWidth * 1.28f, 180f, 260f), 64);
        SetCirclePolygon(_mouthShockwave, portalCenter, Math.Clamp(radius * 1.22f, 260f, 390f), 80);
    }

    private void ApplyMaterial()
    {
        ApplyCommonMaterial(_lensMaterial);
        ApplyCommonMaterial(_sheathMaterial);
        ApplyCommonMaterial(_stripMaterial);
        ApplyCommonMaterial(_starsMaterial);
        ApplyCommonMaterial(_mouthMaterial);
        ApplyCommonMaterial(_apertureMaterial);
        ApplyCommonMaterial(_residualRiftMaterial);
        ApplyShockwaveMaterial(_shipShockwaveMaterial, 0f);
        ApplyShockwaveMaterial(_mouthShockwaveMaterial, 1f);
    }

    private void ApplyCommonMaterial(ShaderMaterial? material)
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
        material.SetShaderParameter("residual_level", _residualActive ? 1f : 0f);
    }

    private void ApplyShockwaveMaterial(ShaderMaterial? material, float shockKind)
    {
        ApplyCommonMaterial(material);
        material?.SetShaderParameter("shock_kind", shockKind);
    }

    private static void SetCirclePolygon(Polygon2D polygon, Vector2 center, float radius, int segments)
    {
        var points = new Vector2[segments];
        var uvs = new Vector2[segments];
        for (var i = 0; i < segments; i++)
        {
            var angle = i / (float)segments * MathF.Tau;
            var unit = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            points[i] = center + unit * radius;
            uvs[i] = new Vector2(
                (0.5f + unit.X * 0.5f) * TextureUvMax,
                (0.5f + unit.Y * 0.5f) * TextureUvMax);
        }

        polygon.Polygon = points;
        polygon.UV = uvs;
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
