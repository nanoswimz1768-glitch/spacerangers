using Godot;

namespace SpaceRangersPrototype;

public static class SolarSystem
{
    public const float ReferenceStarSize = 1000f;
    public const float SunVisualWorldSize = 1320f;
    public const float EarthOrbitPeriodSeconds = 100f;

    public static readonly StarSystemDefinition Sol = new(
        "sol",
        "Sol",
        0,
        new StarDefinition(
            "yellow_main_sequence",
            "Sun",
            "solar_animated_default",
            SunVisualWorldSize,
            Colors.White,
            new Color(1f, 0.34f, 0.02f, 1f),
            1f,
            1f,
            new Color(1f, 0.66f, 0.08f, 1f),
            FrameDirectory: "res://assets/backgrounds/sun"),
        new SpaceBackdropDefinition(
            "sol_nebula",
            "Solar Neighborhood",
            "res://assets/backgrounds/space_nebula_tile.png",
            new Color(0.56f, 0.70f, 0.88f, 1f),
            1.0f,
            0.08f,
            0.32f,
            3311337,
            900,
            3311338,
            28,
            0.24f,
            new[]
            {
                new Color(0.0f, 0.48f, 0.58f, 1f),
                new Color(0.42f, 0.08f, 0.52f, 1f),
                new Color(0.6f, 0.18f, 0.03f, 1f),
                new Color(0.05f, 0.42f, 0.28f, 1f)
            }),
        new[]
        {
            new PlanetDefinition("mercury", "Mercury", "res://assets/planets/mercury.png", 1250f, BodyRadius(150f), 150f, ReferenceSize(150f), OrbitPeriod(0.2408467f), 8.0f, 0.18f, new Color(0.72f, 0.66f, 0.58f, 1f)),
            new PlanetDefinition("venus", "Venus", "res://assets/planets/venus.png", 1780f, BodyRadius(280f), 280f, ReferenceSize(280f), OrbitPeriod(0.6151973f), -13.0f, 1.62f, new Color(0.95f, 0.72f, 0.38f, 1f)),
            new PlanetDefinition("earth", "Earth", "res://assets/planets/earth.png", 2360f, BodyRadius(300f), 300f, ReferenceSize(300f), OrbitPeriod(1.0f), 7.0f, 2.92f, new Color(0.26f, 0.68f, 1f, 1f)),
            new PlanetDefinition("mars", "Mars", "res://assets/planets/mars.png", 3020f, BodyRadius(200f), 200f, ReferenceSize(200f), OrbitPeriod(1.8808476f), 7.3f, 4.36f, new Color(0.92f, 0.34f, 0.18f, 1f)),
            new PlanetDefinition("jupiter", "Jupiter", "res://assets/planets/jupiter.png", 4250f, BodyRadius(650f), 650f, ReferenceSize(650f), OrbitPeriod(11.862615f), 4.2f, 5.42f, new Color(0.94f, 0.74f, 0.48f, 1f)),
            new PlanetDefinition("saturn", "Saturn", "res://assets/planets/saturn_user_ref.png", 5450f, BodyRadius(600f), 600f, ReferenceSize(600f), OrbitPeriod(29.447498f), 4.5f, 0.78f, new Color(0.95f, 0.82f, 0.48f, 1f)),
            new PlanetDefinition("uranus", "Uranus", "res://assets/planets/uranus_user_ref.png", 6550f, BodyRadius(500f), 500f, ReferenceSize(500f), OrbitPeriod(84.016846f), -6.4f, 3.85f, new Color(0.52f, 0.88f, 0.92f, 1f)),
            new PlanetDefinition("neptune", "Neptune", "res://assets/planets/neptune.png", 7550f, BodyRadius(450f), 450f, ReferenceSize(450f), OrbitPeriod(164.79132f), 6.0f, 5.10f, new Color(0.20f, 0.44f, 1f, 1f)),
        },
        "orion",
        "Orion");

    public static IReadOnlyList<PlanetDefinition> Planets => Sol.Planets;

    public static float ReferenceSize(float referenceSize)
    {
        return SunVisualWorldSize * referenceSize / ReferenceStarSize;
    }

    public static float BodyRadius(float visualSize)
    {
        return ReferenceSize(visualSize) * 0.5f;
    }

    public static float OrbitPeriod(float realEarthYears)
    {
        return EarthOrbitPeriodSeconds * MathF.Sqrt(realEarthYears);
    }

    public static Vector2 PositionAt(PlanetDefinition planet, float time)
    {
        var angle = planet.InitialAngle + time / planet.OrbitPeriodSeconds * MathF.Tau;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * planet.OrbitRadius;
    }

    public static float RotationAt(PlanetDefinition planet, float time)
    {
        if (Math.Abs(planet.RotationPeriodSeconds) < 0.001f)
        {
            return 0f;
        }

        return time / planet.RotationPeriodSeconds * MathF.Tau;
    }
}

public sealed record PlanetDefinition(
    string Id,
    string DisplayName,
    string TexturePath,
    float OrbitRadius,
    float BodyRadius,
    float ReferenceTextureWorldSize,
    float TextureWorldSize,
    float OrbitPeriodSeconds,
    float RotationPeriodSeconds,
    float InitialAngle,
    Color MapColor,
    PlanetVisualProfile? Visual = null);

public sealed record StarSystemDefinition(
    string Id,
    string DisplayName,
    int Seed,
    StarDefinition Star,
    SpaceBackdropDefinition Background,
    IReadOnlyList<PlanetDefinition> Planets,
    string SectorId = "",
    string SectorName = "");

public sealed record StarDefinition(
    string Archetype,
    string DisplayName,
    string TextureSet,
    float WorldSize,
    Color DiskTint,
    Color CoronaColor,
    float CoronaIntensity,
    float AnimationSpeed,
    Color MapColor,
    string TexturePath = "",
    string FrameDirectory = "",
    string FramePrefix = "sun_");

public sealed record SpaceBackdropDefinition(
    string Archetype,
    string DisplayName,
    string TexturePath,
    Color TextureTint,
    float TextureAlpha,
    float TextureParallax,
    float StarParallax,
    int StarfieldSeed,
    int StarCount,
    int NebulaSeed,
    int NebulaBlobCount,
    float DustDensity,
    IReadOnlyList<Color> NebulaPalette);

public sealed record PlanetVisualProfile(
    string SurfacePath,
    Color AtmosphereColor,
    float AtmosphereStrength,
    float RotationSpeed,
    float FlowStrength,
    float Contrast,
    float Saturation,
    float GlowStrength,
    PlanetRingProfile? Rings);

public sealed record PlanetRingProfile(
    float InnerRadiusFactor,
    float OuterRadiusFactor,
    float Flattening,
    float Rotation,
    float Alpha,
    Color Color,
    Color AccentColor);
