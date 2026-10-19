using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Placing/removing structures on the grid and screen→cell mapping.</summary>
public partial class World
{
    /// <summary>A left drag is under way: its line is only previewed until the button goes up.</summary>
    internal bool LineDragActive => _lineAnchor is not null;

    /// <summary>The tools that lay belts: the client's one list of them.</summary>
    private static bool IsBelt(BuildTool tool) => tool is BuildTool.Belt or BuildTool.BeltFast or BuildTool.BeltExpress;

    /// <summary>Tools whose block faces a way (R, the drag).</summary>
    private static bool IsOrientable(BuildTool tool) =>
        IsBelt(tool) || KindOf(tool) is { } kind && PlacementPreview.HasDirection(kind);

    /// <summary>
    /// The current tool's line from the drag's anchor (<paramref name="cursor"/> itself without a drag) to
    /// <paramref name="cursor"/>, as <see cref="PlacementLine"/> takes it: the same function as the agents' <c>piazza</c>.
    /// </summary>
    private LineRequest LineTo(GridPosition cursor) => new(
        LocalPlayer, Hud.StructureIdFor(ToHudTool(_tool)), _lineAnchor ?? cursor, cursor, _placeDir, _lineFixed,
        _lineCtrl ? LineShape.Diagonal : LineShape.Straight,
        FilterItemId: _tool == BuildTool.Sorter ? _sorterFilterId : null);

    // The line planned this frame: the ghost and the refusal hint ask for it more than once.
    private (ulong Frame, LineRequest Request, IReadOnlyList<LinePlacement> Plan)? _linePlan;

    /// <summary>The preview of the line to <paramref name="cursor"/> (needs a game and a tool), once per frame.</summary>
    private IReadOnlyList<LinePlacement> PlanLine(GridPosition cursor)
    {
        var request = LineTo(cursor);
        var frame = Engine.GetProcessFrames();
        if (_linePlan is { } cached && cached.Frame == frame && cached.Request == request)
        {
            return cached.Plan;
        }

        var plan = PlacementLine.Plan(_view!, request);
        _linePlan = (frame, request, plan);
        return plan;
    }

    /// <summary>Left press with a tool: the anchor of the line, nothing sent yet.</summary>
    private void BeginLine(GridPosition cell, bool ctrl)
    {
        _lineAnchor = cell;
        _lineFixed = false;
        _lineCtrl = ctrl;
        QueueRedraw();
    }

    /// <summary>
    /// Left release: the line planned again on the game as it is now, and all its commands sent in order from the
    /// anchor. Refusals the line foresees stay quiet while something is sent; when nothing is (a refused click), the first
    /// one toasts. Of the simulation's refusals, position codes stay quiet and any other toasts once for the whole line
    /// (<see cref="OnCommandRejected"/>).
    /// </summary>
    private void SendLine(GridPosition cursor)
    {
        if (_view is null || _lineAnchor is null || _tool == BuildTool.Cursor)
        {
            CancelLine();
            return;
        }

        var line = PlacementLine.Plan(_view, LineTo(cursor));
        var commands = PlacementLine.Commands(line);
        CancelLine();
        _lineToasts.Clear();
        if (commands.Count == 0 && line.FirstOrDefault(p => p.Refusal is not null)?.Refusal is { } refused)
        {
            // Nothing goes out (a refused click, a line refused everywhere): say why once, as a refused click always did.
            _hud?.ShowToast(Testi.Rifiuto(new Refusal(refused), _view));
            return;
        }

        foreach (var command in commands)
        {
            SubmitCommand(command, quietWhenBlocked: true);
        }
    }

    /// <summary>Drops the drag (Esc, right button, a panel, focus lost): nothing is sent and the tool stays.</summary>
    private void CancelLine()
    {
        _lineAnchor = null;
        _lineFixed = false;
        QueueRedraw();
    }

    private void CycleSorterFilter()
    {
        _sorterFilterId = SorterFilters.Cycle(_sorterFilterId);
        _hud?.SetSorterFilterBrush(_sorterFilterId);

        // Also cycle hovered / selected sorter cell if present.
        if (_view is not null
            && _hover is { } hover
            && _view.Belts.TryGetValue(hover, out var cell)
            && cell.Kind == LogisticsKind.Sorter)
        {
            // Cycle from the filter the sorter will have once earlier same-tick presses are applied.
            var current = PendingCommands.OfType<SetSorterFilter>().LastOrDefault(c => c.Tile == hover)?.ItemId
                ?? cell.FilterItemId;
            _sorterFilterId = SorterFilters.Cycle(current);
            SubmitCommand(new SetSorterFilter(LocalPlayer, hover, _sorterFilterId));
            _hud?.SetSorterFilterBrush(_sorterFilterId);
        }

        _hud?.ShowToast(Testi.F("selezionatore.filtro", Testi.Oggetto(_sorterFilterId, _view?.Content)));
    }

    /// <summary>
    /// Remove on <paramref name="cell"/>. A single right-click toggles (it takes a dismantling mark off);
    /// with <paramref name="onlyMark"/> (right-drags) it only marks, so overlapping drags keep their marks.
    /// </summary>
    private void TryRemoveAt(GridPosition cell, bool onlyMark)
    {
        if (_view is null)
        {
            return;
        }

        SubmitCommand(new Remove(LocalPlayer, cell, onlyMark));
    }

    /// <summary>
    /// Right release after a drag: the rectangle from the anchor to <paramref name="cursor"/> marks every block and
    /// cancels every plan in it (<see cref="RemovalArea"/>, the agents' <c>smonta {da, a}</c> too). Never unmarks.
    /// </summary>
    private void RemoveRectangle(GridPosition cursor)
    {
        if (_view is null)
        {
            return;
        }

        foreach (var command in RemovalArea.Commands(_view, LocalPlayer, _removeStart, cursor))
        {
            SubmitCommand(command);
        }
    }

    /// <summary>
    /// Middle click: the block standing (or the site) on <paramref name="cell"/> becomes the tool in hand, with its
    /// direction and the sorter's filter. A locked block toasts and keeps the tool; an empty cell does nothing.
    /// </summary>
    private void PickAt(GridPosition cell)
    {
        if (_view is null || PanelOpen)
        {
            return;
        }

        BuildSpec spec;
        if (_view.TryGetSiteAt(cell, out var site))
        {
            spec = site.Spec;
        }
        else if (!_view.TryGetSpecAt(cell, out spec, out _))
        {
            return;
        }

        var tool = Enum.GetValues<BuildTool>()
            .FirstOrDefault(t => t != BuildTool.Cursor && Hud.StructureIdFor(ToHudTool(t)) == spec.StructureId);
        if (tool == BuildTool.Cursor)
        {
            return;
        }

        CancelLine();
        SelectTool(tool);
        if (_tool != tool)
        {
            return; // locked: SelectTool said so
        }

        if (IsOrientable(tool))
        {
            _placeDir = spec.Direction;
            _hud?.SetDirectionLabel(_placeDir);
        }

        if (tool == BuildTool.Sorter && spec.FilterItemId is { } filter)
        {
            _sorterFilterId = filter;
            _hud?.SetSorterFilterBrush(filter);
        }

        QueueRedraw();
    }

    /// <summary>
    /// Optional bootstrap (<c>TINDUSTRY_SEED_CORE_LINE=1</c>, ui playtest): miner on starter iron + east
    /// belts into Core for quick delivery checks. Dev tool: the line stands at once, as with
    /// <c>InstantBuild</c> local to the seed, so it reaches past the read-only view to
    /// <see cref="Factory.PlaceInstant(BuildSpec, bool)"/> and pays like a placement.
    /// </summary>
    private void SeedCoreDeliveryLine()
    {
        if (_view?.Terrain is null || _view is not Factory factory)
        {
            return;
        }

        var coreOrigin = new GridPosition(
            _view.CoreTiles.Min(t => t.X),
            _view.CoreTiles.Min(t => t.Y));
        var minerPos = new GridPosition(
            Math.Max(0, coreOrigin.X - Miner.Size - 2),
            coreOrigin.Y);
        void Place(BuildSpec spec)
        {
            if (!factory.PlaceInstant(spec, charge: true))
            {
                GD.PrintErr($"SEED_CORE_LINE: {spec.StructureId} su {spec.Origin} rifiutato");
            }
        }

        Place(new BuildSpec(Miner.BasicId, BuildingKind.Miner, minerPos, Direction.East));
        for (var x = minerPos.X + Miner.Size; x < coreOrigin.X; x++)
        {
            Place(new BuildSpec(BuildSpec.DefaultConveyorId, null, new GridPosition(x, coreOrigin.Y), Direction.East));
        }

        _visualDirty = true;
        _buildingsDirty = true;
        RebuildBeltVisual();
        RebuildBuildingVisuals();
        UpdateHud();
        GD.Print($"SEED_CORE_LINE: belts={_view.Belts.Count} miners={_view.Miners.Count}");
        _hud?.ShowToast(Testi.F("dev.linea.core", _view.Belts.Count));
    }

    private GridPosition ScreenToCell(Vector2 screenPos)
    {
        // Viewport → world via canvas transform (Camera2D zoom/pan). Do not use
        // raw screen/tile division — that places far from the focused Core on 1000².
        var world = GetViewport().GetCanvasTransform().AffineInverse() * screenPos;
        var x = Mathf.FloorToInt(world.X / TileSize);
        var y = Mathf.FloorToInt(world.Y / TileSize);
        return new GridPosition(x, y);
    }
}
