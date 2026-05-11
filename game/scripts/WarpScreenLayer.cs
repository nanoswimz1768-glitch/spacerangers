using Godot;

namespace SpaceManagersPrototype;

public partial class WarpScreenLayer : Control
{
    private const string ShaderPath = "res://shaders/warp_tunnel_screen.gdshader";
    private static readonly Color DefaultOuter = new(0.16f, 0.68f, 1f, 1f);
    private static readonly Color DefaultCore = new(0.88f, 0.98f, 1f, 1f);

    private ColorRect _rect = null!;
    private ShaderMaterial? _material;
    private bool _active;
    private bool _arriving;
    private float _phase;
    private float _progress;
    private float _fade;
    private float _flashAge = 99f;
    private float _afterglowAge = 99f;
    private Vector2 _focus;
    private Color _outerColor = DefaultOuter;
    private Color _coreColor = DefaultCore;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _rect = new ColorRect
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Color = Colors.White
        };
        _rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_rect);

        var shader = ResourceLoader.Load<Shader>(ShaderPath);
        if (shader is not null)
        {
            _material = new ShaderMaterial { Shader = shader };
            _rect.Material = _material;
        }

        Visible = false;
        SetProcess(true);
        ApplyMaterial();
    }

    public override void _Process(double delta)
    {
        var step = MathF.Min(0.05f, Math.Max(0f, (float)delta));
        _phase += step;

        if (_flashAge < 2f)
        {
            _flashAge += step;
        }

        if (_afterglowAge < 3f)
        {
            _afterglowAge += step;
        }

        if (_active)
        {
            _fade = MoveToward(_fade, 1f, step * 5.0f);
        }
        else if (_afterglowAge < 1.55f)
        {
            var t = Math.Clamp(_afterglowAge / 1.55f, 0f, 1f);
            _fade = MathF.Pow(1f - t, 1.55f) * 0.76f;
        }
        else
        {
            _fade = MoveToward(_fade, 0f, step * 3.8f);
        }

        Visible = _material is not null && (_fade > 0.01f || _flashAge < 0.65f);
        if (Visible)
        {
            ApplyMaterial();
        }
    }

    public void SetWarpState(bool active, Vector2 focus, Color outerColor, Color coreColor, float progress, bool arriving)
    {
        if (!active)
        {
            _active = false;
            return;
        }

        var started = !_active;
        var phaseChanged = _active && _arriving != arriving;
        _active = true;
        _arriving = arriving;
        _focus = focus;
        _progress = Math.Clamp(progress, 0f, 1f);
        _outerColor = Mix(DefaultOuter, Saturated(outerColor), 0.64f);
        _coreColor = Mix(DefaultCore, Saturated(coreColor), 0.42f);

        if (started || phaseChanged)
        {
            TriggerFlash();
        }

        ApplyMaterial();
    }

    public void StartAfterglow(Vector2 focus, Color outerColor, Color coreColor)
    {
        _active = false;
        _arriving = true;
        _focus = focus;
        _progress = 0.68f;
        _afterglowAge = 0f;
        _outerColor = Mix(DefaultOuter, Saturated(outerColor), 0.64f);
        _coreColor = Mix(DefaultCore, Saturated(coreColor), 0.42f);
        TriggerFlash();
        ApplyMaterial();
    }

    private void TriggerFlash()
    {
        _flashAge = 0f;
    }

    private void ApplyMaterial()
    {
        if (_material is null)
        {
            return;
        }

        var viewportSize = GetViewportRect().Size;
        if (viewportSize.X <= 1f || viewportSize.Y <= 1f)
        {
            viewportSize = Size;
        }

        if (viewportSize.X <= 1f || viewportSize.Y <= 1f)
        {
            viewportSize = new Vector2(1280f, 720f);
        }

        var focusUv = new Vector2(
            Math.Clamp(_focus.X / viewportSize.X, 0f, 1f),
            Math.Clamp(_focus.Y / viewportSize.Y, 0f, 1f));
        var flashProgress = Math.Clamp(_flashAge / 0.62f, 0f, 1f);
        var flashStrength = _flashAge < 0.62f ? MathF.Pow(1f - flashProgress, 1.8f) : 0f;

        _material.SetShaderParameter("time_seconds", _phase);
        _material.SetShaderParameter("progress", _progress);
        _material.SetShaderParameter("fade", _fade);
        _material.SetShaderParameter("arriving", _arriving ? 1f : 0f);
        _material.SetShaderParameter("focus_uv", focusUv);
        _material.SetShaderParameter("warp_color", _outerColor);
        _material.SetShaderParameter("hot_color", _coreColor);
        _material.SetShaderParameter("flash_progress", flashProgress);
        _material.SetShaderParameter("flash_strength", flashStrength);
    }

    private static Color Saturated(Color color)
    {
        var max = Math.Max(color.R, Math.Max(color.G, color.B));
        if (max <= 0.001f)
        {
            return DefaultOuter;
        }

        return new Color(
            Math.Clamp(color.R / max, 0.04f, 1f),
            Math.Clamp(color.G / max, 0.04f, 1f),
            Math.Clamp(color.B / max, 0.04f, 1f),
            1f);
    }

    private static Color Mix(Color from, Color to, float amount)
    {
        return from.Lerp(to, Math.Clamp(amount, 0f, 1f));
    }

    private static float MoveToward(float from, float to, float delta)
    {
        if (from < to)
        {
            return Math.Min(from + Math.Max(0f, delta), to);
        }

        return Math.Max(from - Math.Max(0f, delta), to);
    }
}
