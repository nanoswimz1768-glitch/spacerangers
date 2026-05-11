using Godot;
using Directory = System.IO.Directory;
using IOPath = System.IO.Path;

namespace SpaceManagersPrototype;

public partial class WarpTunnelPreview : Control
{
    private WarpScreenLayer _warp = null!;
    private Sprite2D _ship = null!;
    private float _elapsed;
    private string _captureDirectory = string.Empty;
    private int _captureIndex;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var background = new ColorRect
        {
            Color = new Color(0.002f, 0.004f, 0.018f, 1f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        _warp = new WarpScreenLayer();
        AddChild(_warp);

        _ship = new Sprite2D
        {
            Texture = ResourceLoader.Load<Texture2D>("res://assets/ships/2PeopleR.png"),
            Centered = true,
            ZIndex = 10,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps
        };
        AddChild(_ship);

        ConfigureCapture();
    }

    public override void _Process(double delta)
    {
        _elapsed += MathF.Min(0.05f, Math.Max(0f, (float)delta));
        var size = GetViewportRect().Size;
        if (size.X <= 1f || size.Y <= 1f)
        {
            size = new Vector2(1280f, 720f);
        }

        var cycle = Fract(_elapsed / 3.0f);
        var outbound = cycle < 0.5f;
        var phase = outbound ? cycle * 2f : (cycle - 0.5f) * 2f;
        var focus = new Vector2(size.X * 0.5f, size.Y * 0.40f);
        var outer = new Color(0.18f, 0.66f, 1f, 1f);
        var core = new Color(0.92f, 0.98f, 1f, 1f);

        _warp.SetWarpState(true, focus, outer, core, phase, arriving: !outbound);
        UpdateShipPreview(size, focus, outbound, phase);
        UpdateCapture(delta);
    }

    private void UpdateShipPreview(Vector2 size, Vector2 focus, bool outbound, float phase)
    {
        var start = new Vector2(size.X * 0.5f, size.Y * 0.76f);
        var end = focus + new Vector2(0f, 26f);
        var t = SmoothStep(0.05f, 0.92f, phase);
        var position = outbound
            ? start.Lerp(end, t)
            : end.Lerp(start, t);
        var scale = outbound
            ? Lerp(0.30f, 0.11f, SmoothStep(0.45f, 1f, phase))
            : Lerp(0.11f, 0.30f, SmoothStep(0f, 0.70f, phase));
        var alpha = outbound
            ? 1f - SmoothStep(0.76f, 1f, phase) * 0.88f
            : SmoothStep(0.04f, 0.28f, phase);

        _ship.Position = position;
        _ship.Scale = new Vector2(scale, scale);
        _ship.Modulate = new Color(1f, 1f, 1f, alpha);
        _ship.Rotation = outbound ? 0f : MathF.PI;
    }

    private void ConfigureCapture()
    {
        var directory = ReadStringUserArg("--capture-frame-dir", string.Empty);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        _captureDirectory = IOPath.GetFullPath(directory);
        Directory.CreateDirectory(_captureDirectory);
        _captureIndex = 0;
        GD.Print($"Warp preview capture enabled: {_captureDirectory}");
    }

    private void UpdateCapture(double delta)
    {
        if (string.IsNullOrWhiteSpace(_captureDirectory))
        {
            return;
        }

        var captureTimes = new[] { 0.55, 1.25, 2.05 };
        while (_captureIndex < captureTimes.Length && _elapsed >= captureTimes[_captureIndex])
        {
            var image = GetViewport().GetTexture().GetImage();
            var path = IOPath.Combine(_captureDirectory, $"warp_shader_preview_{_captureIndex:00}.png");
            var error = image.SavePng(path);
            GD.Print(error == Error.Ok
                ? $"Warp preview capture saved: {path}"
                : $"Warp preview capture failed ({error}): {path}");
            _captureIndex++;
        }

        if (_captureIndex >= captureTimes.Length && ReadBoolUserArg("--capture-frame-quit"))
        {
            GetTree().Quit();
        }
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
        foreach (var arg in args)
        {
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

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        if (Math.Abs(edge1 - edge0) <= 0.0001f)
        {
            return value < edge0 ? 0f : 1f;
        }

        var x = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return x * x * (3f - 2f * x);
    }

    private static float Lerp(float from, float to, float amount)
    {
        return from + (to - from) * amount;
    }

    private static float Fract(float value)
    {
        return value - MathF.Floor(value);
    }
}
