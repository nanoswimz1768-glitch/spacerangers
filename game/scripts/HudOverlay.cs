using Godot;
using SpaceManagers.Core;
using CoreVector2 = System.Numerics.Vector2;

namespace SpaceManagersPrototype;

public partial class HudOverlay : Control
{
    private const float BottomPanelHeight = 60f;
    private const float PanelPadding = 12f;
    private const float StatusBlockWidth = 328f;
    private const float RightBlockWidth = 284f;
    private const float EquipmentSlotSize = 20f;
    private const float EquipmentSlotGap = 7f;
    private const float CombatMeterWidth = 94f;
    private const float CombatMeterGap = 10f;
    private const float MinimapWidth = 360f;
    private const float MinimapHeight = 270f;
    private const float MinimapMargin = 10f;
    private const float TargetPanelWidth = 286f;
    private const float TargetPanelHeight = 154f;

    private readonly Color _panel = new(0.01f, 0.19f, 0.29f, 0.78f);
    private readonly Color _panelBright = new(0.03f, 0.58f, 0.78f, 0.88f);
    private readonly Color _line = new(0.18f, 0.9f, 1f, 0.76f);
    private readonly Label _speed = NewLabel();
    private readonly Label _coords = NewLabel();
    private readonly Label _fps = NewLabel();
    private readonly Label _systemName = NewLabel();
    private readonly Label _weapon = NewLabel();
    private readonly Label _ship = NewLabel();
    private readonly Label _godMode = NewLabel();
    private readonly Label _warpTarget = NewLabel();
    private readonly Label _shield = NewLabel();
    private readonly Label _armor = NewLabel();
    private readonly Label _structure = NewLabel();
    private readonly Label _mode = NewLabel();
    private readonly Vector2[] _leftWing = new Vector2[4];
    private readonly Vector2[] _rightWing = new Vector2[4];
    private readonly Vector2[] _mapAsteroidShape = new Vector2[8];
    private readonly Vector2[] _mapAsteroidFacet = new Vector2[3];
    private readonly Vector2[] _mapPlayerMarker = new Vector2[4];
    private readonly Vector2[] _mapNpcMarker = new Vector2[4];
    private readonly SimulationConfig _asteroidTrajectoryConfig = new();
    private WorldSnapshot? _snapshot;
    private StarSystemDefinition _system = SolarSystem.Sol;
    private int _playerId;
    private CoreVector2 _aimWorld;
    private string _shipName = string.Empty;
    private string _warpTargetName = string.Empty;
    private float _warpChargeRatio;
    private bool _warpHasTarget;
    private bool _warpCharging;
    private bool _warpReady;
    private bool _warpTransit;
    private bool _playerGodMode;
    private float _systemTimeSeconds;
    private bool _hasTargetLock;
    private bool _targetHostile;
    private ShipState _targetLock;
    private ShipState _targetPlayer;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);

        AddChild(_speed);
        AddChild(_coords);
        AddChild(_fps);
        AddChild(_systemName);
        AddChild(_weapon);
        AddChild(_ship);
        AddChild(_godMode);
        AddChild(_warpTarget);
        AddChild(_shield);
        AddChild(_armor);
        AddChild(_structure);
        AddChild(_mode);
    }

    public void SetState(WorldSnapshot snapshot, int playerId, CoreVector2 aimWorld, string shipName, bool playerGodMode, float systemTimeSeconds)
    {
        _snapshot = snapshot;
        _playerId = playerId;
        _aimWorld = aimWorld;
        _shipName = shipName;
        _playerGodMode = playerGodMode;
        _systemTimeSeconds = Math.Max(0f, systemTimeSeconds);
        UpdateLabels();
        QueueRedraw();
    }

    public void SetSystem(StarSystemDefinition system)
    {
        _system = system;
        _asteroidTrajectoryConfig.StarVisualWorldSize = MathF.Max(1f, system.Star.WorldSize);
        QueueRedraw();
    }

    public void SetWarpTarget(string targetName)
    {
        _warpTargetName = targetName;
        UpdateLabels();
        QueueRedraw();
    }

    public void SetWarpDriveState(float chargeRatio, bool hasTarget, bool charging, bool ready, bool transit)
    {
        _warpChargeRatio = Math.Clamp(chargeRatio, 0f, 1f);
        _warpHasTarget = hasTarget;
        _warpCharging = charging;
        _warpReady = ready;
        _warpTransit = transit;
        QueueRedraw();
    }

    public void SetTargetLockState(bool hasTarget, ShipState target, ShipState player, bool hostile)
    {
        _hasTargetLock = hasTarget && target.Id != 0 && !target.IsDestroyed;
        _targetLock = target;
        _targetPlayer = player;
        _targetHostile = hostile;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = GetViewportRect().Size;
        DrawBottomPanel(size);
        DrawTargetPanel(size);
        DrawMinimap(size);
    }

    private void UpdateLabels()
    {
        if (_snapshot is null)
        {
            return;
        }

        var ship = PlayerShip();
        var size = GetViewportRect().Size;
        var panelTop = size.Y - BottomPanelHeight;
        var leftX = PanelPadding + 8f;
        var centerX = size.X * 0.5f;
        var rightX = size.X - RightBlockWidth + 18f;
        var meterX = leftX;

        _shield.Position = new Vector2(meterX, panelTop + 8f);
        _armor.Position = new Vector2(meterX + CombatMeterWidth + CombatMeterGap, panelTop + 8f);
        _structure.Position = new Vector2(meterX + (CombatMeterWidth + CombatMeterGap) * 2f, panelTop + 8f);
        _speed.Position = new Vector2(leftX, panelTop + 41f);
        _coords.Position = new Vector2(leftX + 104f, panelTop + 41f);
        _mode.Position = new Vector2(centerX - 124f, panelTop + 9f);
        _ship.Position = new Vector2(rightX, panelTop + 14f);
        _weapon.Position = new Vector2(rightX, panelTop + 34f);
        var minimapLeft = size.X - MinimapWidth - MinimapMargin;
        var targetPanelLeft = Math.Max(PanelPadding, minimapLeft - TargetPanelWidth - 12f);
        var telemetryX = _hasTargetLock ? targetPanelLeft - 78f : minimapLeft - 76f;
        _fps.Position = new Vector2(Math.Max(PanelPadding, telemetryX), 8f);
        _godMode.Position = new Vector2(Math.Max(PanelPadding, telemetryX - 52f), 24f);
        _systemName.Position = new Vector2(minimapLeft + 8f, MinimapMargin + MinimapHeight + 5f);
        _warpTarget.Position = new Vector2(minimapLeft + 8f, MinimapMargin + MinimapHeight + 22f);

        var mode = ship.Mode == ShipMode.Combat ? "COMBAT" : "NAV";
        var swap = ship.ModeSwitchCooldown > 0f ? $"SWAP {ship.ModeSwitchCooldown:0.0}" : "SWAP RDY";
        _mode.AddThemeColorOverride("font_color", ship.Mode == ShipMode.Combat
            ? new Color(1f, 0.72f, 0.42f, 0.98f)
            : new Color(0.62f, 1f, 0.96f, 0.98f));
        _weapon.AddThemeColorOverride("font_color", ship.Mode == ShipMode.Combat
            ? new Color(0.72f, 1f, 0.9f, 0.98f)
            : new Color(0.72f, 0.9f, 1f, 0.78f));

        _speed.Text = $"SPD {ship.Velocity.Length(),3:0}";
        var gridCell = WorldGrid.CellAt(ship.Position, _snapshot.Bounds);
        var gridLocal = WorldGrid.LocalPosition(ship.Position, _snapshot.Bounds);
        _coords.Text = $"G {SignedGridIndex(gridCell.X)},{SignedGridIndex(gridCell.Y)} X {gridLocal.X,5:0} Y {gridLocal.Y,5:0}";
        _mode.Text = $"{mode}  {swap}";
        _warpTarget.Text = string.IsNullOrWhiteSpace(_warpTargetName)
            ? "WARP --"
            : $"WARP -> {_warpTargetName}";
        _ship.Text = $"SHIP {_shipName}";
        _weapon.Text = $"ENE {ship.Energy,3:0}% GUN {WeaponStatus(ship)}";
        _shield.Text = $"SHD {ship.Combat.Shield,4:0}";
        _armor.Text = $"ARM {ship.Combat.Armor,4:0}";
        _structure.Text = $"STR {ship.Combat.Structure,4:0}";
        _fps.Text = $"FPS {Engine.GetFramesPerSecond()}";
        var systemLabel = string.IsNullOrWhiteSpace(_system.SectorName)
            ? _system.DisplayName
            : $"{_system.SectorName} / {_system.DisplayName}";
        _systemName.Text = $"SYSTEM {systemLabel}";
        _systemName.AddThemeColorOverride("font_color", new Color(0.72f, 1f, 0.96f, 0.94f));
        _warpTarget.AddThemeColorOverride("font_color", string.IsNullOrWhiteSpace(_warpTargetName)
            ? new Color(0.50f, 0.70f, 0.78f, 0.58f)
            : new Color(0.72f, 1f, 0.90f, 0.92f));
        _godMode.Text = _playerGodMode ? "GODMODE" : string.Empty;
        _godMode.AddThemeColorOverride("font_color", new Color(1f, 0.72f, 0.22f, 0.98f));
    }

    private static string WeaponStatus(ShipState ship)
    {
        if (ship.Mode != ShipMode.Combat)
        {
            return "LOCK";
        }

        return ship.WeaponCooldown <= 0f ? "RDY" : ship.WeaponCooldown.ToString("0.00");
    }

    private static string SignedGridIndex(int value)
    {
        return value > 0 ? $"+{value}" : value.ToString();
    }

    private void DrawBottomPanel(Vector2 size)
    {
        var y = size.Y - BottomPanelHeight;
        var rect = new Rect2(0f, y, size.X, BottomPanelHeight);
        DrawRect(rect, _panel, true);
        DrawLine(new Vector2(0f, y), new Vector2(size.X, y), _line, 1.1f, true);
        DrawLine(new Vector2(0f, y + 4f), new Vector2(size.X, y + 4f), new Color(0f, 0.7f, 1f, 0.18f), 1f, true);
        DrawLine(new Vector2(0f, y + BottomPanelHeight - 1f), new Vector2(size.X, y + BottomPanelHeight - 1f), new Color(0f, 0.72f, 1f, 0.18f), 1f, true);

        _leftWing[0] = new Vector2(0f, size.Y);
        _leftWing[1] = new Vector2(0f, y + 13f);
        _leftWing[2] = new Vector2(42f, y + 10f);
        _leftWing[3] = new Vector2(76f, size.Y);
        DrawColoredPolygon(_leftWing, new Color(0.02f, 0.42f, 0.58f, 0.82f));

        _rightWing[0] = new Vector2(size.X, size.Y);
        _rightWing[1] = new Vector2(size.X, y + 13f);
        _rightWing[2] = new Vector2(size.X - 42f, y + 10f);
        _rightWing[3] = new Vector2(size.X - 76f, size.Y);
        DrawColoredPolygon(_rightWing, new Color(0.02f, 0.42f, 0.58f, 0.82f));

        DrawHudBlock(new Rect2(10f, y + 7f, StatusBlockWidth, 46f), new Color(0f, 0.08f, 0.12f, 0.28f));
        DrawHudBlock(new Rect2(size.X - RightBlockWidth - 10f, y + 7f, RightBlockWidth, 46f), new Color(0f, 0.08f, 0.12f, 0.26f));

        var slotsWidth = 7f * EquipmentSlotSize + 6f * EquipmentSlotGap;
        var slotsStart = size.X * 0.5f - slotsWidth * 0.5f;
        for (var i = 0; i < 7; i++)
        {
            var x = slotsStart + i * (EquipmentSlotSize + EquipmentSlotGap);
            DrawRect(new Rect2(x, y + 35f, EquipmentSlotSize, 16f), new Color(0.03f, 0.35f, 0.48f, 0.62f), true);
            DrawRect(new Rect2(x, y + 35f, EquipmentSlotSize, 16f), new Color(0.2f, 0.9f, 1f, 0.28f), false, 1f);
        }

        DrawCombatBars(y);
        DrawWarpChargeBar(size, y);
    }

    private void DrawHudBlock(Rect2 rect, Color fill)
    {
        DrawRect(rect, fill, true);
        DrawRect(rect, new Color(0.16f, 0.82f, 1f, 0.18f), false, 1f);
    }

    private void DrawCombatBars(float panelTop)
    {
        if (_snapshot is null)
        {
            return;
        }

        var ship = PlayerShip();
        var origin = new Vector2(20f, panelTop + 28f);
        DrawMeter(origin, CombatMeterWidth, ship.Combat.Shield, ship.Combat.MaxShield, new Color(0.20f, 0.82f, 1f, 0.94f));
        DrawMeter(origin + new Vector2(CombatMeterWidth + CombatMeterGap, 0f), CombatMeterWidth, ship.Combat.Armor, ship.Combat.MaxArmor, new Color(1f, 0.72f, 0.22f, 0.94f));
        DrawMeter(origin + new Vector2((CombatMeterWidth + CombatMeterGap) * 2f, 0f), CombatMeterWidth, ship.Combat.Structure, ship.Combat.MaxStructure, new Color(0.96f, 0.24f, 0.22f, 0.94f));
    }

    private void DrawMeter(Vector2 position, float width, float value, float maxValue, Color color)
    {
        var ratio = maxValue <= 0f ? 0f : Math.Clamp(value / maxValue, 0f, 1f);
        var rect = new Rect2(position, new Vector2(width, 6f));
        DrawRect(rect, new Color(0f, 0.04f, 0.06f, 0.82f), true);
        DrawRect(new Rect2(position, new Vector2(width * ratio, 6f)), color, true);
        DrawRect(rect, new Color(0.22f, 0.92f, 1f, 0.22f), false, 1f);
    }

    private void DrawWarpChargeBar(Vector2 size, float panelTop)
    {
        if (!_warpHasTarget && !_warpTransit)
        {
            return;
        }

        var font = GetThemeDefaultFont();
        var width = Math.Clamp(size.X * 0.25f, 280f, 420f);
        var position = new Vector2(size.X * 0.5f - width * 0.5f, panelTop - 21f);
        var rect = new Rect2(position, new Vector2(width, 8f));
        var color = _warpTransit
            ? new Color(0.72f, 0.96f, 1f, 0.98f)
            : _warpReady
                ? new Color(1f, 0.78f, 0.28f, 0.96f)
                : _warpCharging
                    ? new Color(0.34f, 1f, 0.88f, 0.92f)
                    : new Color(0.32f, 0.58f, 0.66f, 0.62f);

        DrawRect(rect.Grow(3f), new Color(0f, 0.02f, 0.03f, 0.78f), true);
        DrawRect(rect, new Color(0.01f, 0.10f, 0.14f, 0.88f), true);
        DrawRect(new Rect2(rect.Position, new Vector2(rect.Size.X * _warpChargeRatio, rect.Size.Y)), color, true);
        DrawRect(rect, new Color(0.24f, 0.95f, 1f, 0.32f), false, 1f);

        var label = _warpTransit
            ? "WARP TRANSIT"
            : _warpReady
                ? "WARP READY  B"
                : _warpCharging
                    ? "WARP CALIBRATING"
                    : "WARP HOLD";
        var percent = $"{_warpChargeRatio * 100f,3:0}%";
        DrawString(font, PixelSnap(position + new Vector2(0f, -4f)), label, HorizontalAlignment.Left, width * 0.68f, 12, WithAlpha(color, 0.92f));
        var percentWidth = font?.GetStringSize(percent, HorizontalAlignment.Left, -1f, 12).X ?? 34f;
        DrawString(font, PixelSnap(position + new Vector2(width - percentWidth, -4f)), percent, HorizontalAlignment.Left, -1f, 12, WithAlpha(color, 0.92f));
    }

    private void DrawTargetPanel(Vector2 size)
    {
        if (!_hasTargetLock)
        {
            return;
        }

        var mapLeft = size.X - MinimapWidth - MinimapMargin;
        var panelWidth = Math.Min(TargetPanelWidth, Math.Max(230f, mapLeft - PanelPadding * 2f));
        if (panelWidth <= 210f)
        {
            return;
        }

        var origin = new Vector2(Math.Max(PanelPadding, mapLeft - panelWidth - 12f), MinimapMargin);
        var rect = new Rect2(origin, new Vector2(panelWidth, TargetPanelHeight));
        var accent = TargetHudColor(_targetLock, _targetHostile);
        var font = GetThemeDefaultFont();

        DrawRect(rect, new Color(0f, 0.035f, 0.052f, 0.86f), true);
        DrawRect(rect, WithAlpha(accent, _targetHostile ? 0.70f : 0.52f), false, 1.4f);
        DrawRect(rect.Grow(-3f), new Color(0.14f, 0.92f, 1f, 0.08f), false, 1f);
        DrawLine(origin + new Vector2(0f, 28f), origin + new Vector2(panelWidth, 28f), WithAlpha(accent, 0.28f), 1f, true);

        var name = TargetDisplayName(_targetLock);
        var role = RoleLabel(_targetLock.Role);
        var mode = _targetLock.Mode == ShipMode.Combat ? "COMBAT" : "NAV";
        var distance = CoreVector2.Distance(_targetLock.Position, _targetPlayer.Position);
        var speed = _targetLock.Velocity.Length();
        var relation = _targetHostile ? "HOSTILE" : "NEUTRAL";

        DrawCircle(origin + new Vector2(14f, 14f), 5.6f, WithAlpha(accent, 0.22f));
        DrawCircle(origin + new Vector2(14f, 14f), 3.2f, accent);
        DrawString(font, PixelSnap(origin + new Vector2(27f, 20f)), "TARGET LOCK", HorizontalAlignment.Left, panelWidth - 36f, 13, WithAlpha(accent, 0.92f));
        DrawString(font, PixelSnap(origin + new Vector2(14f, 50f)), name, HorizontalAlignment.Left, panelWidth - 28f, 14, new Color(0.82f, 1f, 0.96f, 0.96f));

        DrawTargetLine(font, origin + new Vector2(14f, 74f), "ROLE", role, accent, panelWidth);
        DrawTargetLine(font, origin + new Vector2(14f, 95f), "STATE", $"{relation} / {mode}", accent, panelWidth);
        DrawTargetLine(font, origin + new Vector2(14f, 116f), "DIST", $"{distance:0} u", accent, panelWidth);
        DrawTargetLine(font, origin + new Vector2(panelWidth * 0.54f, 116f), "SPD", $"{speed:0}", accent, panelWidth * 0.46f - 14f);

        var meterTop = origin + new Vector2(14f, 132f);
        var meterWidth = (panelWidth - 42f) / 3f;
        DrawTargetMeter(meterTop, meterWidth, _targetLock.Combat.Shield, _targetLock.Combat.MaxShield, new Color(0.20f, 0.82f, 1f, 0.92f));
        DrawTargetMeter(meterTop + new Vector2(meterWidth + 7f, 0f), meterWidth, _targetLock.Combat.Armor, _targetLock.Combat.MaxArmor, new Color(1f, 0.68f, 0.22f, 0.92f));
        DrawTargetMeter(meterTop + new Vector2((meterWidth + 7f) * 2f, 0f), meterWidth, _targetLock.Combat.Structure, _targetLock.Combat.MaxStructure, new Color(1f, 0.22f, 0.18f, 0.92f));
    }

    private void DrawTargetLine(Font? font, Vector2 position, string label, string value, Color accent, float width)
    {
        DrawString(font, PixelSnap(position), label, HorizontalAlignment.Left, Math.Min(64f, width * 0.36f), 12, new Color(0.52f, 0.74f, 0.82f, 0.84f));
        DrawString(font, PixelSnap(position + new Vector2(64f, 0f)), value, HorizontalAlignment.Left, Math.Max(24f, width - 66f), 12, WithAlpha(MixColors(accent, Colors.White, 0.62f), 0.92f));
    }

    private void DrawTargetMeter(Vector2 position, float width, float value, float maxValue, Color color)
    {
        var ratio = maxValue <= 0f ? 0f : Math.Clamp(value / maxValue, 0f, 1f);
        var rect = new Rect2(position, new Vector2(width, 6f));
        DrawRect(rect, new Color(0f, 0.018f, 0.026f, 0.88f), true);
        DrawRect(new Rect2(position, new Vector2(width * ratio, 6f)), color, true);
        DrawRect(rect, WithAlpha(color, 0.34f), false, 1f);
    }

    private void DrawMinimap(Vector2 size)
    {
        if (_snapshot is null)
        {
            return;
        }

        var mapSize = new Vector2(MinimapWidth, MinimapHeight);
        var mapPos = new Vector2(size.X - mapSize.X - MinimapMargin, MinimapMargin);
        var mapRect = new Rect2(mapPos, mapSize);
        DrawRect(mapRect, new Color(0f, 0.045f, 0.07f, 0.84f), true);
        DrawRect(mapRect, new Color(0.02f, 0.72f, 0.96f, 0.82f), false, 2f);
        DrawRect(mapRect.Grow(-3f), new Color(0.10f, 0.92f, 1f, 0.18f), false, 1f);
        DrawLine(mapPos + new Vector2(mapSize.X * 0.5f, 0f), mapPos + new Vector2(mapSize.X * 0.5f, mapSize.Y), new Color(0.1f, 0.9f, 1f, 0.24f), 1.15f, true);
        DrawLine(mapPos + new Vector2(0f, mapSize.Y * 0.5f), mapPos + new Vector2(mapSize.X, mapSize.Y * 0.5f), new Color(0.1f, 0.9f, 1f, 0.24f), 1.15f, true);

        var center = mapPos + mapSize * 0.5f;
        var scale = Math.Min(mapSize.X / (_snapshot.Bounds.HalfWidth * 2f), mapSize.Y / (_snapshot.Bounds.HalfHeight * 2f));
        var time = _systemTimeSeconds;
        var player = PlayerShip();
        var activeCell = WorldGrid.CellAt(player.Position, _snapshot.Bounds);
        var activeOrigin = WorldGrid.CellOrigin(activeCell, _snapshot.Bounds);
        var activeCellIsPrimary = activeCell.X == 0 && activeCell.Y == 0;

        if (activeCellIsPrimary)
        {
            var starColor = _system.Star.MapColor;
            var starRadius = MinimapStarRadius(_system.Star);
            DrawCircle(center, starRadius + 3.2f, new Color(starColor.R, starColor.G, starColor.B, 0.14f));
            DrawCircle(center, starRadius, starColor);
            DrawCircle(center, Math.Max(1.6f, starRadius * 0.34f), new Color(1f, 0.95f, 0.62f, 0.92f));
            foreach (var planet in _system.Planets)
            {
                var orbitRadius = planet.OrbitRadius * scale;
                var alpha = planet.OrbitRadius < 3200f ? 0.34f : 0.22f;
                DrawMapOrbit(center, orbitRadius, 144, new Color(0.18f, 0.95f, 0.82f, alpha), mapRect);
            }

            foreach (var planet in _system.Planets)
            {
                var local = SolarSystem.PositionAt(planet, time) * scale;
                var radius = MinimapPlanetRadius(planet);
                DrawCircle(center + local, radius + 1.7f, new Color(planet.MapColor.R, planet.MapColor.G, planet.MapColor.B, 0.22f));
                DrawCircle(center + local, radius, planet.MapColor);
                DrawCircle(center + local, Math.Max(0.9f, radius * 0.34f), new Color(1f, 1f, 1f, 0.30f));
            }
        }

        DrawAsteroidsOnMinimap(center, scale, mapRect, activeOrigin, activeCellIsPrimary);

        ShipState playerMarkerShip = default;
        Vector2 playerMarkerPosition = default;
        var hasPlayerMarker = false;
        foreach (var ship in _snapshot.Ships)
        {
            var localWorld = ship.Position - activeOrigin;
            var local = new Vector2(localWorld.X * scale, localWorld.Y * scale);
            var position = center + local;
            if (!mapRect.HasPoint(position))
            {
                continue;
            }

            if (ship.Id == _playerId)
            {
                playerMarkerShip = ship;
                playerMarkerPosition = position;
                hasPlayerMarker = true;
            }
            else
            {
                DrawNpcMinimapMarker(ship, position, MinimapShipPaletteColor(ship));
            }
        }

        if (hasPlayerMarker)
        {
            DrawPlayerMinimapMarker(playerMarkerShip, playerMarkerPosition);
        }

        var aimWorld = _aimWorld - activeOrigin;
        var aim = center + new Vector2(aimWorld.X * scale, aimWorld.Y * scale);
        if (mapRect.HasPoint(aim))
        {
            DrawCircle(aim, 3.1f, new Color(1f, 0.9f, 0.3f, 0.24f));
            DrawCircle(aim, 1.8f, new Color(1f, 0.9f, 0.3f, 0.95f));
        }
    }

    private void DrawAsteroidsOnMinimap(Vector2 center, float scale, Rect2 mapRect, CoreVector2 activeOrigin, bool useSolarTrajectories)
    {
        if (_snapshot is null || _snapshot.Asteroids.Count == 0)
        {
            return;
        }

        foreach (var asteroid in _snapshot.Asteroids)
        {
            if (asteroid.IsDestroyed || !IsFinite(asteroid.Position) || !IsFinite(asteroid.Velocity))
            {
                continue;
            }

            var localWorld = asteroid.Position - activeOrigin;
            if (!AsteroidBelongsToActiveVisualCell(localWorld, asteroid.Radius, _snapshot.Bounds))
            {
                continue;
            }

            DrawAsteroidTrajectory(center, scale, mapRect, asteroid, activeOrigin, useSolarTrajectories);
        }

        foreach (var asteroid in _snapshot.Asteroids)
        {
            if (asteroid.IsDestroyed || !IsFinite(asteroid.Position))
            {
                continue;
            }

            var localWorld = asteroid.Position - activeOrigin;
            if (!AsteroidBelongsToActiveVisualCell(localWorld, asteroid.Radius, _snapshot.Bounds))
            {
                continue;
            }

            var local = new Vector2(localWorld.X * scale, localWorld.Y * scale);
            var position = center + local;
            if (!mapRect.HasPoint(position))
            {
                continue;
            }

            var heat = Math.Clamp(asteroid.Heat, 0f, 1f);
            var color = new Color(0.78f + heat * 0.22f, 0.70f - heat * 0.16f, 0.56f - heat * 0.36f, 0.95f);
            var radius = Math.Clamp(1.5f + MathF.Sqrt(Math.Max(1f, asteroid.Radius)) * 0.13f, 2.0f, 4.8f);
            DrawAsteroidMinimapIcon(asteroid, position, radius, color, heat);
        }
    }

    private static bool AsteroidBelongsToActiveVisualCell(CoreVector2 localWorld, float radius, WorldBounds bounds)
    {
        var margin = radius + 1800f;
        return localWorld.X >= -bounds.HalfWidth - margin
            && localWorld.X <= bounds.HalfWidth + margin
            && localWorld.Y >= -bounds.HalfHeight - margin
            && localWorld.Y <= bounds.HalfHeight + margin;
    }

    private static float MinimapStarRadius(StarDefinition star)
    {
        var ratio = MathF.Sqrt(Math.Max(0.1f, star.WorldSize / SolarSystem.SunVisualWorldSize));
        return Math.Clamp(4.4f + ratio * 2.25f, 5.4f, 7.8f);
    }

    private static float MinimapPlanetRadius(PlanetDefinition planet)
    {
        var ratio = MathF.Sqrt(Math.Max(0.08f, planet.BodyRadius / 190f));
        return Math.Clamp(1.45f + ratio * 1.55f, 2.2f, 4.8f);
    }

    private static Color MinimapShipPaletteColor(ShipState ship)
    {
        if (!string.IsNullOrWhiteSpace(ship.VisualId))
        {
            return WithAlpha(ShipCatalog.ThrustOuterColor(ship.VisualId), 0.96f);
        }

        return ship.Role switch
        {
            ShipRole.Trader => new Color(0.38f, 0.96f, 0.56f, 0.96f),
            ShipRole.Diplomat => new Color(0.72f, 0.86f, 1f, 0.96f),
            ShipRole.Ranger => new Color(0.18f, 0.88f, 1f, 0.96f),
            ShipRole.Military => new Color(1f, 0.74f, 0.22f, 0.96f),
            ShipRole.Pirate => new Color(1f, 0.22f, 0.12f, 0.96f),
            _ => new Color(1f, 0.42f, 0.28f, 0.96f)
        };
    }

    private static Color TargetHudColor(ShipState ship, bool hostile)
    {
        if (hostile)
        {
            return new Color(1f, 0.18f, 0.10f, 1f);
        }

        return ship.Role == ShipRole.Ranger
            ? new Color(0.66f, 1f, 0.96f, 1f)
            : new Color(0.76f, 0.94f, 1f, 1f);
    }

    private static string TargetDisplayName(ShipState ship)
    {
        if (!string.IsNullOrWhiteSpace(ship.Callsign))
        {
            return ship.Callsign;
        }

        return !string.IsNullOrWhiteSpace(ship.VisualId)
            ? ship.VisualId
            : $"Ship {ship.Id}";
    }

    private static string RoleLabel(ShipRole role)
    {
        return role switch
        {
            ShipRole.Trader => "TRADER",
            ShipRole.Diplomat => "DIPLOMAT",
            ShipRole.Ranger => "RANGER",
            ShipRole.Military => "MILITARY",
            ShipRole.Pirate => "PIRATE",
            _ => "SHIP"
        };
    }

    private void DrawNpcMinimapMarker(ShipState ship, Vector2 position, Color palette)
    {
        var forward = ForwardFromRotation(ship.Rotation);
        var right = new Vector2(-forward.Y, forward.X);
        var glow = WithAlpha(palette, ship.Role == ShipRole.Pirate ? 0.15f : 0.10f);
        var outline = WithAlpha(palette, ship.Role == ShipRole.Pirate ? 0.86f : 0.72f);
        var core = string.IsNullOrWhiteSpace(ship.VisualId)
            ? WithAlpha(MixColors(palette, Colors.White, 0.45f), 0.82f)
            : WithAlpha(ShipCatalog.ThrustCoreColor(ship.VisualId), 0.82f);

        DrawCircle(position, ship.Role == ShipRole.Pirate ? 4.6f : 3.9f, glow);
        DrawCircle(position, 2.25f, new Color(0f, 0.025f, 0.035f, 0.82f));
        DrawCircle(position, 1.28f, core);
        DrawLine(position + forward * 1.9f, position + forward * 5.2f, outline, 0.82f, true);

        if (ship.Role == ShipRole.Pirate)
        {
            DrawLine(position - forward * 2.6f - right * 3.0f, position - right * 4.4f, outline, 0.82f, true);
            DrawLine(position - forward * 2.6f + right * 3.0f, position + right * 4.4f, outline, 0.82f, true);
            return;
        }

        switch (ship.Role)
        {
            case ShipRole.Trader:
                DrawLine(position - forward * 2.2f - right * 1.8f, position - forward * 2.2f + right * 1.8f, WithAlpha(core, 0.58f), 0.72f, true);
                break;
            case ShipRole.Diplomat:
                DrawArc(position, 3.4f, 0.25f, MathF.Tau - 0.25f, 14, WithAlpha(palette, 0.42f), 0.72f, true);
                break;
            case ShipRole.Ranger:
                DrawLine(position - right * 2.6f, position + right * 2.6f, WithAlpha(core, 0.48f), 0.68f, true);
                break;
            case ShipRole.Military:
                _mapNpcMarker[0] = position + forward * 3.1f;
                _mapNpcMarker[1] = position + right * 2.3f;
                _mapNpcMarker[2] = position - forward * 3.1f;
                _mapNpcMarker[3] = position - right * 2.3f;
                DrawClosedPolyline(_mapNpcMarker, WithAlpha(core, 0.50f), 0.66f);
                break;
        }
    }

    private void DrawPlayerMinimapMarker(ShipState ship, Vector2 position)
    {
        var forward = ForwardFromRotation(ship.Rotation);
        var right = new Vector2(-forward.Y, forward.X);
        var noseAngle = MathF.Atan2(forward.Y, forward.X);
        DrawCircle(position, 10.8f, new Color(0.06f, 0.90f, 1f, 0.10f));
        DrawCircle(position, 7.3f, new Color(1f, 0.70f, 0.16f, 0.12f));
        for (var segment = 0; segment < 4; segment++)
        {
            var start = segment * MathF.PI * 0.5f + 0.16f;
            DrawArc(position, 8.1f, start, start + 0.78f, 8, new Color(0.15f, 0.98f, 1f, 0.70f), 1.05f, true);
        }
        DrawArc(position, 5.4f, noseAngle - 0.84f, noseAngle + 0.84f, 12, new Color(1f, 0.76f, 0.22f, 0.80f), 0.92f, true);

        SetPlayerMarkerPoints(position, forward, right, 0.52f);
        DrawColoredPolygon(_mapPlayerMarker, new Color(0.03f, 0.02f, 0.01f, 0.90f));
        DrawClosedPolyline(_mapPlayerMarker, new Color(1f, 0.78f, 0.24f, 0.92f), 0.95f);
        DrawLine(position + forward * 1.2f, position + forward * 6.2f, new Color(1f, 0.98f, 0.66f, 0.82f), 0.88f, true);
        DrawLine(position - right * 4.9f, position + right * 4.9f, new Color(0.10f, 0.96f, 1f, 0.28f), 0.68f, true);
    }

    private void DrawAsteroidMinimapIcon(AsteroidState asteroid, Vector2 position, float radius, Color color, float heat)
    {
        var rotation = asteroid.Rotation + asteroid.Variant * 0.37f;
        if (heat > 0.04f)
        {
            SetAsteroidMarkerPoints(position, radius + 2.2f, rotation, asteroid.Seed + 917);
            DrawColoredPolygon(_mapAsteroidShape, new Color(1f, 0.34f, 0.08f, 0.10f + heat * 0.30f));
        }

        SetAsteroidMarkerPoints(position, radius + 0.95f, rotation, asteroid.Seed + 131);
        DrawColoredPolygon(_mapAsteroidShape, new Color(0.035f, 0.030f, 0.026f, 0.78f));

        SetAsteroidMarkerPoints(position, radius, rotation, asteroid.Seed);
        DrawColoredPolygon(_mapAsteroidShape, color);
        for (var index = 0; index < _mapAsteroidShape.Length; index++)
        {
            DrawLine(
                _mapAsteroidShape[index],
                _mapAsteroidShape[(index + 1) % _mapAsteroidShape.Length],
                new Color(0.09f, 0.08f, 0.065f, 0.72f),
                0.85f,
                true);
        }

        var facet = new Vector2(MathF.Cos(rotation + 0.92f), MathF.Sin(rotation + 0.92f));
        var facetTangent = new Vector2(-facet.Y, facet.X);
        _mapAsteroidFacet[0] = position + facet * radius * 0.18f;
        _mapAsteroidFacet[1] = position - facet * radius * 0.58f + facetTangent * radius * 0.30f;
        _mapAsteroidFacet[2] = position - facet * radius * 0.28f - facetTangent * radius * 0.36f;
        DrawColoredPolygon(_mapAsteroidFacet, new Color(0.08f, 0.07f, 0.06f, 0.22f));

        var crackA = position + new Vector2(MathF.Cos(rotation + 2.4f), MathF.Sin(rotation + 2.4f)) * radius * 0.54f;
        var crackB = position + new Vector2(MathF.Cos(rotation + 4.9f), MathF.Sin(rotation + 4.9f)) * radius * 0.43f;
        DrawLine(crackA, crackB, new Color(0.045f, 0.038f, 0.033f, 0.52f), 0.7f, true);
    }

    private void DrawAsteroidTrajectory(Vector2 center, float scale, Rect2 mapRect, AsteroidState asteroid, CoreVector2 activeOrigin, bool useSolarTrajectory)
    {
        if (_snapshot is null)
        {
            return;
        }

        var bounds = _snapshot.Bounds;
        var localStart = asteroid.Position - activeOrigin;
        if (useSolarTrajectory
            && (AsteroidPhysics.IsOutsideRemovalBounds(localStart, asteroid.Radius, _asteroidTrajectoryConfig)
                || AsteroidPhysics.IsInsideSunBurnZone(localStart, asteroid.Radius, _asteroidTrajectoryConfig)))
        {
            return;
        }

        var position = localStart;
        var velocity = asteroid.Velocity;
        var previousWorld = position;
        var previousMap = center + new Vector2(position.X * scale, position.Y * scale);
        var previousInside = mapRect.HasPoint(previousMap);
        var maxPredictionSteps = useSolarTrajectory ? 760 : 240;
        const int drawEverySteps = 5;

        for (var step = 0; step < maxPredictionSteps; step++)
        {
            var predictionDelta = useSolarTrajectory
                ? Math.Clamp(MathF.Max(1f, position.Length()) / 7600f, 0.08f, 0.42f)
                : 0.24f;
            if (useSolarTrajectory)
            {
                velocity += AsteroidPhysics.SolarGravity(position, _asteroidTrajectoryConfig) * predictionDelta;
            }

            position += velocity * predictionDelta;

            if (!IsFinite(position) || !IsFinite(velocity))
            {
                break;
            }

            var currentMap = center + new Vector2(position.X * scale, position.Y * scale);
            var currentInside = mapRect.HasPoint(currentMap);

            if ((step % drawEverySteps) == 0 && previousInside && currentInside && IsFinite(previousMap) && IsFinite(currentMap))
            {
                var alpha = Math.Clamp(0.28f - step / (float)maxPredictionSteps * 0.20f, 0.05f, 0.28f);
                DrawLine(previousMap, currentMap, new Color(1f, 0.72f, 0.30f, alpha), 0.82f, false);
            }

            if (useSolarTrajectory && TryBurnIntersection(previousWorld, position, asteroid.Radius, out var burnPoint))
            {
                var burnMap = center + new Vector2(burnPoint.X * scale, burnPoint.Y * scale);
                if (previousInside && mapRect.HasPoint(burnMap) && IsFinite(previousMap) && IsFinite(burnMap))
                {
                    DrawLine(previousMap, burnMap, new Color(1f, 0.46f, 0.10f, 0.34f), 0.9f, false);
                }

                if (mapRect.HasPoint(burnMap) && IsFinite(burnMap))
                {
                    DrawCircle(burnMap, 2.8f, new Color(1f, 0.28f, 0.05f, 0.20f));
                    DrawCircle(burnMap, 1.5f, new Color(1f, 0.34f, 0.08f, 0.58f));
                }

                break;
            }

            if ((useSolarTrajectory && AsteroidPhysics.IsOutsideRemovalBounds(position, asteroid.Radius, _asteroidTrajectoryConfig))
                || MathF.Abs(position.X) > bounds.HalfWidth + 2400f
                || MathF.Abs(position.Y) > bounds.HalfHeight + 2400f
                || position.LengthSquared() > 120000f * 120000f)
            {
                break;
            }

            previousWorld = position;
            previousMap = currentMap;
            previousInside = currentInside;
        }
    }

    private void DrawMapOrbit(Vector2 center, float radius, int segments, Color color, Rect2 mapRect)
    {
        if (radius <= 0f || !float.IsFinite(radius))
        {
            return;
        }

        var previous = center + new Vector2(radius, 0f);
        var previousInside = mapRect.Grow(2f).HasPoint(previous);
        for (var index = 1; index <= segments; index++)
        {
            var angle = index / (float)segments * MathF.Tau;
            var current = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            var currentInside = mapRect.Grow(2f).HasPoint(current);
            if (previousInside && currentInside && IsFinite(previous) && IsFinite(current))
            {
                DrawLine(previous, current, color, 1f, false);
            }

            previous = current;
            previousInside = currentInside;
        }
    }

    private void SetPlayerMarkerPoints(Vector2 position, Vector2 forward, Vector2 right, float scale)
    {
        _mapPlayerMarker[0] = position + forward * (8.2f * scale);
        _mapPlayerMarker[1] = position - forward * (3.2f * scale) + right * (5.6f * scale);
        _mapPlayerMarker[2] = position - forward * (6.0f * scale);
        _mapPlayerMarker[3] = position - forward * (3.2f * scale) - right * (5.6f * scale);
    }

    private void DrawClosedPolyline(Vector2[] points, Color color, float width)
    {
        DrawPolyline(points, color, width, true);
        if (points.Length > 1)
        {
            DrawLine(points[^1], points[0], color, width, true);
        }
    }

    private void SetAsteroidMarkerPoints(Vector2 position, float radius, float rotation, int seed)
    {
        for (var index = 0; index < _mapAsteroidShape.Length; index++)
        {
            var angle = rotation + index / (float)_mapAsteroidShape.Length * MathF.Tau;
            var jitter = 0.76f + Hash01(seed * 0.013f + index * 9.37f) * 0.34f;
            _mapAsteroidShape[index] = position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * jitter;
        }
    }

    private bool TryBurnIntersection(CoreVector2 from, CoreVector2 to, float asteroidRadius, out CoreVector2 intersection)
    {
        var burnRadius = AsteroidPhysics.AsteroidSunBurnRadius(_asteroidTrajectoryConfig) + asteroidRadius * 0.35f;
        var fromInside = from.LengthSquared() <= burnRadius * burnRadius;
        var toInside = to.LengthSquared() <= burnRadius * burnRadius;
        if (!toInside)
        {
            intersection = default;
            return false;
        }

        if (fromInside)
        {
            intersection = from;
            return true;
        }

        var segment = to - from;
        var a = CoreVector2.Dot(segment, segment);
        var b = 2f * CoreVector2.Dot(from, segment);
        var c = CoreVector2.Dot(from, from) - burnRadius * burnRadius;
        var discriminant = b * b - 4f * a * c;
        if (a <= 0.0001f || discriminant < 0f)
        {
            intersection = to;
            return true;
        }

        var t = Math.Clamp((-b - MathF.Sqrt(discriminant)) / (2f * a), 0f, 1f);
        intersection = from + segment * t;
        return true;
    }

    private static bool IsFinite(CoreVector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }

    private static Vector2 PixelSnap(Vector2 point)
    {
        return new Vector2(MathF.Round(point.X), MathF.Round(point.Y));
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, Math.Clamp(alpha, 0f, 1f));
    }

    private static Color MixColors(Color from, Color to, float amount)
    {
        var t = Math.Clamp(amount, 0f, 1f);
        return new Color(
            from.R + (to.R - from.R) * t,
            from.G + (to.G - from.G) * t,
            from.B + (to.B - from.B) * t,
            from.A + (to.A - from.A) * t);
    }

    private static Vector2 ForwardFromRotation(float rotation)
    {
        var forward = new Vector2(MathF.Sin(rotation), -MathF.Cos(rotation));
        return forward.LengthSquared() <= 0.001f ? Vector2.Up : forward.Normalized();
    }

    private static float Hash01(float value)
    {
        var hashed = MathF.Sin(value) * 43758.5453f;
        return hashed - MathF.Floor(hashed);
    }

    private static Label NewLabel()
    {
        var label = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 10
        };
        label.AddThemeColorOverride("font_color", new Color(0.65f, 0.96f, 1f, 0.95f));
        label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.9f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        label.AddThemeFontSizeOverride("font_size", 12);
        return label;
    }

    private ShipState PlayerShip()
    {
        if (_snapshot is { Ships.Count: > 0 } snapshot && snapshot.Ships[0].Id == _playerId)
        {
            return snapshot.Ships[0];
        }

        if (_snapshot is not null)
        {
            foreach (var ship in _snapshot.Ships)
            {
                if (ship.Id == _playerId)
                {
                    return ship;
                }
            }
        }

        return default;
    }

}
