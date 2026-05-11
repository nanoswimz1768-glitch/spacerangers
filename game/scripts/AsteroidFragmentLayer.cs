using Godot;
using SpaceRangers.Core;

namespace SpaceRangersPrototype;

public partial class AsteroidFragmentLayer : Node2D
{
    private AsteroidEventState _asteroidEvent;
    private AsteroidDebrisResources? _resources;
    private float _lifetime = 1f;
    private float _age;
    private bool _configured;
    private AsteroidModelFragment[] _modelFragments = [];
    private bool _modelFragmentsBuilt;

    public void Configure(AsteroidEventState asteroidEvent, AsteroidDebrisResources resources, float lifetime)
    {
        _asteroidEvent = asteroidEvent;
        _resources = resources;
        _lifetime = Math.Max(0.001f, lifetime);
        _configured = true;
        _modelFragments = [];
        _modelFragmentsBuilt = false;
    }

    public override void _Ready()
    {
        ZIndex = 2;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
    }

    public override void _ExitTree()
    {
        _resources = null;
        _modelFragments = [];
        _modelFragmentsBuilt = false;
    }

    public void SetAge(float age)
    {
        _age = age;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_configured || _resources is null)
        {
            return;
        }

        var t = Math.Clamp(_age / _lifetime, 0f, 1f);
        var isSunBurn = _asteroidEvent.Type == AsteroidEventType.SunBurn;
        var isShipImpact = _asteroidEvent.Type == AsteroidEventType.ShipImpact;
        var radius = Math.Clamp(_asteroidEvent.Radius, 14f, 170f);
        var burst = SmoothStep(0f, isSunBurn ? 0.58f : 0.74f, t);
        var easeOut = 1f - MathF.Pow(1f - burst, 2.2f);
        var fade = MathF.Pow(1f - t, isSunBurn ? 1.05f : 0.82f);
        var dust = SmoothStep(0.03f, 0.56f, t) * fade;
        var rock = VariantColor(_asteroidEvent.Variant);
        var darkDust = new Color(0.13f, 0.115f, 0.10f, 1f).Lerp(rock, 0.18f);
        var warmDust = isSunBurn
            ? new Color(0.76f, 0.30f, 0.10f, 1f)
            : rock.Lerp(new Color(0.42f, 0.37f, 0.31f, 1f), isShipImpact ? 0.42f : 0.32f);

        DrawAsteroidHoldFrame(radius, t, isSunBurn);
        DrawFractureVeins(radius, t, isSunBurn, isShipImpact, warmDust);
        if (isSunBurn)
        {
            DrawSolarBurnBody(radius, t, fade);
            DrawSolarVaporStream(radius, burst, dust, darkDust, warmDust);
        }
        else
        {
            DrawImpactBody(radius, t, fade, warmDust, isShipImpact);
            DrawModelFragments(radius, t, fade, isShipImpact);
            DrawSmokeMass(radius, easeOut, dust, darkDust, warmDust, isSunBurn);
        }

        DrawRockFragments(radius, easeOut, fade, rock, darkDust, isSunBurn);
        DrawHotGrains(radius, easeOut, fade, isSunBurn);
    }

    private void DrawFractureVeins(float radius, float t, bool isSunBurn, bool isShipImpact, Color warmDust)
    {
        var birth = 1f - SmoothStep(0.0f, isSunBurn ? 0.42f : 0.18f, t);
        var hotPulse = isSunBurn
            ? 0.82f + MathF.Sin(_age * 22f + _asteroidEvent.Seed * 0.019f) * 0.18f
            : 0.92f;
        var alpha = birth * (isSunBurn ? 0.46f : isShipImpact ? 0.34f : 0.26f) * hotPulse;
        if (alpha <= 0.01f)
        {
            return;
        }

        var color = isSunBurn
            ? new Color(1f, 0.32f, 0.035f, alpha)
            : WithAlpha(warmDust.Lerp(new Color(0.74f, 0.58f, 0.39f, 1f), 0.22f), alpha * 0.78f);
        var scale = isSunBurn ? 1.75f : 1.44f;
        DrawTexturedQuad(CrackTexture(_asteroidEvent.Variant), Vector2.Zero, _asteroidEvent.Rotation + _asteroidEvent.Seed * 0.0011f, Vector2.One * radius * scale, color);
        DrawTexturedQuad(CrackTexture(_asteroidEvent.Variant + 1), Vector2.Zero, _asteroidEvent.Rotation + _asteroidEvent.Seed * -0.0017f + 1.15f, Vector2.One * radius * (scale * 0.86f), WithAlpha(color, alpha * 0.54f));
    }

    private void DrawAsteroidHoldFrame(float radius, float t, bool isSunBurn)
    {
        var holdLimit = isSunBurn ? 0.48f : 0.18f;
        var coreFade = 1f - SmoothStep(0.02f, holdLimit, t);
        if (coreFade <= 0.01f)
        {
            return;
        }

        var texture = AsteroidTexture(_asteroidEvent.Variant);
        var heat = Math.Clamp(_asteroidEvent.Heat, isSunBurn ? 0.86f : 0f, 1f);
        var pulse = 0.90f + MathF.Sin(_age * 16.0f + _asteroidEvent.Seed * 0.017f) * 0.10f;
        var collapse = isSunBurn ? SmoothStep(0.06f, holdLimit, t) * 0.30f : SmoothStep(0.01f, holdLimit, t) * 0.18f;
        var size = radius * 2.42f * (1f - collapse);
        var tint = isSunBurn
            ? new Color(1f, 0.62f + heat * 0.10f, 0.28f, coreFade * 0.95f * pulse)
            : new Color(1f, 0.92f, 0.78f, coreFade * 0.72f);
        DrawTexturedQuad(texture, Vector2.Zero, _asteroidEvent.Rotation + _age * (isSunBurn ? 1.45f : 0.85f), Vector2.One * size, tint);
    }

    private void DrawImpactBody(float radius, float t, float fade, Color warmDust, bool isShipImpact)
    {
        var flash = 1f - SmoothStep(0.0f, 0.105f, t);
        var burst = SmoothStep(0f, 0.56f, t);
        var ringFade = MathF.Pow(1f - burst, 1.55f);
        var soot = new Color(0.105f, 0.095f, 0.082f, 1f);
        var smoke = soot.Lerp(warmDust, isShipImpact ? 0.42f : 0.34f);
        var denseSmoke = soot.Lerp(warmDust, 0.22f);
        var ember = isShipImpact
            ? new Color(0.88f, 0.58f, 0.30f, 1f)
            : new Color(0.70f, 0.54f, 0.36f, 1f);

        DrawTexturedQuad(_resources?.SmokePuff, Vector2.Zero, _asteroidEvent.Seed * 0.00041f, Vector2.One * radius * (1.36f + burst * 1.10f), WithAlpha(denseSmoke, fade * (0.32f + flash * 0.10f)));
        DrawTexturedQuad(_resources?.DustRing, Vector2.Zero, _asteroidEvent.Seed * 0.00037f, Vector2.One * radius * (1.75f + burst * 3.15f), WithAlpha(smoke, fade * ringFade * 0.30f));
        DrawTexturedQuad(_resources?.EffectBlocksRing, Vector2.Zero, _asteroidEvent.Seed * -0.00029f, Vector2.One * radius * (0.92f + burst * 2.15f), WithAlpha(smoke, fade * ringFade * 0.045f));
        DrawTexturedQuad(_resources?.ImpactFlash, Vector2.Zero, _asteroidEvent.Seed * 0.00051f, Vector2.One * radius * (1.05f + t * 0.82f), WithAlpha(ember, flash * 0.18f));
        DrawSheetFrame(_resources?.EffectBlocksMuzzleFlash, FrameIndex(4), 2, 2, Vector2.Zero, _asteroidEvent.Seed * 0.0019f + t * 0.34f, Vector2.One * radius * (1.58f + burst * 0.58f), WithAlpha(ember, flash * 0.16f));
        DrawTexturedQuad(_resources?.EffectBlocksMuzzleFlashFps, Vector2.Zero, _asteroidEvent.Seed * 0.0027f - t * 0.45f, Vector2.One * radius * (0.82f + burst * 0.90f), new Color(0.86f, 0.48f, 0.20f, flash * 0.075f));
    }

    private void DrawSolarBurnBody(float radius, float t, float fade)
    {
        var glow = 1f - SmoothStep(0.18f, 0.88f, t);
        var pulse = 0.84f + MathF.Sin(_age * 18f + _asteroidEvent.Seed * 0.013f) * 0.16f;
        DrawTexturedQuad(_resources?.HeatCorona, Vector2.Zero, 0f, Vector2.One * radius * (2.40f + t * 2.15f), new Color(1f, 0.18f, 0.025f, fade * 0.50f * pulse));
        DrawTexturedQuad(_resources?.FireGlow, Vector2.Zero, 0f, Vector2.One * radius * (1.20f + t * 1.55f), new Color(1f, 0.68f, 0.18f, glow * 0.62f));
        DrawTexturedQuad(_resources?.EffectBlocksRing, Vector2.Zero, 0f, Vector2.One * radius * (1.16f + t * 1.35f), new Color(1f, 0.30f, 0.06f, fade * 0.035f));
    }

    private void DrawSolarVaporStream(float radius, float burst, float dust, Color darkDust, Color warmDust)
    {
        var worldPosition = _asteroidEvent.Position.ToGodot();
        var away = worldPosition.LengthSquared() > 1f ? worldPosition.Normalized() : Vector2.Right;
        var tangent = new Vector2(-away.Y, away.X);
        var count = 20;
        for (var i = 0; i < count; i++)
        {
            var h0 = Hash01(_asteroidEvent.Seed * 0.191f + i * 5.67f);
            var h1 = Hash01(_asteroidEvent.Seed * 0.223f + i * 11.13f);
            var h2 = Hash01(_asteroidEvent.Seed * 0.251f + i * 17.91f);
            var phase = (i + h0 * 0.62f) / count;
            var distance = radius * (0.42f + phase * 3.75f + h1 * 0.55f) * burst;
            var spread = radius * (h1 - 0.5f) * (0.45f + phase * 1.55f) * burst;
            var position = away * distance + tangent * spread;
            var plumeLength = radius * (0.72f + h2 * 1.15f) * (0.58f + burst * 0.72f);
            var plumeWidth = radius * (0.42f + h1 * 0.58f) * (1f - phase * 0.18f);
            var hotAlpha = dust * (0.11f + h0 * 0.16f) * (1f - phase * 0.38f);
            var smokeAlpha = dust * (0.10f + h2 * 0.10f);
            DrawTexturedQuad(_resources?.FirePlume, position, away.Angle() + (h1 - 0.5f) * 0.46f, new Vector2(plumeLength, plumeWidth), new Color(1f, 0.30f + h2 * 0.20f, 0.05f, hotAlpha));
            DrawTexturedQuad(_resources?.SmokePuff, position + away * radius * (0.18f + h2 * 0.45f), h0 * MathF.Tau + _age * (h1 - 0.5f) * 0.58f, Vector2.One * radius * (0.62f + h2 * 1.45f), WithAlpha(h2 > 0.62f ? warmDust : darkDust, smokeAlpha));
        }
    }

    private void DrawSmokeMass(float radius, float burst, float dust, Color darkDust, Color warmDust, bool isSunBurn)
    {
        var count = isSunBurn ? 16 : 30;
        for (var i = 0; i < count; i++)
        {
            var h0 = Hash01(_asteroidEvent.Seed * 0.037f + i * 6.31f);
            var h1 = Hash01(_asteroidEvent.Seed * 0.043f + i * 12.47f);
            var h2 = Hash01(_asteroidEvent.Seed * 0.059f + i * 18.79f);
            var angle = h0 * MathF.Tau;
            var direction = Direction(angle);
            var tangent = new Vector2(-direction.Y, direction.X);
            var distance = radius * (0.18f + h1 * (isSunBurn ? 2.15f : 3.05f)) * burst;
            var swirl = tangent * radius * (h2 - 0.5f) * 0.36f * burst;
            var position = direction * distance + swirl;
            var size = radius * (0.86f + h2 * (isSunBurn ? 1.65f : 2.2f)) * (0.66f + _age / _lifetime * 0.88f);
            var color = h2 > 0.74f && !isSunBurn ? darkDust.Lerp(warmDust, 0.34f) : darkDust;
            var alpha = dust * (isSunBurn ? 0.12f : 0.24f) * (0.42f + h1 * 0.62f);
            DrawTexturedQuad(_resources?.SmokePuff, position, angle + _age * (h1 - 0.5f) * 0.95f, Vector2.One * size, WithAlpha(color, alpha));
        }
    }

    private void DrawRockFragments(float radius, float burst, float fade, Color rock, Color darkDust, bool isSunBurn)
    {
        var t = Math.Clamp(_age / _lifetime, 0f, 1f);
        var count = isSunBurn ? 11 : 14;
        var worldPosition = _asteroidEvent.Position.ToGodot();
        var away = isSunBurn && worldPosition.LengthSquared() > 1f ? worldPosition.Normalized() : Vector2.Zero;
        for (var i = 0; i < count; i++)
        {
            var h0 = Hash01(_asteroidEvent.Seed * 0.083f + i * 5.17f);
            var h1 = Hash01(_asteroidEvent.Seed * 0.089f + i * 11.73f);
            var h2 = Hash01(_asteroidEvent.Seed * 0.097f + i * 17.89f);
            var h3 = Hash01(_asteroidEvent.Seed * 0.107f + i * 24.41f);
            var angle = isSunBurn
                ? away.Angle() + (h0 - 0.5f) * 1.55f + (h3 - 0.5f) * 0.35f
                : h0 * MathF.Tau;
            var direction = Direction(angle);
            var tangent = new Vector2(-direction.Y, direction.X);
            var travel = radius * (0.36f + h1 * (isSunBurn ? 1.80f : 2.65f)) * burst;
            var drift = tangent * radius * (h2 - 0.5f) * 0.46f * burst;
            var position = direction * travel + drift;
            var size = Math.Clamp(radius * (isSunBurn ? 0.15f + h2 * 0.42f : 0.08f + h2 * 0.24f) * (1f - t * 0.38f), 6f, isSunBurn ? 54f : 34f);
            var rotation = angle + h3 * MathF.Tau + _age * (h1 - 0.5f) * (isSunBurn ? 3.2f : 5.2f);
            var texture = ShardTexture(_asteroidEvent.Variant + i);
            var color = rock.Lerp(darkDust, h1 * 0.34f);
            if (isSunBurn && h2 > 0.55f)
            {
                color = color.Lerp(new Color(1f, 0.36f, 0.08f, 1f), 0.32f);
            }

            var alpha = fade * (isSunBurn ? 0.42f : 0.56f) * (0.72f + h0 * 0.26f);
            DrawTexturedQuad(texture, position, rotation, new Vector2(size * (1.22f + h3 * 0.55f), size * (0.64f + h1 * 0.44f)), WithAlpha(color, alpha));
        }
    }

    private void DrawModelFragments(float radius, float t, float fade, bool isShipImpact)
    {
        EnsureModelFragments();
        if (_modelFragments.Length == 0)
        {
            return;
        }

        var texture = AsteroidTexture(_asteroidEvent.Variant);
        if (texture is null)
        {
            return;
        }

        var textureWidth = MathF.Max(1f, texture.GetSize().X);
        var modelScale = radius * 2.42f / textureWidth;
        var flash = 1f - SmoothStep(0.02f, isShipImpact ? 0.18f : 0.24f, t);
        var release = SmoothStep(0.02f, 0.66f, t);
        for (var i = 0; i < _modelFragments.Length; i++)
        {
            var fragment = _modelFragments[i];
            var localAge = MathF.Max(0f, _age - fragment.Delay);
            var localLifetime = MathF.Max(0.001f, _lifetime - fragment.Delay);
            var localT = Math.Clamp(localAge / localLifetime, 0f, 1f);
            var localRelease = SmoothStep(0.0f, 0.70f, localT);
            if (localAge <= 0f)
            {
                continue;
            }

            var direction = fragment.Direction.Rotated(_asteroidEvent.Rotation);
            var tangent = new Vector2(-direction.Y, direction.X);
            var sourceOffset = fragment.LocalOffset.Rotated(_asteroidEvent.Rotation);
            var travel = direction * fragment.Speed * localAge * (1f - localT * 0.16f);
            var curl = tangent * radius * fragment.Curl * localRelease * (0.30f + localT * 0.44f);
            var position = sourceOffset * (1f - release * 0.08f) + travel + curl;
            var size = fragment.TextureSize * modelScale * fragment.Scale * (1f - localT * 0.10f);
            var alpha = fade * (1f - SmoothStep(0.68f, 1.0f, localT)) * (0.74f + fragment.Heat * 0.26f);
            if (alpha <= 0.01f)
            {
                continue;
            }

            var warm = isShipImpact
                ? new Color(1f, 0.82f, 0.52f, 1f)
                : new Color(1f, 0.90f, 0.68f, 1f);
            var color = new Color(1f, 1f, 1f, alpha).Lerp(WithAlpha(warm, alpha), flash * fragment.Heat * 0.36f);
            DrawTexturedQuad(fragment.Texture, position, _asteroidEvent.Rotation + fragment.BaseRotation + fragment.Spin * localAge, size, color);

            var emberAlpha = flash * fragment.Heat * alpha * 0.42f;
            if (emberAlpha > 0.02f)
            {
                DrawTexturedQuad(_resources?.Spark, position + direction * radius * 0.05f, 0f, Vector2.One * Math.Clamp(size.Length() * 0.08f, 3f, 14f), new Color(1f, 0.54f, 0.10f, emberAlpha));
            }
        }
    }

    private void EnsureModelFragments()
    {
        if (_modelFragmentsBuilt)
        {
            return;
        }

        _modelFragmentsBuilt = true;
        _modelFragments = BuildModelFragments();
    }

    private AsteroidModelFragment[] BuildModelFragments()
    {
        var texture = AsteroidTexture(_asteroidEvent.Variant);
        if (texture is null)
        {
            return [];
        }

        using var image = texture.GetImage();
        if (image is null || image.GetWidth() <= 0 || image.GetHeight() <= 0)
        {
            return [];
        }

        var textureSize = new Vector2(image.GetWidth(), image.GetHeight());
        var modelScale = _asteroidEvent.Radius * 2.42f / MathF.Max(1f, textureSize.X);
        var random = new Random(_asteroidEvent.Seed ^ (_asteroidEvent.Variant * 7919) ^ 0x51F2A3);
        var targetCount = 24 + (int)Math.Clamp(_asteroidEvent.Radius * 0.08f, 3f, 12f);
        var fragments = new List<AsteroidModelFragment>(targetCount);

        for (var attempt = 0; attempt < targetCount * 4 && fragments.Count < targetCount; attempt++)
        {
            var angle = NextFloat(random) * MathF.Tau;
            var distance = MathF.Pow(NextFloat(random), 0.56f) * MathF.Min(textureSize.X, textureSize.Y) * 0.43f;
            var center = textureSize * 0.5f + Direction(angle) * distance;
            var sourceWidth = Lerp(20f, 58f, MathF.Pow(NextFloat(random), 0.72f));
            var sourceHeight = Lerp(18f, 54f, MathF.Pow(NextFloat(random), 0.82f));
            if (fragments.Count < 6)
            {
                sourceWidth *= 1.35f;
                sourceHeight *= 1.25f;
            }

            var x = (int)Math.Clamp(center.X - sourceWidth * 0.5f, 0f, textureSize.X - 2f);
            var y = (int)Math.Clamp(center.Y - sourceHeight * 0.5f, 0f, textureSize.Y - 2f);
            var w = (int)Math.Clamp(sourceWidth, 6f, textureSize.X - x);
            var h = (int)Math.Clamp(sourceHeight, 6f, textureSize.Y - y);
            if (!TryTrimOpaqueBounds(image, x, y, w, h, out var source))
            {
                continue;
            }

            var sourceCenter = source.Position + source.Size * 0.5f;
            var localOffset = (sourceCenter - textureSize * 0.5f) * modelScale;
            var baseDirection = localOffset.LengthSquared() > 2f
                ? localOffset.Normalized()
                : Direction(angle);
            var scatterDirection = Direction(NextFloat(random) * MathF.Tau);
            var direction = (baseDirection * 0.78f + scatterDirection * 0.22f).Normalized();
            var speed = _asteroidEvent.Radius * Lerp(1.04f, 3.15f, MathF.Pow(NextFloat(random), 0.76f));
            var spin = Lerp(-7.5f, 7.5f, NextFloat(random));
            var delay = Lerp(0.004f, 0.105f, MathF.Pow(NextFloat(random), 1.8f));
            var heat = MathF.Pow(NextFloat(random), 0.55f);
            var scale = Lerp(0.90f, 1.18f, NextFloat(random));
            var curl = Lerp(-0.62f, 0.62f, NextFloat(random));
            var fragmentTexture = CreateFragmentTexture(image, source, random);
            if (fragmentTexture is null)
            {
                continue;
            }

            fragments.Add(new AsteroidModelFragment(
                source,
                fragmentTexture,
                source.Size,
                localOffset,
                direction,
                speed,
                spin,
                delay,
                heat,
                scale,
                curl,
                Lerp(-0.38f, 0.38f, NextFloat(random))));
        }

        return fragments.ToArray();
    }

    private static bool TryTrimOpaqueBounds(Image image, int x, int y, int width, int height, out Rect2 source)
    {
        source = default;
        var imageWidth = image.GetWidth();
        var imageHeight = image.GetHeight();
        var minX = imageWidth;
        var minY = imageHeight;
        var maxX = -1;
        var maxY = -1;
        var endX = Math.Min(imageWidth, x + width);
        var endY = Math.Min(imageHeight, y + height);
        for (var py = Math.Max(0, y); py < endY; py++)
        {
            for (var px = Math.Max(0, x); px < endX; px++)
            {
                if (image.GetPixel(px, py).A <= 0.08f)
                {
                    continue;
                }

                minX = Math.Min(minX, px);
                minY = Math.Min(minY, py);
                maxX = Math.Max(maxX, px);
                maxY = Math.Max(maxY, py);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return false;
        }

        var trimmedWidth = maxX - minX + 1;
        var trimmedHeight = maxY - minY + 1;
        if (trimmedWidth < 5 || trimmedHeight < 5)
        {
            return false;
        }

        source = new Rect2(minX, minY, trimmedWidth, trimmedHeight);
        return true;
    }

    private static Texture2D? CreateFragmentTexture(Image sourceImage, Rect2 source, Random random)
    {
        var vertexCount = 7 + random.Next(5);
        var polygon = new Vector2[vertexCount];
        var startAngle = NextFloat(random) * MathF.Tau;
        var center = new Vector2(0.5f, 0.5f);
        for (var i = 0; i < vertexCount; i++)
        {
            var angle = startAngle + i / (float)vertexCount * MathF.Tau + Lerp(-0.10f, 0.10f, NextFloat(random));
            var direction = Direction(angle);
            var edgeDistance = 0.5f / MathF.Max(MathF.Abs(direction.X), MathF.Abs(direction.Y));
            var amount = edgeDistance * Lerp(0.62f, 1.03f, NextFloat(random));
            var unit = center + direction * amount;
            unit.X = Math.Clamp(unit.X, 0.03f, 0.97f);
            unit.Y = Math.Clamp(unit.Y, 0.03f, 0.97f);
            polygon[i] = unit;
        }

        var sourceX = Math.Max(0, (int)MathF.Floor(source.Position.X));
        var sourceY = Math.Max(0, (int)MathF.Floor(source.Position.Y));
        var width = Math.Max(1, (int)MathF.Ceiling(source.Size.X));
        var height = Math.Max(1, (int)MathF.Ceiling(source.Size.Y));
        using var fragmentImage = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        var visiblePixels = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var unit = new Vector2((x + 0.5f) / width, (y + 0.5f) / height);
                if (!IsInsidePolygon(unit, polygon))
                {
                    continue;
                }

                var color = sourceImage.GetPixel(sourceX + x, sourceY + y);
                if (color.A <= 0.08f)
                {
                    continue;
                }

                var feather = PolygonEdgeFeather(unit, polygon);
                color.A *= feather;
                if (color.A <= 0.04f)
                {
                    continue;
                }

                fragmentImage.SetPixel(x, y, color);
                visiblePixels++;
            }
        }

        if (visiblePixels < 18)
        {
            return null;
        }

        return ImageTexture.CreateFromImage(fragmentImage);
    }

    private static bool IsInsidePolygon(Vector2 point, Vector2[] polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            if ((a.Y > point.Y) == (b.Y > point.Y))
            {
                continue;
            }

            var x = (b.X - a.X) * (point.Y - a.Y) / MathF.Max(0.0001f, b.Y - a.Y) + a.X;
            if (point.X < x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static float PolygonEdgeFeather(Vector2 point, Vector2[] polygon)
    {
        var minDistance = float.MaxValue;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            minDistance = MathF.Min(minDistance, DistanceToSegment(point, polygon[j], polygon[i]));
        }

        return SmoothStep(0.0f, 0.035f, minDistance);
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f)
        {
            return (point - start).Length();
        }

        var t = Math.Clamp((point - start).Dot(segment) / lengthSquared, 0f, 1f);
        return (point - (start + segment * t)).Length();
    }

    private void DrawHotGrains(float radius, float burst, float fade, bool isSunBurn)
    {
        var count = isSunBurn ? 34 : 24;
        for (var i = 0; i < count; i++)
        {
            var h0 = Hash01(_asteroidEvent.Seed * 0.131f + i * 8.43f);
            var h1 = Hash01(_asteroidEvent.Seed * 0.157f + i * 15.19f);
            var h2 = Hash01(_asteroidEvent.Seed * 0.181f + i * 25.73f);
            var angle = h0 * MathF.Tau;
            var direction = Direction(angle);
            var travel = radius * (0.46f + h1 * (isSunBurn ? 3.1f : 2.65f)) * burst;
            var position = direction * travel;
            var size = Math.Clamp(radius * (0.030f + h2 * 0.050f), 2.0f, 8.0f) * (1f - _age / _lifetime * 0.30f);
            var alpha = fade * (isSunBurn ? 0.34f : 0.24f) * (0.45f + h1 * 0.55f);
            var color = h2 > 0.66f
                ? new Color(1f, 0.86f, 0.32f, alpha)
                : new Color(1f, 0.38f + h2 * 0.30f, 0.06f, alpha);
            DrawTexturedQuad(_resources?.Spark, position, 0f, Vector2.One * size, color);
        }
    }

    private void DrawTexturedQuad(Texture2D? texture, Vector2 center, float rotation, Vector2 size, Color color)
    {
        if (texture is null || size.X <= 0f || size.Y <= 0f || color.A <= 0f)
        {
            return;
        }

        DrawSetTransform(center, rotation, Vector2.One);
        DrawTextureRect(texture, new Rect2(-size * 0.5f, size), false, color);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private void DrawTexturedRegion(Texture2D? texture, Rect2 source, Vector2 center, float rotation, Vector2 size, Color color)
    {
        if (texture is null || source.Size.X <= 0f || source.Size.Y <= 0f || size.X <= 0f || size.Y <= 0f || color.A <= 0f)
        {
            return;
        }

        DrawSetTransform(center, rotation, Vector2.One);
        DrawTextureRectRegion(texture, new Rect2(-size * 0.5f, size), source, color);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private void DrawSheetFrame(Texture2D? texture, int frameIndex, int columns, int rows, Vector2 center, float rotation, Vector2 size, Color color)
    {
        if (texture is null || columns <= 0 || rows <= 0 || size.X <= 0f || size.Y <= 0f || color.A <= 0f)
        {
            return;
        }

        var frameCount = columns * rows;
        var wrapped = PositiveModulo(frameIndex, frameCount);
        var frameSize = texture.GetSize() / new Vector2(columns, rows);
        var source = new Rect2(
            new Vector2(wrapped % columns, wrapped / columns) * frameSize,
            frameSize);

        DrawSetTransform(center, rotation, Vector2.One);
        DrawTextureRectRegion(texture, new Rect2(-size * 0.5f, size), source, color);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private int FrameIndex(int frameCount)
    {
        return PositiveModulo(_asteroidEvent.Seed + _asteroidEvent.Variant * 7, frameCount);
    }

    private Texture2D? AsteroidTexture(int variant)
    {
        var textures = _resources?.AsteroidTextures;
        if (textures is null || textures.Count == 0)
        {
            return null;
        }

        var wrapped = PositiveModulo(variant, textures.Count);
        return textures[wrapped];
    }

    private Texture2D? ShardTexture(int index)
    {
        var textures = _resources?.ShardTextures;
        if (textures is null || textures.Count == 0)
        {
            return null;
        }

        var wrapped = PositiveModulo(index, textures.Count);
        return textures[wrapped];
    }

    private Texture2D? CrackTexture(int index)
    {
        var textures = _resources?.EffectBlocksCracks;
        if (textures is null || textures.Count == 0)
        {
            return null;
        }

        var wrapped = PositiveModulo(index, textures.Count);
        return textures[wrapped];
    }

    private static int PositiveModulo(int value, int modulo)
    {
        return ((value % modulo) + modulo) % modulo;
    }

    private static Vector2 Direction(float angle)
    {
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    private static Color VariantColor(int variant)
    {
        return PositiveModulo(variant, 8) switch
        {
            0 => new Color(0.58f, 0.54f, 0.48f, 1f),
            1 => new Color(0.34f, 0.33f, 0.32f, 1f),
            2 => new Color(0.42f, 0.50f, 0.54f, 1f),
            3 => new Color(0.47f, 0.28f, 0.25f, 1f),
            4 => new Color(0.38f, 0.30f, 0.50f, 1f),
            5 => new Color(0.32f, 0.47f, 0.37f, 1f),
            6 => new Color(0.55f, 0.46f, 0.30f, 1f),
            _ => new Color(0.36f, 0.43f, 0.48f, 1f)
        };
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, Math.Clamp(alpha, 0f, 1f));
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Lerp(float from, float to, float amount)
    {
        return from + (to - from) * amount;
    }

    private static float Hash01(float value)
    {
        var hashed = MathF.Sin(value) * 43758.5453f;
        return hashed - MathF.Floor(hashed);
    }

    private static float NextFloat(Random random)
    {
        return (float)random.NextDouble();
    }

    private readonly record struct AsteroidModelFragment(
        Rect2 Source,
        Texture2D Texture,
        Vector2 TextureSize,
        Vector2 LocalOffset,
        Vector2 Direction,
        float Speed,
        float Spin,
        float Delay,
        float Heat,
        float Scale,
        float Curl,
        float BaseRotation);
}
