using Godot;
using SpaceManagers.Core;
using System.Collections;
using System.Runtime.CompilerServices;
using CoreVector2 = System.Numerics.Vector2;
using Directory = System.IO.Directory;
using IOPath = System.IO.Path;

namespace SpaceManagersPrototype;

public partial class GameRoot : Node2D
{
    private const double FixedDelta = 1.0 / SimulationConfig.TickRate;
    private static readonly bool UseCameraFollowSmoothing = false;
    private const float CameraFollowSharpness = 8.5f;
    private const int MaxDebugEnemySpawns = 120;
    private const double StressWarmupSeconds = 1.5;
    private const float DebugAsteroidSpawnDistance = 1250f;
    private const float DebugAsteroidExplosionDistance = 2600f;
    private const float WarpCalibrationSeconds = 12f;
    private const float WarpTransitSeconds = 3f;
    private const float TargetLockMaxDistance = 18000f;
    private const float TargetLockPickPadding = 28f;
    private const float TargetLockMissResetPadding = 96f;
    private static readonly CoreVector2 SystemArrivalPosition = new(1650f, -980f);
    private const float SystemArrivalRotation = -0.28f;

    private LocalSimulation _simulation = null!;
    private WorldSnapshot _previousSnapshot = null!;
    private WorldSnapshot _snapshot = null!;
    private StarSystemDefinition _currentSystem = SolarSystem.Sol;
    private IReadOnlyList<StarSystemIndexEntry> _generatedSystems = Array.Empty<StarSystemIndexEntry>();
    private int _generatedSystemIndex = -1;
    private bool _generatedSystemsLoaded;
    private SpaceBackground _background = null!;
    private ShipView _shipView = null!;
    private readonly Dictionary<int, ShipView> _enemyViews = new();
    private readonly Dictionary<int, float> _enemyExplosionRadii = new();
    private readonly Dictionary<int, Color> _enemyExplosionTints = new();
    private readonly Dictionary<int, ShipExplosionVisual> _enemyExplosionVisuals = new();
    private readonly Dictionary<int, int> _shipIdToPilotId = new();
    private readonly Dictionary<int, int> _pilotIdToShipId = new();
    private readonly Dictionary<int, string> _npcShipTexturePaths = new();
    private readonly HashSet<int> _activeEnemyIds = new();
    private readonly HashSet<int> _fullQualityEnemyIds = new();
    private readonly HashSet<int> _balancedQualityEnemyIds = new();
    private readonly HashSet<int> _statusBarEnemyIds = new();
    private readonly List<int> _enemyRemovalBuffer = new();
    private readonly List<EnemyDistance> _visibleEnemyDistances = new();
    private readonly List<EnemyDistance> _drawableEnemyDistances = new();
    private readonly HashSet<int> _handledAsteroidEventIds = new();
    private readonly HashSet<int> _handledProjectileImpactIds = new();
    private readonly SnapshotBuffer<ShipState> _visualShips = new();
    private readonly SnapshotBuffer<ProjectileState> _visualProjectiles = new();
    private readonly SnapshotBuffer<AsteroidState> _visualAsteroids = new();
    private ProjectileLayer _projectileLayer = null!;
    private ProjectileImpactLayer _projectileImpactLayer = null!;
    private WeaponRangeLayer _weaponRangeLayer = null!;
    private ExplosionLayer _explosionLayer = null!;
    private AsteroidLayer _asteroidLayer = null!;
    private AsteroidFireLayer _asteroidFireLayer = null!;
    private AsteroidDebrisLayer _asteroidDebrisLayer = null!;
    private DebugHitboxLayer _debugHitboxLayer = null!;
    private EnemyStatusLayer _enemyStatusLayer = null!;
    private TargetLockLayer _targetLockLayer = null!;
    private ReticleView _reticle = null!;
    private WarpTunnelLayer _warpTunnel = null!;
    private HudOverlay _hud = null!;
    private StarMapOverlay _starMap = null!;
    private StarMapToggleButton? _starMapButton;
    private GalaxyLifeSimulation? _galaxyLife;
    private Camera2D _camera = null!;
    private IReadOnlyList<string> _shipTexturePaths = Array.Empty<string>();
    private IReadOnlyList<string> _ordinaryShipTexturePaths = Array.Empty<string>();
    private IReadOnlyList<string> _klissanShipTexturePaths = Array.Empty<string>();
    private IReadOnlyList<EnginePort> _selectedShipExhaustPorts = Array.Empty<EnginePort>();
    private Texture2D? _selectedShipTexture;
    private ShipVisualProfile? _selectedShipProfile;
    private int _shipTextureIndex = -1;
    private int _ordinaryShipTextureIndex = -1;
    private int _klissanShipTextureIndex = -1;
    private string _selectedShipName = "Prototype";
    private int _lockedTargetShipId = -1;
    private string _warpTargetSystemId = string.Empty;
    private float _selectedShipVisualScale = ShipCatalog.DefaultVisualScale;
    private Color _selectedWarpOuterColor = new(0.08f, 0.85f, 1f, 1f);
    private Color _selectedWarpCoreColor = new(0.82f, 1f, 1f, 1f);
    private float _warpChargeSeconds;
    private bool _warpInTransit;
    private float _warpTransitElapsed;
    private bool _warpTransitSwitched;
    private StarSystemDefinition? _pendingWarpSystem;
    private int _pendingWarpGeneratedIndex = -1;
    private readonly Random _warpRandom = new(0x5A17B00);
    private bool _useKlissanShipGroup;
    private bool _playerDeathExplosionSpawned;
    private bool _showShipHitboxes;
    private bool _queuedModeToggle;
    private bool _stressModeActive;
    private bool _adjacentPreloadQueued;
    private bool _quitRequested;
    private int _adjacentPreloadDelayFrames;
    private double _stressElapsed;
    private double _stressPrintElapsed;
    private double _stressFpsSum;
    private double _stressMinFps = double.MaxValue;
    private double _stressMaxFrameMs;
    private double _stressQuitAfterSeconds;
    private double _stressNextSystemSwitchAt;
    private double _stressSystemSwitchInterval;
    private int _stressFrames;
    private int _stressSystemSwitchesRemaining;
    private bool _stressAutopilot;
    private int _captureQuitDelayFrames;
    private string _vfxCaptureDirectory = string.Empty;
    private double _vfxCaptureElapsed;
    private int _vfxCaptureIndex;
    private string _frameCaptureDirectory = string.Empty;
    private string _frameCapturePrefix = "frame";
    private double _frameCaptureElapsed;
    private int _frameCaptureIndex;
    private long _handledAsteroidEventTick = -1;
    private long _handledProjectileImpactTick = -1;
    private double _accumulator;
    private double _galaxyLifeAccumulator;
    private InputCommand _latestCommand;

    public override void _Ready()
    {
        System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency;
        ConfigureInputMap();
        Input.MouseMode = Input.MouseModeEnum.Hidden;

        _simulation = new LocalSimulation();
        LoadStartupSystemIfRequested();
        ApplyStarSystemPhysics(_currentSystem);
        ResetSimulationForActiveSystem(SystemArrivalPosition, SystemArrivalRotation);
        _snapshot = _simulation.CurrentSnapshot;
        _previousSnapshot = _snapshot;
        _latestCommand = InputCommand.Idle(CoreVector2.Zero);

        _background = new SpaceBackground { Bounds = _simulation.Bounds };
        _background.SetSystem(_currentSystem);
        AddChild(_background);
        PreloadAdjacentStarSystems();

        AddChild(new MusicController());

        ConfigureShipCatalogs();

        _asteroidLayer = new AsteroidLayer();
        AddChild(_asteroidLayer);

        _asteroidFireLayer = new AsteroidFireLayer();
        AddChild(_asteroidFireLayer);

        _projectileLayer = new ProjectileLayer();
        AddChild(_projectileLayer);

        _projectileImpactLayer = new ProjectileImpactLayer();
        AddChild(_projectileImpactLayer);

        _weaponRangeLayer = new WeaponRangeLayer();
        AddChild(_weaponRangeLayer);

        _asteroidDebrisLayer = new AsteroidDebrisLayer();
        AddChild(_asteroidDebrisLayer);

        _explosionLayer = new ExplosionLayer();
        AddChild(_explosionLayer);

        _debugHitboxLayer = new DebugHitboxLayer { Config = _simulation.Config };
        AddChild(_debugHitboxLayer);

        _enemyStatusLayer = new EnemyStatusLayer();
        AddChild(_enemyStatusLayer);

        _targetLockLayer = new TargetLockLayer();
        AddChild(_targetLockLayer);

        _shipView = new ShipView();
        _shipView.Scale = new Vector2(ShipCatalog.DefaultVisualScale, ShipCatalog.DefaultVisualScale);
        AddChild(_shipView);
        SelectShipTexture(ShipCatalog.IndexOfPreferred(_shipTexturePaths, "2PeopleR"));
        SelectStartupShipIfRequested();
        InitializeGalaxyLife();
        SyncGalaxyLifeForActiveSystem();

        _warpTunnel = new WarpTunnelLayer();
        AddChild(_warpTunnel);

        _reticle = new ReticleView();
        _reticle.Scale = new Vector2(0.55f, 0.55f);
        AddChild(_reticle);

        _camera = new Camera2D
        {
            Enabled = true,
            PositionSmoothingEnabled = false,
            Zoom = new Vector2(0.6f, 0.6f)
        };
        AddChild(_camera);

        var canvas = new CanvasLayer();
        _hud = new HudOverlay();
        _hud.SetSystem(_currentSystem);
        canvas.AddChild(_hud);
        _starMap = new StarMapOverlay();
        _starMap.CloseRequested += OnStarMapClosed;
        _starMap.ConfirmTargetRequested += TuneWarpTarget;
        _starMap.ResetTargetRequested += ResetWarpTarget;
        canvas.AddChild(_starMap);
        SyncStarMapData();
        LockFirstVisibleTargetIfRequested();
        AddChild(canvas);
        if (ReadBoolUserArg("--open-star-map"))
        {
            OpenStarMap();
        }

        RunStartupStressMode();
        RunAsteroidVfxSmokeTest();
        RunProjectileImpactVfxSmokeTest();
        RunShipVfxSmokeTest();
        RunWarpChargeSmokeTest();
        RunWarpVfxSmokeTest();
        ConfigureVfxCapture();
        ConfigureFrameCapture();
        UpdateVisuals(0.0, _snapshot, SnapshotTimeSeconds(_snapshot));
    }

    public override void _Process(double delta)
    {
        if (_quitRequested)
        {
            return;
        }

        RunQueuedAdjacentStarSystemPreload();
        UpdateDeferredCaptureQuit();

        if (!_warpInTransit && Input.IsActionJustPressed("star_map_toggle"))
        {
            ToggleStarMap();
        }

        if (Input.IsActionJustPressed("music_toggle"))
        {
            MusicController.ToggleMuted();
        }

        UpdateWarpDrive(delta);
        if (!_warpInTransit && Input.IsActionJustPressed("warp_jump"))
        {
            TryStartWarpTransit();
        }

        if (_warpInTransit)
        {
            if (Input.MouseMode != Input.MouseModeEnum.Hidden)
            {
                Input.MouseMode = Input.MouseModeEnum.Hidden;
            }

            var player = PlayerShipFrom(_snapshot);
            _latestCommand = InputCommand.Idle(player.Id == _simulation.PlayerShipId ? player.Position : CoreVector2.Zero);
            UpdateVisuals(0.0, _snapshot, SnapshotTimeSeconds(_snapshot));
            UpdateFrameCapture(delta);
            return;
        }

        if (_starMap.Visible)
        {
            if (Input.IsActionJustPressed("star_map_close"))
            {
                _starMap.Close();
            }

            if (Input.MouseMode != Input.MouseModeEnum.Visible)
            {
                Input.MouseMode = Input.MouseModeEnum.Visible;
            }

            var player = PlayerShipFrom(_snapshot);
            _latestCommand = InputCommand.Idle(player.Id == _simulation.PlayerShipId ? player.Position : CoreVector2.Zero);
            UpdateVisuals(0.0, _snapshot, SnapshotTimeSeconds(_snapshot));
            UpdateFrameCapture(delta);
            return;
        }

        if (Input.MouseMode != Input.MouseModeEnum.Hidden)
        {
            Input.MouseMode = Input.MouseModeEnum.Hidden;
        }

        if (UpdateStressTelemetry(delta))
        {
            return;
        }

        if (Input.IsActionJustPressed("ship_next_sprite"))
        {
            SelectShipTexture(_shipTextureIndex + 1);
        }

        if (Input.IsActionJustPressed("ship_toggle_catalog"))
        {
            ToggleShipCatalog();
        }

        if (Input.IsActionJustPressed("debug_toggle_ship_hitboxes"))
        {
            _showShipHitboxes = !_showShipHitboxes;
            _shipView.ShowHitbox = _showShipHitboxes;
            foreach (var enemyView in _enemyViews.Values)
            {
                enemyView.ShowHitbox = _showShipHitboxes;
            }

            _debugHitboxLayer.ShowHitboxes = _showShipHitboxes;
            _debugHitboxLayer.QueueRedraw();
        }

        if (Input.IsActionJustPressed("debug_spawn_enemy"))
        {
            SpawnDebugEnemy();
        }

        if (Input.IsActionJustPressed("debug_toggle_player_godmode"))
        {
            _simulation.PlayerGodMode = !_simulation.PlayerGodMode;
        }

        if (Input.IsActionJustPressed("debug_system_sol"))
        {
            SelectStarSystem(SolarSystem.Sol, -1);
        }

        if (Input.IsActionJustPressed("debug_system_next"))
        {
            SelectNextStarSystem();
        }

        if (Input.IsActionJustPressed("debug_spawn_asteroid"))
        {
            SpawnDebugAsteroid();
        }

        if (Input.IsActionJustPressed("debug_burst_asteroid"))
        {
            BurstDebugAsteroid();
        }

        if (Input.IsActionJustPressed("debug_self_destruct_player"))
        {
            DebugSelfDestructPlayer();
        }

        if (Input.IsActionJustPressed("debug_revive_player"))
        {
            DebugRevivePlayer();
        }

        if (Input.IsActionJustPressed("target_lock"))
        {
            HandleTargetLockClick();
        }

        if (Input.IsActionJustPressed("ship_toggle_mode"))
        {
            _queuedModeToggle = true;
        }

        _latestCommand = ReadInputCommand();
        StepGalaxyLife(delta);
        _accumulator += Math.Min(delta, 0.1);

        while (_accumulator >= FixedDelta)
        {
            _previousSnapshot = _snapshot;
            _snapshot = _simulation.Step(_latestCommand);
            HandleProjectileImpacts(_snapshot);
            if (_latestCommand.ToggleMode)
            {
                _queuedModeToggle = false;
                _latestCommand = _latestCommand with { ToggleMode = false };
            }

            _accumulator -= FixedDelta;
        }

        var blend = _snapshot.Tick == _previousSnapshot.Tick
            ? 1f
            : Math.Clamp((float)(_accumulator / FixedDelta), 0f, 1f);
        UpdateVisuals(delta, InterpolateSnapshot(_previousSnapshot, _snapshot, blend), InterpolatedTimeSeconds(_previousSnapshot, _snapshot, blend));
        UpdateVfxCapture(delta);
        UpdateFrameCapture(delta);
    }

    private InputCommand ReadInputCommand()
    {
        if (_stressAutopilot)
        {
            return ReadStressAutopilotCommand();
        }

        var aimWorld = GetGlobalMousePosition().ToCore();
        var forward = Input.IsActionPressed("ship_thrust") ? 1f : 0f;
        var reverse = Input.IsActionPressed("ship_reverse") ? 1f : 0f;
        var strafe = Input.GetActionStrength("ship_strafe_right") - Input.GetActionStrength("ship_strafe_left");
        var turn = Input.GetActionStrength("ship_turn_right") - Input.GetActionStrength("ship_turn_left");
        var fire = Input.IsActionPressed("weapon_fire");
        var afterburner = Input.IsActionPressed("ship_afterburner");

        return new InputCommand(forward, reverse, strafe, turn, aimWorld, fire, afterburner, _queuedModeToggle)
        {
            LockedTargetShipId = _lockedTargetShipId
        };
    }

    private void HandleTargetLockClick()
    {
        var player = PlayerShipFrom(_snapshot);
        if (player.Id != _simulation.PlayerShipId || player.IsDestroyed)
        {
            ClearTargetLock();
            return;
        }

        var clickWorld = GetGlobalMousePosition().ToCore();
        var visibleWorldRect = CameraWorldRect(_camera?.Position ?? player.Position.ToGodot(), 24f);
        var bestShipId = -1;
        var bestScore = float.MaxValue;
        var nearAnyShip = false;

        foreach (var ship in _snapshot.Ships)
        {
            if (ship.Id == _simulation.PlayerShipId || ship.IsDestroyed)
            {
                continue;
            }

            var center = ship.Hitbox.WorldCenter(ship.Position, ship.Rotation);
            if (!visibleWorldRect.HasPoint(center.ToGodot()))
            {
                continue;
            }

            var radius = Math.Clamp(ship.Hitbox.BoundingRadius, 30f, 190f);
            var distance = CoreVector2.Distance(clickWorld, center);
            if (distance <= radius + TargetLockMissResetPadding)
            {
                nearAnyShip = true;
            }

            if (!ship.Hitbox.ContainsWorldPoint(ship.Position, ship.Rotation, clickWorld)
                && distance > radius + TargetLockPickPadding)
            {
                continue;
            }

            var score = Math.Max(0f, distance - radius);
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestShipId = ship.Id;
        }

        if (bestShipId > 0)
        {
            SetTargetLock(bestShipId);
            return;
        }

        if (!nearAnyShip)
        {
            ClearTargetLock();
        }
    }

    private void SetTargetLock(int shipId)
    {
        if (shipId == _simulation.PlayerShipId)
        {
            return;
        }

        _lockedTargetShipId = shipId;
        _targetLockLayer?.QueueRedraw();
    }

    private void LockFirstVisibleTargetIfRequested()
    {
        if (!ReadBoolUserArg("--debug-target-lock-first"))
        {
            return;
        }

        var player = PlayerShipFrom(_snapshot);
        if (player.Id != _simulation.PlayerShipId)
        {
            return;
        }

        var visibleWorldRect = CameraWorldRect(player.Position.ToGodot(), 320f);
        var bestVisibleShipId = -1;
        var bestVisibleDistance = float.MaxValue;
        var bestFallbackShipId = -1;
        var bestFallbackDistance = float.MaxValue;

        foreach (var ship in _snapshot.Ships)
        {
            if (ship.Id == _simulation.PlayerShipId || ship.IsDestroyed)
            {
                continue;
            }

            var distance = CoreVector2.DistanceSquared(player.Position, ship.Position);
            if (distance < bestFallbackDistance)
            {
                bestFallbackDistance = distance;
                bestFallbackShipId = ship.Id;
            }

            var center = ship.Hitbox.WorldCenter(ship.Position, ship.Rotation).ToGodot();
            if (!visibleWorldRect.HasPoint(center) || distance >= bestVisibleDistance)
            {
                continue;
            }

            bestVisibleDistance = distance;
            bestVisibleShipId = ship.Id;
        }

        var targetId = bestVisibleShipId > 0 ? bestVisibleShipId : bestFallbackShipId;
        if (targetId <= 0)
        {
            return;
        }

        SetTargetLock(targetId);
        GD.Print($"Target lock diagnostic: ship {targetId}.");
    }

    private void ClearTargetLock()
    {
        _lockedTargetShipId = -1;
        _targetLockLayer?.ClearTarget();
        _hud?.SetTargetLockState(false, default, default, hostile: false);
    }

    private void UpdateTargetLockVisuals(WorldSnapshot visualSnapshot, ShipState player)
    {
        if (_lockedTargetShipId <= 0
            || player.Id != _simulation.PlayerShipId
            || player.IsDestroyed)
        {
            _targetLockLayer.ClearTarget();
            _hud.SetTargetLockState(false, default, player, hostile: false);
            return;
        }

        if (!TryFindShip(visualSnapshot.Ships, _lockedTargetShipId, out var target)
            || target.IsDestroyed)
        {
            ClearTargetLock();
            _hud.SetTargetLockState(false, default, player, hostile: false);
            return;
        }

        var distance = CoreVector2.Distance(player.Position, target.Position);
        if (distance > TargetLockMaxDistance)
        {
            ClearTargetLock();
            _hud.SetTargetLockState(false, default, player, hostile: false);
            return;
        }

        var hostile = IsHostileTarget(target);
        var color = TargetLockColor(target, hostile);
        _targetLockLayer.SetTarget(target, hostile, color);
        _hud.SetTargetLockState(true, target, player, hostile);
    }

    private static bool TryFindShip(IReadOnlyList<ShipState> ships, int shipId, out ShipState ship)
    {
        for (var index = 0; index < ships.Count; index++)
        {
            if (ships[index].Id == shipId)
            {
                ship = ships[index];
                return true;
            }
        }

        ship = default;
        return false;
    }

    private static bool IsHostileTarget(ShipState ship)
    {
        return ship.Role == ShipRole.Pirate;
    }

    private static Color TargetLockColor(ShipState target, bool hostile)
    {
        if (hostile)
        {
            return new Color(1f, 0.18f, 0.10f, 1f);
        }

        return target.Role switch
        {
            ShipRole.Military => new Color(0.78f, 0.95f, 1f, 1f),
            ShipRole.Ranger => new Color(0.66f, 1f, 0.96f, 1f),
            _ => new Color(0.76f, 0.94f, 1f, 1f)
        };
    }

    private void UpdateVisuals(double delta, WorldSnapshot visualSnapshot, float systemTimeSeconds)
    {
        var ship = PlayerShipFrom(visualSnapshot);
        var shipPosition = ship.Position.ToGodot();
        var playerDestroyed = ship.IsDestroyed;
        if (playerDestroyed && !_playerDeathExplosionSpawned)
        {
            SpawnPlayerDeathExplosion(ship, shipPosition);
            _playerDeathExplosionSpawned = true;
        }
        else if (!playerDestroyed)
        {
            _playerDeathExplosionSpawned = false;
        }

        var warpVisual = BuildWarpVisualState(shipPosition, ship.Rotation);
        _shipView.Position = warpVisual.ShipPosition;
        _shipView.Rotation = ship.Rotation;
        _shipView.Scale = warpVisual.ShipScale;
        _shipView.Modulate = new Color(1f, 1f, 1f, warpVisual.ShipAlpha);
        _shipView.Velocity = ship.Velocity.ToGodot();
        if (_warpTunnel is not null && _warpTunnel.Active && _warpInTransit)
        {
            _warpTunnel.Position = warpVisual.TunnelPosition;
            _warpTunnel.Rotation = warpVisual.TunnelRotation;
            _warpTunnel.MouthOffset = warpVisual.TunnelMouthOffset;
        }

        var aimDelta = _latestCommand.AimWorld.ToGodot() - shipPosition;
        var afterburnerActive = ship.Mode == ShipMode.Navigation && _latestCommand.Afterburner && _latestCommand.Reverse <= 0.01f;
        _shipView.AimDirection = aimDelta.LengthSquared() > 0.001f
            ? aimDelta.Normalized().Rotated(-ship.Rotation)
            : Vector2.Up;
        _shipView.ThrustLevel = MathF.Max(_latestCommand.Forward, afterburnerActive ? 1f : 0f);
        _shipView.ReverseLevel = _latestCommand.Reverse;
        _shipView.StrafeLevel = _latestCommand.Strafe;
        _shipView.AfterburnerLevel = afterburnerActive ? 1f : 0f;
        _shipView.IsFiring = ship.Mode == ShipMode.Combat && _latestCommand.Fire && ship.WeaponCooldown > 0f;
        _shipView.ShowHitbox = _showShipHitboxes;
        _shipView.Visible = !playerDestroyed;
        _shipView.SetProcess(!playerDestroyed || _showShipHitboxes);
        var visibleWorldRect = CameraWorldRect(shipPosition, 320f);
        var drawableWorldRect = CameraWorldRect(shipPosition, 1800f);
        _explosionLayer.VisibleWorldRect = drawableWorldRect;
        _explosionLayer.UseCulling = true;
        UpdateEnemyViews(visualSnapshot, shipPosition, visibleWorldRect, drawableWorldRect);

        _asteroidLayer.Asteroids = visualSnapshot.Asteroids;
        _asteroidLayer.VisibleWorldRect = drawableWorldRect;
        _asteroidLayer.UseCulling = true;
        _asteroidLayer.QueueRedraw();
        _debugHitboxLayer.Config = _simulation.Config;
        _debugHitboxLayer.Asteroids = visualSnapshot.Asteroids;
        _debugHitboxLayer.VisibleWorldRect = drawableWorldRect;
        _debugHitboxLayer.UseCulling = true;
        _debugHitboxLayer.ShowHitboxes = _showShipHitboxes;
        _debugHitboxLayer.QueueRedraw();
        _asteroidFireLayer.Asteroids = visualSnapshot.Asteroids;
        _asteroidFireLayer.VisibleWorldRect = drawableWorldRect;
        _asteroidFireLayer.UseCulling = true;
        HandleAsteroidEvents(_snapshot);

        _projectileLayer.Projectiles = visualSnapshot.Projectiles;
        _projectileLayer.VisibleWorldRect = drawableWorldRect;
        _projectileLayer.UseCulling = true;
        _projectileLayer.QueueRedraw();
        _projectileImpactLayer.VisibleWorldRect = drawableWorldRect;
        _projectileImpactLayer.UseCulling = true;
        _projectileImpactLayer.SetShieldTargets(visualSnapshot.Ships);
        HandleProjectileImpacts(_snapshot);
        _weaponRangeLayer.SetState(
            ship,
            _simulation.Config.PrimaryWeapon,
            !playerDestroyed && ship.Mode == ShipMode.Combat && !_warpInTransit && !_starMap.Visible);

        _reticle.Position = _latestCommand.AimWorld.ToGodot();
        _reticle.Mode = ship.Mode;

        var cameraPosition = shipPosition + WarpCameraOffset();
        var cameraZoom = WarpCameraZoom();
        if (UseCameraFollowSmoothing)
        {
            var cameraBlend = delta <= 0.0 ? 1f : 1f - MathF.Exp(-CameraFollowSharpness * (float)delta);
            _camera.Position = _camera.Position.Lerp(cameraPosition, cameraBlend);
        }
        else
        {
            _camera.Position = cameraPosition;
        }

        _camera.Zoom = new Vector2(cameraZoom, cameraZoom);
        _background.SetVisualTime(systemTimeSeconds);
        _enemyStatusLayer.SetState(visualSnapshot.Ships, _simulation.PlayerShipId, visibleWorldRect, _statusBarEnemyIds);
        UpdateTargetLockVisuals(visualSnapshot, ship);
        _hud.SetState(visualSnapshot, _simulation.PlayerShipId, _latestCommand.AimWorld, _selectedShipName, _simulation.PlayerGodMode, systemTimeSeconds);
    }

    private Vector2 WarpCameraOffset()
    {
        if (!_warpInTransit)
        {
            return Vector2.Zero;
        }

        var phase = CurrentWarpPhase();
        var impulse = WarpCameraImpulse(phase);
        var angle = _warpTransitElapsed * 23.0f;
        return new Vector2(MathF.Sin(angle * 1.31f), MathF.Cos(angle * 1.73f)) * (3.8f * impulse);
    }

    private float WarpCameraZoom()
    {
        const float baseZoom = 0.6f;
        if (!_warpInTransit)
        {
            return baseZoom;
        }

        return baseZoom * (1f + WarpCameraImpulse(CurrentWarpPhase()) * 0.026f);
    }

    private float CurrentWarpPhase()
    {
        var halfTime = Math.Max(0.001f, WarpTransitSeconds * 0.5f);
        var phaseElapsed = _warpTransitSwitched
            ? _warpTransitElapsed - halfTime
            : _warpTransitElapsed;
        return Math.Clamp(phaseElapsed / halfTime, 0f, 1f);
    }

    private static float WarpCameraImpulse(float phase)
    {
        var startHit = 1f - SmoothStep(0.00f, 0.22f, phase);
        var portalHit = SmoothStep(0.66f, 0.92f, phase) * (1f - SmoothStep(0.92f, 1.0f, phase));
        return Math.Clamp(Math.Max(startHit * 0.62f, portalHit), 0f, 1f);
    }

    private WarpVisualState BuildWarpVisualState(Vector2 shipPosition, float shipRotation)
    {
        var baseScale = new Vector2(_selectedShipVisualScale, _selectedShipVisualScale);
        var defaultState = new WarpVisualState(shipPosition, baseScale, 1f, shipPosition, new Vector2(0f, -540f), shipRotation);
        if (!_warpInTransit)
        {
            return defaultState;
        }

        var halfTime = Math.Max(0.001f, WarpTransitSeconds * 0.5f);
        var phaseElapsed = _warpTransitSwitched
            ? _warpTransitElapsed - halfTime
            : _warpTransitElapsed;
        var phase = Math.Clamp(phaseElapsed / halfTime, 0f, 1f);
        var forward = Vector2.Up.Rotated(shipRotation);
        var mouthOffset = new Vector2(0f, -540f);

        if (!_warpTransitSwitched)
        {
            var enter = SmoothStep(0.12f, 0.96f, phase);
            var pull = MathF.Pow(enter, 1.12f);
            var shipOffset = forward * (460f * pull);
            var scale = _selectedShipVisualScale * (1f - 0.42f * SmoothStep(0.40f, 1f, phase));
            var stretch = SmoothStep(0.18f, 0.78f, phase) * (1f - SmoothStep(0.84f, 1f, phase));
            var alpha = 1f - SmoothStep(0.72f, 1f, phase) * 0.92f;
            var tunnelReach = 520f + SmoothStep(0.42f, 1f, phase) * 70f;
            mouthOffset = new Vector2(0f, -tunnelReach);
            return new WarpVisualState(
                shipPosition + shipOffset,
                new Vector2(scale * (1f - stretch * 0.055f), scale * (1f + stretch * 0.18f)),
                alpha,
                shipPosition,
                mouthOffset,
                shipRotation);
        }

        var exit = SmoothStep(0.06f, 0.90f, phase);
        var shipStartOffset = -forward * (460f * (1f - exit));
        var exitScale = _selectedShipVisualScale * (0.58f + 0.42f * exit);
        var exitStretch = (1f - SmoothStep(0.62f, 1f, phase)) * SmoothStep(0.0f, 0.28f, phase);
        var exitAlpha = SmoothStep(0.02f, 0.30f, phase);
        mouthOffset = new Vector2(0f, -520f + SmoothStep(0.60f, 1f, phase) * 45f);
        return new WarpVisualState(
            shipPosition + shipStartOffset,
            new Vector2(exitScale * (1f - exitStretch * 0.045f), exitScale * (1f + exitStretch * 0.15f)),
            exitAlpha,
            shipPosition,
            mouthOffset,
            shipRotation + MathF.PI);
    }

    private void ToggleStarMap()
    {
        if (_starMap.Visible)
        {
            _starMap.Close();
            return;
        }

        OpenStarMap();
    }

    private void OpenStarMap()
    {
        SyncStarMapData();
        _starMap.Open();
        ApplyStarMapDebugArgs();
        if (_starMapButton is not null)
        {
            _starMapButton.Active = true;
            _starMapButton.QueueRedraw();
        }
    }

    private void OnStarMapClosed()
    {
        if (_starMapButton is not null)
        {
            _starMapButton.Active = false;
            _starMapButton.QueueRedraw();
        }
    }

    private void TuneWarpTarget(StarMapSystemEntry target)
    {
        var changed = !SameSystemId(_warpTargetSystemId, target.Id);
        _warpTargetSystemId = target.Id;
        if (changed)
        {
            ResetWarpChargeOnly();
        }

        _hud.SetWarpTarget(target.DisplayName);
        PreloadWarpTarget(target);
        UpdateWarpDriveVisualState(PlayerShipFrom(_snapshot));
        SyncStarMapData();
        GD.Print($"Warp engine tuned: {target.DisplayName} ({target.Id}).");
    }

    private void ResetWarpTarget()
    {
        _warpTargetSystemId = string.Empty;
        ResetWarpChargeOnly();
        _hud.SetWarpTarget(string.Empty);
        UpdateWarpDriveVisualState(PlayerShipFrom(_snapshot));
        SyncStarMapData();
        GD.Print("Warp engine target reset.");
    }

    private void SyncStarMapData()
    {
        if (_starMap is null)
        {
            return;
        }

        _starMap.SetSystems(BuildStarMapEntries(), _currentSystem.Id, _warpTargetSystemId);
    }

    private void ApplyStarMapDebugArgs()
    {
        var selectedSystem = ReadStringUserArg("--star-map-select", string.Empty);
        if (!string.IsNullOrWhiteSpace(selectedSystem))
        {
            _starMap.SelectSystemById(selectedSystem);
        }

        var inspectedSystem = ReadStringUserArg("--star-map-inspect", string.Empty);
        if (!string.IsNullOrWhiteSpace(inspectedSystem))
        {
            _starMap.ShowPlanetPopupForSystem(inspectedSystem);
        }
    }

    private void UpdateWarpDrive(double delta)
    {
        var player = PlayerShipFrom(_snapshot);
        if (_warpInTransit)
        {
            var warpDelta = Math.Min(delta, 0.05);
            UpdateWarpTransit(warpDelta);
            player = PlayerShipFrom(_snapshot);
            UpdateWarpDriveVisualState(player);
            return;
        }

        if (string.IsNullOrWhiteSpace(_warpTargetSystemId)
            || SameSystemId(_warpTargetSystemId, _currentSystem.Id)
            || _starMap.Visible
            || player.Id != _simulation.PlayerShipId
            || player.IsDestroyed)
        {
            if (!_starMap.Visible)
            {
                ResetWarpChargeOnly();
            }

            UpdateWarpDriveVisualState(player);
            return;
        }

        if (player.Mode == ShipMode.Combat)
        {
            ResetWarpChargeOnly();
            UpdateWarpDriveVisualState(player);
            return;
        }

        _warpChargeSeconds = Math.Min(WarpCalibrationSeconds, _warpChargeSeconds + Math.Max(0f, (float)delta));
        UpdateWarpDriveVisualState(player);
    }

    private void UpdateWarpTransit(double delta)
    {
        if (!_warpInTransit)
        {
            return;
        }

        _warpTransitElapsed += Math.Max(0f, (float)delta);
        var halfTime = WarpTransitSeconds * 0.5f;
        if (!_warpTransitSwitched && _warpTransitElapsed >= halfTime)
        {
            CompleteWarpSystemSwitch();
        }

        var phaseElapsed = _warpTransitSwitched
            ? _warpTransitElapsed - halfTime
            : _warpTransitElapsed;
        var phaseDuration = Math.Max(0.001f, halfTime);
        _warpTunnel.SetProgress(phaseElapsed / phaseDuration);

        if (_warpTransitElapsed >= WarpTransitSeconds)
        {
            FinishWarpTransit();
        }
    }

    private void TryStartWarpTransit()
    {
        if (_warpInTransit
            || _starMap.Visible
            || string.IsNullOrWhiteSpace(_warpTargetSystemId)
            || _warpChargeSeconds < WarpCalibrationSeconds - 0.001f)
        {
            return;
        }

        var player = PlayerShipFrom(_snapshot);
        if (player.Id != _simulation.PlayerShipId || player.Mode != ShipMode.Navigation || player.IsDestroyed)
        {
            ResetWarpChargeOnly();
            UpdateWarpDriveVisualState(player);
            return;
        }

        if (!TryResolveWarpTarget(out var targetSystem, out var generatedIndex))
        {
            ResetWarpTarget();
            return;
        }

        _pendingWarpSystem = targetSystem;
        _pendingWarpGeneratedIndex = generatedIndex;
        _warpInTransit = true;
        _warpTransitElapsed = 0f;
        _warpTransitSwitched = false;
        _starMap.Close();
        _warpTunnel.Start(_selectedWarpOuterColor, _selectedWarpCoreColor, arriving: false);
        _hud.SetWarpDriveState(1f, hasTarget: true, charging: false, ready: true, transit: true);
        GD.Print($"Warp jump started: {_currentSystem.DisplayName} -> {targetSystem.DisplayName}.");
    }

    private bool TryResolveWarpTarget(out StarSystemDefinition targetSystem, out int generatedIndex)
    {
        if (SameSystemId(_warpTargetSystemId, SolarSystem.Sol.Id))
        {
            targetSystem = SolarSystem.Sol;
            generatedIndex = -1;
            return true;
        }

        EnsureGeneratedSystemsLoaded();
        for (var index = 0; index < _generatedSystems.Count; index++)
        {
            var entry = _generatedSystems[index];
            if (!SameSystemId(entry.Id, _warpTargetSystemId))
            {
                continue;
            }

            var system = StarSystemLoader.LoadSystem(entry.File);
            if (system is null)
            {
                break;
            }

            targetSystem = system;
            generatedIndex = index;
            return true;
        }

        targetSystem = SolarSystem.Sol;
        generatedIndex = -1;
        return false;
    }

    private void CompleteWarpSystemSwitch()
    {
        if (_warpTransitSwitched || _pendingWarpSystem is null)
        {
            return;
        }

        var arrival = RandomWarpArrival();
        SelectStarSystem(_pendingWarpSystem, _pendingWarpGeneratedIndex, arrival.Position, arrival.Rotation);
        _warpTransitSwitched = true;
        _warpTunnel.Start(_selectedWarpOuterColor, _selectedWarpCoreColor, arriving: true);
        _warpTunnel.SetProgress(0f);
        GD.Print($"Warp arrival: {_currentSystem.DisplayName} at {arrival.Position.X:0}, {arrival.Position.Y:0}.");
    }

    private void FinishWarpTransit()
    {
        if (!_warpTransitSwitched)
        {
            CompleteWarpSystemSwitch();
        }

        var player = PlayerShipFrom(_snapshot);
        var residualWarpVisual = BuildWarpVisualState(player.Position.ToGodot(), player.Rotation);
        if (_warpTunnel is not null)
        {
            _warpTunnel.Position = residualWarpVisual.TunnelPosition;
            _warpTunnel.Rotation = residualWarpVisual.TunnelRotation;
            _warpTunnel.MouthOffset = residualWarpVisual.TunnelMouthOffset;
            _warpTunnel.BeginResidual(_selectedWarpOuterColor, _selectedWarpCoreColor, arriving: true, duration: 1.45f);
        }

        _warpInTransit = false;
        _warpTransitElapsed = 0f;
        _warpTransitSwitched = false;
        _pendingWarpSystem = null;
        _pendingWarpGeneratedIndex = -1;
        ResetWarpChargeOnly();
        UpdateWarpDriveVisualState(PlayerShipFrom(_snapshot));
    }

    private (CoreVector2 Position, float Rotation) RandomWarpArrival()
    {
        var bounds = _simulation.Bounds;
        var padding = MathF.Max(640f, CurrentPlayerHitbox().BoundingRadius + 320f);
        var side = _warpRandom.Next(4);
        var x = RandomRange(-bounds.HalfWidth + padding, bounds.HalfWidth - padding);
        var y = RandomRange(-bounds.HalfHeight + padding, bounds.HalfHeight - padding);
        var position = side switch
        {
            0 => new CoreVector2(x, -bounds.HalfHeight + padding),
            1 => new CoreVector2(bounds.HalfWidth - padding, y),
            2 => new CoreVector2(x, bounds.HalfHeight - padding),
            _ => new CoreVector2(-bounds.HalfWidth + padding, y)
        };
        var rotation = RotationFacing(position, CoreVector2.Zero);
        return (position, rotation);
    }

    private float RandomRange(float min, float max)
    {
        if (max <= min)
        {
            return min;
        }

        return min + (float)_warpRandom.NextDouble() * (max - min);
    }

    private void ResetWarpChargeOnly()
    {
        _warpChargeSeconds = 0f;
    }

    private void UpdateWarpDriveVisualState(ShipState player)
    {
        if (_hud is null || _shipView is null)
        {
            return;
        }

        var hasTarget = !string.IsNullOrWhiteSpace(_warpTargetSystemId)
            && !SameSystemId(_warpTargetSystemId, _currentSystem.Id);
        var chargeRatio = _warpInTransit
            ? 1f
            : Math.Clamp(_warpChargeSeconds / WarpCalibrationSeconds, 0f, 1f);
        var charging = hasTarget
            && !_warpInTransit
            && !_starMap.Visible
            && player.Id == _simulation.PlayerShipId
            && !player.IsDestroyed
            && player.Mode == ShipMode.Navigation
            && chargeRatio < 1f;
        var ready = hasTarget && chargeRatio >= 1f && !_warpInTransit && !_starMap.Visible;
        _hud.SetWarpDriveState(chargeRatio, hasTarget, charging, ready, _warpInTransit);
        _shipView.WarpChargeLevel = hasTarget ? chargeRatio : 0f;
        _shipView.WarpChargeActive = charging || ready;
        _shipView.WarpTransitLevel = _warpInTransit ? 1f : 0f;
    }

    private IReadOnlyList<StarMapSystemEntry> BuildStarMapEntries()
    {
        var fixtureSectors = ReadIntUserArg("--star-map-fixture-sectors", 0);
        if (fixtureSectors > 0)
        {
            return BuildStarMapFixtureEntries(
                fixtureSectors,
                ReadIntUserArg("--star-map-fixture-systems-per-sector", 5));
        }

        EnsureGeneratedSystemsLoaded();
        var entries = new List<StarMapSystemEntry>
        {
            new(
                SolarSystem.Sol.Id,
                SolarSystem.Sol.DisplayName,
                SolarSystem.Sol.SectorId,
                SolarSystem.Sol.SectorName,
                SolarSystem.Sol.Star.Archetype,
                SolarSystem.Sol.Star.DisplayName,
                SolarSystem.Sol.Star.MapColor,
                SolarSystem.Sol.Star.WorldSize,
                SolarSystem.Sol.Star.CoronaIntensity,
                SolarSystem.Sol.Star.AnimationSpeed,
                SolarSystem.Sol.Planets.Count,
                BuildStarMapPlanets(SolarSystem.Sol.Planets),
                "preset",
                string.Empty)
        };

        foreach (var entry in _generatedSystems)
        {
            if (string.Equals(entry.Id, SolarSystem.Sol.Id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var system = string.IsNullOrWhiteSpace(entry.File) ? null : StarSystemLoader.LoadSystem(entry.File);
            entries.Add(new StarMapSystemEntry(
                string.IsNullOrWhiteSpace(entry.Id) ? system?.Id ?? entry.DisplayName : entry.Id,
                system?.DisplayName ?? entry.DisplayName,
                FirstNonEmpty(entry.SectorId, system?.SectorId, "unknown"),
                FirstNonEmpty(entry.SectorName, system?.SectorName, "Unknown"),
                FirstNonEmpty(entry.StarArchetype, system?.Star.Archetype, "unknown_star"),
                FirstNonEmpty(system?.Star.DisplayName, HumanizeToken(entry.StarArchetype), "Unknown Star"),
                system?.Star.MapColor ?? StarMapColorForArchetype(entry.StarArchetype),
                system?.Star.WorldSize ?? SolarSystem.SunVisualWorldSize,
                system?.Star.CoronaIntensity ?? 1f,
                system?.Star.AnimationSpeed ?? 1f,
                system?.Planets.Count ?? entry.PlanetCount,
                system is null ? Array.Empty<StarMapPlanetEntry>() : BuildStarMapPlanets(system.Planets),
                entry.Source,
                entry.File,
                entry.ParsecPosition));
        }

        return entries;
    }

    private IReadOnlyList<StarMapSystemEntry> BuildStarMapFixtureEntries(int sectorCount, int systemsPerSector)
    {
        sectorCount = Math.Clamp(sectorCount, 1, 64);
        systemsPerSector = Math.Clamp(systemsPerSector, 1, 18);
        var sectorNames = new[]
        {
            "Orion", "Taron", "Vaalgir", "Reges", "Ayla", "Hilia", "Mergac", "Denwer",
            "Onika", "Tamuto", "Nordal", "Kastor", "Gurt", "Faara", "Kondur", "Sevek"
        };
        var starArchetypes = new[]
        {
            "yellow_main_sequence", "red_dwarf", "orange_dwarf", "blue_white_star",
            "white_dwarf", "red_giant", "amber_giant", "violet_anomaly", "green_exotic", "neutron_like"
        };

        var entries = new List<StarMapSystemEntry>(sectorCount * systemsPerSector);
        for (var sectorIndex = 0; sectorIndex < sectorCount; sectorIndex++)
        {
            var sectorId = $"fixture_{sectorIndex + 1:00}";
            var sectorName = sectorIndex < sectorNames.Length
                ? sectorNames[sectorIndex]
                : $"Sector {sectorIndex + 1:00}";
            for (var systemIndex = 0; systemIndex < systemsPerSector; systemIndex++)
            {
                var isCurrent = sectorIndex == 0 && systemIndex == 0;
                var archetype = starArchetypes[(sectorIndex * 3 + systemIndex) % starArchetypes.Length];
                var entryArchetype = isCurrent ? _currentSystem.Star.Archetype : archetype;
                entries.Add(new StarMapSystemEntry(
                    isCurrent ? _currentSystem.Id : $"{sectorId}_{systemIndex + 1:000}",
                    isCurrent ? _currentSystem.DisplayName : $"{sectorName} {systemIndex + 1:00}",
                    sectorId,
                    sectorName,
                    entryArchetype,
                    HumanizeToken(entryArchetype),
                    isCurrent ? _currentSystem.Star.MapColor : StarMapColorForArchetype(entryArchetype),
                    isCurrent ? _currentSystem.Star.WorldSize : SolarSystem.SunVisualWorldSize * (0.82f + ((sectorIndex + systemIndex) % 5) * 0.12f),
                    isCurrent ? _currentSystem.Star.CoronaIntensity : 0.72f + ((sectorIndex + systemIndex) % 6) * 0.07f,
                    isCurrent ? _currentSystem.Star.AnimationSpeed : 0.74f + ((sectorIndex * 2 + systemIndex) % 7) * 0.06f,
                    isCurrent ? _currentSystem.Planets.Count : 1 + (sectorIndex * 7 + systemIndex * 5) % 12,
                    isCurrent
                        ? BuildStarMapPlanets(_currentSystem.Planets)
                        : BuildFixturePlanets(sectorIndex, systemIndex, 1 + (sectorIndex * 7 + systemIndex * 5) % 12),
                    isCurrent ? "current" : "fixture",
                    string.Empty,
                    FixtureParsecPosition(sectorIndex, systemIndex, sectorCount)));
            }
        }

        return entries;
    }

    private static Vector2 FixtureParsecPosition(int sectorIndex, int systemIndex, int sectorCount)
    {
        var columns = Math.Clamp((int)MathF.Round(MathF.Sqrt(Math.Max(1, sectorCount) * 2.2f)), 1, Math.Max(1, sectorCount));
        var sectorColumn = sectorIndex % columns;
        var sectorRow = sectorIndex / columns;
        var origin = new Vector2(sectorColumn * 52f, sectorRow * 52f);
        var radius = 0;
        var remaining = systemIndex;
        while (remaining > 0)
        {
            radius++;
            remaining -= radius * 6;
        }

        if (systemIndex == 0)
        {
            return origin;
        }

        var ringStart = 1;
        for (var r = 1; r < radius; r++)
        {
            ringStart += r * 6;
        }

        var offset = systemIndex - ringStart;
        var axial = new Vector2I(radius, 0);
        var directions = new[]
        {
            new Vector2I(0, 1),
            new Vector2I(-1, 1),
            new Vector2I(-1, 0),
            new Vector2I(0, -1),
            new Vector2I(1, -1),
            new Vector2I(1, 0)
        };

        foreach (var direction in directions)
        {
            for (var step = 0; step < radius; step++)
            {
                if (offset <= 0)
                {
                    return origin + AxialToParsec(axial);
                }

                axial += direction;
                offset--;
            }
        }

        return origin + AxialToParsec(axial);
    }

    private static Vector2 AxialToParsec(Vector2I axial)
    {
        const float step = 10f;
        return new Vector2(
            step * (axial.X + axial.Y * 0.5f),
            step * MathF.Sqrt(3f) * 0.5f * axial.Y);
    }

    private static IReadOnlyList<StarMapPlanetEntry> BuildStarMapPlanets(IReadOnlyList<PlanetDefinition> planets)
    {
        return planets
            .Select(planet => new StarMapPlanetEntry(
                planet.DisplayName,
                PlanetArchetypeName(planet),
                planet.MapColor,
                planet.OrbitRadius,
                planet.ReferenceTextureWorldSize,
                planet.Visual?.Rings is not null))
            .ToArray();
    }

    private static IReadOnlyList<StarMapPlanetEntry> BuildFixturePlanets(int sectorIndex, int systemIndex, int count)
    {
        var archetypes = new[]
        {
            "scorched_rock", "barren_rock", "desert", "volcanic", "ocean", "earthlike",
            "ice", "toxic", "warm_gas_giant", "cold_gas_giant", "ringed_giant", "shattered_world"
        };
        var result = new StarMapPlanetEntry[count];
        for (var i = 0; i < result.Length; i++)
        {
            var archetype = archetypes[(sectorIndex * 5 + systemIndex * 3 + i) % archetypes.Length];
            result[i] = new StarMapPlanetEntry(
                $"{HumanizeToken(archetype)} {i + 1}",
                HumanizeToken(archetype),
                PlanetMapColorForArchetype(archetype),
                1200f + i * 520f,
                180f + ((sectorIndex + systemIndex + i) % 6) * 48f,
                archetype.Contains("ring", StringComparison.OrdinalIgnoreCase));
        }

        return result;
    }

    private static string PlanetArchetypeName(PlanetDefinition planet)
    {
        var id = planet.Id;
        if (id.StartsWith("planet_showcase_", StringComparison.OrdinalIgnoreCase))
        {
            return HumanizeToken(id["planet_showcase_".Length..]);
        }

        return HumanizeToken(id);
    }

    private static string HumanizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            token.Split('_', '-', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));
    }

    private static Color PlanetMapColorForArchetype(string archetype)
    {
        return archetype switch
        {
            "scorched_rock" => new Color(0.88f, 0.46f, 0.23f, 1f),
            "barren_rock" => new Color(0.63f, 0.61f, 0.56f, 1f),
            "desert" => new Color(0.92f, 0.62f, 0.28f, 1f),
            "volcanic" => new Color(1f, 0.34f, 0.16f, 1f),
            "ocean" => new Color(0.18f, 0.58f, 0.95f, 1f),
            "earthlike" => new Color(0.26f, 0.68f, 1f, 1f),
            "ice" => new Color(0.70f, 0.93f, 1f, 1f),
            "toxic" => new Color(0.55f, 0.92f, 0.28f, 1f),
            "warm_gas_giant" => new Color(0.95f, 0.63f, 0.35f, 1f),
            "cold_gas_giant" => new Color(0.52f, 0.72f, 1f, 1f),
            "ringed_giant" => new Color(0.90f, 0.75f, 0.48f, 1f),
            "shattered_world" => new Color(0.75f, 0.55f, 0.45f, 1f),
            _ => new Color(0.72f, 0.9f, 1f, 1f)
        };
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static bool SameSystemId(string left, string right)
    {
        return !string.IsNullOrWhiteSpace(left)
            && !string.IsNullOrWhiteSpace(right)
            && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static Color StarMapColorForArchetype(string archetype)
    {
        return archetype switch
        {
            "red_dwarf" or "red_giant" => new Color(1f, 0.28f, 0.12f, 1f),
            "orange_dwarf" or "amber_giant" => new Color(1f, 0.62f, 0.16f, 1f),
            "blue_white_star" => new Color(0.56f, 0.82f, 1f, 1f),
            "white_dwarf" => new Color(0.92f, 0.96f, 1f, 1f),
            "violet_anomaly" => new Color(0.82f, 0.36f, 1f, 1f),
            "green_exotic" => new Color(0.30f, 1f, 0.68f, 1f),
            "neutron_like" => new Color(0.72f, 0.96f, 1f, 1f),
            _ => new Color(1f, 0.72f, 0.18f, 1f)
        };
    }

    private void HandleAsteroidEvents(WorldSnapshot snapshot)
    {
        if (snapshot.Tick != _handledAsteroidEventTick)
        {
            _handledAsteroidEventTick = snapshot.Tick;
            _handledAsteroidEventIds.Clear();
        }

        foreach (var asteroidEvent in snapshot.AsteroidEvents)
        {
            if (!_handledAsteroidEventIds.Add(asteroidEvent.Id))
            {
                continue;
            }

            _asteroidDebrisLayer.Spawn(asteroidEvent);
            _asteroidFireLayer.SpawnBurnBurst(asteroidEvent);
        }
    }

    private void HandleProjectileImpacts(WorldSnapshot snapshot)
    {
        if (snapshot.Tick != _handledProjectileImpactTick)
        {
            _handledProjectileImpactTick = snapshot.Tick;
            _handledProjectileImpactIds.Clear();
        }

        foreach (var impact in snapshot.ProjectileImpacts)
        {
            if (!_handledProjectileImpactIds.Add(impact.Id))
            {
                continue;
            }

            _projectileImpactLayer.Spawn(impact);
        }
    }

    private void SelectNextStarSystem()
    {
        EnsureGeneratedSystemsLoaded();
        if (_generatedSystems.Count == 0)
        {
            GD.Print("Generated star systems are not available.");
            return;
        }

        if (string.Equals(_currentSystem.Id, SolarSystem.Sol.Id, StringComparison.OrdinalIgnoreCase))
        {
            SelectGeneratedSystemByIndex(0);
            return;
        }

        if (_generatedSystemIndex >= 0 && _generatedSystemIndex + 1 < _generatedSystems.Count)
        {
            SelectGeneratedSystemByIndex(_generatedSystemIndex + 1);
            return;
        }

        SelectStarSystem(SolarSystem.Sol, -1);
    }

    private void SelectGeneratedSystemByIndex(int index)
    {
        if ((uint)index >= (uint)_generatedSystems.Count)
        {
            return;
        }

        var entry = _generatedSystems[index];
        var system = StarSystemLoader.LoadSystem(entry.File);
        if (system is null)
        {
            GD.Print($"Generated star system '{entry.DisplayName}' could not be loaded.");
            return;
        }

        SelectStarSystem(system, index);
    }

    private void EnsureGeneratedSystemsLoaded()
    {
        if (_generatedSystemsLoaded)
        {
            return;
        }

        _generatedSystems = StarSystemLoader.LoadGalaxyIndex();
        _generatedSystemsLoaded = true;
        if (_generatedSystemIndex < 0 && !string.Equals(_currentSystem.Id, SolarSystem.Sol.Id, StringComparison.OrdinalIgnoreCase))
        {
            for (var index = 0; index < _generatedSystems.Count; index++)
            {
                if (string.Equals(_generatedSystems[index].Id, _currentSystem.Id, StringComparison.OrdinalIgnoreCase))
                {
                    _generatedSystemIndex = index;
                    break;
                }
            }
        }
    }

    private void InitializeGalaxyLife()
    {
        EnsureGeneratedSystemsLoaded();
        var systems = new List<GalaxyLifeSystemRef>
        {
            new(SolarSystem.Sol.Id, SolarSystem.Sol.DisplayName, SolarSystem.Sol.SectorId)
        };

        foreach (var entry in _generatedSystems)
        {
            if (string.Equals(entry.Id, SolarSystem.Sol.Id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            systems.Add(new GalaxyLifeSystemRef(
                entry.Id,
                string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.Id : entry.DisplayName,
                entry.SectorId));
        }

        _galaxyLife = new GalaxyLifeSimulation(systems);
        GD.Print($"Galaxy life: {_galaxyLife.Pilots.Count} persistent pilots across {systems.Count} systems.");
    }

    private void StepGalaxyLife(double delta)
    {
        if (_galaxyLife is null)
        {
            return;
        }

        _galaxyLifeAccumulator += Math.Min(delta, 0.25);
        if (_galaxyLifeAccumulator < 1.0)
        {
            return;
        }

        var stepSeconds = (float)Math.Min(_galaxyLifeAccumulator, 4.0);
        _galaxyLifeAccumulator = 0.0;
        _galaxyLife.Step(stepSeconds, _currentSystem.Id);
        SyncGalaxyLifeForActiveSystem();
    }

    private void SyncGalaxyLifeForActiveSystem()
    {
        if (_galaxyLife is null || _shipView is null)
        {
            return;
        }

        foreach (var pilot in _galaxyLife.ActivePilotsForSystem(_currentSystem.Id))
        {
            if (_pilotIdToShipId.ContainsKey(pilot.PilotId))
            {
                continue;
            }

            SpawnGalaxyPilot(pilot);
        }

        _snapshot = _simulation.CurrentSnapshot;
        _previousSnapshot = _snapshot;
    }

    private void SpawnGalaxyPilot(GalaxyPilotState pilot)
    {
        var path = ResolveNpcShipTexturePath(pilot);
        var texture = ShipCatalog.LoadTexture(path);
        if (texture is null)
        {
            return;
        }

        var profile = ShipCatalog.VisualProfileForPath(path, texture);
        var hitbox = new ShipHitbox(
            profile.HitboxLocalCenter.ToCore() * profile.Scale,
            profile.HitboxLocalSize.ToCore() * profile.Scale);
        var boundaryRadius = Math.Max(_simulation.Config.ShipRadius, hitbox.BoundingRadius);
        var position = SpawnPositionForPilot(pilot, boundaryRadius);
        var rotation = RotationFacing(position, CoreVector2.Zero);
        var shipId = _simulation.SpawnNpcShip(position, rotation, hitbox, pilot.Role, pilot.Name, pilot.ShipAssetId);
        var view = CreateShipView(path, texture, profile);

        view.Position = position.ToGodot();
        view.Rotation = rotation;
        AddChild(view);
        _enemyViews[shipId] = view;
        _shipIdToPilotId[shipId] = pilot.PilotId;
        _pilotIdToShipId[pilot.PilotId] = shipId;
        _npcShipTexturePaths[shipId] = path;

        var explosionRadius = Math.Clamp(hitbox.BoundingRadius * 2.35f, 84f, 220f);
        var explosionTint = RoleTint(pilot.Role, ShipCatalog.ThrustOuterColor(path));
        _enemyExplosionRadii[shipId] = explosionRadius;
        _enemyExplosionTints[shipId] = explosionTint;
        _enemyExplosionVisuals[shipId] = new ShipExplosionVisual(
            texture,
            profile.Scale,
            profile.ContentBounds,
            ShipCatalog.ExhaustPortsForPath(path),
            explosionRadius,
            explosionTint);
    }

    private string ResolveNpcShipTexturePath(GalaxyPilotState pilot)
    {
        var preferred = FindShipTexturePath(pilot.ShipAssetId);
        if (ResourceLoader.Exists(preferred))
        {
            return preferred;
        }

        var fallback = pilot.Role switch
        {
            ShipRole.Trader => "2PeopleT",
            ShipRole.Diplomat => "2PeopleD",
            ShipRole.Ranger => "2PeopleR",
            ShipRole.Military => "2PeopleW",
            ShipRole.Pirate => "2PeopleP",
            _ => "2PeopleR"
        };
        return FindShipTexturePath(fallback);
    }

    private CoreVector2 SpawnPositionForPilot(GalaxyPilotState pilot, float boundaryRadius)
    {
        var angle = PositiveHash01(pilot.Seed) * MathF.Tau;
        var radius = 2200f + PositiveHash01(pilot.Seed * 31 + pilot.PilotId) * 7600f;
        var position = new CoreVector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        return ClampToPlayerGrid(position, boundaryRadius + 240f);
    }

    private static Color RoleTint(ShipRole role, Color fallback)
    {
        return role switch
        {
            ShipRole.Trader => new Color(0.35f, 0.95f, 0.55f, 1f),
            ShipRole.Diplomat => new Color(0.70f, 0.82f, 1f, 1f),
            ShipRole.Ranger => new Color(0.20f, 0.86f, 1f, 1f),
            ShipRole.Military => new Color(1f, 0.74f, 0.22f, 1f),
            ShipRole.Pirate => new Color(1f, 0.20f, 0.12f, 1f),
            _ => fallback
        };
    }

    private static float PositiveHash01(int value)
    {
        unchecked
        {
            var x = (uint)value;
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            x ^= x >> 16;
            return (x & 0x00ffffff) / 16777215f;
        }
    }

    private void SelectStarSystem(
        StarSystemDefinition system,
        int generatedIndex,
        CoreVector2? arrivalPosition = null,
        float? arrivalRotation = null)
    {
        if (string.Equals(_currentSystem.Id, system.Id, StringComparison.OrdinalIgnoreCase)
            && _generatedSystemIndex == generatedIndex)
        {
            return;
        }

        _currentSystem = system;
        _generatedSystemIndex = generatedIndex;
        _warpTargetSystemId = string.Empty;
        ClearTargetLock();
        ResetWarpChargeOnly();
        ApplyStarSystemPhysics(system);
        ResetSimulationForActiveSystem(arrivalPosition ?? SystemArrivalPosition, arrivalRotation ?? SystemArrivalRotation);
        ClearTransientVisualState();
        SyncGalaxyLifeForActiveSystem();
        _background.SetSystem(system);
        _hud.SetSystem(system);
        _hud.SetWarpTarget(string.Empty);
        UpdateWarpDriveVisualState(PlayerShipFrom(_snapshot));
        SyncStarMapData();
        UpdateVisuals(0.0, _snapshot, SnapshotTimeSeconds(_snapshot));
        QueueAdjacentStarSystemPreload();
        GD.Print($"Star system: {system.DisplayName} ({system.Star.DisplayName}, {system.Planets.Count} planets, background {system.Background.DisplayName}).");
    }

    private void PreloadWarpTarget(StarMapSystemEntry target)
    {
        if (_background is null)
        {
            return;
        }

        if (SameSystemId(target.Id, SolarSystem.Sol.Id))
        {
            _background.PreloadSystemResources(SolarSystem.Sol);
            return;
        }

        if (!string.IsNullOrWhiteSpace(target.File) && StarSystemLoader.LoadSystem(target.File) is { } directSystem)
        {
            _background.PreloadSystemResources(directSystem);
            return;
        }

        EnsureGeneratedSystemsLoaded();
        for (var index = 0; index < _generatedSystems.Count; index++)
        {
            var entry = _generatedSystems[index];
            if (!SameSystemId(entry.Id, target.Id) || string.IsNullOrWhiteSpace(entry.File))
            {
                continue;
            }

            if (StarSystemLoader.LoadSystem(entry.File) is { } system)
            {
                _background.PreloadSystemResources(system);
            }

            return;
        }
    }

    private void PreloadAdjacentStarSystems()
    {
        if (_background is null)
        {
            return;
        }

        _background.PreloadSystemResources(_currentSystem);
        if (string.Equals(_currentSystem.Id, SolarSystem.Sol.Id, StringComparison.OrdinalIgnoreCase))
        {
            PreloadGeneratedSystemResources(0);
            return;
        }

        if (_generatedSystemIndex >= 0)
        {
            PreloadGeneratedSystemResources(_generatedSystemIndex + 1);
        }
        else
        {
            _background.PreloadSystemResources(SolarSystem.Sol);
        }
    }

    private void QueueAdjacentStarSystemPreload(int delayFrames = 1)
    {
        _adjacentPreloadQueued = true;
        _adjacentPreloadDelayFrames = Math.Max(_adjacentPreloadDelayFrames, Math.Max(0, delayFrames));
    }

    private void RunQueuedAdjacentStarSystemPreload()
    {
        if (!_adjacentPreloadQueued)
        {
            return;
        }

        if (_adjacentPreloadDelayFrames > 0)
        {
            _adjacentPreloadDelayFrames--;
            return;
        }

        _adjacentPreloadQueued = false;
        PreloadAdjacentStarSystems();
    }

    private void PreloadGeneratedSystemResources(int index)
    {
        if (_background is null)
        {
            return;
        }

        EnsureGeneratedSystemsLoaded();
        if ((uint)index >= (uint)_generatedSystems.Count)
        {
            _background.PreloadSystemResources(SolarSystem.Sol);
            return;
        }

        var entry = _generatedSystems[index];
        if (!string.IsNullOrWhiteSpace(entry.File) && StarSystemLoader.LoadSystem(entry.File) is { } system)
        {
            _background.PreloadSystemResources(system);
        }
    }

    private void ApplyStarSystemPhysics(StarSystemDefinition system)
    {
        _simulation.Config.StarVisualWorldSize = MathF.Max(1f, system.Star.WorldSize);
    }

    private void ResetSimulationForActiveSystem(CoreVector2 position, float rotation)
    {
        var hitbox = CurrentPlayerHitbox();
        _simulation.ResetPlayerShip(new ShipState(
            _simulation.PlayerShipId,
            position,
            CoreVector2.Zero,
            rotation,
            _simulation.Config.MaxEnergy,
            0f,
            hitbox,
            CombatStats.Default,
            ShipMode.Navigation,
            0f));
        _simulation.SeedAsteroids(_simulation.Config.AsteroidInitialActiveCount);
        _snapshot = _simulation.CurrentSnapshot;
        _previousSnapshot = _snapshot;
        _latestCommand = InputCommand.Idle(position);
        _accumulator = 0.0;
        _handledAsteroidEventTick = -1;
        _handledProjectileImpactTick = -1;
        _handledAsteroidEventIds.Clear();
        _handledProjectileImpactIds.Clear();
        _playerDeathExplosionSpawned = false;
    }

    private ShipHitbox CurrentPlayerHitbox()
    {
        if (_snapshot is not null)
        {
            var player = _snapshot.Ships.FirstOrDefault(ship => ship.Id == _simulation.PlayerShipId);
            if (player.Id == _simulation.PlayerShipId)
            {
                return player.Hitbox;
            }
        }

        return ShipHitbox.Default;
    }

    private void ClearTransientVisualState()
    {
        ClearTargetLock();
        ClearEnemyViews();
        _explosionLayer.ClearEffects();
        _projectileImpactLayer.ClearEffects();
        _asteroidFireLayer.ClearEffects();
        _asteroidDebrisLayer.ClearEffects();
        _projectileLayer.Projectiles = Array.Empty<ProjectileState>();
        _asteroidLayer.Asteroids = Array.Empty<AsteroidState>();
        _asteroidFireLayer.Asteroids = Array.Empty<AsteroidState>();
        _debugHitboxLayer.Asteroids = Array.Empty<AsteroidState>();
        _enemyStatusLayer.SetState(Array.Empty<ShipState>(), _simulation.PlayerShipId, default, _statusBarEnemyIds);
    }

    private void ClearEnemyViews()
    {
        ClearTargetLock();
        foreach (var view in _enemyViews.Values)
        {
            view.QueueFree();
        }

        _enemyViews.Clear();
        _enemyExplosionRadii.Clear();
        _enemyExplosionTints.Clear();
        _enemyExplosionVisuals.Clear();
        _shipIdToPilotId.Clear();
        _pilotIdToShipId.Clear();
        _npcShipTexturePaths.Clear();
        _enemyRemovalBuffer.Clear();
        _activeEnemyIds.Clear();
        _fullQualityEnemyIds.Clear();
        _balancedQualityEnemyIds.Clear();
        _statusBarEnemyIds.Clear();
        _visibleEnemyDistances.Clear();
        _drawableEnemyDistances.Clear();
    }

    private void LoadStartupSystemIfRequested()
    {
        var requested = ReadStringUserArg("--system", string.Empty);
        if (string.IsNullOrWhiteSpace(requested))
        {
            return;
        }

        if (string.Equals(requested, "sol", StringComparison.OrdinalIgnoreCase))
        {
            _currentSystem = SolarSystem.Sol;
            _generatedSystemIndex = -1;
            return;
        }

        TryLoadStartupGeneratedSystem(requested);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void TryLoadStartupGeneratedSystem(string requested)
    {
        var directPath = DirectSystemPath(requested);
        if (!string.IsNullOrWhiteSpace(directPath))
        {
            var directSystem = StarSystemLoader.LoadSystem(directPath);
            if (directSystem is not null)
            {
                _currentSystem = directSystem;
                _generatedSystemIndex = -1;
                GD.Print($"Startup star system: {directSystem.DisplayName}.");
                return;
            }
        }

        EnsureGeneratedSystemsLoaded();
        for (var index = 0; index < _generatedSystems.Count; index++)
        {
            var entry = _generatedSystems[index];
            if (!string.Equals(entry.Id, requested, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(entry.DisplayName, requested, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var system = StarSystemLoader.LoadSystem(entry.File);
            if (system is null)
            {
                return;
            }

            _currentSystem = system;
            _generatedSystemIndex = index;
            GD.Print($"Startup star system: {system.DisplayName}.");
            return;
        }

        GD.Print($"Startup star system '{requested}' was not found; using Sol.");
    }

    private static string DirectSystemPath(string requested)
    {
        var value = requested.Trim();
        if (value.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return value.StartsWith("res://", StringComparison.OrdinalIgnoreCase)
                ? value
                : $"res://assets/generated/systems/{value}";
        }

        return IsGeneratedSystemId(value) && value.Contains('_')
            ? $"res://assets/generated/systems/{value}.json"
            : string.Empty;
    }

    private static bool IsGeneratedSystemId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('/') || value.Contains('\\') || value.Contains('.'))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character) && character != '_' && character != '-')
            {
                return false;
            }
        }

        return true;
    }

    private void SpawnDebugAsteroid()
    {
        var player = _snapshot.Ships.FirstOrDefault(ship => ship.Id == _simulation.PlayerShipId);
        if (player.Id != _simulation.PlayerShipId)
        {
            return;
        }

        var aimDirection = _latestCommand.AimWorld - player.Position;
        if (aimDirection.LengthSquared() < 0.0001f)
        {
            aimDirection = SimulationMath.ForwardFromRotation(player.Rotation);
        }
        else
        {
            aimDirection = CoreVector2.Normalize(aimDirection);
        }

        var spawnPosition = player.Position + aimDirection * DebugAsteroidSpawnDistance;
        var radius = AsteroidPhysics.ReferenceDiameterToWorld(_simulation.Config.AsteroidMaxReferenceDiameter) * 0.5f;
        spawnPosition = ClampToPlayerGrid(spawnPosition, radius + 80f);

        var toPlayer = player.Position - spawnPosition;
        var direction = toPlayer.LengthSquared() < 0.0001f
            ? -aimDirection
            : CoreVector2.Normalize(toPlayer);
        var asteroidId = _simulation.SpawnAsteroid(
            spawnPosition,
            direction * _simulation.Config.AsteroidMinSpeed,
            _simulation.Config.AsteroidMaxReferenceDiameter,
            _simulation.Config.AsteroidMaxStructure,
            variant: 0);

        if (asteroidId < 0)
        {
            GD.Print($"Debug asteroid limit reached: {_simulation.Config.AsteroidMaxActiveCount}");
        }
    }

    private void BurstDebugAsteroid()
    {
        var player = _snapshot.Ships.FirstOrDefault(ship => ship.Id == _simulation.PlayerShipId);
        if (player.Id != _simulation.PlayerShipId)
        {
            return;
        }

        if (!_simulation.TryDestroyNearestAsteroid(player.Position, DebugAsteroidExplosionDistance, AsteroidEventType.RockExplosion, out var asteroidEvent))
        {
            var aimDirection = _latestCommand.AimWorld - player.Position;
            if (aimDirection.LengthSquared() < 0.0001f)
            {
                aimDirection = SimulationMath.ForwardFromRotation(player.Rotation);
            }
            else
            {
                aimDirection = CoreVector2.Normalize(aimDirection);
            }

            var radius = AsteroidPhysics.ReferenceDiameterToWorld(_simulation.Config.AsteroidMaxReferenceDiameter) * 0.5f;
            var spawnPosition = ClampToPlayerGrid(player.Position + aimDirection * 820f, radius + 80f);
            var asteroidId = _simulation.SpawnAsteroid(
                spawnPosition,
                CoreVector2.Zero,
                _simulation.Config.AsteroidMaxReferenceDiameter,
                _simulation.Config.AsteroidMaxStructure,
                variant: 0);
            if (asteroidId < 0 || !_simulation.TryDestroyNearestAsteroid(spawnPosition, radius + 240f, AsteroidEventType.RockExplosion, out asteroidEvent))
            {
                GD.Print("Debug asteroid burst failed: active asteroid limit reached.");
                return;
            }
        }

        _asteroidDebrisLayer.Spawn(asteroidEvent);
        _asteroidFireLayer.SpawnBurnBurst(asteroidEvent);
        _snapshot = _simulation.CurrentSnapshot;
        _previousSnapshot = _snapshot;
        _handledAsteroidEventTick = _snapshot.Tick;
        _handledAsteroidEventIds.Add(asteroidEvent.Id);
    }

    private void SpawnDebugEnemy(bool ignoreDebugLimit = false)
    {
        if (!ignoreDebugLimit && _enemyViews.Count >= MaxDebugEnemySpawns)
        {
            GD.Print($"Debug enemy limit reached: {MaxDebugEnemySpawns}");
            return;
        }

        var path = FindShipTexturePath("2PeopleP");
        var texture = ShipCatalog.LoadTexture(path);
        if (texture is null)
        {
            return;
        }

        var player = _snapshot.Ships.First(s => s.Id == _simulation.PlayerShipId);
        var profile = ShipCatalog.VisualProfileForPath(path, texture);
        var hitbox = new ShipHitbox(
            profile.HitboxLocalCenter.ToCore() * profile.Scale,
            profile.HitboxLocalSize.ToCore() * profile.Scale);
        var spawnIndex = _enemyViews.Count + 1;
        var angle = 0.52f + spawnIndex * 1.618f;
        var offset = new CoreVector2(MathF.Cos(angle), MathF.Sin(angle)) * 860f;
        var boundaryRadius = Math.Max(_simulation.Config.ShipRadius, hitbox.BoundingRadius);
        var position = ClampToPlayerGrid(player.Position + offset, boundaryRadius);
        var rotation = RotationFacing(position, player.Position);
        var enemyId = _simulation.SpawnEnemyShip(position, rotation, hitbox);
        var enemyView = CreateShipView(path, texture, profile);

        enemyView.Position = position.ToGodot();
        enemyView.Rotation = rotation;
        AddChild(enemyView);
        _enemyViews[enemyId] = enemyView;
        var explosionRadius = Math.Clamp(hitbox.BoundingRadius * 2.35f, 84f, 190f);
        var explosionTint = ShipCatalog.ThrustOuterColor(path);
        _enemyExplosionRadii[enemyId] = explosionRadius;
        _enemyExplosionTints[enemyId] = explosionTint;
        _enemyExplosionVisuals[enemyId] = new ShipExplosionVisual(
            texture,
            profile.Scale,
            profile.ContentBounds,
            ShipCatalog.ExhaustPortsForPath(path),
            explosionRadius,
            explosionTint);

        _snapshot = _simulation.CurrentSnapshot;
        _previousSnapshot = _snapshot;
    }

    private CoreVector2 ClampToPlayerGrid(CoreVector2 position, float padding)
    {
        var player = _snapshot.Ships.FirstOrDefault(ship => ship.Id == _simulation.PlayerShipId);
        var referencePosition = player.Id == _simulation.PlayerShipId
            ? player.Position
            : position;
        var cell = WorldGrid.CellAt(referencePosition, _simulation.Bounds);
        var origin = WorldGrid.CellOrigin(cell, _simulation.Bounds);
        return new CoreVector2(
            Math.Clamp(position.X, origin.X - _simulation.Bounds.HalfWidth + padding, origin.X + _simulation.Bounds.HalfWidth - padding),
            Math.Clamp(position.Y, origin.Y - _simulation.Bounds.HalfHeight + padding, origin.Y + _simulation.Bounds.HalfHeight - padding));
    }

    private void SpawnPlayerDeathExplosion(ShipState ship, Vector2 shipPosition)
    {
        var profile = _selectedShipProfile;
        var radius = Math.Clamp(ship.Hitbox.BoundingRadius * 3.15f, 150f, 310f);
        _explosionLayer.SpawnShip(
            shipPosition,
            radius,
            _shipView.EngineOuterColor,
            ShipExplosionKind.Player,
            _selectedShipTexture,
            profile?.Scale ?? Math.Max(_shipView.Scale.X, 0.05f),
            ship.Rotation,
            profile?.ContentBounds ?? default,
            _selectedShipExhaustPorts);
    }

    private void DebugSelfDestructPlayer()
    {
        var player = _snapshot.Ships.FirstOrDefault(ship => ship.Id == _simulation.PlayerShipId);
        if (player.Id != _simulation.PlayerShipId || player.IsDestroyed)
        {
            return;
        }

        var restoreGodMode = _simulation.PlayerGodMode;
        _simulation.PlayerGodMode = false;
        _simulation.ApplyDamageToShip(_simulation.PlayerShipId, 1_000_000f);
        _simulation.PlayerGodMode = restoreGodMode;
        _snapshot = _simulation.CurrentSnapshot;
        _previousSnapshot = _snapshot;
        UpdateVisuals(0.0, _snapshot, SnapshotTimeSeconds(_snapshot));
        GD.Print("Debug F9: player self-destructed.");
    }

    private void DebugRevivePlayer()
    {
        var player = _snapshot.Ships.FirstOrDefault(ship => ship.Id == _simulation.PlayerShipId);
        if (player.Id != _simulation.PlayerShipId)
        {
            return;
        }

        _simulation.RevivePlayerShip(player.Position, player.Rotation);
        _snapshot = _simulation.CurrentSnapshot;
        _previousSnapshot = _snapshot;
        _latestCommand = InputCommand.Idle(player.Position);
        _playerDeathExplosionSpawned = false;
        _shipView.Visible = true;
        _shipView.SetProcess(true);
        UpdateVisuals(0.0, _snapshot, SnapshotTimeSeconds(_snapshot));
        GD.Print("Debug F10: player revived.");
    }

    private void RunStartupStressMode()
    {
        var enemyCount = ReadIntUserArg("--stress-enemies", 0);
        var switchCount = ReadIntUserArg("--stress-system-switches", 0);
        _stressAutopilot = ReadBoolUserArg("--stress-autopilot");
        if (enemyCount <= 0 && switchCount <= 0 && !_stressAutopilot)
        {
            PlaceStressPlayerIfRequested();
            return;
        }

        _simulation.PlayerGodMode = true;
        _stressModeActive = true;
        _stressQuitAfterSeconds = ReadFloatUserArg("--stress-seconds", 0f);
        _stressSystemSwitchesRemaining = Math.Max(0, switchCount);
        _stressSystemSwitchInterval = Math.Max(0.35, ReadFloatUserArg("--stress-system-switch-interval", 0.85f));
        _stressNextSystemSwitchAt = _stressSystemSwitchInterval;
        PlaceStressPlayerIfRequested();
        enemyCount = Math.Clamp(enemyCount, 0, 300);
        for (var i = 0; i < enemyCount; i++)
        {
            SpawnDebugEnemy(ignoreDebugLimit: true);
        }

        GD.Print($"Stress mode: spawned {enemyCount} enemies, queued {_stressSystemSwitchesRemaining} system switches, player godmode enabled.");
    }

    private void RunAsteroidVfxSmokeTest()
    {
        if (!ReadBoolUserArg("--stress-asteroid-vfx"))
        {
            return;
        }

        var player = _snapshot.Ships.First(ship => ship.Id == _simulation.PlayerShipId);
        var basePosition = player.Position + new CoreVector2(720f, -180f);
        var radius = AsteroidPhysics.ReferenceDiameterToWorld(_simulation.Config.AsteroidMaxReferenceDiameter) * 0.5f;
        var events = new[]
        {
            new AsteroidEventState(900001, AsteroidEventType.RockExplosion, basePosition, radius, 0, 77101, 0.25f, 0.16f),
            new AsteroidEventState(900002, AsteroidEventType.SunBurn, basePosition + new CoreVector2(300f, 180f), radius * 0.92f, 13, 77147, -0.55f, 1.0f),
            new AsteroidEventState(900003, AsteroidEventType.ShipImpact, basePosition + new CoreVector2(-320f, 220f), radius * 0.72f, 24, 77213, 1.15f, 0.42f),
            new AsteroidEventState(900004, AsteroidEventType.RockExplosion, basePosition + new CoreVector2(420f, -210f), radius * 0.62f, 30, 77311, -1.25f, 0.28f)
        };

        foreach (var asteroidEvent in events)
        {
            _asteroidDebrisLayer.Spawn(asteroidEvent);
            _asteroidFireLayer.SpawnBurnBurst(asteroidEvent);
        }

        GD.Print("Stress asteroid VFX: spawned sample rock, sunburn, and impact effects.");
    }

    private void RunProjectileImpactVfxSmokeTest()
    {
        if (!ReadBoolUserArg("--stress-projectile-impact-vfx"))
        {
            return;
        }

        var player = _snapshot.Ships.First(ship => ship.Id == _simulation.PlayerShipId);
        var basePosition = player.Position + new CoreVector2(120f, -120f);
        var direction = new CoreVector2(1f, -0.12f);
        direction = CoreVector2.Normalize(direction);
        var playerShieldCenter = player.Hitbox.WorldCenter(player.Position, player.Rotation);
        var playerShieldRadius = Math.Max(_simulation.Config.ShipRadius, player.Hitbox.BoundingRadius);
        var lowShieldCenter = basePosition + new CoreVector2(185f, 104f) + direction * 64f;
        var lowShieldSize = new CoreVector2(118f, 88f);
        var weapon = _simulation.Config.PrimaryWeapon;
        var impactDamage = weapon.Damage;
        var impactSpeed = weapon.ProjectileSpeed;
        var impacts = new[]
        {
            new ProjectileImpactState(910001, player.Id, ProjectileImpactSurface.Shield, playerShieldCenter - direction * playerShieldRadius, direction, playerShieldCenter, playerShieldRadius, player.Hitbox.Size, player.Rotation, 0.92f, impactDamage, impactSpeed, ProjectileImpactKind.Projectile, 88101),
            new ProjectileImpactState(910002, 910002, ProjectileImpactSurface.Shield, lowShieldCenter + direction * 76f, -direction, lowShieldCenter, 76f, lowShieldSize, -0.32f, 0.10f, impactDamage, impactSpeed, ProjectileImpactKind.Projectile, 88149),
            new ProjectileImpactState(910003, 0, ProjectileImpactSurface.Armor, basePosition + new CoreVector2(355f, -18f), -direction, basePosition + new CoreVector2(355f, -18f), 54f, new CoreVector2(92f, 74f), 0.15f, 0f, impactDamage, impactSpeed, ProjectileImpactKind.Projectile, 88197),
            new ProjectileImpactState(910004, 0, ProjectileImpactSurface.Structure, basePosition + new CoreVector2(455f, 104f), new CoreVector2(-0.35f, 0.94f), basePosition + new CoreVector2(455f, 104f), 54f, new CoreVector2(88f, 78f), -0.45f, 0f, impactDamage, impactSpeed, ProjectileImpactKind.Projectile, 88243),
            new ProjectileImpactState(910005, 0, ProjectileImpactSurface.Asteroid, basePosition + new CoreVector2(570f, -72f), new CoreVector2(-0.88f, -0.24f), basePosition + new CoreVector2(570f, -72f), 86f, new CoreVector2(172f, 172f), 0f, 0f, impactDamage, impactSpeed, ProjectileImpactKind.Projectile, 88297)
        };

        foreach (var impact in impacts)
        {
            _projectileImpactLayer.Spawn(impact);
        }

        GD.Print("Stress projectile impact VFX: spawned shield, armor, structure, and asteroid hit samples.");
    }

    private void RunShipVfxSmokeTest()
    {
        if (!ReadBoolUserArg("--stress-ship-vfx"))
        {
            return;
        }

        var player = _snapshot.Ships.First(ship => ship.Id == _simulation.PlayerShipId);
        var samples = new[]
        {
            new ShipVfxSample(_shipTextureIndex >= 0 ? _shipTexturePaths[_shipTextureIndex] : FindShipTexturePath("2PeopleR"), ShipExplosionKind.Player, new Vector2(-520f, -190f), 0.0f),
            new ShipVfxSample(FindShipTexturePath("2PeopleP"), ShipExplosionKind.Enemy, new Vector2(120f, -210f), 0.35f),
            new ShipVfxSample(FindShipTexturePath("2FeiR"), ShipExplosionKind.Enemy, new Vector2(650f, -110f), -0.45f),
            new ShipVfxSample(FindShipTexturePath("2MalocP"), ShipExplosionKind.Enemy, new Vector2(-40f, 330f), 0.95f)
        };

        foreach (var sample in samples)
        {
            var texture = ShipCatalog.LoadTexture(sample.Path);
            if (texture is null)
            {
                continue;
            }

            var profile = ShipCatalog.VisualProfileForPath(sample.Path, texture);
            var hitbox = new ShipHitbox(
                profile.HitboxLocalCenter.ToCore() * profile.Scale,
                profile.HitboxLocalSize.ToCore() * profile.Scale);
            var radius = Math.Clamp(
                hitbox.BoundingRadius * (sample.Kind == ShipExplosionKind.Player ? 3.15f : 2.35f),
                sample.Kind == ShipExplosionKind.Player ? 150f : 84f,
                sample.Kind == ShipExplosionKind.Player ? 310f : 190f);
            _explosionLayer.SpawnShip(
                player.Position.ToGodot() + sample.Offset,
                radius,
                ShipCatalog.ThrustOuterColor(sample.Path),
                sample.Kind,
                texture,
                profile.Scale,
                sample.Rotation,
                profile.ContentBounds,
                ShipCatalog.ExhaustPortsForPath(sample.Path));
        }

        GD.Print("Stress ship VFX: spawned per-ship cutout explosions for player and NPC samples.");
    }

    private void RunWarpChargeSmokeTest()
    {
        if (!ReadBoolUserArg("--warp-charge-smoke"))
        {
            return;
        }

        var targetSystem = FindAlternateWarpTargetForSmoke();
        if (targetSystem is null)
        {
            GD.Print("Warp charge smoke skipped: no alternate star system is available.");
            return;
        }

        _warpTargetSystemId = targetSystem.Id;
        _warpChargeSeconds = WarpCalibrationSeconds;
        _hud.SetWarpTarget(targetSystem.DisplayName);
        UpdateWarpDriveVisualState(PlayerShipFrom(_snapshot));
        GD.Print($"Warp charge smoke: tuned to {targetSystem.DisplayName}.");
    }

    private void RunWarpVfxSmokeTest()
    {
        if (!ReadBoolUserArg("--warp-vfx-smoke"))
        {
            return;
        }

        var targetSystem = FindAlternateWarpTargetForSmoke();
        if (targetSystem is null)
        {
            GD.Print("Warp VFX smoke skipped: no alternate star system is available.");
            return;
        }

        _warpTargetSystemId = targetSystem.Id;
        _warpChargeSeconds = WarpCalibrationSeconds;
        TryStartWarpTransit();
    }

    private StarSystemDefinition? FindAlternateWarpTargetForSmoke()
    {
        EnsureGeneratedSystemsLoaded();
        for (var index = 0; index < _generatedSystems.Count; index++)
        {
            var entry = _generatedSystems[index];
            if (SameSystemId(entry.Id, _currentSystem.Id))
            {
                continue;
            }

            return StarSystemLoader.LoadSystem(entry.File);
        }

        if (!SameSystemId(_currentSystem.Id, SolarSystem.Sol.Id))
        {
            return SolarSystem.Sol;
        }

        return null;
    }

    private void PlaceStressPlayerIfRequested()
    {
        var nearId = ReadStringUserArg("--stress-near", string.Empty);
        if (string.IsNullOrWhiteSpace(nearId))
        {
            return;
        }

        if (string.Equals(nearId, "star", StringComparison.OrdinalIgnoreCase)
            || string.Equals(nearId, "sun", StringComparison.OrdinalIgnoreCase))
        {
            PlacePlayerNearStar();
            return;
        }

        var planet = _currentSystem.Planets.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, nearId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.DisplayName, nearId, StringComparison.OrdinalIgnoreCase));
        if (planet is null)
        {
            GD.Print($"Stress mode: unknown planet '{nearId}', keeping default player position.");
            return;
        }

        var currentPlayer = _simulation.CurrentSnapshot.Ships.First(ship => ship.Id == _simulation.PlayerShipId);
        var planetPosition = SolarSystem.PositionAt(planet, 0f).ToCore();
        var minimumOffsetDistance = planet.BodyRadius + 180f;
        var preferredOffsetDistance = planet.BodyRadius + 290f;
        var maximumOffsetDistance = Math.Max(620f, preferredOffsetDistance);
        var offsetDistance = Math.Clamp(preferredOffsetDistance, minimumOffsetDistance, maximumOffsetDistance);
        var position = _simulation.Bounds.Clamp(
            planetPosition + new CoreVector2(0f, -offsetDistance),
            Math.Max(_simulation.Config.ShipRadius, currentPlayer.Hitbox.BoundingRadius));
        currentPlayer.Position = position;
        currentPlayer.Velocity = CoreVector2.Zero;
        currentPlayer.Rotation = RotationFacing(position, planetPosition);
        currentPlayer.Mode = ShipMode.Navigation;
        currentPlayer.ModeSwitchCooldown = 0f;
        currentPlayer.WeaponCooldown = 0f;

        _simulation.ResetPlayerShip(currentPlayer);
        _snapshot = _simulation.CurrentSnapshot;
        _previousSnapshot = _snapshot;
        _latestCommand = InputCommand.Idle(position);
        GD.Print($"Stress mode: player placed near {planet.DisplayName}.");
    }

    private void PlacePlayerNearStar()
    {
        var currentPlayer = _simulation.CurrentSnapshot.Ships.First(ship => ship.Id == _simulation.PlayerShipId);
        var offsetDistance = Math.Max(_currentSystem.Star.WorldSize * 0.82f, _simulation.Config.ShipRadius + currentPlayer.Hitbox.BoundingRadius + 460f);
        var position = _simulation.Bounds.Clamp(
            new CoreVector2(offsetDistance, -offsetDistance * 0.16f),
            Math.Max(_simulation.Config.ShipRadius, currentPlayer.Hitbox.BoundingRadius));
        currentPlayer.Position = position;
        currentPlayer.Velocity = CoreVector2.Zero;
        currentPlayer.Rotation = RotationFacing(position, CoreVector2.Zero);
        currentPlayer.Mode = ShipMode.Navigation;
        currentPlayer.ModeSwitchCooldown = 0f;
        currentPlayer.WeaponCooldown = 0f;

        _simulation.ResetPlayerShip(currentPlayer);
        _simulation.SeedAsteroids(_simulation.Config.AsteroidInitialActiveCount);
        _snapshot = _simulation.CurrentSnapshot;
        _previousSnapshot = _snapshot;
        _latestCommand = InputCommand.Idle(position);
        GD.Print($"Stress mode: player placed near {_currentSystem.Star.DisplayName}.");
    }

    private InputCommand ReadStressAutopilotCommand()
    {
        var player = _snapshot.Ships.FirstOrDefault(ship => ship.Id == _simulation.PlayerShipId);
        var t = (float)_stressElapsed;
        var aim = player.Position + new CoreVector2(MathF.Sin(t * 1.15f) * 950f, -1100f + MathF.Cos(t * 0.7f) * 420f);
        var strafe = MathF.Sin(t * 2.35f) * 0.92f;
        var turn = MathF.Sin(t * 1.45f) * 0.74f + MathF.Sin(t * 0.52f) * 0.28f;
        return new InputCommand(1f, 0f, strafe, turn, aim, false, true, false);
    }

    private bool UpdateStressTelemetry(double delta)
    {
        if (!_stressModeActive)
        {
            return false;
        }

        _stressElapsed += delta;
        _stressPrintElapsed += delta;
        UpdateStressSystemSwitches();
        if (_stressElapsed >= StressWarmupSeconds)
        {
            var fps = Engine.GetFramesPerSecond();
            if (fps > 0)
            {
                _stressFrames++;
                _stressFpsSum += fps;
                _stressMinFps = Math.Min(_stressMinFps, fps);
            }

            _stressMaxFrameMs = Math.Max(_stressMaxFrameMs, delta * 1000.0);
        }

        if (_stressPrintElapsed < 2.0)
        {
            if (_stressQuitAfterSeconds > 0.0 && _stressElapsed >= _stressQuitAfterSeconds)
            {
                PrintStressSummary("final");
                _quitRequested = true;
                GetTree().Quit();
                return true;
            }

            return false;
        }

        _stressPrintElapsed = 0.0;
        PrintStressSummary("sample");
        if (_stressQuitAfterSeconds > 0.0 && _stressElapsed >= _stressQuitAfterSeconds)
        {
            PrintStressSummary("final");
            _quitRequested = true;
            GetTree().Quit();
            return true;
        }

        return false;
    }

    private void PrintStressSummary(string label)
    {
        var average = _stressFrames > 0 ? _stressFpsSum / _stressFrames : 0.0;
        var min = _stressMinFps < double.MaxValue ? _stressMinFps : 0.0;
        var enemies = Math.Max(0, _snapshot.Ships.Count - 1);
        GD.Print($"Stress perf ({label}): t={_stressElapsed:0.0}s system={_currentSystem.Id} avg_fps={average:0.0} min_fps={min:0.0} max_frame_ms={_stressMaxFrameMs:0.0} enemies={enemies} projectiles={_snapshot.Projectiles.Count} asteroids={_snapshot.Asteroids.Count}");
    }

    private void UpdateStressSystemSwitches()
    {
        while (_stressSystemSwitchesRemaining > 0 && _stressElapsed >= _stressNextSystemSwitchAt)
        {
            SelectNextStarSystem();
            _stressSystemSwitchesRemaining--;
            _stressNextSystemSwitchAt += _stressSystemSwitchInterval;
        }
    }

    private static int ReadIntUserArg(string name, int fallback)
    {
        var args = OS.GetCmdlineUserArgs();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(arg[(name.Length + 1)..], out var value) ? value : fallback;
            }

            if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)
                && i + 1 < args.Length
                && int.TryParse(args[i + 1], out var nextValue))
            {
                return nextValue;
            }
        }

        return fallback;
    }

    private static float ReadFloatUserArg(string name, float fallback)
    {
        var args = OS.GetCmdlineUserArgs();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                return float.TryParse(arg[(name.Length + 1)..], out var value) ? value : fallback;
            }

            if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)
                && i + 1 < args.Length
                && float.TryParse(args[i + 1], out var nextValue))
            {
                return nextValue;
            }
        }

        return fallback;
    }

    private static string ReadStringUserArg(string name, string fallback)
    {
        var args = OS.GetCmdlineUserArgs();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                return arg[(name.Length + 1)..];
            }

            if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return fallback;
    }

    private static bool ReadBoolUserArg(string name)
    {
        var args = OS.GetCmdlineUserArgs();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                var raw = arg[(name.Length + 1)..];
                return raw is "1" or "true" or "True" or "TRUE" or "yes" or "on";
            }
        }

        return false;
    }

    private void ConfigureVfxCapture()
    {
        var captureDirectory = ReadStringUserArg("--capture-vfx-dir", string.Empty);
        if (string.IsNullOrWhiteSpace(captureDirectory))
        {
            return;
        }

        _vfxCaptureDirectory = IOPath.GetFullPath(captureDirectory);
        Directory.CreateDirectory(_vfxCaptureDirectory);
        _vfxCaptureElapsed = 0.0;
        _vfxCaptureIndex = 0;
        GD.Print($"VFX capture enabled: {_vfxCaptureDirectory}");
    }

    private void UpdateVfxCapture(double delta)
    {
        if (string.IsNullOrWhiteSpace(_vfxCaptureDirectory))
        {
            return;
        }

        var captureTimes = new[] { 0.16, 0.46, 0.92, 1.55 };
        _vfxCaptureElapsed += delta;
        while (_vfxCaptureIndex < captureTimes.Length && _vfxCaptureElapsed >= captureTimes[_vfxCaptureIndex])
        {
            var image = GetViewport().GetTexture().GetImage();
            var path = IOPath.Combine(_vfxCaptureDirectory, $"ship_vfx_{_vfxCaptureIndex:00}.png");
            var error = image.SavePng(path);
            GD.Print(error == Error.Ok
                ? $"VFX capture saved: {path}"
                : $"VFX capture failed ({error}): {path}");
            _vfxCaptureIndex++;
        }

        if (_vfxCaptureIndex >= captureTimes.Length)
        {
            _vfxCaptureDirectory = string.Empty;
            RequestCaptureQuit();
        }
    }

    private void RequestCaptureQuit()
    {
        if (_captureQuitDelayFrames <= 0)
        {
            _captureQuitDelayFrames = 3;
        }
    }

    private void UpdateDeferredCaptureQuit()
    {
        if (_captureQuitDelayFrames <= 0)
        {
            return;
        }

        _captureQuitDelayFrames--;
        if (_captureQuitDelayFrames > 0)
        {
            return;
        }

        _quitRequested = true;
        GetTree().Quit();
    }

    private void ConfigureFrameCapture()
    {
        var captureDirectory = ReadStringUserArg("--capture-frame-dir", string.Empty);
        if (string.IsNullOrWhiteSpace(captureDirectory))
        {
            return;
        }

        _frameCaptureDirectory = IOPath.GetFullPath(captureDirectory);
        Directory.CreateDirectory(_frameCaptureDirectory);
        _frameCaptureElapsed = 0.0;
        _frameCaptureIndex = 0;
        _frameCapturePrefix = SanitizeCaptureName(ReadStringUserArg("--capture-frame-prefix", _currentSystem.Id));
        GD.Print($"Frame capture enabled: {_frameCaptureDirectory}");
    }

    private void UpdateFrameCapture(double delta)
    {
        if (string.IsNullOrWhiteSpace(_frameCaptureDirectory))
        {
            return;
        }

        var captureTimes = new[] { 0.45, 1.10, 1.38, 1.92, 3.08, 3.55 };
        _frameCaptureElapsed += delta;
        while (_frameCaptureIndex < captureTimes.Length && _frameCaptureElapsed >= captureTimes[_frameCaptureIndex])
        {
            if (string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase))
            {
                GD.Print("Frame capture skipped: viewport texture is unavailable in headless display mode.");
                _frameCaptureDirectory = string.Empty;
                if (ReadBoolUserArg("--capture-frame-quit"))
                {
                    RequestCaptureQuit();
                }

                return;
            }

            var texture = GetViewport().GetTexture();
            var image = texture?.GetImage();
            if (image is null || image.IsEmpty())
            {
                GD.Print("Frame capture skipped: viewport texture is unavailable in this display mode.");
                _frameCaptureDirectory = string.Empty;
                if (ReadBoolUserArg("--capture-frame-quit"))
                {
                    RequestCaptureQuit();
                }

                return;
            }

            var path = IOPath.Combine(_frameCaptureDirectory, $"{_frameCapturePrefix}_{_frameCaptureIndex:00}.png");
            var error = image.SavePng(path);
            GD.Print(error == Error.Ok
                ? $"Frame capture saved: {path}"
                : $"Frame capture failed ({error}): {path}");
            _frameCaptureIndex++;
        }

        if (_frameCaptureIndex >= captureTimes.Length && ReadBoolUserArg("--capture-frame-quit"))
        {
            _frameCaptureDirectory = string.Empty;
            RequestCaptureQuit();
        }
    }

    private static string SanitizeCaptureName(string value)
    {
        var name = string.IsNullOrWhiteSpace(value) ? "frame" : value.Trim();
        foreach (var character in IOPath.GetInvalidFileNameChars())
        {
            name = name.Replace(character, '_');
        }

        return name.Replace(' ', '_');
    }

    private void UpdateEnemyViews(WorldSnapshot visualSnapshot, Vector2 cameraCenter, Rect2 visibleWorldRect, Rect2 drawableWorldRect)
    {
        BuildEnemyLodSets(visualSnapshot, cameraCenter, visibleWorldRect, drawableWorldRect);

        _enemyRemovalBuffer.Clear();
        foreach (var (id, _) in _enemyViews)
        {
            if (_activeEnemyIds.Contains(id))
            {
                continue;
            }

            _enemyRemovalBuffer.Add(id);
        }

        foreach (var id in _enemyRemovalBuffer)
        {
            var view = _enemyViews[id];
            if (_shipIdToPilotId.TryGetValue(id, out var pilotId))
            {
                _galaxyLife?.ReportDestroyed(pilotId);
                _shipIdToPilotId.Remove(id);
                _pilotIdToShipId.Remove(pilotId);
                _npcShipTexturePaths.Remove(id);
            }

            var radius = _enemyExplosionRadii.TryGetValue(id, out var storedRadius) ? storedRadius : 86f;
            var tint = _enemyExplosionTints.TryGetValue(id, out var storedTint) ? storedTint : new Color(1f, 0.55f, 0.18f, 1f);
            if (_enemyExplosionVisuals.TryGetValue(id, out var visual))
            {
                _explosionLayer.SpawnShip(
                    view.Position,
                    visual.Radius,
                    visual.Tint,
                    ShipExplosionKind.Enemy,
                    visual.Texture,
                    visual.Scale,
                    view.Rotation,
                    visual.ContentBounds,
                    visual.ExhaustPorts);
            }
            else
            {
                _explosionLayer.SpawnShip(
                    view.Position,
                    radius,
                    tint,
                    ShipExplosionKind.Enemy,
                    null,
                    1f,
                    view.Rotation,
                    default,
                    Array.Empty<EnginePort>());
            }

            view.QueueFree();
            _enemyViews.Remove(id);
            _enemyExplosionRadii.Remove(id);
            _enemyExplosionTints.Remove(id);
            _enemyExplosionVisuals.Remove(id);
        }

        for (var index = 0; index < visualSnapshot.Ships.Count; index++)
        {
            var enemy = visualSnapshot.Ships[index];
            if (enemy.Id == _simulation.PlayerShipId)
            {
                continue;
            }

            if (!_enemyViews.TryGetValue(enemy.Id, out var enemyView))
            {
                continue;
            }

            var command = _simulation.TryGetLastCommand(enemy.Id, out var lastCommand)
                ? lastCommand
                : InputCommand.Idle(enemy.Position);
            var enemyPosition = enemy.Position.ToGodot();
            var aimDelta = command.AimWorld.ToGodot() - enemyPosition;
            var afterburnerActive = enemy.Mode == ShipMode.Navigation && command.Afterburner && command.Reverse <= 0.01f;
            var shouldDraw = drawableWorldRect.HasPoint(enemyPosition);

            if (!shouldDraw)
            {
                if (enemyView.Visible || enemyView.EffectQuality != ShipEffectQuality.Hidden)
                {
                    enemyView.Visible = false;
                    enemyView.SetProcess(false);
                    enemyView.EffectQuality = ShipEffectQuality.Hidden;
                }

                continue;
            }

            var quality = EffectQualityForEnemy(enemy.Id, shouldDraw, _fullQualityEnemyIds, _balancedQualityEnemyIds, _drawableEnemyDistances.Count);
            enemyView.Visible = true;
            enemyView.SetProcess(quality != ShipEffectQuality.Hidden || _showShipHitboxes);
            enemyView.EffectQuality = quality;
            enemyView.Position = enemyPosition;
            enemyView.Rotation = enemy.Rotation;
            enemyView.Velocity = enemy.Velocity.ToGodot();
            enemyView.AimDirection = aimDelta.LengthSquared() > 0.001f
                ? aimDelta.Normalized().Rotated(-enemy.Rotation)
                : Vector2.Up;
            enemyView.ThrustLevel = MathF.Max(command.Forward, afterburnerActive ? 1f : 0f);
            enemyView.ReverseLevel = command.Reverse;
            enemyView.StrafeLevel = command.Strafe;
            enemyView.AfterburnerLevel = afterburnerActive ? 1f : 0f;
            enemyView.IsFiring = enemy.Mode == ShipMode.Combat && command.Fire && enemy.WeaponCooldown > 0f;
            enemyView.ShowHitbox = _showShipHitboxes;
        }
    }

    private void BuildEnemyLodSets(WorldSnapshot visualSnapshot, Vector2 cameraCenter, Rect2 visibleWorldRect, Rect2 drawableWorldRect)
    {
        _activeEnemyIds.Clear();
        _fullQualityEnemyIds.Clear();
        _balancedQualityEnemyIds.Clear();
        _statusBarEnemyIds.Clear();
        _visibleEnemyDistances.Clear();
        _drawableEnemyDistances.Clear();

        for (var index = 0; index < visualSnapshot.Ships.Count; index++)
        {
            var ship = visualSnapshot.Ships[index];
            if (ship.Id == _simulation.PlayerShipId)
            {
                continue;
            }

            _activeEnemyIds.Add(ship.Id);
            var position = ship.Position.ToGodot();
            var distanceSquared = position.DistanceSquaredTo(cameraCenter);

            if (visibleWorldRect.HasPoint(position))
            {
                _visibleEnemyDistances.Add(new EnemyDistance(ship.Id, distanceSquared));
            }

            if (drawableWorldRect.HasPoint(position))
            {
                _drawableEnemyDistances.Add(new EnemyDistance(ship.Id, distanceSquared));
            }
        }

        _visibleEnemyDistances.Sort(CompareEnemyDistance);
        _drawableEnemyDistances.Sort(CompareEnemyDistance);

        var fullLimit = _drawableEnemyDistances.Count > 8 ? 0 : _drawableEnemyDistances.Count > 0 ? 1 : 0;
        var balancedLimit = _drawableEnemyDistances.Count > 80 ? 4 : _drawableEnemyDistances.Count > 40 ? 6 : 8;
        var statusLimit = _drawableEnemyDistances.Count > 80 ? 18 : _drawableEnemyDistances.Count > 40 ? 24 : 36;

        var fullCount = Math.Min(fullLimit, _visibleEnemyDistances.Count);
        for (var index = 0; index < fullCount; index++)
        {
            _fullQualityEnemyIds.Add(_visibleEnemyDistances[index].Id);
        }

        var balancedCount = Math.Min(balancedLimit, _drawableEnemyDistances.Count);
        for (var index = 0; index < balancedCount; index++)
        {
            _balancedQualityEnemyIds.Add(_drawableEnemyDistances[index].Id);
        }

        var statusCount = Math.Min(statusLimit, _visibleEnemyDistances.Count);
        for (var index = 0; index < statusCount; index++)
        {
            _statusBarEnemyIds.Add(_visibleEnemyDistances[index].Id);
        }

        if (_lockedTargetShipId > 0)
        {
            if (ContainsEnemyDistance(_drawableEnemyDistances, _lockedTargetShipId))
            {
                _balancedQualityEnemyIds.Add(_lockedTargetShipId);
            }

            if (ContainsEnemyDistance(_visibleEnemyDistances, _lockedTargetShipId))
            {
                _statusBarEnemyIds.Add(_lockedTargetShipId);
            }
        }
    }

    private WorldSnapshot InterpolateSnapshot(WorldSnapshot from, WorldSnapshot to, float amount)
    {
        if (from.Tick == to.Tick || amount >= 0.999f)
        {
            return to;
        }

        if (amount <= 0.001f)
        {
            return from;
        }

        _visualShips.SetCount(to.Ships.Count);
        var previousShipIndex = 0;
        for (var index = 0; index < to.Ships.Count; index++)
        {
            var ship = to.Ships[index];
            _visualShips[index] = TryFindShipInOrder(from.Ships, ship.Id, ref previousShipIndex, out var previous)
                ? InterpolateShip(previous, ship, amount)
                : ship;
        }

        _visualProjectiles.SetCount(to.Projectiles.Count);
        var previousProjectileIndex = 0;
        for (var index = 0; index < to.Projectiles.Count; index++)
        {
            var projectile = to.Projectiles[index];
            _visualProjectiles[index] = TryFindProjectileInOrder(from.Projectiles, projectile.Id, ref previousProjectileIndex, out var previous)
                ? InterpolateProjectile(previous, projectile, amount)
                : projectile;
        }

        _visualAsteroids.SetCount(to.Asteroids.Count);
        var previousAsteroidIndex = 0;
        for (var index = 0; index < to.Asteroids.Count; index++)
        {
            var asteroid = to.Asteroids[index];
            _visualAsteroids[index] = TryFindAsteroidInOrder(from.Asteroids, asteroid.Id, ref previousAsteroidIndex, out var previous)
                ? InterpolateAsteroid(previous, asteroid, amount)
                : asteroid;
        }

        return new WorldSnapshot(to.Tick, _visualShips, _visualProjectiles, to.ProjectileImpacts, to.Bounds, _visualAsteroids, to.AsteroidEvents);
    }

    private static float SnapshotTimeSeconds(WorldSnapshot snapshot)
    {
        return snapshot.Tick / (float)SimulationConfig.TickRate;
    }

    private static float InterpolatedTimeSeconds(WorldSnapshot from, WorldSnapshot to, float amount)
    {
        if (from.Tick == to.Tick)
        {
            return SnapshotTimeSeconds(to);
        }

        var tick = from.Tick + (to.Tick - from.Tick) * Math.Clamp(amount, 0f, 1f);
        return (float)(tick / SimulationConfig.TickRate);
    }

    private static ShipState InterpolateShip(ShipState from, ShipState to, float amount)
    {
        return new ShipState(
            to.Id,
            Lerp(from.Position, to.Position, amount),
            Lerp(from.Velocity, to.Velocity, amount),
            LerpAngle(from.Rotation, to.Rotation, amount),
            Lerp(from.Energy, to.Energy, amount),
            Lerp(from.WeaponCooldown, to.WeaponCooldown, amount),
            to.Hitbox,
            InterpolateCombat(from.Combat, to.Combat, amount),
            to.Mode,
            Lerp(from.ModeSwitchCooldown, to.ModeSwitchCooldown, amount),
            to.Role,
            to.Callsign,
            to.VisualId);
    }

    private static ProjectileState InterpolateProjectile(ProjectileState from, ProjectileState to, float amount)
    {
        return new ProjectileState(
            to.Id,
            to.OwnerId,
            Lerp(from.Position, to.Position, amount),
            Lerp(from.Velocity, to.Velocity, amount),
            Lerp(from.Lifetime, to.Lifetime, amount),
            to.Damage,
            to.WeaponId,
            to.DamageType,
            Lerp(from.RangeRemaining, to.RangeRemaining, amount));
    }

    private static AsteroidState InterpolateAsteroid(AsteroidState from, AsteroidState to, float amount)
    {
        return new AsteroidState(
            to.Id,
            Lerp(from.Position, to.Position, amount),
            Lerp(from.Velocity, to.Velocity, amount),
            to.Radius,
            LerpAngle(from.Rotation, to.Rotation, amount),
            Lerp(from.AngularVelocity, to.AngularVelocity, amount),
            Lerp(from.Structure, to.Structure, amount),
            to.MaxStructure,
            Lerp(from.Heat, to.Heat, amount),
            to.Variant,
            to.Seed);
    }

    private static CombatStats InterpolateCombat(CombatStats from, CombatStats to, float amount)
    {
        return new CombatStats(
            Lerp(from.Shield, to.Shield, amount),
            Lerp(from.Armor, to.Armor, amount),
            Lerp(from.Structure, to.Structure, amount),
            to.MaxShield,
            to.MaxArmor,
            to.MaxStructure,
            Lerp(from.ShieldRegenLockout, to.ShieldRegenLockout, amount));
    }

    private static CoreVector2 Lerp(CoreVector2 from, CoreVector2 to, float amount)
    {
        return from + (to - from) * amount;
    }

    private static float Lerp(float from, float to, float amount)
    {
        return from + (to - from) * amount;
    }

    private static float LerpAngle(float from, float to, float amount)
    {
        var delta = MathF.Atan2(MathF.Sin(to - from), MathF.Cos(to - from));
        return from + delta * amount;
    }

    private static bool TryFindShipInOrder(IReadOnlyList<ShipState> ships, int id, ref int startIndex, out ShipState result)
    {
        for (var index = startIndex; index < ships.Count; index++)
        {
            var ship = ships[index];
            if (ship.Id == id)
            {
                startIndex = index + 1;
                result = ship;
                return true;
            }

            if (ship.Id > id)
            {
                break;
            }
        }

        result = default;
        return false;
    }

    private static bool TryFindProjectileInOrder(IReadOnlyList<ProjectileState> projectiles, int id, ref int startIndex, out ProjectileState result)
    {
        for (var index = startIndex; index < projectiles.Count; index++)
        {
            var projectile = projectiles[index];
            if (projectile.Id == id)
            {
                startIndex = index + 1;
                result = projectile;
                return true;
            }

            if (projectile.Id > id)
            {
                break;
            }
        }

        result = default;
        return false;
    }

    private static bool TryFindAsteroidInOrder(IReadOnlyList<AsteroidState> asteroids, int id, ref int startIndex, out AsteroidState result)
    {
        for (var index = startIndex; index < asteroids.Count; index++)
        {
            var asteroid = asteroids[index];
            if (asteroid.Id == id)
            {
                startIndex = index + 1;
                result = asteroid;
                return true;
            }

            if (asteroid.Id > id)
            {
                break;
            }
        }

        result = default;
        return false;
    }

    private void ConfigureShipCatalogs()
    {
        var allPaths = ShipCatalog.LoadShipTexturePaths();
        _klissanShipTexturePaths = allPaths
            .Where(path => string.Equals(ShipCatalog.RaceFromPath(path), "Klissan", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        _ordinaryShipTexturePaths = allPaths
            .Where(path => !string.Equals(ShipCatalog.RaceFromPath(path), "Klissan", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        _shipTexturePaths = _ordinaryShipTexturePaths.Count > 0 ? _ordinaryShipTexturePaths : allPaths;
        _useKlissanShipGroup = false;
    }

    private void SelectStartupShipIfRequested()
    {
        var requested = ReadStringUserArg("--ship", string.Empty);
        if (string.IsNullOrWhiteSpace(requested))
        {
            return;
        }

        var path = EnumerateShipTexturePaths()
            .FirstOrDefault(candidate =>
                candidate.GetFile().GetBaseName().Contains(requested, StringComparison.OrdinalIgnoreCase)
                || ShipCatalog.DisplayName(candidate).Contains(requested, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(path))
        {
            GD.Print($"Startup ship: '{requested}' was not found.");
            return;
        }

        var useKlissan = string.Equals(ShipCatalog.RaceFromPath(path), "Klissan", StringComparison.OrdinalIgnoreCase);
        var targetPaths = useKlissan ? _klissanShipTexturePaths : _ordinaryShipTexturePaths;
        if (targetPaths.Count == 0)
        {
            targetPaths = _shipTexturePaths;
        }

        _useKlissanShipGroup = useKlissan;
        _shipTexturePaths = targetPaths;
        var index = ShipCatalog.IndexOfPreferred(_shipTexturePaths, path.GetFile().GetBaseName());
        SelectShipTexture(index);
        GD.Print($"Startup ship: {ShipCatalog.DisplayName(path)}.");
    }

    private void ToggleShipCatalog()
    {
        if (_useKlissanShipGroup)
        {
            _klissanShipTextureIndex = _shipTextureIndex;
        }
        else
        {
            _ordinaryShipTextureIndex = _shipTextureIndex;
        }

        var targetKlissan = !_useKlissanShipGroup;
        var targetPaths = targetKlissan ? _klissanShipTexturePaths : _ordinaryShipTexturePaths;
        if (targetPaths.Count == 0)
        {
            GD.Print(targetKlissan
                ? "Ship catalog: Klissan ships are not available."
                : "Ship catalog: ordinary ships are not available.");
            return;
        }

        _useKlissanShipGroup = targetKlissan;
        _shipTexturePaths = targetPaths;
        var rememberedIndex = _useKlissanShipGroup ? _klissanShipTextureIndex : _ordinaryShipTextureIndex;
        if (rememberedIndex < 0)
        {
            rememberedIndex = ShipCatalog.IndexOfPreferred(
                _shipTexturePaths,
                _useKlissanShipGroup ? "2KlissanFrigate" : "2PeopleR");
        }

        SelectShipTexture(rememberedIndex);
        GD.Print($"Ship catalog: {(_useKlissanShipGroup ? "Klissan" : "Ordinary")} ({_shipTexturePaths.Count} ships).");
    }

    private void SelectShipTexture(int index)
    {
        if (_shipTexturePaths.Count == 0)
        {
            return;
        }

        _shipTextureIndex = ((index % _shipTexturePaths.Count) + _shipTexturePaths.Count) % _shipTexturePaths.Count;
        var path = _shipTexturePaths[_shipTextureIndex];
        var texture = ShipCatalog.LoadTexture(path);
        if (texture is null)
        {
            return;
        }

        _selectedShipName = ShipCatalog.DisplayName(path);
        if (_useKlissanShipGroup)
        {
            _klissanShipTextureIndex = _shipTextureIndex;
        }
        else
        {
            _ordinaryShipTextureIndex = _shipTextureIndex;
        }

        var profile = ShipCatalog.VisualProfileForPath(path, texture);
        _selectedShipTexture = texture;
        _selectedShipProfile = profile;
        _selectedShipExhaustPorts = ShipCatalog.ExhaustPortsForPath(path);
        var rigProfile = ShipCatalog.RigProfileForPath(texture, profile, _selectedShipExhaustPorts);
        _shipView.EngineOuterColor = ShipCatalog.ThrustOuterColor(path);
        _shipView.EngineCoreColor = ShipCatalog.ThrustCoreColor(path);
        _selectedWarpOuterColor = ShipCatalog.WarpOuterColor(path);
        _selectedWarpCoreColor = ShipCatalog.WarpCoreColor(path);
        _shipView.WarpOuterColor = _selectedWarpOuterColor;
        _shipView.WarpCoreColor = _selectedWarpCoreColor;
        _shipView.EngineEffectScale = ShipCatalog.ThrustSizeMultiplier(path);
        _shipView.EngineBubbleScale = ShipCatalog.ThrustBubbleMultiplier(path);
        _shipView.EngineParticleDensity = ShipCatalog.ThrustParticleDensity(path);
        _shipView.EnginePlumeTexture = ShipCatalog.LoadTexture(ShipCatalog.ThrustPlumeTexturePath(path));
        _shipView.ExhaustPorts = _selectedShipExhaustPorts;
        _shipView.RigProfile = rigProfile;
        _selectedShipVisualScale = profile.Scale;
        _shipView.Scale = new Vector2(profile.Scale, profile.Scale);
        _shipView.HitboxLocalCenter = profile.HitboxLocalCenter;
        _shipView.HitboxLocalSize = profile.HitboxLocalSize;
        _simulation.SetPlayerShipHitbox(new ShipHitbox(
            profile.HitboxLocalCenter.ToCore() * profile.Scale,
            profile.HitboxLocalSize.ToCore() * profile.Scale));
        _shipView.SetShipTexture(texture);
        _shipView.Visible = true;
        _shipView.SetProcess(true);
    }

    private ShipView CreateShipView(string path, Texture2D texture, ShipVisualProfile profile)
    {
        var exhaustPorts = ShipCatalog.ExhaustPortsForPath(path);
        var view = new ShipView
        {
            Scale = new Vector2(profile.Scale, profile.Scale),
            EngineOuterColor = ShipCatalog.ThrustOuterColor(path),
            EngineCoreColor = ShipCatalog.ThrustCoreColor(path),
            WarpOuterColor = ShipCatalog.WarpOuterColor(path),
            WarpCoreColor = ShipCatalog.WarpCoreColor(path),
            EngineEffectScale = ShipCatalog.ThrustSizeMultiplier(path),
            EngineBubbleScale = ShipCatalog.ThrustBubbleMultiplier(path),
            EngineParticleDensity = ShipCatalog.ThrustParticleDensity(path),
            EnginePlumeTexture = ShipCatalog.LoadTexture(ShipCatalog.ThrustPlumeTexturePath(path)),
            ExhaustPorts = exhaustPorts,
            RigProfile = ShipCatalog.RigProfileForPath(texture, profile, exhaustPorts),
            HitboxLocalCenter = profile.HitboxLocalCenter,
            HitboxLocalSize = profile.HitboxLocalSize,
            ShowHitbox = _showShipHitboxes,
            EffectQuality = ShipEffectQuality.Balanced
        };
        view.SetShipTexture(texture);
        return view;
    }

    private Rect2 CameraWorldRect(Vector2 center, float margin)
    {
        var viewportSize = GetViewportRect().Size;
        var zoom = _camera?.Zoom ?? Vector2.One;
        var worldSize = new Vector2(
            viewportSize.X / Math.Max(0.001f, zoom.X),
            viewportSize.Y / Math.Max(0.001f, zoom.Y));
        return new Rect2(center - worldSize * 0.5f - new Vector2(margin, margin), worldSize + new Vector2(margin * 2f, margin * 2f));
    }

    private static ShipEffectQuality EffectQualityForEnemy(
        int enemyId,
        bool shouldDraw,
        HashSet<int> fullQualityIds,
        HashSet<int> balancedQualityIds,
        int drawableEnemyCount)
    {
        if (!shouldDraw)
        {
            return ShipEffectQuality.Hidden;
        }

        if (fullQualityIds.Contains(enemyId))
        {
            return ShipEffectQuality.Full;
        }

        if (balancedQualityIds.Contains(enemyId))
        {
            return ShipEffectQuality.Balanced;
        }

        return drawableEnemyCount > 32
            ? ShipEffectQuality.Hidden
            : ShipEffectQuality.Minimal;
    }

    private static int CompareEnemyDistance(EnemyDistance left, EnemyDistance right)
    {
        return left.DistanceSquared.CompareTo(right.DistanceSquared);
    }

    private static bool ContainsEnemyDistance(IReadOnlyList<EnemyDistance> distances, int id)
    {
        for (var index = 0; index < distances.Count; index++)
        {
            if (distances[index].Id == id)
            {
                return true;
            }
        }

        return false;
    }

    private ShipState PlayerShipFrom(WorldSnapshot snapshot)
    {
        if (snapshot.Ships.Count > 0 && snapshot.Ships[0].Id == _simulation.PlayerShipId)
        {
            return snapshot.Ships[0];
        }

        foreach (var ship in snapshot.Ships)
        {
            if (ship.Id == _simulation.PlayerShipId)
            {
                return ship;
            }
        }

        return default;
    }

    private string FindShipTexturePath(string id)
    {
        return EnumerateShipTexturePaths()
            .FirstOrDefault(path => path.Contains(id, StringComparison.OrdinalIgnoreCase))
            ?? $"res://assets/ships/{id}.png";
    }

    private IEnumerable<string> EnumerateShipTexturePaths()
    {
        return _shipTexturePaths
            .Concat(_ordinaryShipTexturePaths)
            .Concat(_klissanShipTexturePaths)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static float RotationFacing(CoreVector2 from, CoreVector2 to)
    {
        var direction = to - from;
        if (direction.LengthSquared() < 0.0001f)
        {
            return 0f;
        }

        direction = CoreVector2.Normalize(direction);
        return MathF.Atan2(direction.X, -direction.Y);
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        if (Math.Abs(edge1 - edge0) <= 0.0001f)
        {
            return value < edge0 ? 0f : 1f;
        }

        var x = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return x * x * (3f - 2f * x);
    }

    private readonly record struct WarpVisualState(
        Vector2 ShipPosition,
        Vector2 ShipScale,
        float ShipAlpha,
        Vector2 TunnelPosition,
        Vector2 TunnelMouthOffset,
        float TunnelRotation);

    private static void ConfigureInputMap()
    {
        BindKey("ship_thrust", Key.W);
        BindKey("ship_reverse", Key.S);
        BindKey("ship_turn_left", Key.A);
        BindKey("ship_turn_right", Key.D);
        BindKey("ship_strafe_left", Key.Q);
        BindKey("ship_strafe_right", Key.E);
        BindKey("ship_afterburner", Key.Shift);
        BindKey("ship_toggle_mode", Key.R);
        BindKey("ship_next_sprite", Key.Tab);
        BindKey("ship_toggle_catalog", Key.Quoteleft);
        BindKey("debug_toggle_ship_hitboxes", Key.F3);
        BindKey("debug_spawn_enemy", Key.F5);
        BindKey("debug_toggle_player_godmode", Key.F6);
        BindKey("debug_spawn_asteroid", Key.F7);
        BindKey("debug_burst_asteroid", Key.F8);
        BindKey("debug_self_destruct_player", Key.F9);
        BindKey("debug_revive_player", Key.F10);
        BindKey("debug_system_sol", Key.F11);
        BindKey("debug_system_next", Key.F12);
        BindKey("star_map_toggle", Key.M);
        BindKey("star_map_close", Key.Escape);
        BindKey("music_toggle", Key.N);
        BindKey("warp_jump", Key.B);
        BindMouse("weapon_fire", MouseButton.Left);
        BindMouse("target_lock", MouseButton.Right);
    }

    private static void BindKey(string action, Key key)
    {
        EnsureAction(action);
        var desired = new InputEventKey { PhysicalKeycode = key };
        if (!InputMap.ActionHasEvent(action, desired))
        {
            InputMap.ActionAddEvent(action, desired);
        }
    }

    private static void BindMouse(string action, MouseButton button)
    {
        EnsureAction(action);
        var desired = new InputEventMouseButton { ButtonIndex = button };
        if (!InputMap.ActionHasEvent(action, desired))
        {
            InputMap.ActionAddEvent(action, desired);
        }
    }

    private static void EnsureAction(string action)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action);
        }
    }

    private readonly record struct EnemyDistance(int Id, float DistanceSquared);

    private sealed class SnapshotBuffer<T> : IReadOnlyList<T>
    {
        private T[] _items = Array.Empty<T>();

        public int Count { get; private set; }

        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _items[index];
            }
            set
            {
                if ((uint)index >= (uint)Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                _items[index] = value;
            }
        }

        public void SetCount(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (_items.Length < count)
            {
                var capacity = Math.Max(4, _items.Length);
                while (capacity < count)
                {
                    capacity *= 2;
                }

                Array.Resize(ref _items, capacity);
            }

            Count = count;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (var index = 0; index < Count; index++)
            {
                yield return _items[index];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private readonly record struct ShipExplosionVisual(
        Texture2D Texture,
        float Scale,
        Rect2 ContentBounds,
        IReadOnlyList<EnginePort> ExhaustPorts,
        float Radius,
        Color Tint);

    private readonly record struct ShipVfxSample(string Path, ShipExplosionKind Kind, Vector2 Offset, float Rotation);
}
