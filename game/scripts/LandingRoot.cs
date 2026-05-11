using Godot;
using System;
using System.Collections.Generic;
using Directory = System.IO.Directory;
using IOPath = System.IO.Path;

namespace SpaceManagersPrototype;

public partial class LandingRoot : Node2D
{
    private const string SpaceScenePath = "res://scenes/Main.tscn";
    private const float TileWidth = 80f;
    private const float TileHeight = 36f;
    private const float HalfTileWidth = TileWidth * 0.5f;
    private const float HalfTileHeight = TileHeight * 0.5f;
    private const int MapWidth = 22;
    private const int MapHeight = 16;
    private const float WalkSpeed = 235f;
    private static readonly Vector2I StartCell = new(5, 11);
    private static readonly Vector2I ReturnCell = new(18, 5);
    private static readonly Vector2I[] HexNeighbors =
    {
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
        new(1, -1),
        new(-1, 1)
    };

    private readonly HashSet<Vector2I> _blocked = new();
    private readonly List<Vector2I> _path = new();
    private readonly List<LandingProp> _props = new();
    private readonly Vector2[] _cellHex = new Vector2[6];
    private readonly Vector2[] _ellipse = new Vector2[32];
    private readonly Vector2[] _smallEllipse = new Vector2[18];
    private readonly Vector2[] _avatarBody = new Vector2[5];
    private readonly Vector2[] _avatarTorso = new Vector2[4];
    private readonly Vector2[] _hoverCellOutline = new Vector2[6];

    private Texture2D _stationWall = null!;
    private Texture2D _wallCorner = null!;
    private Texture2D _machineAmmo = null!;
    private Texture2D _machineConsole = null!;
    private Texture2D _lathe = null!;
    private Texture2D _barrel = null!;
    private Font _font = null!;
    private Camera2D _camera = null!;
    private Vector2I _avatarCell;
    private Vector2I _hoverCell;
    private Vector2 _avatarPosition;
    private Vector2 _avatarFacing = new(1f, -0.18f);
    private double _time;
    private double _captureElapsed;
    private int _captureFrames;
    private string _captureDirectory = string.Empty;
    private bool _returnRequested;

    public override void _Ready()
    {
        ConfigureInputMap();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        TextureFilter = TextureFilterEnum.Nearest;

        _font = ThemeDB.FallbackFont;
        _stationWall = LoadTexture("res://assets/landing/aop_station_wall.png");
        _wallCorner = LoadTexture("res://assets/landing/aop_wall_corner.png");
        _machineAmmo = LoadTexture("res://assets/landing/aop_machine_ammo.png");
        _machineConsole = LoadTexture("res://assets/landing/aop_machine_console.png");
        _lathe = LoadTexture("res://assets/landing/aop_lathe.png");
        _barrel = LoadTexture("res://assets/landing/aop_barrel.png");

        BuildStationMap();
        _avatarCell = StartCell;
        _avatarPosition = CellToWorld(_avatarCell);
        _hoverCell = _avatarCell;

        _camera = new Camera2D
        {
            Enabled = true,
            PositionSmoothingEnabled = true,
            PositionSmoothingSpeed = 7.5f,
            Zoom = new Vector2(0.82f, 0.82f)
        };
        AddChild(_camera);
        CenterCamera();
        ConfigureCapture();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            HandleClick(GetGlobalMousePosition());
        }
    }

    public override void _Process(double delta)
    {
        _time += delta;
        _hoverCell = WorldToCell(GetGlobalMousePosition());

        if (Input.IsActionJustPressed("landing_return_debug"))
        {
            ReturnToSpace();
            return;
        }

        UpdateMovement(delta);
        UpdateCamera(delta);
        UpdateCapture(delta);
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawBackdrop();
        DrawFloor();
        DrawRoute();
        DrawDecorAndAvatar();
        DrawTopHud();
    }

    private static Texture2D LoadTexture(string path)
    {
        return GD.Load<Texture2D>(path);
    }

    private void BuildStationMap()
    {
        _blocked.Clear();
        _props.Clear();

        for (var q = 0; q < MapWidth; q++)
        {
            Block(q, 0);
            Block(q, MapHeight - 1);
        }

        for (var r = 0; r < MapHeight; r++)
        {
            Block(0, r);
            Block(MapWidth - 1, r);
        }

        for (var q = 3; q <= 15; q++)
        {
            if (q is 7 or 8)
            {
                continue;
            }

            Block(q, 4);
        }

        for (var r = 7; r <= 13; r++)
        {
            if (r is 10 or 11)
            {
                continue;
            }

            Block(13, r);
        }

        Block(4, 8);
        Block(5, 8);
        Block(15, 9);
        Block(16, 9);
        Block(17, 9);
        Block(10, 12);
        Block(11, 12);

        AddProp(_stationWall, new Vector2I(5, 3), new Vector2(-128f, -125f), 0.82f, 0.2f);
        AddProp(_wallCorner, new Vector2I(14, 4), new Vector2(-22f, -117f), 1.05f, 0.4f);
        AddProp(_machineConsole, new Vector2I(4, 8), new Vector2(-35f, -58f), 1.0f, 0.4f);
        AddProp(_lathe, new Vector2I(5, 8), new Vector2(-45f, -47f), 1.05f, 0.5f);
        AddProp(_machineAmmo, new Vector2I(16, 9), new Vector2(-54f, -112f), 0.94f, 0.8f);
        AddProp(_barrel, new Vector2I(10, 12), new Vector2(-16f, -42f), 1.25f, 0.4f);
        AddProp(_barrel, new Vector2I(11, 12), new Vector2(5f, -42f), 1.15f, 0.5f);
        AddProp(_machineConsole, ReturnCell, new Vector2(-28f, -60f), 0.82f, 0.5f);
    }

    private void Block(int q, int r)
    {
        _blocked.Add(new Vector2I(q, r));
    }

    private void AddProp(Texture2D texture, Vector2I cell, Vector2 offset, float scale, float sortBias)
    {
        _props.Add(new LandingProp(texture, cell, offset, scale, sortBias));
    }

    private void HandleClick(Vector2 worldPosition)
    {
        var requested = WorldToCell(worldPosition);
        if (!TryFindNearestWalkable(requested, out var target))
        {
            return;
        }

        _returnRequested = requested == ReturnCell || target == ReturnCell;
        var route = FindPath(_avatarCell, target);
        if (route.Count <= 0)
        {
            return;
        }

        _path.Clear();
        _path.AddRange(route);
    }

    private void UpdateMovement(double delta)
    {
        if (_path.Count <= 0)
        {
            return;
        }

        var targetCell = _path[0];
        var target = CellToWorld(targetCell);
        var toTarget = target - _avatarPosition;
        var distance = toTarget.Length();
        if (distance <= 0.001f)
        {
            CompleteStep(targetCell, target);
            return;
        }

        _avatarFacing = toTarget.Normalized();
        var step = WalkSpeed * (float)delta;
        if (step >= distance)
        {
            CompleteStep(targetCell, target);
            return;
        }

        _avatarPosition += toTarget / distance * step;
    }

    private void CompleteStep(Vector2I cell, Vector2 worldPosition)
    {
        _avatarCell = cell;
        _avatarPosition = worldPosition;
        _path.RemoveAt(0);

        if (_returnRequested && _avatarCell == ReturnCell)
        {
            ReturnToSpace();
        }
    }

    private void ReturnToSpace()
    {
        Input.MouseMode = Input.MouseModeEnum.Hidden;
        GetTree().ChangeSceneToFile(SpaceScenePath);
    }

    private void UpdateCamera(double delta)
    {
        var bounds = ComputeMapBounds();
        var focus = bounds.GetCenter() + new Vector2(0f, 8f);
        if (_path.Count > 0)
        {
            focus = focus.Lerp(_avatarPosition + new Vector2(0f, -34f), 0.22f);
        }

        _camera.Position = _camera.Position.Lerp(focus, 1f - MathF.Pow(0.001f, (float)delta));
    }

    private void CenterCamera()
    {
        var bounds = ComputeMapBounds();
        _camera.Position = bounds.GetCenter() + new Vector2(0f, 10f);
    }

    private Rect2 ComputeMapBounds()
    {
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        for (var q = 0; q < MapWidth; q++)
        {
            for (var r = 0; r < MapHeight; r++)
            {
                var p = CellToWorld(new Vector2I(q, r));
                min = new Vector2(Math.Min(min.X, p.X - HalfTileWidth), Math.Min(min.Y, p.Y - HalfTileHeight));
                max = new Vector2(Math.Max(max.X, p.X + HalfTileWidth), Math.Max(max.Y, p.Y + HalfTileHeight));
            }
        }

        return new Rect2(min, max - min);
    }

    private void DrawBackdrop()
    {
        var viewport = GetViewportRect().Size;
        var worldSize = viewport / _camera.Zoom;
        var origin = _camera.Position - worldSize * 0.5f;
        DrawRect(new Rect2(-5000f, -3200f, 10000f, 6400f), new Color(0.010f, 0.014f, 0.019f, 1f), true);
        DrawRect(new Rect2(origin, worldSize), new Color(0.012f, 0.017f, 0.021f, 1f), true);

        for (var i = 0; i < 46; i++)
        {
            var t = Hash(i, 13);
            var x = origin.X + worldSize.X * t;
            var y = origin.Y + worldSize.Y * Hash(i, 47);
            var radius = 0.7f + Hash(i, 83) * 1.4f;
            DrawCircle(new Vector2(x, y), radius, new Color(0.25f, 0.47f, 0.55f, 0.15f + Hash(i, 31) * 0.24f));
        }
    }

    private void DrawFloor()
    {
        for (var q = 0; q < MapWidth; q++)
        {
            for (var r = 0; r < MapHeight; r++)
            {
                var cell = new Vector2I(q, r);
                var center = CellToWorld(cell);
                var blocked = IsBlocked(cell);
                IsoHexPoints(center, 0.96f, _cellHex);
                var h = Hash(q * 101 + r * 37, 19);
                var baseTone = 0.24f + h * 0.11f;
                var fill = blocked
                    ? new Color(0.045f, 0.055f, 0.060f, 0.96f)
                    : new Color(baseTone * 0.82f, baseTone * 0.98f, baseTone, 0.98f);
                DrawColoredPolygon(_cellHex, fill);
                DrawPolylineClosed(_cellHex, blocked
                    ? new Color(0.04f, 0.11f, 0.12f, 0.58f)
                    : new Color(0.20f, 0.39f, 0.42f, 0.32f), blocked ? 0.8f : 1.05f);

                if (!IsBlocked(cell) && (q + r) % 5 == 0)
                {
                    DrawLine(center + new Vector2(-17f, -6f), center + new Vector2(17f, 6f), new Color(0.70f, 0.94f, 0.92f, 0.050f), 0.8f, true);
                    DrawLine(center + new Vector2(-17f, 6f), center + new Vector2(17f, -6f), new Color(0.02f, 0.03f, 0.035f, 0.18f), 0.8f, true);
                }
            }
        }

        if (InBounds(_hoverCell) && !IsBlocked(_hoverCell))
        {
            IsoHexPoints(CellToWorld(_hoverCell), 1.02f, _hoverCellOutline);
            DrawPolylineClosed(_hoverCellOutline, new Color(0.70f, 0.96f, 1f, 0.38f), 1.4f);
        }

        var returnCenter = CellToWorld(ReturnCell);
        IsoHexPoints(returnCenter, 1.08f, _cellHex);
        DrawColoredPolygon(_cellHex, new Color(0.04f, 0.70f, 0.66f, 0.12f));
        DrawPolylineClosed(_cellHex, new Color(0.28f, 1f, 0.82f, 0.52f), 1.8f);
        DrawCircle(returnCenter + new Vector2(0f, -20f), 9f + 1.5f * MathF.Sin((float)_time * 3.6f), new Color(0.25f, 1f, 0.78f, 0.16f));
        DrawCircle(returnCenter + new Vector2(0f, -20f), 3.2f, new Color(0.55f, 1f, 0.86f, 0.82f));
    }

    private void DrawRoute()
    {
        if (_path.Count <= 0)
        {
            return;
        }

        var previous = _avatarPosition;
        for (var i = 0; i < _path.Count; i++)
        {
            var center = CellToWorld(_path[i]);
            var alpha = Math.Clamp(0.62f - i * 0.035f, 0.16f, 0.62f);
            IsoHexPoints(center, 0.74f, _cellHex);
            DrawColoredPolygon(_cellHex, new Color(0.06f, 0.88f, 0.75f, 0.10f * alpha));
            DrawPolylineClosed(_cellHex, new Color(0.34f, 1f, 0.83f, 0.32f * alpha), 1.1f);
            DrawLine(previous + new Vector2(0f, -4f), center + new Vector2(0f, -4f), new Color(0.38f, 1f, 0.86f, 0.30f * alpha), 1.5f, true);
            previous = center;
        }
    }

    private void DrawDecorAndAvatar()
    {
        var avatarDrawn = false;
        _props.Sort((a, b) => a.SortKey.CompareTo(b.SortKey));
        foreach (var prop in _props)
        {
            if (!avatarDrawn && _avatarPosition.Y <= prop.SortKey)
            {
                DrawAvatar();
                avatarDrawn = true;
            }

            DrawProp(prop);
        }

        if (!avatarDrawn)
        {
            DrawAvatar();
        }
    }

    private void DrawProp(LandingProp prop)
    {
        var center = CellToWorld(prop.Cell) + prop.Offset;
        var size = prop.Texture.GetSize() * prop.Scale;
        DrawTextureRect(prop.Texture, new Rect2(center, size), false, new Color(0.84f, 0.92f, 0.92f, 0.98f));
    }

    private void DrawAvatar()
    {
        var moving = _path.Count > 0;
        var phase = (float)_time * (moving ? 12.5f : 2.0f);
        var bob = moving ? MathF.Sin(phase) * 2.2f : MathF.Sin(phase * 0.4f) * 0.7f;
        var foot = _avatarPosition + new Vector2(0f, bob);

        DrawEllipseFilled(foot + new Vector2(0f, 7f), new Vector2(24f, 8f), new Color(0f, 0f, 0f, 0.34f), _smallEllipse);

        var side = new Vector2(_avatarFacing.Y, -_avatarFacing.X);
        if (side.LengthSquared() < 0.01f)
        {
            side = new Vector2(1f, 0f);
        }

        side = side.Normalized();
        var legSwing = moving ? MathF.Sin(phase) : 0f;
        var bodyBase = foot + new Vector2(0f, -17f);
        var head = foot + new Vector2(0f, -47f);

        DrawLine(bodyBase + side * 5f, foot + side * (9f + legSwing * 3f) + new Vector2(0f, 1f), new Color(0.07f, 0.11f, 0.13f, 1f), 5.6f, true);
        DrawLine(bodyBase - side * 5f, foot - side * (9f + legSwing * 3f) + new Vector2(0f, 1f), new Color(0.07f, 0.11f, 0.13f, 1f), 5.6f, true);
        DrawLine(bodyBase + side * 5f, foot + side * (9f + legSwing * 3f), new Color(0.34f, 0.83f, 0.82f, 0.82f), 1.5f, true);
        DrawLine(bodyBase - side * 5f, foot - side * (9f + legSwing * 3f), new Color(0.34f, 0.83f, 0.82f, 0.82f), 1.5f, true);

        _avatarTorso[0] = bodyBase + new Vector2(-12f, -6f);
        _avatarTorso[1] = bodyBase + new Vector2(-7f, -25f);
        _avatarTorso[2] = bodyBase + new Vector2(7f, -25f);
        _avatarTorso[3] = bodyBase + new Vector2(12f, -6f);
        DrawColoredPolygon(_avatarTorso, new Color(0.12f, 0.22f, 0.24f, 1f));
        DrawPolylineClosed(_avatarTorso, new Color(0.42f, 1f, 0.86f, 0.64f), 1.2f);

        _avatarBody[0] = bodyBase + new Vector2(0f, -27f);
        _avatarBody[1] = bodyBase + new Vector2(10f, -19f);
        _avatarBody[2] = bodyBase + new Vector2(8f, -7f);
        _avatarBody[3] = bodyBase + new Vector2(-8f, -7f);
        _avatarBody[4] = bodyBase + new Vector2(-10f, -19f);
        DrawColoredPolygon(_avatarBody, new Color(0.06f, 0.37f, 0.38f, 0.96f));
        DrawLine(bodyBase + new Vector2(-9f, -17f), bodyBase + new Vector2(9f, -17f), new Color(0.49f, 1f, 0.82f, 0.42f), 1.0f, true);

        DrawLine(bodyBase + new Vector2(-9f, -20f), bodyBase + new Vector2(-19f, -11f + legSwing * 1.8f), new Color(0.08f, 0.18f, 0.20f, 1f), 4.2f, true);
        DrawLine(bodyBase + new Vector2(9f, -20f), bodyBase + new Vector2(19f, -11f - legSwing * 1.8f), new Color(0.08f, 0.18f, 0.20f, 1f), 4.2f, true);
        DrawLine(bodyBase + new Vector2(-9f, -20f), bodyBase + new Vector2(-19f, -11f + legSwing * 1.8f), new Color(0.36f, 0.96f, 0.88f, 0.38f), 1.1f, true);
        DrawLine(bodyBase + new Vector2(9f, -20f), bodyBase + new Vector2(19f, -11f - legSwing * 1.8f), new Color(0.36f, 0.96f, 0.88f, 0.38f), 1.1f, true);

        DrawEllipseFilled(head, new Vector2(12f, 14f), new Color(0.10f, 0.18f, 0.21f, 1f), _ellipse);
        DrawEllipseFilled(head + new Vector2(1f, -1f), new Vector2(8f, 7f), new Color(0.36f, 0.86f, 0.82f, 0.72f), _smallEllipse);
        DrawEllipseArc(head, new Vector2(12f, 14f), new Color(0.62f, 1f, 0.94f, 0.68f), 1.5f, _ellipse);

        var glow = 0.45f + 0.22f * MathF.Sin((float)_time * 5.0f);
        DrawCircle(bodyBase + new Vector2(0f, -16f), 3.6f, new Color(0.42f, 1f, 0.72f, glow));
    }

    private void DrawTopHud()
    {
        var viewport = GetViewportRect().Size;
        var worldSize = viewport / _camera.Zoom;
        var origin = _camera.Position - worldSize * 0.5f;
        var panel = new Rect2(origin + new Vector2(18f, 18f), new Vector2(386f, 44f) / _camera.Zoom);
        DrawRect(panel, new Color(0.012f, 0.055f, 0.065f, 0.76f), true);
        DrawRect(panel, new Color(0.10f, 0.75f, 0.80f, 0.34f), false, 1.2f);
        DrawString(_font, panel.Position + new Vector2(15f, 29f) / _camera.Zoom, "STATION TEST DECK", HorizontalAlignment.Left, -1f, 18, new Color(0.64f, 0.94f, 0.92f, 0.92f));
        DrawString(_font, panel.Position + new Vector2(205f, 29f) / _camera.Zoom, "F11", HorizontalAlignment.Left, -1f, 18, new Color(0.88f, 0.74f, 0.36f, 0.76f));
    }

    private List<Vector2I> FindPath(Vector2I start, Vector2I goal)
    {
        var result = new List<Vector2I>();
        if (start == goal)
        {
            return result;
        }

        var open = new PriorityQueue<Vector2I, float>();
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var gScore = new Dictionary<Vector2I, float> { [start] = 0f };
        var closed = new HashSet<Vector2I>();
        open.Enqueue(start, 0f);

        while (open.Count > 0)
        {
            var current = open.Dequeue();
            if (!closed.Add(current))
            {
                continue;
            }

            if (current == goal)
            {
                ReconstructPath(cameFrom, current, result);
                return result;
            }

            foreach (var neighbor in Neighbors(current))
            {
                if (!InBounds(neighbor) || IsBlocked(neighbor))
                {
                    continue;
                }

                var tentative = gScore[current] + 1f + TerrainPenalty(neighbor);
                if (gScore.TryGetValue(neighbor, out var known) && tentative >= known)
                {
                    continue;
                }

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentative;
                open.Enqueue(neighbor, tentative + HexDistance(neighbor, goal));
            }
        }

        return result;
    }

    private static void ReconstructPath(Dictionary<Vector2I, Vector2I> cameFrom, Vector2I current, List<Vector2I> result)
    {
        var reversed = new List<Vector2I>();
        while (cameFrom.TryGetValue(current, out var previous))
        {
            reversed.Add(current);
            current = previous;
        }

        for (var i = reversed.Count - 1; i >= 0; i--)
        {
            result.Add(reversed[i]);
        }
    }

    private static IEnumerable<Vector2I> Neighbors(Vector2I cell)
    {
        foreach (var delta in HexNeighbors)
        {
            yield return cell + delta;
        }
    }

    private bool TryFindNearestWalkable(Vector2I requested, out Vector2I target)
    {
        if (InBounds(requested) && !IsBlocked(requested))
        {
            target = requested;
            return true;
        }

        var best = requested;
        var bestDistance = int.MaxValue;
        for (var radius = 1; radius <= 4; radius++)
        {
            for (var dq = -radius; dq <= radius; dq++)
            {
                for (var dr = -radius; dr <= radius; dr++)
                {
                    var cell = new Vector2I(requested.X + dq, requested.Y + dr);
                    if (!InBounds(cell) || IsBlocked(cell))
                    {
                        continue;
                    }

                    var distance = HexDistance(requested, cell);
                    if (distance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = distance;
                    best = cell;
                }
            }

            if (bestDistance < int.MaxValue)
            {
                target = best;
                return true;
            }
        }

        target = default;
        return false;
    }

    private bool InBounds(Vector2I cell)
    {
        return cell.X >= 0 && cell.X < MapWidth && cell.Y >= 0 && cell.Y < MapHeight;
    }

    private bool IsBlocked(Vector2I cell)
    {
        return _blocked.Contains(cell);
    }

    private static float TerrainPenalty(Vector2I cell)
    {
        return (cell.X * 13 + cell.Y * 7) % 9 == 0 ? 0.10f : 0f;
    }

    private static int HexDistance(Vector2I a, Vector2I b)
    {
        var dq = a.X - b.X;
        var dr = a.Y - b.Y;
        var ds = -dq - dr;
        return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(ds)) / 2;
    }

    private static Vector2 CellToWorld(Vector2I cell)
    {
        return new Vector2((cell.X - cell.Y) * HalfTileWidth, (cell.X + cell.Y) * HalfTileHeight);
    }

    private static Vector2I WorldToCell(Vector2 position)
    {
        var x = position.X / HalfTileWidth;
        var y = position.Y / HalfTileHeight;
        var q = Mathf.RoundToInt((x + y) * 0.5f);
        var r = Mathf.RoundToInt((y - x) * 0.5f);
        return new Vector2I(q, r);
    }

    private static void IsoHexPoints(Vector2 center, float scale, Vector2[] output)
    {
        output[0] = center + new Vector2(0f, -HalfTileHeight * 1.36f * scale);
        output[1] = center + new Vector2(HalfTileWidth * 0.78f * scale, -HalfTileHeight * 0.54f * scale);
        output[2] = center + new Vector2(HalfTileWidth * 0.78f * scale, HalfTileHeight * 0.54f * scale);
        output[3] = center + new Vector2(0f, HalfTileHeight * 1.36f * scale);
        output[4] = center + new Vector2(-HalfTileWidth * 0.78f * scale, HalfTileHeight * 0.54f * scale);
        output[5] = center + new Vector2(-HalfTileWidth * 0.78f * scale, -HalfTileHeight * 0.54f * scale);
    }

    private void DrawPolylineClosed(Vector2[] points, Color color, float width)
    {
        for (var i = 0; i < points.Length; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Length];
            if (a == b)
            {
                continue;
            }

            DrawLine(a, b, color, width, true);
        }
    }

    private void DrawEllipseFilled(Vector2 center, Vector2 extents, Color color, Vector2[] buffer)
    {
        for (var i = 0; i < buffer.Length; i++)
        {
            var angle = i * MathF.Tau / buffer.Length;
            buffer[i] = center + new Vector2(MathF.Cos(angle) * extents.X, MathF.Sin(angle) * extents.Y);
        }

        DrawColoredPolygon(buffer, color);
    }

    private void DrawEllipseArc(Vector2 center, Vector2 extents, Color color, float width, Vector2[] buffer)
    {
        for (var i = 0; i < buffer.Length; i++)
        {
            var angle = i * MathF.Tau / (buffer.Length - 1);
            buffer[i] = center + new Vector2(MathF.Cos(angle) * extents.X, MathF.Sin(angle) * extents.Y);
        }

        DrawPolyline(buffer, color, width, true);
    }

    private static float Hash(int index, int salt)
    {
        var value = (uint)(index * 1103515245 + salt * 12345);
        value ^= value >> 16;
        value *= 2246822519u;
        value ^= value >> 13;
        return (value & 0xFFFFFF) / 16777215f;
    }

    private void ConfigureCapture()
    {
        _captureDirectory = ReadStringUserArg("--landing-capture-dir", string.Empty);
        if (string.IsNullOrWhiteSpace(_captureDirectory))
        {
            return;
        }

        _captureDirectory = IOPath.GetFullPath(_captureDirectory);
        Directory.CreateDirectory(_captureDirectory);
        var demoTarget = FindPath(_avatarCell, new Vector2I(15, 7));
        _path.Clear();
        _path.AddRange(demoTarget);
        GD.Print($"Landing capture enabled: {_captureDirectory}");
    }

    private void UpdateCapture(double delta)
    {
        if (string.IsNullOrWhiteSpace(_captureDirectory))
        {
            return;
        }

        _captureElapsed += delta;
        _captureFrames++;
        if (_captureElapsed < 0.55 && _captureFrames < 18)
        {
            return;
        }

        if (DisplayServer.GetName() == "headless")
        {
            GD.Print("Landing capture skipped: viewport texture is unavailable in headless display mode.");
            GetTree().Quit();
            return;
        }

        var image = GetViewport().GetTexture().GetImage();
        if (image is null || image.IsEmpty())
        {
            GD.Print("Landing capture skipped: viewport image is empty.");
            GetTree().Quit();
            return;
        }

        var path = IOPath.Combine(_captureDirectory, "landing_scene.png");
        var error = image.SavePng(path);
        GD.Print(error == Error.Ok
            ? $"Landing capture saved: {path}"
            : $"Landing capture failed ({error}): {path}");
        GetTree().Quit();
    }

    private static string ReadStringUserArg(string name, string fallback)
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                return arg[(name.Length + 1)..];
            }
        }

        return fallback;
    }

    private static void ConfigureInputMap()
    {
        BindMouse("landing_click", MouseButton.Left);
        BindKey("landing_return_debug", Key.F11);
        BindKey("landing_return_debug", Key.Escape);
    }

    private static void BindKey(string action, Key key)
    {
        EnsureAction(action);
        var desired = new InputEventKey { PhysicalKeycode = key };
        if (!InputMap.ActionHasEvent(action, desired))
        {
            InputMap.ActionAddEvent(action, desired);
        }
    }

    private static void BindMouse(string action, MouseButton button)
    {
        EnsureAction(action);
        var desired = new InputEventMouseButton { ButtonIndex = button };
        if (!InputMap.ActionHasEvent(action, desired))
        {
            InputMap.ActionAddEvent(action, desired);
        }
    }

    private static void EnsureAction(string action)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action);
        }
    }

    private readonly record struct LandingProp(Texture2D Texture, Vector2I Cell, Vector2 Offset, float Scale, float SortBias)
    {
        public float SortKey => CellToWorld(Cell).Y + SortBias * TileHeight;
    }
}
