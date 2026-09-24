using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Phase A playable slice: static miner → Mindustry L-belt → core stock.
/// Uses <see cref="FactorySlice"/> from TIndustry.Shared (no fake timer spawn).
/// </summary>
public partial class SpikeWorld : Node2D
{
    public const int TileSize = 64;
    public const int MapWidth = 24;
    public const int MapHeight = 18;

    private FactorySlice? _slice;
    private MindustryBeltVisual? _beltVisual;
    private readonly Dictionary<long, Sprite2D> _itemSprites = [];
    private Node2D? _itemsLayer;
    private Label? _hud;
    private string _contentPath = "";

    public override void _Ready()
    {
        _contentPath = ResolveContentPath();
        var content = FactoryContent.Load(_contentPath);
        _slice = FactorySlice.CreateSpikeDemo(content);

        _itemsLayer = GetNode<Node2D>("Items");
        _hud = GetNode<Label>("Hud/Status");

        PlaceMinerVisual();
        BuildScrollingBelt();
        UpdateHud();

        var timer = GetTree().CreateTimer(5.0);
        timer.Timeout += SavePortScreenshot;
    }

    public override void _Process(double delta)
    {
        if (_slice is null)
        {
            return;
        }

        _slice.Tick((float)delta);
        SyncItemSprites();
        UpdateHud();
        QueueRedraw();
    }

    public override void _Draw()
    {
        var ground = new Color(0.14f, 0.18f, 0.16f);
        var groundAlt = new Color(0.16f, 0.21f, 0.18f);
        for (var y = 0; y < MapHeight; y++)
        {
            for (var x = 0; x < MapWidth; x++)
            {
                var rect = new Rect2(x * TileSize, y * TileSize, TileSize, TileSize);
                DrawRect(rect, ((x + y) % 2 == 0) ? ground : groundAlt);
            }
        }

        var grid = new Color(0.22f, 0.28f, 0.24f, 0.85f);
        for (var x = 0; x <= MapWidth; x++)
        {
            var px = x * TileSize;
            DrawLine(new Vector2(px, 0), new Vector2(px, MapHeight * TileSize), grid, 1f);
        }

        for (var y = 0; y <= MapHeight; y++)
        {
            var py = y * TileSize;
            DrawLine(new Vector2(0, py), new Vector2(MapWidth * TileSize, py), grid, 1f);
        }

        if (_slice is null)
        {
            return;
        }

        // Core stock tiles (ingresso magazzino).
        var coreFill = new Color(0.22f, 0.38f, 0.55f, 0.85f);
        var coreEdge = new Color(0.45f, 0.75f, 0.95f, 1f);
        foreach (var tile in _slice.CoreTiles)
        {
            var rect = new Rect2(tile.X * TileSize, tile.Y * TileSize, TileSize, TileSize);
            DrawRect(rect, coreFill);
            DrawRect(rect, coreEdge, false, 2f);
        }

        // Soft deposit tint under miner (on-deposit hint).
        var deposit = new Color(0.45f, 0.32f, 0.18f, 0.35f);
        foreach (var tile in _slice.Miner.OccupiedTiles())
        {
            var rect = new Rect2(tile.X * TileSize, tile.Y * TileSize, TileSize, TileSize);
            DrawRect(rect, deposit);
        }
    }

    private void PlaceMinerVisual()
    {
        if (_slice is null || !HasNode("Miner"))
        {
            return;
        }

        var miner = GetNode<Node2D>("Miner");
        var origin = _slice.Miner.Position;
        // Center of 2×2 footprint.
        miner.Position = new Vector2(
            (origin.X + MinerProducer.Size * 0.5f) * TileSize,
            (origin.Y + MinerProducer.Size * 0.5f) * TileSize);
    }

    private void BuildScrollingBelt()
    {
        if (_slice is null)
        {
            return;
        }

        var belts = GetNode<Node2D>("Belts");
        foreach (var child in belts.GetChildren())
        {
            child.QueueFree();
        }

        _beltVisual = new MindustryBeltVisual { Name = "MindustryBelt" };
        belts.AddChild(_beltVisual);
        var path = _slice.Belt.Cells.Select(c => c.Position).ToList();
        _beltVisual.Configure(path, _slice.Belt.Definition.RateItemsPerSecond, TileSize);
    }

    private void SyncItemSprites()
    {
        if (_slice is null || _itemsLayer is null)
        {
            return;
        }

        var live = new HashSet<long>();
        var oreTex = GD.Load<Texture2D>("res://assets/iron-ore.png");
        var belt = _slice.Belt;

        for (var cellIndex = 0; cellIndex < belt.Cells.Count; cellIndex++)
        {
            var cell = belt.Cells[cellIndex];
            var dir = belt.DirectionAt(cellIndex);
            foreach (var item in cell.Items)
            {
                live.Add(item.Id);
                if (!_itemSprites.TryGetValue(item.Id, out var sprite))
                {
                    sprite = new Sprite2D
                    {
                        Texture = oreTex,
                        Centered = true,
                        Scale = new Vector2(0.7f, 0.7f),
                        ZIndex = 5,
                        Modulate = new Color(1.15f, 1.05f, 0.95f)
                    };
                    _itemsLayer.AddChild(sprite);
                    _itemSprites[item.Id] = sprite;
                }

                var from = CellCenter(cell.Position);
                var to = CellCenter(cell.Position.Step(dir));
                sprite.Position = from.Lerp(to, Mathf.Clamp(item.Progress, 0f, 1f));
            }
        }

        var dead = _itemSprites.Keys.Where(id => !live.Contains(id)).ToList();
        foreach (var id in dead)
        {
            _itemSprites[id].QueueFree();
            _itemSprites.Remove(id);
        }
    }

    private void UpdateHud()
    {
        if (_hud is null || _slice is null)
        {
            return;
        }

        var oreName = _slice.Content.DisplayName("iron-ore");
        var oreStock = _slice.Wallet.MaterialCount("iron-ore");
        var onBelt = _slice.Belt.Cells.Sum(c => c.Items.Count);
        var scroll = _beltVisual?.ScrollTiles ?? 0f;
        var progressPct = (int)(_slice.Miner.Progress * 100f);

        _hud.Text =
            $"tIndustry Godot — loop fabbrica  |  Nastro={_slice.Belt.Definition.Id}  {_slice.Belt.Definition.RateItemsPerSecond}/s\n" +
            $"Minatore T1 (statico)  progresso={progressPct}%  prodotti={_slice.Miner.ItemsProduced}  |  sul nastro={onBelt}\n" +
            $"Core magazzino: {oreName} = {oreStock}  (consegnati={_slice.CoreDeliveredItems})  |  scroll={scroll:0.00}\n" +
            "WASD / drag centrale = pan · rotella = zoom · nessun combat";
    }

    private static Vector2 CellCenter(GridPosition cell) =>
        new((cell.X + 0.5f) * TileSize, (cell.Y + 0.5f) * TileSize);

    private static string ResolveContentPath()
    {
        var candidates = new[]
        {
            ProjectSettings.GlobalizePath("res://../data/content.json"),
            ProjectSettings.GlobalizePath("res://data/content.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "data", "content.json")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "data", "content.json")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "data", "content.json")),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException(
            "content.json non trovato. Apri il progetto dalla cartella godot/ del repo tIndustry.");
    }

    private void SavePortScreenshot()
    {
        var img = GetViewport().GetTexture().GetImage();
        var mediaCandidates = new[]
        {
            "/cursor/stores/bc-6a5b42f7-8a62-4dc2-b71f-4a3a4f9c2232/media",
            ProjectSettings.GlobalizePath("res://artifacts"),
            "/opt/cursor/artifacts"
        };

        string? destDir = null;
        foreach (var d in mediaCandidates)
        {
            try
            {
                Directory.CreateDirectory(d);
                destDir = d;
                break;
            }
            catch
            {
                // try next
            }
        }

        if (destDir is null)
        {
            GD.PushWarning("Nessuna cartella screenshot scrivibile.");
            return;
        }

        var mapPath = Path.Combine(destDir, "godot-port-loop-map.png");
        var err = img.SavePng(mapPath);
        GD.Print(err == Error.Ok ? $"Screenshot: {mapPath}" : $"Screenshot failed: {err}");

        var crop = img.GetRegion(new Rect2I(80, 200, 900, 520));
        var closePath = Path.Combine(destDir, "godot-port-loop-close.png");
        err = crop.SavePng(closePath);
        GD.Print(err == Error.Ok ? $"Screenshot: {closePath}" : $"Crop failed: {err}");

        // Also keep spike names for continuity.
        img.SavePng(Path.Combine(destDir, "godot-belt-l-map.png"));
        crop.SavePng(Path.Combine(destDir, "godot-belt-l-close.png"));
    }
}
