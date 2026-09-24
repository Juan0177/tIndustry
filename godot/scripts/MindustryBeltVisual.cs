using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Builds Mindustry-style belt visuals from a cell path or a placeable <see cref="BeltGrid"/>:
/// straight strips + platform corner tiles, shared scroll clock.
/// </summary>
public partial class MindustryBeltVisual : Node2D
{
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

            // Ombrette only at effective extremities: skip if neighbor cell is also a corner.
            var shadeEntry = i < 2 || !IsPathCorner(path, i - 1);
            var shadeExit = i >= path.Count - 2 || !IsPathCorner(path, i + 1);
            var corner = new BeltCornerTile { Name = $"Corner_{path[i].X}_{path[i].Y}" };
            AddChild(corner);
            corner.Configure(path[i], into, outward, tileSize, phase, shadeEntry, shadeExit);
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

        // Corners first (platform pads).
        foreach (var (pos, cell) in grid.Cells)
        {
            if (!grid.IsCorner(pos) || !grid.TryGetIncomingDirection(pos, out var incoming))
            {
                continue;
            }

            var corner = new BeltCornerTile { Name = $"Corner_{pos.X}_{pos.Y}" };
            AddChild(corner);
            var pred = pos.Step(DirectionMath.Opposite(incoming));
            var succ = pos.Step(cell.Direction);
            // Z/U: ombrette only where the gallery opens onto a straight (or void),
            // not where two corners abut.
            var shadeEntry = !grid.IsCorner(pred);
            var shadeExit = !grid.IsCorner(succ);
            corner.Configure(pos, incoming, cell.Direction, tileSize, phase, shadeEntry, shadeExit);
            _corners.Add(corner);
            visited.Add(pos);
            phase += 1f;
        }

        // Straight runs: start at cells that are not corners and not mid-run.
        foreach (var (pos, cell) in grid.Cells.OrderBy(kv => kv.Key.Y).ThenBy(kv => kv.Key.X))
        {
            if (visited.Contains(pos) || grid.IsCorner(pos))
            {
                continue;
            }

            // Prefer starting at head of a straight run (no same-dir predecessor).
            var pred = pos.Step(DirectionMath.Opposite(cell.Direction));
            if (grid.TryGet(pred, out var predCell)
                && !grid.IsCorner(pred)
                && predCell.Direction == cell.Direction
                && !visited.Contains(pred))
            {
                continue;
            }

            var run = new List<GridPosition>();
            var cursor = pos;
            while (true)
            {
                if (!grid.TryGet(cursor, out var runCell) || visited.Contains(cursor) || grid.IsCorner(cursor))
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

        // Orphan cells (e.g. isolated after edits).
        foreach (var (pos, cell) in grid.Cells)
        {
            if (visited.Contains(pos))
            {
                continue;
            }

            AddStrip([pos], cell.Direction, tileSize, phase);
            phase += 1f;
            visited.Add(pos);
        }
    }

    private static bool IsPathCorner(IReadOnlyList<GridPosition> path, int i)
    {
        if (i <= 0 || i >= path.Count - 1)
        {
            return false;
        }

        var into = BeltLane.DirectionBetween(path[i - 1], path[i]);
        var outward = BeltLane.DirectionBetween(path[i], path[i + 1]);
        return outward != into;
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

    private void AddStrip(List<GridPosition> cells, Direction direction, int tileSize, float phase)
    {
        if (cells.Count == 0)
        {
            return;
        }

        var strip = new ScrollingBeltStrip { Name = $"Strip_{cells[0].X}_{cells[0].Y}" };
        AddChild(strip);
        strip.Configure(cells, direction, tileSize, phase);
        _strips.Add(strip);
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
