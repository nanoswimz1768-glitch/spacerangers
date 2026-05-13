using Godot;
using System.Globalization;
using System.Text.Json;

namespace SpaceManagersPrototype;

public static class StarSystemLoader
{
    public const string DefaultGalaxyPath = "res://assets/generated/galaxy.json";
    private static readonly Dictionary<string, IReadOnlyList<StarSystemIndexEntry>> GalaxyIndexCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, StarSystemDefinition?> SystemCache = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<StarSystemIndexEntry> LoadGalaxyIndex(string path = DefaultGalaxyPath)
    {
        if (GalaxyIndexCache.TryGetValue(path, out var cachedIndex))
        {
            return cachedIndex;
        }

        var json = ReadAllText(path);
        if (json is null)
        {
            return Array.Empty<StarSystemIndexEntry>();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var entries = new List<StarSystemIndexEntry>();
            if (document.RootElement.TryGetProperty("sectors", out var sectors) && sectors.ValueKind == JsonValueKind.Array)
            {
                foreach (var sector in sectors.EnumerateArray())
                {
                    var sectorId = GetString(sector, "id", string.Empty);
                    var sectorName = GetString(sector, "name", sectorId);
                    if (!sector.TryGetProperty("systems", out var systems) || systems.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var system in systems.EnumerateArray())
                    {
                        entries.Add(ParseIndexEntry(system, sectorId, sectorName));
                    }
                }

                var result = entries.Where(entry => !string.IsNullOrWhiteSpace(entry.File)).ToArray();
                GalaxyIndexCache[path] = result;
                return result;
            }

            if (document.RootElement.TryGetProperty("systems", out var legacySystems) && legacySystems.ValueKind == JsonValueKind.Array)
            {
                foreach (var system in legacySystems.EnumerateArray())
                {
                    entries.Add(ParseIndexEntry(system, string.Empty, string.Empty));
                }

                var result = entries.Where(entry => !string.IsNullOrWhiteSpace(entry.File)).ToArray();
                GalaxyIndexCache[path] = result;
                return result;
            }

            GalaxyIndexCache[path] = Array.Empty<StarSystemIndexEntry>();
            return GalaxyIndexCache[path];
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Failed to parse generated galaxy index '{path}': {exception.Message}");
            GalaxyIndexCache[path] = Array.Empty<StarSystemIndexEntry>();
            return Array.Empty<StarSystemIndexEntry>();
        }
    }

    public static StarSystemDefinition? LoadSystem(string path)
    {
        if (SystemCache.TryGetValue(path, out var cachedSystem))
        {
            return cachedSystem;
        }

        var json = ReadAllText(path);
        if (json is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var star = ParseStar(root.GetProperty("star"));
            var background = ParseBackground(root.GetProperty("background"));
            var planets = ParsePlanets(root.GetProperty("planets"));

            var system = new StarSystemDefinition(
                GetString(root, "id", PathId(path)),
                GetString(root, "name", PathId(path)),
                GetInt(root, "seed", 0),
                star,
                background,
                planets,
                GetString(root, "sectorId", string.Empty),
                GetString(root, "sectorName", string.Empty));
            SystemCache[path] = system;
            return system;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Failed to parse generated star system '{path}': {exception.Message}");
            SystemCache[path] = null;
            return null;
        }
    }

    private static StarSystemIndexEntry ParseIndexEntry(JsonElement system, string fallbackSectorId, string fallbackSectorName)
    {
        var sectorId = GetString(system, "sectorId", fallbackSectorId);
        var sectorName = GetString(system, "sectorName", fallbackSectorName);
        return new StarSystemIndexEntry(
            GetString(system, "id", string.Empty),
            GetString(system, "name", "Unknown"),
            GetString(system, "file", string.Empty),
            GetString(system, "starArchetype", string.Empty),
            GetString(system, "backgroundArchetype", string.Empty),
            GetInt(system, "planetCount", 0),
            sectorId,
            sectorName,
            GetString(system, "source", "generated"),
            GetParsecPosition(system));
    }

    private static Vector2 GetParsecPosition(JsonElement element)
    {
        return new Vector2(
            GetSingle(element, "parsecX", 0f),
            GetSingle(element, "parsecY", 0f));
    }

    private static string? ReadAllText(string path)
    {
        if (!Godot.FileAccess.FileExists(path))
        {
            GD.PushWarning($"Generated star-system file is missing: {path}");
            return null;
        }

        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (file is null)
        {
            GD.PushWarning($"Generated star-system file could not be opened: {path}");
            return null;
        }

        return file.GetAsText();
    }

    private static StarDefinition ParseStar(JsonElement star)
    {
        return new StarDefinition(
            GetString(star, "archetype", "unknown_star"),
            GetString(star, "displayName", "Unknown Star"),
            GetString(star, "textureSet", "solar_animated_default"),
            GetSingle(star, "worldSize", SolarSystem.SunVisualWorldSize),
            GetColor(star, "diskTint", Colors.White),
            GetColor(star, "coronaColor", new Color(1f, 0.34f, 0.02f, 1f)),
            GetSingle(star, "coronaIntensity", 1f),
            GetSingle(star, "animationSpeed", 1f),
            GetColor(star, "mapColor", new Color(1f, 0.66f, 0.08f, 1f)),
            GetString(star, "texturePath", string.Empty),
            GetString(star, "frameDirectory", string.Empty),
            GetString(star, "framePrefix", "sun_"));
    }

    private static SpaceBackdropDefinition ParseBackground(JsonElement background)
    {
        return new SpaceBackdropDefinition(
            GetString(background, "archetype", "unknown_backdrop"),
            GetString(background, "displayName", "Unknown Backdrop"),
            GetString(background, "texturePath", "res://assets/backgrounds/space_nebula_tile.png"),
            GetColor(background, "textureTint", new Color(0.56f, 0.70f, 0.88f, 1f)),
            GetSingle(background, "textureAlpha", 1.0f),
            GetSingle(background, "textureParallax", 0.08f),
            GetSingle(background, "starParallax", 0.32f),
            GetInt(background, "starfieldSeed", 3311337),
            GetInt(background, "starCount", 900),
            GetInt(background, "nebulaSeed", 3311338),
            GetInt(background, "nebulaBlobCount", 28),
            GetSingle(background, "dustDensity", 0.24f),
            ParsePalette(background));
    }

    private static IReadOnlyList<PlanetDefinition> ParsePlanets(JsonElement planets)
    {
        if (planets.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<PlanetDefinition>();
        }

        var result = new List<PlanetDefinition>();
        foreach (var planet in planets.EnumerateArray())
        {
            var visual = ParsePlanetVisual(planet, GetString(planet, "surfaceMap", "res://assets/planets/mercury_spin_map.png"));
            result.Add(new PlanetDefinition(
                GetString(planet, "id", $"planet_{result.Count + 1}"),
                GetString(planet, "name", $"Planet {result.Count + 1}"),
                GetString(planet, "surfaceMap", "res://assets/planets/mercury_spin_map.png"),
                GetSingle(planet, "orbitRadius", 1250f),
                GetSingle(planet, "bodyRadius", 75f),
                GetSingle(planet, "referenceTextureWorldSize", 150f),
                GetSingle(planet, "textureWorldSize", 150f),
                GetSingle(planet, "orbitPeriodSeconds", 100f),
                GetSingle(planet, "rotationPeriodSeconds", 8f),
                GetSingle(planet, "initialAngle", 0f),
                GetColor(planet, "mapColor", new Color(0.72f, 0.9f, 1f, 1f)),
                visual));
        }

        return result;
    }

    private static PlanetVisualProfile? ParsePlanetVisual(JsonElement planet, string fallbackSurfacePath)
    {
        if (!planet.TryGetProperty("visual", out var visual) || visual.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new PlanetVisualProfile(
            fallbackSurfacePath,
            GetColor(visual, "atmosphereColor", new Color(0.35f, 0.7f, 1f, 1f)),
            GetSingle(visual, "atmosphereStrength", 0.18f),
            GetSingle(visual, "rotationSpeed", 0.012f),
            GetSingle(visual, "flowStrength", 0.006f),
            GetSingle(visual, "contrast", 1f),
            GetSingle(visual, "saturation", 1f),
            GetSingle(visual, "glowStrength", 1f),
            ParseRing(visual));
    }

    private static PlanetRingProfile? ParseRing(JsonElement visual)
    {
        if (!visual.TryGetProperty("rings", out var rings) || rings.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new PlanetRingProfile(
            GetSingle(rings, "innerRadiusFactor", 0.55f),
            GetSingle(rings, "outerRadiusFactor", 0.96f),
            GetSingle(rings, "flattening", 0.3f),
            GetSingle(rings, "rotation", -0.24f),
            GetSingle(rings, "alpha", 0.8f),
            GetColor(rings, "color", new Color(0.76f, 0.67f, 0.52f, 1f)),
            GetColor(rings, "accentColor", new Color(1f, 0.92f, 0.72f, 1f)));
    }

    private static IReadOnlyList<Color> ParsePalette(JsonElement background)
    {
        if (!background.TryGetProperty("nebulaPalette", out var palette) || palette.ValueKind != JsonValueKind.Array)
        {
            return SolarSystem.Sol.Background.NebulaPalette;
        }

        var colors = new List<Color>();
        foreach (var color in palette.EnumerateArray())
        {
            if (color.ValueKind == JsonValueKind.String)
            {
                colors.Add(ParseColor(color.GetString(), new Color(0.0f, 0.48f, 0.58f, 1f)));
            }
        }

        return colors.Count > 0 ? colors : SolarSystem.Sol.Background.NebulaPalette;
    }

    private static string GetString(JsonElement element, string name, string fallback)
    {
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? fallback
            : fallback;
    }

    private static int GetInt(JsonElement element, string name, int fallback)
    {
        return element.TryGetProperty(name, out var property) && property.TryGetInt32(out var value)
            ? value
            : fallback;
    }

    private static float GetSingle(JsonElement element, string name, float fallback)
    {
        return element.TryGetProperty(name, out var property) && property.TryGetSingle(out var value)
            ? value
            : fallback;
    }

    private static Color GetColor(JsonElement element, string name, Color fallback)
    {
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? ParseColor(property.GetString(), fallback)
            : fallback;
    }

    private static Color ParseColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }

        var value = hex.Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        if (value.Length != 6 || !int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return fallback;
        }

        return new Color(
            ((rgb >> 16) & 0xff) / 255f,
            ((rgb >> 8) & 0xff) / 255f,
            (rgb & 0xff) / 255f,
            1f);
    }

    private static string PathId(string path)
    {
        var fileName = path.Split('/', '\\').LastOrDefault() ?? "unknown_system";
        return fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^5]
            : fileName;
    }
}

public sealed record StarSystemIndexEntry(
    string Id,
    string DisplayName,
    string File,
    string StarArchetype,
    string BackgroundArchetype,
    int PlanetCount,
    string SectorId = "",
    string SectorName = "",
    string Source = "generated",
    Vector2 ParsecPosition = default);
