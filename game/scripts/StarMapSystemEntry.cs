using Godot;

namespace SpaceManagersPrototype;

public sealed record StarMapSystemEntry(
    string Id,
    string DisplayName,
    string SectorId,
    string SectorName,
    string StarArchetype,
    string StarDisplayName,
    Color StarColor,
    float StarWorldSize,
    float CoronaIntensity,
    float AnimationSpeed,
    int PlanetCount,
    IReadOnlyList<StarMapPlanetEntry> Planets,
    string Source,
    string File);

public sealed record StarMapPlanetEntry(
    string DisplayName,
    string Archetype,
    Color MapColor,
    float OrbitRadius,
    float ReferenceSize,
    bool HasRings);
