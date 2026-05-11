using Godot;
using SpaceRangers.Core;
using File = System.IO.File;

namespace SpaceRangersPrototype;

public partial class ProjectileImpactLayer : Node2D
{
    private const int MaxImpacts = 72;
    private const float ShieldLifetime = 0.36f;
    private readonly List<ImpactView> _impacts = new();
    private readonly Dictionary<int, ShieldTargetVisual> _shieldTargets = new();
    private readonly Dictionary<int, Vector2[]> _ellipseFillPointBuffers = new();
    private readonly Dictionary<int, Vector2[]> _ellipseArcPointBuffers = new();
    private Texture2D? _impactFlash;
    private Texture2D? _dustRing;
    private Texture2D? _smokePuff;
    private Texture2D? _spark;
    private Texture2D? _effectRing;
    private Texture2D? _shieldBubble;
    private Texture2D? _shieldRipple;
    private Texture2D? _effectMuzzleFlashFps;
    private Texture2D? _effectSparkle;
    private Texture2D? _effectCrackA;
    private Texture2D? _effectCrackB;

    public bool UseCulling { get; set; }
    public Rect2 VisibleWorldRect { get; set; }

    public override void _Ready()
    {
        ZIndex = 24;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        _impactFlash = LoadTexture("res://assets/effects/asteroid_impact_flash.png");
        _dustRing = LoadTexture("res://assets/effects/asteroid_dust_ring.png");
        _smokePuff = LoadTexture("res://assets/effects/asteroid_smoke_puff.png");
        _spark = LoadTexture("res://assets/effects/asteroid_fire_spark.png");
        _effectRing = LoadTexture("res://assets/effects/effectblocks/effectblocks_circle_1.png");
        _shieldBubble = LoadTexture("res://assets/effects/shield_bubble.png");
        _shieldRipple = LoadTexture("res://assets/effects/shield_ripple.png");
        _effectMuzzleFlashFps = LoadTexture("res://assets/effects/effectblocks/effectblocks_muzzle_flash_fps_1.png");
        _effectSparkle = LoadTexture("res://assets/effects/effectblocks/effectblocks_sparkle.png");
        _effectCrackA = LoadTexture("res://assets/effects/effectblocks/effectblocks_crack_2.png");
        _effectCrackB = LoadTexture("res://assets/effects/effectblocks/effectblocks_crack_3.png");
    }

    public override void _ExitTree()
    {
        ClearEffects();
        _impactFlash = null;
        _dustRing = null;
        _smokePuff = null;
        _spark = null;
        _effectRing = null;
        _shieldBubble = null;
        _shieldRipple = null;
        _effectMuzzleFlashFps = null;
        _effectSparkle = null;
        _effectCrackA = null;
        _effectCrackB = null;
    }

    public void ClearEffects()
    {
        _impacts.Clear();
        _shieldTargets.Clear();
        _ellipseFillPointBuffers.Clear();
        _ellipseArcPointBuffers.Clear();
        QueueRedraw();
    }

    public void SetShieldTargets(IReadOnlyList<ShipState> ships)
    {
        _shieldTargets.Clear();
        foreach (var ship in ships)
        {
            if (ship.IsDestroyed)
            {
                continue;
            }

            _shieldTargets[ship.Id] = new ShieldTargetVisual(
                ship.Hitbox.WorldCenter(ship.Position, ship.Rotation).ToGodot(),
                ship.Hitbox.BoundingRadius,
                ship.Hitbox.Size.ToGodot(),
                ship.Rotation);
        }
    }

    public void Spawn(ProjectileImpactState impact)
    {
        var cullShape = impact.Surface == ProjectileImpactSurface.Shield ? ShieldShapeFor(impact) : default;
        var cullPoint = impact.Surface == ProjectileImpactSurface.Shield ? cullShape.Center : impact.Position.ToGodot();
        var cullMargin = impact.Surface == ProjectileImpactSurface.Shield ? cullShape.Extents.Length() + 120f : 190f;
        if (UseCulling && !VisibleWorldRect.Grow(cullMargin).HasPoint(cullPoint))
        {
            return;
        }

        if (impact.Surface == ProjectileImpactSurface.Shield)
        {
            for (var index = 0; index < _impacts.Count; index++)
            {
                if (IsSameShieldTarget(_impacts[index].State, impact))
                {
                    _impacts[index] = new ImpactView(impact, ShieldLifetime);
                    QueueRedraw();
                    return;
                }
            }
        }

        while (_impacts.Count >= MaxImpacts)
        {
            _impacts.RemoveAt(0);
        }

        _impacts.Add(new ImpactView(impact, LifetimeFor(impact)));
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_impacts.Count == 0)
        {
            return;
        }

        var step = (float)Math.Min(delta, 0.08);
        for (var index = _impacts.Count - 1; index >= 0; index--)
        {
            var impact = _impacts[index];
            impact.Age += step;
            if (impact.Age >= impact.Lifetime)
            {
                _impacts.RemoveAt(index);
                continue;
            }

            _impacts[index] = impact;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var impact in _impacts)
        {
            var position = impact.State.Position.ToGodot();
            var radius = BaseSize(impact.State);
            var cullPoint = impact.State.Surface == ProjectileImpactSurface.Shield
                ? impact.State.TargetCenter.ToGodot()
                : position;
            var cullRadius = impact.State.Surface == ProjectileImpactSurface.Shield
                ? ShieldCullRadius(impact.State) + radius
                : radius * 3.0f;
            if (UseCulling && !VisibleWorldRect.Grow(cullRadius).HasPoint(cullPoint))
            {
                continue;
            }

            DrawImpact(impact, position, radius);
        }
    }

    private void DrawImpact(ImpactView impact, Vector2 position, float radius)
    {
        var t = Math.Clamp(impact.Age / impact.Lifetime, 0f, 1f);
        var direction = impact.State.Direction.ToGodot();
        if (direction.LengthSquared() <= 0.001f)
        {
            direction = Vector2.Right;
        }
        else
        {
            direction = direction.Normalized();
        }

        var surface = impact.State.Surface;
        if (surface == ProjectileImpactSurface.Shield)
        {
            DrawShieldImpact(impact.State, position, direction, radius, t);
            if (impact.State.Kind == ProjectileImpactKind.SunBurn)
            {
                DrawSolarShieldScorch(impact.State, direction, radius, t);
            }
            return;
        }

        if (surface == ProjectileImpactSurface.Asteroid)
        {
            DrawAsteroidImpact(impact.State, position, direction, radius, t);
            return;
        }

        if (impact.State.Kind == ProjectileImpactKind.SunBurn)
        {
            DrawSolarBurnImpact(impact.State, position, direction, radius, t, surface == ProjectileImpactSurface.Structure);
            return;
        }

        if (impact.State.Kind == ProjectileImpactKind.AsteroidCollision)
        {
            DrawAsteroidShipImpact(impact.State, position, direction, radius, t, surface == ProjectileImpactSurface.Structure);
            return;
        }

        DrawMetalImpact(impact.State, position, direction, radius, t, surface == ProjectileImpactSurface.Structure);
    }

    private void DrawShieldImpact(ProjectileImpactState impact, Vector2 position, Vector2 direction, float radius, float t)
    {
        var shield = ShieldShapeFor(impact);
        var burst = SmoothStep(0f, 0.70f, t);
        var flash = 1f - SmoothStep(0f, 0.18f, t);
        var fade = 1f - SmoothStep(0.70f, 1f, t);
        var palette = ShieldPalette(impact.ShieldRatio);
        var alphaPower = ShieldAlphaPower(impact.ShieldRatio);
        var texturePhase = impact.Seed * 0.00017f;
        var contactDirection = -direction;
        var contactBasis = ShieldContactFor(shield, contactDirection, 0.885f);
        var contact = contactBasis.Point;
        var tangent = contactBasis.Tangent;
        var normal = contactBasis.Normal;
        var maxExtent = Math.Max(shield.Extents.X, shield.Extents.Y);
        var minExtent = Math.Min(shield.Extents.X, shield.Extents.Y);
        var maxImpactRadius = Math.Max(7f, minExtent * 0.36f);
        var minImpactRadius = Math.Min(maxImpactRadius, radius * 0.42f);
        var impactRadius = Math.Clamp(minExtent * 0.22f, minImpactRadius, maxImpactRadius);
        var surfaceRotation = tangent.Angle();
        var arcSpread = Math.Clamp(impactRadius / MathF.Max(1f, maxExtent) * 1.36f, 0.24f, 0.58f);
        var impactCenter = contact - normal * impactRadius * 0.035f;

        DrawTexturedQuad(_shieldBubble, shield.Center, shield.Rotation, shield.Extents * (1.96f + burst * 0.010f), WithAlpha(palette.Field, fade * alphaPower));
        DrawTexturedQuad(_shieldRipple, shield.Center, shield.Rotation, shield.Extents * (1.90f + burst * 0.020f), WithAlpha(palette.Line, fade * (0.22f + flash * 0.18f) * alphaPower));
        DrawEllipseFilled(shield.Center, shield.Extents * (0.935f + burst * 0.006f), shield.Rotation, WithAlpha(palette.Field, fade * 0.055f * alphaPower), 72);
        DrawEllipseArc(shield.Center, shield.Extents * (0.965f + burst * 0.010f), shield.Rotation, 0f, MathF.Tau, 96, WithAlpha(palette.Rim, fade * 0.78f * alphaPower), Math.Clamp(maxExtent * 0.030f, 1.6f, 5.0f));
        DrawEllipseArc(shield.Center, shield.Extents * (0.82f + burst * 0.025f), shield.Rotation, -0.55f + texturePhase, MathF.Tau * 0.56f + texturePhase, 46, WithAlpha(palette.Line, fade * 0.28f * alphaPower), Math.Clamp(maxExtent * 0.012f, 0.8f, 2.3f));
        DrawEllipseArc(shield.Center, shield.Extents * (0.65f + burst * 0.035f), shield.Rotation, MathF.Tau * 0.20f - texturePhase, MathF.Tau * 0.73f - texturePhase, 42, WithAlpha(palette.Line, fade * 0.18f * alphaPower), Math.Clamp(maxExtent * 0.010f, 0.7f, 1.9f));
        DrawShieldFilaments(impact, shield, t, palette, fade * alphaPower);

        DrawTexturedQuad(_shieldRipple, impactCenter, surfaceRotation, new Vector2(impactRadius * (1.46f + burst * 0.54f), impactRadius * (0.42f + burst * 0.16f)), WithAlpha(palette.Rim, flash * 0.54f * alphaPower));
        DrawTexturedQuad(_impactFlash, impactCenter - normal * impactRadius * 0.020f, surfaceRotation + impact.Seed * 0.00008f, new Vector2(impactRadius * (0.86f + burst * 0.24f), impactRadius * (0.32f + burst * 0.10f)), WithAlpha(palette.Core, flash * 0.54f * alphaPower));
        DrawEllipseArc(shield.Center, shield.Extents * (0.942f + burst * 0.014f), shield.Rotation, contactBasis.Angle - arcSpread, contactBasis.Angle + arcSpread, 20, WithAlpha(palette.Rim, flash * 0.70f * alphaPower), Math.Clamp(impactRadius * 0.045f, 1.2f, 3.2f));
        DrawEllipseArc(shield.Center, shield.Extents * (0.840f + burst * 0.026f), shield.Rotation, contactBasis.Angle - arcSpread * 0.82f + 0.05f, contactBasis.Angle + arcSpread * 0.82f + 0.05f, 18, WithAlpha(palette.Line, fade * 0.34f * alphaPower), Math.Clamp(impactRadius * 0.024f, 0.8f, 2.0f));
        DrawEllipseFilled(impactCenter - normal * impactRadius * 0.018f, new Vector2(impactRadius * (0.20f + burst * 0.070f), impactRadius * (0.060f + burst * 0.025f)), surfaceRotation, WithAlpha(palette.Core, flash * 0.72f * alphaPower), 18);
        DrawShieldSurfaceStreaks(impact, impactCenter, tangent, normal, impactRadius * 0.58f, 7, palette.Rim, fade * 0.42f * alphaPower);
    }

    private void DrawSolarShieldScorch(ProjectileImpactState impact, Vector2 direction, float radius, float t)
    {
        var shield = ShieldShapeFor(impact);
        var burst = SmoothStep(0f, 0.68f, t);
        var flash = 1f - SmoothStep(0f, 0.20f, t);
        var fade = 1f - SmoothStep(0.56f, 1f, t);
        var contactDirection = -direction;
        var contactBasis = ShieldContactFor(shield, contactDirection, 0.885f);
        var scorchRadius = Math.Min(radius, Math.Max(6f, Math.Min(shield.Extents.X, shield.Extents.Y) * 0.32f));
        var contact = contactBasis.Point - contactBasis.Normal * scorchRadius * 0.035f;
        var heat = new Color(1f, 0.34f, 0.05f, 1f);
        var core = new Color(1f, 0.88f, 0.34f, 1f);
        var smoke = new Color(0.24f, 0.10f, 0.045f, 1f);

        DrawTexturedQuad(_impactFlash, contact, contactBasis.Tangent.Angle() + impact.Seed * 0.00006f, new Vector2(scorchRadius * (0.90f + burst * 0.34f), scorchRadius * (0.30f + burst * 0.12f)), WithAlpha(core, flash * 0.30f));
        DrawTexturedQuad(_effectRing, contact - contactBasis.Normal * scorchRadius * 0.04f, contactBasis.Tangent.Angle(), new Vector2(scorchRadius * (1.04f + burst * 0.42f), scorchRadius * (0.42f + burst * 0.18f)), WithAlpha(heat, fade * 0.14f));
        DrawTexturedQuad(_smokePuff, contact - contactBasis.Normal * scorchRadius * 0.08f, contactBasis.Tangent.Angle() - impact.Seed * 0.00005f, new Vector2(scorchRadius * (0.86f + burst * 0.36f), scorchRadius * (0.46f + burst * 0.20f)), WithAlpha(smoke, fade * 0.12f));
        DrawShieldSurfaceStreaks(impact, contact, contactBasis.Tangent, contactBasis.Normal, scorchRadius * 0.44f, 6, heat, fade * 0.24f);
    }

    private void DrawSolarBurnImpact(ProjectileImpactState impact, Vector2 position, Vector2 direction, float radius, float t, bool structureHit)
    {
        var burst = SmoothStep(0f, 0.72f, t);
        var flash = 1f - SmoothStep(0f, 0.18f, t);
        var fade = 1f - SmoothStep(0.58f, 1f, t);
        var eject = -direction;
        var rotation = direction.Angle();
        var smoke = structureHit
            ? new Color(0.20f, 0.075f, 0.045f, 1f)
            : new Color(0.18f, 0.105f, 0.065f, 1f);
        var ember = structureHit
            ? new Color(1f, 0.30f, 0.05f, 1f)
            : new Color(1f, 0.48f, 0.10f, 1f);
        var core = new Color(1f, 0.88f, 0.32f, 1f);

        DrawEllipseFilled(position + eject * radius * 0.05f, new Vector2(radius * (0.30f + burst * 0.18f), radius * (0.18f + burst * 0.10f)), rotation, WithAlpha(core, flash * 0.42f), 22);
        DrawTexturedQuad(_impactFlash, position, rotation + impact.Seed * 0.00019f, Vector2.One * radius * (0.44f + burst * 0.24f), WithAlpha(core, flash * 0.24f));
        DrawTexturedQuad(_effectMuzzleFlashFps, position + eject * radius * 0.10f, rotation, Vector2.One * radius * (0.32f + burst * 0.20f), WithAlpha(ember, flash * 0.20f));
        DrawTexturedQuad(_smokePuff, position + eject * radius * 0.25f, rotation - impact.Seed * 0.00017f, Vector2.One * radius * (0.90f + burst * 1.06f), WithAlpha(smoke, fade * (structureHit ? 0.30f : 0.22f)));
        DrawTexturedQuad(_dustRing, position + eject * radius * 0.08f, rotation, Vector2.One * radius * (0.80f + burst * 0.84f), WithAlpha(ember.Lerp(smoke, 0.45f), fade * 0.13f));

        if (structureHit)
        {
            DrawTexturedQuad(_effectCrackA, position, impact.TargetRotation + impact.Seed * 0.00023f, Vector2.One * radius * (0.58f + burst * 0.22f), WithAlpha(new Color(1f, 0.16f, 0.05f, 1f), fade * 0.20f));
        }

        DrawRicochetSparks(impact, position, eject, radius * 0.82f, structureHit ? 10 : 7, ember, fade * (structureHit ? 0.56f : 0.42f), 0.88f);
        DrawImpactDust(impact, position + eject * radius * 0.14f, eject, radius, structureHit ? 7 : 5, smoke.Lerp(ember, 0.18f), fade * (structureHit ? 0.32f : 0.22f));
    }

    private void DrawAsteroidShipImpact(ProjectileImpactState impact, Vector2 position, Vector2 direction, float radius, float t, bool structureHit)
    {
        var burst = SmoothStep(0f, 0.68f, t);
        var flash = 1f - SmoothStep(0f, 0.14f, t);
        var fade = 1f - SmoothStep(0.62f, 1f, t);
        var eject = -direction;
        var rotation = direction.Angle();
        var dust = new Color(0.42f, 0.36f, 0.28f, 1f);
        var darkDust = new Color(0.12f, 0.105f, 0.09f, 1f);
        var ember = structureHit
            ? new Color(1f, 0.38f, 0.12f, 1f)
            : new Color(0.95f, 0.68f, 0.38f, 1f);

        DrawEllipseFilled(position + eject * radius * 0.06f, new Vector2(radius * (0.44f + burst * 0.26f), radius * (0.20f + burst * 0.12f)), rotation, WithAlpha(ember, flash * 0.32f), 24);
        DrawTexturedQuad(_smokePuff, position + eject * radius * 0.24f, rotation + impact.Seed * 0.00017f, Vector2.One * radius * (1.10f + burst * 1.28f), WithAlpha(darkDust.Lerp(dust, 0.28f), fade * 0.40f));
        DrawTexturedQuad(_dustRing, position + eject * radius * 0.08f, rotation, Vector2.One * radius * (1.00f + burst * 1.38f), WithAlpha(dust, fade * 0.24f));
        DrawTexturedQuad(_impactFlash, position, rotation + impact.Seed * 0.00031f, Vector2.One * radius * (0.48f + burst * 0.18f), WithAlpha(ember, flash * 0.18f));

        if (structureHit)
        {
            DrawTexturedQuad(_effectCrackA, position + eject * radius * 0.02f, impact.TargetRotation + impact.Seed * 0.00021f, Vector2.One * radius * (0.82f + burst * 0.24f), WithAlpha(new Color(1f, 0.20f, 0.08f, 1f), fade * 0.18f));
            DrawTexturedQuad(_effectCrackB, position, impact.TargetRotation - impact.Seed * 0.00017f, Vector2.One * radius * (0.64f + burst * 0.26f), WithAlpha(darkDust, fade * 0.30f));
        }

        DrawImpactDust(impact, position + eject * radius * 0.12f, eject, radius, 13, dust, fade * 0.52f);
        DrawRicochetSparks(impact, position, eject, radius * 0.88f, structureHit ? 12 : 8, ember, fade * 0.46f, 1.18f);
    }

    private void DrawMetalImpact(ProjectileImpactState impact, Vector2 position, Vector2 direction, float radius, float t, bool structureHit)
    {
        var burst = SmoothStep(0f, 0.62f, t);
        var flash = 1f - SmoothStep(0f, 0.13f, t);
        var fade = 1f - SmoothStep(0.60f, 1f, t);
        var smoke = structureHit
            ? new Color(0.22f, 0.16f, 0.13f, 1f)
            : new Color(0.18f, 0.17f, 0.16f, 1f);
        var ember = structureHit
            ? new Color(1f, 0.42f, 0.16f, 1f)
            : new Color(0.90f, 0.78f, 0.55f, 1f);
        var core = structureHit
            ? new Color(1f, 0.72f, 0.32f, 1f)
            : new Color(0.78f, 0.96f, 1f, 1f);
        var rotation = direction.Angle();
        var eject = -direction;

        DrawEllipseFilled(position + eject * radius * 0.05f, new Vector2(radius * (0.36f + burst * 0.16f), radius * (0.16f + burst * 0.08f)), rotation, WithAlpha(core, flash * 0.50f), 22);
        DrawTexturedQuad(_smokePuff, position + eject * radius * 0.18f, rotation + impact.Seed * 0.00013f, Vector2.One * radius * (0.86f + burst * 0.94f), WithAlpha(smoke, fade * (structureHit ? 0.33f : 0.20f)));
        DrawTexturedQuad(_effectRing, position, impact.TargetRotation + impact.Seed * 0.00009f, Vector2.One * radius * (0.68f + burst * 0.82f), WithAlpha(core.Lerp(smoke, 0.34f), fade * (structureHit ? 0.12f : 0.075f)));
        DrawTexturedQuad(_effectMuzzleFlashFps, position, rotation + impact.Seed * 0.00031f, Vector2.One * radius * (0.18f + burst * 0.08f), WithAlpha(core, flash * (structureHit ? 0.24f : 0.16f)));
        DrawTexturedQuad(_spark, position, 0f, Vector2.One * radius * (0.26f + burst * 0.16f), WithAlpha(ember, flash * 0.38f));

        if (structureHit)
        {
            DrawTexturedQuad(_effectCrackA, position + eject * radius * 0.02f, impact.TargetRotation + impact.Seed * 0.00021f, Vector2.One * radius * (0.78f + burst * 0.20f), WithAlpha(new Color(1f, 0.22f, 0.10f, 1f), fade * 0.18f));
            DrawTexturedQuad(_effectCrackB, position, impact.TargetRotation - impact.Seed * 0.00017f, Vector2.One * radius * (0.54f + burst * 0.20f), WithAlpha(new Color(0.12f, 0.05f, 0.035f, 1f), fade * 0.26f));
        }
        else
        {
            DrawTexturedQuad(_effectSparkle, position - direction * radius * 0.04f, rotation + impact.Seed * 0.00023f, Vector2.One * radius * (0.20f + burst * 0.08f), WithAlpha(new Color(0.76f, 0.96f, 1f, 1f), flash * 0.42f));
        }

        DrawRicochetSparks(impact, position, eject, radius, structureHit ? 12 : 9, ember, fade * (structureHit ? 0.72f : 0.58f), structureHit ? 1.02f : 0.72f);
        DrawImpactDust(impact, position + eject * radius * 0.12f, eject, radius, structureHit ? 7 : 4, smoke.Lerp(ember, structureHit ? 0.18f : 0.08f), fade * (structureHit ? 0.30f : 0.16f));
    }

    private void DrawAsteroidImpact(ProjectileImpactState impact, Vector2 position, Vector2 direction, float radius, float t)
    {
        var burst = SmoothStep(0f, 0.70f, t);
        var flash = 1f - SmoothStep(0f, 0.12f, t);
        var fade = 1f - SmoothStep(0.56f, 1f, t);
        var dust = new Color(0.40f, 0.36f, 0.30f, 1f);
        var darkDust = new Color(0.11f, 0.10f, 0.09f, 1f);
        var ember = new Color(0.86f, 0.54f, 0.26f, 1f);
        var rotation = direction.Angle();

        var eject = -direction;
        DrawEllipseFilled(position + eject * radius * 0.07f, Vector2.One * radius * (0.22f + burst * 0.16f), 0f, WithAlpha(darkDust.Lerp(dust, 0.18f), fade * 0.22f), 22);
        DrawTexturedQuad(_smokePuff, position + eject * radius * 0.24f, rotation + impact.Seed * 0.00027f, Vector2.One * radius * (1.10f + burst * 1.22f), WithAlpha(darkDust.Lerp(dust, 0.32f), fade * 0.36f));
        DrawTexturedQuad(_dustRing, position + eject * radius * 0.05f, rotation, Vector2.One * radius * (1.08f + burst * 1.42f), WithAlpha(dust, fade * 0.20f));
        DrawTexturedQuad(_impactFlash, position, rotation, Vector2.One * radius * (0.54f + burst * 0.16f), WithAlpha(ember, flash * 0.13f));

        DrawImpactDust(impact, position + eject * radius * 0.12f, eject, radius, 12, dust, fade * 0.54f);
        DrawRicochetSparks(impact, position, eject, radius * 0.78f, 5, dust.Lerp(ember, 0.20f), fade * 0.34f, 1.22f);
    }

    private void DrawRicochetSparks(ProjectileImpactState impact, Vector2 position, Vector2 ejectDirection, float radius, int count, Color color, float alpha, float spread)
    {
        var baseAngle = ejectDirection.Angle();
        for (var i = 0; i < count; i++)
        {
            var h0 = Hash01(impact.Seed * 0.071f + i * 5.19f);
            var h1 = Hash01(impact.Seed * 0.083f + i * 11.73f);
            var h2 = Hash01(impact.Seed * 0.097f + i * 17.41f);
            var angle = baseAngle + (h0 - 0.5f) * spread * MathF.PI;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var start = position + direction * radius * (0.02f + h2 * 0.05f);
            var end = start + direction * radius * (0.18f + h1 * 0.42f);
            var width = Math.Clamp(radius * (0.018f + h2 * 0.022f), 0.7f, 2.0f);
            DrawLine(start, end, WithAlpha(color, alpha * (0.28f + h1 * 0.62f)), width, true);
            if (h2 > 0.58f)
            {
                DrawTexturedQuad(_spark, end, 0f, Vector2.One * Math.Clamp(radius * (0.055f + h0 * 0.045f), 1.5f, 4.5f), WithAlpha(color, alpha * 0.30f));
            }
        }
    }

    private void DrawImpactDust(ProjectileImpactState impact, Vector2 position, Vector2 ejectDirection, float radius, int count, Color color, float alpha)
    {
        var tangent = new Vector2(-ejectDirection.Y, ejectDirection.X);
        for (var i = 0; i < count; i++)
        {
            var h0 = Hash01(impact.Seed * 0.109f + i * 4.87f);
            var h1 = Hash01(impact.Seed * 0.127f + i * 10.21f);
            var h2 = Hash01(impact.Seed * 0.151f + i * 16.63f);
            var direction = (ejectDirection * (0.36f + h0 * 0.74f) + tangent * (h1 - 0.5f) * 1.12f).Normalized();
            var offset = direction * radius * (0.16f + h0 * 0.78f);
            var dustPosition = position + offset;
            var size = Math.Clamp(radius * (0.045f + h2 * 0.055f), 1.5f, 5.5f);
            DrawEllipseFilled(dustPosition, new Vector2(size * (0.72f + h1 * 0.50f), size * (0.62f + h0 * 0.36f)), h2 * MathF.Tau, WithAlpha(color, alpha * (0.24f + h1 * 0.42f)), 7);
        }
    }

    private void DrawImpactStreaks(ProjectileImpactState impact, Vector2 position, Vector2 baseDirection, float radius, int count, Color color, float alpha)
    {
        var tangent = new Vector2(-baseDirection.Y, baseDirection.X);
        for (var i = 0; i < count; i++)
        {
            var h0 = Hash01(impact.Seed * 0.017f + i * 6.13f);
            var h1 = Hash01(impact.Seed * 0.023f + i * 12.91f);
            var h2 = Hash01(impact.Seed * 0.031f + i * 19.37f);
            var radialAngle = (h0 - 0.5f) * MathF.Tau * 0.88f;
            var radial = new Vector2(MathF.Cos(radialAngle), MathF.Sin(radialAngle));
            var direction = (baseDirection * (0.26f + h1 * 0.34f) + radial + tangent * (h2 - 0.5f) * 0.32f).Normalized();
            var start = position + direction * radius * (0.08f + h2 * 0.10f);
            var end = position + direction * radius * (0.30f + h1 * 0.58f);
            var width = Math.Clamp(radius * (0.035f + h2 * 0.035f), 1.0f, 3.2f);
            DrawLine(start, end, WithAlpha(color, alpha * (0.36f + h1 * 0.62f)), width, true);
            if (h2 > 0.52f)
            {
                DrawTexturedQuad(_spark, end, 0f, Vector2.One * Math.Clamp(radius * (0.11f + h0 * 0.06f), 2f, 7f), WithAlpha(color, alpha * 0.44f));
            }
        }
    }

    private void DrawShieldSurfaceStreaks(ProjectileImpactState impact, Vector2 contact, Vector2 tangent, Vector2 normal, float radius, int count, Color color, float alpha)
    {
        if (alpha <= 0f || radius <= 0f)
        {
            return;
        }

        var baseSign = Hash01(impact.Seed * 0.037f) > 0.5f ? 1f : -1f;
        for (var i = 0; i < count; i++)
        {
            var h0 = Hash01(impact.Seed * 0.041f + i * 6.13f);
            var h1 = Hash01(impact.Seed * 0.059f + i * 12.91f);
            var h2 = Hash01(impact.Seed * 0.071f + i * 19.37f);
            var side = (i & 1) == 0 ? baseSign : -baseSign;
            var startOffset = (h0 - 0.5f) * radius * 0.52f;
            var length = radius * (0.42f + h1 * 0.70f);
            var lift = -normal * radius * (0.010f + h2 * 0.040f);
            var start = contact + tangent * startOffset + lift;
            var end = start + tangent * side * length - normal * radius * (0.015f + h2 * 0.055f);
            var width = Math.Clamp(radius * (0.018f + h2 * 0.020f), 0.7f, 2.0f);
            DrawLine(start, end, WithAlpha(color, alpha * (0.30f + h1 * 0.58f)), width, true);
            if (h2 > 0.55f)
            {
                DrawTexturedQuad(_spark, end, tangent.Angle(), new Vector2(Math.Clamp(radius * (0.12f + h0 * 0.08f), 2f, 7f), Math.Clamp(radius * (0.045f + h1 * 0.035f), 1.2f, 4.0f)), WithAlpha(color, alpha * 0.34f));
            }
        }
    }

    private void DrawShieldFilaments(ProjectileImpactState impact, ShieldShape shield, float t, ShieldColors palette, float alpha)
    {
        var count = 5;
        var maxExtent = Math.Max(shield.Extents.X, shield.Extents.Y);
        for (var i = 0; i < count; i++)
        {
            var h0 = Hash01(impact.Seed * 0.041f + i * 7.31f);
            var h1 = Hash01(impact.Seed * 0.053f + i * 13.47f);
            var h2 = Hash01(impact.Seed * 0.067f + i * 19.83f);
            var start = h0 * MathF.Tau + t * (0.34f + h1 * 0.24f);
            var length = MathF.Tau * (0.13f + h1 * 0.18f);
            var extents = shield.Extents * (0.54f + h2 * 0.38f);
            var width = Math.Clamp(maxExtent * (0.006f + h0 * 0.006f), 0.65f, 1.75f);
            DrawEllipseArc(shield.Center, extents, shield.Rotation, start, start + length, 22, WithAlpha(palette.Line, alpha * (0.10f + h2 * 0.12f)), width);
        }
    }

    private static float BaseSize(ProjectileImpactState impact)
    {
        var damageScale = MathF.Sqrt(MathF.Max(1f, impact.Damage)) * 1.90f;
        var speedScale = Math.Clamp(impact.Speed / 1450f, 0.72f, 1.35f);
        var surfaceScale = impact.Surface switch
        {
            ProjectileImpactSurface.Shield => 1.15f,
            ProjectileImpactSurface.Asteroid => 1.28f,
            ProjectileImpactSurface.Structure => 1.05f,
            _ => 0.95f
        };
        var kindScale = impact.Kind switch
        {
            ProjectileImpactKind.AsteroidCollision => 1.28f,
            ProjectileImpactKind.SunBurn => 0.78f,
            _ => 1f
        };
        var minSize = impact.Kind == ProjectileImpactKind.SunBurn ? 18f : 24f;
        var maxSize = impact.Kind switch
        {
            ProjectileImpactKind.AsteroidCollision => 82f,
            ProjectileImpactKind.SunBurn => 46f,
            _ => 58f
        };
        return Math.Clamp((16f + damageScale) * speedScale * surfaceScale * kindScale, minSize, maxSize);
    }

    private static float LifetimeFor(ProjectileImpactState impact)
    {
        return impact.Kind switch
        {
            ProjectileImpactKind.AsteroidCollision => impact.Surface == ProjectileImpactSurface.Shield ? 0.46f : 0.58f,
            ProjectileImpactKind.SunBurn => impact.Surface == ProjectileImpactSurface.Shield ? 0.40f : 0.48f,
            _ => impact.Surface == ProjectileImpactSurface.Asteroid ? 0.46f : impact.Surface == ProjectileImpactSurface.Shield ? ShieldLifetime : 0.38f
        };
    }

    private bool IsSameShieldTarget(ProjectileImpactState existing, ProjectileImpactState incoming)
    {
        if (existing.Surface != ProjectileImpactSurface.Shield || incoming.Surface != ProjectileImpactSurface.Shield)
        {
            return false;
        }

        if (existing.TargetId != 0 && incoming.TargetId != 0)
        {
            return existing.TargetId == incoming.TargetId;
        }

        var existingShape = ShieldShapeFor(existing);
        var incomingShape = ShieldShapeFor(incoming);
        var mergeDistance = Math.Max(existingShape.Extents.Length(), incomingShape.Extents.Length()) * 0.35f;
        return existingShape.Center.DistanceSquaredTo(incomingShape.Center) <= mergeDistance * mergeDistance;
    }

    private ShieldShape ShieldShapeFor(ProjectileImpactState impact)
    {
        if (impact.TargetId != 0 && _shieldTargets.TryGetValue(impact.TargetId, out var target))
        {
            return ShieldShapeFrom(target.Center, target.Radius, target.Size, target.Rotation);
        }

        return ShieldShapeFrom(impact.TargetCenter.ToGodot(), impact.TargetRadius, impact.TargetSize.ToGodot(), impact.TargetRotation);
    }

    private static ShieldShape ShieldShapeFrom(Vector2 center, float radius, Vector2 size, float rotation)
    {
        var halfX = MathF.Abs(size.X) * 0.5f;
        var halfY = MathF.Abs(size.Y) * 0.5f;
        if (halfX <= 1f && halfY <= 1f && radius > 1f)
        {
            halfX = MathF.Max(halfX, radius * 0.52f);
            halfY = MathF.Max(halfY, radius * 0.52f);
        }

        halfX = MathF.Max(4f, halfX);
        halfY = MathF.Max(4f, halfY);
        var major = MathF.Max(halfX, halfY);
        var roundedMinor = major * 0.60f;
        var padding = Math.Clamp(6f + major * 0.16f, 8f, 26f);
        var minExtent = Math.Clamp(major * 0.72f + padding * 0.45f, 22f, 58f);
        var extents = new Vector2(
            Math.Clamp(MathF.Max(halfX, roundedMinor) + padding, minExtent, 210f),
            Math.Clamp(MathF.Max(halfY, roundedMinor) + padding, minExtent, 210f));
        return new ShieldShape(center, extents, rotation);
    }

    private float ShieldCullRadius(ProjectileImpactState impact)
    {
        var shape = ShieldShapeFor(impact);
        return shape.Extents.Length();
    }

    private static ShieldColors ShieldPalette(float shieldRatio)
    {
        var ratio = Math.Clamp(shieldRatio, 0f, 1f);
        var danger = 1f - SmoothStep(0.14f, 0.78f, ratio);
        var depleted = 1f - SmoothStep(0.02f, 0.16f, ratio);
        var healthy = new Color(0.02f, 0.26f, 1.00f, 1f).Lerp(new Color(0.06f, 0.78f, 1.00f, 1f), 0.28f);
        var warning = new Color(1.00f, 0.12f, 0.07f, 1f);
        var dim = new Color(0.42f, 0.045f, 0.035f, 1f);
        var field = healthy.Lerp(warning, danger).Lerp(dim, depleted * 0.58f);
        var rim = new Color(0.68f, 0.98f, 1.00f, 1f).Lerp(new Color(1.00f, 0.26f, 0.16f, 1f), danger).Lerp(new Color(0.72f, 0.08f, 0.055f, 1f), depleted * 0.48f);
        var line = new Color(0.30f, 0.78f, 1.00f, 1f).Lerp(new Color(1.00f, 0.20f, 0.12f, 1f), danger).Lerp(dim, depleted * 0.40f);
        var core = new Color(0.82f, 1.00f, 1.00f, 1f).Lerp(new Color(1.00f, 0.50f, 0.32f, 1f), danger).Lerp(new Color(0.88f, 0.18f, 0.12f, 1f), depleted * 0.42f);
        return new ShieldColors(field, rim, line, core);
    }

    private static float ShieldAlphaPower(float shieldRatio)
    {
        var ratio = Math.Clamp(shieldRatio, 0f, 1f);
        var depletionFade = 0.58f + SmoothStep(0.02f, 0.18f, ratio) * 0.42f;
        return (0.70f + ratio * 0.38f) * depletionFade;
    }

    private void DrawEllipseFilled(Vector2 center, Vector2 extents, float rotation, Color color, int segments)
    {
        if (color.A <= 0f || extents.X <= 0f || extents.Y <= 0f || segments < 3)
        {
            return;
        }

        var points = EllipseFillPointBuffer(segments);
        for (var i = 0; i < segments; i++)
        {
            points[i] = EllipsePoint(center, extents, rotation, i * MathF.Tau / segments);
        }

        DrawColoredPolygon(points, color);
    }

    private void DrawEllipseArc(Vector2 center, Vector2 extents, float rotation, float startAngle, float endAngle, int segments, Color color, float width)
    {
        if (color.A <= 0f || extents.X <= 0f || extents.Y <= 0f || width <= 0f || segments < 2)
        {
            return;
        }

        var points = EllipseArcPointBuffer(segments);
        for (var i = 0; i <= segments; i++)
        {
            var t = i / (float)segments;
            points[i] = EllipsePoint(center, extents, rotation, Mathf.Lerp(startAngle, endAngle, t));
        }

        DrawPolyline(points, color, width, true);
    }

    private static Vector2 PointOnEllipse(ShieldShape shield, Vector2 worldDirection, float scale)
    {
        var direction = worldDirection.LengthSquared() > 0.0001f ? worldDirection.Normalized() : Vector2.Up;
        var localDirection = direction.Rotated(-shield.Rotation);
        var denom = MathF.Sqrt(
            localDirection.X * localDirection.X / (shield.Extents.X * shield.Extents.X)
            + localDirection.Y * localDirection.Y / (shield.Extents.Y * shield.Extents.Y));
        if (denom <= 0.000001f)
        {
            return shield.Center;
        }

        return shield.Center + (localDirection / denom * scale).Rotated(shield.Rotation);
    }

    private static ShieldContact ShieldContactFor(ShieldShape shield, Vector2 worldDirection, float scale)
    {
        var direction = worldDirection.LengthSquared() > 0.0001f ? worldDirection.Normalized() : Vector2.Up;
        var localDirection = direction.Rotated(-shield.Rotation);
        var extentX = MathF.Max(1f, shield.Extents.X);
        var extentY = MathF.Max(1f, shield.Extents.Y);
        var denom = MathF.Sqrt(
            localDirection.X * localDirection.X / (extentX * extentX)
            + localDirection.Y * localDirection.Y / (extentY * extentY));
        if (denom <= 0.000001f)
        {
            return new ShieldContact(shield.Center, direction, new Vector2(-direction.Y, direction.X), 0f);
        }

        var localPoint = localDirection / denom;
        var angle = MathF.Atan2(localPoint.Y / extentY, localPoint.X / extentX);
        var localNormal = new Vector2(localPoint.X / (extentX * extentX), localPoint.Y / (extentY * extentY)).Normalized();
        var localTangent = new Vector2(-extentX * MathF.Sin(angle), extentY * MathF.Cos(angle)).Normalized();
        var normal = localNormal.Rotated(shield.Rotation);
        var tangent = localTangent.Rotated(shield.Rotation);
        var point = shield.Center + (localPoint * scale).Rotated(shield.Rotation);
        return new ShieldContact(point, normal, tangent, angle);
    }

    private static Vector2 EllipsePoint(Vector2 center, Vector2 extents, float rotation, float angle)
    {
        return center + new Vector2(MathF.Cos(angle) * extents.X, MathF.Sin(angle) * extents.Y).Rotated(rotation);
    }

    private Vector2[] EllipseFillPointBuffer(int segments)
    {
        if (!_ellipseFillPointBuffers.TryGetValue(segments, out var points))
        {
            points = new Vector2[segments];
            _ellipseFillPointBuffers[segments] = points;
        }

        return points;
    }

    private Vector2[] EllipseArcPointBuffer(int segments)
    {
        if (!_ellipseArcPointBuffers.TryGetValue(segments, out var points))
        {
            points = new Vector2[segments + 1];
            _ellipseArcPointBuffers[segments] = points;
        }

        return points;
    }

    private void DrawTexturedQuad(Texture2D? texture, Vector2 center, float rotation, Vector2 size, Color color)
    {
        if (texture is null || size.X <= 0f || size.Y <= 0f || color.A <= 0f)
        {
            return;
        }

        DrawSetTransform(center, rotation, Vector2.One);
        DrawTextureRect(texture, new Rect2(-size * 0.5f, size), false, color);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private static Texture2D? LoadTexture(string resourcePath)
    {
        if (ResourceLoader.Exists(resourcePath))
        {
            return ResourceLoader.Load<Texture2D>(resourcePath);
        }

        var globalPath = ProjectSettings.GlobalizePath(resourcePath);
        if (!File.Exists(globalPath))
        {
            return null;
        }

        var image = Image.LoadFromFile(globalPath);
        if (image is null || image.GetWidth() <= 0 || image.GetHeight() <= 0)
        {
            return null;
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, Math.Clamp(alpha, 0f, 1f));
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Hash01(float value)
    {
        var hashed = MathF.Sin(value) * 43758.5453f;
        return hashed - MathF.Floor(hashed);
    }

    private struct ImpactView
    {
        public ImpactView(ProjectileImpactState state, float lifetime)
        {
            State = state;
            Lifetime = lifetime;
            Age = 0f;
        }

        public ProjectileImpactState State { get; }
        public float Lifetime { get; }
        public float Age { get; set; }
    }

    private readonly record struct ShieldColors(Color Field, Color Rim, Color Line, Color Core);
    private readonly record struct ShieldShape(Vector2 Center, Vector2 Extents, float Rotation);
    private readonly record struct ShieldContact(Vector2 Point, Vector2 Normal, Vector2 Tangent, float Angle);
    private readonly record struct ShieldTargetVisual(Vector2 Center, float Radius, Vector2 Size, float Rotation);
}
