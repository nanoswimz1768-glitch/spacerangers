using System.Numerics;

namespace SpaceRangers.Core;

public readonly record struct AsteroidEventState(
    int Id,
    AsteroidEventType Type,
    Vector2 Position,
    float Radius,
    int Variant,
    int Seed,
    float Rotation,
    float Heat);
