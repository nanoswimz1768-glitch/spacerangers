namespace SpaceManagers.Core;

public sealed record GalaxyLifeSystemRef(string Id, string DisplayName, string SectorId = "");

public sealed record GalaxyLifeConfig(
    int ShipsPerSystem = 32,
    int PirateShipsPerSystem = 6,
    int MaxPhysicalShipsPerSystem = 32,
    float MinTransitSeconds = 42f,
    float MaxTransitSeconds = 130f,
    float MinDwellSeconds = 28f,
    float MaxDwellSeconds = 95f);

public sealed class GalaxyPilotState
{
    public GalaxyPilotState(
        int pilotId,
        string name,
        ShipRole role,
        string shipAssetId,
        string currentSystemId,
        int seed)
    {
        PilotId = pilotId;
        Name = name;
        Role = role;
        ShipAssetId = shipAssetId;
        CurrentSystemId = currentSystemId;
        Seed = seed;
    }

    public int PilotId { get; }
    public string Name { get; }
    public ShipRole Role { get; }
    public string ShipAssetId { get; }
    public string CurrentSystemId { get; set; }
    public string DestinationSystemId { get; set; } = string.Empty;
    public bool InTransit { get; set; }
    public float TransitRemainingSeconds { get; set; }
    public float DwellSeconds { get; set; }
    public int Seed { get; }
}

public sealed class GalaxyLifeSimulation
{
    private static readonly ShipRole[] FederationRolePattern =
    {
        ShipRole.Trader, ShipRole.Trader, ShipRole.Trader, ShipRole.Trader,
        ShipRole.Trader, ShipRole.Trader, ShipRole.Trader, ShipRole.Trader,
        ShipRole.Trader, ShipRole.Trader, ShipRole.Trader, ShipRole.Trader,
        ShipRole.Diplomat, ShipRole.Diplomat, ShipRole.Diplomat, ShipRole.Diplomat,
        ShipRole.Ranger, ShipRole.Ranger, ShipRole.Ranger, ShipRole.Ranger, ShipRole.Ranger, ShipRole.Ranger,
        ShipRole.Military, ShipRole.Military, ShipRole.Military, ShipRole.Military,
        ShipRole.Military, ShipRole.Military, ShipRole.Military, ShipRole.Military, ShipRole.Military, ShipRole.Military
    };

    private static readonly string[] Races = { "People", "Fei", "Gaal", "Maloc", "Peleng" };
    private readonly GalaxyLifeConfig _config;
    private readonly List<GalaxyLifeSystemRef> _systems = new();
    private readonly List<GalaxyPilotState> _pilots = new();
    private readonly Random _random;
    private int _nextPilotId = 1000;

    public GalaxyLifeSimulation(IReadOnlyList<GalaxyLifeSystemRef> systems, GalaxyLifeConfig? config = null, int seed = 0x517A)
    {
        _config = config ?? new GalaxyLifeConfig();
        _random = new Random(seed);
        _systems.AddRange(systems.Where(system => !string.IsNullOrWhiteSpace(system.Id))
            .DistinctBy(system => system.Id, StringComparer.OrdinalIgnoreCase));
        if (_systems.Count == 0)
        {
            _systems.Add(new GalaxyLifeSystemRef("sol", "Sol", "Sol"));
        }

        SeedInitialPopulation();
    }

    public GalaxyLifeConfig Config => _config;
    public IReadOnlyList<GalaxyPilotState> Pilots => _pilots;

    public void Step(float seconds, string activeSystemId = "")
    {
        if (seconds <= 0f || _systems.Count <= 1)
        {
            return;
        }

        var delta = Math.Clamp(seconds, 0f, 8f);
        foreach (var pilot in _pilots)
        {
            if (pilot.InTransit)
            {
                pilot.TransitRemainingSeconds -= delta;
                if (pilot.TransitRemainingSeconds <= 0f)
                {
                    pilot.CurrentSystemId = pilot.DestinationSystemId;
                    pilot.DestinationSystemId = string.Empty;
                    pilot.InTransit = false;
                    pilot.DwellSeconds = RandomDwellSeconds(pilot.Role);
                }

                continue;
            }

            if (!string.IsNullOrWhiteSpace(activeSystemId)
                && string.Equals(pilot.CurrentSystemId, activeSystemId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            pilot.DwellSeconds -= delta * RoleTravelTempo(pilot.Role);
            if (pilot.DwellSeconds > 0f)
            {
                continue;
            }

            BeginTransit(pilot, PickDestinationSystem(pilot.CurrentSystemId));
        }
    }

    public IReadOnlyList<GalaxyPilotState> ActivePilotsForSystem(string systemId)
    {
        if (string.IsNullOrWhiteSpace(systemId))
        {
            return Array.Empty<GalaxyPilotState>();
        }

        return _pilots
            .Where(pilot => !pilot.InTransit && string.Equals(pilot.CurrentSystemId, systemId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(pilot => RoleOrder(pilot.Role))
            .ThenBy(pilot => pilot.PilotId)
            .Take(_config.MaxPhysicalShipsPerSystem)
            .ToArray();
    }

    public GalaxyPilotState ReportDestroyed(int pilotId)
    {
        var index = _pilots.FindIndex(pilot => pilot.PilotId == pilotId);
        var role = index >= 0 ? _pilots[index].Role : ShipRole.Trader;
        if (index >= 0)
        {
            _pilots.RemoveAt(index);
        }

        var system = _systems[_random.Next(_systems.Count)];
        var replacement = CreatePilot(role, system.Id);
        replacement.DwellSeconds = RandomDwellSeconds(role);
        _pilots.Add(replacement);
        return replacement;
    }

    private void SeedInitialPopulation()
    {
        foreach (var system in _systems)
        {
            var totalShips = Math.Max(0, _config.ShipsPerSystem);
            var pirateShips = Math.Clamp(_config.PirateShipsPerSystem, 0, totalShips);
            var federationShips = totalShips - pirateShips;
            for (var i = 0; i < federationShips; i++)
            {
                var role = FederationRolePattern[i % FederationRolePattern.Length];
                var pilot = CreatePilot(role, system.Id);
                pilot.DwellSeconds = RandomDwellSeconds(role);
                _pilots.Add(pilot);
            }

            for (var i = 0; i < pirateShips; i++)
            {
                var pilot = CreatePilot(ShipRole.Pirate, system.Id);
                pilot.DwellSeconds = RandomDwellSeconds(ShipRole.Pirate);
                _pilots.Add(pilot);
            }
        }
    }

    private GalaxyPilotState CreatePilot(ShipRole role, string systemId)
    {
        var id = _nextPilotId++;
        var race = Races[_random.Next(Races.Length)];
        return new GalaxyPilotState(
            id,
            GeneratePilotName(race, role),
            role,
            ShipAssetForRole(race, role),
            systemId,
            _random.Next());
    }

    private void BeginTransit(GalaxyPilotState pilot, string destinationSystemId)
    {
        if (string.IsNullOrWhiteSpace(destinationSystemId)
            || string.Equals(destinationSystemId, pilot.CurrentSystemId, StringComparison.OrdinalIgnoreCase))
        {
            pilot.DwellSeconds = RandomDwellSeconds(pilot.Role);
            return;
        }

        pilot.DestinationSystemId = destinationSystemId;
        pilot.InTransit = true;
        pilot.TransitRemainingSeconds = RandomTransitSeconds(pilot.Role);
    }

    private string PickDestinationSystem(string currentSystemId)
    {
        if (_systems.Count <= 1)
        {
            return currentSystemId;
        }

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var candidate = _systems[_random.Next(_systems.Count)].Id;
            if (!string.Equals(candidate, currentSystemId, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return _systems.First(system => !string.Equals(system.Id, currentSystemId, StringComparison.OrdinalIgnoreCase)).Id;
    }

    private float RandomTransitSeconds(ShipRole role)
    {
        var multiplier = role switch
        {
            ShipRole.Diplomat => 0.82f,
            ShipRole.Ranger => 0.76f,
            ShipRole.Military => 0.92f,
            ShipRole.Pirate => 0.70f,
            _ => 1f
        };
        return RandomRange(_config.MinTransitSeconds, _config.MaxTransitSeconds) * multiplier;
    }

    private float RandomDwellSeconds(ShipRole role)
    {
        var multiplier = role switch
        {
            ShipRole.Trader => 1.18f,
            ShipRole.Diplomat => 0.82f,
            ShipRole.Pirate => 0.64f,
            _ => 0.92f
        };
        return RandomRange(_config.MinDwellSeconds, _config.MaxDwellSeconds) * multiplier;
    }

    private float RandomRange(float min, float max)
    {
        return min + (float)_random.NextDouble() * Math.Max(0f, max - min);
    }

    private static float RoleTravelTempo(ShipRole role)
    {
        return role switch
        {
            ShipRole.Pirate => 1.28f,
            ShipRole.Ranger => 1.16f,
            ShipRole.Diplomat => 1.10f,
            _ => 1f
        };
    }

    private static int RoleOrder(ShipRole role)
    {
        return role switch
        {
            ShipRole.Trader => 0,
            ShipRole.Diplomat => 1,
            ShipRole.Ranger => 2,
            ShipRole.Military => 3,
            ShipRole.Pirate => 4,
            _ => 5
        };
    }

    private static string ShipAssetForRole(string race, ShipRole role)
    {
        var suffix = role switch
        {
            ShipRole.Trader => "T",
            ShipRole.Diplomat => "D",
            ShipRole.Ranger => "R",
            ShipRole.Military => "W",
            ShipRole.Pirate => "P",
            _ => "R"
        };
        return $"2{race}{suffix}";
    }

    private string GeneratePilotName(string race, ShipRole role)
    {
        var first = race switch
        {
            "Fei" => Pick(FeiFirst),
            "Gaal" => Pick(GaalFirst),
            "Maloc" => Pick(MalocFirst),
            "Peleng" => Pick(PelengFirst),
            _ => Pick(PeopleFirst)
        };
        var last = race switch
        {
            "Fei" => Pick(FeiLast),
            "Gaal" => Pick(GaalLast),
            "Maloc" => Pick(MalocLast),
            "Peleng" => Pick(PelengLast),
            _ => Pick(PeopleLast)
        };
        var tag = role == ShipRole.Pirate ? Pick(PirateTags) : string.Empty;
        return string.IsNullOrWhiteSpace(tag) ? $"{first} {last}" : $"{first} {tag} {last}";
    }

    private string Pick(IReadOnlyList<string> values)
    {
        return values[_random.Next(values.Count)];
    }

    private static readonly string[] PeopleFirst = { "Alex", "Victor", "Mira", "Nika", "Darin", "Sergey", "Lana", "Anton" };
    private static readonly string[] PeopleLast = { "Volkov", "Sokol", "Raine", "Orlov", "Kane", "Belov", "Starkov", "Meyer" };
    private static readonly string[] FeiFirst = { "Li", "Mey", "Tao", "Sian", "Ren", "Iri", "Fao", "Ney" };
    private static readonly string[] FeiLast = { "Sey", "Lun", "Vey", "Tarin", "Fai", "Norro", "Ithen", "Qel" };
    private static readonly string[] GaalFirst = { "Aru", "Gaal", "Loru", "Tei", "Naar", "Ulo", "Iga", "Sev" };
    private static readonly string[] GaalLast = { "Duun", "Vaal", "Ir", "Orr", "Taal", "Gor", "Maal", "Urr" };
    private static readonly string[] MalocFirst = { "Grum", "Krag", "Mor", "Vark", "Bran", "Dro", "Rog", "Karn" };
    private static readonly string[] MalocLast = { "Torg", "Borr", "Makh", "Drog", "Ghar", "Korr", "Brakk", "Vor" };
    private static readonly string[] PelengFirst = { "Pik", "Zog", "Klim", "Rif", "Tuk", "Narg", "Bim", "Duk" };
    private static readonly string[] PelengLast = { "Shnork", "Prag", "Zyga", "Khlop", "Bzun", "Ryk", "Plok", "Grek" };
    private static readonly string[] PirateTags = { "\"Black\"", "\"Sharp\"", "\"Red\"", "\"Void\"", "\"Lucky\"", "\"Broken\"" };
}
