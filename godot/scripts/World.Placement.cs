using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Placing/removing structures on the grid and screen→cell mapping.</summary>
public partial class World
{
    private void TryPlaceAt(GridPosition cell)
    {
        if (_slice is null || _tool == BuildTool.Cursor)
        {
            return;
        }

        var structureId = FactoryHud.StructureIdFor(ToHudTool(_tool));
        if (!_slice.IsStructureUnlocked(structureId))
        {
            _hud?.ShowToast($"{ToolIt(_tool)} bloccato: sbloccalo in Ricerca (T).");
            return;
        }

        var costMul = _tool == BuildTool.Bridge ? 2 : 1;
        if (!_slice.CanAffordStructure(structureId, costMul))
        {
            var need = _slice.FormatNeedMessage(structureId, costMul);
            _hud?.ShowToast(string.IsNullOrEmpty(need) ? "Risorse insufficienti" : need);
            return;
        }

        var placed = false;
        switch (_tool)
        {
            case BuildTool.Belt:
                placed = _slice.TryPlaceBelt(cell, _placeDir);
                if (placed)
                {
                    _visualDirty = true;
                }

                break;
            case BuildTool.BeltFast:
                placed = _slice.TryPlaceBelt(cell, _placeDir, "conveyor-fast");
                if (placed)
                {
                    _visualDirty = true;
                }

                break;
            case BuildTool.BeltExpress:
                placed = _slice.TryPlaceBelt(cell, _placeDir, "conveyor-express");
                if (placed)
                {
                    _visualDirty = true;
                }

                break;
            case BuildTool.Miner:
                placed = _slice.TryPlaceMiner(cell, _placeDir);
                if (placed)
                {
                    _buildingsDirty = true;
                }

                break;
            case BuildTool.MinerAdvanced:
                placed = _slice.TryPlaceMiner(cell, _placeDir, definitionId: Miner.AdvancedId);
                if (placed)
                {
                    _buildingsDirty = true;
                }

                break;
            case BuildTool.Smelter:
                placed = _slice.TryPlaceSmelter(cell, _placeDir);
                if (placed)
                {
                    _buildingsDirty = true;
                }

                break;
            case BuildTool.Assembler:
                placed = _slice.TryPlaceAssembler(cell, _placeDir);
                if (placed)
                {
                    _buildingsDirty = true;
                }

                break;
            case BuildTool.Junction:
                placed = _slice.TryPlaceJunction(cell, _placeDir);
                if (placed)
                {
                    _visualDirty = true;
                }

                break;
            case BuildTool.Splitter:
                placed = _slice.TryPlaceSplitter(cell, _placeDir);
                if (placed)
                {
                    _visualDirty = true;
                }

                break;
            case BuildTool.Generator:
                placed = _slice.TryPlaceGenerator(cell, _placeDir);
                if (placed)
                {
                    _buildingsDirty = true;
                }

                break;
            case BuildTool.Sorter:
                placed = _slice.TryPlaceSorter(cell, _placeDir, _sorterFilterId);
                if (placed)
                {
                    _visualDirty = true;
                }

                break;
            case BuildTool.Bridge:
                placed = _slice.TryPlaceBridge(cell, _placeDir);
                if (placed)
                {
                    _visualDirty = true;
                }

                break;
            case BuildTool.Extractor:
                placed = _slice.TryPlaceExtractor(cell, _placeDir);
                if (placed)
                {
                    _buildingsDirty = true;
                }

                break;
            case BuildTool.PowerNode:
                placed = _slice.TryPlacePowerNode(cell);
                if (placed)
                {
                    _buildingsDirty = true;
                }

                break;
            case BuildTool.PowerNodeT2:
                placed = _slice.TryPlacePowerNode(cell, PowerNode.Tier2Id);
                if (placed)
                {
                    _buildingsDirty = true;
                }

                break;
        }

        if (!placed && _slice.CanAffordStructure(structureId, costMul))
        {
            if (!_draggingPlace)
            {
                _hud?.ShowToast(_slice.CoreTiles.Contains(cell)
                    ? "Non puoi costruire sul Core"
                    : "Cella occupata o non valida");
            }
        }
    }

    private void CycleSorterFilter()
    {
        var idx = Array.FindIndex(
            SorterFilterIds,
            id => string.Equals(id, _sorterFilterId, StringComparison.Ordinal));
        _sorterFilterId = SorterFilterIds[(idx + 1 + SorterFilterIds.Length) % SorterFilterIds.Length];
        _hud?.SetSorterFilterBrush(_sorterFilterId);

        // Also cycle hovered / selected sorter cell if present.
        if (_slice is not null
            && _hover is { } hover
            && _slice.Belts.TryGet(hover, out var cell)
            && cell.Kind == LogisticsKind.Sorter)
        {
            cell.CycleFilterItem(SorterFilterIds);
            _sorterFilterId = cell.FilterItemId ?? _sorterFilterId;
            _hud?.SetSorterFilterBrush(_sorterFilterId);
            _visualDirty = true;
        }

        var label = _slice?.Content.DisplayName(_sorterFilterId) ?? _sorterFilterId;
        _hud?.ShowToast($"Filtro: {label}");
    }

    private void TryRemoveAt(GridPosition cell)
    {
        if (_slice is null)
        {
            return;
        }

        if (_slice.TryRemoveBuildingAt(cell))
        {
            _buildingsDirty = true;
            return;
        }

        if (_slice.TryRemoveBelt(cell))
        {
            _visualDirty = true;
        }
    }

    /// <summary>
    /// Optional bootstrap (<c>TINDUSTRY_SEED_CORE_LINE=1</c>): miner on starter iron
    /// + east belts into Core for quick delivery checks.
    /// </summary>
    private void SeedCoreDeliveryLine()
    {
        if (_slice?.Terrain is null)
        {
            return;
        }

        var coreOrigin = new GridPosition(
            _slice.CoreTiles.Min(t => t.X),
            _slice.CoreTiles.Min(t => t.Y));
        var minerPos = new GridPosition(
            Math.Max(0, coreOrigin.X - Miner.Size - 2),
            coreOrigin.Y);
        if (!_slice.TryPlaceMiner(minerPos, Direction.East))
        {
            GD.PrintErr($"SEED_CORE_LINE: miner fail at {minerPos}");
            return;
        }

        for (var x = minerPos.X + Miner.Size; x < coreOrigin.X; x++)
        {
            if (!_slice.TryPlaceBelt(new GridPosition(x, coreOrigin.Y), Direction.East))
            {
                GD.PrintErr($"SEED_CORE_LINE: belt fail at ({x},{coreOrigin.Y})");
            }
        }

        _visualDirty = true;
        _buildingsDirty = true;
        RebuildBeltVisual();
        RebuildBuildingVisuals();
        UpdateHud();
        GD.Print($"SEED_CORE_LINE: belts={_slice.Belts.Count} miners={_slice.Miners.Count}");
        _hud?.ShowToast($"Seed linea Core · nastro {_slice.Belts.Count}");
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
