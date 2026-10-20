using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Builds the belt visuals of a placeable <see cref="BeltGrid"/>: one sprite per straight cell, one per
/// corner, T merge or side join, junction / splitter / sorter / bridge sprites and the bridge spans. Every belt tier scrolls on
/// its own clock (cells per second = the tier's item rate), shared by all its cells so runs move in step:
/// every cell of a tier draws the same <see cref="AtlasTexture"/> (one per shape: straight, corner, T, side joins), and a
/// frame change moves only that texture's region, once per tier, whatever the number of cells.
/// </summary>
public partial class MindustryBeltVisual : Node2D
{
    public const float BridgeThicknessScale = BridgeSpanVisual.ThicknessScale;

    /// <summary>Animation state of one belt tier, kept across rebuilds so placing a belt does not reset the others.</summary>
    private sealed class TierClock(string id, float rateCellsPerSecond, bool armored)
    {
        public readonly SharedStrip Straight = new(id);
        public readonly SharedStrip Corner = new(id + "-corner");
        public readonly SharedStrip T = new(id + "-t");
        public readonly SharedStrip JoinLeft = new(id + "-join-left");
        public readonly SharedStrip JoinRight = new(id + "-join-right");
        public readonly SharedStrip JoinBoth = new(id + "-join-both");
        // An armored belt never blends with a building (BeltGrid.BuildingFeeders): it has no such strip.
        public readonly SharedStrip? JoinBuildingBoth = armored ? null : new(id + "-join-building-both");
        public readonly float Rate = rateCellsPerSecond;
        public float Scroll;
        public int Frame;
    }

    /// <summary>One window on a <c>{id}-anim</c> strip (square frames), shared by every cell that shows it.</summary>
    private sealed class SharedStrip
    {
        private readonly int _side;
        public readonly int Count;
        public readonly AtlasTexture Texture;

        public SharedStrip(string id)
        {
            var strip = Sprites.For(id + "-anim");
            _side = strip.GetHeight();
            Count = Math.Max(1, strip.GetWidth() / _side);
            Texture = new AtlasTexture { Atlas = strip, Region = new Rect2(0, 0, _side, _side) };
        }

        public void Show(int frame) => Texture.Region = new Rect2(frame % Count * _side, 0, _side, _side);
    }

    private readonly Dictionary<string, TierClock> _tiers = new(StringComparer.Ordinal);

    /// <summary>Set by the <c>oggetti-fluidi</c> playtest probe: belts scroll although playtests force effects off.</summary>
    internal static bool PlaytestScrolls { get; set; }

    /// <summary>Strip frame a belt tier shows now (playtest probe), or -1 if no cell of that tier is drawn.</summary>
    internal int FrameOf(string conveyorId) => _tiers.TryGetValue(conveyorId, out var clock) ? clock.Frame : -1;

    /// <summary>Frames per cell of a belt tier's straight strip (playtest probe), or 0 if no cell of that tier is drawn.</summary>
    internal int FramesPerCell(string conveyorId) => _tiers.TryGetValue(conveyorId, out var clock) ? clock.Straight.Count : 0;

    /// <summary>Back to the first frame on every tier (after the playtest probe, so later captures match).</summary>
    internal void PlaytestRewind()
    {
        foreach (var clock in _tiers.Values)
        {
            clock.Scroll = 0f;
            clock.Frame = 0;
            foreach (var strip in new[] { clock.Straight, clock.Corner, clock.T, clock.JoinLeft, clock.JoinRight, clock.JoinBoth, clock.JoinBuildingBoth ?? clock.JoinBoth })
            {
                strip.Show(0);
            }
        }
    }

    public void ConfigureFromGrid(IGameView view, int tileSize)
    {
        ClearVisuals();
        foreach (var (pos, cell) in view.Belts)
        {
            switch (cell.Kind)
            {
                case LogisticsKind.Junction or LogisticsKind.Splitter or LogisticsKind.Sorter:
                    AddSpecial(view, pos, cell.Kind, cell.Direction, tileSize, cell.FilterItemId);
                    break;
                case LogisticsKind.Bridge:
                    // Both ends get their pad; the span is drawn once, from the entry.
                    AddSpecial(view, pos, LogisticsKind.Bridge, cell.Direction, tileSize);
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
        if (ClientSettings.PlaytestForcesEffectsOff && !PlaytestScrolls)
        {
            return; // belts hold their first frame in playtests (repeatable captures)
        }

        foreach (var clock in _tiers.Values)
        {
            clock.Scroll += clock.Rate * (float)delta;
            if (clock.Scroll > 1024f)
            {
                clock.Scroll %= 1f;
            }

            // One frame per pixel of tread (the strip's frame count), so the belt moves as smoothly as its items.
            var count = clock.Straight.Count;
            var frame = (int)(clock.Scroll * count) % count;
            if (frame == clock.Frame)
            {
                continue;
            }

            clock.Frame = frame;
            clock.Straight.Show(frame);
            clock.Corner.Show(frame);
            clock.T.Show(frame);
            clock.JoinLeft.Show(frame);
            clock.JoinRight.Show(frame);
            clock.JoinBoth.Show(frame);
            clock.JoinBuildingBoth?.Show(frame);
        }
    }

    private void AddBelt(IGameView view, GridPosition pos, BeltGridCell cell, int tileSize)
    {
        var clock = ClockFor(cell.Definition);
        var shape = view.GetBeltShape(pos);
        if (shape == BeltShape.Corner && view.TryGetBeltIncomingDirection(pos, out var incoming))
        {
            var corner = new BeltCornerTile { Name = $"Corner_{pos.X}_{pos.Y}" };
            corner.Configure(pos, incoming, cell.Direction, tileSize, clock.Corner.Texture);
            AddChild(corner);
            return;
        }

        // T merge and side joins (art: out East, left of the flow North) turn to the output like a straight belt.
        var (name, strip) = shape switch
        {
            BeltShape.T => ("BeltT", clock.T),
            BeltShape.JoinLeft => ("BeltJoinLeft", clock.JoinLeft),
            BeltShape.JoinRight => ("BeltJoinRight", clock.JoinRight),
            BeltShape.JoinBoth => ("BeltJoinBoth", clock.JoinBoth),
            BeltShape.JoinBuildingBoth => ("BeltJoinBuildingBoth", clock.JoinBuildingBoth ?? clock.JoinBoth),
            _ => ("Belt", clock.Straight)
        };
        var straight = new ScrollingBeltStrip { Name = $"{name}_{pos.X}_{pos.Y}" };
        straight.Configure(pos, cell.Direction, tileSize, strip.Texture);
        AddChild(straight);
    }

    private TierClock ClockFor(ConveyorDefinition definition)
    {
        if (!_tiers.TryGetValue(definition.Id, out var clock))
        {
            clock = new TierClock(definition.Id, Math.Max(0.05f, definition.RateItemsPerSecond), definition.IsArmoredBelt);
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
        IGameView view,
        GridPosition pos,
        LogisticsKind kind,
        Direction direction,
        int tileSize,
        string? sorterFilterId = null)
    {
        var texture = Sprites.For(kind switch
        {
            LogisticsKind.Junction => "junction-base",
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

        // Junction: every side without an input or output keeps its rail (the piece is drawn for the North side).
        if (kind == LogisticsKind.Junction)
        {
            var rail = Sprites.For("junction-bordo");
            foreach (var side in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
            {
                if (!view.IsJunctionSideConnected(pos, side))
                {
                    node.AddChild(new Sprite2D
                    {
                        Name = $"Rail{side}",
                        Texture = rail,
                        Centered = true,
                        TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                        Scale = sprite.Scale,
                        RotationDegrees = ScrollingBeltStrip.RotationFor(side) + 90f
                    });
                }
            }
        }

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
