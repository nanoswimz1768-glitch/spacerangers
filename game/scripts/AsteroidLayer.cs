using Godot;
using SpaceManagers.Core;
using File = System.IO.File;

namespace SpaceManagersPrototype;

public partial class AsteroidLayer : Node2D
{
    private const int AsteroidTextureCount = 32;
    private readonly Texture2D?[] _textures = new Texture2D?[AsteroidTextureCount];

    public IReadOnlyList<AsteroidState> Asteroids { get; set; } = Array.Empty<AsteroidState>();
    public bool UseCulling { get; set; }
    public Rect2 VisibleWorldRect { get; set; }

    public override void _Ready()
    {
        ZIndex = 10;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        for (var index = 0; index < _textures.Length; index++)
        {
            _textures[index] = LoadTexture(AsteroidTexturePath(index));
        }
    }

    public override void _ExitTree()
    {
        for (var index = 0; index < _textures.Length; index++)
        {
            _textures[index] = null;
        }
    }

    public override void _Draw()
    {
        foreach (var asteroid in Asteroids)
        {
            DrawAsteroid(asteroid);
        }
    }

    private void DrawAsteroid(AsteroidState asteroid)
    {
        var position = asteroid.Position.ToGodot();
        var radius = asteroid.Radius;
        if (UseCulling && !VisibleWorldRect.Grow(radius * 3f).HasPoint(position))
        {
            return;
        }

        var heat = Math.Clamp(asteroid.Heat, 0f, 1f);
        var texture = TextureFor(asteroid.Variant);
        if (texture is null)
        {
            DrawFallbackAsteroid(asteroid, position, radius, heat);
            return;
        }

        var drawSize = new Vector2(radius * 2.42f, radius * 2.42f);
        var rect = new Rect2(-drawSize * 0.5f, drawSize);
        var heatModulate = new Color(
            1f,
            1f - heat * 0.22f,
            1f - heat * 0.58f,
            1f);

        DrawSetTransform(position, asteroid.Rotation, Vector2.One);
        DrawTextureRect(texture, rect, false, heatModulate);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private Texture2D? TextureFor(int variant)
    {
        if (_textures.Length == 0)
        {
            return null;
        }

        var index = Math.Abs(variant) % _textures.Length;
        return _textures[index];
    }

    private static string AsteroidTexturePath(int index)
    {
        return $"res://assets/asteroids/asteroid_{index:00}.png";
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

    private void DrawHeatTrail(AsteroidState asteroid, Vector2 position, float radius, float heat)
    {
        var velocity = asteroid.Velocity.ToGodot();
        if (velocity.LengthSquared() < 1f)
        {
            return;
        }

        var backward = -velocity.Normalized();
        var tangent = new Vector2(-backward.Y, backward.X);
        var seed = asteroid.Seed * 0.0001f;
        var length = radius * (4.6f + heat * 8.2f);
        var smokeCount = 5 + (int)MathF.Round(heat * 4f);
        for (var i = 0; i < smokeCount; i++)
        {
            var h0 = Hash01(seed + i * 6.71f);
            var h1 = Hash01(seed + i * 11.93f);
            var offset = tangent * ((h0 - 0.5f) * radius * (1.15f + heat * 0.45f));
            var start = position + offset + backward * radius * (0.28f + h1 * 0.24f);
            var end = position + offset + backward * length * (0.54f + h0 * 0.62f);
            var width = radius * (0.045f + h1 * 0.048f);
            DrawLine(start, end, new Color(0.16f, 0.13f, 0.11f, heat * (0.035f + h0 * 0.035f)), Math.Clamp(width, 1.4f, 8.5f), true);
        }

        var flameCount = 13 + (int)MathF.Round(heat * 7f);
        for (var i = 0; i < flameCount; i++)
        {
            var h0 = Hash01(seed + i * 4.17f);
            var h1 = Hash01(seed + i * 9.31f);
            var h2 = Hash01(seed + i * 15.73f);
            var offset = tangent * ((h0 - 0.5f) * radius * (0.88f + heat * 0.3f));
            var start = position + offset - backward * radius * (0.08f + h1 * 0.16f);
            var end = position + offset + backward * length * (0.28f + h0 * 0.88f);
            var alpha = heat * (0.05f + h1 * 0.16f) * (1f - h2 * 0.24f);
            var width = Math.Clamp(radius * (0.018f + h2 * 0.042f), 1.2f, 8.2f);
            var color = h1 > 0.68f
                ? new Color(1f, 0.75f, 0.20f, alpha * 0.78f)
                : new Color(1f, 0.28f + h1 * 0.26f, 0.035f, alpha);
            DrawLine(start, end, color, width, true);
        }

        var emberCount = 8 + (int)MathF.Round(heat * 10f);
        for (var i = 0; i < emberCount; i++)
        {
            var h0 = Hash01(seed + i * 18.11f);
            var h1 = Hash01(seed + i * 23.47f);
            var h2 = Hash01(seed + i * 31.09f);
            var offset = tangent * ((h1 - 0.5f) * radius * (1.35f + h2 * 0.8f));
            var drift = backward * length * (0.18f + h0 * 0.82f);
            var emberPosition = position + drift + offset;
            var emberRadius = Math.Clamp(radius * (0.012f + h2 * 0.022f), 1f, 4.6f);
            DrawCircle(emberPosition, emberRadius, new Color(1f, 0.48f + h2 * 0.32f, 0.08f, heat * (0.16f + h0 * 0.22f)));
        }
    }

    private void DrawHeatedSurface(AsteroidState asteroid, float radius, float heat)
    {
        var velocity = asteroid.Velocity.ToGodot();
        var forward = velocity.LengthSquared() > 1f ? velocity.Normalized().Rotated(-asteroid.Rotation) : Vector2.Right;
        var tangent = new Vector2(-forward.Y, forward.X);
        var seed = asteroid.Seed * 0.00013f;

        DrawCircle(forward * radius * 0.18f, radius * (0.52f + heat * 0.18f), new Color(1f, 0.24f, 0.03f, 0.075f * heat));
        for (var i = 0; i < 7; i++)
        {
            var h0 = Hash01(seed + i * 5.37f);
            var h1 = Hash01(seed + i * 12.91f);
            var along = radius * (-0.48f + h0 * 0.95f);
            var edge = forward * radius * (0.48f + h1 * 0.36f) + tangent * along;
            var inner = edge - forward * radius * (0.28f + h0 * 0.22f);
            var alpha = heat * (0.12f + h1 * 0.18f);
            var width = Math.Clamp(radius * (0.012f + h0 * 0.016f), 1f, 3.2f);
            DrawLine(inner, edge, new Color(1f, 0.58f + h0 * 0.2f, 0.11f, alpha), width, true);
        }
    }

    private void DrawFallbackAsteroid(AsteroidState asteroid, Vector2 position, float radius, float heat)
    {
        var body = new Color(0.42f, 0.40f, 0.38f, 1f).Lerp(new Color(1f, 0.42f, 0.12f, 1f), heat * 0.45f);
        DrawCircle(position, radius, body);
        DrawArc(position, radius * 0.96f, 0f, MathF.Tau, 40, new Color(0.82f, 0.8f, 0.76f, 0.55f), 1.2f, true);
        DrawLine(position + new Vector2(-radius * 0.35f, -radius * 0.20f), position + new Vector2(radius * 0.28f, radius * 0.18f), new Color(0.18f, 0.17f, 0.16f, 0.38f), 2f, true);
        DrawCircle(position + new Vector2(radius * 0.22f, -radius * 0.18f), radius * 0.18f, new Color(0.12f, 0.11f, 0.1f, 0.34f));
    }

    private static float Hash01(float value)
    {
        var hashed = MathF.Sin(value) * 43758.5453f;
        return hashed - MathF.Floor(hashed);
    }
}
