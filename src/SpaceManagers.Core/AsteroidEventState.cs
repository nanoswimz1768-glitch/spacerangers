using System.Numerics;

namespace SpaceManagers.Core;

public readonly record struct AsteroidEventState(
    int Id,
    AsteroidEventType Type,
    Vector2 Position,
    float Radius,
    int Variant,
    int Seed,
    float Rotation,
    float Heat);
