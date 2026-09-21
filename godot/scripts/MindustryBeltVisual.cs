using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Builds Mindustry-style belt visuals from a cell path: straight strips + corner tiles,
/// sharing one scroll clock so chevrons stay phase-continuous around L turns.
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
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _strips.Clear();
        _corners.Clear();
        _rateTilesPerSecond = Math.Max(0.05f, rateItemsPerSecond);
        _scroll = 0f;

        if (path.Count < 2)
        {
            throw new ArgumentException("Serve almeno 2 celle per un nastro.", nameof(path));
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

            // Straight before corner: path[segStart .. i-1]
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

        // Trailing straight after last corner (or full path if no corners).
        if (segStart <= path.Count - 1)
        {
            var straight = Slice(path, segStart, path.Count - 1);
            var dir = ResolveStripDirection(path, straight, segStart);
            AddStrip(straight, dir, tileSize, phase);
        }
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
