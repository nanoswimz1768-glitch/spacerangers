using Godot;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceRangersPrototype;

public static class ShipCatalog
{
    public const float DefaultVisualScale = 0.42f;
    private const float HitboxAlphaThreshold = 0.28f;

    private static ShipManifestEntry[]? _manifestCache;

    private static readonly string[] KnownShipIds =
    {
        "2FeiD",
        "2FeiL",
        "2FeiP",
        "2FeiR",
        "2FeiT",
        "2FeiW",
        "2GaalD",
        "2GaalL",
        "2GaalP",
        "2GaalR",
        "2GaalT",
        "2GaalW",
        "2MalocD",
        "2MalocL",
        "2MalocP",
        "2MalocR",
        "2MalocT",
        "2MalocW",
        "2PelengD",
        "2PelengL",
        "2PelengP",
        "2PelengR",
        "2PelengT",
        "2PelengW",
        "2PeopleD",
        "2PeopleL",
        "2PeopleP",
        "2PeopleR",
        "2PeopleT",
        "2PeopleW",
    };

    public static IReadOnlyList<string> LoadShipTexturePaths()
    {
        var manifest = LoadManifestEntries();
        if (manifest.Length > 0)
        {
            return manifest
                .Select(entry => entry.Path)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return KnownShipIds
            .Select(id => $"res://assets/ships/{id}.png")
            .ToArray();
    }

    public static Texture2D? LoadTexture(string path)
    {
        return ResourceLoader.Load<Texture2D>(path);
    }

    public static int IndexOfPreferred(IReadOnlyList<string> paths, string id)
    {
        for (var i = 0; i < paths.Count; i++)
        {
            if (paths[i].Contains(id, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return paths.Count > 0 ? 0 : -1;
    }

    public static string DisplayName(string path)
    {
        var file = path.GetFile().GetBaseName();
        return file.StartsWith("2", StringComparison.Ordinal) ? file[1..] : file;
    }

    public static string RaceFromPath(string path)
    {
        var file = path.GetFile().GetBaseName();
        if (file.StartsWith("2", StringComparison.Ordinal))
        {
            file = file[1..];
        }

        foreach (var race in new[] { "People", "Fei", "Gaal", "Maloc", "Peleng", "Klissan" })
        {
            if (file.StartsWith(race, StringComparison.OrdinalIgnoreCase))
            {
                return race;
            }
        }

        return "Unknown";
    }

    public static Color ThrustOuterColor(string path)
    {
        return RaceFromPath(path) switch
        {
            "People" => new Color(1f, 0.78f, 0.08f, 1f),
            "Fei" => new Color(1f, 0.16f, 0.88f, 1f),
            "Gaal" => new Color(0.04f, 0.54f, 1f, 1f),
            "Maloc" => new Color(1f, 0.22f, 0.02f, 1f),
            "Peleng" => new Color(0.72f, 1f, 0.08f, 1f),
            "Klissan" => new Color(0.02f, 0.92f, 0.76f, 1f),
            _ => new Color(0.1f, 0.8f, 1f, 1f),
        };
    }

    public static Color ThrustCoreColor(string path)
    {
        return RaceFromPath(path) switch
        {
            "People" => new Color(1f, 0.98f, 0.56f, 1f),
            "Fei" => new Color(1f, 0.62f, 1f, 1f),
            "Gaal" => new Color(0.64f, 0.96f, 1f, 1f),
            "Maloc" => new Color(1f, 0.72f, 0.22f, 1f),
            "Peleng" => new Color(0.96f, 1f, 0.44f, 1f),
            "Klissan" => new Color(0.56f, 1f, 0.86f, 1f),
            _ => new Color(0.75f, 1f, 1f, 1f),
        };
    }

    public static float ThrustSizeMultiplier(string path)
    {
        return RaceFromPath(path) switch
        {
            "Peleng" => 1.18f,
            _ => 1f
        };
    }

    public static float ThrustBubbleMultiplier(string path)
    {
        return 1f;
    }

    public static float ThrustParticleDensity(string path)
    {
        return 1f;
    }

    public static IReadOnlyList<EnginePort> ExhaustPortsForPath(string path)
    {
        var entry = FindManifestEntry(path);

        if (entry?.ExhaustPorts is not { Length: > 0 })
        {
            return Array.Empty<EnginePort>();
        }

        return entry.ExhaustPorts
            .Select(port => new EnginePort(new Vector2(port.X, port.Y), Math.Clamp(port.Radius, 5f, 64f)))
            .ToArray();
    }

    public static ShipVisualProfile VisualProfileForPath(string path, Texture2D texture)
    {
        var entry = FindManifestEntry(path);
        var scale = Math.Clamp(entry?.Scale ?? DefaultVisualScale, 0.05f, 4f);
        var textureSize = new Vector2(Math.Max(1f, texture.GetWidth()), Math.Max(1f, texture.GetHeight()));
        var contentBounds = AlphaBoundsForTexture(texture) ?? ContentBoundsForEntry(entry, textureSize);
        var center = contentBounds.Position + contentBounds.Size * 0.5f - textureSize * 0.5f;

        return new ShipVisualProfile(
            scale,
            center,
            contentBounds.Size,
            contentBounds);
    }

    public static ShipRigProfile RigProfileForPath(Texture2D texture, ShipVisualProfile visualProfile, IReadOnlyList<EnginePort> exhaustPorts)
    {
        var textureSize = new Vector2(Math.Max(1f, texture.GetWidth()), Math.Max(1f, texture.GetHeight()));
        var bounds = visualProfile.ContentBounds;
        var width = Math.Max(1f, bounds.Size.X);
        var height = Math.Max(1f, bounds.Size.Y);

        var wingTop = bounds.Position.Y + height * 0.16f;
        var wingHeight = height * 0.70f;
        var leftWing = ClampTextureRect(
            new Rect2(
                new Vector2(bounds.Position.X, wingTop),
                new Vector2(width * 0.46f, wingHeight)),
            textureSize);
        var rightWing = ClampTextureRect(
            new Rect2(
                new Vector2(bounds.Position.X + width * 0.54f, wingTop),
                new Vector2(width * 0.46f, wingHeight)),
            textureSize);
        var hull = ClampTextureRect(
            new Rect2(
                new Vector2(bounds.Position.X + width * 0.30f, bounds.Position.Y + height * 0.05f),
                new Vector2(width * 0.40f, height * 0.84f)),
            textureSize);

        var center = visualProfile.HitboxLocalCenter;
        var size = visualProfile.HitboxLocalSize;
        var halfWidth = Math.Max(1f, size.X * 0.5f);
        var halfHeight = Math.Max(1f, size.Y * 0.5f);
        var core = center + new Vector2(0f, -halfHeight * 0.06f);
        var nose = center + new Vector2(0f, -halfHeight * 0.76f);
        var leftWingTip = center + new Vector2(-halfWidth * 0.82f, -halfHeight * 0.08f);
        var rightWingTip = center + new Vector2(halfWidth * 0.82f, -halfHeight * 0.08f);
        var leftWingRoot = center + new Vector2(-halfWidth * 0.24f, -halfHeight * 0.05f);
        var rightWingRoot = center + new Vector2(halfWidth * 0.24f, -halfHeight * 0.05f);
        var enginePorts = exhaustPorts.Count > 0
            ? exhaustPorts.ToArray()
            : new[]
            {
                new EnginePort(center + new Vector2(-halfWidth * 0.24f, halfHeight * 0.84f), Math.Clamp(size.X * 0.045f, 6f, 18f)),
                new EnginePort(center + new Vector2(halfWidth * 0.24f, halfHeight * 0.84f), Math.Clamp(size.X * 0.045f, 6f, 18f))
            };

        return new ShipRigProfile(
            textureSize,
            leftWing,
            rightWing,
            hull,
            CenterLocalForRegion(leftWing, textureSize),
            CenterLocalForRegion(rightWing, textureSize),
            CenterLocalForRegion(hull, textureSize),
            core,
            nose,
            leftWingRoot,
            rightWingRoot,
            leftWingTip,
            rightWingTip,
            enginePorts);
    }

    private static Rect2? AlphaBoundsForTexture(Texture2D texture)
    {
        var image = texture.GetImage();
        if (image is null || image.IsEmpty())
        {
            return null;
        }

        var width = image.GetWidth();
        var height = image.GetHeight();
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (image.GetPixel(x, y).A <= HitboxAlphaThreshold)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return null;
        }

        var position = new Vector2(minX, minY);
        var size = new Vector2(maxX - minX + 1f, maxY - minY + 1f);
        return new Rect2(position, size);
    }

    private static ShipManifestEntry? FindManifestEntry(string path)
    {
        var manifest = LoadManifestEntries();
        return manifest.FirstOrDefault(candidate =>
            string.Equals(candidate.Path, path, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Path.GetFile(), path.GetFile(), StringComparison.OrdinalIgnoreCase));
    }

    private static Rect2 ContentBoundsForEntry(ShipManifestEntry? entry, Vector2 textureSize)
    {
        if (entry?.ContentBounds is not { Length: >= 4 } bounds)
        {
            return new Rect2(Vector2.Zero, textureSize);
        }

        var left = Math.Clamp(Math.Min(bounds[0], bounds[2]), 0f, textureSize.X);
        var top = Math.Clamp(Math.Min(bounds[1], bounds[3]), 0f, textureSize.Y);
        var right = Math.Clamp(Math.Max(bounds[0], bounds[2]), 0f, textureSize.X);
        var bottom = Math.Clamp(Math.Max(bounds[1], bounds[3]), 0f, textureSize.Y);
        var width = Math.Max(1f, right - left);
        var height = Math.Max(1f, bottom - top);
        return new Rect2(new Vector2(left, top), new Vector2(width, height));
    }

    private static Rect2 ClampTextureRect(Rect2 rect, Vector2 textureSize)
    {
        var left = Math.Clamp(rect.Position.X, 0f, Math.Max(0f, textureSize.X - 1f));
        var top = Math.Clamp(rect.Position.Y, 0f, Math.Max(0f, textureSize.Y - 1f));
        var right = Math.Clamp(rect.Position.X + rect.Size.X, left + 1f, textureSize.X);
        var bottom = Math.Clamp(rect.Position.Y + rect.Size.Y, top + 1f, textureSize.Y);
        return new Rect2(new Vector2(left, top), new Vector2(right - left, bottom - top));
    }

    private static Vector2 CenterLocalForRegion(Rect2 region, Vector2 textureSize)
    {
        return region.Position + region.Size * 0.5f - textureSize * 0.5f;
    }

    private static ShipManifestEntry[] LoadManifestEntries()
    {
        if (_manifestCache is not null)
        {
            return _manifestCache;
        }

        try
        {
            var json = Godot.FileAccess.GetFileAsString("res://assets/ships/ships_manifest.json");
            _manifestCache = JsonSerializer.Deserialize<ShipManifestEntry[]>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Array.Empty<ShipManifestEntry>();
        }
        catch
        {
            // Fall back to the built-in catalog below; exported builds should never fail over here.
            _manifestCache = Array.Empty<ShipManifestEntry>();
        }

        return _manifestCache;
    }

    private sealed class ShipManifestEntry
    {
        public string Path { get; set; } = string.Empty;
        public float Scale { get; set; } = DefaultVisualScale;

        [JsonPropertyName("content_bounds")]
        public float[] ContentBounds { get; set; } = Array.Empty<float>();

        [JsonPropertyName("exhaust_ports")]
        public ExhaustPortEntry[] ExhaustPorts { get; set; } = Array.Empty<ExhaustPortEntry>();
    }

    private sealed class ExhaustPortEntry
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Radius { get; set; } = 10f;
    }
}

public sealed record ShipVisualProfile(
    float Scale,
    Vector2 HitboxLocalCenter,
    Vector2 HitboxLocalSize,
    Rect2 ContentBounds);

public sealed record ShipRigProfile(
    Vector2 TextureSize,
    Rect2 LeftWingRegion,
    Rect2 RightWingRegion,
    Rect2 HullRegion,
    Vector2 LeftWingCenter,
    Vector2 RightWingCenter,
    Vector2 HullCenter,
    Vector2 CoreAnchor,
    Vector2 NoseAnchor,
    Vector2 LeftWingRoot,
    Vector2 RightWingRoot,
    Vector2 LeftWingTip,
    Vector2 RightWingTip,
    EnginePort[] EnginePorts)
{
    public static readonly ShipRigProfile Empty = new(
        Vector2.Zero,
        new Rect2(Vector2.Zero, Vector2.Zero),
        new Rect2(Vector2.Zero, Vector2.Zero),
        new Rect2(Vector2.Zero, Vector2.Zero),
        Vector2.Zero,
        Vector2.Zero,
        Vector2.Zero,
        Vector2.Zero,
        Vector2.Zero,
        Vector2.Zero,
        Vector2.Zero,
        Vector2.Zero,
        Vector2.Zero,
        Array.Empty<EnginePort>());

    public bool HasRegions => TextureSize.X > 0f && TextureSize.Y > 0f && LeftWingRegion.Size.X > 0f && RightWingRegion.Size.X > 0f;
}
