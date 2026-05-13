namespace SpaceManagers.Core;

public readonly record struct WeaponDamageProfile(
    float ShieldMultiplier,
    float ArmorMultiplier,
    float StructureMultiplier,
    float AsteroidMultiplier)
{
    public float MultiplierFor(ProjectileImpactSurface surface)
    {
        return surface switch
        {
            ProjectileImpactSurface.Shield => ShieldMultiplier,
            ProjectileImpactSurface.Armor => ArmorMultiplier,
            ProjectileImpactSurface.Structure => StructureMultiplier,
            ProjectileImpactSurface.Asteroid => AsteroidMultiplier,
            _ => 1f
        };
    }

    public float DamageFor(ProjectileImpactSurface surface, float baseDamage)
    {
        return MathF.Max(0f, baseDamage) * MathF.Max(0f, MultiplierFor(surface));
    }
}
