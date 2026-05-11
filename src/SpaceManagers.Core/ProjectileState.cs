using System.Numerics;

namespace SpaceManagers.Core;

public struct ProjectileState
{
    public ProjectileState(int id, int ownerId, Vector2 position, Vector2 velocity, float lifetime)
        : this(id, ownerId, position, velocity, lifetime, 100f)
    {
    }

    public ProjectileState(int id, int ownerId, Vector2 position, Vector2 velocity, float lifetime, float damage)
    {
        Id = id;
        OwnerId = ownerId;
        Position = position;
        Velocity = velocity;
        Lifetime = lifetime;
        Damage = damage;
    }

    public int Id { get; set; }
    public int OwnerId { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public float Lifetime { get; set; }
    public float Damage { get; set; }
}
