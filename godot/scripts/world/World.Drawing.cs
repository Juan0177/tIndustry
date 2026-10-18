using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Immediate-mode drawing over the terrain chunks (<see cref="TerrainChunks"/>): grid, map border, deposits; and on
/// the placement overlay above the world: the placement ghost (<see cref="PlacementPreview"/>) and the bridge span.
/// </summary>
public partial class World
{
    /// <summary>Canvas layer of the local plans (<see cref="ConstructionLayer"/>): above the world (0).</summary>
    internal const int PlansLayer = 1;

    /// <summary>Canvas layer of the placement overlay: above the plans, under the HUD (20).</summary>
    internal const int PlacementLayer = 2;

    private Node2D? _placementOverlay;

    /// <summary>
    /// The placement ghost is drawn on its own canvas layer that follows the camera: aligned with the world cells,
    /// but outside the world's canvas, so the night's <see cref="CanvasModulate"/> (and the reduced render scale) never
    /// darken or blur it, and it shows over the blocks it would collide with.
    /// </summary>
    private void EnsurePlacementOverlay()
    {
        var layer = new CanvasLayer { Name = "Piazzamento", Layer = PlacementLayer, FollowViewportEnabled = true };
        _placementOverlay = new Node2D { Name = "Fantasma" };
        _placementOverlay.Draw += () => DrawPlacementOverlay(_placementOverlay);
        layer.AddChild(_placementOverlay);
        AddChild(layer);
    }

    private void DrawPlacementOverlay(CanvasItem canvas)
    {
        if (_view is not null
            && _hover is { } hover
            && hover.X >= 0 && hover.Y >= 0
            && hover.X < ActiveMapWidth && hover.Y < ActiveMapHeight
            && _tool != BuildTool.Cursor)
        {
            DrawGhost(canvas, hover);
        }
    }

    public override void _Draw()
    {
        GetVisibleTileRange(out var visMinX, out var visMinY, out var visMaxX, out var visMaxY);
        var mapW = ActiveMapWidth;
        var mapH = ActiveMapHeight;
        var camZoom = HasNode("Camera") ? Math.Max(0.01f, GetNode<Camera2D>("Camera").Zoom.X) : 1f;
        var screenTilePx = TileSize * camZoom;

        // Grid only in view (full 1000² grid lines would dominate frame time). Over the terrain art it is hidden,
        // and drawn very faint only while a build tool is selected (placing by cells).
        var artLod = screenTilePx >= TerrainChunks.ArtMinCellPx;
        Color? gridColor = artLod
            ? (_tool != BuildTool.Cursor ? new Color(0f, 0f, 0f, 0.14f) : null)
            : (screenTilePx >= 10f ? new Color(0.22f, 0.28f, 0.24f, 0.55f) : null);
        if (gridColor is { } grid)
        {
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

        // Seeded deposits at the colour level of detail: the ore icon per cell from DepositIconMinPx, ore-coloured
        // runs below (culled). From TerrainChunks.ArtMinCellPx the terrain art draws them as pebbles; a cell whose
        // art chunk is still being composed keeps its icon meanwhile.
        if (_view.Terrain is { } map)
        {
            var buildingTiles = BuildingTilesThisFrame();
            var icons = screenTilePx >= DepositIconMinPx;
            for (var y = visMinY; y <= visMaxY; y++)
            {
                // Zoomed out: consecutive cells of one ore on a row are one rect (thousands of cells at minimum zoom).
                var runStart = -1;
                var runKind = DepositKind.None;
                for (var x = visMinX; x <= visMaxX + 1; x++)
                {
                    var deposit = x <= visMaxX ? map[x, y].Deposit : DepositKind.None;
                    if (deposit != DepositKind.None && artLod && _terrainChunks?.HasArtAt(x, y) == true)
                    {
                        deposit = DepositKind.None;
                    }

                    if (deposit != DepositKind.None)
                    {
                        var tile = new GridPosition(x, y);
                        // Belts/buildings own the tile — don't paint deposit icons as fake cargo.
                        if (_view.Belts.ContainsKey(tile)
                            || _view.CoreTiles.Contains(tile)
                            || buildingTiles.Contains(tile))
                        {
                            deposit = DepositKind.None;
                        }
                    }

                    if (icons)
                    {
                        if (deposit != DepositKind.None)
                        {
                            DrawDepositIcon(deposit, x, y);
                        }

                        continue;
                    }

                    if (deposit == runKind)
                    {
                        continue;
                    }

                    if (runKind != DepositKind.None)
                    {
                        DrawDepositRun(runKind, runStart, x - 1, y);
                    }

                    runKind = deposit;
                    runStart = x;
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
    }

    private readonly HashSet<GridPosition> _buildingTiles = [];
    private readonly long[] _buildingTilesKey = new long[16];
    private readonly long[] _structureKey = new long[16];
    private IGameView? _buildingTilesView;

    /// <summary>
    /// Cells under buildings and construction sites (the same cells as <see cref="IGameView.IsBuildingTile"/>, which
    /// scans every building per call), rebuilt only when the structures change: count and highest id of each list
    /// differ as soon as one is added or removed (ids are never reused within a game).
    /// </summary>
    private HashSet<GridPosition> BuildingTilesThisFrame()
    {
        var view = _view!;
        FillStructureKey(view, _structureKey);
        if (ReferenceEquals(view, _buildingTilesView) && _structureKey.AsSpan().SequenceEqual(_buildingTilesKey))
        {
            return _buildingTiles;
        }

        _buildingTilesView = view;
        _structureKey.CopyTo(_buildingTilesKey, 0);
        _buildingTiles.Clear();
        foreach (var site in view.Sites)
        {
            _buildingTiles.UnionWith(site.Tiles);
        }

        foreach (var miner in view.Miners)
        {
            _buildingTiles.UnionWith(miner.OccupiedTiles());
        }

        foreach (var crafter in view.Smelters.Concat(view.Assemblers))
        {
            _buildingTiles.UnionWith(crafter.OccupiedTiles());
        }

        foreach (var generator in view.Generators)
        {
            _buildingTiles.UnionWith(generator.OccupiedTiles());
        }

        foreach (var extractor in view.Extractors)
        {
            _buildingTiles.UnionWith(extractor.OccupiedTiles());
        }

        foreach (var node in view.PowerNodes)
        {
            _buildingTiles.UnionWith(node.OccupiedTiles());
        }

        foreach (var lamp in view.Lamps)
        {
            _buildingTiles.Add(lamp.Position);
        }

        return _buildingTiles;
    }

    private static void FillStructureKey(IGameView view, long[] key)
    {
        var i = 0;
        void Add(int count, long maxId)
        {
            key[i++] = count;
            key[i++] = maxId;
        }

        long max = 0;
        foreach (var s in view.Sites)
        {
            max = Math.Max(max, s.Id);
        }

        Add(view.Sites.Count, max);
        max = 0;
        foreach (var m in view.Miners)
        {
            max = Math.Max(max, m.Id);
        }

        Add(view.Miners.Count, max);
        max = 0;
        foreach (var c in view.Smelters)
        {
            max = Math.Max(max, c.Id);
        }

        Add(view.Smelters.Count, max);
        max = 0;
        foreach (var c in view.Assemblers)
        {
            max = Math.Max(max, c.Id);
        }

        Add(view.Assemblers.Count, max);
        max = 0;
        foreach (var g in view.Generators)
        {
            max = Math.Max(max, g.Id);
        }

        Add(view.Generators.Count, max);
        max = 0;
        foreach (var e in view.Extractors)
        {
            max = Math.Max(max, e.Id);
        }

        Add(view.Extractors.Count, max);
        max = 0;
        foreach (var n in view.PowerNodes)
        {
            max = Math.Max(max, n.Id);
        }

        Add(view.PowerNodes.Count, max);
        max = 0;
        foreach (var l in view.Lamps)
        {
            max = Math.Max(max, l.Id);
        }

        Add(view.Lamps.Count, max);
    }

    /// <summary>Cell size on screen from which deposits show their ore icon instead of plain colour.</summary>
    private const float DepositIconMinPx = 14f;

    private void DrawDepositIcon(DepositKind deposit, int x, int y)
    {
        var rect = new Rect2(x * TileSize, y * TileSize, TileSize, TileSize);
        DrawRect(rect, new Color(0.2f, 0.18f, 0.14f, 0.55f));
        var itemId = TerrainMap.ItemIdForDeposit(deposit);
        if (Sprites.TryFor(itemId) is { } tex)
        {
            // The ore item sprite in its own colours, at the world art scale (32 px icon = one tile), just below
            // the zoom where the terrain art takes over.
            DrawTextureRect(tex, rect, false);
        }
        else
        {
            DrawDepositRun(deposit, x, x, y);
        }
    }

    /// <summary>Cells <paramref name="x0"/>..<paramref name="x1"/> of row <paramref name="y"/> in the ore colour.</summary>
    private void DrawDepositRun(DepositKind deposit, int x0, int x1, int y)
    {
        var fill = ItemPalette.ColorFor(TerrainMap.ItemIdForDeposit(deposit));
        var inset = TileSize * 0.12f;
        DrawRect(
            new Rect2(x0 * TileSize, y * TileSize + inset, (x1 - x0 + 1) * TileSize, TileSize - 2 * inset),
            fill with { A = 0.9f });
    }

    private void DrawGhost(CanvasItem canvas, GridPosition hover)
    {
        if (_view is null)
        {
            return;
        }

        var size = GhostSize();
        var ok = GhostRefusal(hover) is null;

        // A belt touching the Core is planned facing it (as the simulation does): show that direction.
        var belt = _tool is BuildTool.Belt or BuildTool.BeltFast or BuildTool.BeltExpress;
        var direction = belt && CoreStockSink.PreferDirectionIntoCore(hover, _view.CoreTiles) is { } intoCore
            ? intoCore
            : _placeDir;
        var rect = new Rect2(hover.X * TileSize, hover.Y * TileSize, size * TileSize, size * TileSize);
        PlacementPreview.Draw(canvas, Hud.StructureIdFor(ToHudTool(_tool)), rect, direction, turnSprite: belt,
            arrow: PlacementPreview.HasDirection(KindOf(_tool)), valid: ok);

        if (_tool == BuildTool.Bridge)
        {
            DrawBridgeGhost(canvas, hover);
        }
    }

    /// <summary>Building kind the tool places (null for belts and the cursor).</summary>
    private static BuildingKind? KindOf(BuildTool tool) => tool switch
    {
        BuildTool.Miner => BuildingKind.Miner,
        BuildTool.MinerAdvanced => BuildingKind.MinerAdvanced,
        BuildTool.Smelter => BuildingKind.Smelter,
        BuildTool.Assembler => BuildingKind.Assembler,
        BuildTool.Junction => BuildingKind.Junction,
        BuildTool.Splitter => BuildingKind.Splitter,
        BuildTool.Generator => BuildingKind.Generator,
        BuildTool.Sorter => BuildingKind.Sorter,
        BuildTool.Bridge => BuildingKind.Bridge,
        BuildTool.Extractor => BuildingKind.Extractor,
        BuildTool.PowerNode => BuildingKind.PowerNode,
        BuildTool.PowerNodeT2 => BuildingKind.PowerNodeT2,
        BuildTool.Lamp => BuildingKind.Lamp,
        _ => null
    };

    /// <summary>Footprint edge of the block the current tool places.</summary>
    private int GhostSize() => _tool switch
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

    /// <summary>
    /// Why the ghost at <paramref name="hover"/> is refused, null when the block can go there: the simulation's own
    /// <see cref="Factory.CanPlace"/> on the spec the tool would send (walls, deep water, a miner too weak for the ore,
    /// a taken cell…), plus a cell taken by a pending plan, which the plan queue refuses the same way.
    /// </summary>
    private RefusalCode? GhostRefusal(GridPosition hover)
    {
        if (_view is null || _tool == BuildTool.Cursor)
        {
            return null;
        }

        var spec = PlaceCommandAt(hover) switch
        {
            PlaceBelt belt => BuildSpec.FromCommand(belt),
            PlaceBuilding building => BuildSpec.FromCommand(building),
            _ => null
        };
        if (spec is null)
        {
            return null;
        }

        if (!_view.CanPlace(spec, out var reason))
        {
            return reason ?? RefusalCode.Occupied;
        }

        return FootprintPlanned(hover, GhostSize()) ? RefusalCode.Occupied : null;
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

    private void DrawBridgeGhost(CanvasItem canvas, GridPosition entry)
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
            var mid = planned ? UiColors.PonteRifiutatoCampata : UiColors.PonteCampata;
            var tip = planned ? UiColors.PonteRifiutatoEstremi : UiColors.PonteEstremi;
            canvas.DrawRect(new Rect2(entry.X * TileSize, entry.Y * TileSize, TileSize, TileSize), tip);
            canvas.DrawRect(new Rect2(exit.X * TileSize, exit.Y * TileSize, TileSize, TileSize), tip);
            var a = CellCenter(entry);
            var b = CellCenter(exit);
            canvas.DrawLine(a, b, mid, TileSize * MindustryBeltVisual.BridgeThicknessScale);
            return;
        }
    }
}
