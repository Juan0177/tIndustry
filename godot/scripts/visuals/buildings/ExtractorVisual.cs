using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// 1×1 extractor: the <c>extractor</c> sprite with its filter's item icon in the empty centre slot (none without a
/// filter, as Mindustry's Unloader), and a small puff when it pulls an item (G3 effect <c>sbuffo-estrattore</c>, at most
/// one every <see cref="PuffSeconds"/>).
/// </summary>
public partial class ExtractorVisual : Node2D
{
    /// <summary>Shortest time between two puffs of one extractor (it moves up to 11 items a second).</summary>
    private const double PuffSeconds = 0.8;

    private int _tileSize;
    private Sprite2D? _filterIcon;
    private string? _filterItemId;
    private long _extracted;
    private double _nextPuff;
    public GridPosition Origin { get; private set; }

    public static ExtractorVisual Create(Extractor ex, int tileSize)
    {
        var root = new ExtractorVisual
        {
            Name = $"Extractor_{ex.Position.X}_{ex.Position.Y}",
            ZIndex = 3,
            Origin = ex.Position,
            _tileSize = tileSize,
            _extracted = ex.ItemsExtracted
        };
        BuildingPad.AddShadow(root, Sprites.For("extractor"), Extractor.Size, tileSize);
        root.AddChild(BuildingPad.Layer("Block", Sprites.For("extractor"), Extractor.Size, tileSize, 0));
        root.SyncFilter(ex.FilterItemId);
        return root;
    }

    public void Sync(Extractor ex, EffectManager? fx)
    {
        if (fx is not null && ex.ItemsExtracted != _extracted)
        {
            _extracted = ex.ItemsExtracted;
            if (fx.Clock >= _nextPuff)
            {
                _nextPuff = fx.Clock + PuffSeconds;
                fx.Play("sbuffo-estrattore", GlobalPosition);
            }
        }

        SyncFilter(ex.FilterItemId);
    }

    /// <summary>The icon follows the filter (the panel of the filter changes it while the block stands).</summary>
    private void SyncFilter(string? itemId)
    {
        if (_filterIcon is not null && itemId == _filterItemId)
        {
            return;
        }

        if (_filterIcon is null && string.IsNullOrWhiteSpace(itemId))
        {
            _filterItemId = null;
            return;
        }

        _filterIcon?.QueueFree();
        _filterItemId = itemId;
        _filterIcon = Sprites.FilterIcon(itemId, _tileSize);
        if (_filterIcon is not null)
        {
            AddChild(_filterIcon);
        }
    }
}
