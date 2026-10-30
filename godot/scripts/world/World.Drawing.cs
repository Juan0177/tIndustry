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

        if (_view is not null && _draggingRemove && _removeMoved && _hover is { } cursor)
        {
            DrawRemovalRectangle(canvas, cursor);
        }
    }

    /// <summary>The right drag's removal rectangle (<see cref="RemovalArea.Rect"/>): a faint fill and a red outline.</summary>
    private void DrawRemovalRectangle(CanvasItem canvas, GridPosition cursor)
    {
        var (min, max) = RemovalArea.Rect(_removeStart, cursor);
        var rect = new Rect2(min.X * TileSize, min.Y * TileSize, (max.X - min.X + 1) * TileSize, (max.Y - min.Y + 1) * TileSize);
        canvas.DrawRect(rect, UiColors.SmontaggioArea);
        if (UiColors.SottoSegno.A > 0f)
        {
            canvas.DrawRect(rect, UiColors.SottoSegno, filled: false, width: 6f);
        }

        canvas.DrawRect(rect, UiColors.Smontaggio, filled: false, width: 3f);
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
    private readonly long[] _buildingTilesKey = new long[18];
    private readonly long[] _structureKey = new long[18];
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

        foreach (var battery in view.Batteries)
        {
            _buildingTiles.Add(battery.Position);
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
        max = 0;
        foreach (var b in view.Batteries)
        {
            max = Math.Max(max, b.Id);
        }

        Add(view.Batteries.Count, max);
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

    /// <summary>
    /// The ghost of the line from the drag's anchor (or of the block under the cursor without a drag): one
    /// <see cref="PlacementPreview"/> per cell, red where the line foresees a refusal, the bridge's span for a bridge.
    /// </summary>
    private void DrawGhost(CanvasItem canvas, GridPosition hover)
    {
        if (_view is null)
        {
            return;
        }

        var size = IsBelt(_tool) ? 1 : GhostSize();
        foreach (var placement in PlanLine(hover))
        {
            if (placement.Command is not { } command)
            {
                continue; // spanned by a bridge
            }

            var spec = GhostSpec(command);
            var valid = placement.Refusal is null;
            var rect = new Rect2(placement.Cell.X * TileSize, placement.Cell.Y * TileSize, size * TileSize, size * TileSize);
            PlacementPreview.Draw(canvas, spec.StructureId, rect, placement.Direction, turnSprite: spec.Kind is null,
                arrow: PlacementPreview.HasDirection(spec.Kind), valid);
            if (spec.Kind == BuildingKind.Bridge)
            {
                DrawBridgeGhost(canvas, spec, refused: !valid);
            }
        }

        if (IsCoreTool(_tool))
        {
            DrawCoreUpgradeTaken(canvas);
        }
    }

    /// <summary>
    /// The Core block's ghost stands where the growth rule puts it (<see cref="LineTo"/>); each of the new cells that is
    /// taken (a block, a belt, a site, a plan) or not buildable is marked on its own as a refused cell, so the player sees
    /// what to clear.
    /// </summary>
    private void DrawCoreUpgradeTaken(CanvasItem canvas)
    {
        foreach (var tile in _view!.CoreUpgradeNewTiles())
        {
            if (_view.CanOccupy(tile) && !_view.IsPlannedTile(tile))
            {
                continue;
            }

            var rect = new Rect2(tile.X * TileSize, tile.Y * TileSize, TileSize, TileSize);
            PlacementPreview.Draw(canvas, null, rect, Direction.East, turnSprite: false, arrow: false, valid: false);
        }
    }

    /// <summary>What a line command builds: a crossing shows the junction when the simulation would make one.</summary>
    private BuildSpec GhostSpec(SimCommand command) => command switch
    {
        PlaceBelt { Crossing: true } crossing when _view!.BeltCrossingAt(crossing.Position, crossing.Direction) == BeltCrossing.Junction
            => BuildSpec.FromCommand(new PlaceBuilding(LocalPlayer, BuildingKind.Junction, crossing.Position, crossing.Direction)),
        PlaceBelt belt => BuildSpec.FromCommand(belt),
        _ => BuildSpec.FromCommand((PlaceBuilding)command)
    };

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
        BuildTool.Battery => BuildingKind.Battery,
        BuildTool.Container => BuildingKind.Container,
        BuildTool.Vault => BuildingKind.Vault,
        BuildTool.CoreFoundation => BuildingKind.CoreFoundation,
        BuildTool.CoreNucleus => BuildingKind.CoreNucleus,
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
        BuildTool.Container or BuildTool.Vault => StorageBlock.SizeOf(KindOf(_tool)),
        BuildTool.CoreFoundation or BuildTool.CoreNucleus when _view is not null
            => _view.Content.CoreTiers[BuildSpec.CoreTierOf(KindOf(_tool))].Size,
        _ => 1
    };

    /// <summary>
    /// Why the ghost at <paramref name="hover"/> is refused, null when the block can go there: the refusal
    /// <see cref="PlacementLine"/> foresees for the line's last cell (the block itself for a block that is not laid in
    /// a line), from the simulation's own <see cref="Factory.CanPlace"/> and the pending plans.
    /// </summary>
    private RefusalCode? GhostRefusal(GridPosition hover) =>
        _view is null || _tool == BuildTool.Cursor ? null : PlanLine(hover)[^1].Refusal;

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

    /// <summary>
    /// The bridge's span to the exit the simulation would pick (<see cref="Factory.Footprint"/>), in the refused colours
    /// when <paramref name="refused"/>; nothing when the bridge has no exit.
    /// </summary>
    private void DrawBridgeGhost(CanvasItem canvas, BuildSpec spec, bool refused)
    {
        if (_view is null || _view.Footprint(spec) is not [var entry, var exit])
        {
            return;
        }

        var mid = refused ? UiColors.PonteRifiutatoCampata : UiColors.PonteCampata;
        var tip = refused ? UiColors.PonteRifiutatoEstremi : UiColors.PonteEstremi;
        canvas.DrawRect(new Rect2(entry.X * TileSize, entry.Y * TileSize, TileSize, TileSize), tip);
        canvas.DrawRect(new Rect2(exit.X * TileSize, exit.Y * TileSize, TileSize, TileSize), tip);
        canvas.DrawLine(CellCenter(entry), CellCenter(exit), mid, TileSize * MindustryBeltVisual.BridgeThicknessScale);
    }

    // ---------------------------------------------------------------- power: lasers, selected node

    /// <summary>Above the buildings and their own child layers (≤ BuildingsZ + 6), under the construction layer.</summary>
    private const int LaserZ = BuildingsZ + 7;

    /// <summary>Laser width, in world pixels (a tenth of a cell).</summary>
    private const float LaserWidth = TileSize * 0.1f;

    private Node2D? _laserLayer;

    // What the laser layer last drew: it is redrawn only when one of these changes.
    private (object? View, bool InPlay, long? Node, int Structures, int Links, Color Colour)? _laserKey;

    /// <summary>Queues a redraw of the laser layer only when something it shows has changed.</summary>
    private void QueueLaserRedraw()
    {
        var key = (_view as object, GameInPlay, GameInPlay ? _selectedNode : null, _structureCount,
            _view?.PowerLinks.Count ?? 0, UiColors.Energia);
        if (_laserKey != key)
        {
            _laserKey = key;
            _laserLayer?.QueueRedraw();
        }
    }

    /// <summary>The power node selected with the cursor (its range and lasers are shown; a click links or unlinks); null for none.</summary>
    private long? _selectedNode;

    // Square of every power block by id, for the lasers' ends: rebuilt when the structures change (ids are never reused).
    private readonly Dictionary<long, Rect2> _powerRects = [];
    private IGameView? _powerRectsView;
    private int _powerRectsStructures = -1;

    // Power blocks the selected node reaches, from the simulation's own range rule: rebuilt when the selection, the
    // structures or the lasers change.
    private readonly List<PowerBlock> _inRange = [];
    private (long Node, int Structures, int Links)? _inRangeKey;

    /// <summary>Lasers and the selected node, drawn on their own world layer above the blocks.</summary>
    private void EnsureLaserLayer()
    {
        _laserLayer = new Node2D { Name = "Laser", ZIndex = LaserZ };
        _laserLayer.Draw += () => DrawPower(_laserLayer);
        AddChild(_laserLayer);
    }

    private Dictionary<long, Rect2> PowerRects()
    {
        var view = _view!;
        var stale = !ReferenceEquals(view, _powerRectsView) || _structureCount != _powerRectsStructures;
        if (!stale)
        {
            foreach (var (a, b) in view.PowerLinks)
            {
                if (!_powerRects.ContainsKey(a) || !_powerRects.ContainsKey(b))
                {
                    stale = true; // a block replaced another between two frames
                    break;
                }
            }
        }

        if (stale)
        {
            _powerRectsView = view;
            _powerRectsStructures = _structureCount;
            _powerRects.Clear();
            foreach (var block in view.PowerBlocks)
            {
                _powerRects[block.Id] = BlockRect(block);
            }
        }

        return _powerRects;
    }

    private static Rect2 BlockRect(PowerBlock block) =>
        new(block.Origin.X * TileSize, block.Origin.Y * TileSize, block.Size * TileSize, block.Size * TileSize);

    /// <summary>
    /// Every laser (<see cref="IGameView.PowerLinks"/>) from block to block, the selected node's ones wider; with a node
    /// selected, its range and the blocks it reaches (linked ones bright, the others faint).
    /// </summary>
    private void DrawPower(CanvasItem canvas)
    {
        if (_view is null)
        {
            return;
        }

        var rects = PowerRects();
        var selected = GameInPlay ? _selectedNode : null;
        var linkSet = selected is null ? null : new HashSet<(long, long)>(_view.PowerLinks);
        if (selected is { } node && rects.TryGetValue(node, out var nodeRect) && _view.PowerNodeLinksOf(node) is { } links)
        {
            DrawNodeRange(canvas, nodeRect, links.Range);
            foreach (var block in BlocksInRange(node))
            {
                var linked = linkSet!.Contains((Math.Min(node, block.Id), Math.Max(node, block.Id)));
                canvas.DrawRect(BlockRect(block).Grow(-2f), UiColors.Energia with { A = linked ? 0.95f : 0.4f }, filled: false,
                    width: 3f);
            }

            canvas.DrawRect(nodeRect, UiColors.Energia with { A = 0.25f });
            canvas.DrawRect(nodeRect.Grow(-2f), UiColors.Energia, filled: false, width: 3f);
        }

        foreach (var (a, b) in _view.PowerLinks)
        {
            if (rects.TryGetValue(a, out var ra) && rects.TryGetValue(b, out var rb))
            {
                DrawLaser(canvas, ra, rb, wide: selected == a || selected == b);
            }
        }
    }

    /// <summary>Blocks in reach of the selected node, asked to the simulation once per change.</summary>
    private List<PowerBlock> BlocksInRange(long node)
    {
        var key = (node, _structureCount, _view!.PowerLinks.Count);
        if (_inRangeKey != key)
        {
            _inRangeKey = key;
            _inRange.Clear();
            foreach (var block in _view.PowerBlocks)
            {
                if (block.Id != node && _view.PowerLinkInRange(node, block.Id))
                {
                    _inRange.Add(block);
                }
            }
        }

        return _inRange;
    }

    /// <summary>
    /// A laser between two blocks: from inside each square's edge, a dark under-stroke where the colours ask for one
    /// (high contrast), the energy line and a dot at each end.
    /// </summary>
    private static void DrawLaser(CanvasItem canvas, Rect2 from, Rect2 to, bool wide)
    {
        var start = LaserEnd(from, to.GetCenter());
        var end = LaserEnd(to, from.GetCenter());
        var width = wide ? LaserWidth * 1.6f : LaserWidth;
        if (UiColors.SottoSegno.A > 0f)
        {
            canvas.DrawLine(start, end, UiColors.SottoSegno, width + 4f);
        }

        canvas.DrawLine(start, end, UiColors.Energia with { A = 0.9f }, width);
        canvas.DrawCircle(start, width * 0.9f, UiColors.Energia);
        canvas.DrawCircle(end, width * 0.9f, UiColors.Energia);
    }

    /// <summary>Where a laser leaves <paramref name="block"/> towards <paramref name="toward"/>: well inside its edge.</summary>
    private static Vector2 LaserEnd(Rect2 block, Vector2 toward)
    {
        var center = block.GetCenter();
        var d = toward - center;
        var half = block.Size * 0.5f * 0.6f;
        if (Math.Abs(d.X) < 0.001f && Math.Abs(d.Y) < 0.001f)
        {
            return center;
        }

        var t = Math.Min(
            Math.Abs(d.X) < 0.001f ? float.MaxValue : half.X / Math.Abs(d.X),
            Math.Abs(d.Y) < 0.001f ? float.MaxValue : half.Y / Math.Abs(d.Y));
        return center + d * Math.Min(1f, t);
    }

    /// <summary>
    /// The selected node's range: its square grown by the range plus the cell at the end, corners rounded by the same
    /// radius (the reach is measured between the squares' edges). The blocks it reaches are outlined exactly.
    /// </summary>
    private static void DrawNodeRange(CanvasItem canvas, Rect2 node, int range)
    {
        var r = (range + 1) * TileSize;
        var p = node.Position;
        var q = node.End;
        const int arcPoints = 12;
        var corners = new (Vector2 Center, float From)[]
        {
            (new Vector2(q.X, p.Y), -Mathf.Pi / 2), (new Vector2(q.X, q.Y), 0f),
            (new Vector2(p.X, q.Y), Mathf.Pi / 2), (new Vector2(p.X, p.Y), Mathf.Pi)
        };
        var outline = new Vector2[corners.Length * (arcPoints + 1) + 1];
        var i = 0;
        foreach (var (center, from) in corners)
        {
            for (var k = 0; k <= arcPoints; k++)
            {
                var angle = from + Mathf.Pi / 2 * k / arcPoints;
                outline[i++] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
            }
        }

        outline[i] = outline[0];
        canvas.DrawColoredPolygon(outline[..^1], UiColors.Energia with { A = 0.06f });
        canvas.DrawPolyline(outline, UiColors.Energia with { A = 0.7f }, 3f);
    }
}
