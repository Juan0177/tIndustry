using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Placing/removing structures on the grid and screen→cell mapping.</summary>
public partial class World
{
    private void TryPlaceAt(GridPosition cell)
    {
        if (_view is null || _tool == BuildTool.Cursor)
        {
            return;
        }

        SimCommand command = _tool switch
        {
            BuildTool.Belt => new PlaceBelt(LocalPlayer, cell, _placeDir, "conveyor-basic"),
            BuildTool.BeltFast => new PlaceBelt(LocalPlayer, cell, _placeDir, "conveyor-fast"),
            BuildTool.BeltExpress => new PlaceBelt(LocalPlayer, cell, _placeDir, "conveyor-express"),
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
            _ => throw new InvalidOperationException($"Strumento non piazzabile: {_tool}")
        };

        // Outcome (graphics refresh, rejection toast) arrives through the host events.
        SubmitCommand(command, quietWhenBlocked: _draggingPlace);

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

        var label = _view?.Content.DisplayName(_sorterFilterId) ?? _sorterFilterId;
        _hud?.ShowToast($"Filtro: {label}");
    }

    private void TryRemoveAt(GridPosition cell)
    {
        if (_view is null)
        {
            return;
        }

        SubmitCommand(new Remove(LocalPlayer, cell));
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
        _hud?.ShowToast($"Seed linea Core · nastro {_view.Belts.Count}");
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
