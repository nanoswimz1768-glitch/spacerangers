using Godot;

namespace SpaceRangersPrototype;

public partial class AnimatedEarthView : Node2D
{
    private ShaderMaterial? _material;

    public float Diameter { get; set; } = 300f;
    public float TimeSeconds { get; set; }
    public float Daylight { get; set; } = 1f;

    public Texture2D? SurfaceTexture { get; set; }
    public Texture2D? CloudTexture { get; set; }

    public bool IsAvailable => _material is not null && SurfaceTexture is not null && CloudTexture is not null;

    public override void _Ready()
    {
        ZIndex = 1;
        TextureFilter = TextureFilterEnum.Linear;

        var shader = ResourceLoader.Load<Shader>("res://shaders/animated_earth.gdshader");
        if (shader is not null)
        {
            _material = new ShaderMaterial { Shader = shader };
            Material = _material;
        }
    }

    public override void _Draw()
    {
        var material = _material;
        var surface = SurfaceTexture;
        var clouds = CloudTexture;
        if (material is null || surface is null || clouds is null)
        {
            return;
        }

        material.SetShaderParameter("surface_map", surface);
        material.SetShaderParameter("clouds_map", clouds);
        material.SetShaderParameter("time_seconds", TimeSeconds);
        material.SetShaderParameter("daylight", Daylight);
        material.SetShaderParameter("atmosphere_color", new Color(0.20f, 0.62f, 1f, 1f));

        var size = new Vector2(Diameter, Diameter);
        DrawRect(new Rect2(-size * 0.5f, size), Colors.White, true);
    }
}
