using System.Numerics;

namespace SpaceManagers.Core;

public struct AsteroidState
{
    public AsteroidState(
        int id,
        Vector2 position,
        Vector2 velocity,
        float radius,
        float rotation,
        float angularVelocity,
        float structure,
        float maxStructure,
        float heat,
        int variant,
        int seed)
    {
        Id = id;
        Position = position;
        Velocity = velocity;
        Radius = radius;
        Rotation = rotation;
        AngularVelocity = angularVelocity;
        Structure = structure;
        MaxStructure = maxStructure;
        Heat = heat;
        Variant = variant;
        Seed = seed;
    }

    public int Id { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public float Radius { get; set; }
    public float Rotation { get; set; }
    public float AngularVelocity { get; set; }
    public float Structure { get; set; }
    public float MaxStructure { get; set; }
    public float Heat { get; set; }
    public int Variant { get; set; }
    public int Seed { get; set; }
    public bool IsDestroyed => Structure <= 0f;
    public float CollisionDamage => MathF.Max(0f, Structure);
}

