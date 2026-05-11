using System.Numerics;

namespace SpaceManagers.Core;

public readonly record struct ProjectileImpactState(
    int Id,
    int TargetId,
    ProjectileImpactSurface Surface,
    Vector2 Position,
    Vector2 Direction,
    Vector2 TargetCenter,
    float TargetRadius,
    Vector2 TargetSize,
    float TargetRotation,
    float ShieldRatio,
    float Damage,
    float Speed,
    ProjectileImpactKind Kind,
    int Seed);
