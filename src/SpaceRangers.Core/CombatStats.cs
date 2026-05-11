namespace SpaceRangers.Core;

public readonly record struct CombatStats(
    float Shield,
    float Armor,
    float Structure,
    float MaxShield,
    float MaxArmor,
    float MaxStructure,
    float ShieldRegenLockout)
{
    public static CombatStats Default { get; } = new(1000f, 1000f, 1000f, 1000f, 1000f, 1000f, 0f);

    public bool IsDestroyed => Structure <= 0f;

    public CombatStats ApplyDamage(float damage, float shieldZeroRegenLockout)
    {
        if (damage <= 0f || IsDestroyed)
        {
            return this;
        }

        var remaining = damage;
        var shield = Shield;
        var armor = Armor;
        var structure = Structure;
        var lockout = ShieldRegenLockout;

        if (shield > 0f)
        {
            var absorbed = MathF.Min(shield, remaining);
            shield -= absorbed;
            remaining -= absorbed;

            if (shield <= 0f)
            {
                shield = 0f;
                lockout = MathF.Max(lockout, shieldZeroRegenLockout);
            }
        }

        if (remaining > 0f && armor > 0f)
        {
            var absorbed = MathF.Min(armor, remaining);
            armor -= absorbed;
            remaining -= absorbed;
        }

        if (remaining > 0f && structure > 0f)
        {
            structure = MathF.Max(0f, structure - remaining);
        }

        return this with
        {
            Shield = Math.Clamp(shield, 0f, MaxShield),
            Armor = Math.Clamp(armor, 0f, MaxArmor),
            Structure = Math.Clamp(structure, 0f, MaxStructure),
            ShieldRegenLockout = lockout
        };
    }

    public CombatStats RegenerateShield(float delta, float shieldRegenPerSecond)
    {
        if (delta <= 0f || IsDestroyed)
        {
            return this;
        }

        var lockout = MathF.Max(0f, ShieldRegenLockout - delta);
        var shield = Shield;
        if (lockout <= 0f && shield < MaxShield && shieldRegenPerSecond > 0f)
        {
            shield = MathF.Min(MaxShield, shield + shieldRegenPerSecond * delta);
        }

        return this with
        {
            Shield = shield,
            ShieldRegenLockout = lockout
        };
    }
}
