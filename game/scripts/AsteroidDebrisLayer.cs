using Godot;
using SpaceRangers.Core;
using File = System.IO.File;

namespace SpaceRangersPrototype;

public partial class AsteroidDebrisLayer : Node2D
{
    private const int MaxEffects = 28;
    private const int AsteroidTextureCount = 32;

    private static readonly string[] ShardTexturePaths =
    [
        "res://assets/effects/asteroid_shard_00.png",
        "res://assets/effects/asteroid_shard_01.png",
        "res://assets/effects/asteroid_shard_02.png",
        "res://assets/effects/asteroid_shard_03.png",
        "res://assets/effects/asteroid_shard_04.png",
        "res://assets/effects/asteroid_shard_05.png"
    ];

    private readonly Texture2D?[] _asteroidTextures = new Texture2D?[AsteroidTextureCount];
    private readonly Texture2D?[] _shardTextures = new Texture2D?[ShardTexturePaths.Length];
    private AsteroidDebrisResources? _resources;

    public override void _Ready()
    {
        ZIndex = 23;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;

        for (var index = 0; index < _asteroidTextures.Length; index++)
        {
            _asteroidTextures[index] = LoadTexture(AsteroidTexturePath(index));
        }

        for (var index = 0; index < ShardTexturePaths.Length; index++)
        {
            _shardTextures[index] = ResourceLoader.Load<Texture2D>(ShardTexturePaths[index]);
        }

        _resources = new AsteroidDebrisResources(
            _asteroidTextures,
            _shardTextures,
            LoadTexture("res://assets/effects/asteroid_smoke_puff.png"),
            LoadTexture("res://assets/effects/asteroid_dust_ring.png"),
            LoadTexture("res://assets/effects/asteroid_impact_flash.png"),
            LoadTexture("res://assets/effects/asteroid_fire_spark.png"),
            LoadTexture("res://assets/effects/asteroid_heat_corona.png"),
            LoadTexture("res://assets/effects/asteroid_fire_glow.png"),
            LoadTexture("res://assets/effects/asteroid_fire_plume.png"),
            LoadTexture("res://assets/effects/effectblocks/effectblocks_circle_1.png"),
            LoadTexture("res://assets/effects/effectblocks/effectblocks_muzzleflash_1.png"),
            LoadTexture("res://assets/effects/effectblocks/effectblocks_muzzle_flash_fps_1.png"),
            LoadTexture("res://assets/effects/effectblocks/effectblocks_sparkle.png"),
            new[]
            {
                LoadTexture("res://assets/effects/effectblocks/effectblocks_crack_2.png"),
                LoadTexture("res://assets/effects/effectblocks/effectblocks_crack_3.png")
            },
            ResourceLoader.Load<Shader>("res://shaders/asteroid_vfx_burst.gdshader"),
            ResourceLoader.Load<Shader>("res://shaders/asteroid_vfx_heat.gdshader"));
    }

    private static Texture2D? LoadTexture(string resourcePath)
    {
        if (ResourceLoader.Exists(resourcePath))
        {
            return ResourceLoader.Load<Texture2D>(resourcePath);
        }

        var globalPath = ProjectSettings.GlobalizePath(resourcePath);
        if (!File.Exists(globalPath))
        {
            return null;
        }

        var image = Image.LoadFromFile(globalPath);
        if (image is null || image.GetWidth() <= 0 || image.GetHeight() <= 0)
        {
            return null;
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static string AsteroidTexturePath(int index)
    {
        return $"res://assets/asteroids/asteroid_{index:00}.png";
    }

    public override void _ExitTree()
    {
        ClearEffects();
        _resources = null;
        for (var index = 0; index < _asteroidTextures.Length; index++)
        {
            _asteroidTextures[index] = null;
        }

        for (var index = 0; index < _shardTextures.Length; index++)
        {
            _shardTextures[index] = null;
        }
    }

    public void ClearEffects()
    {
        while (GetChildCount() > 0)
        {
            var child = GetChild(0);
            RemoveChild(child);
            child.QueueFree();
        }
    }

    public void Spawn(AsteroidEventState asteroidEvent)
    {
        if (_resources is null)
        {
            return;
        }

        while (GetChildCount() >= MaxEffects)
        {
            var oldest = GetChild(0);
            RemoveChild(oldest);
            oldest.QueueFree();
        }

        var view = new AsteroidDebrisEffectView();
        view.Configure(asteroidEvent, _resources);
        AddChild(view);
    }
}

public sealed class AsteroidDebrisResources
{
    public AsteroidDebrisResources(
        IReadOnlyList<Texture2D?> asteroidTextures,
        IReadOnlyList<Texture2D?> shardTextures,
        Texture2D? smokePuff,
        Texture2D? dustRing,
        Texture2D? impactFlash,
        Texture2D? spark,
        Texture2D? heatCorona,
        Texture2D? fireGlow,
        Texture2D? firePlume,
        Texture2D? effectBlocksRing,
        Texture2D? effectBlocksMuzzleFlash,
        Texture2D? effectBlocksMuzzleFlashFps,
        Texture2D? effectBlocksSparkle,
        IReadOnlyList<Texture2D?> effectBlocksCracks,
        Shader? burstShader,
        Shader? heatShader)
    {
        AsteroidTextures = asteroidTextures;
        ShardTextures = shardTextures;
        SmokePuff = smokePuff;
        DustRing = dustRing;
        ImpactFlash = impactFlash;
        Spark = spark;
        HeatCorona = heatCorona;
        FireGlow = fireGlow;
        FirePlume = firePlume;
        EffectBlocksRing = effectBlocksRing;
        EffectBlocksMuzzleFlash = effectBlocksMuzzleFlash;
        EffectBlocksMuzzleFlashFps = effectBlocksMuzzleFlashFps;
        EffectBlocksSparkle = effectBlocksSparkle;
        EffectBlocksCracks = effectBlocksCracks;
        BurstShader = burstShader;
        HeatShader = heatShader;
    }

    public IReadOnlyList<Texture2D?> AsteroidTextures { get; }
    public IReadOnlyList<Texture2D?> ShardTextures { get; }
    public Texture2D? SmokePuff { get; }
    public Texture2D? DustRing { get; }
    public Texture2D? ImpactFlash { get; }
    public Texture2D? Spark { get; }
    public Texture2D? HeatCorona { get; }
    public Texture2D? FireGlow { get; }
    public Texture2D? FirePlume { get; }
    public Texture2D? EffectBlocksRing { get; }
    public Texture2D? EffectBlocksMuzzleFlash { get; }
    public Texture2D? EffectBlocksMuzzleFlashFps { get; }
    public Texture2D? EffectBlocksSparkle { get; }
    public IReadOnlyList<Texture2D?> EffectBlocksCracks { get; }
    public Shader? BurstShader { get; }
    public Shader? HeatShader { get; }
}
