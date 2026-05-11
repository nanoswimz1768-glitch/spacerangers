using Godot;
using System.Linq;

namespace SpaceManagersPrototype;

public partial class StarMapOverlay : Control
{
    private const float OuterMargin = 34f;
    private const float PanelRadius = 8f;
    private const float SidePanelWidth = 292f;
    private const float HeaderHeight = 48f;
    private const float FooterHeight = 54f;
    private const float SectorGap = 18f;
    private const float SystemHitRadius = 14f;

    private readonly List<StarMapSystemEntry> _systems = new();
    private readonly List<SystemLayout> _layouts = new();
    private readonly Dictionary<string, SectorLayout> _sectorLayouts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Vector2[] _currentMarker = new Vector2[4];
    private readonly Vector2[] _targetMarker = new Vector2[4];
    private readonly Vector2[] _starSpark = new Vector2[4];

    private Rect2 _panelRect;
    private Rect2 _mapRect;
    private Rect2 _sideRect;
    private Rect2 _okRect;
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

    public event Action? CloseRequested;
    public event Action<StarMapSystemEntry>? ConfirmTargetRequested;

    public override void _Ready()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        ZIndex = 100;
        SetAnchorsPreset(LayoutPreset.FullRect);
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

        if (_okRect.HasPoint(click.Position) && _selected is not null)
        {
            ConfirmTargetRequested?.Invoke(_selected);
            _tunedSystemId = _selected.Id;
            _layoutDirty = true;
            QueueRedraw();
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
            .OrderBy(group => group.First().SectorName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sectorCount = Math.Max(1, groups.Length);
        var aspect = Math.Max(0.5f, _mapRect.Size.X / Math.Max(1f, _mapRect.Size.Y));
        var columns = sectorCount == 1
            ? 1
            : Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(sectorCount * aspect)));
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
            var sectorInset = Math.Clamp(minCell * 0.035f, 2f, 8f);
            var sectorRect = cell.Grow(-sectorInset);
            var sectorName = string.IsNullOrWhiteSpace(group[0].SectorName) ? group[0].SectorId : group[0].SectorName;
            var sectorShape = ShapeForSector(group[0].SectorId, group.Length, minCell);
            var sectorLayout = new SectorLayout(
                group[0].SectorId,
                sectorName,
                sectorRect,
                group.Length,
                sectorShape,
                BuildSectorPolygon(sectorRect, group[0].SectorId, sectorShape));
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

        var aspect = Math.Max(0.5f, inner.Size.X / Math.Max(1f, inner.Size.Y));
        var columns = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(count * aspect)));
        var rows = Math.Max(1, (int)MathF.Ceiling(count / (float)columns));
        var cell = new Vector2(inner.Size.X / columns, inner.Size.Y / rows);
        var fontSize = _systemFontSize;

        for (var index = 0; index < count; index++)
        {
            var entry = sectorSystems[index];
            var row = index / columns;
            var column = index % columns;
            var jitter = new Vector2(
                HashSigned(entry.Id, 17) * Math.Min(16f, cell.X * 0.20f),
                HashSigned(entry.Id, 31) * Math.Min(12f, cell.Y * 0.18f));
            var position = inner.Position + new Vector2((column + 0.5f) * cell.X, (row + 0.5f) * cell.Y) + jitter;
            var clampInset = Math.Clamp(sectorMin * 0.10f, 2f, 22f);
            position = ClampToRect(position, sector.Rect.Grow(-clampInset));
            position = MoveInsidePolygon(position, sector.Rect.GetCenter(), sector.Polygon);
            var starRadius = StarRadius(entry);
            var labelRect = LabelRectFor(entry.DisplayName, position, starRadius, fontSize, sector.Rect);
            _layouts.Add(new SystemLayout(entry, sector, position, starRadius, labelRect, !_compactSystemLabels));
        }

        ResolveLabelOverlaps(sector);
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

    private Rect2 LabelRectFor(string text, Vector2 star, float starRadius, int fontSize, Rect2 bounds)
    {
        var font = GetThemeDefaultFont();
        var width = font?.GetStringSize(text, HorizontalAlignment.Left, -1f, fontSize).X ?? text.Length * fontSize * 0.55f;
        var maxWidth = _compactSystemLabels ? 86f : 132f;
        width = Math.Clamp(width, 28f, maxWidth);
        var height = fontSize + 5f;
        var offset = star.X > bounds.GetCenter().X
            ? new Vector2(-width - starRadius - 13f, -height * 0.5f)
            : new Vector2(starRadius + 13f, -height * 0.5f);
        var position = star + offset;
        position.X = Math.Clamp(position.X, bounds.Position.X + 8f, bounds.End.X - width - 8f);
        position.Y = Math.Clamp(position.Y, bounds.Position.Y + 8f, bounds.End.Y - height - 8f);
        return new Rect2(position, new Vector2(width, height));
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
        DrawString(font, _panelRect.Position + new Vector2(24f, 31f), "STARMAP", HorizontalAlignment.Left, -1f, 20, new Color(0.66f, 1f, 0.94f, 0.96f));
        DrawString(font, _panelRect.Position + new Vector2(132f, 31f), "M", HorizontalAlignment.Left, -1f, 14, new Color(1f, 0.82f, 0.34f, 0.76f));
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
        DrawRect(_mapRect, new Color(0f, 0.018f, 0.036f, 0.96f), true);
        DrawRect(new Rect2(_mapRect.Position, new Vector2(_mapRect.Size.X, _mapRect.Size.Y * 0.44f)), new Color(0.02f, 0.09f, 0.13f, 0.24f), true);
        DrawRect(new Rect2(_mapRect.Position + new Vector2(0f, _mapRect.Size.Y * 0.58f), new Vector2(_mapRect.Size.X, _mapRect.Size.Y * 0.42f)), new Color(0.04f, 0.02f, 0.07f, 0.18f), true);

        for (var i = 0; i < 9; i++)
        {
            var y = _mapRect.Position.Y + Hash01(i * 71 + 9) * _mapRect.Size.Y;
            var start = new Vector2(_mapRect.Position.X - 80f, y);
            var end = new Vector2(_mapRect.End.X + 80f, y + (Hash01(i * 101 + 12) * 2f - 1f) * 120f);
            var color = i % 2 == 0
                ? new Color(0.04f, 0.26f, 0.38f, 0.055f)
                : new Color(0.18f, 0.08f, 0.22f, 0.045f);
            DrawLine(start, end, color, 34f + Hash01(i * 19) * 44f, true);
        }

        for (var i = 0; i < 250; i++)
        {
            var x = _mapRect.Position.X + Hash01(i * 37 + 11) * _mapRect.Size.X;
            var y = _mapRect.Position.Y + Hash01(i * 53 + 7) * _mapRect.Size.Y;
            var bright = Hash01(i * 83 + 3);
            var alpha = 0.14f + bright * 0.58f;
            var radius = 0.45f + Hash01(i * 97 + 19) * 1.35f;
            var star = new Vector2(x, y);
            var tint = bright > 0.86f
                ? new Color(1f, 0.86f, 0.52f, alpha)
                : new Color(0.58f, 0.90f, 1f, alpha);
            DrawCircle(star, radius, tint);
            if (bright > 0.94f)
            {
                DrawLine(star - new Vector2(radius * 2.4f, 0f), star + new Vector2(radius * 2.4f, 0f), WithAlpha(tint, alpha * 0.36f), 0.7f, true);
                DrawLine(star - new Vector2(0f, radius * 2.4f), star + new Vector2(0f, radius * 2.4f), WithAlpha(tint, alpha * 0.30f), 0.7f, true);
            }
        }

        for (var i = 1; i < 4; i++)
        {
            var x = _mapRect.Position.X + _mapRect.Size.X * i / 4f;
            DrawLine(new Vector2(x, _mapRect.Position.Y), new Vector2(x, _mapRect.End.Y), new Color(0.06f, 0.58f, 0.82f, 0.045f), 1f, true);
        }

        for (var i = 1; i < 3; i++)
        {
            var y = _mapRect.Position.Y + _mapRect.Size.Y * i / 3f;
            DrawLine(new Vector2(_mapRect.Position.X, y), new Vector2(_mapRect.End.X, y), new Color(0.06f, 0.58f, 0.82f, 0.04f), 1f, true);
        }

        DrawRect(_mapRect, new Color(0.02f, 0.66f, 0.95f, 0.58f), false, 1.4f);
    }

    private void DrawSectors()
    {
        var font = GetThemeDefaultFont();
        foreach (var sector in _sectorLayouts.Values)
        {
            DrawColoredPolygon(sector.Polygon, SectorFill(sector.Id));
            for (var i = 0; i < sector.Polygon.Length; i++)
            {
                DrawLine(sector.Polygon[i], sector.Polygon[(i + 1) % sector.Polygon.Length], SectorLine(sector.Id), 1.1f, true);
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
                    sector.Rect.Position + new Vector2(8f, 16f),
                    compactName,
                    HorizontalAlignment.Left,
                    sector.Rect.Size.X - 16f,
                    _sectorFontSize,
                    new Color(0.68f, 0.92f, 1f, 0.48f));
                continue;
            }

            var center = sector.Rect.GetCenter();
            var width = font?.GetStringSize(name, HorizontalAlignment.Left, -1f, _sectorFontSize).X ?? name.Length * _sectorFontSize * 0.55f;
            DrawString(font, center - new Vector2(width * 0.5f, 4f), name, HorizontalAlignment.Left, -1f, _sectorFontSize, new Color(0.62f, 0.82f, 0.90f, 0.20f));
        }
    }

    private void DrawWarpRoute()
    {
        if (_selected is null || SameSystem(_selected.Id, _currentSystemId))
        {
            return;
        }

        var from = _layouts.FirstOrDefault(layout => SameSystem(layout.Entry.Id, _currentSystemId));
        var to = _layouts.FirstOrDefault(layout => SameSystem(layout.Entry.Id, _selected.Id));
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
        var routeColor = new Color(0.42f, 1f, 0.86f, 0.94f);
        var control = (start + end) * 0.5f - normal * Math.Clamp(routeLength * 0.18f, 48f, 112f);
        var previous = start;
        for (var i = 1; i <= 36; i++)
        {
            var t = i / 36f;
            var point = QuadraticPoint(start, control, end, t);
            DrawLine(previous, point, new Color(0.08f, 0.90f, 1f, 0.16f), 6.2f, true);
            previous = point;
        }

        for (var i = 0; i < 28; i += 2)
        {
            var t0 = i / 28f;
            var t1 = Math.Min(1f, t0 + 0.038f);
            DrawLine(QuadraticPoint(start, control, end, t0), QuadraticPoint(start, control, end, t1), routeColor, 2.6f, true);
        }

        DrawCircle(end, 6.6f, new Color(0.20f, 1f, 0.84f, 0.28f));
        DrawCircle(end, 2.8f, new Color(0.72f, 1f, 0.92f, 0.95f));
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
                DrawCircle(layout.Position, layout.StarRadius + 12f, new Color(0.28f, 0.96f, 1f, isSelected ? 0.18f : 0.10f));
            }

            if (isCurrent)
            {
                DrawCurrentMarker(layout.Position, layout.StarRadius + 12f + pulse * 2f);
            }

            if (isTarget)
            {
                DrawTargetMarker(layout.Position, layout.StarRadius + 8f);
            }

            DrawStarGlyph(layout, color, isCurrent || isSelected || isTarget, pulse);

            if (layout.LabelVisible || isCurrent || isTarget || isSelected || isHovered)
            {
                var textColor = isCurrent
                    ? new Color(1f, 0.86f, 0.42f, 0.98f)
                    : isSelected
                        ? new Color(0.72f, 1f, 0.94f, 0.98f)
                        : new Color(0.68f, 0.90f, 1f, 0.88f);
                if (isCurrent || isTarget || isSelected || isHovered)
                {
                    DrawRect(layout.LabelRect.Grow(3f), new Color(0f, 0.018f, 0.030f, 0.58f), true);
                }

                DrawTextWithShadow(font, layout.LabelRect.Position + new Vector2(0f, fontSize), TrimLabel(layout.Entry.DisplayName), layout.LabelRect.Size.X, fontSize, textColor);
            }
        }
    }

    private void DrawStarGlyph(SystemLayout layout, Color color, bool emphasized, float pulse)
    {
        var center = layout.Position;
        var radius = layout.StarRadius;
        var glow = emphasized ? 0.34f + pulse * 0.12f : 0.18f;
        DrawCircle(center, radius * 3.2f, new Color(color.R, color.G, color.B, glow * 0.22f));
        DrawCircle(center, radius * 2.0f, new Color(color.R, color.G, color.B, glow * 0.34f));
        DrawArc(center, radius * 1.75f, _pulse * 0.4f, _pulse * 0.4f + MathF.Tau * 0.72f, 24, new Color(color.R, color.G, color.B, emphasized ? 0.44f : 0.22f), 1.0f, true);

        var rayLength = radius * (emphasized ? 2.4f : 1.85f);
        _starSpark[0] = center + new Vector2(-rayLength, 0f);
        _starSpark[1] = center + new Vector2(0f, -rayLength);
        _starSpark[2] = center + new Vector2(rayLength, 0f);
        _starSpark[3] = center + new Vector2(0f, rayLength);
        DrawLine(_starSpark[0], _starSpark[2], new Color(color.R, color.G, color.B, emphasized ? 0.42f : 0.24f), 1.1f, true);
        DrawLine(_starSpark[1], _starSpark[3], new Color(color.R, color.G, color.B, emphasized ? 0.36f : 0.18f), 1.1f, true);

        DrawCircle(center, radius + 1.2f, new Color(0.01f, 0.018f, 0.022f, 0.86f));
        DrawCircle(center, radius, color);
        DrawCircle(center - new Vector2(radius * 0.25f, radius * 0.30f), Math.Max(1.4f, radius * 0.38f), new Color(1f, 0.96f, 0.72f, 0.92f));
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

    private void DrawTargetMarker(Vector2 center, float radius)
    {
        var color = new Color(0.28f, 1f, 0.86f, 0.82f);
        _targetMarker[0] = center + new Vector2(0f, -radius);
        _targetMarker[1] = center + new Vector2(radius, 0f);
        _targetMarker[2] = center + new Vector2(0f, radius);
        _targetMarker[3] = center + new Vector2(-radius, 0f);
        DrawPolyline(_targetMarker, color, 1.4f, true);
        DrawLine(_targetMarker[^1], _targetMarker[0], color, 1.4f, true);
    }

    private void DrawTextWithShadow(Font? font, Vector2 position, string text, float width, int fontSize, Color color)
    {
        DrawString(font, position + new Vector2(1.2f, 1.2f), text, HorizontalAlignment.Left, width, fontSize, new Color(0f, 0f, 0f, Math.Clamp(color.A * 0.72f, 0f, 0.86f)));
        DrawString(font, position, text, HorizontalAlignment.Left, width, fontSize, color);
    }

    private void DrawSidePanel()
    {
        var font = GetThemeDefaultFont();
        DrawRect(_sideRect, new Color(0f, 0.08f, 0.12f, 0.82f), true);
        DrawRect(_sideRect, new Color(0.12f, 0.78f, 1f, 0.34f), false, 1.1f);
        DrawString(font, _sideRect.Position + new Vector2(18f, 32f), "WARP TARGET", HorizontalAlignment.Left, -1f, 15, new Color(0.56f, 1f, 0.92f, 0.94f));

        var currentName = _systems.FirstOrDefault(system => SameSystem(system.Id, _currentSystemId))?.DisplayName ?? _currentSystemId;
        DrawString(font, _sideRect.Position + new Vector2(18f, 62f), "CURRENT", HorizontalAlignment.Left, -1f, 12, new Color(0.62f, 0.78f, 0.86f, 0.68f));
        DrawString(font, _sideRect.Position + new Vector2(18f, 82f), currentName, HorizontalAlignment.Left, _sideRect.Size.X - 36f, 16, new Color(1f, 0.86f, 0.42f, 0.96f));

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

        var okColor = _selected is null
            ? new Color(0.16f, 0.30f, 0.34f, 0.54f)
            : new Color(0.02f, 0.48f, 0.58f, 0.86f);
        DrawRect(_okRect, okColor, true);
        DrawRect(_okRect, new Color(0.28f, 1f, 0.88f, _selected is null ? 0.26f : 0.76f), false, 1.3f);
        var okText = _selected is null ? "SELECT SYSTEM" : "OK";
        var textWidth = font?.GetStringSize(okText, HorizontalAlignment.Left, -1f, 16).X ?? 40f;
        DrawString(font, _okRect.Position + new Vector2((_okRect.Size.X - textWidth) * 0.5f, 23f), okText, HorizontalAlignment.Left, -1f, 16, new Color(0.80f, 1f, 0.94f, _selected is null ? 0.46f : 0.96f));
    }

    private void DrawSelectedSystem(Font? font, StarMapSystemEntry entry)
    {
        var p = _selectedCardRect.Position + new Vector2(16f, 28f);
        DrawCircle(p + new Vector2(10f, 6f), 8f, new Color(entry.StarColor.R, entry.StarColor.G, entry.StarColor.B, 0.18f));
        DrawCircle(p + new Vector2(10f, 6f), 5f, entry.StarColor);
        DrawTextWithShadow(font, p + new Vector2(26f, 12f), entry.DisplayName, _selectedCardRect.Size.X - 50f, 17, new Color(0.78f, 1f, 0.94f, 0.98f));
        DrawInfoLine(font, p + new Vector2(0f, 48f), "SECTOR", SectorName(entry));
        DrawInfoLine(font, p + new Vector2(0f, 72f), "STAR", entry.StarDisplayName);
        DrawInfoLine(font, p + new Vector2(0f, 96f), "SIZE", entry.StarWorldSize.ToString("0"));
        DrawInfoLine(font, p + new Vector2(0f, 120f), "CORONA", entry.CoronaIntensity.ToString("0.00"));
        DrawInfoLine(font, p + new Vector2(0f, 144f), "MOTION", entry.AnimationSpeed.ToString("0.00"));
        DrawInfoLine(font, p + new Vector2(0f, 168f), "PLANETS", entry.PlanetCount.ToString());
        var status = SameSystem(entry.Id, _currentSystemId)
            ? "CURRENT SYSTEM"
            : SameSystem(entry.Id, _tunedSystemId)
                ? "ENGINE TUNED"
                : "READY TO TUNE";
        DrawTextWithShadow(font, p + new Vector2(0f, 216f), status, _selectedCardRect.Size.X - 32f, 14, new Color(1f, 0.82f, 0.34f, 0.90f));
    }

    private void DrawInfoLine(Font? font, Vector2 position, string label, string value)
    {
        DrawString(font, position, label, HorizontalAlignment.Left, 72f, 12, new Color(0.58f, 0.78f, 0.88f, 0.72f));
        DrawTextWithShadow(font, position + new Vector2(74f, 0f), value, _selectedCardRect.Size.X - 112f, 13, new Color(0.70f, 0.92f, 1f, 0.84f));
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
        var width = 318f;
        var height = 76f + rows * 22f + (planets.Count > rows ? 20f : 0f);
        var position = _planetPopupAnchor + new Vector2(20f, -12f);
        position.X = Math.Clamp(position.X, _panelRect.Position.X + 18f, _panelRect.End.X - width - 18f);
        position.Y = Math.Clamp(position.Y, _panelRect.Position.Y + 56f, _panelRect.End.Y - height - 18f);
        var rect = new Rect2(position, new Vector2(width, height));

        DrawRect(rect, new Color(0.005f, 0.030f, 0.046f, 0.96f), true);
        DrawRect(rect, new Color(0.24f, 0.96f, 1f, 0.54f), false, 1.2f);
        DrawRect(new Rect2(rect.Position, new Vector2(rect.Size.X, 34f)), new Color(0.02f, 0.15f, 0.20f, 0.70f), true);
        DrawCircle(rect.Position + new Vector2(19f, 21f), 6.0f, _planetPopupEntry.StarColor);
        DrawTextWithShadow(font, rect.Position + new Vector2(34f, 25f), _planetPopupEntry.DisplayName, rect.Size.X - 48f, 15, new Color(0.76f, 1f, 0.94f, 0.98f));
        DrawString(font, rect.Position + new Vector2(16f, 54f), "PLANETS", HorizontalAlignment.Left, -1f, 12, new Color(0.58f, 0.78f, 0.88f, 0.72f));

        for (var i = 0; i < rows; i++)
        {
            var planet = planets[i];
            var y = rect.Position.Y + 76f + i * 22f;
            DrawCircle(new Vector2(rect.Position.X + 18f, y - 5f), 5.0f, new Color(planet.MapColor.R, planet.MapColor.G, planet.MapColor.B, 0.28f));
            DrawCircle(new Vector2(rect.Position.X + 18f, y - 5f), 3.2f, planet.MapColor);
            if (planet.HasRings)
            {
                DrawArc(new Vector2(rect.Position.X + 18f, y - 5f), 7.2f, -0.2f, MathF.PI + 0.2f, 16, new Color(0.92f, 0.84f, 0.62f, 0.72f), 1f, true);
            }

            DrawTextWithShadow(font, new Vector2(rect.Position.X + 32f, y), planet.DisplayName, 148f, 13, new Color(0.78f, 0.94f, 1f, 0.92f));
            DrawString(font, new Vector2(rect.Position.X + 184f, y), planet.Archetype, HorizontalAlignment.Left, rect.Size.X - 200f, 11, new Color(0.56f, 0.76f, 0.84f, 0.66f));
        }

        if (planets.Count > rows)
        {
            DrawString(font, rect.Position + new Vector2(16f, height - 12f), $"+{planets.Count - rows} more", HorizontalAlignment.Left, -1f, 11, new Color(0.58f, 0.78f, 0.88f, 0.72f));
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

    private int SystemFontSize(int sectorCount, float minCell)
    {
        if (sectorCount > 24 || minCell < 120f || _systems.Count > 140)
        {
            return 8;
        }

        if (sectorCount > 12 || minCell < 155f || _systems.Count > 72)
        {
            return 9;
        }

        if (sectorCount > 6 || minCell < 190f || _systems.Count > 36)
        {
            return 10;
        }

        return _systems.Count switch
        {
            > 96 => 9,
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
        return new Color(0.015f + h * 0.030f, 0.070f + h * 0.030f, 0.14f + h * 0.040f, 0.24f);
    }

    private static Color SectorLine(string key)
    {
        var h = Hash01(key, 29);
        return new Color(0.08f + h * 0.12f, 0.64f + h * 0.24f, 1f, 0.52f);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, Math.Clamp(alpha, 0f, 1f));
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
