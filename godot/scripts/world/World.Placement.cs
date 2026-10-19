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
    /// anchor. Position refusals stay quiet; any other refusal toasts once for the whole line (<see cref="OnCommandRejected"/>).
    /// </summary>
    private void SendLine(GridPosition cursor)
    {
        if (_view is null || _lineAnchor is null || _tool == BuildTool.Cursor)
        {
            CancelLine();
            return;
        }

        var commands = PlacementLine.Commands(PlacementLine.Plan(_view, LineTo(cursor)));
        CancelLine();
        _lineToasts.Clear();
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
    /// Right-drag: a marking <see cref="TryRemoveAt"/> once per block in the whole drag, like Mindustry's
    /// removal drag. Every cell of the block (its footprint, or the site's cells) counts as done after the first.
    /// </summary>
    private void RemoveOnceInDrag(GridPosition cell)
    {
        if (_view is null || !_removedThisDrag.Add(cell))
        {
            return;
        }

        if (_view.TryGetSiteAt(cell, out var site))
        {
            _removedThisDrag.UnionWith(site.Tiles);
        }
        else
        {
            // A breaking plan covers the whole block standing on the cell (just the cell when it is empty).
            var probe = new BuildPlan(new BuildSpec("", null, cell, Direction.East), Breaking: true);
            _removedThisDrag.UnionWith(_view.PlanTiles(probe));
        }

        TryRemoveAt(cell, onlyMark: true);
    }

    /// <summary>
    /// Cells from <paramref name="from"/> (excluded) to <paramref name="to"/> (included) along a Bresenham line,
    /// in order: what a right drag crossed between two motion events.
    /// </summary>
    private static List<GridPosition> DragCells(GridPosition from, GridPosition to)
    {
        var cells = new List<GridPosition>();
        // 4-connected line (one axis per step, as Mindustry does for belts): every cell shares an edge
        // with the previous one, so a fast diagonal drag never leaves corner-only gaps.
        int x = from.X, y = from.Y;
        int dx = Math.Abs(to.X - x), dy = Math.Abs(to.Y - y);
        int sx = x < to.X ? 1 : -1, sy = y < to.Y ? 1 : -1;
        int ix = 0, iy = 0;
        while (ix < dx || iy < dy)
        {
            // Step along the axis that lags behind the ideal line; ties go to x.
            if ((1 + 2 * ix) * dy <= (1 + 2 * iy) * dx && ix < dx)
            {
                x += sx;
                ix++;
            }
            else
            {
                y += sy;
                iy++;
            }

            cells.Add(new GridPosition(x, y));
        }

        return cells;
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
