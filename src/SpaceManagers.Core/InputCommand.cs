using System.Numerics;

namespace SpaceManagers.Core;

public readonly record struct InputCommand(
    float Forward,
    float Reverse,
    float Strafe,
    float Turn,
    Vector2 AimWorld,
    bool Fire,
    bool Afterburner = false,
    bool ToggleMode = false)
{
    public static InputCommand Idle(Vector2 aimWorld) => new(0f, 0f, 0f, 0f, aimWorld, false, false, false);
}
