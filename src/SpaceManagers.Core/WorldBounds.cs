using System.Numerics;

namespace SpaceManagers.Core;

public readonly record struct WorldBounds(float HalfWidth, float HalfHeight)
{
    public Vector2 Clamp(Vector2 position, float padding = 0f)
    {
        return new Vector2(
            Math.Clamp(position.X, -HalfWidth + padding, HalfWidth - padding),
            Math.Clamp(position.Y, -HalfHeight + padding, HalfHeight - padding));
    }

    public bool Contains(Vector2 position, float padding = 0f)
    {
        return position.X >= -HalfWidth + padding
            && position.X <= HalfWidth - padding
            && position.Y >= -HalfHeight + padding
            && position.Y <= HalfHeight - padding;
    }
}

