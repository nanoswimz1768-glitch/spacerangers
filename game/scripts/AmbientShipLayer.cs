using Godot;

namespace SpaceManagersPrototype;

public partial class AmbientShipLayer : Node2D
{
    private static readonly AmbientShip[] Layout =
    {
        new("2FeiT", new Vector2(-1240f, -560f), -0.15f, 0.34f),
        new("2GaalW", new Vector2(940f, -460f), 0.7f, 0.31f),
        new("2MalocR", new Vector2(-1120f, 720f), 1.15f, 0.28f),
        new("2PelengD", new Vector2(520f, 650f), -0.62f, 0.33f),
        new("2PeopleR", new Vector2(1080f, 250f), -1.2f, 0.3f),
        new("2PelengT", new Vector2(-420f, -980f), 0.34f, 0.26f),
        new("2GaalL", new Vector2(1350f, -900f), 1.72f, 0.24f),
        new("2FeiP", new Vector2(-1520f, 230f), -2.05f, 0.22f),
    };

    public override void _Ready()
    {
        ZIndex = 12;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
    }

    public void Populate(IReadOnlyList<string> paths)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        foreach (var ship in Layout)
        {
            var texture = ShipCatalog.LoadTexture(FindPath(paths, ship.Id));
            if (texture is null)
            {
                continue;
            }

            AddShipSprite(texture, ship);
        }
    }

    private static string FindPath(IReadOnlyList<string> paths, string id)
    {
        return paths.FirstOrDefault(path => path.Contains(id, StringComparison.OrdinalIgnoreCase))
            ?? paths.FirstOrDefault()
            ?? string.Empty;
    }

    private void AddShipSprite(Texture2D texture, AmbientShip ship)
    {
        var rig = new Node2D
        {
            Position = ship.Position,
            Rotation = ship.Rotation,
            Scale = new Vector2(ship.Scale, ship.Scale)
        };

        var glow = new Sprite2D
        {
            Texture = texture,
            Centered = true,
            Modulate = new Color(0.0f, 0.9f, 1f, 0.24f),
            Scale = new Vector2(1.16f, 1.16f),
            ZIndex = -1
        };
        glow.TextureFilter = TextureFilterEnum.LinearWithMipmaps;

        var sprite = new Sprite2D
        {
            Texture = texture,
            Centered = true,
            Modulate = new Color(0.9f, 1f, 0.96f, 0.96f),
            ZIndex = 1
        };
        sprite.TextureFilter = TextureFilterEnum.LinearWithMipmaps;

        rig.AddChild(glow);
        rig.AddChild(sprite);
        AddChild(rig);
    }

    private readonly record struct AmbientShip(string Id, Vector2 Position, float Rotation, float Scale);
}
