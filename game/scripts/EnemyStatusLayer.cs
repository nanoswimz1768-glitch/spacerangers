using Godot;
using SpaceManagers.Core;

namespace SpaceManagersPrototype;

public partial class EnemyStatusLayer : Node2D
{
    private IReadOnlyList<ShipState> _ships = Array.Empty<ShipState>();
    private int _playerShipId;
    private Rect2 _visibleWorldRect;
    private IReadOnlySet<int> _visibleStatusIds = EmptyStatusIds;

    private static readonly IReadOnlySet<int> EmptyStatusIds = new HashSet<int>();

    public override void _Ready()
    {
        ZIndex = 26;
    }

    public void SetState(IReadOnlyList<ShipState> ships, int playerShipId, Rect2 visibleWorldRect, IReadOnlySet<int> visibleStatusIds)
    {
        _ships = ships;
        _playerShipId = playerShipId;
        _visibleWorldRect = visibleWorldRect;
        _visibleStatusIds = visibleStatusIds;
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var ship in _ships)
        {
            if (ship.Id == _playerShipId
                || ship.IsDestroyed
                || !_visibleStatusIds.Contains(ship.Id)
                || !_visibleWorldRect.HasPoint(ship.Position.ToGodot()))
            {
                continue;
            }

            DrawShipStatus(ship);
        }
    }

    private void DrawShipStatus(ShipState ship)
    {
        const float width = 64f;
        var rowHeight = 3.5f;
        var gap = 2.2f;
        const float panelHeight = 17.5f;
        var offsetY = -Math.Max(56f, ship.Hitbox.BoundingRadius + 18f);
        var topLeft = ship.Position.ToGodot() + new Vector2(-width * 0.5f, offsetY);
        var barTop = topLeft + new Vector2(0f, 1.2f);

        DrawRect(new Rect2(topLeft - new Vector2(3f, 3f), new Vector2(width + 6f, panelHeight)), new Color(0f, 0.03f, 0.05f, 0.58f), true);
        DrawRect(new Rect2(topLeft - new Vector2(3f, 3f), new Vector2(width + 6f, panelHeight)), RoleColor(ship.Role, 0.35f), false, 1f);

        DrawStatusBar(barTop, width, rowHeight, ship.Combat.Shield, ship.Combat.MaxShield, new Color(0.18f, 0.82f, 1f, 0.92f));
        DrawStatusBar(barTop + new Vector2(0f, rowHeight + gap), width, rowHeight, ship.Combat.Armor, ship.Combat.MaxArmor, new Color(1f, 0.64f, 0.18f, 0.92f));
        DrawStatusBar(barTop + new Vector2(0f, (rowHeight + gap) * 2f), width, rowHeight, ship.Combat.Structure, ship.Combat.MaxStructure, new Color(1f, 0.18f, 0.16f, 0.92f));
    }

    private void DrawStatusBar(Vector2 position, float width, float height, float value, float maxValue, Color color)
    {
        var ratio = maxValue <= 0f ? 0f : Math.Clamp(value / maxValue, 0f, 1f);
        var rect = new Rect2(position, new Vector2(width, height));
        DrawRect(rect, new Color(0f, 0f, 0f, 0.82f), true);
        DrawRect(new Rect2(position, new Vector2(width * ratio, height)), color, true);
    }

    private static Color RoleColor(ShipRole role, float alpha)
    {
        var color = role switch
        {
            ShipRole.Trader => new Color(0.35f, 1f, 0.55f, 1f),
            ShipRole.Diplomat => new Color(0.72f, 0.86f, 1f, 1f),
            ShipRole.Ranger => new Color(0.18f, 0.88f, 1f, 1f),
            ShipRole.Military => new Color(1f, 0.74f, 0.22f, 1f),
            ShipRole.Pirate => new Color(1f, 0.22f, 0.12f, 1f),
            _ => new Color(1f, 0.28f, 0.18f, 1f)
        };
        return new Color(color.R, color.G, color.B, Math.Clamp(alpha, 0f, 1f));
    }
}
