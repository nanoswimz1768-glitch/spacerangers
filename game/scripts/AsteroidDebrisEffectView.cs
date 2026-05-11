using Godot;
using SpaceManagers.Core;

namespace SpaceManagersPrototype;

public partial class AsteroidDebrisEffectView : Node2D
{
    private const float RockLifetime = 2.85f;
    private const float SunBurnLifetime = 2.45f;

    private AsteroidEventState _asteroidEvent;
    private AsteroidDebrisResources? _resources;
    private AsteroidBurstShaderLayer? _heatLayer;
    private AsteroidBurstShaderLayer? _dustLayer;
    private AsteroidFragmentLayer? _fragmentLayer;
    private float _age;
    private float _lifetime = RockLifetime;
    private bool _configured;

    public bool IsFinished => _age >= _lifetime;

    public void Configure(AsteroidEventState asteroidEvent, AsteroidDebrisResources resources)
    {
        _asteroidEvent = asteroidEvent;
        _resources = resources;
        _lifetime = asteroidEvent.Type == AsteroidEventType.SunBurn ? SunBurnLifetime : RockLifetime;
        Position = asteroidEvent.Position.ToGodot();
        _configured = true;
    }

    public override void _Ready()
    {
        if (!_configured || _resources is null)
        {
            QueueFree();
            return;
        }

        ZIndex = 0;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;

        _heatLayer = new AsteroidBurstShaderLayer(AsteroidBurstLayerRole.Heat);
        _heatLayer.Configure(_asteroidEvent, _resources, _lifetime);
        AddChild(_heatLayer);

        _dustLayer = new AsteroidBurstShaderLayer(AsteroidBurstLayerRole.Dust);
        _dustLayer.Configure(_asteroidEvent, _resources, _lifetime);
        AddChild(_dustLayer);

        _fragmentLayer = new AsteroidFragmentLayer();
        _fragmentLayer.Configure(_asteroidEvent, _resources, _lifetime);
        AddChild(_fragmentLayer);

        UpdateChildren();
    }

    public override void _ExitTree()
    {
        _heatLayer = null;
        _dustLayer = null;
        _fragmentLayer = null;
        _resources = null;
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= _lifetime)
        {
            QueueFree();
            return;
        }

        UpdateChildren();
    }

    private void UpdateChildren()
    {
        _heatLayer?.SetAge(_age);
        _dustLayer?.SetAge(_age);
        _fragmentLayer?.SetAge(_age);
    }
}
