using System.Numerics;

namespace SpaceManagers.Core;

public readonly record struct WorldGridCell(int X, int Y);

public static class WorldGrid
{
    public static Vector2 CellSize(WorldBounds bounds)
    {
        return new Vector2(MathF.Max(1f, bounds.HalfWidth * 2f), MathF.Max(1f, bounds.HalfHeight * 2f));
    }

    public static WorldGridCell CellAt(Vector2 position, WorldBounds bounds)
    {
        var size = CellSize(bounds);
        return new WorldGridCell(
            (int)MathF.Floor((position.X + bounds.HalfWidth) / size.X),
            (int)MathF.Floor((position.Y + bounds.HalfHeight) / size.Y));
    }

    public static Vector2 CellOrigin(WorldGridCell cell, WorldBounds bounds)
    {
        var size = CellSize(bounds);
        return new Vector2(cell.X * size.X, cell.Y * size.Y);
    }

    public static Vector2 LocalPosition(Vector2 position, WorldBounds bounds)
    {
        return position - CellOrigin(CellAt(position, bounds), bounds);
    }

    public static bool IsPrimaryCell(Vector2 position, WorldBounds bounds)
    {
        var cell = CellAt(position, bounds);
        return cell.X == 0 && cell.Y == 0;
    }
}
