using Godot;
using SpaceRangers.Core;

namespace SpaceRangersPrototype;

public partial class AsteroidFireLayer : Node2D
{
    private const float BurnBurstLifetime = 2.1f;
    private const int MaxBurnBursts = 18;
    private static readonly string[] FlameLobePaths =
    [
        "res://assets/effects/asteroid_flame_lobe_00.png",
        "res://assets/effects/asteroid_flame_lobe_01.png",
        "res://assets/effects/asteroid_flame_lobe_02.png",
        "res://assets/effects/asteroid_flame_lobe_03.png"
    ];

    private readonly List<BurnBurst> _burnBursts = new();

    private readonly Texture2D?[] _flameLobes = new Texture2D?[FlameLobePaths.Length];
    private Texture2D? _heatCorona;
    private Texture2D? _fireGlow;
    private Texture2D? _firePlume;
    private Texture2D? _smokePuff;
    private Texture2D? _spark;
    private float _timeSeconds;

    public IReadOnlyList<AsteroidState> Asteroids { get; set; } = Array.Empty<AsteroidState>();
    public bool UseCulling { get; set; }
    public Rect2 VisibleWorldRect { get; set; }

    public override void _Ready()
    {
        ZIndex = 24;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        Material = new CanvasItemMaterial
        {
            BlendMode = CanvasItemMaterial.BlendModeEnum.Add
        };

        for (var index = 0; index < FlameLobePaths.Length; index++)
        {
            _flameLobes[index] = ResourceLoader.Load<Texture2D>(FlameLobePaths[index]);
        }

        _heatCorona = ResourceLoader.Load<Texture2D>("res://assets/effects/asteroid_heat_corona.png");
        _fireGlow = ResourceLoader.Load<Texture2D>("res://assets/effects/asteroid_fire_glow.png");
        _firePlume = ResourceLoader.Load<Texture2D>("res://assets/effects/asteroid_fire_plume.png");
        _smokePuff = ResourceLoader.Load<Texture2D>("res://assets/effects/asteroid_smoke_puff.png");
        _spark = ResourceLoader.Load<Texture2D>("res://assets/effects/asteroid_fire_spark.png");
    }

    public override void _ExitTree()
    {
        ClearEffects();
        for (var index = 0; index < _flameLobes.Length; index++)
        {
            _flameLobes[index] = null;
        }

        _heatCorona = null;
        _fireGlow = null;
        _firePlume = null;
        _smokePuff = null;
        _spark = null;
        Material = null;
    }

    public void ClearEffects()
    {
        _burnBursts.Clear();
        Asteroids = Array.Empty<AsteroidState>();
        QueueRedraw();
    }

    public void SpawnBurnBurst(AsteroidEventState asteroidEvent)
    {
        if (asteroidEvent.Type != AsteroidEventType.SunBurn)
        {
            return;
        }

        while (_burnBursts.Count >= MaxBurnBursts)
        {
            _burnBursts.RemoveAt(0);
        }

        _burnBursts.Add(new BurnBurst(
            asteroidEvent.Position.ToGodot(),
            Math.Clamp(asteroidEvent.Radius, 18f, 168f),
            asteroidEvent.Seed,
            Math.Max(asteroidEvent.Heat, 0.88f)));
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _timeSeconds += (float)delta;

        for (var index = _burnBursts.Count - 1; index >= 0; index--)
        {
            var burst = _burnBursts[index] with { Age = _burnBursts[index].Age + (float)delta };
            if (burst.Age >= BurnBurstLifetime)
            {
                _burnBursts.RemoveAt(index);
                continue;
            }

            _burnBursts[index] = burst;
        }

        if (Asteroids.Count > 0 || _burnBursts.Count > 0)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        foreach (var asteroid in Asteroids)
        {
            DrawAsteroidFire(asteroid);
        }

        foreach (var burst in _burnBursts)
        {
            DrawBurnBurst(burst);
        }
    }

    private void DrawAsteroidFire(AsteroidState asteroid)
    {
        var position = asteroid.Position.ToGodot();
        var radius = asteroid.Radius;
        if (UseCulling && !VisibleWorldRect.Grow(radius * 18f).HasPoint(position))
        {
            return;
        }

        var heat = Math.Clamp(asteroid.Heat, 0f, 1f);
        var visualHeat = MathF.Pow(SmoothStep(0.055f, 0.92f, heat), 0.55f);
        if (visualHeat <= 0.008f)
        {
            return;
        }

        var velocity = asteroid.Velocity.ToGodot();
        var speed = velocity.Length();
        var fallbackBackward = position.LengthSquared() > 1f ? position.Normalized() : Vector2.Right;
        var backward = speed > 1f ? -velocity / speed : fallbackBackward;
        var tangent = new Vector2(-backward.Y, backward.X);
        var seed = asteroid.Seed * 0.0031f;
        var pulse = 0.88f + MathF.Sin(_timeSeconds * 8.2f + seed * 12.7f) * 0.12f;

        DrawFireCorona(asteroid, position, radius, visualHeat, seed, pulse);

        if (speed < 1f)
        {
            return;
        }

        var speedScale = Math.Clamp(speed / 900f, 0.62f, 1.28f);
        var angle = backward.Angle();
        var length = radius * (0.82f + visualHeat * 1.85f) * speedScale;
        length = Math.Min(length, radius * 2.75f);

        DrawCometSheath(position, backward, tangent, angle, length, radius, visualHeat, seed);
        DrawAblationDust(position, backward, tangent, length, radius, visualHeat, seed);
        DrawSparks(position, backward, tangent, length, radius, visualHeat, seed, speedScale);
    }

    private void DrawFireCorona(AsteroidState asteroid, Vector2 position, float radius, float heat, float seed, float pulse)
    {
        DrawTexturedQuad(_heatCorona, position, 0f, Vector2.One * radius * (1.75f + heat * 1.15f), new Color(1f, 0.20f, 0.035f, heat * 0.42f * pulse));
        DrawTexturedQuad(_fireGlow, position, 0f, Vector2.One * radius * (0.92f + heat * 0.72f), new Color(1f, 0.78f, 0.24f, heat * 0.22f));

        var sunward = position.LengthSquared() > 1f ? -position.Normalized() : Vector2.Up;
        var count = 3 + (int)MathF.Round(heat * 2f);
        for (var i = 0; i < count; i++)
        {
            var h0 = Hash01(seed + i * 3.91f);
            var h1 = Hash01(seed + i * 8.43f);
            var h2 = Hash01(seed + i * 15.17f);
            var baseAngle = sunward.Angle() + (h0 - 0.5f) * (1.6f + heat * 0.35f) + MathF.Sin(_timeSeconds * (1.4f + h1 * 1.8f) + h2 * 8.0f) * 0.10f;
            var direction = new Vector2(MathF.Cos(baseAngle), MathF.Sin(baseAngle));
            var sunBias = Math.Clamp((direction.Dot(sunward) + 1f) * 0.5f, 0f, 1f);
            var length = radius * (0.24f + h1 * 0.34f + heat * (0.28f + sunBias * 0.22f));
            var width = radius * (0.28f + h2 * 0.34f) * (0.86f + heat * 0.18f);
            var start = position + direction * radius * (0.62f + h2 * 0.18f);
            var center = start + direction * length * 0.5f;
            var alpha = heat * (0.06f + h1 * 0.09f + sunBias * 0.08f) * pulse;
            var color = h2 > 0.62f
                ? new Color(1f, 0.72f, 0.17f, alpha)
                : new Color(1f, 0.22f, 0.025f, alpha);
            DrawTexturedQuad(FlameLobe(i), center, direction.Angle(), new Vector2(length, width), color);
        }
    }

    private void DrawCometSheath(Vector2 position, Vector2 backward, Vector2 tangent, float angle, float length, float radius, float heat, float seed)
    {
        DrawTexturedQuad(_fireGlow, position + backward * radius * 0.16f, 0f, Vector2.One * radius * (1.72f + heat * 1.35f), new Color(1f, 0.18f, 0.025f, heat * 0.28f));

        for (var i = 0; i < 4; i++)
        {
            var h0 = Hash01(seed + i * 5.83f);
            var h1 = Hash01(seed + i * 13.19f);
            var h2 = Hash01(seed + i * 21.41f);
            var phase = (i + 0.35f) / 4f;
            var localLength = length * (0.56f + h0 * 0.36f) * (1f - phase * 0.08f);
            var localWidth = radius * (0.44f + h1 * 0.42f) * (1.0f - phase * 0.18f);
            var center = position + backward * localLength * (0.42f + phase * 0.12f) + tangent * radius * (h1 - 0.5f) * (0.34f + phase * 0.22f);
            var localAngle = angle + (h1 - 0.5f) * 0.24f;
            var alpha = heat * (0.18f + h2 * 0.18f) * (1f - phase * 0.26f);
            var color = h0 > 0.62f
                ? new Color(1f, 0.62f, 0.12f, alpha * 0.74f)
                : new Color(1f, 0.22f + h2 * 0.16f, 0.028f, alpha);
            DrawTexturedQuad(_firePlume, center, localAngle, new Vector2(localLength, localWidth), color);
        }
    }

    private void DrawAblationDust(Vector2 position, Vector2 backward, Vector2 tangent, float length, float radius, float heat, float seed)
    {
        var count = 5 + (int)MathF.Round(heat * 5f);
        for (var i = 0; i < count; i++)
        {
            var h0 = Hash01(seed + i * 7.77f);
            var h1 = Hash01(seed + i * 19.31f);
            var h2 = Hash01(seed + i * 29.09f);
            var phase = Fract(h0 + _timeSeconds * (0.12f + h2 * 0.12f));
            var tail = length * (0.25f + phase * 0.72f);
            var spread = radius * (0.42f + phase * 1.2f) * (h1 - 0.5f);
            var center = position + backward * tail + tangent * spread;
            var size = radius * (0.38f + h2 * 0.54f) * (0.75f + phase * 0.62f);
            var alpha = heat * (1f - phase * 0.58f) * (0.035f + h0 * 0.055f);
            DrawTexturedQuad(_smokePuff, center, h0 * MathF.Tau + _timeSeconds * (h1 - 0.5f) * 0.35f, Vector2.One * size, new Color(0.22f, 0.15f, 0.10f, alpha));
        }
    }

    private void DrawSparks(Vector2 position, Vector2 backward, Vector2 tangent, float length, float radius, float heat, float seed, float speedScale)
    {
        var count = 8 + (int)MathF.Round(heat * 10f);
        var flowSpeed = 0.42f + speedScale * 0.34f;
        for (var i = 0; i < count; i++)
        {
            var h0 = Hash01(seed + i * 17.17f);
            var h1 = Hash01(seed + i * 23.61f);
            var h2 = Hash01(seed + i * 41.09f);
            var phase = Fract(h0 + _timeSeconds * (flowSpeed + h2 * 0.42f));
            var tail = length * phase * (0.16f + h1 * 0.86f);
            var spread = radius * (0.34f + phase * 2.55f) * (h1 - 0.5f);
            var jitter = MathF.Sin(_timeSeconds * (5.2f + h2 * 4.1f) + h0 * 9.4f) * radius * 0.18f;
            var sparkPosition = position + backward * tail + tangent * (spread + jitter);
            var size = Math.Clamp(radius * (0.022f + h2 * 0.034f) * (1f - phase * 0.55f), 1.6f, 6.5f);
            var alpha = heat * (1f - phase * 0.68f) * (0.24f + h0 * 0.28f);
            var color = h2 > 0.72f
                ? new Color(1f, 0.92f, 0.38f, alpha)
                : new Color(1f, 0.42f + h2 * 0.28f, 0.06f, alpha);
            DrawTexturedQuad(_spark, sparkPosition, 0f, Vector2.One * size, color);
        }
    }

    private void DrawBurnBurst(BurnBurst burst)
    {
        var t = Math.Clamp(burst.Age / BurnBurstLifetime, 0f, 1f);
        var fade = MathF.Pow(1f - t, 0.82f);
        var expansion = SmoothStep(0f, 0.72f, t);
        var flash = 1f - SmoothStep(0.0f, 0.18f, t);
        var radius = burst.Radius;
        var seed = burst.Seed * 0.0027f;

        DrawTexturedQuad(_heatCorona, burst.Position, 0f, Vector2.One * radius * (2.0f + expansion * 1.65f), new Color(1f, 0.18f, 0.025f, fade * 0.42f));
        DrawTexturedQuad(_fireGlow, burst.Position, 0f, Vector2.One * radius * (1.06f + expansion * 1.25f), new Color(1f, 0.84f, 0.25f, (flash * 0.62f + fade * 0.14f) * burst.Heat));

        var plumeCount = 5;
        for (var i = 0; i < plumeCount; i++)
        {
            var h0 = Hash01(seed + i * 4.23f);
            var h1 = Hash01(seed + i * 9.91f);
            var h2 = Hash01(seed + i * 17.47f);
            var angle = h0 * MathF.Tau;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var length = radius * (0.42f + h1 * 0.88f) * (0.18f + expansion * 0.74f);
            var width = radius * (0.34f + h2 * 0.52f) * (1f - t * 0.22f);
            var center = burst.Position + direction * length * 0.48f;
            var alpha = fade * (0.12f + h1 * 0.18f) * burst.Heat;
            var color = h2 > 0.52f
                ? new Color(1f, 0.64f, 0.14f, alpha)
                : new Color(1f, 0.22f, 0.025f, alpha);
            DrawTexturedQuad(_firePlume, center, direction.Angle(), new Vector2(length, width), color);
        }

        var sparkCount = 32;
        for (var i = 0; i < sparkCount; i++)
        {
            var h0 = Hash01(seed + i * 21.13f);
            var h1 = Hash01(seed + i * 31.71f);
            var h2 = Hash01(seed + i * 48.29f);
            var angle = h0 * MathF.Tau;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var travel = radius * (0.35f + h1 * 5.8f) * expansion;
            var position = burst.Position + direction * travel;
            var size = Math.Clamp(radius * (0.02f + h2 * 0.044f) * (1f - t * 0.48f), 1.8f, 8f);
            var alpha = fade * burst.Heat * (0.18f + h1 * 0.34f);
            DrawTexturedQuad(_spark, position, 0f, Vector2.One * size, new Color(1f, 0.48f + h2 * 0.38f, 0.08f, alpha));
        }
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

    private Texture2D? FlameLobe(int index)
    {
        if (_flameLobes.Length == 0)
        {
            return null;
        }

        var wrapped = ((index % _flameLobes.Length) + _flameLobes.Length) % _flameLobes.Length;
        return _flameLobes[wrapped];
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Fract(float value)
    {
        return value - MathF.Floor(value);
    }

    private static float Hash01(float value)
    {
        var hashed = MathF.Sin(value) * 43758.5453f;
        return hashed - MathF.Floor(hashed);
    }

    private readonly record struct BurnBurst(Vector2 Position, float Radius, int Seed, float Heat)
    {
        public float Age { get; init; }
    }
}
