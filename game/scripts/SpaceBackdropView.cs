using Godot;
using SpaceManagers.Core;

namespace SpaceManagersPrototype;

public partial class SpaceBackdropView : Node2D
{
    private const bool UseGeneratedNebulaOverlay = false;
    private const float GeneratedNebulaOverlayAlphaScale = 0.52f;
    private const float TexturePrimaryLayerAlpha = 1.0f;
    private const float TextureSecondaryLayerAlpha = 0.055f;
    private const float TextureTertiaryLayerAlpha = 0.035f;
    private static readonly Color BaseBackgroundColor = new(0.0f, 0.006f, 0.012f, 1f);

    private readonly List<BackdropStarPoint> _stars = new();
    private readonly List<BackdropNebulaBlob> _nebulae = new();

    private SpaceBackdropTextureLayer? _textureLayer;
    private GeneratedNebulaOverlayLayer? _nebulaLayer;
    private ProceduralStarfieldLayer? _starLayer;
    private StarSystemDefinition _system = SolarSystem.Sol;
    private Texture2D? _spaceTexture;
    private WorldBounds _bounds = new(24000f, 16000f);

    public WorldBounds Bounds
    {
        get => _bounds;
        set
        {
            _bounds = value;
            PropagateLayerState();
        }
    }

    public override void _Ready()
    {
        ZIndex = -2;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;

        _textureLayer = new SpaceBackdropTextureLayer { ZIndex = 0 };
        AddChild(_textureLayer);

        _nebulaLayer = new GeneratedNebulaOverlayLayer { ZIndex = 1, Visible = false };
        AddChild(_nebulaLayer);

        _starLayer = new ProceduralStarfieldLayer { ZIndex = 2 };
        AddChild(_starLayer);

        PropagateLayerState();
    }

    public void SetSystem(StarSystemDefinition system, Texture2D? spaceTexture)
    {
        _system = system;
        _spaceTexture = spaceTexture;
        GenerateField();
        PropagateLayerState();
        QueueBackdropRedraw();
    }

    public void QueueBackdropRedraw()
    {
        _textureLayer?.QueueRedraw();
        _nebulaLayer?.QueueRedraw();
        _starLayer?.QueueRedraw();
    }

    private void GenerateField()
    {
        _stars.Clear();
        _nebulae.Clear();

        var background = _system.Background;
        var rng = new RandomNumberGenerator();
        rng.Seed = (ulong)Math.Max(1, background.StarfieldSeed);

        var starCount = IsLegacyFullscreenBackgroundTexturePath(background.TexturePath)
            ? Math.Min(background.StarCount * 8, 9000)
            : background.StarCount;
        for (var i = 0; i < starCount; i++)
        {
            var position = new Vector2(
                rng.RandfRange(-Bounds.HalfWidth, Bounds.HalfWidth),
                rng.RandfRange(-Bounds.HalfHeight, Bounds.HalfHeight));
            var radius = IsLegacyFullscreenBackgroundTexturePath(background.TexturePath)
                ? rng.RandfRange(0.55f, rng.RandfRange(1.1f, 2.45f))
                : rng.RandfRange(0.35f, rng.RandfRange(0.8f, 1.9f));
            var tint = rng.RandfRange(0.78f, 1f);
            var mix = rng.RandfRange(0.15f, 0.42f);
            var backdrop = background.TextureTint;
            var color = new Color(
                tint * Lerp(1f, backdrop.R, mix) * rng.RandfRange(0.82f, 1f),
                tint * Lerp(1f, backdrop.G, mix) * rng.RandfRange(0.9f, 1f),
                tint * Lerp(1f, backdrop.B, mix),
                rng.RandfRange(0.34f, 0.86f));
            _stars.Add(new BackdropStarPoint(position, radius, color));
        }

        rng.Seed = (ulong)Math.Max(1, background.NebulaSeed);
        var palette = background.NebulaPalette.Count > 0
            ? background.NebulaPalette
            : SolarSystem.Sol.Background.NebulaPalette;

        for (var i = 0; i < background.NebulaBlobCount; i++)
        {
            var position = new Vector2(
                rng.RandfRange(-Bounds.HalfWidth * 0.92f, Bounds.HalfWidth * 0.92f),
                rng.RandfRange(-Bounds.HalfHeight * 0.92f, Bounds.HalfHeight * 0.92f));
            var radius = rng.RandfRange(1200f, 4300f);
            var color = palette[rng.RandiRange(0, palette.Count - 1)];
            var alpha = rng.RandfRange(0.014f, 0.032f) + background.DustDensity * 0.052f;
            _nebulae.Add(new BackdropNebulaBlob(position, radius, WithAlpha(color, Math.Clamp(alpha, 0.014f, 0.072f))));
        }
    }

    private void PropagateLayerState()
    {
        if (_textureLayer is not null)
        {
            _textureLayer.Bounds = Bounds;
            _textureLayer.Background = _system.Background;
            _textureLayer.SpaceTexture = _spaceTexture;
        }

        if (_nebulaLayer is not null)
        {
            _nebulaLayer.Background = _system.Background;
            _nebulaLayer.Nebulae = _nebulae;
            _nebulaLayer.Visible = UseGeneratedNebulaOverlay
                && !string.Equals(_system.Id, SolarSystem.Sol.Id, StringComparison.OrdinalIgnoreCase);
        }

        if (_starLayer is not null)
        {
            _starLayer.Background = _system.Background;
            _starLayer.Stars = _stars;
        }
    }

    private static float Lerp(float from, float to, float amount)
    {
        return from + (to - from) * amount;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, Math.Clamp(alpha, 0f, 1f));
    }

    private static bool IsLegacyFullscreenBackgroundTexturePath(string texturePath)
    {
        return texturePath.Contains("assets/generated/backgrounds", StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct BackdropStarPoint(Vector2 Position, float Radius, Color Color);

    private readonly record struct BackdropNebulaBlob(Vector2 Position, float Radius, Color Color);

    private partial class SpaceBackdropTextureLayer : Node2D
    {
        public Texture2D? SpaceTexture { get; set; }
        public SpaceBackdropDefinition Background { get; set; } = SolarSystem.Sol.Background;
        public WorldBounds Bounds { get; set; } = new(24000f, 16000f);

        public override void _Ready()
        {
            TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        }

        public override void _Draw()
        {
            DrawRect(new Rect2(-Bounds.HalfWidth, -Bounds.HalfHeight, Bounds.HalfWidth * 2f, Bounds.HalfHeight * 2f), BaseBackgroundColor, true);
            if (SpaceTexture is null)
            {
                return;
            }

            var camera = GetViewport().GetCamera2D();
            var cameraPosition = camera?.GlobalPosition ?? Vector2.Zero;
            var zoom = camera?.Zoom ?? Vector2.One;
            var viewportSize = GetViewportRect().Size;
            var viewWorldSize = new Vector2(
                viewportSize.X / Math.Max(0.001f, zoom.X),
                viewportSize.Y / Math.Max(0.001f, zoom.Y)) * 1.2f;
            var textureSizePixels = SpaceTexture.GetSize();
            var tileSize = new Vector2(textureSizePixels.X, textureSizePixels.Y);
            var viewMin = cameraPosition - viewWorldSize * 0.5f;
            var viewMax = cameraPosition + viewWorldSize * 0.5f;
            var tint = TextureModulate(Background);
            var primaryLayer = GetTextureLayerVariant(Background, 0);
            var secondaryLayer = GetTextureLayerVariant(Background, 1);
            var tertiaryLayer = GetTextureLayerVariant(Background, 2);
            var primaryTileSize = ScaleTileSize(tileSize, primaryLayer.Scale);
            var secondaryTileSize = ScaleTileSize(tileSize, secondaryLayer.Scale);
            var tertiaryTileSize = ScaleTileSize(tileSize, tertiaryLayer.Scale);
            var primaryOrigin = cameraPosition * (1f - Background.TextureParallax) + TexturePhaseOffset(Background, primaryTileSize, 0);

            DrawTextureTilePass(
                SpaceTexture,
                primaryOrigin,
                primaryTileSize,
                viewMin,
                viewMax,
                WithTextureAlpha(tint, primaryLayer.Alpha));

            DrawTextureTilePass(
                SpaceTexture,
                cameraPosition * (1f - LayerParallax(Background, 1)) + TexturePhaseOffset(Background, secondaryTileSize, 1) + TextureLayerOffset(Background, secondaryTileSize, 1),
                secondaryTileSize,
                viewMin,
                viewMax,
                WithTextureAlpha(tint, secondaryLayer.Alpha));

            DrawTextureTilePass(
                SpaceTexture,
                cameraPosition * (1f - LayerParallax(Background, 2)) + TexturePhaseOffset(Background, tertiaryTileSize, 2) + TextureLayerOffset(Background, tertiaryTileSize, 2),
                tertiaryTileSize,
                viewMin,
                viewMax,
                WithTextureAlpha(tint, tertiaryLayer.Alpha));
        }

        private void DrawTextureTilePass(Texture2D texture, Vector2 patternOrigin, Vector2 tileSize, Vector2 viewMin, Vector2 viewMax, Color tint)
        {
            var startX = patternOrigin.X + MathF.Floor((viewMin.X - patternOrigin.X) / tileSize.X) * tileSize.X;
            var startY = patternOrigin.Y + MathF.Floor((viewMin.Y - patternOrigin.Y) / tileSize.Y) * tileSize.Y;
            for (var y = startY; y < viewMax.Y; y += tileSize.Y)
            {
                for (var x = startX; x < viewMax.X; x += tileSize.X)
                {
                    DrawTextureRect(texture, new Rect2(new Vector2(x, y), tileSize), false, tint);
                }
            }
        }

        private static Color WithTextureAlpha(Color color, float alpha)
        {
            return new Color(color.R, color.G, color.B, Math.Clamp(alpha, 0f, 1f));
        }

        private static TextureLayerVariant GetTextureLayerVariant(SpaceBackdropDefinition background, int layer)
        {
            if (!IsGeneratedHighresTexturePath(background.TexturePath))
            {
                return layer switch
                {
                    1 => new TextureLayerVariant(new Vector2(1.021f, 1.021f), TextureSecondaryLayerAlpha),
                    2 => new TextureLayerVariant(new Vector2(0.987f, 0.987f), TextureTertiaryLayerAlpha),
                    _ => new TextureLayerVariant(Vector2.One, TexturePrimaryLayerAlpha),
                };
            }

            var hash = StableHash($"{TextureVariationKey(background)}|variant:{layer}");
            if (layer == 1)
            {
                return new TextureLayerVariant(
                    new Vector2(0.76f + HashUnit(hash, 0) * 0.48f, 0.82f + HashUnit(hash, 8) * 0.42f),
                    0.042f + HashUnit(hash, 16) * 0.058f);
            }

            if (layer == 2)
            {
                return new TextureLayerVariant(
                    new Vector2(1.07f + HashUnit(hash, 0) * 0.52f, 0.72f + HashUnit(hash, 8) * 0.46f),
                    0.024f + HashUnit(hash, 16) * 0.046f);
            }

            return new TextureLayerVariant(
                new Vector2(0.91f + HashUnit(hash, 0) * 0.22f, 0.92f + HashUnit(hash, 8) * 0.20f),
                TexturePrimaryLayerAlpha);
        }

        private static float LayerParallax(SpaceBackdropDefinition background, int layer)
        {
            if (!IsGeneratedHighresTexturePath(background.TexturePath))
            {
                return background.TextureParallax;
            }

            var hash = StableHash($"{TextureVariationKey(background)}|parallax:{layer}");
            var offset = layer == 1
                ? -0.018f + HashUnit(hash, 0) * 0.036f
                : -0.026f + HashUnit(hash, 0) * 0.052f;
            return Math.Clamp(background.TextureParallax + offset, 0.045f, 0.14f);
        }

        private static Vector2 ScaleTileSize(Vector2 tileSize, Vector2 scale)
        {
            return new Vector2(tileSize.X * scale.X, tileSize.Y * scale.Y);
        }

        private static Vector2 TextureLayerOffset(SpaceBackdropDefinition background, Vector2 tileSize, int layer)
        {
            var hash = StableHash($"{TextureVariationKey(background)}|layer:{layer}");
            var x = 0.19f + ((hash & 0xFF) / 255f) * 0.47f;
            var y = 0.23f + (((hash >> 8) & 0xFF) / 255f) * 0.43f;
            if ((hash & 0x10000) != 0)
            {
                x = -x;
            }

            if ((hash & 0x20000) != 0)
            {
                y = -y;
            }

            return new Vector2(tileSize.X * x, tileSize.Y * y);
        }

        private static Vector2 TexturePhaseOffset(SpaceBackdropDefinition background, Vector2 tileSize, int layer)
        {
            var hash = StableHash($"{TextureVariationKey(background)}|phase:{layer}");
            var x = AvoidMirrorPhase(0.17f + ((hash & 0xFF) / 255f) * 0.66f);
            var y = AvoidMirrorPhase(0.13f + (((hash >> 8) & 0xFF) / 255f) * 0.70f);
            return new Vector2(tileSize.X * x, tileSize.Y * y);
        }

        private static string TextureVariationKey(SpaceBackdropDefinition background)
        {
            return $"{background.Archetype}|{background.DisplayName}|{background.TexturePath}|stars:{background.StarfieldSeed}|nebula:{background.NebulaSeed}";
        }

        private static float AvoidMirrorPhase(float phase)
        {
            phase -= MathF.Floor(phase);
            if (Math.Abs(phase - 0.5f) < 0.11f)
            {
                phase = phase < 0.5f ? phase - 0.14f : phase + 0.14f;
            }

            if (phase < 0.12f)
            {
                phase += 0.21f;
            }
            else if (phase > 0.88f)
            {
                phase -= 0.21f;
            }

            return phase;
        }

        private static bool IsGeneratedHighresTexturePath(string texturePath)
        {
            return texturePath.Contains("assets/generated/background_tiles", StringComparison.OrdinalIgnoreCase);
        }

        private static float HashUnit(uint hash, int shift)
        {
            return ((hash >> shift) & 0xFF) / 255f;
        }

        private static uint StableHash(string text)
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            var hash = fnvOffset;
            foreach (var character in text)
            {
                hash ^= character;
                hash *= fnvPrime;
            }

            return hash;
        }

        private static Color TextureModulate(SpaceBackdropDefinition background)
        {
            return Colors.White;
        }

        private readonly record struct TextureLayerVariant(Vector2 Scale, float Alpha);
    }

    private partial class GeneratedNebulaOverlayLayer : Node2D
    {
        public SpaceBackdropDefinition Background { get; set; } = SolarSystem.Sol.Background;
        public IReadOnlyList<BackdropNebulaBlob> Nebulae { get; set; } = Array.Empty<BackdropNebulaBlob>();

        public override void _Draw()
        {
            var visibleWorld = CurrentVisibleWorldRect(this, 600f);
            var cameraPosition = GetViewport().GetCamera2D()?.GlobalPosition ?? Vector2.Zero;
            var parallax = Math.Clamp(Background.StarParallax * 0.45f, 0.12f, 0.28f);
            foreach (var nebula in Nebulae)
            {
                var parallaxPosition = cameraPosition + (nebula.Position - cameraPosition) * parallax;
                if (!IsCircleVisible(visibleWorld, parallaxPosition, nebula.Radius))
                {
                    continue;
                }

                var color = WithAlpha(nebula.Color, nebula.Color.A * GeneratedNebulaOverlayAlphaScale);
                DrawCircle(parallaxPosition, nebula.Radius, color);
                DrawCircle(
                    parallaxPosition + new Vector2(nebula.Radius * 0.18f, -nebula.Radius * 0.11f),
                    nebula.Radius * 0.58f,
                    WithAlpha(color, color.A * 0.54f));
            }
        }
    }

    private partial class ProceduralStarfieldLayer : Node2D
    {
        public SpaceBackdropDefinition Background { get; set; } = SolarSystem.Sol.Background;
        public IReadOnlyList<BackdropStarPoint> Stars { get; set; } = Array.Empty<BackdropStarPoint>();

        public override void _Draw()
        {
            var visibleWorld = CurrentVisibleWorldRect(this, 32f);
            var cameraPosition = GetViewport().GetCamera2D()?.GlobalPosition ?? Vector2.Zero;
            var parallax = Background.StarParallax;
            foreach (var star in Stars)
            {
                var parallaxPosition = cameraPosition + (star.Position - cameraPosition) * parallax;
                if (!IsCircleVisible(visibleWorld, parallaxPosition, star.Radius + 1.5f))
                {
                    continue;
                }

                DrawCircle(parallaxPosition, star.Radius, star.Color);
                if (star.Radius > 1.65f)
                {
                    var glint = WithAlpha(star.Color, star.Color.A * 0.28f);
                    DrawLine(
                        parallaxPosition - new Vector2(star.Radius * 2.6f, 0f),
                        parallaxPosition + new Vector2(star.Radius * 2.6f, 0f),
                        glint,
                        0.65f,
                        true);
                    DrawLine(
                        parallaxPosition - new Vector2(0f, star.Radius * 2.6f),
                        parallaxPosition + new Vector2(0f, star.Radius * 2.6f),
                        glint,
                        0.65f,
                        true);
                }
            }
        }
    }

    private static Rect2 CurrentVisibleWorldRect(Node2D node, float margin)
    {
        var camera = node.GetViewport().GetCamera2D();
        var cameraPosition = camera?.GlobalPosition ?? Vector2.Zero;
        var zoom = camera?.Zoom ?? Vector2.One;
        var viewportSize = node.GetViewportRect().Size;
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
}
