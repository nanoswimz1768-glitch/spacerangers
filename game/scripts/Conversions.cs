using CoreVector2 = System.Numerics.Vector2;
using GodotVector2 = Godot.Vector2;

namespace SpaceRangersPrototype;

public static class Conversions
{
    public static GodotVector2 ToGodot(this CoreVector2 value) => new(value.X, value.Y);

    public static CoreVector2 ToCore(this GodotVector2 value) => new(value.X, value.Y);
}

