using Godot;

namespace SpaceManagersPrototype;

public sealed record StarMapSystemEntry(
    string Id,
    string DisplayName,
    string SectorId,
    string SectorName,
    string StarArchetype,
    Color StarColor,
    int PlanetCount,
    string Source,
    string File);
