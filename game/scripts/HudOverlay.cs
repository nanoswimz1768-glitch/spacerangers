using Godot;
using SpaceRangers.Core;
using CoreVector2 = System.Numerics.Vector2;

namespace SpaceRangersPrototype;

public partial class HudOverlay : Control
{
    private const float BottomPanelHeight = 52f;
    private const float PanelPadding = 12f;
    private const float StatusBlockWidth = 294f;
    private const float RightBlockWidth = 252f;
    private const float EquipmentSlotSize = 18f;
    private const float EquipmentSlotGap = 6f;
    private const float CombatMeterWidth = 82f;
    private const float CombatMeterGap = 10f;
    private const float MinimapWidth = 264f;
    private const float MinimapHeight = 204f;
    private const float MinimapMargin = 10f;

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
    private readonly Label _shield = NewLabel();
    private readonly Label _armor = NewLabel();
    private readonly Label _structure = NewLabel();
    private readonly Label _mode = NewLabel();
    private readonly Vector2[] _leftWing = new Vector2[4];
    private readonly Vector2[] _rightWing = new Vector2[4];
    private readonly Vector2[] _mapAsteroidShape = new Vector2[8];
    private readonly Vector2[] _mapAsteroidFacet = new Vector2[3];
    private readonly Vector2[] _mapPlayerMarker = new Vector2[4];
    private readonly Vector2[] _mapVelocityArrow = new Vector2[3];
    private readonly SimulationConfig _asteroidTrajectoryConfig = new();
    private WorldSnapshot? _snapshot;
    private StarSystemDefinition _system = SolarSystem.Sol;
    private int _playerId;
    private CoreVector2 _aimWorld;
    private string _shipName = string.Empty;
    private bool _playerGodMode;
    private float _systemTimeSeconds;

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

    public override void _Draw()
    {
        var size = GetViewportRect().Size;
        DrawBottomPanel(size);
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

        _shield.Position = new Vector2(meterX, panelTop + 6f);
        _armor.Position = new Vector2(meterX + CombatMeterWidth + CombatMeterGap, panelTop + 6f);
        _structure.Position = new Vector2(meterX + (CombatMeterWidth + CombatMeterGap) * 2f, panelTop + 6f);
        _speed.Position = new Vector2(leftX, panelTop + 35f);
        _coords.Position = new Vector2(leftX + 86f, panelTop + 35f);
        _mode.Position = new Vector2(centerX - 120f, panelTop + 7f);
        _ship.Position = new Vector2(rightX, panelTop + 13f);
        _weapon.Position = new Vector2(rightX, panelTop + 31f);
        var minimapLeft = size.X - MinimapWidth - MinimapMargin;
        _fps.Position = new Vector2(Math.Max(PanelPadding, minimapLeft - 76f), 8f);
        _godMode.Position = new Vector2(Math.Max(PanelPadding, minimapLeft - 128f), 24f);
        _systemName.Position = new Vector2(minimapLeft + 8f, MinimapMargin + MinimapHeight + 5f);

        var mode = ship.Mode == ShipMode.Combat ? "COMBAT" : "NAV";
        var swap = ship.ModeSwitchCooldown > 0f ? $"SWAP {ship.ModeSwitchCooldown:0.0}" : "SWAP RDY";
        _mode.AddThemeColorOverride("font_color", ship.Mode == ShipMode.Combat
            ? new Color(1f, 0.72f, 0.42f, 0.98f)
            : new Color(0.62f, 1f, 0.96f, 0.98f));
        _weapon.AddThemeColorOverride("font_color", ship.Mode == ShipMode.Combat
            ? new Color(0.72f, 1f, 0.9f, 0.98f)
            : new Color(0.72f, 0.9f, 1f, 0.78f));

        _speed.Text = $"SPD {ship.Velocity.Length(),3:0}";
        _coords.Text = $"X {ship.Position.X,5:0} Y {ship.Position.Y,5:0}";
        _mode.Text = $"{mode}  {swap}";
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

        DrawHudBlock(new Rect2(10f, y + 6f, StatusBlockWidth, 40f), new Color(0f, 0.08f, 0.12f, 0.28f));
        DrawHudBlock(new Rect2(size.X - RightBlockWidth - 10f, y + 6f, RightBlockWidth, 40f), new Color(0f, 0.08f, 0.12f, 0.26f));

        var slotsWidth = 7f * EquipmentSlotSize + 6f * EquipmentSlotGap;
        var slotsStart = size.X * 0.5f - slotsWidth * 0.5f;
        for (var i = 0; i < 7; i++)
        {
            var x = slotsStart + i * (EquipmentSlotSize + EquipmentSlotGap);
            DrawRect(new Rect2(x, y + 29f, EquipmentSlotSize, 14f), new Color(0.03f, 0.35f, 0.48f, 0.62f), true);
            DrawRect(new Rect2(x, y + 29f, EquipmentSlotSize, 14f), new Color(0.2f, 0.9f, 1f, 0.28f), false, 1f);
        }

        DrawCombatBars(y);
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
        var origin = new Vector2(20f, panelTop + 23f);
        DrawMeter(origin, CombatMeterWidth, ship.Combat.Shield, ship.Combat.MaxShield, new Color(0.20f, 0.82f, 1f, 0.94f));
        DrawMeter(origin + new Vector2(CombatMeterWidth + CombatMeterGap, 0f), CombatMeterWidth, ship.Combat.Armor, ship.Combat.MaxArmor, new Color(1f, 0.72f, 0.22f, 0.94f));
        DrawMeter(origin + new Vector2((CombatMeterWidth + CombatMeterGap) * 2f, 0f), CombatMeterWidth, ship.Combat.Structure, ship.Combat.MaxStructure, new Color(0.96f, 0.24f, 0.22f, 0.94f));
    }

    private void DrawMeter(Vector2 position, float width, float value, float maxValue, Color color)
    {
        var ratio = maxValue <= 0f ? 0f : Math.Clamp(value / maxValue, 0f, 1f);
        var rect = new Rect2(position, new Vector2(width, 5f));
        DrawRect(rect, new Color(0f, 0.04f, 0.06f, 0.82f), true);
        DrawRect(new Rect2(position, new Vector2(width * ratio, 5f)), color, true);
        DrawRect(rect, new Color(0.22f, 0.92f, 1f, 0.22f), false, 1f);
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

        DrawAsteroidsOnMinimap(center, scale, mapRect);

        foreach (var ship in _snapshot.Ships)
        {
            var local = new Vector2(ship.Position.X * scale, ship.Position.Y * scale);
            var position = center + local;
            if (!mapRect.HasPoint(position))
            {
                continue;
            }

            if (ship.Id == _playerId)
            {
                DrawPlayerVelocityVector(ship, position, scale, mapRect);
                DrawPlayerMinimapMarker(ship, position);
            }
            else
            {
                DrawCircle(position, 4.8f, new Color(1f, 0.34f, 0.20f, 0.22f));
                DrawCircle(position, 2.7f, new Color(1f, 0.42f, 0.28f, 0.95f));
            }
        }

        var aim = center + new Vector2(_aimWorld.X * scale, _aimWorld.Y * scale);
        if (mapRect.HasPoint(aim))
        {
            DrawCircle(aim, 3.1f, new Color(1f, 0.9f, 0.3f, 0.24f));
            DrawCircle(aim, 1.8f, new Color(1f, 0.9f, 0.3f, 0.95f));
        }
    }

    private void DrawAsteroidsOnMinimap(Vector2 center, float scale, Rect2 mapRect)
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

            DrawAsteroidTrajectory(center, scale, mapRect, asteroid);
        }

        foreach (var asteroid in _snapshot.Asteroids)
        {
            if (asteroid.IsDestroyed || !IsFinite(asteroid.Position))
            {
                continue;
            }

            var local = new Vector2(asteroid.Position.X * scale, asteroid.Position.Y * scale);
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

    private void DrawPlayerVelocityVector(ShipState ship, Vector2 position, float scale, Rect2 mapRect)
    {
        var velocity = new Vector2(ship.Velocity.X, ship.Velocity.Y);
        var speed = velocity.Length();
        if (speed < 20f)
        {
            return;
        }

        var direction = velocity / speed;
        var length = Math.Clamp(speed * scale * 2.75f, 8f, 30f);
        var end = ClampToRect(position + direction * length, mapRect.Grow(-5f));
        if (position.DistanceSquaredTo(end) < 12f)
        {
            return;
        }

        var tangent = new Vector2(-direction.Y, direction.X);
        DrawLine(position, end, new Color(0.18f, 0.10f, 0.02f, 0.70f), 3.4f, true);
        DrawLine(position, end, new Color(1f, 0.70f, 0.18f, 0.84f), 1.25f, true);
        DrawLine(position + direction * 2.2f, end, new Color(1f, 0.96f, 0.62f, 0.34f), 0.65f, true);

        _mapVelocityArrow[0] = end;
        _mapVelocityArrow[1] = end - direction * 5.8f + tangent * 3.2f;
        _mapVelocityArrow[2] = end - direction * 5.8f - tangent * 3.2f;
        DrawColoredPolygon(_mapVelocityArrow, new Color(1f, 0.74f, 0.22f, 0.90f));
    }

    private void DrawPlayerMinimapMarker(ShipState ship, Vector2 position)
    {
        var forward = new Vector2(MathF.Sin(ship.Rotation), -MathF.Cos(ship.Rotation));
        if (forward.LengthSquared() <= 0.001f)
        {
            forward = Vector2.Up;
        }
        else
        {
            forward = forward.Normalized();
        }

        var right = new Vector2(-forward.Y, forward.X);
        SetPlayerMarkerPoints(position, forward, right, 0.78f);
        DrawColoredPolygon(_mapPlayerMarker, new Color(0.20f, 0.105f, 0.015f, 0.92f));
        DrawPolyline(_mapPlayerMarker, new Color(1f, 0.76f, 0.22f, 0.96f), 1.05f, true);
        DrawLine(_mapPlayerMarker[^1], _mapPlayerMarker[0], new Color(1f, 0.76f, 0.22f, 0.96f), 1.05f, true);

        SetPlayerMarkerPoints(position, forward, right, 0.52f);
        DrawColoredPolygon(_mapPlayerMarker, new Color(1f, 0.56f, 0.08f, 0.94f));
        DrawLine(position - forward * 1.3f, position + forward * 3.5f, new Color(1f, 0.96f, 0.58f, 0.68f), 0.75f, true);
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

    private void DrawAsteroidTrajectory(Vector2 center, float scale, Rect2 mapRect, AsteroidState asteroid)
    {
        if (AsteroidPhysics.IsOutsideRemovalBounds(asteroid.Position, asteroid.Radius, _asteroidTrajectoryConfig)
            || AsteroidPhysics.IsInsideSunBurnZone(asteroid.Position, asteroid.Radius, _asteroidTrajectoryConfig))
        {
            return;
        }

        var position = asteroid.Position;
        var velocity = asteroid.Velocity;
        var previousWorld = position;
        var previousMap = center + new Vector2(position.X * scale, position.Y * scale);
        var previousInside = mapRect.HasPoint(previousMap);
        const int maxPredictionSteps = 760;
        const int drawEverySteps = 5;

        for (var step = 0; step < maxPredictionSteps; step++)
        {
            var distanceToSun = MathF.Max(1f, position.Length());
            var predictionDelta = Math.Clamp(distanceToSun / 7600f, 0.08f, 0.42f);
            velocity += AsteroidPhysics.SolarGravity(position, _asteroidTrajectoryConfig) * predictionDelta;
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

            if (TryBurnIntersection(previousWorld, position, asteroid.Radius, out var burnPoint))
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

            if (AsteroidPhysics.IsOutsideRemovalBounds(position, asteroid.Radius, _asteroidTrajectoryConfig)
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

    private void SetAsteroidMarkerPoints(Vector2 position, float radius, float rotation, int seed)
    {
        for (var index = 0; index < _mapAsteroidShape.Length; index++)
        {
            var angle = rotation + index / (float)_mapAsteroidShape.Length * MathF.Tau;
            var jitter = 0.76f + Hash01(seed * 0.013f + index * 9.37f) * 0.34f;
            _mapAsteroidShape[index] = position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * jitter;
        }
    }

    private static Vector2 ClampToRect(Vector2 point, Rect2 rect)
    {
        var max = rect.Position + rect.Size;
        return new Vector2(
            Math.Clamp(point.X, rect.Position.X, max.X),
            Math.Clamp(point.Y, rect.Position.Y, max.Y));
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
        label.AddThemeFontSizeOverride("font_size", 10);
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
