using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Immediate-mode drawing: terrain, deposits, placement ghost, direction hints.</summary>
public partial class World
{
    public override void _Draw()
    {
        GetVisibleTileRange(out var visMinX, out var visMinY, out var visMaxX, out var visMaxY);
        var mapW = ActiveMapWidth;
        var mapH = ActiveMapHeight;
        var camZoom = HasNode("Camera") ? Math.Max(0.01f, GetNode<Camera2D>("Camera").Zoom.X) : 1f;
        var screenTilePx = TileSize * camZoom;

        for (var y = visMinY; y <= visMaxY; y++)
        {
            for (var x = visMinX; x <= visMaxX; x++)
            {
                var rect = new Rect2(x * TileSize, y * TileSize, TileSize, TileSize);
                var ground = TerrainColorRaylib(TerrainKind.Grass, x, y);
                if (_view?.Terrain is { } terrain && terrain.InBounds(new GridPosition(x, y)))
                {
                    ground = TerrainColorRaylib(terrain[x, y].Terrain, x, y);
                }

                DrawRect(rect, ground);
                if (screenTilePx >= 12f && _view?.Terrain is { } detailMap
                    && detailMap.InBounds(new GridPosition(x, y)))
                {
                    DrawTerrainSpeckle(detailMap[x, y].Terrain, x, y, rect);
                }
            }
        }

        // Grid only in view (full 1000² grid lines would dominate frame time).
        if (screenTilePx >= 10f)
        {
            var grid = new Color(0.22f, 0.28f, 0.24f, 0.55f);
            var gridTop = visMinY * TileSize;
            var gridBottom = (visMaxY + 1) * TileSize;
            var gridLeft = visMinX * TileSize;
            var gridRight = (visMaxX + 1) * TileSize;
            for (var x = visMinX; x <= visMaxX + 1; x++)
            {
                var px = x * TileSize;
                DrawLine(new Vector2(px, gridTop), new Vector2(px, gridBottom), grid, 1f);
            }

            for (var y = visMinY; y <= visMaxY + 1; y++)
            {
                var py = y * TileSize;
                DrawLine(new Vector2(gridLeft, py), new Vector2(gridRight, py), grid, 1f);
            }
        }

        // World AABB border (visible when any edge is in view).
        var border = new Color(0.85f, 0.78f, 0.45f, 0.95f);
        var worldRect = new Rect2(0, 0, mapW * TileSize, mapH * TileSize);
        DrawRect(worldRect, border, filled: false, width: Math.Max(2f, 3f / camZoom));

        if (_view is null)
        {
            return;
        }

        // Seeded deposits — Raylib-style icons / bold patches (culled).
        if (_view.Terrain is { } map)
        {
            for (var y = visMinY; y <= visMaxY; y++)
            {
                for (var x = visMinX; x <= visMaxX; x++)
                {
                    if (x < 0 || y < 0 || x >= mapW || y >= mapH)
                    {
                        continue;
                    }

                    var deposit = map[x, y].Deposit;
                    if (deposit == DepositKind.None)
                    {
                        continue;
                    }

                    var tile = new GridPosition(x, y);
                    // Belts/buildings own the tile — don't paint deposit icons as fake cargo.
                    if (_view.Belts.ContainsKey(tile)
                        || _view.CoreTiles.Contains(tile)
                        || _view.IsBuildingTile(tile))
                    {
                        continue;
                    }

                    DrawDepositMarker(deposit, x, y, screenTilePx);
                }
            }
        }

        // Core is a node in the Buildings layer (see AddCoreVisual) so belt items slide under it.

        // Deposit tint under miners stays soft; the building pad is the readable frame.
        foreach (var miner in _view.Miners)
        {
            var c = ItemPalette.ColorFor(miner.OutputItemId);
            var deposit = new Color(c.R, c.G, c.B, 0.4f);
            foreach (var tile in miner.OccupiedTiles())
            {
                DrawRect(new Rect2(tile.X * TileSize, tile.Y * TileSize, TileSize, TileSize), deposit);
            }
        }

        if (_hover is { } hover
            && hover.X >= 0 && hover.Y >= 0
            && hover.X < ActiveMapWidth && hover.Y < ActiveMapHeight
            && _tool != BuildTool.Cursor)
        {
            DrawGhost(hover);
        }
    }

    /// <summary>Raylib WorldGraphics.TerrainColor — richer base hues + micro hash variation.</summary>
    private static Color TerrainColorRaylib(TerrainKind kind, int worldX, int worldY)
    {
        var baseColor = kind switch
        {
            TerrainKind.Grass => new Color(46 / 255f, 78 / 255f, 54 / 255f),
            TerrainKind.Soil => new Color(108 / 255f, 88 / 255f, 58 / 255f),
            TerrainKind.Stone => new Color(92 / 255f, 98 / 255f, 96 / 255f),
            TerrainKind.Water => new Color(42 / 255f, 78 / 255f, 98 / 255f),
            _ => new Color(0.1f, 0.12f, 0.1f)
        };
        var n1 = Hash01(worldX, worldY, 917);
        var n2 = Hash01(worldX * 3 + 7, worldY * 5 - 3, 421);
        var delta = ((n1 - 0.5f) * 22f + (n2 - 0.5f) * 10f) / 255f;
        if (kind is TerrainKind.Grass or TerrainKind.Soil && ((worldX + worldY) & 1) == 0)
        {
            delta -= 4f / 255f;
        }

        return new Color(
            Math.Clamp(baseColor.R + delta, 0f, 1f),
            Math.Clamp(baseColor.G + delta, 0f, 1f),
            Math.Clamp(baseColor.B + (kind == TerrainKind.Water ? delta * 0.5f : delta), 0f, 1f));
    }

    private static float Hash01(int x, int y, int salt)
    {
        unchecked
        {
            var v = (uint)(x * 374761393 + y * 668265263 + salt * 1442695041);
            v = (v ^ (v >> 13)) * 1274126177u;
            return (v ^ (v >> 16)) / (float)uint.MaxValue;
        }
    }

    private void DrawTerrainSpeckle(TerrainKind kind, int worldX, int worldY, Rect2 rect)
    {
        if (kind == TerrainKind.Water)
        {
            return;
        }

        var speck = kind switch
        {
            TerrainKind.Grass => new Color(36 / 255f, 62 / 255f, 42 / 255f, 0.35f),
            TerrainKind.Soil => new Color(86 / 255f, 68 / 255f, 42 / 255f, 0.35f),
            _ => new Color(70 / 255f, 74 / 255f, 72 / 255f, 0.3f)
        };
        var hx = Hash01(worldX, worldY, 55);
        var hy = Hash01(worldX, worldY, 77);
        var sx = rect.Position.X + 4f + hx * (TileSize - 10f);
        var sy = rect.Position.Y + 4f + hy * (TileSize - 10f);
        DrawRect(new Rect2(sx, sy, Math.Max(2f, TileSize / 14f), Math.Max(2f, TileSize / 14f)), speck);
    }

    private void DrawDepositMarker(DepositKind deposit, int x, int y, float screenTilePx)
    {
        var rect = new Rect2(x * TileSize, y * TileSize, TileSize, TileSize);
        var pad = new Color(0.2f, 0.18f, 0.14f, 0.55f);
        DrawRect(rect, pad);

        var itemId = TerrainMap.ItemIdForDeposit(deposit);
        var fill = ItemPalette.ColorFor(itemId);
        var tex = deposit switch
        {
            DepositKind.Iron => _oreTex,
            DepositKind.Copper => _copperOreTex,
            DepositKind.Coal => _coalTex,
            DepositKind.Lead => _leadOreTex,
            DepositKind.Titanium => _titaniumOreTex,
            _ => null
        };

        if (screenTilePx >= 14f && tex is not null)
        {
            var inset = TileSize * 0.18f;
            // White silhouette PNGs — tint like Raylib GameIcons.TryDraw + UiTheme.ItemColor.
            DrawTextureRect(
                tex,
                new Rect2(
                    x * TileSize + inset,
                    y * TileSize + inset,
                    TileSize - inset * 2f,
                    TileSize - inset * 2f),
                false,
                fill);
        }
        else
        {
            // Raylib fallback circles when zoomed out.
            var cx = (x + 0.5f) * TileSize;
            var cy = (y + 0.5f) * TileSize;
            DrawCircle(new Vector2(cx - TileSize * 0.15f, cy - TileSize * 0.1f), TileSize * 0.18f, fill);
            DrawCircle(new Vector2(cx + TileSize * 0.12f, cy + TileSize * 0.1f), TileSize * 0.22f, fill);
        }
    }

    private void DrawGhost(GridPosition hover)
    {
        if (_view is null)
        {
            return;
        }

        var size = _tool switch
        {
            BuildTool.Miner or BuildTool.MinerAdvanced => Miner.Size,
            BuildTool.Smelter => Crafter.Size,
            BuildTool.Assembler => Crafter.Size,
            BuildTool.Generator => Generator.Size,
            BuildTool.PowerNode => 1,
            BuildTool.PowerNodeT2 => 2,
            BuildTool.Extractor => Extractor.Size,
            _ => 1
        };

        var ok = size == 1
            ? _view.CanOccupy(hover)
            : _view.CanOccupyFootprint(hover, size)
              || (_tool is BuildTool.Miner or BuildTool.MinerAdvanced && _view.Miners.Count == 1)
              || (_tool == BuildTool.Smelter && _view.Smelters.Count == 1)
              || (_tool == BuildTool.Assembler && _view.Assemblers.Count == 1)
              || (_tool == BuildTool.Generator && _view.Generators.Count == 1);
        // Cells already taken by a pending plan are refused like occupied ones (sites count as buildings).
        ok = ok && !FootprintPlanned(hover, size);

        var ghost = ok
            ? new Color(0.35f, 0.85f, 0.55f, 0.35f)
            : new Color(0.9f, 0.25f, 0.2f, 0.35f);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var rect = new Rect2((hover.X + x) * TileSize, (hover.Y + y) * TileSize, TileSize, TileSize);
                DrawRect(rect, ghost);
                DrawRect(rect, new Color(0.9f, 0.95f, 0.85f, 0.9f), false, 2f);
            }
        }

        if (_tool is BuildTool.Belt or BuildTool.BeltFast or BuildTool.BeltExpress
            or BuildTool.Junction or BuildTool.Splitter
            or BuildTool.Sorter or BuildTool.Bridge or BuildTool.Extractor)
        {
            DrawDirectionHint(hover, _placeDir);
        }

        if (_tool == BuildTool.Bridge)
        {
            DrawBridgeGhost(hover);
        }
    }

    private bool FootprintPlanned(GridPosition origin, int size)
    {
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (_view!.IsPlannedTile(new GridPosition(origin.X + x, origin.Y + y)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void DrawBridgeGhost(GridPosition entry)
    {
        if (_view is null)
        {
            return;
        }

        for (var span = BeltGridCell.MinBridgeSpan; span <= BeltGridCell.MaxBridgeSpan; span++)
        {
            var exit = entry.Step(_placeDir, span);
            if (!_view.CanOccupy(entry) || !_view.CanOccupy(exit))
            {
                continue;
            }

            if (_view.Belts.ContainsKey(entry) || _view.Belts.ContainsKey(exit))
            {
                continue;
            }

            // Same exit as the simulation picks; a planned end makes the plan refused: show it red.
            var planned = _view.IsPlannedTile(entry) || _view.IsPlannedTile(exit);
            var mid = planned ? new Color(0.9f, 0.25f, 0.2f, 0.28f) : new Color(0.55f, 0.7f, 0.95f, 0.28f);
            var tip = planned ? new Color(0.9f, 0.25f, 0.2f, 0.55f) : new Color(0.7f, 0.85f, 1f, 0.55f);
            DrawRect(new Rect2(entry.X * TileSize, entry.Y * TileSize, TileSize, TileSize), tip);
            DrawRect(new Rect2(exit.X * TileSize, exit.Y * TileSize, TileSize, TileSize), tip);
            var a = CellCenter(entry);
            var b = CellCenter(exit);
            DrawLine(a, b, mid, TileSize * MindustryBeltVisual.BridgeThicknessScale);
            return;
        }
    }

    private void DrawDirectionHint(GridPosition cell, Direction dir)
    {
        var center = CellCenter(cell);
        var (dx, dy) = DirectionMath.ToOffset(dir);
        var tip = center + new Vector2(dx, dy) * (TileSize * 0.32f);
        DrawLine(center, tip, new Color(0.95f, 0.95f, 0.7f, 0.95f), 3f);
        DrawCircle(tip, 4f, new Color(0.95f, 0.95f, 0.7f, 0.95f));
    }
}
