using System.Numerics;

namespace SpaceRangers.Core;

public sealed class LocalSimulation
{
    private readonly SimulationConfig _config;
    private readonly List<ProjectileState> _projectiles = new();
    private readonly List<ProjectileImpactState> _projectileImpacts = new();
    private readonly List<ShipState> _enemyShips = new();
    private readonly List<AsteroidState> _asteroids = new();
    private readonly List<AsteroidEventState> _asteroidEvents = new();
    private readonly List<Vector2> _asteroidPreviousPositions = new();
    private readonly Dictionary<int, InputCommand> _lastCommands = new();
    private readonly Dictionary<int, long> _nextSunBurnImpactTicks = new();
    private readonly Dictionary<ulong, long> _nextShipCollisionDamageTicks = new();
    private readonly Random _random;
    private ShipState _playerShip;
    private long _tick;
    private int _nextProjectileId = 1;
    private int _nextProjectileImpactId = 1;
    private int _nextShipId = 2;
    private int _nextAsteroidId = 1;
    private float _asteroidSpawnTimer;

    public LocalSimulation(SimulationConfig? config = null)
    {
        _config = config ?? new SimulationConfig();
        _random = new Random(_config.AsteroidSeed);
        _asteroidSpawnTimer = RandomAsteroidSpawnInterval();
        _playerShip = new ShipState(
            1,
            Vector2.Zero,
            Vector2.Zero,
            0f,
            _config.MaxEnergy,
            0f,
            ShipHitbox.Default,
            CreateFullCombatStats(),
            ShipMode.Navigation,
            0f);
        _lastCommands[_playerShip.Id] = InputCommand.Idle(_playerShip.Position);
    }

    public SimulationConfig Config => _config;
    public int PlayerShipId => _playerShip.Id;
    public bool PlayerGodMode { get; set; }
    public WorldBounds Bounds => _config.Bounds;

    public WorldSnapshot CurrentSnapshot => CreateSnapshot();

    public void ResetPlayerShip(ShipState state)
    {
        _playerShip = state;
        _enemyShips.Clear();
        _lastCommands.Clear();
        _lastCommands[_playerShip.Id] = InputCommand.Idle(_playerShip.Position);
        _projectiles.Clear();
        _projectileImpacts.Clear();
        _asteroids.Clear();
        _asteroidEvents.Clear();
        _asteroidPreviousPositions.Clear();
        _nextSunBurnImpactTicks.Clear();
        _nextShipCollisionDamageTicks.Clear();
        _tick = 0;
        _nextProjectileId = 1;
        _nextProjectileImpactId = 1;
        _nextShipId = Math.Max(2, _playerShip.Id + 1);
        _nextAsteroidId = 1;
        _asteroidSpawnTimer = RandomAsteroidSpawnInterval();
    }

    public void SetPlayerShipHitbox(ShipHitbox hitbox)
    {
        _playerShip.Hitbox = hitbox;
    }

    public void RevivePlayerShip(Vector2? position = null, float? rotation = null)
    {
        _playerShip.Position = Bounds.Clamp(position ?? _playerShip.Position, MathF.Max(_config.ShipRadius, _playerShip.Hitbox.BoundingRadius));
        _playerShip.Velocity = Vector2.Zero;
        _playerShip.Rotation = rotation ?? _playerShip.Rotation;
        _playerShip.Energy = _config.MaxEnergy;
        _playerShip.WeaponCooldown = 0f;
        _playerShip.Combat = CreateFullCombatStats();
        _playerShip.Mode = ShipMode.Navigation;
        _playerShip.ModeSwitchCooldown = 0f;
        _nextSunBurnImpactTicks.Remove(_playerShip.Id);
        RemoveShipCollisionCooldownsFor(_playerShip.Id);
        _lastCommands[_playerShip.Id] = InputCommand.Idle(_playerShip.Position);
    }

    public bool TryGetLastCommand(int shipId, out InputCommand command)
    {
        return _lastCommands.TryGetValue(shipId, out command);
    }

    public int SpawnEnemyShip(Vector2 position, float rotation, ShipHitbox hitbox)
    {
        var id = _nextShipId++;
        var boundaryRadius = Math.Max(_config.ShipRadius, hitbox.BoundingRadius);
        var ship = new ShipState(
            id,
            _config.Bounds.Clamp(position, boundaryRadius),
            Vector2.Zero,
            rotation,
            _config.MaxEnergy,
            0f,
            hitbox,
            CreateFullCombatStats(),
            ShipMode.Navigation,
            0f);

        _enemyShips.Add(ship);
        _lastCommands[id] = InputCommand.Idle(_playerShip.Position);
        return id;
    }

    public bool ApplyDamageToShip(int shipId, float damage)
    {
        if (shipId == _playerShip.Id)
        {
            ApplyDamageToShipState(ref _playerShip, damage, PlayerGodMode);
            return true;
        }

        for (var index = 0; index < _enemyShips.Count; index++)
        {
            var ship = _enemyShips[index];
            if (ship.Id != shipId)
            {
                continue;
            }

            ApplyDamageToShipState(ref ship, damage, protectedByGodMode: false);
            _enemyShips[index] = ship;
            return true;
        }

        return false;
    }

    private void ApplyDamageToShipState(ref ShipState ship, float damage, bool protectedByGodMode)
    {
        if (protectedByGodMode)
        {
            return;
        }

        ship.Combat = ship.Combat.ApplyDamage(damage, _config.ShieldZeroRegenerationLockout);
        if (ship.IsDestroyed)
        {
            ship.Velocity *= 0.15f;
        }
    }

    public void SpawnProjectile(int ownerId, Vector2 position, Vector2 velocity, float lifetime, float damage)
    {
        if (_config.MaxProjectiles <= 0)
        {
            return;
        }

        while (_projectiles.Count >= _config.MaxProjectiles)
        {
            _projectiles.RemoveAt(0);
        }

        _projectiles.Add(new ProjectileState(_nextProjectileId++, ownerId, position, velocity, lifetime, damage));
    }

    public void SeedAsteroids(int count)
    {
        if (!_config.AsteroidsEnabled || count <= 0 || _config.AsteroidMaxActiveCount <= 0)
        {
            return;
        }

        var target = Math.Min(count, _config.AsteroidMaxActiveCount);
        for (var i = _asteroids.Count; i < target; i++)
        {
            if (!TrySpawnAsteroid(prewarmSeconds: RandomRange(0f, 22f)))
            {
                break;
            }
        }
    }

    public int SpawnAsteroid(Vector2 position, Vector2 velocity, float referenceDiameter, float? structure = null, int variant = 0)
    {
        if (_config.AsteroidMaxActiveCount <= 0 || _asteroids.Count >= _config.AsteroidMaxActiveCount)
        {
            return -1;
        }

        var clampedReferenceDiameter = Math.Clamp(
            referenceDiameter,
            _config.AsteroidMinReferenceDiameter,
            _config.AsteroidMaxReferenceDiameter);
        var radius = AsteroidPhysics.ReferenceDiameterToWorld(clampedReferenceDiameter) * 0.5f;
        var maxStructure = Math.Clamp(structure ?? StructureForReferenceDiameter(clampedReferenceDiameter), 1f, _config.AsteroidMaxStructure);
        var asteroid = new AsteroidState(
            _nextAsteroidId++,
            position,
            velocity,
            radius,
            RandomRange(0f, MathF.Tau),
            RandomRange(-1.2f, 1.2f),
            maxStructure,
            maxStructure,
            AsteroidPhysics.HeatRatio(position, _config),
            variant,
            _random.Next());
        _asteroids.Add(asteroid);
        return asteroid.Id;
    }

    public bool TryDestroyNearestAsteroid(Vector2 position, float maxDistance, AsteroidEventType eventType, out AsteroidEventState asteroidEvent)
    {
        asteroidEvent = default;
        if (_asteroids.Count == 0)
        {
            return false;
        }

        var maxDistanceSquared = maxDistance <= 0f ? float.PositiveInfinity : maxDistance * maxDistance;
        var nearestIndex = -1;
        var nearestDistanceSquared = maxDistanceSquared;
        for (var index = 0; index < _asteroids.Count; index++)
        {
            var asteroid = _asteroids[index];
            if (asteroid.IsDestroyed)
            {
                continue;
            }

            var distanceSquared = Vector2.DistanceSquared(position, asteroid.Position);
            if (distanceSquared > nearestDistanceSquared)
            {
                continue;
            }

            nearestDistanceSquared = distanceSquared;
            nearestIndex = index;
        }

        if (nearestIndex < 0)
        {
            return false;
        }

        DestroyAsteroid(nearestIndex, eventType);
        asteroidEvent = _asteroidEvents[^1];
        return true;
    }

    public WorldSnapshot Step(InputCommand command)
    {
        var dt = _config.FixedDelta;
        _asteroidEvents.Clear();
        _projectileImpacts.Clear();
        _lastCommands[_playerShip.Id] = command;
        StepShip(ref _playerShip, command, dt);

        for (var index = 0; index < _enemyShips.Count; index++)
        {
            var enemy = _enemyShips[index];
            var enemyCommand = ShouldRefreshEnemyCommand(enemy)
                ? BuildEnemyCommand(enemy, _playerShip)
                : ReuseEnemyCommand(enemy);
            StepShip(ref enemy, enemyCommand, dt);
            _lastCommands[enemy.Id] = enemyCommand with { ToggleMode = false };
            _enemyShips[index] = enemy;
        }

        CheckShipShipCollisions();
        StepAsteroidSpawning(dt);
        StepAsteroids(dt);
        StepProjectiles(dt);
        RemoveInactiveAsteroids();
        RegenerateShipsCombat(dt);
        ApplySunBurnDamage(dt);
        RemoveDestroyedEnemyShips();
        PruneExpiredShipCollisionCooldowns();
        _tick++;
        return CreateSnapshot();
    }

    private void StepShip(ref ShipState ship, InputCommand command, float dt)
    {
        if (ship.IsDestroyed)
        {
            ship.Velocity *= MathF.Exp(-_config.LinearDamping * dt);
            ship.Position += ship.Velocity * dt;
            return;
        }

        ship.ModeSwitchCooldown = MathF.Max(0f, ship.ModeSwitchCooldown - dt);
        if (command.ToggleMode && ship.ModeSwitchCooldown <= 0f)
        {
            ship.Mode = ship.Mode == ShipMode.Navigation ? ShipMode.Combat : ShipMode.Navigation;
            ship.ModeSwitchCooldown = _config.ShipModeSwitchCooldown;
        }

        ship.Rotation += Math.Clamp(command.Turn, -1f, 1f) * _config.TurnSpeed * dt;

        var forward = SimulationMath.ForwardFromRotation(ship.Rotation);
        var right = SimulationMath.RightFromRotation(ship.Rotation);
        var speedBefore = ship.Velocity.Length();
        var afterburnerActive = ship.Mode == ShipMode.Navigation && command.Afterburner && command.Reverse <= 0.01f;
        var forwardInput = Math.Clamp(command.Forward, 0f, 1f);
        var effectiveForward = afterburnerActive ? MathF.Max(1f, forwardInput) : forwardInput;
        var forwardAcceleration = _config.ForwardAcceleration;
        if (afterburnerActive && speedBefore >= _config.MaxSpeed)
        {
            forwardAcceleration = _config.AfterburnerHighSpeedAcceleration;
        }

        var acceleration = Vector2.Zero;
        acceleration += forward * effectiveForward * forwardAcceleration;
        acceleration -= forward * Math.Clamp(command.Reverse, 0f, 1f) * _config.ReverseAcceleration;
        acceleration += right * Math.Clamp(command.Strafe, -1f, 1f) * _config.StrafeAcceleration;

        ship.Velocity += acceleration * dt;
        var dampingFactor = MathF.Exp(-_config.LinearDamping * dt);
        ship.Velocity *= dampingFactor;
        var speedLimit = afterburnerActive
            ? _config.AfterburnerMaxSpeed
            : MathF.Max(_config.MaxSpeed, speedBefore * dampingFactor);
        ship.Velocity = SimulationMath.ClampLength(ship.Velocity, speedLimit);

        ship.Position += ship.Velocity * dt;
        var boundaryRadius = Math.Max(_config.ShipRadius, ship.Hitbox.BoundingRadius);
        if (!_config.Bounds.Contains(ship.Position, boundaryRadius))
        {
            var clamped = _config.Bounds.Clamp(ship.Position, boundaryRadius);
            if (Math.Abs(clamped.X - ship.Position.X) > 0.001f)
            {
                ship.Velocity = new Vector2(-ship.Velocity.X * 0.18f, ship.Velocity.Y);
            }

            if (Math.Abs(clamped.Y - ship.Position.Y) > 0.001f)
            {
                ship.Velocity = new Vector2(ship.Velocity.X, -ship.Velocity.Y * 0.18f);
            }

            ship.Position = clamped;
        }

        ship.WeaponCooldown = MathF.Max(0f, ship.WeaponCooldown - dt);
        ship.Energy = MathF.Min(_config.MaxEnergy, ship.Energy + _config.EnergyRechargePerSecond * dt);

        if (ship.Mode == ShipMode.Combat && command.Fire && ship.WeaponCooldown <= 0f && ship.Energy >= _config.WeaponEnergyCost)
        {
            FireProjectile(ship, command.AimWorld);
            ship.WeaponCooldown = _config.WeaponCooldown;
            ship.Energy -= _config.WeaponEnergyCost;
        }
    }

    private void FireProjectile(ShipState ship, Vector2 aimWorld)
    {
        var forward = SimulationMath.ForwardFromRotation(ship.Rotation);
        var direction = SimulationMath.SafeNormalize(aimWorld - ship.Position, forward);
        var muzzleDistance = Math.Max(_config.ShipRadius, ship.Hitbox.ForwardExtent) + 6f;
        var muzzlePosition = ship.Position + direction * muzzleDistance;
        var velocity = direction * _config.ProjectileSpeed + ship.Velocity * 0.25f;
        SpawnProjectile(ship.Id, muzzlePosition, velocity, _config.ProjectileLifetime, _config.ProjectileDamage);
    }

    private void StepProjectiles(float dt)
    {
        for (var index = _projectiles.Count - 1; index >= 0; index--)
        {
            var projectile = _projectiles[index];
            var previousPosition = projectile.Position;
            projectile.Position += projectile.Velocity * dt;
            projectile.Lifetime -= dt;

            if (TryHitShip(projectile, previousPosition, projectile.Position))
            {
                _projectiles.RemoveAt(index);
                continue;
            }

            if (TryHitAsteroid(projectile, previousPosition, projectile.Position))
            {
                _projectiles.RemoveAt(index);
                continue;
            }

            if (projectile.Lifetime <= 0f || !_config.Bounds.Contains(projectile.Position, 0f))
            {
                _projectiles.RemoveAt(index);
                continue;
            }

            _projectiles[index] = projectile;
        }
    }

    private bool TryHitShip(ProjectileState projectile, Vector2 previousPosition, Vector2 currentPosition)
    {
        var playerSurface = SurfaceForCombat(_playerShip.Combat);
        if (CanProjectileHit(projectile, _playerShip.Id)
            && !_playerShip.IsDestroyed
            && TryIntersectShipSurface(_playerShip.Position, _playerShip.Rotation, _playerShip.Hitbox, playerSurface, previousPosition, currentPosition, out var playerImpactPosition))
        {
            AddProjectileImpact(
                projectile,
                previousPosition,
                currentPosition,
                _playerShip.Id,
                _playerShip.Position,
                _playerShip.Rotation,
                _playerShip.Hitbox,
                playerImpactPosition,
                playerSurface,
                ShieldRatioAfterHit(_playerShip.Combat, projectile.Damage));
            ApplyDamageToShip(_playerShip.Id, projectile.Damage);
            return true;
        }

        if (projectile.OwnerId != _playerShip.Id)
        {
            return false;
        }

        for (var index = 0; index < _enemyShips.Count; index++)
        {
            var enemy = _enemyShips[index];
            if (!CanProjectileHit(projectile, enemy.Id)
                || enemy.IsDestroyed)
            {
                continue;
            }

            var enemySurface = SurfaceForCombat(enemy.Combat);
            if (!TryIntersectShipSurface(enemy.Position, enemy.Rotation, enemy.Hitbox, enemySurface, previousPosition, currentPosition, out var enemyImpactPosition))
            {
                continue;
            }

            AddProjectileImpact(
                projectile,
                previousPosition,
                currentPosition,
                enemy.Id,
                enemy.Position,
                enemy.Rotation,
                enemy.Hitbox,
                enemyImpactPosition,
                enemySurface,
                ShieldRatioAfterHit(enemy.Combat, projectile.Damage));
            ApplyDamageToShipState(ref enemy, projectile.Damage, protectedByGodMode: false);
            _enemyShips[index] = enemy;
            return true;
        }

        return false;
    }

    private static bool TryIntersectShipSurface(
        Vector2 shipPosition,
        float shipRotation,
        ShipHitbox hitbox,
        ProjectileImpactSurface surface,
        Vector2 start,
        Vector2 end,
        out Vector2 impactPosition)
    {
        if (surface == ProjectileImpactSurface.Shield)
        {
            var shieldCenter = hitbox.WorldCenter(shipPosition, shipRotation);
            var shieldExtents = ShieldHalfExtents(hitbox);
            if (!SegmentMayHitCircle(start, end, shieldCenter, shieldExtents.Length()))
            {
                impactPosition = Vector2.Zero;
                return false;
            }

            return TrySegmentEllipseEntryPoint(start, end, shieldCenter, shipRotation, shieldExtents, out impactPosition);
        }

        if (!SegmentMayHitCircle(start, end, shipPosition, hitbox.BoundingRadius))
        {
            impactPosition = Vector2.Zero;
            return false;
        }

        return hitbox.TryIntersectWorldSegment(shipPosition, shipRotation, start, end, out impactPosition);
    }

    private static bool SegmentMayHitCircle(Vector2 start, Vector2 end, Vector2 center, float radius)
    {
        var minX = MathF.Min(start.X, end.X) - radius;
        var maxX = MathF.Max(start.X, end.X) + radius;
        if (center.X < minX || center.X > maxX)
        {
            return false;
        }

        var minY = MathF.Min(start.Y, end.Y) - radius;
        var maxY = MathF.Max(start.Y, end.Y) + radius;
        return center.Y >= minY && center.Y <= maxY;
    }

    private static bool SegmentIntersectsCircle(Vector2 start, Vector2 end, Vector2 center, float radius)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f)
        {
            return Vector2.DistanceSquared(start, center) <= radius * radius;
        }

        var t = Math.Clamp(Vector2.Dot(center - start, segment) / lengthSquared, 0f, 1f);
        var closest = start + segment * t;
        return Vector2.DistanceSquared(closest, center) <= radius * radius;
    }

    private void AddProjectileImpact(
        ProjectileState projectile,
        Vector2 previousPosition,
        Vector2 currentPosition,
        int targetId,
        Vector2 targetPosition,
        float targetRotation,
        ShipHitbox targetHitbox,
        Vector2 hullImpactPosition,
        ProjectileImpactSurface surface,
        float shieldRatio)
    {
        var direction = SimulationMath.SafeNormalize(projectile.Velocity, currentPosition - previousPosition);
        var targetCenter = targetHitbox.WorldCenter(targetPosition, targetRotation);
        var targetRadius = targetHitbox.BoundingRadius;
        var impactPosition = surface == ProjectileImpactSurface.Shield
            ? SegmentEllipseEntryPoint(previousPosition, currentPosition, targetCenter, targetRotation, ShieldHalfExtents(targetHitbox), direction)
            : hullImpactPosition;
        _projectileImpacts.Add(new ProjectileImpactState(
            _nextProjectileImpactId++,
            targetId,
            surface,
            impactPosition,
            direction,
            targetCenter,
            targetRadius,
            targetHitbox.Size,
            targetRotation,
            Math.Clamp(shieldRatio, 0f, 1f),
            projectile.Damage,
            projectile.Velocity.Length(),
            ProjectileImpactKind.Projectile,
            projectile.Id * 92821 + _nextProjectileImpactId * 104729));
    }

    private void AddProjectileImpact(
        ProjectileState projectile,
        Vector2 previousPosition,
        Vector2 currentPosition,
        Vector2 targetCenter,
        float targetRadius,
        ProjectileImpactSurface surface,
        float shieldRatio)
    {
        var direction = SimulationMath.SafeNormalize(projectile.Velocity, currentPosition - previousPosition);
        var impactPosition = surface == ProjectileImpactSurface.Shield || surface == ProjectileImpactSurface.Asteroid
            ? SegmentCircleEntryPoint(previousPosition, currentPosition, targetCenter, targetRadius, direction)
            : ClosestPointOnSegment(previousPosition, currentPosition, targetCenter);
        _projectileImpacts.Add(new ProjectileImpactState(
            _nextProjectileImpactId++,
            0,
            surface,
            impactPosition,
            direction,
            targetCenter,
            targetRadius,
            new Vector2(targetRadius * 2f, targetRadius * 2f),
            0f,
            Math.Clamp(shieldRatio, 0f, 1f),
            projectile.Damage,
            projectile.Velocity.Length(),
            ProjectileImpactKind.Projectile,
            projectile.Id * 92821 + _nextProjectileImpactId * 104729));
    }

    private void AddShipDamageImpact(
        ShipState ship,
        Vector2 incomingDirection,
        float damage,
        float speed,
        ProjectileImpactKind kind,
        int seedBase)
    {
        var direction = SimulationMath.SafeNormalize(incomingDirection, SimulationMath.ForwardFromRotation(ship.Rotation));
        var surface = SurfaceForCombat(ship.Combat);
        var targetCenter = ship.Hitbox.WorldCenter(ship.Position, ship.Rotation);
        var traceDistance = MathF.Max(96f, ship.Hitbox.BoundingRadius + speed * _config.FixedDelta + 180f);
        var start = targetCenter - direction * traceDistance;
        var end = targetCenter + direction * MathF.Max(24f, ship.Hitbox.BoundingRadius * 0.18f);
        if (!TryIntersectShipSurface(ship.Position, ship.Rotation, ship.Hitbox, surface, start, end, out var impactPosition))
        {
            impactPosition = surface == ProjectileImpactSurface.Shield
                ? SegmentEllipseEntryPoint(start, end, targetCenter, ship.Rotation, ShieldHalfExtents(ship.Hitbox), direction)
                : targetCenter - direction * MathF.Max(_config.ShipRadius, ship.Hitbox.BoundingRadius);
        }

        _projectileImpacts.Add(new ProjectileImpactState(
            _nextProjectileImpactId++,
            ship.Id,
            surface,
            impactPosition,
            direction,
            targetCenter,
            ship.Hitbox.BoundingRadius,
            ship.Hitbox.Size,
            ship.Rotation,
            ShieldRatioAfterHit(ship.Combat, damage),
            MathF.Max(0f, damage),
            MathF.Max(0f, speed),
            kind,
            seedBase * 92821 + _nextProjectileImpactId * 104729));
    }

    private static Vector2 ClosestPointOnSegment(Vector2 start, Vector2 end, Vector2 point)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f)
        {
            return start;
        }

        var t = Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0f, 1f);
        return start + segment * t;
    }

    private static Vector2 SegmentCircleEntryPoint(Vector2 start, Vector2 end, Vector2 center, float radius, Vector2 fallbackDirection)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f)
        {
            return center - fallbackDirection * radius;
        }

        var toStart = start - center;
        var a = lengthSquared;
        var b = 2f * Vector2.Dot(toStart, segment);
        var c = toStart.LengthSquared() - radius * radius;
        var discriminant = b * b - 4f * a * c;
        if (discriminant >= 0f)
        {
            var root = MathF.Sqrt(discriminant);
            var inv = 1f / (2f * a);
            var t0 = (-b - root) * inv;
            var t1 = (-b + root) * inv;
            if (t0 >= 0f && t0 <= 1f)
            {
                return start + segment * t0;
            }

            if (t1 >= 0f && t1 <= 1f)
            {
                return start + segment * t1;
            }
        }

        return center - fallbackDirection * radius;
    }

    private static Vector2 SegmentEllipseEntryPoint(Vector2 start, Vector2 end, Vector2 center, float rotation, Vector2 halfExtents, Vector2 fallbackDirection)
    {
        return TrySegmentEllipseEntryPoint(start, end, center, rotation, halfExtents, out var impactPosition)
            ? impactPosition
            : PointOnEllipse(center, rotation, new Vector2(MathF.Max(1f, halfExtents.X), MathF.Max(1f, halfExtents.Y)), -fallbackDirection);
    }

    private static bool TrySegmentEllipseEntryPoint(Vector2 start, Vector2 end, Vector2 center, float rotation, Vector2 halfExtents, out Vector2 impactPosition)
    {
        var extents = new Vector2(MathF.Max(1f, halfExtents.X), MathF.Max(1f, halfExtents.Y));
        var localStart = Rotate(start - center, -rotation);
        var localEnd = Rotate(end - center, -rotation);
        var segment = localEnd - localStart;

        var a2 = extents.X * extents.X;
        var b2 = extents.Y * extents.Y;
        var quadraticA = segment.X * segment.X / a2 + segment.Y * segment.Y / b2;
        if (quadraticA <= 0.000001f)
        {
            var isInside = localStart.X * localStart.X / a2 + localStart.Y * localStart.Y / b2 <= 1f;
            impactPosition = isInside ? start : Vector2.Zero;
            return isInside;
        }

        var quadraticB = 2f * (localStart.X * segment.X / a2 + localStart.Y * segment.Y / b2);
        var quadraticC = localStart.X * localStart.X / a2 + localStart.Y * localStart.Y / b2 - 1f;
        if (quadraticC <= 0f)
        {
            impactPosition = start;
            return true;
        }

        var discriminant = quadraticB * quadraticB - 4f * quadraticA * quadraticC;
        if (discriminant >= 0f)
        {
            var root = MathF.Sqrt(discriminant);
            var inv = 1f / (2f * quadraticA);
            var t0 = (-quadraticB - root) * inv;
            var t1 = (-quadraticB + root) * inv;
            if (t0 >= 0f && t0 <= 1f)
            {
                impactPosition = start + (end - start) * t0;
                return true;
            }

            if (t1 >= 0f && t1 <= 1f)
            {
                impactPosition = start + (end - start) * t1;
                return true;
            }
        }

        impactPosition = Vector2.Zero;
        return false;
    }

    private static Vector2 ShieldHalfExtents(ShipHitbox hitbox)
    {
        var halfX = MathF.Max(4f, hitbox.HalfWidth);
        var halfY = MathF.Max(4f, hitbox.HalfHeight);
        var major = MathF.Max(halfX, halfY);
        var roundedMinor = major * 0.60f;
        var padding = Math.Clamp(6f + major * 0.16f, 8f, 26f);
        var minExtent = Math.Clamp(major * 0.72f + padding * 0.45f, 22f, 58f);
        return new Vector2(
            Math.Clamp(MathF.Max(halfX, roundedMinor) + padding, minExtent, 210f),
            Math.Clamp(MathF.Max(halfY, roundedMinor) + padding, minExtent, 210f));
    }

    private static Vector2 PointOnEllipse(Vector2 center, float rotation, Vector2 halfExtents, Vector2 worldDirection)
    {
        var direction = worldDirection.LengthSquared() > 0.0001f
            ? Vector2.Normalize(worldDirection)
            : new Vector2(0f, -1f);
        var localDirection = Rotate(direction, -rotation);
        var denom = MathF.Sqrt(
            localDirection.X * localDirection.X / (halfExtents.X * halfExtents.X)
            + localDirection.Y * localDirection.Y / (halfExtents.Y * halfExtents.Y));
        if (denom <= 0.000001f)
        {
            return center;
        }

        return center + Rotate(localDirection / denom, rotation);
    }

    private static Vector2 Rotate(Vector2 value, float radians)
    {
        var sin = MathF.Sin(radians);
        var cos = MathF.Cos(radians);
        return new Vector2(
            value.X * cos - value.Y * sin,
            value.X * sin + value.Y * cos);
    }

    private static ProjectileImpactSurface SurfaceForCombat(CombatStats combat)
    {
        if (combat.Shield > 0f)
        {
            return ProjectileImpactSurface.Shield;
        }

        if (combat.Armor > 0f)
        {
            return ProjectileImpactSurface.Armor;
        }

        return ProjectileImpactSurface.Structure;
    }

    private static float ShieldRatioAfterHit(CombatStats combat, float damage)
    {
        if (combat.MaxShield <= 0f)
        {
            return 0f;
        }

        return Math.Clamp((combat.Shield - MathF.Max(0f, damage)) / combat.MaxShield, 0f, 1f);
    }

    private static float Lerp(float from, float to, float amount)
    {
        return from + (to - from) * amount;
    }

    private bool CanProjectileHit(ProjectileState projectile, int targetShipId)
    {
        if (projectile.OwnerId == targetShipId)
        {
            return false;
        }

        if (projectile.OwnerId == _playerShip.Id)
        {
            return targetShipId != _playerShip.Id;
        }

        return targetShipId == _playerShip.Id;
    }

    private bool TryHitAsteroid(ProjectileState projectile, Vector2 previousPosition, Vector2 currentPosition)
    {
        for (var index = 0; index < _asteroids.Count; index++)
        {
            var asteroid = _asteroids[index];
            if (asteroid.IsDestroyed
                || !SegmentMayHitCircle(previousPosition, currentPosition, asteroid.Position, asteroid.Radius)
                || !SegmentIntersectsCircle(previousPosition, currentPosition, asteroid.Position, asteroid.Radius))
            {
                continue;
            }

            AddProjectileImpact(projectile, previousPosition, currentPosition, asteroid.Position, asteroid.Radius, ProjectileImpactSurface.Asteroid, 0f);
            asteroid.Structure = MathF.Max(0f, asteroid.Structure - projectile.Damage);
            _asteroids[index] = asteroid;
            if (asteroid.IsDestroyed)
            {
                DestroyAsteroid(index, AsteroidEventType.RockExplosion);
            }

            return true;
        }

        return false;
    }

    private void StepAsteroidSpawning(float dt)
    {
        if (!_config.AsteroidsEnabled || _config.AsteroidMaxActiveCount <= 0)
        {
            return;
        }

        while (_asteroids.Count < Math.Min(_config.AsteroidMinActiveCount, _config.AsteroidMaxActiveCount))
        {
            if (!TrySpawnAsteroid(prewarmSeconds: 0f))
            {
                break;
            }
        }

        if (_asteroids.Count >= _config.AsteroidMaxActiveCount)
        {
            return;
        }

        _asteroidSpawnTimer -= dt;
        if (_asteroidSpawnTimer > 0f)
        {
            return;
        }

        TrySpawnAsteroid(prewarmSeconds: 0f);
        _asteroidSpawnTimer = RandomAsteroidSpawnInterval();
    }

    private void StepAsteroids(float dt)
    {
        if (_asteroids.Count == 0)
        {
            return;
        }

        _asteroidPreviousPositions.Clear();
        for (var index = 0; index < _asteroids.Count; index++)
        {
            _asteroidPreviousPositions.Add(_asteroids[index].Position);
        }

        for (var index = 0; index < _asteroids.Count; index++)
        {
            var asteroid = _asteroids[index];
            if (asteroid.IsDestroyed)
            {
                continue;
            }

            asteroid.Velocity += AsteroidPhysics.SolarGravity(asteroid.Position, _config) * dt;
            asteroid.Position += asteroid.Velocity * dt;
            asteroid.Rotation = NormalizeAngle(asteroid.Rotation + asteroid.AngularVelocity * dt);

            var heatTarget = AsteroidPhysics.HeatRatio(asteroid.Position, _config);
            var heatRate = heatTarget > asteroid.Heat ? 3.4f : 1.1f;
            asteroid.Heat = SimulationMath.Approach(asteroid.Heat, heatTarget, heatRate * dt);

            if (AsteroidPhysics.IsOutsideRemovalBounds(asteroid.Position, asteroid.Radius, _config))
            {
                asteroid.Structure = -1f;
                _asteroids[index] = asteroid;
                continue;
            }

            if (AsteroidPhysics.IsInsideSunBurnZone(asteroid.Position, asteroid.Radius, _config))
            {
                var burnDamage = _config.AsteroidBurnDamagePerSecond + asteroid.MaxStructure * 1.35f;
                asteroid.Structure = MathF.Max(0f, asteroid.Structure - burnDamage * dt);
            }

            _asteroids[index] = asteroid;
            if (asteroid.IsDestroyed)
            {
                DestroyAsteroid(index, AsteroidEventType.SunBurn);
            }
        }

        CheckAsteroidShipCollisions();
        CheckAsteroidAsteroidCollisions();
        RemoveInactiveAsteroids();
    }

    private void CheckAsteroidShipCollisions()
    {
        for (var asteroidIndex = 0; asteroidIndex < _asteroids.Count; asteroidIndex++)
        {
            var asteroid = _asteroids[asteroidIndex];
            if (asteroid.IsDestroyed)
            {
                continue;
            }

            var previousPosition = _asteroidPreviousPositions[asteroidIndex];
            if (!_playerShip.IsDestroyed
                && SegmentIntersectsCircle(
                    previousPosition,
                    asteroid.Position,
                    _playerShip.Position,
                    asteroid.Radius + _playerShip.Hitbox.BoundingRadius))
            {
                AddAsteroidShipImpact(_playerShip, asteroid, previousPosition);
                ApplyDamageToShip(_playerShip.Id, asteroid.CollisionDamage);
                DestroyAsteroid(asteroidIndex, AsteroidEventType.ShipImpact);
                continue;
            }

            for (var shipIndex = 0; shipIndex < _enemyShips.Count; shipIndex++)
            {
                var enemy = _enemyShips[shipIndex];
                if (enemy.IsDestroyed
                    || !SegmentIntersectsCircle(
                        previousPosition,
                        asteroid.Position,
                        enemy.Position,
                        asteroid.Radius + enemy.Hitbox.BoundingRadius))
                {
                    continue;
                }

                AddAsteroidShipImpact(enemy, asteroid, previousPosition);
                ApplyDamageToShipState(ref enemy, asteroid.CollisionDamage, protectedByGodMode: false);
                _enemyShips[shipIndex] = enemy;
                DestroyAsteroid(asteroidIndex, AsteroidEventType.ShipImpact);
                break;
            }
        }
    }

    private void CheckShipShipCollisions()
    {
        for (var enemyIndex = 0; enemyIndex < _enemyShips.Count; enemyIndex++)
        {
            var enemy = _enemyShips[enemyIndex];
            var player = _playerShip;
            if (!ResolveShipCollision(ref player, ref enemy))
            {
                continue;
            }

            _playerShip = player;
            _enemyShips[enemyIndex] = enemy;
        }

        for (var firstIndex = 0; firstIndex < _enemyShips.Count; firstIndex++)
        {
            var first = _enemyShips[firstIndex];
            for (var secondIndex = firstIndex + 1; secondIndex < _enemyShips.Count; secondIndex++)
            {
                var second = _enemyShips[secondIndex];
                if (!ResolveShipCollision(ref first, ref second))
                {
                    continue;
                }

                _enemyShips[firstIndex] = first;
                _enemyShips[secondIndex] = second;
            }
        }
    }

    private bool ResolveShipCollision(ref ShipState first, ref ShipState second)
    {
        if (first.IsDestroyed || second.IsDestroyed)
        {
            return false;
        }

        var radiusSum = first.Hitbox.BoundingRadius + second.Hitbox.BoundingRadius;
        var roughDelta = second.Position - first.Position;
        if (roughDelta.LengthSquared() > radiusSum * radiusSum)
        {
            return false;
        }

        if (!TryGetShipCollisionManifold(first, second, out var normal, out var penetration))
        {
            return false;
        }

        SeparateShips(ref first, ref second, normal, penetration);

        var closingSpeed = Vector2.Dot(first.Velocity - second.Velocity, normal);
        if (closingSpeed <= 0f)
        {
            return true;
        }

        var pairKey = ShipPairKey(first.Id, second.Id);
        var canDamage = closingSpeed > _config.ShipCollisionDamageSpeedThreshold
            && (!_nextShipCollisionDamageTicks.TryGetValue(pairKey, out var nextDamageTick) || _tick >= nextDamageTick);

        ApplyShipCollisionImpulse(ref first, ref second, normal, closingSpeed, canDamage);
        if (!canDamage)
        {
            return true;
        }

        var damageToFirst = ShipCollisionDamage(second, closingSpeed);
        var damageToSecond = ShipCollisionDamage(first, closingSpeed);
        if (damageToFirst > 0f)
        {
            AddShipDamageImpact(first, -normal, damageToFirst, closingSpeed, ProjectileImpactKind.ShipCollision, first.Id * 173 + second.Id * 257 + (int)_tick);
            ApplyDamageToShipState(ref first, damageToFirst, first.Id == _playerShip.Id && PlayerGodMode);
        }

        if (damageToSecond > 0f)
        {
            AddShipDamageImpact(second, normal, damageToSecond, closingSpeed, ProjectileImpactKind.ShipCollision, second.Id * 173 + first.Id * 257 + (int)_tick);
            ApplyDamageToShipState(ref second, damageToSecond, second.Id == _playerShip.Id && PlayerGodMode);
        }

        var cooldownTicks = Math.Max(1L, (long)MathF.Ceiling(_config.ShipCollisionDamageCooldown / _config.FixedDelta));
        _nextShipCollisionDamageTicks[pairKey] = _tick + cooldownTicks;
        return true;
    }

    private void SeparateShips(ref ShipState first, ref ShipState second, Vector2 normal, float penetration)
    {
        if (penetration <= 0f)
        {
            return;
        }

        var firstMass = ShipCollisionMass(first);
        var secondMass = ShipCollisionMass(second);
        var totalMass = firstMass + secondMass;
        if (totalMass <= 0f)
        {
            return;
        }

        const float slop = 1.5f;
        var correction = MathF.Min(
            MathF.Max(0f, penetration - slop) * Math.Clamp(_config.ShipCollisionSeparationPercent, 0f, 1f),
            MathF.Max(0f, _config.ShipCollisionMaxCorrection));
        first.Position -= normal * correction * (secondMass / totalMass);
        second.Position += normal * correction * (firstMass / totalMass);
    }

    private void ApplyShipCollisionImpulse(ref ShipState first, ref ShipState second, Vector2 normal, float closingSpeed, bool fullImpact)
    {
        var firstMass = ShipCollisionMass(first);
        var secondMass = ShipCollisionMass(second);
        var invFirstMass = 1f / firstMass;
        var invSecondMass = 1f / secondMass;
        var restitution = Math.Clamp(_config.ShipCollisionRestitution, 0f, 0.75f) * (fullImpact ? 1f : 0.24f);
        var impulse = (1f + restitution) * closingSpeed / (invFirstMass + invSecondMass);
        impulse *= Math.Clamp(_config.ShipCollisionImpulseScale, 0f, 2f);
        var impulseVector = normal * impulse;
        first.Velocity -= impulseVector * invFirstMass;
        second.Velocity += impulseVector * invSecondMass;
    }

    private float ShipCollisionDamage(ShipState source, float closingSpeed)
    {
        var excessSpeed = closingSpeed - _config.ShipCollisionDamageSpeedThreshold;
        if (excessSpeed <= 0f)
        {
            return 0f;
        }

        var speedRatio = excessSpeed / MathF.Max(1f, _config.ShipCollisionDamageReferenceSpeed);
        return MathF.Max(1f, source.Combat.Structure)
            * speedRatio
            * speedRatio
            * MathF.Max(0f, _config.ShipCollisionDamageScale);
    }

    private static bool TryGetShipCollisionManifold(ShipState first, ShipState second, out Vector2 normal, out float penetration)
    {
        var firstCenter = first.Hitbox.WorldCenter(first.Position, first.Rotation);
        var secondCenter = second.Hitbox.WorldCenter(second.Position, second.Rotation);
        var firstRight = SimulationMath.RightFromRotation(first.Rotation);
        var firstForward = SimulationMath.ForwardFromRotation(first.Rotation);
        var secondRight = SimulationMath.RightFromRotation(second.Rotation);
        var secondForward = SimulationMath.ForwardFromRotation(second.Rotation);
        var centerDelta = secondCenter - firstCenter;
        normal = Vector2.Zero;
        penetration = float.PositiveInfinity;

        return TryAccumulateCollisionAxis(centerDelta, first.Hitbox, firstRight, firstForward, second.Hitbox, secondRight, secondForward, firstRight, ref normal, ref penetration)
            && TryAccumulateCollisionAxis(centerDelta, first.Hitbox, firstRight, firstForward, second.Hitbox, secondRight, secondForward, firstForward, ref normal, ref penetration)
            && TryAccumulateCollisionAxis(centerDelta, first.Hitbox, firstRight, firstForward, second.Hitbox, secondRight, secondForward, secondRight, ref normal, ref penetration)
            && TryAccumulateCollisionAxis(centerDelta, first.Hitbox, firstRight, firstForward, second.Hitbox, secondRight, secondForward, secondForward, ref normal, ref penetration)
            && penetration < float.PositiveInfinity;
    }

    private static bool TryAccumulateCollisionAxis(
        Vector2 centerDelta,
        ShipHitbox firstHitbox,
        Vector2 firstRight,
        Vector2 firstForward,
        ShipHitbox secondHitbox,
        Vector2 secondRight,
        Vector2 secondForward,
        Vector2 axis,
        ref Vector2 normal,
        ref float penetration)
    {
        var firstProjection = firstHitbox.HalfWidth * MathF.Abs(Vector2.Dot(firstRight, axis))
            + firstHitbox.HalfHeight * MathF.Abs(Vector2.Dot(firstForward, axis));
        var secondProjection = secondHitbox.HalfWidth * MathF.Abs(Vector2.Dot(secondRight, axis))
            + secondHitbox.HalfHeight * MathF.Abs(Vector2.Dot(secondForward, axis));
        var signedDistance = Vector2.Dot(centerDelta, axis);
        var overlap = firstProjection + secondProjection - MathF.Abs(signedDistance);
        if (overlap <= 0f)
        {
            return false;
        }

        if (overlap < penetration)
        {
            penetration = overlap;
            normal = signedDistance >= 0f ? axis : -axis;
        }

        return true;
    }

    private static float ShipCollisionMass(ShipState ship)
    {
        return MathF.Max(100f, ship.Combat.Structure);
    }

    private static Vector2 CollisionFallbackNormal(int firstId, int secondId)
    {
        var seed = firstId * 92821 + secondId * 68917;
        var angle = (seed % 360) * (MathF.PI / 180f);
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    private static ulong ShipPairKey(int firstId, int secondId)
    {
        var a = (uint)Math.Min(firstId, secondId);
        var b = (uint)Math.Max(firstId, secondId);
        return ((ulong)a << 32) | b;
    }

    private void AddAsteroidShipImpact(ShipState ship, AsteroidState asteroid, Vector2 asteroidPreviousPosition)
    {
        var asteroidMotion = asteroid.Position - asteroidPreviousPosition;
        var incomingDirection = asteroidMotion.LengthSquared() > 0.0001f
            ? asteroidMotion
            : ship.Hitbox.WorldCenter(ship.Position, ship.Rotation) - asteroid.Position;
        var relativeSpeed = MathF.Max(asteroid.Velocity.Length(), _config.AsteroidMinSpeed);
        AddShipDamageImpact(
            ship,
            incomingDirection,
            asteroid.CollisionDamage,
            relativeSpeed,
            ProjectileImpactKind.AsteroidCollision,
            asteroid.Id * 131 + asteroid.Seed);
    }

    private void CheckAsteroidAsteroidCollisions()
    {
        for (var firstIndex = 0; firstIndex < _asteroids.Count; firstIndex++)
        {
            var first = _asteroids[firstIndex];
            if (first.IsDestroyed)
            {
                continue;
            }

            for (var secondIndex = firstIndex + 1; secondIndex < _asteroids.Count; secondIndex++)
            {
                var second = _asteroids[secondIndex];
                if (second.IsDestroyed)
                {
                    continue;
                }

                var previousRelative = _asteroidPreviousPositions[firstIndex] - _asteroidPreviousPositions[secondIndex];
                var currentRelative = first.Position - second.Position;
                if (!SegmentIntersectsCircle(previousRelative, currentRelative, Vector2.Zero, first.Radius + second.Radius))
                {
                    continue;
                }

                DestroyAsteroid(firstIndex, AsteroidEventType.RockExplosion);
                DestroyAsteroid(secondIndex, AsteroidEventType.RockExplosion);
                break;
            }
        }
    }

    private void DestroyAsteroid(int index, AsteroidEventType eventType)
    {
        var asteroid = _asteroids[index];
        if (asteroid.Structure < -0.5f)
        {
            return;
        }

        asteroid.Structure = 0f;
        _asteroids[index] = asteroid;
        if (HasAsteroidEvent(asteroid.Id))
        {
            return;
        }

        _asteroidEvents.Add(new AsteroidEventState(
            asteroid.Id,
            eventType,
            asteroid.Position,
            asteroid.Radius,
            asteroid.Variant,
            asteroid.Seed,
            asteroid.Rotation,
            asteroid.Heat));
    }

    private bool HasAsteroidEvent(int asteroidId)
    {
        for (var index = 0; index < _asteroidEvents.Count; index++)
        {
            if (_asteroidEvents[index].Id == asteroidId)
            {
                return true;
            }
        }

        return false;
    }

    private void RemoveInactiveAsteroids()
    {
        for (var index = _asteroids.Count - 1; index >= 0; index--)
        {
            if (_asteroids[index].Structure <= 0f)
            {
                _asteroids.RemoveAt(index);
            }
        }
    }

    private bool TrySpawnAsteroid(float prewarmSeconds)
    {
        if (_asteroids.Count >= _config.AsteroidMaxActiveCount)
        {
            return false;
        }

        for (var attempt = 0; attempt < 16; attempt++)
        {
            var asteroid = CreateRandomAsteroid();
            if (prewarmSeconds > 0f)
            {
                asteroid = PrewarmAsteroid(asteroid, prewarmSeconds);
            }

            if (AsteroidPhysics.IsOutsideRemovalBounds(asteroid.Position, asteroid.Radius, _config)
                || AsteroidPhysics.IsInsideSunBurnZone(asteroid.Position, asteroid.Radius, _config)
                || Vector2.DistanceSquared(asteroid.Position, _playerShip.Position) < 1800f * 1800f
                || OverlapsExistingAsteroid(asteroid))
            {
                continue;
            }

            _asteroids.Add(asteroid);
            return true;
        }

        return false;
    }

    private AsteroidState CreateRandomAsteroid()
    {
        var sizeRoll = MathF.Pow(RandomRange(0f, 1f), 1.55f);
        var referenceDiameter = Lerp(_config.AsteroidMinReferenceDiameter, _config.AsteroidMaxReferenceDiameter, sizeRoll);
        var radius = AsteroidPhysics.ReferenceDiameterToWorld(referenceDiameter) * 0.5f;
        var sizeRatio = AsteroidSizeRatio(referenceDiameter);
        var maxStructure = StructureForReferenceDiameter(referenceDiameter);
        var speed = Lerp(_config.AsteroidMinSpeed, _config.AsteroidMaxSpeed, MathF.Pow(RandomRange(0f, 1f), 1.35f));
        var side = _random.Next(4);
        var spawnPadding = radius + 90f;
        Vector2 position = side switch
        {
            0 => new Vector2(RandomRange(-_config.Bounds.HalfWidth, _config.Bounds.HalfWidth), -_config.Bounds.HalfHeight - spawnPadding),
            1 => new Vector2(_config.Bounds.HalfWidth + spawnPadding, RandomRange(-_config.Bounds.HalfHeight, _config.Bounds.HalfHeight)),
            2 => new Vector2(RandomRange(-_config.Bounds.HalfWidth, _config.Bounds.HalfWidth), _config.Bounds.HalfHeight + spawnPadding),
            _ => new Vector2(-_config.Bounds.HalfWidth - spawnPadding, RandomRange(-_config.Bounds.HalfHeight, _config.Bounds.HalfHeight))
        };

        var toSun = SimulationMath.SafeNormalize(-position, new Vector2(0f, -1f));
        var tangent = new Vector2(-toSun.Y, toSun.X);
        var tangentWeight = RandomRange(-0.95f, 0.95f);
        var inwardWeight = RandomRange(0.46f, 1.0f);
        if (RandomRange(0f, 1f) < 0.24f)
        {
            tangentWeight = MathF.CopySign(RandomRange(1.0f, 1.45f), tangentWeight == 0f ? 1f : tangentWeight);
            inwardWeight = RandomRange(0.18f, 0.42f);
        }

        var direction = SimulationMath.SafeNormalize(toSun * inwardWeight + tangent * tangentWeight, toSun);
        var angularVelocity = RandomRange(-1.65f, 1.65f) * Lerp(1.1f, 0.48f, sizeRatio);
        return new AsteroidState(
            _nextAsteroidId++,
            position,
            direction * speed,
            radius,
            RandomRange(0f, MathF.Tau),
            angularVelocity,
            maxStructure,
            maxStructure,
            0f,
            _random.Next(Math.Max(1, _config.AsteroidVariantCount)),
            _random.Next());
    }

    private float StructureForReferenceDiameter(float referenceDiameter)
    {
        var sizeRatio = AsteroidSizeRatio(referenceDiameter);
        return Math.Clamp(_config.AsteroidMaxStructure * (0.10f + MathF.Pow(sizeRatio, 1.25f) * 0.90f), 90f, _config.AsteroidMaxStructure);
    }

    private float AsteroidSizeRatio(float referenceDiameter)
    {
        return Math.Clamp(
            (referenceDiameter - _config.AsteroidMinReferenceDiameter)
            / MathF.Max(0.001f, _config.AsteroidMaxReferenceDiameter - _config.AsteroidMinReferenceDiameter),
            0f,
            1f);
    }

    private AsteroidState PrewarmAsteroid(AsteroidState asteroid, float seconds)
    {
        const float step = 0.12f;
        var iterations = Math.Clamp((int)(seconds / step), 0, 220);
        for (var i = 0; i < iterations; i++)
        {
            asteroid.Velocity += AsteroidPhysics.SolarGravity(asteroid.Position, _config) * step;
            asteroid.Position += asteroid.Velocity * step;
            asteroid.Rotation = NormalizeAngle(asteroid.Rotation + asteroid.AngularVelocity * step);
            asteroid.Heat = AsteroidPhysics.HeatRatio(asteroid.Position, _config);
            if (AsteroidPhysics.IsOutsideRemovalBounds(asteroid.Position, asteroid.Radius, _config)
                || AsteroidPhysics.IsInsideSunBurnZone(asteroid.Position, asteroid.Radius, _config))
            {
                break;
            }
        }

        return asteroid;
    }

    private bool OverlapsExistingAsteroid(AsteroidState asteroid)
    {
        for (var index = 0; index < _asteroids.Count; index++)
        {
            var existing = _asteroids[index];
            var minDistance = asteroid.Radius + existing.Radius + 220f;
            if (Vector2.DistanceSquared(asteroid.Position, existing.Position) < minDistance * minDistance)
            {
                return true;
            }
        }

        return false;
    }

    private float RandomAsteroidSpawnInterval()
    {
        return RandomRange(_config.AsteroidSpawnIntervalMin, MathF.Max(_config.AsteroidSpawnIntervalMin, _config.AsteroidSpawnIntervalMax));
    }

    private float RandomRange(float min, float max)
    {
        return min + (max - min) * _random.NextSingle();
    }

    private void RegenerateShipsCombat(float dt)
    {
        _playerShip.Combat = _playerShip.Combat.RegenerateShield(dt, _config.ShieldRegenerationPerSecond);
        for (var index = 0; index < _enemyShips.Count; index++)
        {
            var enemy = _enemyShips[index];
            enemy.Combat = enemy.Combat.RegenerateShield(dt, _config.ShieldRegenerationPerSecond);
            _enemyShips[index] = enemy;
        }
    }

    private void ApplySunBurnDamage(float dt)
    {
        ApplySunBurnDamageToShip(ref _playerShip, dt, PlayerGodMode);
        for (var index = 0; index < _enemyShips.Count; index++)
        {
            var enemy = _enemyShips[index];
            ApplySunBurnDamageToShip(ref enemy, dt, protectedByGodMode: false);
            _enemyShips[index] = enemy;
        }
    }

    private void ApplySunBurnDamageToShip(ref ShipState ship, float dt, bool protectedByGodMode)
    {
        if (dt <= 0f || ship.IsDestroyed || protectedByGodMode)
        {
            return;
        }

        var shipRadius = MathF.Max(_config.ShipRadius, ship.Hitbox.BoundingRadius);
        var damagePerSecond = AsteroidPhysics.ShipSunBurnDamagePerSecond(ship.Position, shipRadius, _config);
        if (damagePerSecond <= 0f)
        {
            return;
        }

        var damage = damagePerSecond * dt;
        if (ShouldEmitSunBurnImpact(ship.Id, damagePerSecond))
        {
            var sunDirection = SimulationMath.SafeNormalize(
                ship.Hitbox.WorldCenter(ship.Position, ship.Rotation),
                SimulationMath.ForwardFromRotation(ship.Rotation));
            AddShipDamageImpact(
                ship,
                sunDirection,
                Math.Clamp(
                    damagePerSecond,
                    MathF.Max(0f, _config.SunBurnDamageMinPerSecond),
                    MathF.Max(MathF.Max(0f, _config.SunBurnDamageMinPerSecond), _config.SunBurnDamageMaxPerSecond)),
                860f + damagePerSecond * 5.6f,
                ProjectileImpactKind.SunBurn,
                ship.Id * 157 + (int)_tick);
        }

        ship.Combat = ship.Combat.ApplyDamage(damage, _config.ShieldZeroRegenerationLockout);
        if (ship.IsDestroyed)
        {
            ship.Velocity *= 0.15f;
        }
    }

    private bool ShouldEmitSunBurnImpact(int shipId, float damagePerSecond)
    {
        if (_nextSunBurnImpactTicks.TryGetValue(shipId, out var nextTick) && _tick < nextTick)
        {
            return false;
        }

        var heat = Math.Clamp(
            (damagePerSecond - _config.SunBurnDamageMinPerSecond)
            / MathF.Max(0.001f, _config.SunBurnDamageMaxPerSecond - _config.SunBurnDamageMinPerSecond),
            0f,
            1f);
        var intervalTicks = (long)Math.Clamp(MathF.Round(Lerp(24f, 9f, heat)), 8f, 28f);
        _nextSunBurnImpactTicks[shipId] = _tick + intervalTicks;
        return true;
    }

    private void RemoveDestroyedEnemyShips()
    {
        for (var index = _enemyShips.Count - 1; index >= 0; index--)
        {
            if (!_enemyShips[index].IsDestroyed)
            {
                continue;
            }

            _lastCommands.Remove(_enemyShips[index].Id);
            _nextSunBurnImpactTicks.Remove(_enemyShips[index].Id);
            RemoveShipCollisionCooldownsFor(_enemyShips[index].Id);
            _enemyShips.RemoveAt(index);
        }
    }

    private void RemoveShipCollisionCooldownsFor(int shipId)
    {
        if (_nextShipCollisionDamageTicks.Count == 0)
        {
            return;
        }

        var id = (uint)shipId;
        foreach (var key in _nextShipCollisionDamageTicks.Keys.ToArray())
        {
            if ((uint)(key >> 32) == id || (uint)key == id)
            {
                _nextShipCollisionDamageTicks.Remove(key);
            }
        }
    }

    private void PruneExpiredShipCollisionCooldowns()
    {
        if (_nextShipCollisionDamageTicks.Count == 0 || (_tick & 127) != 0)
        {
            return;
        }

        foreach (var pair in _nextShipCollisionDamageTicks.ToArray())
        {
            if (pair.Value <= _tick)
            {
                _nextShipCollisionDamageTicks.Remove(pair.Key);
            }
        }
    }

    private InputCommand BuildEnemyCommand(ShipState enemy, ShipState target)
    {
        if (enemy.IsDestroyed || target.IsDestroyed)
        {
            return InputCommand.Idle(target.Position);
        }

        var toTarget = target.Position - enemy.Position;
        var distance = toTarget.Length();
        var boundaryDirection = BoundaryRecoveryDirection(enemy.Position);
        var avoidingBoundary = boundaryDirection.LengthSquared() > 0.001f;
        var avoidanceForce = CollisionAvoidanceForce(enemy);
        var avoidanceStrength = avoidanceForce.Length();
        var avoidingCollision = avoidanceStrength > 0.001f;
        var targetDirection = SimulationMath.SafeNormalize(toTarget, SimulationMath.ForwardFromRotation(enemy.Rotation));
        var desiredDirection = avoidingBoundary
            ? SimulationMath.SafeNormalize(boundaryDirection, SimulationMath.ForwardFromRotation(enemy.Rotation))
            : avoidingCollision
                ? SimulationMath.SafeNormalize(
                    targetDirection * Math.Clamp(1f - avoidanceStrength * 0.32f, 0.22f, 1f)
                    + SimulationMath.SafeNormalize(avoidanceForce, targetDirection) * Math.Clamp(avoidanceStrength, 0.25f, 1.45f),
                    targetDirection)
                : targetDirection;
        var desiredRotation = RotationFromForward(desiredDirection);
        var angle = NormalizeAngle(desiredRotation - enemy.Rotation);
        var absAngle = MathF.Abs(angle);
        var turn = Math.Clamp(angle / 0.55f, -1f, 1f);

        var wantsNavigation = avoidingBoundary || avoidanceStrength > 1.18f || distance > 1450f;
        var wantsCombat = !avoidingBoundary && avoidanceStrength < 1.05f && distance <= 1150f;
        var toggleMode = false;
        if (enemy.ModeSwitchCooldown <= 0f)
        {
            if (wantsCombat && enemy.Mode == ShipMode.Navigation)
            {
                toggleMode = true;
            }
            else if (wantsNavigation && enemy.Mode == ShipMode.Combat)
            {
                toggleMode = true;
            }
        }

        var effectiveMode = toggleMode
            ? enemy.Mode == ShipMode.Navigation ? ShipMode.Combat : ShipMode.Navigation
            : enemy.Mode;

        var forward = 0f;
        var reverse = 0f;
        var strafe = 0f;
        var afterburner = false;
        var fire = false;
        var orbit = (enemy.Id & 1) == 0 ? 1f : -1f;
        var avoidanceDirection = avoidingCollision
            ? SimulationMath.SafeNormalize(avoidanceForce, desiredDirection)
            : Vector2.Zero;
        var avoidanceSide = avoidingCollision
            ? Math.Clamp(Vector2.Dot(SimulationMath.RightFromRotation(enemy.Rotation), avoidanceDirection), -1f, 1f)
            : 0f;

        if (avoidingBoundary)
        {
            forward = absAngle < 1.2f ? 1f : 0.28f;
            strafe = orbit * 0.2f;
        }
        else if (avoidanceStrength > 1.18f)
        {
            forward = absAngle < 1.05f ? 0.68f : 0.16f;
            reverse = Vector2.Dot(SimulationMath.ForwardFromRotation(enemy.Rotation), avoidanceDirection) < -0.35f ? 0.34f : 0f;
            strafe = Math.Clamp(avoidanceSide * 0.88f + orbit * 0.10f, -1f, 1f);
        }
        else if (effectiveMode == ShipMode.Combat)
        {
            if (distance > 900f)
            {
                forward = absAngle < 1.05f ? 0.74f : 0.18f;
            }
            else if (distance > 650f)
            {
                forward = absAngle < 0.85f ? 0.34f : 0f;
            }
            else if (distance < 480f)
            {
                reverse = 0.58f;
            }

            strafe = orbit * (distance < 1200f ? 0.34f : 0.16f);
            if (avoidingCollision)
            {
                strafe = Math.Clamp(strafe + avoidanceSide * Math.Clamp(avoidanceStrength * 0.38f, 0.12f, 0.46f), -1f, 1f);
                forward *= Math.Clamp(1f - avoidanceStrength * 0.18f, 0.54f, 1f);
            }

            if (absAngle > 0.8f)
            {
                strafe *= 0.35f;
            }

            fire = distance <= 1400f && absAngle < 0.22f && avoidanceStrength < 1.20f;
        }
        else
        {
            forward = absAngle < 1.15f ? 1f : 0.24f;
            strafe = distance < 1800f ? orbit * 0.12f : 0f;
            if (avoidingCollision)
            {
                strafe = Math.Clamp(strafe + avoidanceSide * Math.Clamp(avoidanceStrength * 0.44f, 0.16f, 0.62f), -1f, 1f);
                forward *= Math.Clamp(1f - avoidanceStrength * 0.14f, 0.62f, 1f);
            }

            afterburner = distance > 1600f && absAngle < 0.36f && avoidanceStrength < 0.65f;
        }

        return new InputCommand(forward, reverse, strafe, turn, target.Position, fire, afterburner, toggleMode);
    }

    private bool ShouldRefreshEnemyCommand(ShipState enemy)
    {
        var distanceSquared = Vector2.DistanceSquared(enemy.Position, _playerShip.Position);
        if (distanceSquared <= 1800f * 1800f)
        {
            return true;
        }

        var interval = distanceSquared <= 3600f * 3600f ? 4 : 12;
        return ((_tick + enemy.Id) % interval) == 0;
    }

    private InputCommand ReuseEnemyCommand(ShipState enemy)
    {
        return _lastCommands.TryGetValue(enemy.Id, out var command)
            ? command with { ToggleMode = false, AimWorld = _playerShip.Position }
            : InputCommand.Idle(_playerShip.Position);
    }

    private Vector2 CollisionAvoidanceForce(ShipState ship)
    {
        var force = Vector2.Zero;
        AddShipAvoidanceForce(ship, _playerShip, ref force);
        for (var index = 0; index < _enemyShips.Count; index++)
        {
            AddShipAvoidanceForce(ship, _enemyShips[index], ref force);
        }

        for (var index = 0; index < _asteroids.Count; index++)
        {
            var asteroid = _asteroids[index];
            if (asteroid.IsDestroyed)
            {
                continue;
            }

            var safeDistance = ship.Hitbox.BoundingRadius + asteroid.Radius + MathF.Max(0f, _config.ShipAiAsteroidAvoidanceMargin);
            var roughDelta = ship.Position - asteroid.Position;
            if (roughDelta.LengthSquared() >= safeDistance * safeDistance)
            {
                continue;
            }

            var shipCenter = ship.Hitbox.WorldCenter(ship.Position, ship.Rotation);
            var delta = shipCenter - asteroid.Position;
            var distanceSquared = delta.LengthSquared();
            if (distanceSquared >= safeDistance * safeDistance)
            {
                continue;
            }

            var distance = distanceSquared > 0.0001f ? MathF.Sqrt(distanceSquared) : 0f;
            var away = distance > 0.001f ? delta / distance : CollisionFallbackNormal(ship.Id, asteroid.Id);
            var relativeVelocity = ship.Velocity - asteroid.Velocity;
            var closing = MathF.Max(0f, -Vector2.Dot(relativeVelocity, away));
            var proximity = 1f - Math.Clamp(distance / MathF.Max(1f, safeDistance), 0f, 1f);
            force += away * (proximity * proximity * 1.28f + Math.Clamp(closing / 720f, 0f, 1.35f) * proximity);
        }

        return force;
    }

    private void AddShipAvoidanceForce(ShipState ship, ShipState obstacle, ref Vector2 force)
    {
        if (obstacle.Id == ship.Id || obstacle.IsDestroyed)
        {
            return;
        }

        var safeDistance = ship.Hitbox.BoundingRadius + obstacle.Hitbox.BoundingRadius + MathF.Max(0f, _config.ShipAiAvoidanceMargin);
        var roughDelta = ship.Position - obstacle.Position;
        if (roughDelta.LengthSquared() >= safeDistance * safeDistance)
        {
            return;
        }

        var shipCenter = ship.Hitbox.WorldCenter(ship.Position, ship.Rotation);
        var obstacleCenter = obstacle.Hitbox.WorldCenter(obstacle.Position, obstacle.Rotation);
        var delta = shipCenter - obstacleCenter;
        var distanceSquared = delta.LengthSquared();
        if (distanceSquared >= safeDistance * safeDistance)
        {
            return;
        }

        var distance = distanceSquared > 0.0001f ? MathF.Sqrt(distanceSquared) : 0f;
        var away = distance > 0.001f ? delta / distance : CollisionFallbackNormal(ship.Id, obstacle.Id);
        var relativeVelocity = ship.Velocity - obstacle.Velocity;
        var closing = MathF.Max(0f, -Vector2.Dot(relativeVelocity, away));
        var proximity = 1f - Math.Clamp(distance / MathF.Max(1f, safeDistance), 0f, 1f);
        force += away * (proximity * proximity * 1.72f + Math.Clamp(closing / 620f, 0f, 1.55f) * proximity);
    }

    private Vector2 BoundaryRecoveryDirection(Vector2 position)
    {
        const float margin = 1500f;
        var force = Vector2.Zero;
        var left = position.X + _config.Bounds.HalfWidth;
        var right = _config.Bounds.HalfWidth - position.X;
        var top = position.Y + _config.Bounds.HalfHeight;
        var bottom = _config.Bounds.HalfHeight - position.Y;

        if (left < margin)
        {
            force.X += 1f - Math.Clamp(left / margin, 0f, 1f);
        }

        if (right < margin)
        {
            force.X -= 1f - Math.Clamp(right / margin, 0f, 1f);
        }

        if (top < margin)
        {
            force.Y += 1f - Math.Clamp(top / margin, 0f, 1f);
        }

        if (bottom < margin)
        {
            force.Y -= 1f - Math.Clamp(bottom / margin, 0f, 1f);
        }

        return force;
    }

    private static float RotationFromForward(Vector2 direction)
    {
        return MathF.Atan2(direction.X, -direction.Y);
    }

    private static float NormalizeAngle(float angle)
    {
        return MathF.Atan2(MathF.Sin(angle), MathF.Cos(angle));
    }

    private CombatStats CreateFullCombatStats()
    {
        return new CombatStats(
            _config.MaxShield,
            _config.MaxArmor,
            _config.MaxStructure,
            _config.MaxShield,
            _config.MaxArmor,
            _config.MaxStructure,
            0f);
    }

    private WorldSnapshot CreateSnapshot()
    {
        var ships = new ShipState[_enemyShips.Count + 1];
        ships[0] = _playerShip;
        for (var index = 0; index < _enemyShips.Count; index++)
        {
            ships[index + 1] = _enemyShips[index];
        }

        return new WorldSnapshot(
            _tick,
            ships,
            _projectiles.Count == 0 ? Array.Empty<ProjectileState>() : _projectiles.ToArray(),
            _projectileImpacts.Count == 0 ? Array.Empty<ProjectileImpactState>() : _projectileImpacts.ToArray(),
            _config.Bounds,
            _asteroids.Count == 0 ? Array.Empty<AsteroidState>() : _asteroids.ToArray(),
            _asteroidEvents.Count == 0 ? Array.Empty<AsteroidEventState>() : _asteroidEvents.ToArray());
    }
}
