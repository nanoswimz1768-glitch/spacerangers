using System.Numerics;

namespace SpaceRangers.Core;

public struct ShipState
{
    public ShipState(int id, Vector2 position, Vector2 velocity, float rotation, float energy, float weaponCooldown)
        : this(id, position, velocity, rotation, energy, weaponCooldown, ShipHitbox.Default, CombatStats.Default)
    {
    }

    public ShipState(
        int id,
        Vector2 position,
        Vector2 velocity,
        float rotation,
        float energy,
        float weaponCooldown,
        ShipHitbox hitbox,
        CombatStats combat)
        : this(id, position, velocity, rotation, energy, weaponCooldown, hitbox, combat, ShipMode.Navigation, 0f)
    {
    }

    public ShipState(
        int id,
        Vector2 position,
        Vector2 velocity,
        float rotation,
        float energy,
        float weaponCooldown,
        ShipHitbox hitbox,
        CombatStats combat,
        ShipMode mode,
        float modeSwitchCooldown)
    {
        Id = id;
        Position = position;
        Velocity = velocity;
        Rotation = rotation;
        Energy = energy;
        WeaponCooldown = weaponCooldown;
        Hitbox = hitbox;
        Combat = combat;
        Mode = mode;
        ModeSwitchCooldown = modeSwitchCooldown;
    }

    public int Id { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public float Rotation { get; set; }
    public float Energy { get; set; }
    public float WeaponCooldown { get; set; }
    public ShipHitbox Hitbox { get; set; }
    public CombatStats Combat { get; set; }
    public ShipMode Mode { get; set; }
    public float ModeSwitchCooldown { get; set; }
    public bool IsDestroyed => Combat.IsDestroyed;
}
