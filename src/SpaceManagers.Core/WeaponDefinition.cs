namespace SpaceManagers.Core;

public sealed record WeaponDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public WeaponDamageType DamageType { get; init; }
    public WeaponFireMode FireMode { get; init; }
    public float Damage { get; init; }
    public float Cooldown { get; init; }
    public float EnergyCost { get; init; }
    public float ProjectileSpeed { get; init; }
    public float ProjectileLifetime { get; init; }
    public float Range { get; init; }
    public float ManualConeDegrees { get; init; } = 60f;

    public WeaponDamageProfile DamageProfile => WeaponCatalog.DamageProfileFor(DamageType);

    public float EffectiveRange => Range > 0f
        ? Range
        : MathF.Max(0f, ProjectileSpeed) * MathF.Max(0f, ProjectileLifetime);
}
