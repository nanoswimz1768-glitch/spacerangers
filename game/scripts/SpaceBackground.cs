using Godot;
using SpaceManagers.Core;
using CoreVector2 = System.Numerics.Vector2;

namespace SpaceManagersPrototype;

public partial class SpaceBackground : Node2D
{
    private const bool ShowPlanetScaleDebugLabels = false;
    private const int OrbitArcSegments = 192;
    private const int SunCoronaArcSegments = 72;
    private const int MaxCachedSunFrameSets = 1;
    private static readonly bool UseAnimatedNonEarthPlanets = true;
    private static readonly bool UseSolarRendererForAllStars = true;
    private static readonly Dictionary<string, Texture2D[]> SunFrameCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string[]> SunFramePathCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Queue<string> SunFrameCacheOrder = new();
    private static readonly Dictionary<string, Texture2D> TextureCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ThreadedTextureRequests = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> MissingTexturePaths = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Vector2 StarPosition = Vector2.Zero;
    private static readonly IReadOnlyDictionary<string, PlanetRenderSettings> PlanetRenderSettingsById =
        new Dictionary<string, PlanetRenderSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["mercury"] = new("res://assets/planets/mercury_spin_map.png", new Color(0.68f, 0.63f, 0.56f, 1f), 0.045f, 0.008f, 0.0015f, 1.08f, 0.96f, 0.50f),
            ["venus"] = new("res://assets/planets/venus_spin_map.png", new Color(1.00f, 0.70f, 0.25f, 1f), 0.220f, -0.005f, 0.0020f, 1.02f, 1.07f, 0.92f),
            ["mars"] = new("res://assets/planets/mars_spin_map.png", new Color(1.00f, 0.34f, 0.14f, 1f), 0.090f, 0.014f, 0.0020f, 1.08f, 1.08f, 0.64f),
            ["jupiter"] = new("res://assets/planets/jupiter_spin_map.png", new Color(1.00f, 0.78f, 0.44f, 1f), 0.135f, 0.034f, 0.0055f, 1.03f, 1.05f, 0.74f),
            ["saturn"] = new(
                "res://assets/planets/saturn_spin_map.png",
                new Color(1.00f, 0.83f, 0.48f, 1f),
                0.115f,
                0.028f,
                0.0035f,
                1.02f,
                1.04f,
                0.72f,
                new RingRenderSettings(0.55f, 0.96f, 0.30f, -0.24f, 0.80f, new Color(0.76f, 0.67f, 0.52f, 1f), new Color(1.00f, 0.92f, 0.72f, 1f))),
            ["uranus"] = new(
                "res://assets/planets/uranus_spin_map.png",
                new Color(0.54f, 0.96f, 1.00f, 1f),
                0.220f,
                -0.020f,
                0.0035f,
                1.02f,
                1.10f,
                0.98f,
                new RingRenderSettings(0.58f, 0.92f, 0.23f, -0.30f, 0.48f, new Color(0.58f, 0.72f, 0.82f, 1f), new Color(0.90f, 0.95f, 1.00f, 1f))),
            ["neptune"] = new("res://assets/planets/neptune_spin_map.png", new Color(0.25f, 0.56f, 1.00f, 1f), 0.230f, 0.022f, 0.0035f, 1.05f, 1.10f, 0.94f),
        };

    private readonly List<Texture2D> _sunFrames = new();
    private readonly HashSet<string> _activeSunFrameTexturePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D> _planetTextures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AnimatedPlanetView> _animatedPlanets = new(StringComparer.OrdinalIgnoreCase);

    private Texture2D? _spaceTexture;
    private Texture2D? _starTexture;
    private Texture2D? _moonTexture;
    private Font? _debugFont;
    private AnimatedEarthView? _animatedEarth;
    private StabilizedSunView? _sunView;
    private SpaceBackdropView? _backdropView;
    private string _loadedSunFrameDirectory = string.Empty;
    private string _loadedSunFramePrefix = string.Empty;
    private string _loadedContentSystemId = string.Empty;
    private StarSystemDefinition _currentSystem = SolarSystem.Sol;
    private WorldBounds _bounds = new(24000f, 16000f);
    private float _time;
    private bool _usesExternalVisualTime;
    private bool _currentSystemHasPendingResources;
    private double _pendingResourcePollElapsed;

    public StarSystemDefinition CurrentSystem => _currentSystem;
    public WorldBounds Bounds
    {
        get => _bounds;
        set
        {
            _bounds = value;
            if (_backdropView is not null)
            {
                _backdropView.Bounds = value;
            }
        }
    }

    public void SetSystem(StarSystemDefinition system)
    {
        _currentSystem = system;
        _time = 0f;
        PreloadSystemResources(system);
        if (IsInsideTree())
        {
            ReloadSystemContent();
        }
    }

    public void PreloadSystemResources(StarSystemDefinition system)
    {
        foreach (var path in EnumerateSystemTexturePaths(system))
        {
            RequestThreadedTexture(path);
        }
    }

    public void SetVisualTime(float timeSeconds)
    {
        _usesExternalVisualTime = true;
        _time = Math.Max(0f, timeSeconds);
        UpdateAnimatedSystemViews();
        _backdropView?.QueueBackdropRedrawIfCameraChanged();
        QueueRedraw();
    }

    public override void _Ready()
    {
        ZIndex = -100;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        LoadSpaceBackdropRenderer();
        _debugFont = ThemeDB.FallbackFont;
        ReloadSystemContent();
    }

    public override void _ExitTree()
    {
        _sunView?.SetFrames(Array.Empty<Texture2D>());
        _sunFrames.Clear();
        _activeSunFrameTexturePaths.Clear();
        _planetTextures.Clear();
        _spaceTexture = null;
        _starTexture = null;
        _moonTexture = null;
        if (_animatedEarth is not null)
        {
            _animatedEarth.SurfaceTexture = null;
            _animatedEarth.CloudTexture = null;
        }

        foreach (var planet in _animatedPlanets.Values)
        {
            planet.SurfaceTexture = null;
        }

        SunFrameCache.Clear();
        SunFramePathCache.Clear();
        SunFrameCacheOrder.Clear();
        DrainThreadedTextureRequests();
        TextureCache.Clear();
        ThreadedTextureRequests.Clear();
        MissingTexturePaths.Clear();
        _currentSystemHasPendingResources = false;
    }

    private void ReloadSystemContent()
    {
        var sameSystemReload = string.Equals(_loadedContentSystemId, _currentSystem.Id, StringComparison.OrdinalIgnoreCase);
        _currentSystemHasPendingResources = false;
        _pendingResourcePollElapsed = 0.0;
        if (sameSystemReload)
        {
            ReleaseTextureReferencesOnly();
        }
        else
        {
            ReleaseActiveSystemContent();
        }

        _spaceTexture = ShouldLoadSpaceTexture(_currentSystem.Background.TexturePath)
            ? LoadTexture(_currentSystem.Background.TexturePath)
            : null;
        _starTexture = UseSolarRendererForAllStars || string.IsNullOrWhiteSpace(_currentSystem.Star.TexturePath)
            ? null
            : LoadTexture(_currentSystem.Star.TexturePath);
        LoadSunFramesForCurrentSystem();
        EnsureStabilizedSun();
        LoadPlanetTextures();
        SyncAnimatedEarthForSystem();
        if (UseAnimatedNonEarthPlanets)
        {
            SyncAnimatedPlanets(sameSystemReload);
        }

        UpdateSpaceBackdropRenderer();
        UpdateStabilizedSun();
        UpdateAnimatedEarth();
        if (UseAnimatedNonEarthPlanets)
        {
            UpdateAnimatedPlanets();
        }

        QueueRedraw();
        _loadedContentSystemId = _currentSystem.Id;
    }

    private void ReleaseActiveSystemContent()
    {
        ReleaseTextureReferencesOnly();
        ReleaseAnimatedPlanets();
    }

    private void ReleaseTextureReferencesOnly()
    {
        _spaceTexture = null;
        _starTexture = null;
        _planetTextures.Clear();
    }

    private void LoadSpaceBackdropRenderer()
    {
        if (_backdropView is not null)
        {
            return;
        }

        _backdropView = new SpaceBackdropView
        {
            Bounds = Bounds
        };
        AddChild(_backdropView);
    }

    private void UpdateSpaceBackdropRenderer()
    {
        LoadSpaceBackdropRenderer();
        _backdropView!.Bounds = Bounds;
        _backdropView.SetSystem(_currentSystem, _spaceTexture);
    }

    public override void _Process(double delta)
    {
        if (_usesExternalVisualTime)
        {
            TryRefreshPendingSystemResources(delta);
            _backdropView?.QueueBackdropRedrawIfCameraChanged();
            return;
        }

        _time += (float)delta;
        TryRefreshPendingSystemResources(delta);
        UpdateAnimatedSystemViews();
        _backdropView?.QueueBackdropRedrawIfCameraChanged();
        QueueRedraw();
    }

    private void TryRefreshPendingSystemResources(double delta)
    {
        if (!_currentSystemHasPendingResources)
        {
            return;
        }

        _pendingResourcePollElapsed += delta;
        if (_pendingResourcePollElapsed < 0.18)
        {
            return;
        }

        _pendingResourcePollElapsed = 0.0;
        if (!SystemThreadedResourcesReady(_currentSystem))
        {
            return;
        }

        ReloadSystemContent();
    }

    private void UpdateAnimatedSystemViews()
    {
        if (!CameraIsInPrimaryGrid())
        {
            SetAnimatedSystemViewsVisible(false);
            return;
        }

        UpdateStabilizedSun();
        UpdateAnimatedEarth();
        if (UseAnimatedNonEarthPlanets)
        {
            UpdateAnimatedPlanets();
        }
    }

    public override void _Draw()
    {
        if (!CameraIsInPrimaryGrid())
        {
            return;
        }

        DrawOrbits();
        DrawPlanets();

        DrawSun();
    }

    private bool CameraIsInPrimaryGrid()
    {
        var cameraPosition = GetViewport().GetCamera2D()?.GlobalPosition ?? Vector2.Zero;
        return WorldGrid.IsPrimaryCell(new CoreVector2(cameraPosition.X, cameraPosition.Y), Bounds);
    }

    private void SetAnimatedSystemViewsVisible(bool visible)
    {
        if (_sunView is not null)
        {
            _sunView.Visible = visible;
        }

        if (_animatedEarth is not null)
        {
            _animatedEarth.Visible = visible;
        }

        foreach (var planet in _animatedPlanets.Values)
        {
            planet.Visible = visible;
        }
    }

    private void LoadSunFramesForCurrentSystem()
    {
        var requestedDirectory = StarFrameDirectory(_currentSystem.Star);
        var directory = PreferredGeneratedStarFrameDirectoryOrDefault(requestedDirectory, _currentSystem.Star);
        var prefix = StarFramePrefix(_currentSystem.Star);
        if (!string.Equals(directory, requestedDirectory, StringComparison.OrdinalIgnoreCase))
        {
            GD.Print($"Using primary generated star frames: {directory}");
        }

        var loadResult = LoadSunFrames(directory, prefix);
        if (loadResult == SunFrameLoadResult.Missing && !IsDefaultSolarFrameDirectory(directory))
        {
            LoadSunFrames(DefaultSolarFrameDirectory, "sun_");
        }

        PruneCachedSunFrameTextures();
    }

    private SunFrameLoadResult LoadSunFrames(string frameDirectory, string framePrefix)
    {
        var normalizedDirectory = frameDirectory.TrimEnd('/');
        var normalizedPrefix = string.IsNullOrWhiteSpace(framePrefix) ? "sun_" : framePrefix;
        if (_sunFrames.Count > 0
            && string.Equals(_loadedSunFrameDirectory, normalizedDirectory, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_loadedSunFramePrefix, normalizedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return SunFrameLoadResult.Loaded;
        }

        _sunFrames.Clear();
        _activeSunFrameTexturePaths.Clear();
        _loadedSunFrameDirectory = normalizedDirectory;
        _loadedSunFramePrefix = normalizedPrefix;

        var cacheKey = SunFrameCacheKey(normalizedDirectory, normalizedPrefix);
        EvictSunFrameCachesExcept(cacheKey);
        if (SunFrameCache.TryGetValue(cacheKey, out var cachedFrames))
        {
            _sunFrames.AddRange(cachedFrames);
            if (SunFramePathCache.TryGetValue(cacheKey, out var cachedPaths))
            {
                foreach (var path in cachedPaths)
                {
                    _activeSunFrameTexturePaths.Add(path);
                }
            }

            _sunView?.SetFrames(_sunFrames);
            return SunFrameLoadResult.Loaded;
        }

        var loadedFramePaths = new List<string>();
        var foundFrameResources = false;
        var frameLoadPending = false;
        for (var i = 0; i < 128; i++)
        {
            var path = $"{normalizedDirectory}/{normalizedPrefix}{i:00}.png";
            if (!ResourceLoader.Exists(path))
            {
                break;
            }

            foundFrameResources = true;
            var pendingBeforeLoad = IsThreadedTextureRequestInProgress(path);
            var texture = LoadTexture(path);
            if (texture is not null)
            {
                _sunFrames.Add(texture);
                loadedFramePaths.Add(path);
                _activeSunFrameTexturePaths.Add(path);
            }
            else if (pendingBeforeLoad || IsThreadedTextureRequestInProgress(path))
            {
                frameLoadPending = true;
            }
        }

        if (!_currentSystemHasPendingResources)
        {
            CacheSunFrames(cacheKey, _sunFrames, loadedFramePaths);
        }

        _sunView?.SetFrames(_sunFrames);
        if (_sunFrames.Count > 0)
        {
            return SunFrameLoadResult.Loaded;
        }

        return foundFrameResources && frameLoadPending
            ? SunFrameLoadResult.Loading
            : SunFrameLoadResult.Missing;
    }

    private static string SunFrameCacheKey(string frameDirectory, string framePrefix)
    {
        return $"{frameDirectory.TrimEnd('/')}|{framePrefix}";
    }

    private static void CacheSunFrames(string cacheKey, IReadOnlyList<Texture2D> frames, IReadOnlyList<string> framePaths)
    {
        if (frames.Count == 0 || SunFrameCache.ContainsKey(cacheKey))
        {
            return;
        }

        while (SunFrameCache.Count >= MaxCachedSunFrameSets && SunFrameCacheOrder.Count > 0)
        {
            var evictedKey = SunFrameCacheOrder.Dequeue();
            if (!string.Equals(evictedKey, cacheKey, StringComparison.OrdinalIgnoreCase))
            {
                RemoveSunFrameCacheEntry(evictedKey);
            }
        }

        SunFrameCache[cacheKey] = frames.ToArray();
        SunFramePathCache[cacheKey] = framePaths.ToArray();
        SunFrameCacheOrder.Enqueue(cacheKey);
    }

    private static void EvictSunFrameCachesExcept(string cacheKeyToKeep)
    {
        foreach (var key in SunFrameCache.Keys.ToArray())
        {
            if (!string.Equals(key, cacheKeyToKeep, StringComparison.OrdinalIgnoreCase))
            {
                RemoveSunFrameCacheEntry(key);
            }
        }

        RebuildSunFrameCacheOrder();
    }

    private static void RemoveSunFrameCacheEntry(string cacheKey)
    {
        SunFrameCache.Remove(cacheKey);
        if (SunFramePathCache.Remove(cacheKey, out var framePaths))
        {
            foreach (var path in framePaths)
            {
                TextureCache.Remove(path);
            }
        }
    }

    private static void RebuildSunFrameCacheOrder()
    {
        if (SunFrameCacheOrder.Count == 0)
        {
            return;
        }

        var retainedKeys = SunFrameCacheOrder
            .Where(SunFrameCache.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        SunFrameCacheOrder.Clear();
        foreach (var key in retainedKeys)
        {
            SunFrameCacheOrder.Enqueue(key);
        }
    }

    private void PruneCachedSunFrameTextures()
    {
        var keepPaths = new HashSet<string>(_activeSunFrameTexturePaths, StringComparer.OrdinalIgnoreCase);
        foreach (var framePaths in SunFramePathCache.Values)
        {
            foreach (var path in framePaths)
            {
                keepPaths.Add(path);
            }
        }

        foreach (var path in TextureCache.Keys.Where(IsSunFrameTexturePath).ToArray())
        {
            if (!keepPaths.Contains(path))
            {
                TextureCache.Remove(path);
            }
        }
    }

    private void EnsureStabilizedSun()
    {
        if (_sunFrames.Count < 4 || _currentSystemHasPendingResources)
        {
            return;
        }

        if (_sunView is not null)
        {
            _sunView.SetFrames(_sunFrames);
            return;
        }

        _sunView = new StabilizedSunView
        {
            WorldSize = _currentSystem.Star.WorldSize,
            Position = StarPosition,
            DiskTint = _currentSystem.Star.DiskTint,
            CoronaColor = _currentSystem.Star.CoronaColor,
            CoronaIntensity = _currentSystem.Star.CoronaIntensity,
            AnimationSpeed = _currentSystem.Star.AnimationSpeed,
            TintStrength = StarTintStrength(_currentSystem.Star)
        };
        _sunView.SetFrames(_sunFrames);
        AddChild(_sunView);
    }

    private void LoadPlanetTextures()
    {
        _planetTextures.Clear();
        foreach (var planet in _currentSystem.Planets)
        {
            if (UsesAnimatedPlanetRenderer(planet))
            {
                continue;
            }

            var texture = LoadTexture(planet.TexturePath);
            if (texture is not null)
            {
                _planetTextures[planet.Id] = texture;
            }
        }
    }

    private void SyncAnimatedEarthForSystem()
    {
        var hasEarth = _currentSystem.Planets.Any(planet => planet.Id == "earth");
        if (!hasEarth)
        {
            ReleaseAnimatedEarth();
            return;
        }

        var surface = LoadTexture("res://assets/planets/earth_surface_map.png");
        var clouds = LoadTexture("res://assets/planets/earth_clouds.png");
        if (surface is null || clouds is null)
        {
            if (_animatedEarth is not null)
            {
                _animatedEarth.SurfaceTexture = surface ?? _animatedEarth.SurfaceTexture;
                _animatedEarth.CloudTexture = clouds ?? _animatedEarth.CloudTexture;
            }

            _moonTexture ??= LoadTexture("res://assets/planets/moon.png");
            return;
        }

        if (_animatedEarth is null)
        {
            _animatedEarth = new AnimatedEarthView();
            AddChild(_animatedEarth);
        }

        _animatedEarth.SurfaceTexture = surface;
        _animatedEarth.CloudTexture = clouds;
        _moonTexture ??= LoadTexture("res://assets/planets/moon.png");
    }

    private void ReleaseAnimatedEarth()
    {
        if (_animatedEarth is not null)
        {
            RemoveChild(_animatedEarth);
            _animatedEarth.QueueFree();
            _animatedEarth = null;
        }

        _moonTexture = null;
    }

    private void SyncAnimatedPlanets(bool reuseExisting)
    {
        if (!reuseExisting)
        {
            ReleaseAnimatedPlanets();
        }

        var desiredPlanetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var planet in _currentSystem.Planets)
        {
            if (planet.Id == "earth" || RenderSettingsFor(planet) is not { } settings)
            {
                continue;
            }

            desiredPlanetIds.Add(planet.Id);
            var surface = LoadTexture(settings.SurfacePath);
            if (!_animatedPlanets.TryGetValue(planet.Id, out var view))
            {
                if (surface is null)
                {
                    continue;
                }

                view = new AnimatedPlanetView();
                _animatedPlanets[planet.Id] = view;
                AddChild(view);
            }

            ApplyAnimatedPlanetSettings(view, settings, surface);
        }

        if (!reuseExisting)
        {
            return;
        }

        foreach (var id in _animatedPlanets.Keys.ToArray())
        {
            if (desiredPlanetIds.Contains(id))
            {
                continue;
            }

            var view = _animatedPlanets[id];
            RemoveChild(view);
            view.QueueFree();
            _animatedPlanets.Remove(id);
        }
    }

    private static void ApplyAnimatedPlanetSettings(AnimatedPlanetView view, PlanetRenderSettings settings, Texture2D? surface)
    {
        if (surface is not null)
        {
            view.SurfaceTexture = surface;
        }

        view.AtmosphereColor = settings.AtmosphereColor;
        view.AtmosphereStrength = settings.AtmosphereStrength;
        view.RotationSpeed = settings.RotationSpeed;
        view.FlowStrength = settings.FlowStrength;
        view.Contrast = settings.Contrast;
        view.Saturation = settings.Saturation;
        view.GlowStrength = settings.GlowStrength;
        view.HasRings = settings.Rings is not null;
        if (settings.Rings is { } rings)
        {
            view.RingInnerRadiusFactor = rings.InnerRadiusFactor;
            view.RingOuterRadiusFactor = rings.OuterRadiusFactor;
            view.RingFlattening = rings.Flattening;
            view.RingRotation = rings.Rotation;
            view.RingAlpha = rings.Alpha;
            view.RingColor = rings.Color;
            view.RingAccentColor = rings.AccentColor;
        }

        view.ApplyVisualState();
    }

    private void ReleaseAnimatedPlanets()
    {
        foreach (var view in _animatedPlanets.Values.ToArray())
        {
            RemoveChild(view);
            view.QueueFree();
        }

        _animatedPlanets.Clear();
    }

    private Texture2D? LoadTexture(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            return null;
        }

        if (ResourceLoader.Exists(resourcePath))
        {
            if (TextureCache.TryGetValue(resourcePath, out var cachedTexture))
            {
                return cachedTexture;
            }

            if (ThreadedTextureRequests.Contains(resourcePath))
            {
                var status = ResourceLoader.LoadThreadedGetStatus(resourcePath);
                if (status == ResourceLoader.ThreadLoadStatus.Loaded)
                {
                    var threadedTexture = ResourceLoader.LoadThreadedGet(resourcePath) as Texture2D;
                    ThreadedTextureRequests.Remove(resourcePath);
                    if (threadedTexture is not null)
                    {
                        TextureCache[resourcePath] = threadedTexture;
                    }

                    return threadedTexture;
                }

                if (status == ResourceLoader.ThreadLoadStatus.InProgress)
                {
                    _currentSystemHasPendingResources = true;
                    return null;
                }

                ThreadedTextureRequests.Remove(resourcePath);
            }

            var texture = LoadTextureSync(resourcePath);
            if (texture is not null)
            {
                TextureCache[resourcePath] = texture;
            }

            return texture;
        }

        var globalPath = ProjectSettings.GlobalizePath(resourcePath);
        if (!System.IO.File.Exists(globalPath))
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

    private static Texture2D? LoadTextureSync(string resourcePath)
    {
        try
        {
            return ResourceLoader.Load<Texture2D>(resourcePath);
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> EnumerateSystemTexturePaths(StarSystemDefinition system)
    {
        if (ShouldLoadSpaceTexture(system.Background.TexturePath))
        {
            yield return system.Background.TexturePath;
        }

        if (!UseSolarRendererForAllStars && !string.IsNullOrWhiteSpace(system.Star.TexturePath))
        {
            yield return system.Star.TexturePath;
        }

        foreach (var path in EnumerateStarFramePaths(system.Star))
        {
            yield return path;
        }

        foreach (var planet in system.Planets)
        {
            var settings = RenderSettingsFor(planet);
            if (UseAnimatedNonEarthPlanets && planet.Id != "earth" && settings is not null)
            {
                yield return settings.SurfacePath;
            }
            else
            {
                yield return planet.TexturePath;
                if (settings is not null)
                {
                    yield return settings.SurfacePath;
                }
            }

            if (planet.Id == "earth")
            {
                yield return "res://assets/planets/earth_surface_map.png";
                yield return "res://assets/planets/earth_clouds.png";
                yield return "res://assets/planets/moon.png";
            }
        }
    }

    private static IEnumerable<string> EnumerateStarFramePaths(StarDefinition star)
    {
        var requestedDirectory = StarFrameDirectory(star);
        var directory = PreferredGeneratedStarFrameDirectoryOrDefault(requestedDirectory, star);
        var foundPreferredFrames = false;
        foreach (var path in EnumerateFramePaths(directory, StarFramePrefix(star)))
        {
            foundPreferredFrames = true;
            yield return path;
        }

        if (!foundPreferredFrames && !IsDefaultSolarFrameDirectory(directory))
        {
            foreach (var path in EnumerateFramePaths(DefaultSolarFrameDirectory, "sun_"))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> EnumerateFramePaths(string frameDirectory, string framePrefix)
    {
        var normalizedDirectory = frameDirectory.TrimEnd('/');
        var normalizedPrefix = string.IsNullOrWhiteSpace(framePrefix) ? "sun_" : framePrefix;
        for (var i = 0; i < 128; i++)
        {
            var path = $"{normalizedDirectory}/{normalizedPrefix}{i:00}.png";
            if (!ResourceLoader.Exists(path))
            {
                break;
            }

            yield return path;
        }
    }

    private static void RequestThreadedTexture(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath) || MissingTexturePaths.Contains(resourcePath) || ThreadedTextureRequests.Contains(resourcePath))
        {
            return;
        }

        if (TextureCache.ContainsKey(resourcePath))
        {
            return;
        }

        if (!ResourceLoader.Exists(resourcePath))
        {
            MissingTexturePaths.Add(resourcePath);
            return;
        }

        var error = ResourceLoader.LoadThreadedRequest(resourcePath, "Texture2D", useSubThreads: true, cacheMode: ResourceLoader.CacheMode.Reuse);
        if (error == Error.Ok)
        {
            ThreadedTextureRequests.Add(resourcePath);
        }
    }

    private static bool IsThreadedTextureRequestInProgress(string resourcePath)
    {
        if (!ThreadedTextureRequests.Contains(resourcePath))
        {
            return false;
        }

        try
        {
            return ResourceLoader.LoadThreadedGetStatus(resourcePath) == ResourceLoader.ThreadLoadStatus.InProgress;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSunFrameTexturePath(string resourcePath)
    {
        var normalizedPath = resourcePath.Replace('\\', '/');
        return normalizedPath.Contains("/assets/backgrounds/sun/", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.Contains("/assets/generated/star_frames/", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.Contains("/assets/generated/star_frames_experimental/", StringComparison.OrdinalIgnoreCase);
    }

    private static void DrainThreadedTextureRequests()
    {
        foreach (var path in ThreadedTextureRequests.ToArray())
        {
            try
            {
                if (!ResourceLoader.Exists(path))
                {
                    continue;
                }

                var status = ResourceLoader.LoadThreadedGetStatus(path);
                if (status is ResourceLoader.ThreadLoadStatus.Loaded or ResourceLoader.ThreadLoadStatus.InProgress)
                {
                    _ = ResourceLoader.LoadThreadedGet(path);
                }
            }
            catch
            {
                // Shutdown cleanup only: avoid turning diagnostic resource drain into a crash path.
            }
        }
    }

    private static bool SystemThreadedResourcesReady(StarSystemDefinition system)
    {
        foreach (var path in EnumerateSystemTexturePaths(system))
        {
            if (!ThreadedTextureRequests.Contains(path))
            {
                continue;
            }

            if (ResourceLoader.LoadThreadedGetStatus(path) == ResourceLoader.ThreadLoadStatus.InProgress)
            {
                return false;
            }
        }

        return true;
    }

    private void DrawSun()
    {
        if (_sunView?.IsAvailable == true)
        {
            return;
        }

        var star = _currentSystem.Star;
        if (!IsCircleVisible(CurrentVisibleWorldRect(120f), StarPosition, star.WorldSize * 0.58f))
        {
            return;
        }

        var scale = star.WorldSize / SolarSystem.SunVisualWorldSize;
        var slow = _time * 0.26f * star.AnimationSpeed;

        if (_starTexture is not null)
        {
            DrawGeneratedStar(star, scale);
        }
        else if (_sunFrames.Count > 0)
        {
            var frameProgress = _time * 18f;
            var frame = (int)MathF.Floor(frameProgress) % _sunFrames.Count;
            var nextFrame = (frame + 1) % _sunFrames.Count;
            var blend = SmoothStep(frameProgress - MathF.Floor(frameProgress));
            var size = star.WorldSize;
            var rect = new Rect2(StarPosition - new Vector2(size * 0.5f, size * 0.5f), new Vector2(size, size));
            DrawTextureRect(_sunFrames[frame], rect, false, star.DiskTint);
            DrawTextureRect(_sunFrames[nextFrame], rect, false, WithAlpha(star.DiskTint, blend));
        }
        else
        {
            var corona = star.CoronaColor;
            var disk = star.DiskTint;
            for (var i = 10; i >= 1; i--)
            {
                var radius = 170f + i * 48f;
                var alpha = 0.012f * (11 - i);
                DrawCircle(StarPosition, radius, WithAlpha(corona, alpha * star.CoronaIntensity));
            }

            DrawCircle(StarPosition, 430f, WithAlpha(corona, 0.98f));
            DrawCircle(StarPosition, 336f, disk);
            DrawCircle(StarPosition + new Vector2(-74f, -48f), 76f, new Color(1f, 0.95f, 0.32f, 0.75f));
        }

        for (var i = 0; i < 8; i++)
        {
            var radius = (480f + i * 18f + MathF.Sin(_time * 0.28f * star.AnimationSpeed + i) * 2.8f) * scale;
            var start = slow * (i % 2 == 0 ? 1f : -0.7f) + i * 0.8f;
            DrawArc(StarPosition, radius, start, start + 1.1f, SunCoronaArcSegments, WithAlpha(star.CoronaColor, 0.045f * star.CoronaIntensity), 3.2f * scale, true);
        }
    }

    private void DrawGeneratedStar(StarDefinition star, float scale)
    {
        if (_starTexture is null)
        {
            return;
        }

        var pulse = 0.5f + 0.5f * MathF.Sin(_time * 1.65f * star.AnimationSpeed);
        var slowPulse = 0.5f + 0.5f * MathF.Sin(_time * 0.58f * star.AnimationSpeed + 1.7f);
        var corona = star.CoronaColor;
        var baseSize = star.WorldSize;

        for (var i = 7; i >= 1; i--)
        {
            var radius = baseSize * (0.54f + i * 0.105f + pulse * 0.012f);
            var alpha = (0.015f + i * 0.004f) * star.CoronaIntensity * (0.76f + pulse * 0.34f);
            DrawCircle(StarPosition, radius, WithAlpha(corona, alpha));
        }

        for (var i = 0; i < 12; i++)
        {
            var angle = i / 12f * MathF.Tau + _time * (0.18f + i * 0.003f) * star.AnimationSpeed;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var tangent = new Vector2(-direction.Y, direction.X);
            var inner = baseSize * (0.43f + 0.018f * MathF.Sin(_time * 0.9f + i));
            var outer = baseSize * (0.63f + 0.055f * Hash01(i * 13.17f) + pulse * 0.026f);
            var width = baseSize * (0.018f + Hash01(i * 7.31f) * 0.018f);
            var alpha = (0.035f + Hash01(i * 3.73f) * 0.035f) * star.CoronaIntensity;
            DrawLine(
                StarPosition + direction * inner - tangent * width * 0.35f,
                StarPosition + direction * outer + tangent * width * 0.35f,
                WithAlpha(corona, alpha),
                Math.Clamp(width, 2f, 22f),
                true);
        }

        var glowSize = baseSize * (1.045f + pulse * 0.026f);
        DrawTextureRect(
            _starTexture,
            new Rect2(StarPosition - Vector2.One * glowSize * 0.5f, Vector2.One * glowSize),
            false,
            WithAlpha(corona, 0.18f * star.CoronaIntensity));

        var bodySize = baseSize * (0.992f + slowPulse * 0.014f);
        DrawTextureRect(
            _starTexture,
            new Rect2(StarPosition - Vector2.One * bodySize * 0.5f, Vector2.One * bodySize),
            false,
            Colors.White);

        if (_sunFrames.Count > 0)
        {
            var frameProgress = _time * (11.0f + star.AnimationSpeed * 7.0f);
            var frame = (int)MathF.Floor(frameProgress) % _sunFrames.Count;
            var nextFrame = (frame + 1) % _sunFrames.Count;
            var blend = SmoothStep(frameProgress - MathF.Floor(frameProgress));
            var overlaySize = bodySize * (0.965f + pulse * 0.012f);
            var overlayRect = new Rect2(StarPosition - Vector2.One * overlaySize * 0.5f, Vector2.One * overlaySize);
            var overlayAlpha = 0.16f * star.CoronaIntensity;
            DrawTextureRect(_sunFrames[frame], overlayRect, false, WithAlpha(star.DiskTint, overlayAlpha));
            DrawTextureRect(_sunFrames[nextFrame], overlayRect, false, WithAlpha(star.DiskTint, overlayAlpha * blend));
        }

        for (var i = 0; i < 10; i++)
        {
            var radius = (480f + i * 24f + MathF.Sin(_time * 0.32f * star.AnimationSpeed + i) * 7f) * scale;
            var start = _time * (0.17f + i * 0.008f) * star.AnimationSpeed + i * 0.64f;
            var arcAlpha = (0.028f + pulse * 0.022f) * star.CoronaIntensity;
            DrawArc(StarPosition, radius, start, start + 0.82f + Hash01(i * 2.11f) * 0.62f, SunCoronaArcSegments, WithAlpha(corona, arcAlpha), Math.Clamp(2.2f * scale, 1.2f, 5.4f), true);
        }

        DrawCircle(StarPosition, baseSize * (0.18f + pulse * 0.025f), WithAlpha(new Color(1f, 0.96f, 0.72f, 1f), 0.055f + pulse * 0.035f));
    }

    private void UpdateStabilizedSun()
    {
        if (_sunView is null)
        {
            return;
        }

        var star = _currentSystem.Star;
        var visible = IsCircleVisible(CurrentVisibleWorldRect(120f), StarPosition, star.WorldSize * 0.58f);
        _sunView.Visible = visible;
        if (!visible)
        {
            return;
        }

        _sunView.Position = StarPosition;
        _sunView.WorldSize = star.WorldSize;
        _sunView.DiskTint = star.DiskTint;
        _sunView.CoronaColor = star.CoronaColor;
        _sunView.CoronaIntensity = star.CoronaIntensity;
        _sunView.AnimationSpeed = star.AnimationSpeed;
        _sunView.TintStrength = StarTintStrength(star);
        _sunView.TimeSeconds = _time;
        _sunView.QueueSunRedraw();
    }

    private static float StarTintStrength(StarDefinition star)
    {
        if (!string.IsNullOrWhiteSpace(star.FrameDirectory) && !IsDefaultSolarFrameDirectory(StarFrameDirectory(star)))
        {
            return 0.28f;
        }

        return string.IsNullOrWhiteSpace(star.TexturePath) ? 0f : 0.82f;
    }

    private const string DefaultSolarFrameDirectory = "res://assets/backgrounds/sun";
    private const string GeneratedStarFrameRoot = "res://assets/generated/star_frames";
    private const string ExperimentalGeneratedStarFrameRoot = "res://assets/generated/star_frames_experimental";

    private static string StarFrameDirectory(StarDefinition star)
    {
        return string.IsNullOrWhiteSpace(star.FrameDirectory)
            ? DefaultSolarFrameDirectory
            : star.FrameDirectory.TrimEnd('/');
    }

    private static string StarFramePrefix(StarDefinition star)
    {
        return string.IsNullOrWhiteSpace(star.FramePrefix)
            ? "sun_"
            : star.FramePrefix;
    }

    private static bool IsDefaultSolarFrameDirectory(string frameDirectory)
    {
        return string.Equals(frameDirectory.TrimEnd('/'), DefaultSolarFrameDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static string PreferredGeneratedStarFrameDirectoryOrDefault(string frameDirectory, StarDefinition star)
    {
        if (UseStableGeneratedStarFrames() || IsDefaultSolarFrameDirectory(frameDirectory))
        {
            return frameDirectory;
        }

        var normalizedDirectory = frameDirectory.TrimEnd('/');
        if (!normalizedDirectory.StartsWith($"{GeneratedStarFrameRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            return frameDirectory;
        }

        var variantId = normalizedDirectory[(GeneratedStarFrameRoot.Length + 1)..];
        if (string.IsNullOrWhiteSpace(variantId))
        {
            return frameDirectory;
        }

        var experimentalDirectory = $"{ExperimentalGeneratedStarFrameRoot}/{variantId}";
        var firstFramePath = $"{experimentalDirectory}/{StarFramePrefix(star)}00.png";
        return ResourceLoader.Exists(firstFramePath) ? experimentalDirectory : frameDirectory;
    }

    private static bool UseStableGeneratedStarFrames()
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (string.Equals(arg, "--stable-star-frames", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (arg.StartsWith("--stable-star-frames=", StringComparison.OrdinalIgnoreCase))
            {
                return IsTruthyFlagValue(arg["--stable-star-frames=".Length..]);
            }

            if (arg.StartsWith("--experimental-star-frames=", StringComparison.OrdinalIgnoreCase))
            {
                return IsFalsyFlagValue(arg["--experimental-star-frames=".Length..]);
            }

            if (arg.StartsWith("--star-frames=", StringComparison.OrdinalIgnoreCase))
            {
                var value = arg["--star-frames=".Length..];
                if (string.Equals(value, "stable", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, "classic", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsFalsyFlagValue(string value)
    {
        return string.Equals(value, "0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTruthyFlagValue(string value)
    {
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldLoadSpaceTexture(string texturePath)
    {
        return !IsLegacyFullscreenBackgroundTexturePath(texturePath);
    }

    private static float SmoothStep(float value)
    {
        var t = Math.Clamp(value, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private void DrawOrbits()
    {
        var visibleWorld = CurrentVisibleWorldRect(120f);
        foreach (var planet in _currentSystem.Planets)
        {
            if (!OrbitIntersectsRect(visibleWorld, StarPosition, planet.OrbitRadius, 10f))
            {
                continue;
            }

            var alpha = planet.OrbitRadius < 3200f ? 0.044f : 0.032f;
            DrawArc(StarPosition, planet.OrbitRadius, 0f, MathF.Tau, OrbitArcSegments, new Color(0.18f, 0.86f, 0.76f, alpha), 1.0f, true);
        }
    }

    private void DrawPlanets()
    {
        var visibleWorld = CurrentVisibleWorldRect(160f);
        foreach (var planet in _currentSystem.Planets)
        {
            var position = SolarSystem.PositionAt(planet, _time);
            var settings = RenderSettingsFor(planet);
            var usesAnimatedPlanetRenderer = UsesAnimatedPlanetRenderer(planet);
            if (!IsCircleVisible(visibleWorld, position, PlanetCullRadius(planet, settings)))
            {
                continue;
            }

            var eclipse = Math.Clamp(position.X / Math.Max(1f, planet.OrbitRadius) * 0.24f + 0.76f, 0.56f, 1f);
            var glow = planet.MapColor;
            var useAnimatedEarth = planet.Id == "earth" && _animatedEarth?.IsAvailable == true;
            var useAnimatedPlanet = usesAnimatedPlanetRenderer
                && _animatedPlanets.TryGetValue(planet.Id, out var animatedPlanet)
                && animatedPlanet.IsAvailable;

            for (var i = 3; i >= 1; i--)
            {
                DrawCircle(position, planet.BodyRadius + i * 10f, new Color(glow.R, glow.G, glow.B, 0.017f / i));
            }

            if (useAnimatedEarth || useAnimatedPlanet)
            {
                // Animated planet views draw as child canvas items; avoid the old solid fallback disk behind them.
            }
            else if (!usesAnimatedPlanetRenderer && _planetTextures.TryGetValue(planet.Id, out var texture))
            {
                var textureSize = new Vector2(planet.TextureWorldSize, planet.TextureWorldSize);
                var rect = new Rect2(position - textureSize * 0.5f, textureSize);
                DrawTextureRect(texture, rect, false, new Color(eclipse, eclipse, eclipse, 1f));
            }
            else
            {
                DrawCircle(position, planet.BodyRadius, new Color(planet.MapColor.R * eclipse, planet.MapColor.G * eclipse, planet.MapColor.B * eclipse, 1f));
                DrawCircle(position + new Vector2(-planet.BodyRadius * 0.24f, -planet.BodyRadius * 0.2f), planet.BodyRadius * 0.48f, new Color(1f, 1f, 1f, 0.12f));
            }

            if (planet.Id == "earth")
            {
                var moonPosition = position + new Vector2(planet.BodyRadius * 1.32f, -planet.BodyRadius * 0.22f);
                if (_moonTexture is not null)
                {
                    var moonSize = new Vector2(42f, 42f);
                    DrawTextureRect(_moonTexture, new Rect2(moonPosition - moonSize * 0.5f, moonSize), false, new Color(0.86f, 0.9f, 0.84f, 0.96f));
                }
                else
                {
                    DrawCircle(moonPosition, 18f, new Color(0.78f, 0.82f, 0.72f, 0.95f));
                }
            }

            DrawPlanetScaleDebugLabel(planet, position);
        }
    }

    private void DrawPlanetScaleDebugLabel(PlanetDefinition planet, Vector2 position)
    {
        if (!ShowPlanetScaleDebugLabels || _debugFont is null)
        {
            return;
        }

        var text = $"{planet.DisplayName} {planet.ReferenceTextureWorldSize:0}";
        var labelPosition = position + new Vector2(-planet.TextureWorldSize * 0.42f, -planet.BodyRadius - 28f);
        DrawString(_debugFont, labelPosition + new Vector2(2f, 2f), text, HorizontalAlignment.Left, -1f, 28, new Color(0f, 0f, 0f, 0.72f));
        DrawString(_debugFont, labelPosition, text, HorizontalAlignment.Left, -1f, 28, new Color(0.72f, 0.96f, 1f, 0.92f));
    }

    private void UpdateAnimatedEarth()
    {
        if (_animatedEarth is null)
        {
            return;
        }

        var earth = _currentSystem.Planets.FirstOrDefault(planet => planet.Id == "earth");
        if (earth is null || !_animatedEarth.IsAvailable)
        {
            _animatedEarth.Visible = false;
            return;
        }

        var position = SolarSystem.PositionAt(earth, _time);
        if (!IsCircleVisible(CurrentVisibleWorldRect(180f), position, PlanetCullRadius(earth, null)))
        {
            _animatedEarth.Visible = false;
            return;
        }

        var daylight = Math.Clamp(position.X / Math.Max(1f, earth.OrbitRadius) * 0.24f + 0.76f, 0.56f, 1f);
        _animatedEarth.Visible = true;
        _animatedEarth.Position = position;
        _animatedEarth.Diameter = earth.TextureWorldSize;
        _animatedEarth.TimeSeconds = _time;
        _animatedEarth.Daylight = daylight;
        _animatedEarth.QueueRedraw();
    }

    private void UpdateAnimatedPlanets()
    {
        var visibleWorld = CurrentVisibleWorldRect(220f);
        foreach (var planet in _currentSystem.Planets)
        {
            if (planet.Id == "earth" || !_animatedPlanets.TryGetValue(planet.Id, out var view))
            {
                continue;
            }

            if (RenderSettingsFor(planet) is not { } settings)
            {
                view.Visible = false;
                continue;
            }

            var position = SolarSystem.PositionAt(planet, _time);
            if (!IsCircleVisible(visibleWorld, position, PlanetCullRadius(planet, settings)))
            {
                view.Visible = false;
                continue;
            }

            var daylight = Math.Clamp(position.X / Math.Max(1f, planet.OrbitRadius) * 0.24f + 0.76f, 0.56f, 1f);
            view.Visible = true;
            view.Position = position;
            view.TextureWorldSize = planet.TextureWorldSize;
            view.BodyDiameter = planet.BodyRadius * 2f;
            view.TimeSeconds = _time;
            view.Daylight = daylight;
            view.ApplyVisualState();
            if (!view.IsAvailable)
            {
                view.Visible = false;
                continue;
            }
        }
    }

    private Rect2 CurrentVisibleWorldRect(float margin)
    {
        var camera = GetViewport().GetCamera2D();
        var cameraPosition = camera?.GlobalPosition ?? Vector2.Zero;
        var zoom = camera?.Zoom ?? Vector2.One;
        var viewportSize = GetViewportRect().Size;
        var half = new Vector2(
            viewportSize.X / Math.Max(0.001f, zoom.X),
            viewportSize.Y / Math.Max(0.001f, zoom.Y)) * 0.5f + new Vector2(margin, margin);

        return new Rect2(cameraPosition - half, half * 2f);
    }

    private static bool IsCircleVisible(Rect2 rect, Vector2 position, float radius)
    {
        var min = rect.Position;
        var max = rect.Position + rect.Size;
        return position.X + radius >= min.X
            && position.X - radius <= max.X
            && position.Y + radius >= min.Y
            && position.Y - radius <= max.Y;
    }

    private static bool OrbitIntersectsRect(Rect2 rect, Vector2 center, float radius, float strokeMargin)
    {
        var min = rect.Position;
        var max = rect.Position + rect.Size;
        var closestX = Math.Clamp(center.X, min.X, max.X);
        var closestY = Math.Clamp(center.Y, min.Y, max.Y);
        var closestDistance = center.DistanceTo(new Vector2(closestX, closestY));

        var farthestDistance = Math.Max(
            Math.Max(center.DistanceTo(min), center.DistanceTo(new Vector2(max.X, min.Y))),
            Math.Max(center.DistanceTo(max), center.DistanceTo(new Vector2(min.X, max.Y))));

        return closestDistance <= radius + strokeMargin && farthestDistance >= radius - strokeMargin;
    }

    private static PlanetRenderSettings? RenderSettingsFor(PlanetDefinition planet)
    {
        if (planet.Visual is { } visual)
        {
            return new PlanetRenderSettings(
                visual.SurfacePath,
                visual.AtmosphereColor,
                visual.AtmosphereStrength,
                visual.RotationSpeed,
                visual.FlowStrength,
                visual.Contrast,
                visual.Saturation,
                visual.GlowStrength,
                visual.Rings is null
                    ? null
                    : new RingRenderSettings(
                        visual.Rings.InnerRadiusFactor,
                        visual.Rings.OuterRadiusFactor,
                        visual.Rings.Flattening,
                        visual.Rings.Rotation,
                        visual.Rings.Alpha,
                        visual.Rings.Color,
                        visual.Rings.AccentColor));
        }

        return PlanetRenderSettingsById.TryGetValue(planet.Id, out var settings)
            ? settings
            : null;
    }

    private static bool UsesAnimatedPlanetRenderer(PlanetDefinition planet)
    {
        return UseAnimatedNonEarthPlanets
            && planet.Id != "earth"
            && RenderSettingsFor(planet) is not null;
    }

    private static float PlanetCullRadius(PlanetDefinition planet, PlanetRenderSettings? settings)
    {
        var radius = Math.Max(planet.BodyRadius, planet.TextureWorldSize * 0.5f) + 80f;
        if (settings?.Rings is { } rings)
        {
            radius = Math.Max(radius, planet.BodyRadius * 2f * rings.OuterRadiusFactor + 96f);
        }

        return radius;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, Math.Clamp(alpha, 0f, 1f));
    }

    private static float Hash01(float value)
    {
        var hashed = MathF.Sin(value) * 43758.5453f;
        return hashed - MathF.Floor(hashed);
    }

    private static bool IsLegacyFullscreenBackgroundTexturePath(string texturePath)
    {
        return texturePath.Contains("assets/generated/backgrounds", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record PlanetRenderSettings(
        string SurfacePath,
        Color AtmosphereColor,
        float AtmosphereStrength,
        float RotationSpeed,
        float FlowStrength,
        float Contrast,
        float Saturation,
        float GlowStrength,
        RingRenderSettings? Rings = null);

    private sealed record RingRenderSettings(
        float InnerRadiusFactor,
        float OuterRadiusFactor,
        float Flattening,
        float Rotation,
        float Alpha,
        Color Color,
        Color AccentColor);

    private enum SunFrameLoadResult
    {
        Missing,
        Loading,
        Loaded
    }
}
