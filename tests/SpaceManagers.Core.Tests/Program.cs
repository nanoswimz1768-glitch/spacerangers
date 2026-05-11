using System.Numerics;
using SpaceManagers.Core;

var tests = new (string Name, Action Body)[]
{
    ("forward thrust accelerates along the nose", ForwardThrust),
    ("reverse thrust is slower and moves backward", ReverseThrust),
    ("strafe moves relative to the hull without rotation", Strafe),
    ("turn input rotates the ship", Turn),
    ("damping slows an unpowered ship", Damping),
    ("max speed clamps long acceleration", MaxSpeed),
    ("afterburner reaches boosted cap", AfterburnerMaxSpeed),
    ("afterburner acceleration softens above normal cap", AfterburnerAccelerationSoftens),
    ("afterburner release keeps inertia", AfterburnerReleaseKeepsInertia),
    ("projectiles fire toward the cursor", ProjectileDirection),
    ("projectiles expire after lifetime", ProjectileLifetime),
    ("projectile count is capped", ProjectileCap),
    ("damage drains shield armor structure in order", DamageOrder),
    ("revive restores player without resetting the world", RevivePlayerKeepsWorld),
    ("godmode blocks player damage only", PlayerGodModeBlocksDamage),
    ("sun burn damage scales toward the solar core", SunBurnDamageScalesTowardCore),
    ("sun burn radius follows star scale", SunBurnRadiusFollowsStarScale),
    ("sun burn emits ship damage impact", SunBurnEmitsShipDamageImpact),
    ("godmode blocks player sun burn", PlayerGodModeBlocksSunBurn),
    ("shield regenerates after zero lockout", ShieldZeroLockout),
    ("hostile projectiles hit player hitbox", ProjectileHitboxDamage),
    ("navigation mode blocks player fire", NavigationModeBlocksFire),
    ("combat mode allows player fire", CombatModeAllowsFire),
    ("mode switch cooldown blocks immediate toggle back", ModeSwitchCooldown),
    ("combat mode blocks afterburner", CombatModeBlocksAfterburner),
    ("simulation starts without ambient enemies", NoAmbientEnemyShips),
    ("enemy spawn adds a combat ship", EnemySpawn),
    ("player projectiles damage enemies", PlayerProjectileDamagesEnemy),
    ("enemy ai attacks the player", EnemyAiAttacksPlayer),
    ("ship collision damages and pushes both ships", ShipCollisionDamagesAndPushesBoth),
    ("soft ship collision separates without damage", SoftShipCollisionSeparatesWithoutDamage),
    ("enemy ai steers away from nearby ships", EnemyAiAvoidsNearbyShips),
    ("destroyed enemies are removed", DestroyedEnemyRemoved),
    ("asteroid seeding respects the active cap", AsteroidSeedCap),
    ("projectiles destroy asteroids", ProjectileDestroysAsteroid),
    ("asteroid impact damages the player", AsteroidImpactDamagesPlayer),
    ("asteroids burn up over the sun", AsteroidSunBurn),
    ("asteroid collisions destroy both rocks", AsteroidCollisionDestroysBoth),
    ("debug asteroid burst emits a rock event", DebugAsteroidBurst)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

if (failed > 0)
{
    Console.Error.WriteLine($"{failed} test(s) failed.");
    return 1;
}

Console.WriteLine($"{tests.Length} tests passed.");
return 0;

static void ForwardThrust()
{
    var sim = new LocalSimulation();
    var snapshot = sim.Step(new InputCommand(1f, 0f, 0f, 0f, new Vector2(0f, -100f), false));
    var ship = snapshot.Ships[0];
    Assert(ship.Velocity.Y < 0f, $"expected negative Y velocity, got {ship.Velocity}");
}

static void ReverseThrust()
{
    var forwardSim = new LocalSimulation();
    var reverseSim = new LocalSimulation();
    var forward = forwardSim.Step(new InputCommand(1f, 0f, 0f, 0f, Vector2.Zero, false)).Ships[0];
    var reverse = reverseSim.Step(new InputCommand(0f, 1f, 0f, 0f, Vector2.Zero, false)).Ships[0];
    Assert(reverse.Velocity.Y > 0f, $"expected positive Y reverse velocity, got {reverse.Velocity}");
    Assert(reverse.Velocity.Length() < forward.Velocity.Length(), "reverse thrust should be weaker than forward thrust");
}

static void Strafe()
{
    var sim = new LocalSimulation();
    var ship = sim.Step(new InputCommand(0f, 0f, 1f, 0f, Vector2.Zero, false)).Ships[0];
    Assert(ship.Velocity.X > 0f, $"expected positive X strafe velocity, got {ship.Velocity}");
    Assert(Math.Abs(ship.Rotation) < 0.001f, "strafe should not rotate the ship");
}

static void Turn()
{
    var sim = new LocalSimulation();
    var ship = sim.Step(new InputCommand(0f, 0f, 0f, 1f, Vector2.Zero, false)).Ships[0];
    Assert(ship.Rotation > 0f, $"expected positive rotation, got {ship.Rotation}");
}

static void Damping()
{
    var sim = new LocalSimulation();
    sim.ResetPlayerShip(new ShipState(1, Vector2.Zero, new Vector2(400f, 0f), 0f, 100f, 0f));
    var ship = sim.Step(InputCommand.Idle(Vector2.Zero)).Ships[0];
    Assert(ship.Velocity.Length() < 400f, $"expected damping below 400, got {ship.Velocity.Length()}");
}

static void MaxSpeed()
{
    var sim = new LocalSimulation();
    ShipState ship = default;
    for (var i = 0; i < 600; i++)
    {
        ship = sim.Step(new InputCommand(1f, 0f, 0f, 0f, new Vector2(0f, -1000f), false)).Ships[0];
    }

    Assert(ship.Velocity.Length() <= sim.Config.MaxSpeed + 0.05f, $"speed exceeded max: {ship.Velocity.Length()}");
}

static void AfterburnerMaxSpeed()
{
    var sim = new LocalSimulation();
    ShipState ship = default;
    for (var i = 0; i < 700; i++)
    {
        ship = sim.Step(new InputCommand(0f, 0f, 0f, 0f, new Vector2(0f, -1000f), false, true)).Ships[0];
    }

    Assert(ship.Velocity.Length() > sim.Config.MaxSpeed + 250f, $"afterburner should exceed normal max, got {ship.Velocity.Length()}");
    Assert(ship.Velocity.Length() <= sim.Config.AfterburnerMaxSpeed + 0.05f, $"afterburner exceeded boosted max: {ship.Velocity.Length()}");
}

static void AfterburnerAccelerationSoftens()
{
    var config = new SimulationConfig { LinearDamping = 0f };
    var lowSpeedSim = new LocalSimulation(config);
    var lowSpeedShip = lowSpeedSim.Step(new InputCommand(0f, 0f, 0f, 0f, Vector2.Zero, false, true)).Ships[0];

    var highSpeedSim = new LocalSimulation(config);
    highSpeedSim.ResetPlayerShip(new ShipState(1, Vector2.Zero, new Vector2(0f, -600f), 0f, 100f, 0f));
    var highSpeedShip = highSpeedSim.Step(new InputCommand(0f, 0f, 0f, 0f, Vector2.Zero, false, true)).Ships[0];

    var lowSpeedGain = lowSpeedShip.Velocity.Length();
    var highSpeedGain = highSpeedShip.Velocity.Length() - 600f;
    Assert(highSpeedGain < lowSpeedGain, $"expected softer high-speed boost, low gain {lowSpeedGain}, high gain {highSpeedGain}");
}

static void AfterburnerReleaseKeepsInertia()
{
    var sim = new LocalSimulation();
    ShipState ship = default;
    for (var i = 0; i < 360; i++)
    {
        ship = sim.Step(new InputCommand(0f, 0f, 0f, 0f, new Vector2(0f, -1000f), false, true)).Ships[0];
    }

    var boostedSpeed = ship.Velocity.Length();
    ship = sim.Step(InputCommand.Idle(Vector2.Zero)).Ships[0];
    var releasedSpeed = ship.Velocity.Length();

    Assert(boostedSpeed > sim.Config.MaxSpeed + 100f, $"setup should be boosted, got {boostedSpeed}");
    Assert(releasedSpeed < boostedSpeed, $"speed should decay after release, before {boostedSpeed}, after {releasedSpeed}");
    Assert(releasedSpeed > sim.Config.MaxSpeed, $"release should not snap immediately to normal max, got {releasedSpeed}");
}

static void ProjectileDirection()
{
    var sim = new LocalSimulation();
    sim.Step(new InputCommand(0f, 0f, 0f, 0f, new Vector2(1000f, 0f), false, false, true));
    var snapshot = sim.Step(new InputCommand(0f, 0f, 0f, 0f, new Vector2(1000f, 0f), true));
    var projectile = snapshot.Projectiles.Single();
    Assert(projectile.Velocity.X > 0f, $"expected projectile to travel right, got {projectile.Velocity}");
    Assert(Math.Abs(projectile.Velocity.Y) < 0.001f, $"expected flat right shot, got {projectile.Velocity}");
}

static void ProjectileLifetime()
{
    var sim = new LocalSimulation();
    sim.Step(new InputCommand(0f, 0f, 0f, 0f, new Vector2(1000f, 0f), false, false, true));
    var snapshot = sim.Step(new InputCommand(0f, 0f, 0f, 0f, new Vector2(1000f, 0f), true));
    Assert(snapshot.Projectiles.Count == 1, "projectile should spawn");

    for (var i = 0; i < 120; i++)
    {
        snapshot = sim.Step(InputCommand.Idle(Vector2.Zero));
    }

    Assert(snapshot.Projectiles.Count == 0, "projectile should expire");
}

static void ProjectileCap()
{
    var sim = new LocalSimulation(new SimulationConfig { MaxProjectiles = 12 });
    for (var i = 0; i < 40; i++)
    {
        sim.SpawnProjectile(sim.PlayerShipId, new Vector2(i * 4f, 100f), new Vector2(1f, 0f), 30f, 1f);
    }

    var snapshot = sim.CurrentSnapshot;
    Assert(snapshot.Projectiles.Count == 12, $"projectile cap should keep only 12 projectiles, got {snapshot.Projectiles.Count}");
}

static void DamageOrder()
{
    var sim = new LocalSimulation();
    sim.ApplyDamageToShip(sim.PlayerShipId, 1200f);
    var ship = sim.CurrentSnapshot.Ships[0];

    Assert(Math.Abs(ship.Combat.Shield) < 0.001f, $"shield should be empty, got {ship.Combat.Shield}");
    Assert(Math.Abs(ship.Combat.Armor - 800f) < 0.001f, $"armor should absorb overflow, got {ship.Combat.Armor}");
    Assert(Math.Abs(ship.Combat.Structure - 1000f) < 0.001f, $"structure should be untouched, got {ship.Combat.Structure}");

    sim.ApplyDamageToShip(sim.PlayerShipId, 1900f);
    ship = sim.CurrentSnapshot.Ships[0];

    Assert(Math.Abs(ship.Combat.Armor) < 0.001f, $"armor should be empty, got {ship.Combat.Armor}");
    Assert(Math.Abs(ship.Combat.Structure) < 0.001f, $"structure should be destroyed, got {ship.Combat.Structure}");
    Assert(ship.IsDestroyed, "ship should be destroyed when structure reaches zero");
}

static void RevivePlayerKeepsWorld()
{
    var sim = new LocalSimulation();
    var enemyId = sim.SpawnEnemyShip(new Vector2(700f, 0f), 0f, new ShipHitbox(Vector2.Zero, new Vector2(100f, 100f)));
    sim.SeedAsteroids(2);
    sim.ApplyDamageToShip(sim.PlayerShipId, 3000f);

    var destroyed = sim.CurrentSnapshot.Ships.Single(ship => ship.Id == sim.PlayerShipId);
    Assert(destroyed.IsDestroyed, "setup should destroy the player");

    sim.RevivePlayerShip(new Vector2(120f, -80f), 0.42f);
    var snapshot = sim.CurrentSnapshot;
    var player = snapshot.Ships.Single(ship => ship.Id == sim.PlayerShipId);

    Assert(!player.IsDestroyed, "revived player should be alive");
    Assert(Math.Abs(player.Combat.Shield - player.Combat.MaxShield) < 0.001f, "revive should restore shield");
    Assert(Math.Abs(player.Combat.Armor - player.Combat.MaxArmor) < 0.001f, "revive should restore armor");
    Assert(Math.Abs(player.Combat.Structure - player.Combat.MaxStructure) < 0.001f, "revive should restore structure");
    Assert(snapshot.Ships.Any(ship => ship.Id == enemyId), "revive should keep existing enemies");
    Assert(snapshot.Asteroids.Count == 2, "revive should keep existing asteroids");
    Assert(Vector2.Distance(player.Position, new Vector2(120f, -80f)) < 0.001f, "revive should place player at requested position");
}

static void PlayerGodModeBlocksDamage()
{
    var sim = new LocalSimulation();
    var enemyId = sim.SpawnEnemyShip(new Vector2(700f, 0f), 0f, new ShipHitbox(Vector2.Zero, new Vector2(100f, 100f)));
    sim.PlayerGodMode = true;

    Assert(sim.ApplyDamageToShip(sim.PlayerShipId, 3000f), "damage call should still report that the player was hit");
    Assert(sim.ApplyDamageToShip(enemyId, 100f), "enemy should remain damageable while player godmode is enabled");

    var snapshot = sim.CurrentSnapshot;
    var player = snapshot.Ships.Single(ship => ship.Id == sim.PlayerShipId);
    var enemy = snapshot.Ships.Single(ship => ship.Id == enemyId);

    Assert(Math.Abs(player.Combat.Shield - player.Combat.MaxShield) < 0.001f, "godmode should preserve player shield");
    Assert(Math.Abs(player.Combat.Armor - player.Combat.MaxArmor) < 0.001f, "godmode should preserve player armor");
    Assert(Math.Abs(player.Combat.Structure - player.Combat.MaxStructure) < 0.001f, "godmode should preserve player structure");
    Assert(enemy.Combat.Shield < enemy.Combat.MaxShield, "godmode should not protect enemies");
}

static void SunBurnDamageScalesTowardCore()
{
    var config = new SimulationConfig
    {
        AsteroidsEnabled = false,
        AsteroidMinActiveCount = 0,
        AsteroidMaxActiveCount = 0,
        LinearDamping = 0f,
        ShieldRegenerationPerSecond = 0f,
        SunBurnDamageRadius = 660f,
        SunBurnDamageMinPerSecond = 10f,
        SunBurnDamageMaxPerSecond = 100f
    };
    var hitbox = new ShipHitbox(Vector2.Zero, new Vector2(100f, 100f));
    var shipRadius = MathF.Max(config.ShipRadius, hitbox.BoundingRadius);
    var edgeDistance = config.SunBurnDamageRadius + shipRadius * 0.35f;
    var sim = new LocalSimulation(config);

    sim.ResetPlayerShip(new ShipState(
        1,
        new Vector2(edgeDistance, 0f),
        Vector2.Zero,
        0f,
        config.MaxEnergy,
        0f,
        hitbox,
        CombatStats.Default));
    for (var i = 0; i < SimulationConfig.TickRate; i++)
    {
        sim.Step(InputCommand.Idle(new Vector2(edgeDistance, 0f)));
    }

    var edgeShip = sim.CurrentSnapshot.Ships.Single(ship => ship.Id == sim.PlayerShipId);
    Assert(Math.Abs(edgeShip.Combat.Shield - 990f) < 0.2f, $"edge burn should deal 10 shield damage over one second, got {edgeShip.Combat.Shield}");

    sim.ResetPlayerShip(new ShipState(
        1,
        Vector2.Zero,
        Vector2.Zero,
        0f,
        config.MaxEnergy,
        0f,
        hitbox,
        CombatStats.Default));
    for (var i = 0; i < SimulationConfig.TickRate; i++)
    {
        sim.Step(InputCommand.Idle(Vector2.Zero));
    }

    var coreShip = sim.CurrentSnapshot.Ships.Single(ship => ship.Id == sim.PlayerShipId);
    Assert(Math.Abs(coreShip.Combat.Shield - 900f) < 0.2f, $"core burn should deal 100 shield damage over one second, got {coreShip.Combat.Shield}");
}

static void SunBurnRadiusFollowsStarScale()
{
    var config = new SimulationConfig
    {
        StarVisualWorldSize = AsteroidPhysics.SunVisualWorldSize * 2f,
        SunBurnDamageRadius = 660f,
        SunBurnDamageMinPerSecond = 10f,
        SunBurnDamageMaxPerSecond = 100f,
        AsteroidSunBurnRadius = 680f
    };
    const float shipRadius = 50f;
    var defaultEdge = config.SunBurnDamageRadius + shipRadius * 0.35f;
    var scaledEdge = AsteroidPhysics.ShipSunBurnDamageRadius(config) + shipRadius * 0.35f;

    Assert(Math.Abs(AsteroidPhysics.ShipSunBurnDamageRadius(config) - 1320f) < 0.001f, "ship sun burn radius should double with a double-sized star");
    Assert(AsteroidPhysics.ShipSunBurnDamagePerSecond(new Vector2(defaultEdge + 20f, 0f), shipRadius, config) > 0f, "larger star should burn beyond the original Sol radius");
    Assert(AsteroidPhysics.ShipSunBurnDamagePerSecond(new Vector2(scaledEdge + 1f, 0f), shipRadius, config) <= 0f, "burn should stop outside the scaled radius");
    Assert(AsteroidPhysics.IsInsideSunBurnZone(new Vector2(1000f, 0f), 0f, config), "asteroid burn zone should also follow star scale");
}

static void SunBurnEmitsShipDamageImpact()
{
    var config = new SimulationConfig
    {
        AsteroidsEnabled = false,
        AsteroidMinActiveCount = 0,
        AsteroidMaxActiveCount = 0,
        ShieldRegenerationPerSecond = 0f,
        SunBurnDamageRadius = 660f,
        SunBurnDamageMinPerSecond = 10f,
        SunBurnDamageMaxPerSecond = 100f
    };
    var sim = new LocalSimulation(config);
    sim.ResetPlayerShip(new ShipState(
        1,
        Vector2.Zero,
        Vector2.Zero,
        0f,
        config.MaxEnergy,
        0f,
        new ShipHitbox(Vector2.Zero, new Vector2(100f, 100f)),
        CombatStats.Default));

    var snapshot = sim.Step(InputCommand.Idle(Vector2.Zero));
    Assert(snapshot.ProjectileImpacts.Count == 1, $"solar burn should emit one damage impact, got {snapshot.ProjectileImpacts.Count}");
    Assert(snapshot.ProjectileImpacts[0].Kind == ProjectileImpactKind.SunBurn, $"expected sun burn impact kind, got {snapshot.ProjectileImpacts[0].Kind}");
    Assert(snapshot.ProjectileImpacts[0].Surface == ProjectileImpactSurface.Shield, $"sun burn should hit the active shield first, got {snapshot.ProjectileImpacts[0].Surface}");
}

static void PlayerGodModeBlocksSunBurn()
{
    var config = new SimulationConfig
    {
        AsteroidsEnabled = false,
        AsteroidMinActiveCount = 0,
        AsteroidMaxActiveCount = 0,
        ShieldRegenerationPerSecond = 0f,
        SunBurnDamageRadius = 660f,
        SunBurnDamageMinPerSecond = 10f,
        SunBurnDamageMaxPerSecond = 100f
    };
    var sim = new LocalSimulation(config);
    sim.PlayerGodMode = true;
    sim.ResetPlayerShip(new ShipState(
        1,
        Vector2.Zero,
        Vector2.Zero,
        0f,
        config.MaxEnergy,
        0f,
        new ShipHitbox(Vector2.Zero, new Vector2(100f, 100f)),
        CombatStats.Default));

    for (var i = 0; i < SimulationConfig.TickRate * 2; i++)
    {
        sim.Step(InputCommand.Idle(Vector2.Zero));
    }

    var player = sim.CurrentSnapshot.Ships.Single(ship => ship.Id == sim.PlayerShipId);
    Assert(Math.Abs(player.Combat.Shield - player.Combat.MaxShield) < 0.001f, "godmode should block solar burn shield damage");
    Assert(Math.Abs(player.Combat.Armor - player.Combat.MaxArmor) < 0.001f, "godmode should block solar burn armor damage");
    Assert(Math.Abs(player.Combat.Structure - player.Combat.MaxStructure) < 0.001f, "godmode should block solar burn structure damage");
}

static void ShieldZeroLockout()
{
    var config = new SimulationConfig
    {
        ShieldRegenerationPerSecond = 40f,
        ShieldZeroRegenerationLockout = 12f,
        SunBurnDamageMaxPerSecond = 0f
    };
    var sim = new LocalSimulation(config);
    sim.ApplyDamageToShip(sim.PlayerShipId, 1000f);

    for (var i = 0; i < 11 * SimulationConfig.TickRate; i++)
    {
        sim.Step(InputCommand.Idle(Vector2.Zero));
    }

    var ship = sim.CurrentSnapshot.Ships[0];
    Assert(Math.Abs(ship.Combat.Shield) < 0.001f, $"shield should stay offline during lockout, got {ship.Combat.Shield}");

    for (var i = 0; i < 2 * SimulationConfig.TickRate; i++)
    {
        sim.Step(InputCommand.Idle(Vector2.Zero));
    }

    ship = sim.CurrentSnapshot.Ships[0];
    Assert(ship.Combat.Shield > 0f, "shield should regenerate after zero lockout");
    Assert(ship.Combat.Shield < 100f, $"shield should regenerate at 40/sec after lockout, got {ship.Combat.Shield}");
}

static void ProjectileHitboxDamage()
{
    var sim = new LocalSimulation(new SimulationConfig { SunBurnDamageMaxPerSecond = 0f });
    sim.ResetPlayerShip(new ShipState(
        1,
        Vector2.Zero,
        Vector2.Zero,
        0f,
        100f,
        0f,
        new ShipHitbox(Vector2.Zero, new Vector2(100f, 100f)),
        CombatStats.Default));
    sim.SpawnProjectile(99, new Vector2(-60f, 0f), new Vector2(1800f, 0f), 1f, 100f);

    var snapshot = sim.Step(InputCommand.Idle(Vector2.Zero));
    var ship = snapshot.Ships[0];

    Assert(snapshot.Projectiles.Count == 0, "hostile projectile should disappear on hit");
    Assert(snapshot.ProjectileImpacts.Count == 1, $"hostile projectile should emit one impact event, got {snapshot.ProjectileImpacts.Count}");
    Assert(snapshot.ProjectileImpacts[0].Surface == ProjectileImpactSurface.Shield, $"first hit should impact shield, got {snapshot.ProjectileImpacts[0].Surface}");
    Assert(snapshot.ProjectileImpacts[0].Kind == ProjectileImpactKind.Projectile, $"expected projectile impact kind, got {snapshot.ProjectileImpacts[0].Kind}");
    Assert(ship.Combat.Shield < ship.Combat.MaxShield, $"shield should take projectile damage, got {ship.Combat.Shield}");
}

static void NavigationModeBlocksFire()
{
    var sim = new LocalSimulation();
    var snapshot = sim.Step(new InputCommand(0f, 0f, 0f, 0f, new Vector2(1000f, 0f), true));
    Assert(snapshot.Ships[0].Mode == ShipMode.Navigation, "ship should start in navigation mode");
    Assert(snapshot.Projectiles.Count == 0, "navigation mode should block player fire");
}

static void CombatModeAllowsFire()
{
    var sim = new LocalSimulation();
    var snapshot = sim.Step(new InputCommand(0f, 0f, 0f, 0f, new Vector2(1000f, 0f), false, false, true));
    Assert(snapshot.Ships[0].Mode == ShipMode.Combat, "R should switch to combat mode immediately");

    snapshot = sim.Step(new InputCommand(0f, 0f, 0f, 0f, new Vector2(1000f, 0f), true));
    Assert(snapshot.Projectiles.Count == 1, "combat mode should allow player fire");
}

static void ModeSwitchCooldown()
{
    var sim = new LocalSimulation();
    var snapshot = sim.Step(new InputCommand(0f, 0f, 0f, 0f, Vector2.Zero, false, false, true));
    Assert(snapshot.Ships[0].Mode == ShipMode.Combat, "first toggle should enter combat");
    Assert(snapshot.Ships[0].ModeSwitchCooldown > 2.9f, $"cooldown should start near 3 sec, got {snapshot.Ships[0].ModeSwitchCooldown}");

    snapshot = sim.Step(new InputCommand(0f, 0f, 0f, 0f, Vector2.Zero, false, false, true));
    Assert(snapshot.Ships[0].Mode == ShipMode.Combat, "cooldown should block immediate toggle back");

    for (var i = 0; i < 181; i++)
    {
        snapshot = sim.Step(InputCommand.Idle(Vector2.Zero));
    }

    snapshot = sim.Step(new InputCommand(0f, 0f, 0f, 0f, Vector2.Zero, false, false, true));
    Assert(snapshot.Ships[0].Mode == ShipMode.Navigation, "toggle should work after cooldown");
}

static void CombatModeBlocksAfterburner()
{
    var sim = new LocalSimulation(new SimulationConfig { LinearDamping = 0f });
    sim.Step(new InputCommand(0f, 0f, 0f, 0f, Vector2.Zero, false, false, true));

    ShipState ship = default;
    for (var i = 0; i < 120; i++)
    {
        ship = sim.Step(new InputCommand(0f, 0f, 0f, 0f, new Vector2(0f, -1000f), false, true)).Ships[0];
    }

    Assert(ship.Mode == ShipMode.Combat, "setup should stay in combat mode");
    Assert(ship.Velocity.Length() < 0.001f, $"afterburner should be blocked in combat mode, got velocity {ship.Velocity}");
}

static void NoAmbientEnemyShips()
{
    var sim = new LocalSimulation();
    Assert(sim.CurrentSnapshot.Ships.Count == 1, "new simulations should only contain the player ship");
}

static void EnemySpawn()
{
    var sim = new LocalSimulation();
    var enemyId = sim.SpawnEnemyShip(new Vector2(700f, 0f), -MathF.PI * 0.5f, new ShipHitbox(Vector2.Zero, new Vector2(100f, 100f)));
    var snapshot = sim.CurrentSnapshot;

    Assert(snapshot.Ships.Count == 2, $"expected player plus one enemy, got {snapshot.Ships.Count}");
    Assert(snapshot.Ships.Any(ship => ship.Id == enemyId), "spawned enemy should be present in the snapshot");
    Assert(sim.TryGetLastCommand(enemyId, out _), "spawned enemy should expose a visual command");
}

static void PlayerProjectileDamagesEnemy()
{
    var config = new SimulationConfig
    {
        ForwardAcceleration = 0f,
        ReverseAcceleration = 0f,
        StrafeAcceleration = 0f,
        TurnSpeed = 0f,
        LinearDamping = 0f
    };
    var sim = new LocalSimulation(config);
    var enemyId = sim.SpawnEnemyShip(new Vector2(100f, 0f), 0f, new ShipHitbox(Vector2.Zero, new Vector2(100f, 100f)));
    sim.SpawnProjectile(sim.PlayerShipId, new Vector2(35f, 0f), new Vector2(1200f, 0f), 1f, 100f);

    WorldSnapshot snapshot = sim.CurrentSnapshot;
    for (var i = 0; i < 8; i++)
    {
        snapshot = sim.Step(InputCommand.Idle(Vector2.Zero));
    }

    var enemy = snapshot.Ships.Single(ship => ship.Id == enemyId);
    Assert(enemy.Combat.Shield < enemy.Combat.MaxShield, $"enemy shield should take damage, got {enemy.Combat.Shield}");
}

static void EnemyAiAttacksPlayer()
{
    var config = new SimulationConfig
    {
        ForwardAcceleration = 0f,
        ReverseAcceleration = 0f,
        StrafeAcceleration = 0f,
        TurnSpeed = 0f,
        LinearDamping = 0f
    };
    var sim = new LocalSimulation(config);
    sim.ResetPlayerShip(new ShipState(
        1,
        Vector2.Zero,
        Vector2.Zero,
        0f,
        100f,
        0f,
        new ShipHitbox(Vector2.Zero, new Vector2(120f, 120f)),
        CombatStats.Default));
    sim.SpawnEnemyShip(new Vector2(0f, -700f), MathF.PI, new ShipHitbox(Vector2.Zero, new Vector2(100f, 100f)));

    WorldSnapshot snapshot = sim.CurrentSnapshot;
    for (var i = 0; i < 90; i++)
    {
        snapshot = sim.Step(InputCommand.Idle(Vector2.Zero));
    }

    var player = snapshot.Ships.Single(ship => ship.Id == sim.PlayerShipId);
    Assert(player.Combat.Shield < player.Combat.MaxShield, $"enemy should damage player shield, got {player.Combat.Shield}");
}

static void ShipCollisionDamagesAndPushesBoth()
{
    var config = new SimulationConfig
    {
        AsteroidsEnabled = false,
        AsteroidMinActiveCount = 0,
        AsteroidMaxActiveCount = 0,
        SunBurnDamageMaxPerSecond = 0f,
        ShieldRegenerationPerSecond = 0f,
        ForwardAcceleration = 0f,
        ReverseAcceleration = 0f,
        StrafeAcceleration = 0f,
        TurnSpeed = 0f,
        LinearDamping = 0f
    };
    var hitbox = new ShipHitbox(Vector2.Zero, new Vector2(100f, 100f));
    var sim = new LocalSimulation(config);
    sim.ResetPlayerShip(new ShipState(1, Vector2.Zero, new Vector2(780f, 0f), 0f, 100f, 0f, hitbox, CombatStats.Default));
    var enemyId = sim.SpawnEnemyShip(new Vector2(82f, 0f), 0f, hitbox);

    var snapshot = sim.Step(InputCommand.Idle(Vector2.Zero));
    var player = snapshot.Ships.Single(ship => ship.Id == sim.PlayerShipId);
    var enemy = snapshot.Ships.Single(ship => ship.Id == enemyId);

    Assert(player.Combat.Shield < player.Combat.MaxShield, $"player shield should take collision damage, got {player.Combat.Shield}");
    Assert(enemy.Combat.Shield < enemy.Combat.MaxShield, $"enemy shield should take collision damage, got {enemy.Combat.Shield}");
    Assert(player.Velocity.X < 780f, $"player should lose forward velocity from collision, got {player.Velocity.X}");
    Assert(enemy.Velocity.X > 0f, $"enemy should be pushed by collision, got {enemy.Velocity}");
    Assert(snapshot.ProjectileImpacts.Count == 2, $"ship collision should emit two impact events, got {snapshot.ProjectileImpacts.Count}");
    Assert(snapshot.ProjectileImpacts.All(impact => impact.Kind == ProjectileImpactKind.ShipCollision), "collision impacts should be marked as ship collisions");
    Assert(snapshot.ProjectileImpacts.All(impact => impact.Surface == ProjectileImpactSurface.Shield), "collision should hit active shields first");
}

static void SoftShipCollisionSeparatesWithoutDamage()
{
    var config = new SimulationConfig
    {
        AsteroidsEnabled = false,
        AsteroidMinActiveCount = 0,
        AsteroidMaxActiveCount = 0,
        SunBurnDamageMaxPerSecond = 0f,
        ShieldRegenerationPerSecond = 0f,
        ForwardAcceleration = 0f,
        ReverseAcceleration = 0f,
        StrafeAcceleration = 0f,
        TurnSpeed = 0f,
        LinearDamping = 0f
    };
    var hitbox = new ShipHitbox(Vector2.Zero, new Vector2(100f, 100f));
    var sim = new LocalSimulation(config);
    sim.ResetPlayerShip(new ShipState(1, Vector2.Zero, new Vector2(30f, 0f), 0f, 100f, 0f, hitbox, CombatStats.Default));
    var enemyId = sim.SpawnEnemyShip(new Vector2(82f, 0f), 0f, hitbox);

    var snapshot = sim.Step(InputCommand.Idle(Vector2.Zero));
    var player = snapshot.Ships.Single(ship => ship.Id == sim.PlayerShipId);
    var enemy = snapshot.Ships.Single(ship => ship.Id == enemyId);
    var centerDistance = Vector2.Distance(
        player.Hitbox.WorldCenter(player.Position, player.Rotation),
        enemy.Hitbox.WorldCenter(enemy.Position, enemy.Rotation));

    Assert(Math.Abs(player.Combat.Shield - player.Combat.MaxShield) < 0.001f, $"soft bump should not damage player shield, got {player.Combat.Shield}");
    Assert(Math.Abs(enemy.Combat.Shield - enemy.Combat.MaxShield) < 0.001f, $"soft bump should not damage enemy shield, got {enemy.Combat.Shield}");
    Assert(snapshot.ProjectileImpacts.Count == 0, $"soft bump should not emit impact VFX, got {snapshot.ProjectileImpacts.Count}");
    Assert(centerDistance > 82f, $"soft bump should separate ship centers, got distance {centerDistance}");
    Assert(centerDistance < 96f, $"soft bump should not over-correct from bounding radius, got distance {centerDistance}");
}

static void EnemyAiAvoidsNearbyShips()
{
    var config = new SimulationConfig
    {
        AsteroidsEnabled = false,
        AsteroidMinActiveCount = 0,
        AsteroidMaxActiveCount = 0,
        SunBurnDamageMaxPerSecond = 0f,
        ForwardAcceleration = 0f,
        ReverseAcceleration = 0f,
        StrafeAcceleration = 0f,
        LinearDamping = 0f
    };
    var hitbox = new ShipHitbox(Vector2.Zero, new Vector2(100f, 100f));
    var sim = new LocalSimulation(config);
    sim.ResetPlayerShip(new ShipState(1, Vector2.Zero, Vector2.Zero, 0f, 100f, 0f, hitbox, CombatStats.Default));
    var blockedEnemyId = sim.SpawnEnemyShip(new Vector2(0f, -900f), MathF.PI, hitbox);
    sim.SpawnEnemyShip(new Vector2(0f, -830f), MathF.PI, hitbox);

    sim.Step(InputCommand.Idle(Vector2.Zero));
    Assert(sim.TryGetLastCommand(blockedEnemyId, out var command), "enemy command should be available after AI step");
    Assert(
        Math.Abs(command.Turn) > 0.2f || Math.Abs(command.Strafe) > 0.05f || command.Reverse > 0.01f,
        $"enemy should steer away from nearby ship, command turn={command.Turn}, strafe={command.Strafe}, reverse={command.Reverse}");
    Assert(!command.Afterburner, "enemy should not afterburner while avoiding a nearby ship");
}

static void DestroyedEnemyRemoved()
{
    var sim = new LocalSimulation();
    var enemyId = sim.SpawnEnemyShip(new Vector2(700f, 0f), 0f, new ShipHitbox(Vector2.Zero, new Vector2(100f, 100f)));
    Assert(sim.ApplyDamageToShip(enemyId, 3000f), "damage should apply to spawned enemy");

    var snapshot = sim.Step(InputCommand.Idle(Vector2.Zero));
    Assert(snapshot.Ships.All(ship => ship.Id != enemyId), "destroyed enemy should be removed on the next simulation tick");
}

static void AsteroidSeedCap()
{
    var sim = new LocalSimulation(new SimulationConfig { AsteroidMaxActiveCount = 6 });
    sim.SeedAsteroids(20);
    var snapshot = sim.CurrentSnapshot;

    Assert(snapshot.Asteroids.Count == 6, $"seed should respect max active count, got {snapshot.Asteroids.Count}");
    Assert(snapshot.Asteroids.All(asteroid => asteroid.Radius > 0f), "seeded asteroids should have a positive radius");
    Assert(snapshot.Asteroids.All(asteroid => asteroid.Structure <= sim.Config.AsteroidMaxStructure), "asteroid HP should be capped");
}

static void ProjectileDestroysAsteroid()
{
    var sim = new LocalSimulation(new SimulationConfig
    {
        AsteroidsEnabled = false,
        AsteroidMinActiveCount = 0,
        AsteroidMaxActiveCount = 4,
        AsteroidSunGravity = 0f,
        AsteroidSunBurnRadius = 0f,
        AsteroidHeatRadius = 0f,
        SunBurnDamageMaxPerSecond = 0f
    });
    sim.SpawnAsteroid(new Vector2(96f, 0f), Vector2.Zero, 30f, structure: 80f);
    sim.SpawnProjectile(sim.PlayerShipId, new Vector2(70f, 0f), new Vector2(1200f, 0f), 1f, 100f);

    var snapshot = sim.Step(InputCommand.Idle(Vector2.Zero));
    Assert(snapshot.Asteroids.Count == 0, "destroyed asteroid should be removed");
    Assert(snapshot.AsteroidEvents.Count == 1, "destroyed asteroid should emit one event");
    Assert(snapshot.AsteroidEvents[0].Type == AsteroidEventType.RockExplosion, $"expected rock explosion, got {snapshot.AsteroidEvents[0].Type}");
}

static void AsteroidImpactDamagesPlayer()
{
    var sim = new LocalSimulation(new SimulationConfig
    {
        AsteroidsEnabled = false,
        AsteroidMinActiveCount = 0,
        AsteroidMaxActiveCount = 4,
        AsteroidSunGravity = 0f,
        AsteroidSunBurnRadius = 0f,
        AsteroidHeatRadius = 0f,
        SunBurnDamageMaxPerSecond = 0f
    });
    sim.ResetPlayerShip(new ShipState(
        1,
        Vector2.Zero,
        Vector2.Zero,
        0f,
        100f,
        0f,
        new ShipHitbox(Vector2.Zero, new Vector2(100f, 100f)),
        CombatStats.Default));
    sim.SpawnAsteroid(new Vector2(-90f, 0f), Vector2.Zero, 80f, structure: 1000f);

    var snapshot = sim.Step(InputCommand.Idle(Vector2.Zero));
    var player = snapshot.Ships.Single(ship => ship.Id == sim.PlayerShipId);

    Assert(player.Combat.Shield <= 0.001f, $"asteroid should deal structure-sized damage, player shield {player.Combat.Shield}");
    Assert(snapshot.ProjectileImpacts.Count == 1, $"asteroid collision should emit one ship damage impact, got {snapshot.ProjectileImpacts.Count}");
    Assert(snapshot.ProjectileImpacts[0].Kind == ProjectileImpactKind.AsteroidCollision, $"expected asteroid collision impact kind, got {snapshot.ProjectileImpacts[0].Kind}");
    Assert(snapshot.ProjectileImpacts[0].Surface == ProjectileImpactSurface.Shield, $"asteroid should hit the active shield first, got {snapshot.ProjectileImpacts[0].Surface}");
    Assert(snapshot.Asteroids.Count == 0, "impacting asteroid should be consumed");
    Assert(snapshot.AsteroidEvents.Single().Type == AsteroidEventType.ShipImpact, "ship collision should emit a ship impact event");
}

static void AsteroidSunBurn()
{
    var sim = new LocalSimulation(new SimulationConfig
    {
        AsteroidsEnabled = false,
        AsteroidMinActiveCount = 0,
        AsteroidMaxActiveCount = 4,
        AsteroidSunGravity = 0f
    });
    sim.SpawnAsteroid(new Vector2(0f, 620f), Vector2.Zero, 40f, structure: 40f);

    var snapshot = sim.Step(InputCommand.Idle(Vector2.Zero));
    Assert(snapshot.Asteroids.Count == 0, "asteroid inside the sun burn zone should be removed");
    Assert(snapshot.AsteroidEvents.Single().Type == AsteroidEventType.SunBurn, "sun burn should emit a sun burn event");
}

static void AsteroidCollisionDestroysBoth()
{
    var sim = new LocalSimulation(new SimulationConfig
    {
        AsteroidsEnabled = false,
        AsteroidMinActiveCount = 0,
        AsteroidMaxActiveCount = 4,
        AsteroidSunGravity = 0f,
        AsteroidSunBurnRadius = 0f,
        AsteroidHeatRadius = 0f
    });
    sim.SpawnAsteroid(new Vector2(-40f, 600f), Vector2.Zero, 80f, structure: 500f);
    sim.SpawnAsteroid(new Vector2(40f, 600f), Vector2.Zero, 80f, structure: 500f);

    var snapshot = sim.Step(InputCommand.Idle(Vector2.Zero));
    Assert(snapshot.Asteroids.Count == 0, "colliding asteroids should both be destroyed");
    Assert(snapshot.AsteroidEvents.Count == 2, $"expected two asteroid events, got {snapshot.AsteroidEvents.Count}");
    Assert(snapshot.AsteroidEvents.All(e => e.Type == AsteroidEventType.RockExplosion), "asteroid collision should emit rock explosions");
}

static void DebugAsteroidBurst()
{
    var sim = new LocalSimulation(new SimulationConfig
    {
        AsteroidsEnabled = false,
        AsteroidMinActiveCount = 0,
        AsteroidMaxActiveCount = 4,
        AsteroidSunGravity = 0f,
        AsteroidSunBurnRadius = 0f,
        AsteroidHeatRadius = 0f
    });
    sim.SpawnAsteroid(new Vector2(120f, 0f), Vector2.Zero, 80f, structure: 500f);

    var destroyed = sim.TryDestroyNearestAsteroid(Vector2.Zero, 300f, AsteroidEventType.RockExplosion, out var asteroidEvent);
    Assert(destroyed, "debug destroy should find the nearest asteroid");
    Assert(asteroidEvent.Type == AsteroidEventType.RockExplosion, $"expected rock event, got {asteroidEvent.Type}");
    Assert(sim.CurrentSnapshot.AsteroidEvents.Count == 1, "debug destroy should expose an asteroid event immediately");

    var snapshot = sim.Step(InputCommand.Idle(Vector2.Zero));
    Assert(snapshot.Asteroids.Count == 0, "debug-destroyed asteroid should be removed on the next step");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
