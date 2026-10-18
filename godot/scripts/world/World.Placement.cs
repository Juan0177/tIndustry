using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Placing/removing structures on the grid and screen→cell mapping.</summary>
public partial class World
{
    /// <summary>
    /// Sends the current tool's command for <paramref name="cell"/>. <paramref name="crossing"/>: a belt inside a dragged
    /// line over a perpendicular belt, which the simulation may turn into a junction (<see cref="Factory.BeltCrossingAt"/>).
    /// </summary>
    private void TryPlaceAt(GridPosition cell, bool crossing = false)
    {
        if (_view is null || _tool == BuildTool.Cursor)
        {
            return;
        }

        // Outcome (graphics refresh, rejection toast) arrives through the host events.
        SubmitCommand(PlaceCommandAt(cell, crossing), quietWhenBlocked: _draggingPlace);
    }

    // Belt drag: which cells to place and which is a crossing (shared with the agents' piazza line).
    private readonly BeltDrag _beltDrag = new();

    /// <summary>Starts a placing drag on the press cell (never a crossing).</summary>
    private void BeginPlaceDrag(GridPosition cell)
    {
        _beltDrag.Begin(cell);
        _crossingToastShown = false;
        TryPlaceAt(cell);
    }

    /// <summary>
    /// One cell a drag moved onto: <see cref="BeltDrag.Next"/> says what to place (a belt over a perpendicular one waits
    /// for the next cell, then goes as a crossing when it lies inside the line). A held cell the drag never passes would
    /// only be refused as occupied, so on release it is simply dropped.
    /// </summary>
    private void PlaceDragged(GridPosition cell)
    {
        if (_view is null)
        {
            return;
        }

        var belts = _tool is BuildTool.Belt or BuildTool.BeltFast or BuildTool.BeltExpress;
        foreach (var (place, crossing) in _beltDrag.Next(_view, cell, _placeDir, belts))
        {
            TryPlaceAt(place, crossing);
        }
    }

    /// <summary>The command the current tool sends for <paramref name="cell"/> (not the cursor).</summary>
    private SimCommand PlaceCommandAt(GridPosition cell, bool crossing = false)
    {
        return _tool switch
        {
            BuildTool.Belt => new PlaceBelt(LocalPlayer, cell, _placeDir, "conveyor-basic", crossing),
            BuildTool.BeltFast => new PlaceBelt(LocalPlayer, cell, _placeDir, "conveyor-fast", crossing),
            BuildTool.BeltExpress => new PlaceBelt(LocalPlayer, cell, _placeDir, "conveyor-express", crossing),
            BuildTool.Miner => Place(BuildingKind.Miner),
            BuildTool.MinerAdvanced => Place(BuildingKind.MinerAdvanced),
            BuildTool.Smelter => Place(BuildingKind.Smelter),
            BuildTool.Assembler => Place(BuildingKind.Assembler),
            BuildTool.Junction => Place(BuildingKind.Junction),
            BuildTool.Splitter => Place(BuildingKind.Splitter),
            BuildTool.Generator => Place(BuildingKind.Generator),
            BuildTool.Sorter => new PlaceBuilding(
                LocalPlayer, BuildingKind.Sorter, cell, _placeDir, FilterItemId: _sorterFilterId),
            BuildTool.Bridge => Place(BuildingKind.Bridge),
            BuildTool.Extractor => Place(BuildingKind.Extractor),
            BuildTool.PowerNode => Place(BuildingKind.PowerNode),
            BuildTool.PowerNodeT2 => Place(BuildingKind.PowerNodeT2),
            BuildTool.Lamp => Place(BuildingKind.Lamp),
            _ => throw new InvalidOperationException($"Strumento non piazzabile: {_tool}")
        };

        PlaceBuilding Place(BuildingKind kind) => new(LocalPlayer, kind, cell, _placeDir);
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
    /// in order: what a drag crossed between two motion events.
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
