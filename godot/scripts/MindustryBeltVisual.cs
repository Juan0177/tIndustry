using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Builds Mindustry-style belt visuals from a cell path or a placeable <see cref="BeltGrid"/>:
/// straight strips + platform corner tiles, shared scroll clock.
/// Bridge: full 1×1 ends + thin (~0.78) mid-span (decision 14).
/// </summary>
public partial class MindustryBeltVisual : Node2D
{
    public const float BridgeThicknessScale = 0.78f;

    private readonly List<ScrollingBeltStrip> _strips = [];
    private readonly List<BeltCornerTile> _corners = [];
    private float _scroll;
    private float _rateTilesPerSecond = 0.5f;

    public float ScrollTiles => _scroll;
    public float RateTilesPerSecond => _rateTilesPerSecond;

    public void Configure(IReadOnlyList<GridPosition> path, float rateItemsPerSecond, int tileSize)
    {
        ClearVisuals();
        _rateTilesPerSecond = Math.Max(0.05f, rateItemsPerSecond);
        _scroll = 0f;

        if (path.Count < 1)
        {
            return;
        }

        if (path.Count == 1)
        {
            AddStrip([path[0]], Direction.East, tileSize, 0f);
            return;
        }

        var phase = 0f;
        var segStart = 0;

        for (var i = 1; i < path.Count; i++)
        {
            var into = BeltLane.DirectionBetween(path[i - 1], path[i]);
            if (i >= path.Count - 1)
            {
                break;
            }

            var outward = BeltLane.DirectionBetween(path[i], path[i + 1]);
            if (outward == into)
            {
                continue;
            }

            if (i - 1 >= segStart)
            {
                var straight = Slice(path, segStart, i - 1);
                var dir = ResolveStripDirection(path, straight, segStart);
                AddStrip(straight, dir, tileSize, phase);
                phase += straight.Count;
            }

            var corner = new BeltCornerTile { Name = $"Corner_{path[i].X}_{path[i].Y}" };
            AddChild(corner);
            corner.Configure(path[i], into, outward, tileSize, phase);
            _corners.Add(corner);
            phase += 1f;
            segStart = i + 1;
        }

        if (segStart <= path.Count - 1)
        {
            var straight = Slice(path, segStart, path.Count - 1);
            var dir = ResolveStripDirection(path, straight, segStart);
            AddStrip(straight, dir, tileSize, phase);
        }
    }

    /// <summary>
    /// Rebuild from a placeable grid: platform corners where flow turns 90°,
    /// otherwise merged straight strips along each run.
    /// </summary>
    public void ConfigureFromGrid(BeltGrid grid, float rateItemsPerSecond, int tileSize)
    {
        ClearVisuals();
        _rateTilesPerSecond = Math.Max(0.05f, rateItemsPerSecond);
        _scroll = 0f;

        if (grid.Count == 0)
        {
            return;
        }

        var visited = new HashSet<GridPosition>();
        var phase = 0f;

        // Junction / splitter / sorter icons (not gallery corners or strips).
        foreach (var (pos, cell) in grid.Cells)
        {
            if (cell.Kind is not (LogisticsKind.Junction or LogisticsKind.Splitter or LogisticsKind.Sorter))
            {
                continue;
            }

            AddSpecial(pos, cell.Kind, cell.Direction, tileSize);
            visited.Add(pos);
            phase += 1f;
        }

        // Bridges: full 1×1 ends + thin mid-span (once per pair, from entry).
        foreach (var (pos, cell) in grid.Cells)
        {
            if (visited.Contains(pos)
                || cell.Kind != LogisticsKind.Bridge
                || cell.BridgePartner is not { } partner)
            {
                continue;
            }

            if (!IsBridgeEntryVisual(pos, cell.Direction, partner))
            {
                continue;
            }

            AddBridgePair(pos, partner, cell.Direction, tileSize, phase);
            visited.Add(pos);
            visited.Add(partner);
            phase += 1f;
        }

        // Corners first (platform pads) — belts only.
        foreach (var (pos, cell) in grid.Cells)
        {
            if (visited.Contains(pos)
                || !grid.IsCorner(pos)
                || !grid.TryGetIncomingDirection(pos, out var incoming))
            {
                continue;
            }

            var corner = new BeltCornerTile { Name = $"Corner_{pos.X}_{pos.Y}" };
            AddChild(corner);
            corner.Configure(pos, incoming, cell.Direction, tileSize, phase);
            _corners.Add(corner);
            visited.Add(pos);
            phase += 1f;
        }

        // Straight runs: start at cells that are not corners and not mid-run.
        foreach (var (pos, cell) in grid.Cells.OrderBy(kv => kv.Key.Y).ThenBy(kv => kv.Key.X))
        {
            if (visited.Contains(pos) || grid.IsCorner(pos) || cell.Kind != LogisticsKind.Belt)
            {
                continue;
            }

            // Prefer starting at head of a straight run (no same-dir predecessor).
            var pred = pos.Step(DirectionMath.Opposite(cell.Direction));
            if (grid.TryGet(pred, out var predCell)
                && !grid.IsCorner(pred)
                && predCell.Kind == LogisticsKind.Belt
                && predCell.Direction == cell.Direction
                && !visited.Contains(pred))
            {
                continue;
            }

            var run = new List<GridPosition>();
            var cursor = pos;
            while (true)
            {
                if (!grid.TryGet(cursor, out var runCell)
                    || visited.Contains(cursor)
                    || grid.IsCorner(cursor)
                    || runCell.Kind != LogisticsKind.Belt)
                {
                    break;
                }

                if (run.Count > 0)
                {
                    var prev = run[^1];
                    if (!grid.TryGet(prev, out var prevCell) || prevCell.Direction != runCell.Direction)
                    {
                        break;
                    }

                    if (!cursor.Equals(prev.Step(prevCell.Direction)))
                    {
                        break;
                    }
                }

                run.Add(cursor);
                visited.Add(cursor);
                cursor = cursor.Step(runCell.Direction);
            }

            if (run.Count == 0)
            {
                continue;
            }

            var dir = grid.Cells[run[0]].Direction;
            AddStrip(run, dir, tileSize, phase);
            phase += run.Count;
        }

        // Orphan belt cells (e.g. isolated after edits).
        foreach (var (pos, cell) in grid.Cells)
        {
            if (visited.Contains(pos) || cell.Kind != LogisticsKind.Belt)
            {
                continue;
            }

            AddStrip([pos], cell.Direction, tileSize, phase);
            phase += 1f;
            visited.Add(pos);
        }
    }

    private void ClearVisuals()
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _strips.Clear();
        _corners.Clear();
    }

    private static Direction ResolveStripDirection(
        IReadOnlyList<GridPosition> path,
        List<GridPosition> straight,
        int segStart)
    {
        if (straight.Count >= 2)
        {
            return BeltLane.DirectionBetween(straight[^2], straight[^1]);
        }

        if (segStart > 0)
        {
            return BeltLane.DirectionBetween(path[segStart - 1], path[segStart]);
        }

        return BeltLane.DirectionBetween(path[0], path[1]);
    }

    public override void _Process(double delta)
    {
        _scroll += _rateTilesPerSecond * (float)delta;
        if (_scroll > 1024f)
        {
            _scroll %= 1f;
        }

        foreach (var strip in _strips)
        {
            strip.SetScroll(_scroll);
        }

        foreach (var corner in _corners)
        {
            corner.SetScroll(_scroll);
        }
    }

    private void AddStrip(
        List<GridPosition> cells,
        Direction direction,
        int tileSize,
        float phase,
        float thicknessScale = 1f)
    {
        if (cells.Count == 0)
        {
            return;
        }

        var strip = new ScrollingBeltStrip { Name = $"Strip_{cells[0].X}_{cells[0].Y}" };
        AddChild(strip);
        strip.Configure(cells, direction, tileSize, phase, thicknessScale);
        if (thicknessScale < 0.99f)
        {
            // Bridge thin-span sits above underpass belts, below end pads.
            strip.ZIndex = 2;
        }

        _strips.Add(strip);
    }

    private void AddBridgePair(
        GridPosition entry,
        GridPosition exit,
        Direction direction,
        int tileSize,
        float phase)
    {
        AddSpecial(entry, LogisticsKind.Bridge, direction, tileSize);
        AddSpecial(exit, LogisticsKind.Bridge, direction, tileSize);

        // Thin mid-span between ends (decision 14 ~78%). Include both ends in path
        // so length covers center-to-center; end sprites sit above (ZIndex 3).
        var spanCells = new List<GridPosition> { entry };
        var cursor = entry.Step(direction);
        var guard = 0;
        while (!cursor.Equals(exit) && guard++ < BeltGridCell.MaxBridgeSpan + 1)
        {
            spanCells.Add(cursor);
            cursor = cursor.Step(direction);
        }

        spanCells.Add(exit);
        AddStrip(spanCells, direction, tileSize, phase, BridgeThicknessScale);
    }

    private static bool IsBridgeEntryVisual(GridPosition entry, Direction direction, GridPosition partner)
    {
        var dx = partner.X - entry.X;
        var dy = partner.Y - entry.Y;
        var span = Math.Abs(dx) + Math.Abs(dy);
        if (span < BeltGridCell.MinBridgeSpan || span > BeltGridCell.MaxBridgeSpan)
        {
            return false;
        }

        return direction switch
        {
            Direction.North => dx == 0 && dy < 0,
            Direction.East => dy == 0 && dx > 0,
            Direction.South => dx == 0 && dy > 0,
            Direction.West => dy == 0 && dx < 0,
            _ => false
        };
    }

    private void AddSpecial(GridPosition pos, LogisticsKind kind, Direction direction, int tileSize)
    {
        var texPath = kind switch
        {
            LogisticsKind.Junction => "res://assets/junction.png",
            LogisticsKind.Splitter => "res://assets/splitter.png",
            LogisticsKind.Sorter => "res://assets/sorter.png",
            LogisticsKind.Bridge => "res://assets/bridge.png",
            _ => "res://assets/conveyor-basic.png"
        };
        var node = new Node2D
        {
            Name = $"{kind}_{pos.X}_{pos.Y}",
            Position = new Vector2((pos.X + 0.5f) * tileSize, (pos.Y + 0.5f) * tileSize),
            ZIndex = 3
        };
        var sprite = new Sprite2D
        {
            Texture = GD.Load<Texture2D>(texPath),
            Centered = true,
            Scale = Vector2.One * (tileSize / 64f)
        };
        // Splitter / sorter / bridge rotate with facing; junction is axis-symmetric.
        if (kind is LogisticsKind.Splitter or LogisticsKind.Sorter or LogisticsKind.Bridge)
        {
            sprite.RotationDegrees = direction switch
            {
                Direction.East => 0f,
                Direction.South => 90f,
                Direction.West => 180f,
                Direction.North => -90f,
                _ => 0f
            };
        }

        node.AddChild(sprite);
        AddChild(node);
    }

    private static List<GridPosition> Slice(IReadOnlyList<GridPosition> path, int fromInclusive, int toInclusive)
    {
        var list = new List<GridPosition>(toInclusive - fromInclusive + 1);
        for (var i = fromInclusive; i <= toInclusive; i++)
        {
            list.Add(path[i]);
        }

        return list;
    }
}
