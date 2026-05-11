namespace SpaceRangers.Core;

public sealed record WorldSnapshot(
    long Tick,
    IReadOnlyList<ShipState> Ships,
    IReadOnlyList<ProjectileState> Projectiles,
    IReadOnlyList<ProjectileImpactState> ProjectileImpacts,
    WorldBounds Bounds,
    IReadOnlyList<AsteroidState> Asteroids,
    IReadOnlyList<AsteroidEventState> AsteroidEvents)
{
    public WorldSnapshot(
        long tick,
        IReadOnlyList<ShipState> ships,
        IReadOnlyList<ProjectileState> projectiles,
        WorldBounds bounds)
        : this(tick, ships, projectiles, Array.Empty<ProjectileImpactState>(), bounds, Array.Empty<AsteroidState>(), Array.Empty<AsteroidEventState>())
    {
    }
}
