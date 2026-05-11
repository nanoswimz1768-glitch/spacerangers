using Godot;
using File = System.IO.File;

namespace SpaceRangersPrototype;

public enum ShipExplosionKind
{
    Enemy,
    Player
}

public partial class ExplosionLayer : Node2D
{
    private const int MaxExplosions = 22;
    private const float FragmentAlphaThreshold = 0.11f;

    private static readonly string[] ShipDebrisPaths =
    [
        "res://assets/effects/ship_debris_00.png",
        "res://assets/effects/ship_debris_01.png",
        "res://assets/effects/ship_debris_02.png",
        "res://assets/effects/ship_debris_03.png",
        "res://assets/effects/ship_debris_04.png",
        "res://assets/effects/ship_debris_05.png"
    ];

    private readonly List<Explosion> _explosions = new();
    private readonly Texture2D?[] _shipDebris = new Texture2D?[ShipDebrisPaths.Length];
    private Texture2D? _smokePuff;
    private Texture2D? _dustRing;
    private Texture2D? _impactFlash;
    private Texture2D? _spark;
    private Texture2D? _heatCorona;
    private Texture2D? _fireGlow;
    private Texture2D? _firePlume;
    private Texture2D? _effectRing;
    private Texture2D? _effectMuzzleFlash;
    private Texture2D? _effectMuzzleFlashFps;
    private Texture2D? _effectSparkle;
    private int _nextSeed = 1;

    public bool UseCulling { get; set; }
    public Rect2 VisibleWorldRect { get; set; }

    public override void _Ready()
    {
        ZIndex = 22;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;

        _smokePuff = LoadTexture("res://assets/effects/asteroid_smoke_puff.png");
        _dustRing = LoadTexture("res://assets/effects/asteroid_dust_ring.png");
        _impactFlash = LoadTexture("res://assets/effects/asteroid_impact_flash.png");
        _spark = LoadTexture("res://assets/effects/asteroid_fire_spark.png");
        _heatCorona = LoadTexture("res://assets/effects/asteroid_heat_corona.png");
        _fireGlow = LoadTexture("res://assets/effects/asteroid_fire_glow.png");
        _firePlume = LoadTexture("res://assets/effects/asteroid_fire_plume.png");
        _effectRing = LoadTexture("res://assets/effects/effectblocks/effectblocks_circle_1.png");
        _effectMuzzleFlash = LoadTexture("res://assets/effects/effectblocks/effectblocks_muzzleflash_1.png");
        _effectMuzzleFlashFps = LoadTexture("res://assets/effects/effectblocks/effectblocks_muzzle_flash_fps_1.png");
        _effectSparkle = LoadTexture("res://assets/effects/effectblocks/effectblocks_sparkle.png");

        for (var index = 0; index < ShipDebrisPaths.Length; index++)
        {
            _shipDebris[index] = LoadTexture(ShipDebrisPaths[index]);
        }
    }

    public override void _ExitTree()
    {
        ClearEffects();
        _smokePuff = null;
        _dustRing = null;
        _impactFlash = null;
        _spark = null;
        _heatCorona = null;
        _fireGlow = null;
        _firePlume = null;
        _effectRing = null;
        _effectMuzzleFlash = null;
        _effectMuzzleFlashFps = null;
        _effectSparkle = null;

        for (var index = 0; index < _shipDebris.Length; index++)
        {
            _shipDebris[index] = null;
        }
    }

    public void ClearEffects()
    {
        _explosions.Clear();
        QueueRedraw();
    }

    public void Spawn(Vector2 position, float radius, Color tint)
    {
        SpawnShip(position, radius, tint, ShipExplosionKind.Enemy, null, 1f, 0f, default, Array.Empty<EnginePort>());
    }

    public void SpawnShip(
        Vector2 position,
        float radius,
        Color tint,
        ShipExplosionKind kind,
        Texture2D? shipTexture,
        float shipScale,
        float shipRotation,
        Rect2 shipContentBounds,
        IReadOnlyList<EnginePort>? exhaustPorts)
    {
        while (_explosions.Count >= MaxExplosions)
        {
            _explosions.RemoveAt(0);
        }

        var clampedRadius = kind == ShipExplosionKind.Player
            ? Math.Clamp(radius, 128f, 310f)
            : Math.Clamp(radius, 82f, 215f);
        var lifetime = kind == ShipExplosionKind.Player ? 2.55f : 1.95f;
        var seed = _nextSeed++;
        var contentBounds = SanitizeContentBounds(shipTexture, shipContentBounds);
        var explosion = new Explosion(
            position,
            clampedRadius,
            tint,
            kind,
            seed,
            lifetime,
            shipTexture,
            Math.Clamp(shipScale, 0.05f, 4f),
            shipRotation,
            contentBounds,
            exhaustPorts?.ToArray() ?? Array.Empty<EnginePort>());

        explosion.ShipFragments = BuildShipFragments(explosion);
        _explosions.Add(explosion);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_explosions.Count == 0)
        {
            return;
        }

        var dt = (float)delta;
        for (var index = _explosions.Count - 1; index >= 0; index--)
        {
            var explosion = _explosions[index];
            explosion.Age += dt;
            if (explosion.Age >= explosion.Lifetime)
            {
                _explosions.RemoveAt(index);
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var explosion in _explosions)
        {
            DrawExplosion(explosion);
        }
    }

    private void DrawExplosion(Explosion explosion)
    {
        if (UseCulling && !VisibleWorldRect.Grow(explosion.Radius * 5.4f).HasPoint(explosion.Position))
        {
            return;
        }

        var t = Math.Clamp(explosion.Age / explosion.Lifetime, 0f, 1f);
        var seed = explosion.Seed * 0.0137f;

        DrawSmokeClouds(explosion, t, seed, frontLayer: false);
        DrawShockAndBloom(explosion, t, seed);
        DrawCoreFlash(explosion, t, seed);
        DrawShipFragments(explosion, t);
        DrawMetalSplinters(explosion, t, seed);
        DrawSparks(explosion, t, seed);
        DrawSmokeClouds(explosion, t, seed + 37.2f, frontLayer: true);
    }

    private void DrawShockAndBloom(Explosion explosion, float t, float seed)
    {
        var radius = explosion.Radius;
        var isPlayer = explosion.Kind == ShipExplosionKind.Player;
        var power = isPlayer ? 1.26f : 1f;
        var shock = SmoothStep(0.0f, 0.64f, t);
        var earlyFlash = 1f - SmoothStep(0.0f, 0.18f, t);
        var bloomFade = 1f - SmoothStep(0.14f, 0.88f, t);
        var ringFade = MathF.Pow(1f - shock, 2.15f);
        var hot = Mix(new Color(1f, 0.83f, 0.24f, 1f), explosion.Tint, 0.22f);

        DrawTexturedQuad(
            _dustRing,
            explosion.Position,
            seed,
            Vector2.One * radius * (1.05f + shock * 2.45f) * power,
            new Color(0.78f, 0.58f, 0.36f, (0.12f + earlyFlash * 0.08f) * ringFade * power));

        DrawTexturedQuad(
            _effectRing,
            explosion.Position,
            -seed * 0.42f,
            Vector2.One * radius * (0.76f + shock * 3.2f) * power,
            WithAlpha(Mix(hot, new Color(0.65f, 0.88f, 1f, 1f), isPlayer ? 0.22f : 0.08f), 0.13f * ringFade * power));

        if (isPlayer)
        {
            DrawTexturedQuad(
                _effectRing,
                explosion.Position,
                seed * 0.73f,
                Vector2.One * radius * (0.52f + shock * 4.2f),
                new Color(0.46f, 0.82f, 1f, 0.06f * ringFade));
        }

        DrawTexturedQuad(
            _heatCorona,
            explosion.Position,
            seed,
            Vector2.One * radius * (2.2f + shock * 1.25f) * power,
            new Color(1f, 0.18f, 0.025f, 0.42f * bloomFade * power));

        DrawTexturedQuad(
            _fireGlow,
            explosion.Position,
            0f,
            Vector2.One * radius * (1.25f + shock * 1.2f) * power,
            WithAlpha(hot, (0.48f * earlyFlash + 0.18f * bloomFade) * power));
    }

    private void DrawCoreFlash(Explosion explosion, float t, float seed)
    {
        var radius = explosion.Radius;
        var isPlayer = explosion.Kind == ShipExplosionKind.Player;
        var power = isPlayer ? 1.22f : 1f;
        var flash = 1f - SmoothStep(0.0f, 0.17f, t);
        var body = 1f - SmoothStep(0.08f, 0.74f, t);
        var ignition = flash * 0.76f + body * 0.34f;
        var core = Mix(new Color(1f, 0.92f, 0.48f, 1f), explosion.Tint, 0.24f);

        DrawTexturedQuad(
            _impactFlash,
            explosion.Position,
            seed * 0.48f,
            Vector2.One * radius * (1.04f + flash * 0.62f + body * 0.52f) * power,
            WithAlpha(core, ignition * power));

        DrawTexturedQuad(
            _effectMuzzleFlashFps,
            explosion.Position,
            seed * 1.7f,
            new Vector2(radius * 1.72f, radius * 1.42f) * (0.86f + power * 0.16f),
            new Color(1f, 0.72f, 0.18f, flash * 0.58f * power));

        var flareCount = isPlayer ? 7 : 5;
        for (var i = 0; i < flareCount; i++)
        {
            var h0 = Hash01(seed + i * 4.31f);
            var h1 = Hash01(seed + i * 9.17f);
            var h2 = Hash01(seed + i * 15.73f);
            var angle = h0 * MathF.Tau + t * (h1 - 0.5f) * 0.32f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var length = radius * (0.84f + h1 * 0.92f) * power * (0.82f + flash * 0.28f);
            var width = radius * (0.38f + h2 * 0.46f) * power;
            var center = explosion.Position + direction * radius * (0.10f + h2 * 0.10f) * SmoothStep(0f, 0.45f, t);
            var alpha = (flash * 0.36f + body * 0.15f) * (0.72f + h1 * 0.28f);
            var color = h2 > 0.58f
                ? new Color(1f, 0.54f, 0.08f, alpha)
                : WithAlpha(core, alpha);

            DrawSheetFrame(_effectMuzzleFlash, i % 4, 2, 2, center, angle, new Vector2(length, width), color);
        }

        var plumeCount = isPlayer ? 6 : 4;
        for (var i = 0; i < plumeCount; i++)
        {
            var h0 = Hash01(seed + i * 19.27f);
            var h1 = Hash01(seed + i * 31.41f);
            var h2 = Hash01(seed + i * 47.09f);
            var angle = h0 * MathF.Tau;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var expansion = SmoothStep(0.02f, 0.72f, t);
            var length = radius * (0.62f + h1 * 1.02f) * expansion * power;
            var width = radius * (0.38f + h2 * 0.58f) * (1f - t * 0.16f) * power;
            var center = explosion.Position + direction * length * 0.42f;
            var alpha = (1f - SmoothStep(0.18f, 0.92f, t)) * (0.12f + h1 * 0.14f) * power;
            DrawTexturedQuad(_firePlume, center, angle, new Vector2(length, width), new Color(1f, 0.34f, 0.035f, alpha));
        }
    }

    private void DrawShipFragments(Explosion explosion, float t)
    {
        if (explosion.ShipTexture is null || explosion.ShipFragments.Length == 0)
        {
            return;
        }

        var breakup = SmoothStep(0.0f, 0.48f, t);
        var globalFade = 1f - SmoothStep(0.68f, 1.0f, t);
        for (var index = 0; index < explosion.ShipFragments.Length; index++)
        {
            var fragment = explosion.ShipFragments[index];
            var localAge = explosion.Age - fragment.Delay;
            if (localAge <= 0f)
            {
                continue;
            }

            var localT = Math.Clamp(localAge / explosion.Lifetime, 0f, 1f);
            var outward = fragment.Direction * fragment.Speed * localAge * (1f - localT * 0.22f);
            var opening = fragment.LocalOffset * (1f + breakup * 0.18f);
            var local = opening + outward;
            var world = explosion.Position + local.Rotated(explosion.ShipRotation);
            var rotation = explosion.ShipRotation + fragment.BaseRotation + fragment.Spin * localAge;
            var size = fragment.Source.Size * explosion.ShipScale * fragment.Scale * (1f - localT * 0.11f);
            if (size.X <= 1f || size.Y <= 1f)
            {
                continue;
            }

            var alpha = globalFade * (1f - SmoothStep(0.78f, 1.0f, localT));
            var heat = fragment.Heat * (1f - SmoothStep(0.18f, 0.76f, localT));
            var shade = 1f - localT * 0.46f;
            var color = new Color(
                Math.Clamp(shade + heat * 0.48f, 0f, 1.35f),
                Math.Clamp(shade * 0.92f + heat * 0.22f, 0f, 1.18f),
                Math.Clamp(shade * 0.84f + heat * 0.06f, 0f, 1.05f),
                alpha);

            DrawTextureRegion(explosion.ShipTexture, fragment.Source, world, rotation, size, color);
        }
    }

    private void DrawMetalSplinters(Explosion explosion, float t, float seed)
    {
        var expansion = SmoothStep(0.0f, 0.74f, t);
        var fade = 1f - SmoothStep(0.44f, 1.0f, t);
        if (fade <= 0f)
        {
            return;
        }

        var count = explosion.Kind == ShipExplosionKind.Player ? 20 : 12;
        var radius = explosion.Radius;
        for (var i = 0; i < count; i++)
        {
            var texture = DebrisTexture(i);
            var h0 = Hash01(seed + i * 6.31f);
            var h1 = Hash01(seed + i * 13.09f);
            var h2 = Hash01(seed + i * 24.71f);
            var h3 = Hash01(seed + i * 39.43f);
            var angle = h0 * MathF.Tau;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var travel = radius * (0.36f + h1 * 3.1f) * expansion;
            var position = explosion.Position + direction * travel;
            var length = radius * (0.14f + h2 * 0.22f) * (1f - t * 0.18f);
            var width = radius * (0.035f + h3 * 0.06f);
            var alpha = fade * (0.38f + h1 * 0.28f);
            var color = h2 > 0.68f
                ? new Color(1f, 0.63f, 0.22f, alpha)
                : new Color(0.82f, 0.88f, 0.88f, alpha * 0.86f);
            DrawTexturedQuad(texture, position, angle + t * (h1 - 0.5f) * 7.0f, new Vector2(length, width), color);
        }
    }

    private void DrawSparks(Explosion explosion, float t, float seed)
    {
        var radius = explosion.Radius;
        var isPlayer = explosion.Kind == ShipExplosionKind.Player;
        var count = isPlayer ? 92 : 58;
        var burst = SmoothStep(0.0f, 0.56f, t);
        var fade = 1f - SmoothStep(0.22f, 1.0f, t);
        if (fade <= 0f)
        {
            return;
        }

        for (var i = 0; i < count; i++)
        {
            var h0 = Hash01(seed + i * 17.17f);
            var h1 = Hash01(seed + i * 23.61f);
            var h2 = Hash01(seed + i * 41.09f);
            var h3 = Hash01(seed + i * 59.57f);
            var angle = h0 * MathF.Tau;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var travel = radius * (0.24f + h1 * (isPlayer ? 4.9f : 3.85f)) * burst;
            var jitter = new Vector2(-direction.Y, direction.X) * radius * (h2 - 0.5f) * 0.38f * burst;
            var position = explosion.Position + direction * travel + jitter;
            var length = Math.Clamp(radius * (0.055f + h2 * 0.13f) * (1f - t * 0.34f), 5f, isPlayer ? 30f : 22f);
            var width = Math.Clamp(radius * (0.012f + h3 * 0.024f), 1.7f, isPlayer ? 6f : 4.8f);
            var alpha = fade * (0.24f + h0 * 0.42f);
            var color = h2 > 0.72f
                ? new Color(0.72f, 0.96f, 1f, alpha * 0.72f)
                : new Color(1f, 0.48f + h2 * 0.34f, 0.06f, alpha);

            DrawTexturedQuad(_spark, position, angle, new Vector2(length, width), color);
        }

        var glintCount = isPlayer ? 14 : 8;
        for (var i = 0; i < glintCount; i++)
        {
            var h0 = Hash01(seed + i * 71.11f);
            var h1 = Hash01(seed + i * 83.31f);
            var h2 = Hash01(seed + i * 97.91f);
            var angle = h0 * MathF.Tau;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var position = explosion.Position + direction * radius * (0.36f + h1 * 2.4f) * burst;
            var size = radius * (0.055f + h2 * 0.05f) * (1f - t * 0.42f);
            DrawTexturedQuad(_effectSparkle, position, angle + t * 2.8f, Vector2.One * size, new Color(0.66f, 0.92f, 1f, fade * 0.16f));
        }
    }

    private void DrawSmokeClouds(Explosion explosion, float t, float seed, bool frontLayer)
    {
        var smokeBirth = SmoothStep(frontLayer ? 0.12f : 0.04f, frontLayer ? 0.36f : 0.24f, t);
        var smokeFade = 1f - SmoothStep(0.70f, 1.0f, t);
        var opacity = smokeBirth * smokeFade;
        if (opacity <= 0f)
        {
            return;
        }

        var radius = explosion.Radius;
        var isPlayer = explosion.Kind == ShipExplosionKind.Player;
        var count = (frontLayer ? 8 : 18) + (isPlayer ? (frontLayer ? 6 : 10) : 0);
        var expansion = SmoothStep(0.0f, 0.92f, t);
        for (var i = 0; i < count; i++)
        {
            var h0 = Hash01(seed + i * 5.87f);
            var h1 = Hash01(seed + i * 12.29f);
            var h2 = Hash01(seed + i * 29.63f);
            var h3 = Hash01(seed + i * 44.51f);
            var angle = h0 * MathF.Tau;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var travel = radius * (frontLayer ? 0.18f : 0.28f) + radius * (0.54f + h1 * (isPlayer ? 2.7f : 2.1f)) * expansion;
            var position = explosion.Position + direction * travel + new Vector2(-direction.Y, direction.X) * radius * (h2 - 0.5f) * 0.72f * expansion;
            var size = radius * (0.55f + h2 * 0.92f) * (0.70f + expansion * 0.72f);
            var alpha = opacity * (frontLayer ? 0.11f : 0.16f) * (0.62f + h3 * 0.52f);
            var warmth = 0.08f + h1 * 0.10f;
            var color = new Color(0.12f + warmth, 0.105f + warmth * 0.7f, 0.095f + warmth * 0.38f, alpha);

            DrawTexturedQuad(_smokePuff, position, angle + t * (h1 - 0.5f) * 0.75f, Vector2.One * size, color);
        }
    }

    private ShipFragment[] BuildShipFragments(Explosion explosion)
    {
        if (explosion.ShipTexture is null || explosion.ShipContentBounds.Size.X <= 2f || explosion.ShipContentBounds.Size.Y <= 2f)
        {
            return [];
        }

        var textureSize = new Vector2(explosion.ShipTexture.GetWidth(), explosion.ShipTexture.GetHeight());
        if (textureSize.X <= 1f || textureSize.Y <= 1f)
        {
            return [];
        }

        var image = explosion.ShipTexture.GetImage();
        var isPlayer = explosion.Kind == ShipExplosionKind.Player;
        var fragments = new List<ShipFragment>(isPlayer ? 30 : 22);
        var recipes = BuildStructuralFragmentRecipes(isPlayer);
        var seed = explosion.Seed * 0.0179f;

        for (var index = 0; index < recipes.Length; index++)
        {
            TryAddStructuralFragment(explosion, image, textureSize, fragments, recipes[index], seed, index);
        }

        AddEnginePortFragments(explosion, image, textureSize, fragments, seed + 73.1f);
        AddSecondaryHullFragments(explosion, image, textureSize, fragments, seed + 143.7f, isPlayer ? 10 : 6);
        return fragments.ToArray();
    }

    private static FragmentRecipe[] BuildStructuralFragmentRecipes(bool isPlayer)
    {
        var playerBoost = isPlayer ? 1.14f : 1f;
        return
        [
            new FragmentRecipe(new Rect2(0.34f, 0.00f, 0.32f, 0.27f), new Vector2(0f, -1f), 1.04f * playerBoost, 0.58f, 4.4f, 0.00f, 0.88f, 1.04f),
            new FragmentRecipe(new Rect2(0.31f, 0.20f, 0.38f, 0.28f), new Vector2(0f, -0.34f), 0.74f * playerBoost, 0.46f, 3.2f, 0.025f, 0.78f, 1.06f),
            new FragmentRecipe(new Rect2(0.30f, 0.43f, 0.40f, 0.28f), new Vector2(0f, 0.12f), 0.58f * playerBoost, 0.42f, 2.8f, 0.040f, 0.62f, 1.03f),
            new FragmentRecipe(new Rect2(0.36f, 0.68f, 0.28f, 0.30f), new Vector2(0f, 1f), 1.08f * playerBoost, 0.62f, 5.2f, 0.018f, 0.96f, 1.02f),
            new FragmentRecipe(new Rect2(0.00f, 0.12f, 0.43f, 0.43f), new Vector2(-1f, -0.34f), 1.16f * playerBoost, 0.70f, 5.4f, 0.010f, 0.74f, 1.08f),
            new FragmentRecipe(new Rect2(0.57f, 0.12f, 0.43f, 0.43f), new Vector2(1f, -0.34f), 1.16f * playerBoost, 0.70f, 5.4f, 0.010f, 0.74f, 1.08f),
            new FragmentRecipe(new Rect2(0.02f, 0.42f, 0.42f, 0.36f), new Vector2(-1f, 0.12f), 1.02f * playerBoost, 0.64f, 4.9f, 0.030f, 0.68f, 1.02f),
            new FragmentRecipe(new Rect2(0.56f, 0.42f, 0.42f, 0.36f), new Vector2(1f, 0.12f), 1.02f * playerBoost, 0.64f, 4.9f, 0.030f, 0.68f, 1.02f),
            new FragmentRecipe(new Rect2(0.05f, 0.68f, 0.38f, 0.30f), new Vector2(-0.72f, 0.82f), 1.20f * playerBoost, 0.70f, 6.0f, 0.025f, 1.00f, 0.96f),
            new FragmentRecipe(new Rect2(0.57f, 0.68f, 0.38f, 0.30f), new Vector2(0.72f, 0.82f), 1.20f * playerBoost, 0.70f, 6.0f, 0.025f, 1.00f, 0.96f),
            new FragmentRecipe(new Rect2(0.18f, 0.76f, 0.28f, 0.22f), new Vector2(-0.38f, 1f), 1.28f * playerBoost, 0.68f, 6.4f, 0.000f, 1.00f, 0.92f),
            new FragmentRecipe(new Rect2(0.54f, 0.76f, 0.28f, 0.22f), new Vector2(0.38f, 1f), 1.28f * playerBoost, 0.68f, 6.4f, 0.000f, 1.00f, 0.92f)
        ];
    }

    private static void TryAddStructuralFragment(
        Explosion explosion,
        Image? image,
        Vector2 textureSize,
        List<ShipFragment> fragments,
        FragmentRecipe recipe,
        float seed,
        int index)
    {
        var source = SourceFromNormalized(explosion.ShipContentBounds, recipe.NormalizedSource);
        TryAddSourceFragment(explosion, image, textureSize, fragments, source, recipe, seed, index);
    }

    private static void AddEnginePortFragments(
        Explosion explosion,
        Image? image,
        Vector2 textureSize,
        List<ShipFragment> fragments,
        float seed)
    {
        if (explosion.EnginePorts.Count == 0)
        {
            return;
        }

        var portLimit = Math.Min(explosion.EnginePorts.Count, explosion.Kind == ShipExplosionKind.Player ? 6 : 4);
        for (var index = 0; index < portLimit; index++)
        {
            var port = explosion.EnginePorts[index];
            var center = textureSize * 0.5f + port.Position;
            var sourceSize = new Vector2(
                Math.Clamp(port.Radius * 8.5f, 34f, 96f),
                Math.Clamp(port.Radius * 10.5f, 40f, 112f));
            var source = ClampSourceRect(new Rect2(center - sourceSize * 0.5f, sourceSize), textureSize);
            var localDirection = port.Position.LengthSquared() > 1f
                ? new Vector2(port.Position.X * 0.34f, Math.Max(port.Position.Y, 42f)).Normalized()
                : new Vector2(0f, 1f);
            var recipe = new FragmentRecipe(default, localDirection, 1.36f, 0.70f, 7.2f, 0.0f, 1.0f, 0.94f);
            TryAddSourceFragment(explosion, image, textureSize, fragments, source, recipe, seed, 100 + index);
        }
    }

    private static void AddSecondaryHullFragments(
        Explosion explosion,
        Image? image,
        Vector2 textureSize,
        List<ShipFragment> fragments,
        float seed,
        int count)
    {
        var bounds = explosion.ShipContentBounds;
        for (var index = 0; index < count; index++)
        {
            var h0 = Hash01(seed + index * 7.41f);
            var h1 = Hash01(seed + index * 13.83f);
            var h2 = Hash01(seed + index * 22.47f);
            var center = bounds.Position + new Vector2(h0, h1) * bounds.Size;
            var sourceSize = new Vector2(bounds.Size.X * (0.10f + h2 * 0.10f), bounds.Size.Y * (0.08f + h0 * 0.10f));
            sourceSize.X = Math.Clamp(sourceSize.X, 12f, Math.Max(12f, bounds.Size.X * 0.22f));
            sourceSize.Y = Math.Clamp(sourceSize.Y, 12f, Math.Max(12f, bounds.Size.Y * 0.20f));
            var source = ClampSourceRect(new Rect2(center - sourceSize * 0.5f, sourceSize), textureSize);
            var localOffset = (center - textureSize * 0.5f) * explosion.ShipScale;
            var direction = localOffset.LengthSquared() > 1f
                ? localOffset.Normalized()
                : new Vector2(MathF.Cos(h2 * MathF.Tau), MathF.Sin(h2 * MathF.Tau));
            var recipe = new FragmentRecipe(default, direction, 1.02f, 0.92f, 7.4f, 0.04f + h1 * 0.045f, 0.68f + h2 * 0.28f, 0.76f + h0 * 0.18f);
            TryAddSourceFragment(explosion, image, textureSize, fragments, source, recipe, seed, 200 + index);
        }
    }

    private static void TryAddSourceFragment(
        Explosion explosion,
        Image? image,
        Vector2 textureSize,
        List<ShipFragment> fragments,
        Rect2 requestedSource,
        FragmentRecipe recipe,
        float seed,
        int index)
    {
        if (!TryTrimOpaqueBounds(image, requestedSource, out var source))
        {
            return;
        }

        var h0 = Hash01(seed + index * 4.91f);
        var h1 = Hash01(seed + index * 9.83f);
        var h2 = Hash01(seed + index * 14.77f);
        var h3 = Hash01(seed + index * 21.19f);
        var h4 = Hash01(seed + index * 36.47f);
        var h5 = Hash01(seed + index * 53.03f);
        var sourceCenter = source.Position + source.Size * 0.5f;
        var localOffset = (sourceCenter - textureSize * 0.5f) * explosion.ShipScale;
        var randomAngle = h4 * MathF.Tau;
        var randomDirection = new Vector2(MathF.Cos(randomAngle), MathF.Sin(randomAngle));
        var direction = recipe.LocalDirection.LengthSquared() > 0.0001f
            ? recipe.LocalDirection.Normalized()
            : (localOffset.LengthSquared() > 1f ? localOffset.Normalized() : randomDirection);
        direction = (direction * (0.78f + h2 * 0.22f) + randomDirection * (0.10f + h3 * 0.28f)).Normalized();

        var playerMultiplier = explosion.Kind == ShipExplosionKind.Player ? 1.08f : 1f;
        var speed = explosion.Radius * (recipe.Speed + h1 * recipe.SpeedJitter) * playerMultiplier;
        var spinSign = Hash01(seed + index * 67.7f) > 0.5f ? 1f : -1f;
        var spin = recipe.Spin * (0.46f + h5 * 0.72f) * spinSign;
        var delay = recipe.Delay + h0 * 0.035f;
        var heat = Math.Clamp(recipe.Heat * (0.78f + h4 * 0.44f), 0.18f, 1.2f);
        var scale = recipe.Scale * (0.92f + h2 * 0.16f);
        var baseRotation = (h3 - 0.5f) * 0.22f;

        fragments.Add(new ShipFragment(source, localOffset, direction, speed, spin, delay, heat, scale, baseRotation));
    }

    private static Rect2 SanitizeContentBounds(Texture2D? texture, Rect2 contentBounds)
    {
        if (texture is null || texture.GetWidth() <= 0 || texture.GetHeight() <= 0)
        {
            return default;
        }

        var textureSize = new Vector2(texture.GetWidth(), texture.GetHeight());
        if (contentBounds.Size.X <= 1f || contentBounds.Size.Y <= 1f)
        {
            return new Rect2(Vector2.Zero, textureSize);
        }

        var left = Math.Clamp(contentBounds.Position.X, 0f, textureSize.X - 1f);
        var top = Math.Clamp(contentBounds.Position.Y, 0f, textureSize.Y - 1f);
        var right = Math.Clamp(contentBounds.Position.X + contentBounds.Size.X, left + 1f, textureSize.X);
        var bottom = Math.Clamp(contentBounds.Position.Y + contentBounds.Size.Y, top + 1f, textureSize.Y);
        return new Rect2(new Vector2(left, top), new Vector2(right - left, bottom - top));
    }

    private static Rect2 SourceFromNormalized(Rect2 bounds, Rect2 normalized)
    {
        var position = bounds.Position + new Vector2(normalized.Position.X * bounds.Size.X, normalized.Position.Y * bounds.Size.Y);
        var size = new Vector2(normalized.Size.X * bounds.Size.X, normalized.Size.Y * bounds.Size.Y);
        return new Rect2(position, size);
    }

    private static Rect2 ClampSourceRect(Rect2 source, Vector2 textureSize)
    {
        var width = Math.Clamp(source.Size.X, 1f, textureSize.X);
        var height = Math.Clamp(source.Size.Y, 1f, textureSize.Y);
        var left = Math.Clamp(source.Position.X, 0f, Math.Max(0f, textureSize.X - width));
        var top = Math.Clamp(source.Position.Y, 0f, Math.Max(0f, textureSize.Y - height));
        return new Rect2(new Vector2(left, top), new Vector2(width, height));
    }

    private static bool TryTrimOpaqueBounds(Image? image, Rect2 requestedSource, out Rect2 trimmed)
    {
        trimmed = default;
        if (image is null || image.IsEmpty())
        {
            if (requestedSource.Size.X <= 2f || requestedSource.Size.Y <= 2f)
            {
                return false;
            }

            trimmed = requestedSource;
            return true;
        }

        var imageWidth = image.GetWidth();
        var imageHeight = image.GetHeight();
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            return false;
        }

        var left = Math.Clamp((int)MathF.Floor(requestedSource.Position.X), 0, imageWidth - 1);
        var top = Math.Clamp((int)MathF.Floor(requestedSource.Position.Y), 0, imageHeight - 1);
        var right = Math.Clamp((int)MathF.Ceiling(requestedSource.Position.X + requestedSource.Size.X), left + 1, imageWidth);
        var bottom = Math.Clamp((int)MathF.Ceiling(requestedSource.Position.Y + requestedSource.Size.Y), top + 1, imageHeight);

        var minX = right;
        var minY = bottom;
        var maxX = left - 1;
        var maxY = top - 1;
        var opaquePixels = 0;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                if (image.GetPixel(x, y).A <= FragmentAlphaThreshold)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                opaquePixels++;
            }
        }

        if (opaquePixels < 18 || maxX < minX || maxY < minY)
        {
            return false;
        }

        const int padding = 3;
        minX = Math.Max(left, minX - padding);
        minY = Math.Max(top, minY - padding);
        maxX = Math.Min(right - 1, maxX + padding);
        maxY = Math.Min(bottom - 1, maxY + padding);

        var size = new Vector2(maxX - minX + 1f, maxY - minY + 1f);
        if (size.X <= 3f || size.Y <= 3f)
        {
            return false;
        }

        trimmed = new Rect2(new Vector2(minX, minY), size);
        return true;
    }

    private Texture2D? DebrisTexture(int index)
    {
        if (_shipDebris.Length == 0)
        {
            return null;
        }

        var wrapped = ((index % _shipDebris.Length) + _shipDebris.Length) % _shipDebris.Length;
        return _shipDebris[wrapped];
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

    private void DrawTextureRegion(Texture2D texture, Rect2 source, Vector2 center, float rotation, Vector2 size, Color color)
    {
        if (size.X <= 0f || size.Y <= 0f || source.Size.X <= 0f || source.Size.Y <= 0f || color.A <= 0f)
        {
            return;
        }

        DrawSetTransform(center, rotation, Vector2.One);
        DrawTextureRectRegion(texture, new Rect2(-size * 0.5f, size), source, color);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private void DrawSheetFrame(Texture2D? texture, int frameIndex, int columns, int rows, Vector2 center, float rotation, Vector2 size, Color color)
    {
        if (texture is null || columns <= 0 || rows <= 0 || size.X <= 0f || size.Y <= 0f || color.A <= 0f)
        {
            return;
        }

        var frameWidth = texture.GetWidth() / (float)columns;
        var frameHeight = texture.GetHeight() / (float)rows;
        var wrapped = ((frameIndex % (columns * rows)) + columns * rows) % (columns * rows);
        var column = wrapped % columns;
        var row = wrapped / columns;
        var source = new Rect2(new Vector2(column * frameWidth, row * frameHeight), new Vector2(frameWidth, frameHeight));

        DrawTextureRegion(texture, source, center, rotation, size, color);
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

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        var t = Math.Clamp((value - edge0) / Math.Max(0.0001f, edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Hash01(float value)
    {
        var hashed = MathF.Sin(value) * 43758.5453f;
        return hashed - MathF.Floor(hashed);
    }

    private sealed class Explosion
    {
        public Explosion(
            Vector2 position,
            float radius,
            Color tint,
            ShipExplosionKind kind,
            int seed,
            float lifetime,
            Texture2D? shipTexture,
            float shipScale,
            float shipRotation,
            Rect2 shipContentBounds,
            IReadOnlyList<EnginePort> enginePorts)
        {
            Position = position;
            Radius = radius;
            Tint = tint;
            Kind = kind;
            Seed = seed;
            Lifetime = lifetime;
            ShipTexture = shipTexture;
            ShipScale = shipScale;
            ShipRotation = shipRotation;
            ShipContentBounds = shipContentBounds;
            EnginePorts = enginePorts;
        }

        public Vector2 Position { get; }
        public float Radius { get; }
        public Color Tint { get; }
        public ShipExplosionKind Kind { get; }
        public int Seed { get; }
        public float Lifetime { get; }
        public Texture2D? ShipTexture { get; }
        public float ShipScale { get; }
        public float ShipRotation { get; }
        public Rect2 ShipContentBounds { get; }
        public IReadOnlyList<EnginePort> EnginePorts { get; }
        public ShipFragment[] ShipFragments { get; set; } = [];
        public float Age { get; set; }
    }

    private readonly record struct FragmentRecipe(
        Rect2 NormalizedSource,
        Vector2 LocalDirection,
        float Speed,
        float SpeedJitter,
        float Spin,
        float Delay,
        float Heat,
        float Scale);

    private readonly record struct ShipFragment(
        Rect2 Source,
        Vector2 LocalOffset,
        Vector2 Direction,
        float Speed,
        float Spin,
        float Delay,
        float Heat,
        float Scale,
        float BaseRotation);
}
