namespace SpaceManagers.Core;

public static class WeaponCatalog
{
    public static readonly WeaponDefinition BasicProjectileCannon = new()
    {
        Id = "basic_projectile_cannon",
        DisplayName = "Projectile Cannon",
        DamageType = WeaponDamageType.Projectile,
        FireMode = WeaponFireMode.Manual,
        Damage = 100f,
        Cooldown = 0.135f,
        EnergyCost = 2.5f,
        ProjectileSpeed = 1450f,
        ProjectileLifetime = 1.35f,
        Range = 1450f * 1.35f,
        ManualConeDegrees = 60f
    };

    private static readonly Dictionary<string, WeaponDefinition> Definitions = new(StringComparer.OrdinalIgnoreCase)
    {
        [BasicProjectileCannon.Id] = BasicProjectileCannon
    };

    public static WeaponDefinition Default => BasicProjectileCannon;

    public static WeaponDamageProfile DamageProfileFor(WeaponDamageType type)
    {
        return type switch
        {
            WeaponDamageType.Projectile => new WeaponDamageProfile(0.50f, 1.00f, 0.60f, 1.00f),
            WeaponDamageType.Laser => new WeaponDamageProfile(1.00f, 0.50f, 0.60f, 1.00f),
            WeaponDamageType.Hybrid => new WeaponDamageProfile(0.80f, 0.80f, 0.60f, 1.00f),
            _ => new WeaponDamageProfile(1.00f, 1.00f, 0.60f, 1.00f)
        };
    }

    public static WeaponDefinition Get(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Default;
        }

        return Definitions.TryGetValue(id, out var definition)
            ? definition
            : Default;
    }
}
