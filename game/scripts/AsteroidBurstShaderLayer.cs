using Godot;
using SpaceRangers.Core;

namespace SpaceRangersPrototype;

public enum AsteroidBurstLayerRole
{
    Heat,
    Dust
}

public partial class AsteroidBurstShaderLayer : Node2D
{
    private readonly AsteroidBurstLayerRole _role;
    private ShaderMaterial? _material;
    private AsteroidEventState _asteroidEvent;
    private AsteroidDebrisResources? _resources;
    private float _lifetime = 1f;
    private float _age;
    private bool _configured;

    public AsteroidBurstShaderLayer()
        : this(AsteroidBurstLayerRole.Dust)
    {
    }

    public AsteroidBurstShaderLayer(AsteroidBurstLayerRole role)
    {
        _role = role;
    }

    public void Configure(AsteroidEventState asteroidEvent, AsteroidDebrisResources resources, float lifetime)
    {
        _asteroidEvent = asteroidEvent;
        _resources = resources;
        _lifetime = Math.Max(0.001f, lifetime);
        _configured = true;
    }

    public override void _Ready()
    {
        if (!_configured || _resources is null)
        {
            Visible = false;
            return;
        }

        ZIndex = _role == AsteroidBurstLayerRole.Heat ? 0 : 1;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;

        var shader = _role == AsteroidBurstLayerRole.Heat ? _resources.HeatShader : _resources.BurstShader;
        if (shader is not null)
        {
            _material = new ShaderMaterial { Shader = shader };
            Material = _material;
        }
    }

    public override void _ExitTree()
    {
        Material = null;
        _material = null;
        _resources = null;
    }

    public void SetAge(float age)
    {
        _age = age;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var material = _material;
        if (material is null)
        {
            return;
        }

        var t = Math.Clamp(_age / _lifetime, 0f, 1f);
        var isSunBurn = _asteroidEvent.Type == AsteroidEventType.SunBurn;
        var isShipImpact = _asteroidEvent.Type == AsteroidEventType.ShipImpact;
        var radius = Math.Clamp(_asteroidEvent.Radius, 14f, 170f);
        var heat = Math.Clamp(_asteroidEvent.Heat, isSunBurn ? 0.82f : 0f, 1f);
        var energy = isSunBurn ? 1.18f : isShipImpact ? 0.72f : 0.66f;
        var sizeFactor = _role == AsteroidBurstLayerRole.Heat
            ? isSunBurn ? 5.55f : 1.55f
            : isSunBurn ? 3.85f : 5.85f;
        var fade = 1f - SmoothStep(0.72f, 1f, t);
        var alphaScale = _role == AsteroidBurstLayerRole.Heat
            ? (isSunBurn ? 1.02f : 0.18f) * fade
            : (isSunBurn ? 0.42f : 1.10f) * fade;

        var rock = VariantColor(_asteroidEvent.Variant);
        var hot = isSunBurn
            ? new Color(1f, 0.90f, 0.33f, 1f)
            : new Color(0.78f, 0.58f, 0.38f, 1f);
        var mid = isSunBurn
            ? new Color(1f, 0.24f + heat * 0.10f, 0.025f, 1f)
            : new Color(0.43f, 0.32f, 0.23f, 1f);
        var dust = isSunBurn
            ? new Color(0.43f, 0.25f, 0.13f, 1f).Lerp(rock, 0.20f)
            : rock.Lerp(new Color(0.34f, 0.31f, 0.27f, 1f), 0.48f);
        var dark = isSunBurn
            ? new Color(0.10f, 0.06f, 0.04f, 1f)
            : new Color(0.055f, 0.052f, 0.048f, 1f);

        material.SetShaderParameter("progress", t);
        material.SetShaderParameter("seed", _asteroidEvent.Seed * 0.0013f + _asteroidEvent.Variant * 0.217f);
        material.SetShaderParameter("sun_burn", isSunBurn ? 1f : 0f);
        material.SetShaderParameter("alpha_scale", alphaScale);
        material.SetShaderParameter("energy", energy);
        material.SetShaderParameter("hot_color", hot);
        material.SetShaderParameter("mid_color", mid);
        material.SetShaderParameter("dust_color", dust);
        material.SetShaderParameter("dark_color", dark);

        var size = Vector2.One * radius * sizeFactor;
        DrawRect(new Rect2(-size * 0.5f, size), Colors.White, true);
    }

    private static Color VariantColor(int variant)
    {
        return (Math.Abs(variant) % 4) switch
        {
            0 => new Color(0.58f, 0.54f, 0.48f, 1f),
            1 => new Color(0.34f, 0.33f, 0.32f, 1f),
            2 => new Color(0.42f, 0.50f, 0.54f, 1f),
            _ => new Color(0.47f, 0.28f, 0.25f, 1f)
        };
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
