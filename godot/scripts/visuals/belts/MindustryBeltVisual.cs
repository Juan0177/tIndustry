using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Builds the belt visuals of a placeable <see cref="BeltGrid"/>: one sprite per straight cell, one per
/// corner, junction / splitter / sorter / bridge sprites and the bridge spans. Every belt tier scrolls on
/// its own clock (cells per second = the tier's item rate), shared by all its cells so runs move in step.
/// </summary>
public partial class MindustryBeltVisual : Node2D
{
    public const float BridgeThicknessScale = BridgeSpanVisual.ThicknessScale;

    /// <summary>Animation state of one belt tier, kept across rebuilds so placing a belt does not reset the others.</summary>
    private sealed class TierClock(Texture2D[] straight, Texture2D[] corner, float rateCellsPerSecond)
    {
        public readonly Texture2D[] Straight = straight;
        public readonly Texture2D[] Corner = corner;
        public readonly float Rate = rateCellsPerSecond;
        public readonly List<Sprite2D> StraightCells = [];
        public readonly List<Sprite2D> CornerCells = [];
        public float Scroll;
        public int Frame;
    }

    private readonly Dictionary<string, TierClock> _tiers = new(StringComparer.Ordinal);

    public void ConfigureFromGrid(IGameView view, int tileSize)
    {
        ClearVisuals();
        foreach (var (pos, cell) in view.Belts)
        {
            switch (cell.Kind)
            {
                case LogisticsKind.Junction or LogisticsKind.Splitter or LogisticsKind.Sorter:
                    AddSpecial(pos, cell.Kind, cell.Direction, tileSize, cell.FilterItemId);
                    break;
                case LogisticsKind.Bridge:
                    // Both ends get their pad; the span is drawn once, from the entry.
                    AddSpecial(pos, LogisticsKind.Bridge, cell.Direction, tileSize);
                    if (cell.BridgePartner is { } partner && IsBridgeEntryVisual(pos, cell.Direction, partner))
                    {
                        var span = new BridgeSpanVisual { Name = $"BridgeSpan_{pos.X}_{pos.Y}" };
                        AddChild(span);
                        span.Configure(pos, partner, cell.Direction, tileSize);
                    }

                    break;
                case LogisticsKind.Belt:
                    AddBelt(view, pos, cell, tileSize);
                    break;
            }
        }
    }

    public override void _Process(double delta)
    {
        foreach (var clock in _tiers.Values)
        {
            clock.Scroll += clock.Rate * (float)delta;
            if (clock.Scroll > 1024f)
            {
                clock.Scroll %= 1f;
            }

            var frame = (int)(clock.Scroll * 8) % 8;
            if (frame == clock.Frame)
            {
                continue;
            }

            clock.Frame = frame;
            foreach (var sprite in clock.StraightCells)
            {
                sprite.Texture = clock.Straight[frame % clock.Straight.Length];
            }

            foreach (var sprite in clock.CornerCells)
            {
                sprite.Texture = clock.Corner[frame % clock.Corner.Length];
            }
        }
    }

    private void AddBelt(IGameView view, GridPosition pos, BeltGridCell cell, int tileSize)
    {
        var clock = ClockFor(cell.Definition);
        if (view.IsBeltCorner(pos) && view.TryGetBeltIncomingDirection(pos, out var incoming))
        {
            var corner = new BeltCornerTile { Name = $"Corner_{pos.X}_{pos.Y}" };
            corner.Configure(pos, incoming, cell.Direction, tileSize, clock.Corner[clock.Frame % clock.Corner.Length]);
            AddChild(corner);
            clock.CornerCells.Add(corner);
            return;
        }

        var straight = new ScrollingBeltStrip { Name = $"Belt_{pos.X}_{pos.Y}" };
        straight.Configure(pos, cell.Direction, tileSize, clock.Straight[clock.Frame % clock.Straight.Length]);
        AddChild(straight);
        clock.StraightCells.Add(straight);
    }

    private TierClock ClockFor(ConveyorDefinition definition)
    {
        if (!_tiers.TryGetValue(definition.Id, out var clock))
        {
            clock = new TierClock(
                Sprites.Frames(definition.Id),
                Sprites.Frames(definition.Id + "-corner"),
                Math.Max(0.05f, definition.RateItemsPerSecond));
            _tiers[definition.Id] = clock;
        }

        return clock;
    }

    private void ClearVisuals()
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        foreach (var clock in _tiers.Values)
        {
            clock.StraightCells.Clear();
            clock.CornerCells.Clear();
        }
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

    private void AddSpecial(
        GridPosition pos,
        LogisticsKind kind,
        Direction direction,
        int tileSize,
        string? sorterFilterId = null)
    {
        var texture = Sprites.For(kind switch
        {
            LogisticsKind.Junction => "junction",
            LogisticsKind.Splitter => "splitter",
            LogisticsKind.Sorter => "sorter",
            LogisticsKind.Bridge => "conveyor-bridge",
            _ => BuildSpec.DefaultConveyorId
        });
        var node = new Node2D
        {
            Name = $"{kind}_{pos.X}_{pos.Y}",
            Position = new Vector2((pos.X + 0.5f) * tileSize, (pos.Y + 0.5f) * tileSize),
            ZIndex = 3
        };
        var sprite = new Sprite2D
        {
            Texture = texture,
            Centered = true,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            Scale = Vector2.One * Sprites.ScaleToCells(texture, 1, tileSize)
        };
        // Splitter / sorter / bridge rotate with facing; junction is axis-symmetric.
        if (kind is LogisticsKind.Splitter or LogisticsKind.Sorter or LogisticsKind.Bridge)
        {
            sprite.RotationDegrees = ScrollingBeltStrip.RotationFor(direction);
        }

        node.AddChild(sprite);

        // Sorter: filtered item icon in the sprite's centre slot (upright: a sibling of the rotated frame).
        if (kind == LogisticsKind.Sorter)
        {
            var filterId = string.IsNullOrWhiteSpace(sorterFilterId)
                ? BeltGridCell.DefaultSorterFilter
                : sorterFilterId;
            // The 10 px belt sprite of the item at the world art scale, inside the 14 px slot.
            node.AddChild(new Sprite2D
            {
                Name = "FilterIcon",
                Texture = Sprites.BeltItem(filterId),
                Centered = true,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                Scale = Vector2.One * ((float)tileSize / Sprites.PixelsPerCell),
                ZIndex = 1
            });
        }

        AddChild(node);
    }
}
