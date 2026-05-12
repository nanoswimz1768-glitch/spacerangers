using Godot;

namespace SpaceManagersPrototype;

public enum ShipEffectQuality
{
    Full,
    Balanced,
    Minimal,
    Hidden
}

public partial class ShipView : Node2D
{
    private const string WarpChargeAuraShaderPath = "res://shaders/ship_warp_charge_aura.gdshader";
    private const float IdleRollRadians = 0.052f;
    private const float IdleHoverPixels = 1.6f;
    private const float RigWingBankOffset = 0f;

    private static readonly EnginePort[] DefaultExhaustPorts =
    {
        new(new Vector2(-25f, 94f), 11f),
        new(new Vector2(25f, 94f), 11f)
    };

    private static int _nextIdleSeed;

    private float _phase;
    private Sprite2D _glowSprite = null!;
    private Sprite2D _warpChargeAura = null!;
    private Sprite2D _shipSprite = null!;
    private Sprite2D _leftWingRigSprite = null!;
    private Sprite2D _rightWingRigSprite = null!;
    private ShipIdleOverlay _idleOverlay = null!;
    private Texture2D? _pendingTexture;
    private ShipRigProfile _rigProfile = ShipRigProfile.Empty;
    private ShaderMaterial? _warpChargeAuraMaterial;
    private float _textureEffectScale = 1f;
    private readonly EnginePort[] _renderPorts = new EnginePort[4];
    private ShipEffectQuality _effectQuality = ShipEffectQuality.Full;
    private float _redrawAccumulator;
    private readonly float _idleSeed = NextIdleSeed();
    private Vector2 _idleVisualOffset;
    private Vector2 _idleVisualScale = Vector2.One;
    private float _idleVisualRotation;
    private float _idleBankAmount;
    private float _idlePulse;
    private float _idleActivity;

    public Vector2 Velocity { get; set; }
    public Vector2 AimDirection { get; set; } = Vector2.Up;
    public float ThrustLevel { get; set; }
    public float ReverseLevel { get; set; }
    public float StrafeLevel { get; set; }
    public float AfterburnerLevel { get; set; }
    public bool IsFiring { get; set; }
    public bool ShowHitbox { get; set; }
    public bool IdleAnimationEnabled { get; set; } = true;
    public Vector2 HitboxLocalCenter { get; set; } = Vector2.Zero;
    public Vector2 HitboxLocalSize { get; set; } = new(84f, 84f);
    public Color EngineOuterColor { get; set; } = new(0.1f, 0.8f, 1f, 1f);
    public Color EngineCoreColor { get; set; } = new(0.75f, 1f, 1f, 1f);
    public Color WarpOuterColor { get; set; } = new(0.16f, 0.72f, 1f, 1f);
    public Color WarpCoreColor { get; set; } = new(0.86f, 1f, 1f, 1f);
    public float EngineEffectScale { get; set; } = 1f;
    public float EngineBubbleScale { get; set; } = 1f;
    public float EngineParticleDensity { get; set; } = 1f;
    public Texture2D? EnginePlumeTexture { get; set; }
    public float WarpChargeLevel { get; set; }
    public bool WarpChargeActive { get; set; }
    public float WarpTransitLevel { get; set; }
    public IReadOnlyList<EnginePort> ExhaustPorts { get; set; } = Array.Empty<EnginePort>();
    public ShipRigProfile RigProfile
    {
        get => _rigProfile;
        set
        {
            _rigProfile = value ?? ShipRigProfile.Empty;
            UpdateRigSprites();
            UpdateIdleOverlay();
        }
    }

    public ShipEffectQuality EffectQuality
    {
        get => _effectQuality;
        set
        {
            if (_effectQuality == value)
            {
                return;
            }

            _effectQuality = value;
            ApplyIdleAnimation();
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        ZIndex = 15;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;

        _glowSprite = new Sprite2D
        {
            Centered = true,
            Modulate = new Color(0f, 0.86f, 1f, 0.2f),
            Scale = new Vector2(1.14f, 1.14f),
            ZIndex = 0
        };
        _glowSprite.TextureFilter = TextureFilterEnum.LinearWithMipmaps;

        _warpChargeAura = new Sprite2D
        {
            Centered = true,
            Modulate = Colors.White,
            Visible = false,
            ZIndex = 2
        };
        _warpChargeAura.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        var warpAuraShader = ResourceLoader.Load<Shader>(WarpChargeAuraShaderPath);
        if (warpAuraShader is not null)
        {
            _warpChargeAuraMaterial = new ShaderMaterial { Shader = warpAuraShader };
            _warpChargeAura.Material = _warpChargeAuraMaterial;
        }

        _shipSprite = new Sprite2D
        {
            Centered = true,
            Modulate = Colors.White,
            ZIndex = 3
        };
        _shipSprite.TextureFilter = TextureFilterEnum.LinearWithMipmaps;

        _leftWingRigSprite = CreateRigRegionSprite();
        _rightWingRigSprite = CreateRigRegionSprite();

        _idleOverlay = new ShipIdleOverlay
        {
            Visible = false,
            ZIndex = 4
        };

        AddChild(_glowSprite);
        AddChild(_warpChargeAura);
        AddChild(_leftWingRigSprite);
        AddChild(_rightWingRigSprite);
        AddChild(_shipSprite);
        AddChild(_idleOverlay);
        UpdateRigSprites();

        if (_pendingTexture is not null)
        {
            SetShipTexture(_pendingTexture);
            _pendingTexture = null;
        }
    }

    public void SetShipTexture(Texture2D texture)
    {
        if (_shipSprite is null || _glowSprite is null)
        {
            _pendingTexture = texture;
            return;
        }

        _shipSprite.Texture = texture;
        _glowSprite.Texture = texture;
        _warpChargeAura.Texture = texture;
        _leftWingRigSprite.Texture = texture;
        _rightWingRigSprite.Texture = texture;
        _textureEffectScale = Math.Clamp(Math.Max(texture.GetWidth(), texture.GetHeight()) / 256f, 1f, 4f);
        _glowSprite.Modulate = WithAlpha(EngineOuterColor, 0.18f);
        UpdateRigSprites();
    }

    public override void _Process(double delta)
    {
        _phase += (float)delta * 12f;
        ApplyIdleAnimation();
        UpdateWarpChargeAura();
        _redrawAccumulator += (float)delta;
        if (ShouldRedrawEffects())
        {
            _redrawAccumulator = 0f;
            QueueRedraw();
            _idleOverlay?.QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (EffectQuality != ShipEffectQuality.Hidden)
        {
            DrawSetTransform(_idleVisualOffset, _idleVisualRotation, _idleVisualScale);
            DrawEngineGlow();
            if (EffectQuality == ShipEffectQuality.Full || (EffectQuality == ShipEffectQuality.Balanced && IsFiring))
            {
                DrawTurretCue();
            }

            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }

        DrawDebugHitbox();
    }

    private bool ShouldRedrawEffects()
    {
        if (ShowHitbox || EffectQuality == ShipEffectQuality.Full)
        {
            return true;
        }

        if (EffectQuality == ShipEffectQuality.Hidden)
        {
            return false;
        }

        var hasAnimatedEffect = IdleAnimationEnabled
            || ThrustLevel > 0.01f
            || ReverseLevel > 0.01f
            || Math.Abs(StrafeLevel) > 0.01f
            || AfterburnerLevel > 0.01f
            || WarpChargeLevel > 0.01f
            || WarpTransitLevel > 0.01f
            || IsFiring;
        if (!hasAnimatedEffect)
        {
            return false;
        }

        var interval = EffectQuality switch
        {
            ShipEffectQuality.Balanced => 1f / 30f,
            ShipEffectQuality.Minimal => 1f / 15f,
            _ => 1f / 18f
        };
        return _redrawAccumulator >= interval;
    }

    private void ApplyIdleAnimation()
    {
        if (_shipSprite is null || _glowSprite is null)
        {
            return;
        }

        var quality = IdleQualityMultiplier();
        if (!IdleAnimationEnabled || quality <= 0f || _shipSprite.Texture is null)
        {
            ResetIdleAnimation();
            return;
        }

        var t = _phase + _idleSeed;
        var speed = Math.Clamp(Velocity.Length() / 520f, 0f, 1f);
        var thrust = Math.Clamp(Math.Max(ThrustLevel, ReverseLevel), 0f, 1f);
        var strafe = Math.Clamp(Math.Abs(StrafeLevel), 0f, 1f);
        var afterburner = Math.Clamp(AfterburnerLevel, 0f, 1f);
        _idleActivity = Math.Clamp(0.18f + speed * 0.20f + thrust * 0.24f + strafe * 0.14f + afterburner * 0.52f + (IsFiring ? 0.14f : 0f), 0f, 1.35f);

        var animationLevel = (0.78f + _idleActivity * 0.28f) * quality;
        var bankPrimary = MathF.Sin(t * 0.128f);
        var bankSecondary = MathF.Sin(t * 0.073f + 1.7f) * 0.34f;
        _idleBankAmount = Math.Clamp((bankPrimary + bankSecondary + StrafeLevel * 0.28f) * animationLevel, -1f, 1f);
        _idleVisualRotation = _idleBankAmount * IdleRollRadians;

        var hoverScale = Math.Clamp(_textureEffectScale, 1f, 2.2f);
        _idleVisualOffset = new Vector2(
            MathF.Sin(t * 0.071f + 2.1f) * IdleHoverPixels * 0.22f * hoverScale,
            MathF.Sin(t * 0.094f + 0.4f) * IdleHoverPixels * 0.55f * hoverScale) * animationLevel;

        var perspectiveNarrow = 1f - Math.Abs(_idleBankAmount) * 0.006f;
        var bodyPulse = MathF.Sin(t * 0.111f + 2.6f) * 0.002f * animationLevel;
        _idleVisualScale = new Vector2(
            perspectiveNarrow,
            1f + bodyPulse);

        _idlePulse = 0.5f + MathF.Sin(t * 0.172f + MathF.Sin(t * 0.051f)) * 0.5f;

        _shipSprite.Position = _idleVisualOffset;
        _shipSprite.Rotation = _idleVisualRotation;
        _shipSprite.Scale = _idleVisualScale;

        var glowBreath = 1f + _idlePulse * 0.035f + _idleActivity * 0.018f;
        var glowBankStretch = 1f + Math.Abs(_idleBankAmount) * 0.018f;
        _glowSprite.Position = _idleVisualOffset * 0.82f;
        _glowSprite.Rotation = _idleVisualRotation * 0.72f;
        _glowSprite.Scale = new Vector2(1.14f * glowBreath * glowBankStretch, 1.14f * glowBreath);
        _glowSprite.Modulate = WithAlpha(EngineOuterColor, 0.095f + _idlePulse * 0.018f + _idleActivity * 0.012f);

        UpdateRigSprites();
        UpdateIdleOverlay();
    }

    private void ResetIdleAnimation()
    {
        _idleVisualOffset = Vector2.Zero;
        _idleVisualScale = Vector2.One;
        _idleVisualRotation = 0f;
        _idleBankAmount = 0f;
        _idlePulse = 0f;
        _idleActivity = 0f;

        _shipSprite.Position = Vector2.Zero;
        _shipSprite.Rotation = 0f;
        _shipSprite.Scale = Vector2.One;
        _glowSprite.Position = Vector2.Zero;
        _glowSprite.Rotation = 0f;
        _glowSprite.Scale = new Vector2(1.14f, 1.14f);
        _glowSprite.Modulate = WithAlpha(EngineOuterColor, 0.18f);
        UpdateRigSprites();

        if (_idleOverlay is not null)
        {
            _idleOverlay.Visible = false;
        }
    }

    private void UpdateIdleOverlay()
    {
        if (_idleOverlay is null)
        {
            return;
        }

        var visible = IdleAnimationEnabled && EffectQuality != ShipEffectQuality.Hidden && _shipSprite.Texture is not null;
        _idleOverlay.Visible = visible;
        _idleOverlay.Position = _idleVisualOffset;
        _idleOverlay.Rotation = _idleVisualRotation;
        _idleOverlay.Scale = _idleVisualScale;
        _idleOverlay.Phase = _phase + _idleSeed;
        _idleOverlay.Pulse = _idlePulse;
        _idleOverlay.Activity = _idleActivity;
        _idleOverlay.BankAmount = _idleBankAmount;
        _idleOverlay.TextureEffectScale = _textureEffectScale;
        _idleOverlay.EngineOuterColor = EngineOuterColor;
        _idleOverlay.EngineCoreColor = EngineCoreColor;
        _idleOverlay.HitboxLocalCenter = HitboxLocalCenter;
        _idleOverlay.HitboxLocalSize = HitboxLocalSize;
        _idleOverlay.RigProfile = _rigProfile;
        _idleOverlay.EffectQuality = EffectQuality;
    }

    private float IdleQualityMultiplier()
    {
        return EffectQuality switch
        {
            ShipEffectQuality.Full => 1f,
            ShipEffectQuality.Balanced => 0.76f,
            ShipEffectQuality.Minimal => 0.46f,
            _ => 0f
        };
    }

    private void UpdateRigSprites()
    {
        if (_leftWingRigSprite is null || _rightWingRigSprite is null)
        {
            return;
        }

        var texture = _shipSprite?.Texture;
        var visible = false;
        UpdateRigWingSprite(_leftWingRigSprite, _rigProfile.LeftWingRegion, _rigProfile.LeftWingCenter, -1f, visible);
        UpdateRigWingSprite(_rightWingRigSprite, _rigProfile.RightWingRegion, _rigProfile.RightWingCenter, 1f, visible);
    }

    private void UpdateRigWingSprite(Sprite2D sprite, Rect2 region, Vector2 center, float side, bool visible)
    {
        if (!visible || region.Size.X <= 1f || region.Size.Y <= 1f)
        {
            sprite.Visible = false;
            return;
        }

        var sideBank = _idleBankAmount * side;
        var bankAbs = Math.Abs(_idleBankAmount);
        var lift = -sideBank * RigWingBankOffset * _textureEffectScale;
        var spread = side * bankAbs * 1.2f * _textureEffectScale;
        var alpha = RigWingAlpha() * (0.14f + bankAbs * 0.86f);
        var tintAmount = sideBank > 0f ? 0.20f + bankAbs * 0.18f : 0.06f;
        var tint = sideBank > 0f
            ? Mix(Colors.White, EngineCoreColor, tintAmount)
            : Mix(Colors.White, new Color(0.48f, 0.72f, 0.78f, 1f), tintAmount);

        sprite.Visible = true;
        sprite.Texture = _shipSprite.Texture;
        sprite.RegionEnabled = true;
        sprite.RegionRect = region;
        sprite.Position = _idleVisualOffset + center + new Vector2(spread, lift);
        sprite.Rotation = _idleVisualRotation + sideBank * 0.026f;
        sprite.Scale = new Vector2(
            _idleVisualScale.X * (1f + Math.Max(0f, sideBank) * 0.010f),
            _idleVisualScale.Y * (1f - Math.Min(0f, sideBank) * 0.006f));
        sprite.Modulate = WithAlpha(tint, alpha);
    }

    private static Sprite2D CreateRigRegionSprite()
    {
        var sprite = new Sprite2D
        {
            Centered = true,
            RegionEnabled = true,
            Visible = false,
            ZIndex = 2
        };
        sprite.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        return sprite;
    }

    private float RigWingAlpha()
    {
        return EffectQuality switch
        {
            ShipEffectQuality.Full => 0.0f,
            ShipEffectQuality.Balanced => 0.0f,
            ShipEffectQuality.Minimal => 0.0f,
            _ => 0f
        };
    }

    private void DrawEngineGlow()
    {
        var flicker = 0.94f + MathF.Sin(_phase * 1.1f) * 0.045f + MathF.Sin(_phase * 2.25f) * 0.025f;
        if (ThrustLevel > 0.01f)
        {
            DrawMainThrust(ThrustLevel, flicker);
        }

        if (ReverseLevel > 0.01f)
        {
            var reverse = Math.Clamp(ReverseLevel, 0f, 1f);
            var outer = WithAlpha(EngineOuterColor, 0.34f * reverse);
            var core = WithAlpha(EngineCoreColor, 0.56f * reverse);
            DrawLine(new Vector2(-18f, -82f), new Vector2(-18f, -112f - 12f * flicker), outer, 6.2f, true);
            DrawLine(new Vector2(-18f, -84f), new Vector2(-18f, -105f - 9f * flicker), core, 2.2f, true);
            DrawLine(new Vector2(18f, -82f), new Vector2(18f, -112f - 12f * flicker), outer, 6.2f, true);
            DrawLine(new Vector2(18f, -84f), new Vector2(18f, -105f - 9f * flicker), core, 2.2f, true);
        }

        if (Math.Abs(StrafeLevel) > 0.01f)
        {
            var side = Math.Sign(StrafeLevel);
            var strafe = Math.Clamp(Math.Abs(StrafeLevel), 0f, 1f);
            DrawLine(new Vector2(-side * 54f, 22f), new Vector2(-side * (90f + 10f * flicker), 22f), WithAlpha(EngineOuterColor, 0.46f * strafe), 6f, true);
            DrawLine(new Vector2(-side * 48f, -12f), new Vector2(-side * (76f + 8f * flicker), -12f), WithAlpha(EngineCoreColor, 0.42f * strafe), 2.6f, true);
        }
    }

    private void UpdateWarpChargeAura()
    {
        if (_warpChargeAura is null)
        {
            return;
        }

        var charge = Math.Clamp(WarpChargeLevel, 0f, 1f);
        var transit = Math.Clamp(WarpTransitLevel, 0f, 1f);
        var intensity = Math.Max(charge * (WarpChargeActive ? 1f : 0.38f), transit);
        var quality = EffectQuality switch
        {
            ShipEffectQuality.Full => 1f,
            ShipEffectQuality.Balanced => 0.72f,
            ShipEffectQuality.Minimal => 0.42f,
            _ => 0f
        };

        intensity *= quality;
        var texture = _shipSprite?.Texture;
        var material = _warpChargeAuraMaterial;
        var visible = material is not null
            && texture is not null
            && intensity > 0.01f;
        _warpChargeAura.Visible = visible;
        if (!visible)
        {
            return;
        }

        var shipTexture = texture!;
        _warpChargeAura.Position = _idleVisualOffset;
        _warpChargeAura.Rotation = _idleVisualRotation;
        _warpChargeAura.Scale = _idleVisualScale * (1.016f + charge * 0.010f + transit * 0.014f);

        var ready = charge >= 0.985f && WarpChargeActive ? 1f : 0f;
        material!.SetShaderParameter("time_seconds", _phase / 12f);
        material.SetShaderParameter("charge", charge);
        material.SetShaderParameter("intensity", intensity);
        material.SetShaderParameter("transit", transit);
        material.SetShaderParameter("ready", ready);
        material.SetShaderParameter("ship_texture", shipTexture);
        material.SetShaderParameter(
            "ship_texture_pixel_size",
            new Vector2(1f / Math.Max(1f, shipTexture.GetWidth()), 1f / Math.Max(1f, shipTexture.GetHeight())));
        material.SetShaderParameter("outer_color", WarpOuterColor);
        material.SetShaderParameter("core_color", Mix(WarpOuterColor, WarpCoreColor, 0.30f + transit * 0.12f));
    }

    private void DrawMainThrust(float level, float flicker)
    {
        var intensity = Math.Clamp(level, 0f, 1f);
        var afterburner = Math.Clamp(AfterburnerLevel, 0f, 1f);
        var lengthBoost = 1f + afterburner;
        var widthBoost = 1f + afterburner * 0.56f;
        var glowBoost = 1f + afterburner * 0.72f;
        var effectScale = _textureEffectScale * Math.Clamp(EngineEffectScale, 0.35f, 2.5f);
        var length = (128f + 72f * intensity) * intensity * flicker * lengthBoost * effectScale;
        var portCount = SelectRenderablePorts();
        var usesTexturedPlume = EnginePlumeTexture is not null;

        var clusterMinX = float.PositiveInfinity;
        var clusterMaxX = float.NegativeInfinity;
        var clusterY = 0f;
        for (var i = 0; i < portCount; i++)
        {
            var port = _renderPorts[i];
            clusterMinX = Math.Min(clusterMinX, port.Position.X);
            clusterMaxX = Math.Max(clusterMaxX, port.Position.X);
            clusterY += port.Position.Y;
        }

        clusterY /= Math.Max(1, portCount);
        var clusterWidth = Math.Max(30f * effectScale, clusterMaxX - clusterMinX + 32f * effectScale) * widthBoost;
        var clusterCenter = new Vector2((clusterMinX + clusterMaxX) * 0.5f, clusterY);
        var hasCentralPort = HasCentralRenderPort(portCount, clusterCenter.X, Math.Max(8f * effectScale, clusterWidth * 0.13f));
        var isolateSeparatedPorts = portCount > 1 && !hasCentralPort;

        for (var i = 0; i < portCount; i++)
        {
            var port = _renderPorts[i];
            var nozzle = port.Position;
            var bubbleScale = Math.Clamp(EngineBubbleScale, 0f, 1.5f);
            var nozzleBubbleScale = usesTexturedPlume ? Math.Clamp(bubbleScale, 0.12f, 0.34f) : Math.Clamp(bubbleScale, 0.18f, 1.5f);
            var radius = Math.Clamp(port.Radius * Math.Clamp(EngineEffectScale, 0.35f, 2.5f), 6f, 64f);
            var portPhase = _phase + nozzle.X * 0.061f + nozzle.Y * 0.025f;
            var sway = MathF.Sin(portPhase * 1.7f) * (2.8f + radius * 0.08f);
            var portLength = length * (0.88f + MathF.Sin(portPhase) * 0.12f);
            var halfWidth = radius * (0.64f + intensity * 0.52f) * widthBoost;
            var seed = nozzle.X * 12.9898f + nozzle.Y * 78.233f + radius * 19.19f;
            var coreNozzleScale = usesTexturedPlume ? 0.62f : MathF.Max(0.52f, nozzleBubbleScale);

            DrawCircle(nozzle + new Vector2(0f, 7f * effectScale), radius * (1.9f + intensity * 0.92f) * widthBoost * nozzleBubbleScale, WithAlpha(EngineOuterColor, 0.16f * intensity * glowBoost * nozzleBubbleScale));
            DrawCircle(nozzle + new Vector2(0f, 4f * effectScale), radius * 1.12f * widthBoost * nozzleBubbleScale, WithAlpha(Mix(EngineOuterColor, EngineCoreColor, 0.58f), 0.42f * intensity * glowBoost * nozzleBubbleScale));
            DrawCircle(nozzle + new Vector2(0f, 1.5f * effectScale), radius * 0.56f * widthBoost * coreNozzleScale, WithAlpha(EngineCoreColor, 0.92f * intensity * MathF.Max(0.68f, nozzleBubbleScale)));
            if (afterburner > 0.01f && !usesTexturedPlume)
            {
                DrawCircle(nozzle + new Vector2(0f, 11f * effectScale), radius * 3.4f * nozzleBubbleScale, WithAlpha(Mix(EngineCoreColor, Colors.White, 0.38f), 0.18f * intensity * afterburner * nozzleBubbleScale));
            }

            if (usesTexturedPlume)
            {
                DrawTexturedPlasmaPlume(nozzle, portLength, isolateSeparatedPorts ? halfWidth * 0.58f : halfWidth, sway, Math.Clamp(intensity * glowBoost, 0f, 1.7f), seed);
            }
            else
            {
                DrawPlasmaPlume(nozzle, portLength, isolateSeparatedPorts ? halfWidth * 0.62f : halfWidth, sway, Math.Clamp(intensity * glowBoost, 0f, 1.7f), seed);
            }
        }

        if (portCount == 1 || hasCentralPort)
        {
            var emberCount = DetailCount(afterburner > 0.01f ? 12 : 7, afterburner > 0.01f ? 5 : 3, afterburner > 0.01f ? 2 : 1);
            for (var i = 0; i < emberCount; i++)
            {
                var offset = MathF.Sin(_phase * 1.8f + i * 1.7f) * clusterWidth * 0.22f;
                var y = clusterY + 18f * effectScale + i * (afterburner > 0.01f ? 8f : 12f) * effectScale + length * 0.16f;
                DrawCircle(new Vector2(clusterCenter.X + offset, y), (1.5f + intensity * (1.2f + afterburner * 1.1f)) * effectScale, WithAlpha(EngineCoreColor, 0.16f * intensity * glowBoost));
            }
        }
    }

    private void DrawTexturedPlasmaPlume(Vector2 nozzle, float length, float halfWidth, float sway, float intensity, float seed)
    {
        if (EnginePlumeTexture is not { } texture)
        {
            return;
        }

        var afterburner = Math.Clamp(AfterburnerLevel, 0f, 1f);
        var shimmer = 0.94f + MathF.Sin(_phase * 1.35f + seed * 0.017f) * 0.05f;
        var width = Math.Clamp(halfWidth * (1.44f + afterburner * 0.18f), 15f, 82f);
        var height = Math.Clamp(length * (0.88f + afterburner * 0.09f) * shimmer, 50f, 238f);
        var y = nozzle.Y + Math.Max(5f, halfWidth * 0.24f);
        var x = nozzle.X - width * 0.5f + sway * 0.08f;
        var outer = new Rect2(new Vector2(x, y), new Vector2(width, height));
        DrawTextureRect(texture, outer, false, WithAlpha(EngineOuterColor, 0.82f * intensity));

        var coreWidth = width * 0.52f;
        var coreHeight = height * 0.82f;
        var core = new Rect2(
            new Vector2(nozzle.X - coreWidth * 0.5f + sway * 0.04f, y + height * 0.02f),
            new Vector2(coreWidth, coreHeight));
        DrawTextureRect(texture, core, false, WithAlpha(Mix(EngineCoreColor, Colors.White, 0.16f), 0.55f * intensity));

        DrawLine(
            nozzle + new Vector2(sway * 0.04f, Math.Max(2f, halfWidth * 0.10f)),
            nozzle + new Vector2(sway * 0.30f, Math.Min(height * 0.62f, 128f)),
            WithAlpha(Mix(EngineCoreColor, Colors.White, 0.18f), 0.44f * intensity),
            Math.Clamp(width * 0.070f, 1.2f, 5.0f),
            true);

        DrawTexturedPlumeParticles(nozzle, height, width, sway, intensity, seed);
    }

    private void DrawTexturedPlumeParticles(Vector2 nozzle, float length, float width, float sway, float intensity, float seed)
    {
        var afterburner = Math.Clamp(AfterburnerLevel, 0f, 1f);
        var count = DetailCount(afterburner > 0.01f ? 18 : 12, afterburner > 0.01f ? 8 : 5, afterburner > 0.01f ? 4 : 2);
        for (var i = 0; i < count; i++)
        {
            var h0 = Hash01(seed + i * 31.17f);
            var h1 = Hash01(seed + i * 53.41f);
            var h2 = Hash01(seed + i * 77.71f);
            var life = Fract(h0 + _phase * (0.032f + h2 * 0.034f + afterburner * 0.016f));
            var fade = MathF.Pow(1f - life, 1.42f);
            var spread = width * (0.16f + life * 0.34f);
            var x = HashSigned(seed + i * 19.37f) * spread + sway * life * 0.52f;
            var y = length * (0.12f + life * 0.82f);
            var position = nozzle + new Vector2(x, y);
            var tint = Mix(EngineCoreColor, EngineOuterColor, 0.38f + h1 * 0.42f);
            var alpha = intensity * fade * (0.12f + h2 * 0.18f);
            var radius = 0.55f + h1 * (1.2f + afterburner * 0.55f);
            var streak = new Vector2(HashSigned(seed + i * 11.11f) * width * 0.08f, 4.5f + h0 * (8.5f + afterburner * 5.5f));

            DrawLine(position - streak * 0.22f, position + streak * 0.72f, WithAlpha(tint, alpha * 0.72f), Math.Max(0.45f, radius * 0.38f), true);
            DrawCircle(position, radius, WithAlpha(tint, alpha));
        }
    }

    private int SelectRenderablePorts()
    {
        var source = ExhaustPorts.Count == 0 ? DefaultExhaustPorts : ExhaustPorts;
        if (source.Count == 0)
        {
            return 0;
        }

        if (ExhaustPorts.Count > 0)
        {
            var manifestCount = Math.Min(source.Count, _renderPorts.Length);
            for (var i = 0; i < manifestCount; i++)
            {
                _renderPorts[i] = source[i];
            }

            SortRenderPortsByX(manifestCount);
            return manifestCount;
        }

        var bottomY = float.NegativeInfinity;
        for (var i = 0; i < source.Count; i++)
        {
            bottomY = Math.Max(bottomY, source[i].Position.Y);
        }

        var count = 0;
        for (var i = 0; i < source.Count; i++)
        {
            var port = source[i];
            if (port.Position.Y < bottomY - 30f)
            {
                continue;
            }

            if (count < _renderPorts.Length)
            {
                _renderPorts[count++] = port;
                continue;
            }

            var shallowestIndex = 0;
            for (var j = 1; j < _renderPorts.Length; j++)
            {
                if (_renderPorts[j].Position.Y < _renderPorts[shallowestIndex].Position.Y)
                {
                    shallowestIndex = j;
                }
            }

            if (port.Position.Y > _renderPorts[shallowestIndex].Position.Y)
            {
                _renderPorts[shallowestIndex] = port;
            }
        }

        if (count == 0)
        {
            count = Math.Min(source.Count, _renderPorts.Length);
            for (var i = 0; i < count; i++)
            {
                _renderPorts[i] = source[i];
            }
        }

        SortRenderPortsByX(count);
        return count;
    }

    private void SortRenderPortsByX(int count)
    {
        for (var i = 1; i < count; i++)
        {
            var current = _renderPorts[i];
            var j = i - 1;
            while (j >= 0 && _renderPorts[j].Position.X > current.Position.X)
            {
                _renderPorts[j + 1] = _renderPorts[j];
                j--;
            }

            _renderPorts[j + 1] = current;
        }
    }

    private bool HasCentralRenderPort(int count, float clusterX, float threshold)
    {
        for (var i = 0; i < count; i++)
        {
            if (MathF.Abs(_renderPorts[i].Position.X - clusterX) <= threshold)
            {
                return true;
            }
        }

        return false;
    }

    private void DrawPlasmaPlume(Vector2 nozzle, float length, float halfWidth, float sway, float intensity, float seed)
    {
        var tip = nozzle + new Vector2(sway, length);
        DrawHeatCone(nozzle, length, halfWidth, sway, intensity, seed);
        DrawCoreFilaments(nozzle, length, halfWidth, sway, intensity, seed);

        var bubbleScale = Math.Clamp(EngineBubbleScale, 0f, 1.5f);
        var orbCount = ScaledDetailCount(DetailCount(6, 4, 2), MathF.Max(0.25f, bubbleScale));
        for (var i = 0; i < orbCount; i++)
        {
            var t = 0.07f + i * (0.79f / Math.Max(1, orbCount));
            var fade = MathF.Pow(1f - t, 1.45f);
            var wobble = MathF.Sin(seed * 0.031f + _phase * (0.92f + i * 0.09f) + i * 2.2f) * halfWidth * (0.10f + t * 0.38f);
            var center = LerpVec(nozzle, tip, t) + new Vector2(wobble, 0f);
            var radius = halfWidth * (1.02f - t * 0.74f) * (0.66f + Hash01(seed + i * 41.7f) * 0.32f) * bubbleScale;
            var bubbleAlpha = 0.30f + bubbleScale * 0.70f;
            DrawCircle(center, Math.Max(0.7f, radius), WithAlpha(EngineOuterColor, (0.09f + 0.12f * fade) * intensity * bubbleAlpha));
            DrawCircle(center + new Vector2(-wobble * 0.22f, 0f), Math.Max(0.6f, radius * 0.46f), WithAlpha(Mix(EngineOuterColor, EngineCoreColor, 0.58f), (0.12f + 0.18f * fade) * intensity * bubbleAlpha));
        }

        DrawNozzleRings(nozzle, halfWidth, intensity, seed);
        DrawShockDiamonds(nozzle, length, halfWidth, sway, intensity, seed);
        DrawExhaustParticles(nozzle, length, halfWidth, sway, intensity, seed);
        DrawSparkBurst(nozzle, length, halfWidth, sway, intensity, seed);
        DrawCircle(tip, halfWidth * 0.28f * bubbleScale, WithAlpha(EngineOuterColor, 0.06f * intensity * bubbleScale));
    }

    private void DrawHeatCone(Vector2 nozzle, float length, float halfWidth, float sway, float intensity, float seed)
    {
        var layers = DetailCount(4, 3, 2);
        for (var layer = layers - 1; layer >= 0; layer--)
        {
            var amount = layers <= 1 ? 0f : layer / (layers - 1f);
            var width = halfWidth * (0.56f + amount * 1.34f);
            var reach = length * (0.48f + amount * 0.46f);
            var drift = sway * (0.42f + amount * 0.58f);
            var shoulder = nozzle + new Vector2(drift * 0.28f + HashSigned(seed + layer * 8.1f) * halfWidth * 0.18f, reach * 0.46f);
            var tip = nozzle + new Vector2(drift, reach);
            var color = layer <= 1
                ? Mix(EngineCoreColor, EngineOuterColor, 0.34f)
                : EngineOuterColor;
            var inverseLayer = layers - 1 - layer;
            var alpha = intensity * (0.052f + inverseLayer * 0.030f * (4f / Math.Max(1f, layers - 1f)));
            var lineWidth = Math.Max(1f, width * (0.28f + amount * 0.18f));
            var start = nozzle + new Vector2(HashSigned(seed + layer * 5.37f) * halfWidth * 0.06f, 2f);
            DrawLine(start, shoulder, WithAlpha(color, alpha * 0.54f), lineWidth, true);
            DrawLine(shoulder, tip, WithAlpha(color, alpha * 0.82f), Math.Max(1f, lineWidth * 0.56f), true);

            var sideColor = WithAlpha(Mix(color, EngineCoreColor, 0.35f), alpha * 0.34f);
            DrawLine(
                nozzle + new Vector2(-width * 0.22f, 2.5f),
                tip + new Vector2(-width * 0.07f, 0f),
                sideColor,
                Math.Max(0.8f, lineWidth * 0.18f),
                true);
            DrawLine(
                nozzle + new Vector2(width * 0.22f, 2.5f),
                tip + new Vector2(width * 0.07f, 0f),
                sideColor,
                Math.Max(0.8f, lineWidth * 0.18f),
                true);
        }
    }

    private void DrawCoreFilaments(Vector2 nozzle, float length, float halfWidth, float sway, float intensity, float seed)
    {
        var afterburner = Math.Clamp(AfterburnerLevel, 0f, 1f);
        var count = DetailCount(afterburner > 0.01f ? 5 : 4, afterburner > 0.01f ? 3 : 2, afterburner > 0.01f ? 2 : 1);
        for (var i = 0; i < count; i++)
        {
            var side = HashSigned(seed + i * 23.71f);
            var start = nozzle + new Vector2(side * halfWidth * 0.22f, 2f + i * 0.7f);
            var mid = nozzle + new Vector2(side * halfWidth * (0.14f + i * 0.035f) + sway * 0.20f, length * (0.30f + i * 0.035f));
            var end = nozzle + new Vector2(side * halfWidth * (0.28f + i * 0.06f) + sway * 0.48f, length * (0.56f + i * 0.028f));
            var color = i <= 1
                ? EngineCoreColor
                : Mix(EngineCoreColor, EngineOuterColor, 0.42f);
            var alpha = intensity * (0.32f - i * 0.022f);

            DrawLine(start, mid, WithAlpha(color, alpha), 1.9f - i * 0.18f, true);
            DrawLine(mid, end, WithAlpha(color, alpha * 0.72f), 1.25f - i * 0.12f, true);
        }
    }

    private void DrawNozzleRings(Vector2 nozzle, float halfWidth, float intensity, float seed)
    {
        var bubbleScale = Math.Clamp(EngineBubbleScale, 0f, 1.5f);
        var count = ScaledDetailCount(DetailCount(2, 1, 1), MathF.Max(0.35f, bubbleScale));
        for (var i = 0; i < count; i++)
        {
            var pulse = Fract(_phase * 0.09f + Hash01(seed + i * 13.7f));
            var radius = halfWidth * (0.78f + pulse * 1.24f + i * 0.18f) * bubbleScale;
            var alpha = intensity * (1f - pulse) * (0.18f - i * 0.032f) * bubbleScale;
            DrawArc(nozzle + new Vector2(0f, 5f + i * 1.4f), radius, 0f, MathF.Tau, 48, WithAlpha(EngineCoreColor, alpha), 1.2f, true);
        }
    }

    private void DrawShockDiamonds(Vector2 nozzle, float length, float halfWidth, float sway, float intensity, float seed)
    {
        var count = DetailCount(AfterburnerLevel > 0.01f ? 3 : 2, 2, 1);
        for (var i = 0; i < count; i++)
        {
            var t = 0.22f + i * 0.19f + MathF.Sin(_phase * 1.1f + seed + i) * 0.012f;
            var center = nozzle + new Vector2(sway * t, length * t);
            var width = halfWidth * (0.18f - i * 0.028f) * (0.82f + Hash01(seed + i * 17.13f) * 0.26f);
            var height = length * (0.028f + i * 0.003f);
            var alpha = intensity * (0.23f - i * 0.052f);
            DrawLine(
                center + new Vector2(-width, 0f),
                center + new Vector2(width, 0f),
                WithAlpha(EngineCoreColor, alpha * 0.70f),
                Math.Max(0.7f, height * 0.42f),
                true);
            DrawLine(
                center + new Vector2(0f, -height),
                center + new Vector2(0f, height),
                WithAlpha(Mix(EngineCoreColor, EngineOuterColor, 0.45f), alpha * 0.38f),
                Math.Max(0.55f, width * 0.18f),
                true);
            DrawCircle(center, Math.Max(0.7f, width * 0.28f), WithAlpha(Mix(EngineCoreColor, EngineOuterColor, 0.45f), alpha * 0.48f));
        }
    }

    private void DrawExhaustParticles(Vector2 nozzle, float length, float halfWidth, float sway, float intensity, float seed)
    {
        var afterburner = Math.Clamp(AfterburnerLevel, 0f, 1f);
        var particleDensity = Math.Clamp(EngineParticleDensity, 0.5f, 2.2f);
        var count = ScaledDetailCount(DetailCount(afterburner > 0.01f ? 22 : 14, afterburner > 0.01f ? 8 : 6, afterburner > 0.01f ? 3 : 2), particleDensity);
        var streakStretch = 1f + Math.Max(0f, particleDensity - 1f) * 0.45f;
        for (var i = 0; i < count; i++)
        {
            var h0 = Hash01(seed + i * 29.17f);
            var h1 = Hash01(seed + i * 43.91f);
            var h2 = Hash01(seed + i * 71.33f);
            var speed = 0.035f + h2 * 0.052f + afterburner * 0.024f;
            var life = Fract(h0 + _phase * speed);
            var fade = MathF.Pow(1f - life, 1.38f);
            var y = length * (0.05f + life * 0.9f);
            var spread = halfWidth * (0.24f + life * 1.45f);
            var x = HashSigned(seed + i * 19.37f) * spread + sway * life;
            x += MathF.Sin(_phase * (1.1f + h2) + i * 1.73f) * halfWidth * 0.09f;

            var position = nozzle + new Vector2(x, y);
            var tint = Mix(EngineCoreColor, EngineOuterColor, 0.28f + h2 * 0.54f);
            var alpha = intensity * fade * (0.13f + h1 * 0.24f) * (1f + afterburner * 0.34f);
            var radius = (0.6f + h2 * (1.45f + afterburner * 0.9f)) * (0.32f + fade * 0.72f);
            var streak = new Vector2(HashSigned(seed + i * 11.11f) * halfWidth * 0.20f, (6.4f + h0 * (14.0f + afterburner * 12f)) * streakStretch);

            DrawLine(position - streak * 0.20f, position + streak * 0.78f, WithAlpha(tint, alpha * 0.82f), Math.Max(0.55f, radius * 0.46f), true);
            DrawCircle(position, radius, WithAlpha(tint, alpha));
        }
    }

    private void DrawSparkBurst(Vector2 nozzle, float length, float halfWidth, float sway, float intensity, float seed)
    {
        var afterburner = Math.Clamp(AfterburnerLevel, 0f, 1f);
        var particleDensity = Math.Clamp(EngineParticleDensity, 0.5f, 2.2f);
        var count = ScaledDetailCount(DetailCount(afterburner > 0.01f ? 8 : 5, afterburner > 0.01f ? 3 : 2, afterburner > 0.01f ? 1 : 1), particleDensity);
        for (var i = 0; i < count; i++)
        {
            var h0 = Hash01(seed + i * 97.17f);
            var h1 = Hash01(seed + i * 53.13f);
            var life = Fract(h0 + _phase * (0.05f + h1 * 0.045f));
            var side = HashSigned(seed + i * 31.71f);
            var y = length * (0.10f + life * 0.72f);
            var x = side * halfWidth * (0.72f + life * 2.2f) + sway * life;
            var start = nozzle + new Vector2(x, y);
            var end = start + new Vector2(side * halfWidth * (0.28f + h1 * 0.62f), (10f + h0 * 18f) * (1f + Math.Max(0f, particleDensity - 1f) * 0.35f));
            var alpha = intensity * MathF.Pow(1f - life, 1.6f) * (0.16f + afterburner * 0.10f);
            DrawLine(start, end, WithAlpha(Mix(EngineCoreColor, EngineOuterColor, 0.68f), alpha), 0.75f, true);
        }
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

    private static Vector2 LerpVec(Vector2 from, Vector2 to, float amount)
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

    private static float HashSigned(float value)
    {
        return Hash01(value) * 2f - 1f;
    }

    private static float NextIdleSeed()
    {
        var index = _nextIdleSeed++;
        return 11.7f + index * 37.31f;
    }

    private int DetailCount(int full, int balanced, int minimal)
    {
        return EffectQuality switch
        {
            ShipEffectQuality.Full => full,
            ShipEffectQuality.Balanced => Math.Max(0, balanced),
            ShipEffectQuality.Minimal => Math.Max(0, minimal),
            _ => 0
        };
    }

    private static int ScaledDetailCount(int count, float multiplier)
    {
        if (count <= 0 || multiplier <= 0f)
        {
            return 0;
        }

        return Math.Max(1, (int)MathF.Round(count * multiplier));
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private void DrawTurretCue()
    {
        var aim = AimDirection.LengthSquared() > 0.001f ? AimDirection.Normalized() : Vector2.Up;
        DrawArc(Vector2.Zero, 12f, 0f, MathF.Tau, 42, new Color(0.3f, 0.95f, 1f, 0.22f), 0.9f, true);
        DrawLine(Vector2.Zero, aim * 36f, new Color(0.35f, 1f, 0.95f, 0.18f), 1.2f, true);
        if (IsFiring)
        {
            DrawCircle(aim * 42f, 5f, new Color(0.8f, 1f, 0.9f, 0.55f));
        }
    }

    private void DrawDebugHitbox()
    {
        if (!ShowHitbox)
        {
            return;
        }

        var half = HitboxLocalSize * 0.5f;
        var rect = new Rect2(HitboxLocalCenter - half, HitboxLocalSize);
        DrawRect(rect, new Color(1f, 0.85f, 0.1f, 0.055f), true);
        DrawRect(rect, new Color(1f, 0.86f, 0.18f, 0.95f), false, 2.2f);
        DrawLine(HitboxLocalCenter + new Vector2(-8f, 0f), HitboxLocalCenter + new Vector2(8f, 0f), new Color(1f, 0.98f, 0.55f, 0.9f), 1.2f, true);
        DrawLine(HitboxLocalCenter + new Vector2(0f, -8f), HitboxLocalCenter + new Vector2(0f, 8f), new Color(1f, 0.98f, 0.55f, 0.9f), 1.2f, true);
    }
}

public partial class ShipIdleOverlay : Node2D
{
    public float Phase { get; set; }
    public float Pulse { get; set; }
    public float Activity { get; set; }
    public float BankAmount { get; set; }
    public float TextureEffectScale { get; set; } = 1f;
    public Color EngineOuterColor { get; set; } = new(0.1f, 0.8f, 1f, 1f);
    public Color EngineCoreColor { get; set; } = new(0.75f, 1f, 1f, 1f);
    public Vector2 HitboxLocalCenter { get; set; } = Vector2.Zero;
    public Vector2 HitboxLocalSize { get; set; } = new(84f, 84f);
    public ShipRigProfile RigProfile { get; set; } = ShipRigProfile.Empty;
    public ShipEffectQuality EffectQuality { get; set; } = ShipEffectQuality.Full;

    public override void _Draw()
    {
        if (EffectQuality == ShipEffectQuality.Hidden)
        {
            return;
        }

        DrawBankLighting();
        DrawCoreBreath();
        DrawEnginePortGlow();
    }

    private void DrawBankLighting()
    {
        var bank = Math.Clamp(BankAmount, -1f, 1f);
        var strength = Math.Abs(bank);
        if (strength < 0.08f)
        {
            return;
        }

        var side = MathF.Sign(bank);
        var halfX = Math.Max(34f, HitboxLocalSize.X * 0.5f);
        var halfY = Math.Max(42f, HitboxLocalSize.Y * 0.46f);
        var rimAlpha = (0.025f + strength * 0.050f) * QualityAlpha();
        var edgeX = HitboxLocalCenter.X + side * halfX * 0.66f;
        DrawLine(
            new Vector2(edgeX, HitboxLocalCenter.Y - halfY * 0.42f),
            new Vector2(edgeX + side * halfX * 0.035f, HitboxLocalCenter.Y + halfY * 0.46f),
            WithAlpha(Mix(EngineOuterColor, EngineCoreColor, 0.54f), rimAlpha),
            0.85f + strength * 0.40f,
            true);
    }

    private void DrawCoreBreath()
    {
        var halfX = Math.Max(24f, HitboxLocalSize.X * 0.30f);
        var halfY = Math.Max(30f, HitboxLocalSize.Y * 0.32f);
        var radius = Math.Min(halfX, halfY) * 0.12f;
        var center = RigProfile.HasRegions ? RigProfile.CoreAnchor : HitboxLocalCenter + new Vector2(0f, -halfY * 0.05f);
        var alpha = (0.018f + Activity * 0.012f) * QualityAlpha();

        DrawCircle(center, radius * 1.8f, WithAlpha(EngineOuterColor, alpha * 0.32f));
        DrawCircle(center, radius * 0.72f, WithAlpha(Mix(EngineOuterColor, EngineCoreColor, 0.64f), alpha * 0.46f));
    }

    private void DrawEnginePortGlow()
    {
        var ports = RigProfile.EnginePorts;
        var count = Math.Min(ports.Length, DetailCount(4, 3, 1));
        var quality = QualityAlpha();
        for (var i = 0; i < count; i++)
        {
            var port = ports[i];
            var alpha = (0.030f + Activity * 0.020f) * quality;
            DrawCircle(port.Position, port.Radius * 1.60f, WithAlpha(EngineOuterColor, alpha * 0.35f));
            DrawCircle(port.Position, port.Radius * 0.58f, WithAlpha(EngineCoreColor, alpha * 0.64f));
        }
    }

    private int DetailCount(int full, int balanced, int minimal)
    {
        return EffectQuality switch
        {
            ShipEffectQuality.Full => full,
            ShipEffectQuality.Balanced => balanced,
            ShipEffectQuality.Minimal => minimal,
            _ => 0
        };
    }

    private float QualityAlpha()
    {
        return EffectQuality switch
        {
            ShipEffectQuality.Full => 1f,
            ShipEffectQuality.Balanced => 0.72f,
            ShipEffectQuality.Minimal => 0.44f,
            _ => 0f
        };
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

}
