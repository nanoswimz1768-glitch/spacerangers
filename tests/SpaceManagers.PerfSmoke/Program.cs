using System.Diagnostics;
using System.Numerics;
using SpaceManagers.Core;

var scenarios = new (string Name, int Ticks, Func<LocalSimulation> Create)[]
{
    ("offscreen pursuit: 80 enemies / no sustained fire", 3600, CreateOffscreenPursuit),
    ("close dogfight: 32 enemies / active AI fire", 3600, CreateCloseDogfight),
    ("projectile storm: 48 enemies / 1800 spawn requests / capped active projectiles", 900, CreateProjectileStorm),
};

Console.WriteLine("Space Managers perf smoke");
Console.WriteLine($"Runtime: {Environment.Version}");
Console.WriteLine();

foreach (var scenario in scenarios)
{
    RunScenario(scenario.Name, scenario.Ticks, scenario.Create);
}

static void RunScenario(string name, int ticks, Func<LocalSimulation> create)
{
    var sim = create();

    for (var i = 0; i < 180; i++)
    {
        sim.Step(InputCommand.Idle(Vector2.Zero));
    }

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    var beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
    var beforeGen0 = GC.CollectionCount(0);
    var beforeGen1 = GC.CollectionCount(1);
    var beforeGen2 = GC.CollectionCount(2);
    var stopwatch = Stopwatch.StartNew();
    WorldSnapshot snapshot = sim.CurrentSnapshot;

    for (var i = 0; i < ticks; i++)
    {
        snapshot = sim.Step(InputCommand.Idle(Vector2.Zero));
    }

    stopwatch.Stop();
    var allocated = GC.GetAllocatedBytesForCurrentThread() - beforeAllocated;
    var msPerTick = stopwatch.Elapsed.TotalMilliseconds / ticks;
    var ticksPerSecond = ticks / stopwatch.Elapsed.TotalSeconds;
    var ships = snapshot.Ships.Count;
    var projectiles = snapshot.Projectiles.Count;

    Console.WriteLine(name);
    Console.WriteLine($"  ships: {ships}, projectiles: {projectiles}, ticks: {ticks}");
    Console.WriteLine($"  time: {stopwatch.Elapsed.TotalMilliseconds:0.00} ms total, {msPerTick:0.0000} ms/tick, {ticksPerSecond:0} ticks/sec");
    Console.WriteLine($"  alloc: {allocated / 1024.0 / 1024.0:0.00} MB total, {allocated / (double)ticks:0} B/tick");
    Console.WriteLine($"  GC: gen0 +{GC.CollectionCount(0) - beforeGen0}, gen1 +{GC.CollectionCount(1) - beforeGen1}, gen2 +{GC.CollectionCount(2) - beforeGen2}");
    Console.WriteLine();
}

static LocalSimulation CreateOffscreenPursuit()
{
    var sim = new LocalSimulation(new SimulationConfig { LinearDamping = 0.72f });
    var hitbox = new ShipHitbox(Vector2.Zero, new Vector2(96f, 104f));
    SpawnRing(sim, 80, 5200f, hitbox);
    return sim;
}

static LocalSimulation CreateCloseDogfight()
{
    var sim = new LocalSimulation(new SimulationConfig { LinearDamping = 0.72f });
    var hitbox = new ShipHitbox(Vector2.Zero, new Vector2(96f, 104f));
    SpawnRing(sim, 32, 920f, hitbox);
    return sim;
}

static LocalSimulation CreateProjectileStorm()
{
    var sim = new LocalSimulation(new SimulationConfig { LinearDamping = 0.72f });
    var hitbox = new ShipHitbox(Vector2.Zero, new Vector2(96f, 104f));
    SpawnRing(sim, 48, 1400f, hitbox);

    for (var i = 0; i < 1800; i++)
    {
        var x = 7600f + i % 60 * 24f;
        var y = -6200f + i / 60 * 18f;
        sim.SpawnProjectile(sim.PlayerShipId, new Vector2(x, y), new Vector2(18f, 4f), 30f, 12f);
    }

    return sim;
}

static void SpawnRing(LocalSimulation sim, int count, float radius, ShipHitbox hitbox)
{
    for (var i = 0; i < count; i++)
    {
        var angle = i * MathF.Tau / count;
        var position = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        var rotation = MathF.Atan2(-position.X, position.Y);
        sim.SpawnEnemyShip(position, rotation, hitbox);
    }
}
