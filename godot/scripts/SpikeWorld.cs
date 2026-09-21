using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Spike root: draws a small grid, runs one BeltLane from Shared (content.json rates),
/// and hosts a static miner sprite (no drill / tip animation — matches Raylib).
/// </summary>
public partial class SpikeWorld : Node2D
{
    public const int TileSize = 64;
    public const int MapWidth = 24;
    public const int MapHeight = 16;

    private BeltLane? _belt;
    private readonly Dictionary<long, Sprite2D> _itemSprites = [];
    private Node2D? _itemsLayer;
    private Label? _hud;
    private float _spawnAccum;
    private string _contentPath = "";
    private ConveyorDefinition? _beltDef;

    public override void _Ready()
    {
        _contentPath = ResolveContentPath();
        _beltDef = ConveyorContent.RequireBelt(_contentPath, "conveyor-basic");

        var path = new List<GridPosition>();
        for (var x = 4; x <= 14; x++)
        {
            path.Add(new GridPosition(x, 8));
        }

        _belt = new BeltLane(path, Direction.East, _beltDef);
        _itemsLayer = GetNode<Node2D>("Items");
        _hud = GetNode<Label>("Hud/Status");

        DrawStaticBeltTiles();
        UpdateHud();

        // Auto-screenshot after a short settle (for spike evidence / headless capture).
        var timer = GetTree().CreateTimer(2.0);
        timer.Timeout += SaveSpikeScreenshot;
    }

    public override void _Process(double delta)
    {
        if (_belt is null || _beltDef is null)
        {
            return;
        }

        var dt = (float)delta;
        _belt.Tick(dt);

        _spawnAccum += dt;
        // Spawn a bit faster than belt rate so multiple items are visible on the strip.
        var interval = 0.55f / Math.Max(0.05f, _beltDef.RateItemsPerSecond);
        if (_spawnAccum >= interval)
        {
            _spawnAccum = 0f;
            _belt.TrySpawnAtStart("iron-ore");
        }

        SyncItemSprites();
        UpdateHud();
    }

    public override void _Draw()
    {
        // Soft ground + grid (atmosphere, not flat fill only).
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
    }

    private void DrawStaticBeltTiles()
    {
        if (_belt is null)
        {
            return;
        }

        var belts = GetNode<Node2D>("Belts");
        var tex = GD.Load<Texture2D>("res://assets/conveyor-basic.png");
        foreach (var cell in _belt.Cells)
        {
            var sprite = new Sprite2D
            {
                Texture = tex,
                Centered = true,
                Position = CellCenter(cell.Position),
                Scale = new Vector2(0.9f, 0.9f),
                Modulate = new Color(0.85f, 0.9f, 1f)
            };
            // conveyor-basic.png arrow points roughly up; -90° faces east (belt Direction).
            sprite.RotationDegrees = -90f;
            belts.AddChild(sprite);
        }
    }

    private void SyncItemSprites()
    {
        if (_belt is null || _itemsLayer is null)
        {
            return;
        }

        var live = new HashSet<long>();
        var oreTex = GD.Load<Texture2D>("res://assets/iron-ore.png");

        foreach (var cell in _belt.Cells)
        {
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
                var to = CellCenter(cell.Position.Step(_belt.Direction));
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
        if (_hud is null || _beltDef is null || _belt is null)
        {
            return;
        }

        var count = _belt.Cells.Sum(c => c.Items.Count);
        _hud.Text =
            $"tIndustry Godot spike  |  belt={_beltDef.Id} rate={_beltDef.RateItemsPerSecond}/s  |  items={count}\n" +
            "WASD / middle-drag pan · wheel zoom · static miner (no drill anim)";
    }

    private static Vector2 CellCenter(GridPosition cell) =>
        new((cell.X + 0.5f) * TileSize, (cell.Y + 0.5f) * TileSize);

    private static string ResolveContentPath()
    {
        // Prefer repo data/ when running from editor / source tree.
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

    private void SaveSpikeScreenshot()
    {
        var img = GetViewport().GetTexture().GetImage();
        // Prefer store media path if mounted; else write under godot/artifacts
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

        // Full map overview + a second crop focused on miner+belt.
        var mapPath = Path.Combine(destDir, "godot-spike-map.png");
        var err = img.SavePng(mapPath);
        GD.Print(err == Error.Ok ? $"Screenshot: {mapPath}" : $"Screenshot failed: {err}");

        // Crop around miner (tile 3,8) through belt mid (tile 10,8) in screen space roughly.
        var crop = img.GetRegion(new Rect2I(80, 280, 720, 280));
        var beltPath = Path.Combine(destDir, "godot-spike-belt.png");
        err = crop.SavePng(beltPath);
        GD.Print(err == Error.Ok ? $"Screenshot: {beltPath}" : $"Belt crop failed: {err}");

        var minerCrop = img.GetRegion(new Rect2I(40, 300, 280, 240));
        var minerPath = Path.Combine(destDir, "godot-spike-miner-static.png");
        err = minerCrop.SavePng(minerPath);
        GD.Print(err == Error.Ok ? $"Screenshot: {minerPath}" : $"Miner crop failed: {err}");
    }
}
