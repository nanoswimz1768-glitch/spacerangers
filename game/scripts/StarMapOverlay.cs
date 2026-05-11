using Godot;
using System.Globalization;
using System.Linq;

namespace SpaceManagersPrototype;

public partial class StarMapOverlay : Control
{
    private const float OuterMargin = 34f;
    private const float PanelRadius = 8f;
    private const float SidePanelWidth = 324f;
    private const float HeaderHeight = 48f;
    private const float FooterHeight = 54f;
    private const float SectorGap = 0f;
    private const float SystemHitRadius = 14f;
    private const float SectorParsecPadding = 7.5f;
    private const string MapBackdropPath = "res://assets/generated/background_tiles/cold_blue_void_01_tile.png";

    private readonly List<StarMapSystemEntry> _systems = new();
    private readonly List<SystemLayout> _layouts = new();
    private readonly Dictionary<string, SectorLayout> _sectorLayouts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Vector2[] _currentMarker = new Vector2[4];

    private Rect2 _panelRect;
    private Rect2 _mapRect;
    private Rect2 _sideRect;
    private Rect2 _okRect;
    private Rect2 _resetRect;
    private Rect2 _closeRect;
    private Rect2 _selectedCardRect;
    private string _currentSystemId = string.Empty;
    private string _tunedSystemId = string.Empty;
    private StarMapSystemEntry? _selected;
    private int _hoveredIndex = -1;
    private int _systemFontSize = 12;
    private int _sectorFontSize = 30;
    private float _starRadiusScale = 1f;
    private bool _compactSystemLabels;
    private bool _hideRoutineLabels;
    private bool _layoutDirty = true;
    private Vector2 _layoutViewportSize = new(-1f, -1f);
    private bool _rightMouseHeld;
    private StarMapSystemEntry? _planetPopupEntry;
    private Vector2 _planetPopupAnchor;
    private float _pulse;
    private Texture2D? _mapBackdropTexture;

    public event Action? CloseRequested;
    public event Action<StarMapSystemEntry>? ConfirmTargetRequested;
    public event Action? ResetTargetRequested;

    public override void _Ready()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        ZIndex = 100;
        SetAnchorsPreset(LayoutPreset.FullRect);
        _mapBackdropTexture = LoadOptionalTexture(MapBackdropPath);
        SetProcess(true);
        SetProcessInput(true);
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        _pulse = (_pulse + (float)delta) % 1000f;
        QueueRedraw();
    }

    public void SetSystems(IReadOnlyList<StarMapSystemEntry> systems, string currentSystemId, string tunedSystemId)
    {
        _systems.Clear();
        _systems.AddRange(systems.OrderBy(system => system.SectorName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(system => system.DisplayName, StringComparer.OrdinalIgnoreCase));
        _currentSystemId = currentSystemId;
        _tunedSystemId = tunedSystemId;

        _selected = _systems.FirstOrDefault(system => SameSystem(system.Id, _tunedSystemId))
            ?? _systems.FirstOrDefault(system => SameSystem(system.Id, _currentSystemId))
            ?? _systems.FirstOrDefault();
        _layoutDirty = true;
        QueueRedraw();
    }

    public void Open()
    {
        Visible = true;
        if (IsInsideTree())
        {
            GrabFocus();
        }

        QueueRedraw();
    }

    public void Close()
    {
        Visible = false;
        _rightMouseHeld = false;
        _planetPopupEntry = null;
        CloseRequested?.Invoke();
    }

    public bool SelectSystemById(string systemId)
    {
        EnsureLayout();
        var layout = _layouts.FirstOrDefault(candidate => SameSystem(candidate.Entry.Id, systemId));
        if (layout.Entry is null)
        {
            return false;
        }

        SelectSystem(layout.Entry);
        return true;
    }

    public bool ShowPlanetPopupForSystem(string systemId)
    {
        EnsureLayout();
        var layout = _layouts.FirstOrDefault(candidate => SameSystem(candidate.Entry.Id, systemId));
        if (layout.Entry is null)
        {
            return false;
        }

        _planetPopupEntry = layout.Entry;
        _planetPopupAnchor = layout.Position;
        QueueRedraw();
        return true;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (Visible)
        {
            AcceptEvent();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible)
        {
            return;
        }

        if (HandleMapInput(@event))
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private bool HandleMapInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion)
        {
            EnsureLayout();
            var hit = HitTestSystem(motion.Position);
            if (hit != _hoveredIndex)
            {
                _hoveredIndex = hit;
                if (_rightMouseHeld && hit >= 0)
                {
                    _planetPopupEntry = _layouts[hit].Entry;
                    _planetPopupAnchor = motion.Position;
                }

                QueueRedraw();
            }

            return true;
        }

        if (@event is not InputEventMouseButton click)
        {
            return false;
        }

        EnsureLayout();
        if (click.ButtonIndex == MouseButton.Right)
        {
            _rightMouseHeld = click.Pressed;
            if (!click.Pressed)
            {
                _planetPopupEntry = null;
                QueueRedraw();
                return true;
            }

            var hit = HitTestSystem(click.Position);
            _planetPopupEntry = hit >= 0 ? _layouts[hit].Entry : null;
            _planetPopupAnchor = click.Position;
            QueueRedraw();
            return true;
        }

        if (!click.Pressed || click.ButtonIndex != MouseButton.Left)
        {
            return true;
        }

        if (_closeRect.HasPoint(click.Position))
        {
            Close();
            return true;
        }

        if (_resetRect.HasPoint(click.Position))
        {
            _tunedSystemId = string.Empty;
            ResetTargetRequested?.Invoke();
            QueueRedraw();
            return true;
        }

        if (_okRect.HasPoint(click.Position) && _selected is not null)
        {
            if (!SameSystem(_selected.Id, _currentSystemId))
            {
                ConfirmTargetRequested?.Invoke(_selected);
                _tunedSystemId = _selected.Id;
            }

            Close();
            return true;
        }

        var systemHit = HitTestSystem(click.Position);
        if (systemHit >= 0)
        {
            SelectSystem(_layouts[systemHit].Entry);
        }

        return true;
    }

    private void SelectSystem(StarMapSystemEntry entry)
    {
        _selected = entry;
        _layoutDirty = true;
        QueueRedraw();
    }

    public override void _Draw()
    {
        EnsureLayout();
        var viewport = GetViewportRect().Size;
        DrawRect(new Rect2(Vector2.Zero, viewport), new Color(0f, 0.015f, 0.025f, 0.74f), true);
        DrawFrame();
        DrawMapBackground();
        DrawSectors();
        DrawWarpRoute();
        DrawSystems();
        DrawSidePanel();
        DrawPlanetPopup();
    }

    private void EnsureLayout()
    {
        var size = GetViewportRect().Size;
        if (!_layoutDirty
            && Math.Abs(_layoutViewportSize.X - size.X) < 0.5f
            && Math.Abs(_layoutViewportSize.Y - size.Y) < 0.5f)
        {
            return;
        }

        _layoutDirty = false;
        _layoutViewportSize = size;
        _panelRect = new Rect2(
            new Vector2(OuterMargin, OuterMargin),
            new Vector2(Math.Max(720f, size.X - OuterMargin * 2f), Math.Max(460f, size.Y - OuterMargin * 2f)));
        _sideRect = new Rect2(
            new Vector2(_panelRect.End.X - SidePanelWidth - 18f, _panelRect.Position.Y + HeaderHeight + 14f),
            new Vector2(SidePanelWidth, _panelRect.Size.Y - HeaderHeight - FooterHeight - 28f));
        _mapRect = new Rect2(
            _panelRect.Position + new Vector2(18f, HeaderHeight + 14f),
            new Vector2(_sideRect.Position.X - _panelRect.Position.X - 34f, _sideRect.Size.Y));
        _okRect = new Rect2(
            new Vector2(_sideRect.Position.X + 18f, _sideRect.End.Y - 50f),
            new Vector2(_sideRect.Size.X - 36f, 34f));
        _resetRect = new Rect2(
            new Vector2(_sideRect.Position.X + 18f, _sideRect.End.Y - 90f),
            new Vector2(_sideRect.Size.X - 36f, 28f));
        _closeRect = new Rect2(
            new Vector2(_panelRect.End.X - 52f, _panelRect.Position.Y + 12f),
            new Vector2(32f, 32f));
        _selectedCardRect = new Rect2(_sideRect.Position + new Vector2(14f, 118f), new Vector2(_sideRect.Size.X - 28f, 250f));

        _sectorLayouts.Clear();
        _layouts.Clear();
        if (_systems.Count == 0)
        {
            return;
        }

        var groups = _systems
            .GroupBy(system => SectorKey(system), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Average(system => system.ParsecPosition.Y))
            .ThenBy(group => group.Average(system => system.ParsecPosition.X))
            .ThenBy(group => group.First().SectorName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sectorCount = Math.Max(1, groups.Length);
        var aspect = Math.Max(0.5f, _mapRect.Size.X / Math.Max(1f, _mapRect.Size.Y));
        var columns = sectorCount == 1
            ? 1
            : Math.Clamp((int)MathF.Round(MathF.Sqrt(sectorCount * aspect)), 1, sectorCount);
        var rows = Math.Max(1, (int)MathF.Ceiling(sectorCount / (float)columns));
        var cellSize = new Vector2(
            (_mapRect.Size.X - SectorGap * (columns - 1)) / columns,
            (_mapRect.Size.Y - SectorGap * (rows - 1)) / rows);
        var minCell = MathF.Min(cellSize.X, cellSize.Y);
        _systemFontSize = SystemFontSize(sectorCount, minCell);
        _sectorFontSize = SectorFontSize(sectorCount, minCell);
        _starRadiusScale = Math.Clamp(minCell / 230f, 0.48f, 1f);
        _compactSystemLabels = sectorCount > 8 || _systems.Count > 48 || minCell < 180f;
        _hideRoutineLabels = sectorCount > 18 || _systems.Count > 120 || minCell < 118f;

        for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            var group = groups[groupIndex].ToArray();
            var row = groupIndex / columns;
            var column = groupIndex % columns;
            var cell = new Rect2(
                _mapRect.Position + new Vector2(column * (cellSize.X + SectorGap), row * (cellSize.Y + SectorGap)),
                cellSize);
            var sectorRect = cell;
            var sectorName = string.IsNullOrWhiteSpace(group[0].SectorName) ? group[0].SectorId : group[0].SectorName;
            var sectorShape = ShapeForSector(group[0].SectorId, group.Length, minCell);
            var hasLeftNeighbor = column > 0;
            var hasTopNeighbor = row > 0;
            var hasRightNeighbor = groupIndex + 1 < groups.Length && column + 1 < columns;
            var hasBottomNeighbor = groupIndex + columns < groups.Length;
            var sectorLayout = new SectorLayout(
                group[0].SectorId,
                sectorName,
                sectorRect,
                group.Length,
                sectorShape,
                BuildMosaicPolygon(sectorRect, hasLeftNeighbor, hasTopNeighbor, hasRightNeighbor, hasBottomNeighbor));
            _sectorLayouts[SectorKey(group[0])] = sectorLayout;
            LayoutSystemsInSector(group, sectorLayout);
        }
    }

    private void LayoutSystemsInSector(IReadOnlyList<StarMapSystemEntry> sectorSystems, SectorLayout sector)
    {
        var count = sectorSystems.Count;
        var sectorMin = MathF.Min(sector.Rect.Size.X, sector.Rect.Size.Y);
        var innerInset = Math.Clamp(sectorMin * 0.16f, 5f, 34f);
        var inner = sector.Rect.Grow(-innerInset);
        if (inner.Size.X < 28f || inner.Size.Y < 28f)
        {
            inner = sector.Rect.Grow(-Math.Clamp(sectorMin * 0.07f, 2f, 10f));
        }

        var useParsecs = HasUsefulParsecLayout(sectorSystems);
        var parsecBounds = useParsecs
            ? ParsecBounds(sectorSystems)
            : default;
        var parsecCenter = useParsecs
            ? parsecBounds.GetCenter()
            : Vector2.Zero;
        var parsecScale = useParsecs
            ? ParsecScale(parsecBounds, inner)
            : 1f;
        var aspect = Math.Max(0.5f, inner.Size.X / Math.Max(1f, inner.Size.Y));
        var columns = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(count * aspect)));
        var rows = Math.Max(1, (int)MathF.Ceiling(count / (float)columns));
        var cell = new Vector2(inner.Size.X / columns, inner.Size.Y / rows);
        var fontSize = _systemFontSize;
        var titleRect = SectorTitleRect(sector);
        var occupied = new List<Rect2>();

        for (var index = 0; index < count; index++)
        {
            var entry = sectorSystems[index];
            var position = useParsecs
                ? inner.GetCenter() + (entry.ParsecPosition - parsecCenter) * parsecScale
                : GridSystemPosition(entry, index, columns, cell, inner);
            var clampInset = Math.Clamp(sectorMin * 0.10f, 2f, 22f);
            position = ClampToRect(position, sector.Rect.Grow(-clampInset));
            position = MoveInsidePolygon(position, sector.Rect.GetCenter(), sector.Polygon);
            position = MovePointAwayFromRect(position, titleRect.Grow(12f), sector.Rect.Grow(-clampInset), sector.Polygon, entry.Id);
            var starRadius = StarRadius(entry);
            var labelRect = LabelRectFor(entry.DisplayName, position, starRadius, fontSize, sector.Rect, titleRect, occupied);
            occupied.Add(new Rect2(position - Vector2.One * (starRadius + 6f), Vector2.One * (starRadius * 2f + 12f)));
            occupied.Add(labelRect.Grow(3f));
            _layouts.Add(new SystemLayout(entry, sector, position, starRadius, labelRect, !_compactSystemLabels));
        }

        ResolveLabelOverlaps(sector);
    }

    private static Vector2 GridSystemPosition(StarMapSystemEntry entry, int index, int columns, Vector2 cell, Rect2 inner)
    {
        var row = index / columns;
        var column = index % columns;
        var jitter = new Vector2(
            HashSigned(entry.Id, 17) * Math.Min(16f, cell.X * 0.20f),
            HashSigned(entry.Id, 31) * Math.Min(12f, cell.Y * 0.18f));
        return inner.Position + new Vector2((column + 0.5f) * cell.X, (row + 0.5f) * cell.Y) + jitter;
    }

    private static bool HasUsefulParsecLayout(IReadOnlyList<StarMapSystemEntry> systems)
    {
        if (systems.Count <= 1)
        {
            return true;
        }

        var first = systems[0].ParsecPosition;
        return systems.Any(system => system.ParsecPosition.DistanceSquaredTo(first) > 0.01f);
    }

    private static Rect2 ParsecBounds(IReadOnlyList<StarMapSystemEntry> systems)
    {
        var min = systems[0].ParsecPosition;
        var max = min;
        foreach (var system in systems.Skip(1))
        {
            min = new Vector2(Math.Min(min.X, system.ParsecPosition.X), Math.Min(min.Y, system.ParsecPosition.Y));
            max = new Vector2(Math.Max(max.X, system.ParsecPosition.X), Math.Max(max.Y, system.ParsecPosition.Y));
        }

        return new Rect2(min, max - min);
    }

    private static float ParsecScale(Rect2 parsecBounds, Rect2 inner)
    {
        var paddedSpan = new Vector2(
            Math.Max(10f, parsecBounds.Size.X + SectorParsecPadding * 2f),
            Math.Max(10f, parsecBounds.Size.Y + SectorParsecPadding * 2f));
        var scale = Math.Min(inner.Size.X / paddedSpan.X, inner.Size.Y / paddedSpan.Y);
        return Math.Clamp(scale, 3.5f, 18f);
    }

    private void ResolveLabelOverlaps(SectorLayout sector)
    {
        var indexes = Enumerable.Range(0, _layouts.Count)
            .Where(index => ReferenceEquals(_layouts[index].Sector, sector))
            .OrderBy(index => _layouts[index].LabelRect.Position.Y)
            .ToArray();

        for (var pass = 0; pass < 3 && !_hideRoutineLabels; pass++)
        {
            for (var i = 1; i < indexes.Length; i++)
            {
                var previousIndex = indexes[i - 1];
                var currentIndex = indexes[i];
                var previous = _layouts[previousIndex];
                var current = _layouts[currentIndex];
                if (!previous.LabelRect.Intersects(current.LabelRect))
                {
                    continue;
                }

                var shift = previous.LabelRect.End.Y - current.LabelRect.Position.Y + 2f;
                var shifted = new Rect2(
                    current.LabelRect.Position + new Vector2(0f, shift),
                    current.LabelRect.Size);
                if (shifted.End.Y > sector.Rect.End.Y - 8f)
                {
                    shifted.Position = new Vector2(shifted.Position.X, sector.Rect.Position.Y + 8f);
                }

                _layouts[currentIndex] = current with { LabelRect = shifted };
            }
        }

        for (var pass = 0; pass < 4 && !_hideRoutineLabels; pass++)
        {
            for (var i = 0; i < indexes.Length; i++)
            {
                for (var j = i + 1; j < indexes.Length; j++)
                {
                    var leftIndex = indexes[i];
                    var rightIndex = indexes[j];
                    var left = _layouts[leftIndex];
                    var right = _layouts[rightIndex];
                    if (!left.LabelRect.Grow(2f).Intersects(right.LabelRect.Grow(2f)))
                    {
                        continue;
                    }

                    var direction = right.LabelRect.GetCenter() - left.LabelRect.GetCenter();
                    if (direction.LengthSquared() < 0.001f)
                    {
                        direction = new Vector2(HashSigned(right.Entry.Id, pass + 301), HashSigned(right.Entry.Id, pass + 401));
                    }

                    direction = direction.Normalized();
                    var shifted = ClampRectToBounds(
                        new Rect2(right.LabelRect.Position + new Vector2(direction.X * 12f, direction.Y * 7f), right.LabelRect.Size),
                        sector.Rect.Grow(-8f));
                    _layouts[rightIndex] = right with { LabelRect = shifted };
                }
            }
        }

        if (!_compactSystemLabels)
        {
            return;
        }

        var occupied = new List<Rect2>();
        foreach (var index in indexes
            .OrderByDescending(index => IsPriorityLabel(_layouts[index]))
            .ThenBy(index => _layouts[index].LabelRect.Position.Y)
            .ThenBy(index => _layouts[index].LabelRect.Position.X))
        {
            var layout = _layouts[index];
            var priority = IsPriorityLabel(layout);
            var canDrawRoutine = !_hideRoutineLabels
                && !occupied.Any(rect => rect.Intersects(layout.LabelRect.Grow(2f)));
            var visible = priority || canDrawRoutine;
            if (visible)
            {
                occupied.Add(layout.LabelRect.Grow(2f));
            }

            _layouts[index] = layout with { LabelVisible = visible };
        }
    }

    private Rect2 LabelRectFor(string text, Vector2 star, float starRadius, int fontSize, Rect2 bounds, Rect2 avoid, IReadOnlyList<Rect2> occupied)
    {
        var font = GetThemeDefaultFont();
        var width = font?.GetStringSize(text, HorizontalAlignment.Left, -1f, fontSize).X ?? text.Length * fontSize * 0.55f;
        var maxWidth = _compactSystemLabels ? 96f : 150f;
        width = Math.Clamp(width + 2f, 34f, maxWidth);
        var height = fontSize + 7f;
        var size = new Vector2(width, height);
        var centerBias = star.X > bounds.GetCenter().X ? -1f : 1f;
        var candidates = new[]
        {
            new Vector2(centerBias * (starRadius + 13f), -height * 0.5f),
            new Vector2(-centerBias * (width + starRadius + 13f), -height * 0.5f),
            new Vector2(-width * 0.5f, -starRadius - height - 10f),
            new Vector2(-width * 0.5f, starRadius + 10f),
            new Vector2(centerBias * (starRadius + 10f), -starRadius - height - 8f),
            new Vector2(centerBias * (starRadius + 10f), starRadius + 8f),
            new Vector2(-centerBias * (width + starRadius + 10f), -starRadius - height - 8f),
            new Vector2(-centerBias * (width + starRadius + 10f), starRadius + 8f)
        };

        Rect2? bestRect = null;
        var bestPenalty = float.MaxValue;
        foreach (var offset in candidates)
        {
            var rect = ClampRectToBounds(new Rect2(star + offset, size), bounds.Grow(-8f));
            var penalty = OverlapPenalty(rect, avoid, occupied);
            if (penalty <= 0.001f)
            {
                return rect;
            }

            if (penalty < bestPenalty)
            {
                bestPenalty = penalty;
                bestRect = rect;
            }
        }

        return bestRect ?? ClampRectToBounds(new Rect2(star + candidates[0], size), bounds.Grow(-8f));
    }

    private void DrawFrame()
    {
        DrawRect(_panelRect, new Color(0.01f, 0.13f, 0.18f, 0.96f), true);
        DrawRect(_panelRect, new Color(0.06f, 0.72f, 0.95f, 0.88f), false, 3f);
        DrawRect(_panelRect.Grow(-5f), new Color(0.14f, 0.95f, 1f, 0.20f), false, 1.2f);
        DrawLine(
            _panelRect.Position + new Vector2(18f, HeaderHeight),
            _panelRect.Position + new Vector2(_panelRect.Size.X - 18f, HeaderHeight),
            new Color(0.12f, 0.86f, 1f, 0.32f),
            1.2f,
            true);

        var font = GetThemeDefaultFont();
        DrawString(font, PixelSnap(_panelRect.Position + new Vector2(24f, 32f)), "STARMAP", HorizontalAlignment.Left, -1f, 22, new Color(0.66f, 1f, 0.94f, 0.96f));
        DrawString(font, PixelSnap(_panelRect.Position + new Vector2(138f, 32f)), "M", HorizontalAlignment.Left, -1f, 15, new Color(1f, 0.82f, 0.34f, 0.76f));
        DrawCloseButton();
    }

    private void DrawCloseButton()
    {
        var color = new Color(0.20f, 0.95f, 1f, 0.72f);
        DrawRect(_closeRect, new Color(0f, 0.26f, 0.34f, 0.54f), true);
        DrawRect(_closeRect, color, false, 1.2f);
        DrawLine(_closeRect.Position + new Vector2(8f, 8f), _closeRect.End - new Vector2(8f, 8f), color, 2f, true);
        DrawLine(new Vector2(_closeRect.End.X - 8f, _closeRect.Position.Y + 8f), new Vector2(_closeRect.Position.X + 8f, _closeRect.End.Y - 8f), color, 2f, true);
    }

    private void DrawMapBackground()
    {
        DrawRect(_mapRect, new Color(0f, 0.012f, 0.026f, 0.97f), true);
        if (_mapBackdropTexture is not null)
        {
            DrawTextureCover(_mapBackdropTexture, _mapRect, new Color(0.72f, 0.92f, 1f, 0.66f));
            DrawRect(_mapRect, new Color(0f, 0.012f, 0.026f, 0.42f), true);
        }

        DrawRect(new Rect2(_mapRect.Position, new Vector2(_mapRect.Size.X, _mapRect.Size.Y * 0.34f)), new Color(0.02f, 0.075f, 0.115f, 0.10f), true);
        DrawRect(new Rect2(_mapRect.Position + new Vector2(0f, _mapRect.Size.Y * 0.72f), new Vector2(_mapRect.Size.X, _mapRect.Size.Y * 0.28f)), new Color(0.045f, 0.015f, 0.062f, 0.09f), true);

        for (var i = 0; i < 38; i++)
        {
            var x = _mapRect.Position.X + Hash01(i * 37 + 11) * _mapRect.Size.X;
            var y = _mapRect.Position.Y + Hash01(i * 53 + 7) * _mapRect.Size.Y;
            var bright = Hash01(i * 83 + 3);
            var radius = 0.40f + Hash01(i * 97 + 19) * 0.85f;
            var alpha = 0.10f + bright * 0.26f;
            var tint = bright > 0.88f
                ? new Color(1f, 0.86f, 0.56f, alpha)
                : new Color(0.62f, 0.90f, 1f, alpha);
            DrawBackgroundStar(new Vector2(x, y), radius, tint, bright > 0.94f);
        }

        for (var i = 0; i < 8; i++)
        {
            var cluster = new Vector2(
                _mapRect.Position.X + Hash01(i * 131 + 4) * _mapRect.Size.X,
                _mapRect.Position.Y + Hash01(i * 151 + 9) * _mapRect.Size.Y);
            var radius = 1.2f + Hash01(i * 41 + 17) * 1.6f;
            DrawBackgroundStar(cluster, radius, new Color(0.78f, 0.95f, 1f, 0.42f), true);
        }

        for (var i = 1; i < 4; i++)
        {
            var x = _mapRect.Position.X + _mapRect.Size.X * i / 4f;
            DrawLine(new Vector2(x, _mapRect.Position.Y), new Vector2(x, _mapRect.End.Y), new Color(0.06f, 0.58f, 0.82f, 0.014f), 1f, true);
        }

        for (var i = 1; i < 3; i++)
        {
            var y = _mapRect.Position.Y + _mapRect.Size.Y * i / 3f;
            DrawLine(new Vector2(_mapRect.Position.X, y), new Vector2(_mapRect.End.X, y), new Color(0.06f, 0.58f, 0.82f, 0.012f), 1f, true);
        }

        DrawRect(_mapRect, new Color(0.02f, 0.66f, 0.95f, 0.46f), false, 1.2f);
    }

    private void DrawBackgroundStar(Vector2 position, float radius, Color color, bool glint)
    {
        var center = PixelSnap(position);
        DrawCircle(center, radius, color);
        if (!glint)
        {
            return;
        }

        var lineColor = WithAlpha(color, color.A * 0.42f);
        DrawLine(center - new Vector2(radius * 2.8f, 0f), center + new Vector2(radius * 2.8f, 0f), lineColor, 0.75f, true);
        DrawLine(center - new Vector2(0f, radius * 2.8f), center + new Vector2(0f, radius * 2.8f), WithAlpha(lineColor, lineColor.A * 0.78f), 0.75f, true);
    }

    private void DrawTextureCover(Texture2D texture, Rect2 target, Color color)
    {
        var textureSize = new Vector2(Math.Max(1f, texture.GetWidth()), Math.Max(1f, texture.GetHeight()));
        var source = CoverSourceRect(textureSize, target.Size);
        DrawTextureRectRegion(texture, target, source, color);
    }

    private void DrawSectors()
    {
        var font = GetThemeDefaultFont();
        foreach (var sector in _sectorLayouts.Values)
        {
            DrawColoredPolygon(sector.Polygon, SectorFill(sector.Id));
            for (var i = 0; i < sector.Polygon.Length; i++)
            {
                DrawLine(sector.Polygon[i], sector.Polygon[(i + 1) % sector.Polygon.Length], SectorLine(sector.Id), 1.35f, true);
            }

            if (_sectorFontSize < 9 || sector.Rect.Size.X < 64f || sector.Rect.Size.Y < 48f)
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(sector.Name) ? "UNKNOWN" : sector.Name.ToUpperInvariant();
            if (_sectorFontSize <= 13 || sector.Rect.Size.X < 150f || sector.Rect.Size.Y < 110f)
            {
                var compactName = TrimSectorLabel(name, sector.Rect.Size.X);
                DrawString(
                    font,
                    PixelSnap(sector.Rect.Position + new Vector2(8f, 16f)),
                    compactName,
                    HorizontalAlignment.Left,
                    sector.Rect.Size.X - 16f,
                    _sectorFontSize,
                    new Color(0.68f, 0.92f, 1f, 0.64f));
                continue;
            }

            var titleRect = SectorTitleRect(sector);
            DrawMapText(font, titleRect.Position + new Vector2(0f, _sectorFontSize), name, titleRect.Size.X, _sectorFontSize, new Color(0.66f, 0.86f, 0.96f, 0.34f));
        }
    }

    private void DrawWarpRoute()
    {
        var target = _selected is not null && !SameSystem(_selected.Id, _currentSystemId)
            ? _selected
            : _systems.FirstOrDefault(system => SameSystem(system.Id, _tunedSystemId));
        if (target is null || SameSystem(target.Id, _currentSystemId))
        {
            return;
        }

        var from = FindLayout(_currentSystemId);
        var to = FindLayout(target.Id);
        if (from.Entry is null || to.Entry is null)
        {
            return;
        }

        var delta = to.Position - from.Position;
        var length = delta.Length();
        if (length < 1f)
        {
            return;
        }

        var direction = delta / length;
        var normal = new Vector2(-direction.Y, direction.X);
        var start = from.Position + direction * (from.StarRadius + 20f);
        var end = to.Position - direction * (to.StarRadius + 18f);
        var routeLength = start.DistanceTo(end);
        var locked = SameSystem(target.Id, _tunedSystemId);
        var routeColor = locked
            ? new Color(1f, 0.76f, 0.30f, 0.96f)
            : new Color(0.40f, 1f, 0.88f, 0.92f);
        var glowColor = locked
            ? new Color(1f, 0.58f, 0.12f, 0.12f)
            : new Color(0.08f, 0.90f, 1f, 0.12f);
        var control = (start + end) * 0.5f - normal * Math.Clamp(routeLength * 0.15f, 38f, 92f);
        var previous = start;
        for (var i = 1; i <= 42; i++)
        {
            var t = i / 42f;
            var point = QuadraticPoint(start, control, end, t);
            DrawLine(previous, point, glowColor, locked ? 4.0f : 3.4f, true);
            previous = point;
        }

        for (var i = 0; i < 36; i += 2)
        {
            var t0 = i / 36f;
            var t1 = Math.Min(1f, t0 + 0.032f);
            DrawLine(QuadraticPoint(start, control, end, t0), QuadraticPoint(start, control, end, t1), routeColor, locked ? 1.9f : 1.6f, true);
        }

        var routeEndDirection = (end - QuadraticPoint(start, control, end, 0.94f)).Normalized();
        DrawRoutePointer(end, routeEndDirection, routeColor, locked);
        DrawRouteDistanceLabel(start, control, end, RouteDistanceParsecs(from.Entry, to.Entry), routeColor);
    }

    private SystemLayout FindLayout(string systemId)
    {
        return _layouts.FirstOrDefault(layout => SameSystem(layout.Entry.Id, systemId));
    }

    private void DrawRoutePointer(Vector2 tip, Vector2 direction, Color color, bool locked)
    {
        if (direction.LengthSquared() < 0.001f)
        {
            direction = Vector2.Right;
        }

        direction = direction.Normalized();
        var normal = new Vector2(-direction.Y, direction.X);
        var size = locked ? 13f : 11f;
        var width = locked ? 5.5f : 4.6f;
        var back = tip - direction * size;
        DrawLine(tip, back + normal * width, color, locked ? 2.0f : 1.65f, true);
        DrawLine(tip, back - normal * width, color, locked ? 2.0f : 1.65f, true);
        DrawCircle(tip, locked ? 3.4f : 2.6f, WithAlpha(color, locked ? 0.80f : 0.66f));
    }

    private void DrawRouteDistanceLabel(Vector2 start, Vector2 control, Vector2 end, float parsecs, Color color)
    {
        var font = GetThemeDefaultFont();
        var label = FormatParsecs(parsecs);
        var point = QuadraticPoint(start, control, end, 0.52f) + new Vector2(8f, -7f);
        DrawMapText(font, point, label, -1f, Math.Max(11, _systemFontSize), WithAlpha(color, 0.92f));
    }

    private static float RouteDistanceParsecs(StarMapSystemEntry from, StarMapSystemEntry to)
    {
        return MathF.Round(from.ParsecPosition.DistanceTo(to.ParsecPosition) * 10f) / 10f;
    }

    private static string FormatParsecs(float parsecs)
    {
        return $"{parsecs.ToString("0.#", CultureInfo.InvariantCulture)} pc";
    }

    private void DrawSystems()
    {
        var font = GetThemeDefaultFont();
        var fontSize = _systemFontSize;
        for (var i = 0; i < _layouts.Count; i++)
        {
            var layout = _layouts[i];
            var isCurrent = SameSystem(layout.Entry.Id, _currentSystemId);
            var isTarget = SameSystem(layout.Entry.Id, _tunedSystemId);
            var isSelected = _selected is not null && SameSystem(layout.Entry.Id, _selected.Id);
            var isHovered = i == _hoveredIndex;
            var pulse = 0.5f + 0.5f * MathF.Sin(_pulse * 4.3f);
            var color = layout.Entry.StarColor;

            if (isSelected || isHovered)
            {
                DrawCircle(layout.Position, layout.StarRadius + 12f, new Color(0.28f, 0.96f, 1f, isSelected ? 0.14f : 0.08f));
            }

            if (isCurrent)
            {
                DrawCurrentMarker(layout.Position, layout.StarRadius + 12f + pulse * 2f);
            }

            if (isTarget)
            {
                DrawTargetMarker(layout.Position, layout.StarRadius + 9f, true);
            }

            DrawStarGlyph(layout, color, isCurrent || isSelected || isTarget, pulse);

            if (layout.LabelVisible || isCurrent || isTarget || isSelected || isHovered)
            {
                var textColor = isCurrent
                    ? new Color(1f, 0.86f, 0.42f, 0.98f)
                    : isSelected
                        ? new Color(0.72f, 1f, 0.94f, 0.98f)
                        : new Color(0.68f, 0.90f, 1f, 0.88f);
                DrawMapText(font, layout.LabelRect.Position + new Vector2(0f, fontSize), TrimLabel(layout.Entry.DisplayName), layout.LabelRect.Size.X, fontSize, textColor);
            }
        }
    }

    private void DrawStarGlyph(SystemLayout layout, Color color, bool emphasized, float pulse)
    {
        var center = layout.Position;
        var radius = layout.StarRadius;
        var glow = emphasized ? 0.28f + pulse * 0.08f : 0.16f;
        DrawCircle(center, radius * 4.0f, new Color(color.R, color.G, color.B, glow * 0.12f));
        DrawCircle(center, radius * 2.5f, new Color(color.R, color.G, color.B, glow * 0.23f));
        DrawArc(center, radius * 2.05f, _pulse * 0.55f, _pulse * 0.55f + MathF.Tau * 0.66f, 28, new Color(color.R, color.G, color.B, emphasized ? 0.36f : 0.16f), 1.0f, true);

        var rayLength = radius * (emphasized ? 2.9f : 2.15f);
        var diagonal = rayLength * 0.70f;
        var rayColor = new Color(color.R, color.G, color.B, emphasized ? 0.44f : 0.24f);
        DrawLine(center - new Vector2(rayLength, 0f), center + new Vector2(rayLength, 0f), rayColor, 1.1f, true);
        DrawLine(center - new Vector2(0f, rayLength), center + new Vector2(0f, rayLength), WithAlpha(rayColor, rayColor.A * 0.82f), 1.1f, true);
        DrawLine(center - new Vector2(diagonal, diagonal), center + new Vector2(diagonal, diagonal), WithAlpha(rayColor, rayColor.A * 0.46f), 0.85f, true);
        DrawLine(center - new Vector2(diagonal, -diagonal), center + new Vector2(diagonal, -diagonal), WithAlpha(rayColor, rayColor.A * 0.40f), 0.85f, true);

        DrawCircle(center, radius + 1.5f, new Color(0.005f, 0.014f, 0.020f, 0.82f));
        DrawCircle(center, radius, new Color(color.R, color.G, color.B, 0.95f));
        DrawCircle(center, Math.Max(1.5f, radius * 0.50f), new Color(1f, 0.96f, 0.72f, 0.90f));
        DrawCircle(center - new Vector2(radius * 0.22f, radius * 0.24f), Math.Max(1.1f, radius * 0.24f), new Color(1f, 1f, 1f, 0.86f));
    }

    private void DrawCurrentMarker(Vector2 center, float radius)
    {
        var color = new Color(1f, 0.82f, 0.24f, 0.90f);
        DrawArc(center, radius, 0f, MathF.Tau, 36, WithAlpha(color, 0.46f), 1.2f, true);
        _currentMarker[0] = center + new Vector2(0f, -radius - 4f);
        _currentMarker[1] = center + new Vector2(radius + 4f, 0f);
        _currentMarker[2] = center + new Vector2(0f, radius + 4f);
        _currentMarker[3] = center + new Vector2(-radius - 4f, 0f);
        DrawLine(_currentMarker[0], _currentMarker[2], WithAlpha(color, 0.30f), 0.9f, true);
        DrawLine(_currentMarker[1], _currentMarker[3], WithAlpha(color, 0.30f), 0.9f, true);
    }

    private void DrawTargetMarker(Vector2 center, float radius, bool locked)
    {
        var color = locked
            ? new Color(1f, 0.76f, 0.30f, 0.88f)
            : new Color(0.42f, 1f, 0.88f, 0.82f);
        var spin = _pulse * 0.85f;
        DrawCircle(center, radius + 4f, WithAlpha(color, 0.075f));
        for (var i = 0; i < 3; i++)
        {
            var start = spin + i * MathF.Tau / 3f;
            DrawArc(center, radius, start, start + MathF.Tau * 0.15f, 12, color, locked ? 1.45f : 1.15f, true);
        }

        DrawArc(center, radius + 5f, -spin * 0.55f, -spin * 0.55f + MathF.Tau * 0.30f, 18, WithAlpha(color, 0.38f), 0.9f, true);
    }

    private void DrawTextWithShadow(Font? font, Vector2 position, string text, float width, int fontSize, Color color)
    {
        var snapped = PixelSnap(position);
        DrawString(font, snapped, text, HorizontalAlignment.Left, width, fontSize, color);
    }

    private void DrawMapText(Font? font, Vector2 position, string text, float width, int fontSize, Color color)
    {
        var snapped = PixelSnap(position);
        var outline = new Color(0f, 0.012f, 0.020f, Math.Clamp(color.A * 0.92f, 0f, 0.86f));
        var softOutline = WithAlpha(outline, outline.A * 0.68f);
        DrawString(font, snapped + new Vector2(-1f, -1f), text, HorizontalAlignment.Left, width, fontSize, softOutline);
        DrawString(font, snapped + new Vector2(1f, -1f), text, HorizontalAlignment.Left, width, fontSize, softOutline);
        DrawString(font, snapped + new Vector2(-1f, 1f), text, HorizontalAlignment.Left, width, fontSize, softOutline);
        DrawString(font, snapped + new Vector2(1f, 1f), text, HorizontalAlignment.Left, width, fontSize, softOutline);
        DrawString(font, snapped + new Vector2(-1f, 0f), text, HorizontalAlignment.Left, width, fontSize, outline);
        DrawString(font, snapped + new Vector2(1f, 0f), text, HorizontalAlignment.Left, width, fontSize, outline);
        DrawString(font, snapped + new Vector2(0f, -1f), text, HorizontalAlignment.Left, width, fontSize, outline);
        DrawString(font, snapped + new Vector2(0f, 1f), text, HorizontalAlignment.Left, width, fontSize, outline);
        DrawString(font, snapped, text, HorizontalAlignment.Left, width, fontSize, color);
    }

    private void DrawSidePanel()
    {
        var font = GetThemeDefaultFont();
        DrawRect(_sideRect, new Color(0f, 0.08f, 0.12f, 0.82f), true);
        DrawRect(_sideRect, new Color(0.12f, 0.78f, 1f, 0.34f), false, 1.1f);
        DrawString(font, PixelSnap(_sideRect.Position + new Vector2(18f, 33f)), "WARP TARGET", HorizontalAlignment.Left, -1f, 17, new Color(0.56f, 1f, 0.92f, 0.94f));

        var currentName = _systems.FirstOrDefault(system => SameSystem(system.Id, _currentSystemId))?.DisplayName ?? _currentSystemId;
        DrawString(font, PixelSnap(_sideRect.Position + new Vector2(18f, 62f)), "CURRENT", HorizontalAlignment.Left, -1f, 12, new Color(0.62f, 0.78f, 0.86f, 0.68f));
        DrawString(font, PixelSnap(_sideRect.Position + new Vector2(18f, 84f)), currentName, HorizontalAlignment.Left, _sideRect.Size.X - 36f, 18, new Color(1f, 0.86f, 0.42f, 0.96f));

        DrawRect(_selectedCardRect, new Color(0.005f, 0.025f, 0.04f, 0.72f), true);
        DrawRect(_selectedCardRect, new Color(0.12f, 0.78f, 1f, 0.22f), false, 1f);
        if (_selected is null)
        {
            DrawTextWithShadow(font, _selectedCardRect.Position + new Vector2(16f, 34f), "Select a system", -1f, 16, new Color(0.72f, 0.92f, 1f, 0.76f));
        }
        else
        {
            DrawSelectedSystem(font, _selected);
        }

        var hasTunedTarget = !string.IsNullOrWhiteSpace(_tunedSystemId);
        var resetFill = hasTunedTarget
            ? new Color(0.15f, 0.12f, 0.08f, 0.72f)
            : new Color(0.05f, 0.11f, 0.13f, 0.48f);
        var resetLine = hasTunedTarget
            ? new Color(1f, 0.72f, 0.26f, 0.62f)
            : new Color(0.34f, 0.56f, 0.62f, 0.22f);
        DrawRect(_resetRect, resetFill, true);
        DrawRect(_resetRect, resetLine, false, 1.05f);
        var resetText = "RESET DRIVE";
        var resetTextWidth = font?.GetStringSize(resetText, HorizontalAlignment.Left, -1f, 13).X ?? 80f;
        DrawString(
            font,
            PixelSnap(_resetRect.Position + new Vector2((_resetRect.Size.X - resetTextWidth) * 0.5f, 20f)),
            resetText,
            HorizontalAlignment.Left,
            -1f,
            13,
            hasTunedTarget ? new Color(1f, 0.82f, 0.40f, 0.86f) : new Color(0.54f, 0.70f, 0.76f, 0.42f));

        var okColor = _selected is null
            ? new Color(0.16f, 0.30f, 0.34f, 0.54f)
            : new Color(0.02f, 0.48f, 0.58f, 0.86f);
        DrawRect(_okRect, okColor, true);
        DrawRect(_okRect, new Color(0.28f, 1f, 0.88f, _selected is null ? 0.26f : 0.76f), false, 1.3f);
        var okText = _selected is null ? "SELECT SYSTEM" : "OK";
        var textWidth = font?.GetStringSize(okText, HorizontalAlignment.Left, -1f, 18).X ?? 40f;
        DrawString(font, PixelSnap(_okRect.Position + new Vector2((_okRect.Size.X - textWidth) * 0.5f, 24f)), okText, HorizontalAlignment.Left, -1f, 18, new Color(0.80f, 1f, 0.94f, _selected is null ? 0.46f : 0.96f));
    }

    private void DrawSelectedSystem(Font? font, StarMapSystemEntry entry)
    {
        var p = _selectedCardRect.Position + new Vector2(16f, 28f);
        DrawCircle(p + new Vector2(10f, 6f), 8f, new Color(entry.StarColor.R, entry.StarColor.G, entry.StarColor.B, 0.18f));
        DrawCircle(p + new Vector2(10f, 6f), 5f, entry.StarColor);
        DrawTextWithShadow(font, p + new Vector2(26f, 13f), entry.DisplayName, _selectedCardRect.Size.X - 50f, 19, new Color(0.78f, 1f, 0.94f, 0.98f));
        var current = _systems.FirstOrDefault(system => SameSystem(system.Id, _currentSystemId));
        var parsecs = current is null
            ? 0f
            : RouteDistanceParsecs(current, entry);
        DrawInfoLine(font, p + new Vector2(0f, 52f), "SECTOR", SectorName(entry));
        DrawInfoLine(font, p + new Vector2(0f, 80f), "PLANETS", entry.PlanetCount.ToString(CultureInfo.InvariantCulture));
        DrawInfoLine(font, p + new Vector2(0f, 108f), "PARSECS", FormatParsecs(parsecs));
        DrawInfoLine(font, p + new Vector2(0f, 136f), "TARGET", SameSystem(entry.Id, _tunedSystemId) ? "Tuned" : "Not tuned");
        var status = SameSystem(entry.Id, _currentSystemId)
            ? "CURRENT SYSTEM"
            : SameSystem(entry.Id, _tunedSystemId)
                ? "ENGINE TUNED"
                : "READY TO TUNE";
        DrawTextWithShadow(font, p + new Vector2(0f, 196f), status, _selectedCardRect.Size.X - 32f, 15, new Color(1f, 0.82f, 0.34f, 0.90f));
    }

    private void DrawInfoLine(Font? font, Vector2 position, string label, string value)
    {
        DrawString(font, PixelSnap(position), label, HorizontalAlignment.Left, 78f, 12, new Color(0.58f, 0.78f, 0.88f, 0.72f));
        DrawTextWithShadow(font, position + new Vector2(84f, 1f), value, _selectedCardRect.Size.X - 124f, 14, new Color(0.70f, 0.92f, 1f, 0.84f));
    }

    private void DrawPlanetPopup()
    {
        if (_planetPopupEntry is null)
        {
            return;
        }

        var font = GetThemeDefaultFont();
        var planets = _planetPopupEntry.Planets;
        var rows = Math.Min(planets.Count, 14);
        var width = 360f;
        var rowHeight = 24f;
        var height = 82f + rows * rowHeight + (planets.Count > rows ? 22f : 0f);
        var position = _planetPopupAnchor + new Vector2(20f, -12f);
        position.X = Math.Clamp(position.X, _mapRect.Position.X + 12f, _mapRect.End.X - width - 12f);
        position.Y = Math.Clamp(position.Y, _mapRect.Position.Y + 12f, _mapRect.End.Y - height - 12f);
        var rect = new Rect2(position, new Vector2(width, height));

        DrawRect(rect, new Color(0.005f, 0.030f, 0.046f, 0.96f), true);
        DrawRect(rect, new Color(0.24f, 0.96f, 1f, 0.54f), false, 1.2f);
        DrawRect(new Rect2(rect.Position, new Vector2(rect.Size.X, 34f)), new Color(0.02f, 0.15f, 0.20f, 0.70f), true);
        DrawCircle(rect.Position + new Vector2(19f, 21f), 6.0f, _planetPopupEntry.StarColor);
        DrawTextWithShadow(font, rect.Position + new Vector2(34f, 26f), _planetPopupEntry.DisplayName, rect.Size.X - 48f, 17, new Color(0.76f, 1f, 0.94f, 0.98f));
        DrawString(font, PixelSnap(rect.Position + new Vector2(16f, 58f)), "PLANETS", HorizontalAlignment.Left, -1f, 12, new Color(0.58f, 0.78f, 0.88f, 0.72f));

        for (var i = 0; i < rows; i++)
        {
            var planet = planets[i];
            var y = rect.Position.Y + 82f + i * rowHeight;
            DrawCircle(new Vector2(rect.Position.X + 18f, y - 5f), 5.0f, new Color(planet.MapColor.R, planet.MapColor.G, planet.MapColor.B, 0.28f));
            DrawCircle(new Vector2(rect.Position.X + 18f, y - 5f), 3.2f, planet.MapColor);
            if (planet.HasRings)
            {
                DrawArc(new Vector2(rect.Position.X + 18f, y - 5f), 7.2f, -0.2f, MathF.PI + 0.2f, 16, new Color(0.92f, 0.84f, 0.62f, 0.72f), 1f, true);
            }

            DrawTextWithShadow(font, new Vector2(rect.Position.X + 34f, y), planet.DisplayName, 164f, 14, new Color(0.78f, 0.94f, 1f, 0.92f));
            DrawString(font, PixelSnap(new Vector2(rect.Position.X + 212f, y)), planet.Archetype, HorizontalAlignment.Left, rect.Size.X - 228f, 12, new Color(0.56f, 0.76f, 0.84f, 0.66f));
        }

        if (planets.Count > rows)
        {
            DrawString(font, PixelSnap(rect.Position + new Vector2(16f, height - 12f)), $"+{planets.Count - rows} more", HorizontalAlignment.Left, -1f, 11, new Color(0.58f, 0.78f, 0.88f, 0.72f));
        }
    }

    private int HitTestSystem(Vector2 position)
    {
        EnsureLayout();
        for (var i = _layouts.Count - 1; i >= 0; i--)
        {
            var layout = _layouts[i];
            if (layout.Position.DistanceSquaredTo(position) <= SystemHitRadius * SystemHitRadius
                || layout.LabelRect.Grow(4f).HasPoint(position))
            {
                return i;
            }
        }

        return -1;
    }

    private static SectorShape ShapeForSector(string sectorId, int systemCount, float minCell)
    {
        if (systemCount > 6 || minCell < 170f)
        {
            return SectorShape.Octagon;
        }

        if (systemCount > 3 || minCell < 215f)
        {
            return Hash01(sectorId, 41) < 0.45f ? SectorShape.Hexagon : SectorShape.Octagon;
        }

        return (int)(Hash01(sectorId, 43) * 5f) switch
        {
            0 => SectorShape.Circle,
            1 => SectorShape.Hexagon,
            2 => SectorShape.Diamond,
            3 => SectorShape.Triangle,
            _ => SectorShape.Square
        };
    }

    private static Vector2[] BuildSectorPolygon(Rect2 rect, string key, SectorShape shape)
    {
        return shape switch
        {
            SectorShape.Circle => BuildRegularPolygon(rect, 18, Hash01(key, 57) * MathF.Tau),
            SectorShape.Hexagon => BuildRegularPolygon(rect, 6, MathF.PI / 6f + Hash01(key, 61) * 0.34f),
            SectorShape.Diamond => BuildDiamond(rect),
            SectorShape.Triangle => BuildTriangle(rect, key),
            SectorShape.Square => BuildSquare(rect),
            _ => BuildOctagon(rect, key)
        };
    }

    private static Vector2[] BuildOctagon(Rect2 rect, string key)
    {
        var maxInset = MathF.Min(rect.Size.X, rect.Size.Y) * 0.22f;
        var insetA = Math.Min(maxInset, 10f + Hash01(key, 1) * 18f);
        var insetB = Math.Min(maxInset, 10f + Hash01(key, 2) * 18f);
        var insetC = Math.Min(maxInset, 10f + Hash01(key, 3) * 18f);
        var insetD = Math.Min(maxInset, 10f + Hash01(key, 4) * 18f);
        return new[]
        {
            rect.Position + new Vector2(insetA, 0f),
            rect.Position + new Vector2(rect.Size.X - insetB, 0f),
            rect.Position + new Vector2(rect.Size.X, insetB),
            rect.Position + new Vector2(rect.Size.X, rect.Size.Y - insetC),
            rect.Position + new Vector2(rect.Size.X - insetC, rect.Size.Y),
            rect.Position + new Vector2(insetD, rect.Size.Y),
            rect.Position + new Vector2(0f, rect.Size.Y - insetD),
            rect.Position + new Vector2(0f, insetA)
        };
    }

    private static Vector2[] BuildRegularPolygon(Rect2 rect, int sides, float rotation)
    {
        var points = new Vector2[sides];
        var center = rect.GetCenter();
        var radiusX = Math.Max(4f, rect.Size.X * 0.5f - 2f);
        var radiusY = Math.Max(4f, rect.Size.Y * 0.5f - 2f);
        for (var i = 0; i < sides; i++)
        {
            var angle = rotation + MathF.Tau * i / sides;
            points[i] = center + new Vector2(MathF.Cos(angle) * radiusX, MathF.Sin(angle) * radiusY);
        }

        return points;
    }

    private static Vector2[] BuildDiamond(Rect2 rect)
    {
        var center = rect.GetCenter();
        return new[]
        {
            new Vector2(center.X, rect.Position.Y),
            new Vector2(rect.End.X, center.Y),
            new Vector2(center.X, rect.End.Y),
            new Vector2(rect.Position.X, center.Y)
        };
    }

    private static Vector2[] BuildSquare(Rect2 rect)
    {
        return new[]
        {
            rect.Position,
            new Vector2(rect.End.X, rect.Position.Y),
            rect.End,
            new Vector2(rect.Position.X, rect.End.Y)
        };
    }

    private static Vector2[] BuildTriangle(Rect2 rect, string key)
    {
        return ((int)(Hash01(key, 67) * 4f)) switch
        {
            0 => new[]
            {
                rect.Position + new Vector2(rect.Size.X * 0.5f, 0f),
                rect.End,
                new Vector2(rect.Position.X, rect.End.Y)
            },
            1 => new[]
            {
                rect.Position,
                new Vector2(rect.End.X, rect.Position.Y + rect.Size.Y * 0.5f),
                new Vector2(rect.Position.X, rect.End.Y)
            },
            2 => new[]
            {
                rect.Position,
                new Vector2(rect.End.X, rect.Position.Y),
                rect.Position + new Vector2(rect.Size.X * 0.5f, rect.Size.Y)
            },
            _ => new[]
            {
                new Vector2(rect.End.X, rect.Position.Y),
                rect.End,
                new Vector2(rect.Position.X, rect.Position.Y + rect.Size.Y * 0.5f)
            }
        };
    }

    private static Vector2[] BuildMosaicPolygon(Rect2 rect, bool hasLeftNeighbor, bool hasTopNeighbor, bool hasRightNeighbor, bool hasBottomNeighbor)
    {
        var minSide = MathF.Min(rect.Size.X, rect.Size.Y);
        var notch = Math.Clamp(minSide * 0.045f, 8f, 24f);
        var topLeft = rect.Position;
        var topRight = new Vector2(rect.End.X, rect.Position.Y);
        var bottomRight = rect.End;
        var bottomLeft = new Vector2(rect.Position.X, rect.End.Y);
        var points = new List<Vector2>(8) { topLeft };

        if (hasTopNeighbor)
        {
            points.Add(SharedHorizontalBoundaryPoint(rect.Position.X, rect.End.X, rect.Position.Y, notch));
        }

        points.Add(topRight);

        if (hasRightNeighbor)
        {
            points.Add(SharedVerticalBoundaryPoint(rect.End.X, rect.Position.Y, rect.End.Y, notch));
        }

        points.Add(bottomRight);

        if (hasBottomNeighbor)
        {
            points.Add(SharedHorizontalBoundaryPoint(rect.End.X, rect.Position.X, rect.End.Y, notch));
        }

        points.Add(bottomLeft);

        if (hasLeftNeighbor)
        {
            points.Add(SharedVerticalBoundaryPoint(rect.Position.X, rect.End.Y, rect.Position.Y, notch));
        }

        return points.ToArray();
    }

    private static Vector2 SharedHorizontalBoundaryPoint(float startX, float endX, float y, float notch)
    {
        var midX = (startX + endX) * 0.5f;
        var offset = HashSigned($"{MathF.Round(midX)}:{MathF.Round(y)}:h", 113) * notch;
        return new Vector2(midX, y + offset);
    }

    private static Vector2 SharedVerticalBoundaryPoint(float x, float startY, float endY, float notch)
    {
        var midY = (startY + endY) * 0.5f;
        var offset = HashSigned($"{MathF.Round(x)}:{MathF.Round(midY)}:v", 127) * notch;
        return new Vector2(x + offset, midY);
    }

    private int SystemFontSize(int sectorCount, float minCell)
    {
        if (sectorCount > 24 || minCell < 120f || _systems.Count > 140)
        {
            return 10;
        }

        if (sectorCount > 12 || minCell < 155f || _systems.Count > 72)
        {
            return 10;
        }

        if (sectorCount > 6 || minCell < 190f || _systems.Count > 36)
        {
            return 11;
        }

        return _systems.Count switch
        {
            > 96 => 10,
            > 48 => 10,
            > 24 => 11,
            _ => 12
        };
    }

    private static int SectorFontSize(int sectorCount, float minCell)
    {
        if (sectorCount > 24 || minCell < 110f)
        {
            return 9;
        }

        if (sectorCount > 12 || minCell < 150f)
        {
            return 13;
        }

        if (sectorCount > 6 || minCell < 210f)
        {
            return 18;
        }

        return 30;
    }

    private float StarRadius(StarMapSystemEntry entry)
    {
        var sourceBoost = string.Equals(entry.Source, "preset", StringComparison.OrdinalIgnoreCase) ? 1.1f : 0f;
        var baseRadius = 4.1f + MathF.Sqrt(Math.Max(1, entry.PlanetCount)) * 0.42f + sourceBoost;
        return Math.Clamp(baseRadius * _starRadiusScale, 2.6f, 7.4f);
    }

    private bool IsPriorityLabel(SystemLayout layout)
    {
        return SameSystem(layout.Entry.Id, _currentSystemId)
            || SameSystem(layout.Entry.Id, _tunedSystemId)
            || (_selected is not null && SameSystem(layout.Entry.Id, _selected.Id));
    }

    private static string TrimLabel(string label)
    {
        return label.Length <= 18 ? label : label[..17] + ".";
    }

    private static string TrimSectorLabel(string label, float width)
    {
        var maxChars = Math.Clamp((int)(width / 8f), 4, 16);
        return label.Length <= maxChars ? label : label[..Math.Max(3, maxChars - 1)] + ".";
    }

    private static string SectorKey(StarMapSystemEntry entry)
    {
        return string.IsNullOrWhiteSpace(entry.SectorId) ? "unknown" : entry.SectorId;
    }

    private static string SectorName(StarMapSystemEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.SectorName))
        {
            return entry.SectorName;
        }

        return string.IsNullOrWhiteSpace(entry.SectorId) ? "UNKNOWN" : entry.SectorId;
    }

    private static bool SameSystem(string left, string right)
    {
        return !string.IsNullOrWhiteSpace(left)
            && !string.IsNullOrWhiteSpace(right)
            && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static Vector2 ClampToRect(Vector2 point, Rect2 rect)
    {
        return new Vector2(
            Math.Clamp(point.X, rect.Position.X, rect.End.X),
            Math.Clamp(point.Y, rect.Position.Y, rect.End.Y));
    }

    private static Vector2 PixelSnap(Vector2 point)
    {
        return new Vector2(MathF.Round(point.X), MathF.Round(point.Y));
    }

    private Rect2 SectorTitleRect(SectorLayout sector)
    {
        var font = GetThemeDefaultFont();
        var name = string.IsNullOrWhiteSpace(sector.Name) ? "UNKNOWN" : sector.Name.ToUpperInvariant();
        var width = font?.GetStringSize(name, HorizontalAlignment.Left, -1f, _sectorFontSize).X
            ?? name.Length * _sectorFontSize * 0.58f;
        width = Math.Clamp(width + 18f, 72f, Math.Max(72f, sector.Rect.Size.X - 34f));
        var height = _sectorFontSize + 14f;
        var center = sector.Rect.GetCenter();
        return new Rect2(
            PixelSnap(center - new Vector2(width * 0.5f, height * 0.5f)),
            new Vector2(width, height));
    }

    private static Vector2 MovePointAwayFromRect(Vector2 point, Rect2 avoid, Rect2 bounds, Vector2[] polygon, string key)
    {
        if (!avoid.HasPoint(point))
        {
            return point;
        }

        var direction = point - avoid.GetCenter();
        if (direction.LengthSquared() < 0.001f)
        {
            var angle = Hash01(key, 211) * MathF.Tau;
            direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }
        else
        {
            direction = direction.Normalized();
        }

        for (var step = 1; step <= 8; step++)
        {
            var distance = 18f + step * 12f;
            var candidate = ClampToRect(point + direction * distance, bounds);
            candidate = MoveInsidePolygon(candidate, bounds.GetCenter(), polygon);
            if (!avoid.Grow(4f).HasPoint(candidate))
            {
                return candidate;
            }
        }

        return point;
    }

    private static Rect2 ClampRectToBounds(Rect2 rect, Rect2 bounds)
    {
        if (rect.Size.X >= bounds.Size.X || rect.Size.Y >= bounds.Size.Y)
        {
            return new Rect2(bounds.Position, new Vector2(Math.Min(rect.Size.X, bounds.Size.X), Math.Min(rect.Size.Y, bounds.Size.Y)));
        }

        return new Rect2(
            new Vector2(
                Math.Clamp(rect.Position.X, bounds.Position.X, bounds.End.X - rect.Size.X),
                Math.Clamp(rect.Position.Y, bounds.Position.Y, bounds.End.Y - rect.Size.Y)),
            rect.Size);
    }

    private static float OverlapPenalty(Rect2 rect, Rect2 avoid, IReadOnlyList<Rect2> occupied)
    {
        var penalty = RectIntersectionArea(rect.Grow(3f), avoid) * 3.0f;
        foreach (var occupiedRect in occupied)
        {
            penalty += RectIntersectionArea(rect.Grow(2f), occupiedRect);
        }

        return penalty;
    }

    private static float RectIntersectionArea(Rect2 a, Rect2 b)
    {
        var left = Math.Max(a.Position.X, b.Position.X);
        var top = Math.Max(a.Position.Y, b.Position.Y);
        var right = Math.Min(a.End.X, b.End.X);
        var bottom = Math.Min(a.End.Y, b.End.Y);
        return Math.Max(0f, right - left) * Math.Max(0f, bottom - top);
    }

    private static Rect2 CoverSourceRect(Vector2 textureSize, Vector2 targetSize)
    {
        var textureAspect = textureSize.X / Math.Max(1f, textureSize.Y);
        var targetAspect = targetSize.X / Math.Max(1f, targetSize.Y);
        if (textureAspect > targetAspect)
        {
            var width = textureSize.Y * targetAspect;
            return new Rect2(new Vector2((textureSize.X - width) * 0.5f, 0f), new Vector2(width, textureSize.Y));
        }

        var height = textureSize.X / targetAspect;
        return new Rect2(new Vector2(0f, (textureSize.Y - height) * 0.5f), new Vector2(textureSize.X, height));
    }

    private static Vector2 QuadraticPoint(Vector2 start, Vector2 control, Vector2 end, float t)
    {
        var inverse = 1f - t;
        return start * (inverse * inverse) + control * (2f * inverse * t) + end * (t * t);
    }

    private static Vector2 MoveInsidePolygon(Vector2 point, Vector2 center, Vector2[] polygon)
    {
        if (IsPointInsidePolygon(point, polygon))
        {
            return point;
        }

        for (var step = 1; step <= 10; step++)
        {
            var candidate = point.Lerp(center, step / 10f);
            if (IsPointInsidePolygon(candidate, polygon))
            {
                return candidate;
            }
        }

        return center;
    }

    private static bool IsPointInsidePolygon(Vector2 point, Vector2[] polygon)
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

            var intersectionX = (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X;
            if (point.X < intersectionX)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static Color SectorFill(string key)
    {
        var h = Hash01(key, 19);
        return new Color(0.015f + h * 0.028f, 0.060f + h * 0.026f, 0.12f + h * 0.036f, 0.17f);
    }

    private static Color SectorLine(string key)
    {
        var h = Hash01(key, 29);
        return new Color(0.08f + h * 0.12f, 0.62f + h * 0.22f, 1f, 0.38f);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, Math.Clamp(alpha, 0f, 1f));
    }

    private static Texture2D? LoadOptionalTexture(string resourcePath)
    {
        if (!ResourceLoader.Exists(resourcePath))
        {
            return null;
        }

        try
        {
            return ResourceLoader.Load<Texture2D>(resourcePath);
        }
        catch
        {
            return null;
        }
    }

    private static float Hash01(int value)
    {
        unchecked
        {
            var x = (uint)value;
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            x ^= x >> 16;
            return (x & 0x00ffffff) / 16777215f;
        }
    }

    private static float Hash01(string value, int salt)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in value)
            {
                hash ^= character;
                hash *= 16777619u;
            }

            return Hash01((int)(hash + (uint)salt * 374761393u));
        }
    }

    private static float HashSigned(string value, int salt)
    {
        return Hash01(value, salt) * 2f - 1f;
    }

    private enum SectorShape
    {
        Octagon,
        Hexagon,
        Diamond,
        Triangle,
        Square,
        Circle
    }

    private sealed record SectorLayout(string Id, string Name, Rect2 Rect, int SystemCount, SectorShape Shape, Vector2[] Polygon);

    private readonly record struct SystemLayout(
        StarMapSystemEntry Entry,
        SectorLayout Sector,
        Vector2 Position,
        float StarRadius,
        Rect2 LabelRect,
        bool LabelVisible);
}
