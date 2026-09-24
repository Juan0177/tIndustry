using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Phase C: placeable belts + miner + forno stub (ore→plate→core).
/// 1=nastro · 2=minatore · 3=forno · R=ruota · click piazza · destro rimuovi.
/// </summary>
public partial class SpikeWorld : Node2D
{
    public const int TileSize = 64;
    public const int MapWidth = 24;
    public const int MapHeight = 18;

    private enum BuildTool
    {
        Belt,
        Miner,
        Smelter
    }

    private FactorySlice? _slice;
    private MindustryBeltVisual? _beltVisual;
    private readonly Dictionary<long, Sprite2D> _itemSprites = [];
    private Node2D? _itemsLayer;
    private Node2D? _buildingsLayer;
    private Label? _hud;
    private string _contentPath = "";
    private Direction _placeDir = Direction.East;
    private BuildTool _tool = BuildTool.Belt;
    private GridPosition? _hover;
    private bool _draggingPlace;
    private bool _draggingRemove;
    private bool _visualDirty = true;
    private bool _buildingsDirty = true;
    private Texture2D? _oreTex;
    private Texture2D? _plateTex;

    public override void _Ready()
    {
        _contentPath = ResolveContentPath();
        var content = FactoryContent.Load(_contentPath);
        _slice = FactorySlice.CreatePhaseCDemo(content);

        _itemsLayer = GetNode<Node2D>("Items");
        _hud = GetNode<Label>("Hud/Status");
        EnsureBuildingsLayer();
        _oreTex = GD.Load<Texture2D>("res://assets/iron-ore.png");
        _plateTex = GD.Load<Texture2D>("res://assets/iron-plate.png");

        EnsureBeltVisual();
        RebuildBeltVisual();
        RebuildBuildingVisuals();
        UpdateHud();

        var timer = GetTree().CreateTimer(3.0);
        timer.Timeout += SavePortScreenshot;
        if (HasNode("Camera"))
        {
            // Zoom 1: pixel-verify belt outer edge == tile edge.
            var cam = GetNode<Camera2D>("Camera");
            cam.Position = new Vector2(10.5f * TileSize, 8.5f * TileSize);
            cam.Zoom = new Vector2(1f, 1f);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_slice is null)
        {
            return;
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.Key1 || key.Keycode == Key.N)
            {
                _tool = BuildTool.Belt;
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Key2 || key.Keycode == Key.M)
            {
                _tool = BuildTool.Miner;
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Key3 || key.Keycode == Key.F)
            {
                _tool = BuildTool.Smelter;
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.R)
            {
                _placeDir = DirectionMath.Right(_placeDir);
                QueueRedraw();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (@event is InputEventMouseButton mouse)
        {
            var cell = ScreenToCell(mouse.Position);
            if (mouse.ButtonIndex == MouseButton.Left)
            {
                if (mouse.Pressed)
                {
                    _draggingPlace = _tool == BuildTool.Belt;
                    TryPlaceAt(cell);
                    GetViewport().SetInputAsHandled();
                }
                else
                {
                    _draggingPlace = false;
                }
            }
            else if (mouse.ButtonIndex == MouseButton.Right)
            {
                if (mouse.Pressed)
                {
                    _draggingRemove = true;
                    TryRemoveAt(cell);
                    GetViewport().SetInputAsHandled();
                }
                else
                {
                    _draggingRemove = false;
                }
            }
        }
        else if (@event is InputEventMouseMotion motion)
        {
            var cell = ScreenToCell(motion.Position);
            if (!_hover.Equals(cell))
            {
                _hover = cell;
                QueueRedraw();
            }

            if (_draggingPlace && _tool == BuildTool.Belt)
            {
                TryPlaceAt(cell);
            }
            else if (_draggingRemove)
            {
                TryRemoveAt(cell);
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_slice is null)
        {
            return;
        }

        _slice.Tick((float)delta);
        if (_visualDirty)
        {
            RebuildBeltVisual();
            _visualDirty = false;
        }

        if (_buildingsDirty)
        {
            RebuildBuildingVisuals();
            _buildingsDirty = false;
        }

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

        var coreFill = new Color(0.22f, 0.38f, 0.55f, 0.85f);
        var coreEdge = new Color(0.45f, 0.75f, 0.95f, 1f);
        foreach (var tile in _slice.CoreTiles)
        {
            var rect = new Rect2(tile.X * TileSize, tile.Y * TileSize, TileSize, TileSize);
            DrawRect(rect, coreFill);
            DrawRect(rect, coreEdge, false, 2f);
        }

        foreach (var miner in _slice.Miners)
        {
            var deposit = new Color(0.45f, 0.32f, 0.18f, 0.35f);
            foreach (var tile in miner.OccupiedTiles())
            {
                DrawRect(new Rect2(tile.X * TileSize, tile.Y * TileSize, TileSize, TileSize), deposit);
            }
        }

        if (_hover is { } hover
            && hover.X >= 0 && hover.Y >= 0
            && hover.X < MapWidth && hover.Y < MapHeight)
        {
            DrawGhost(hover);
        }
    }

    private void DrawGhost(GridPosition hover)
    {
        if (_slice is null)
        {
            return;
        }

        var size = _tool switch
        {
            BuildTool.Miner => MinerProducer.Size,
            BuildTool.Smelter => SmelterStub.Size,
            _ => 1
        };

        var ok = size == 1
            ? _slice.CanOccupy(hover)
            : _slice.CanOccupyFootprint(hover, size)
              || (_tool == BuildTool.Miner && _slice.Miners.Count == 1)
              || (_tool == BuildTool.Smelter && _slice.Smelters.Count == 1);

        var ghost = ok
            ? new Color(0.35f, 0.85f, 0.55f, 0.35f)
            : new Color(0.9f, 0.25f, 0.2f, 0.35f);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var rect = new Rect2((hover.X + x) * TileSize, (hover.Y + y) * TileSize, TileSize, TileSize);
                DrawRect(rect, ghost);
                DrawRect(rect, new Color(0.9f, 0.95f, 0.85f, 0.9f), false, 2f);
            }
        }

        if (_tool == BuildTool.Belt)
        {
            DrawDirectionHint(hover, _placeDir);
        }
    }

    private void DrawDirectionHint(GridPosition cell, Direction dir)
    {
        var center = CellCenter(cell);
        var (dx, dy) = DirectionMath.ToOffset(dir);
        var tip = center + new Vector2(dx, dy) * (TileSize * 0.32f);
        DrawLine(center, tip, new Color(0.95f, 0.95f, 0.7f, 0.95f), 3f);
        DrawCircle(tip, 4f, new Color(0.95f, 0.95f, 0.7f, 0.95f));
    }

    private void TryPlaceAt(GridPosition cell)
    {
        if (_slice is null)
        {
            return;
        }

        switch (_tool)
        {
            case BuildTool.Belt:
                if (_slice.TryPlaceBelt(cell, _placeDir))
                {
                    _visualDirty = true;
                }

                break;
            case BuildTool.Miner:
                if (_slice.TryPlaceMiner(cell, _placeDir))
                {
                    _buildingsDirty = true;
                }

                break;
            case BuildTool.Smelter:
                if (_slice.TryPlaceSmelter(cell, _placeDir))
                {
                    _buildingsDirty = true;
                }

                break;
        }
    }

    private void TryRemoveAt(GridPosition cell)
    {
        if (_slice is null)
        {
            return;
        }

        if (_slice.TryRemoveBuildingAt(cell))
        {
            _buildingsDirty = true;
            return;
        }

        if (_slice.TryRemoveBelt(cell))
        {
            _visualDirty = true;
        }
    }

    private GridPosition ScreenToCell(Vector2 screenPos)
    {
        _ = screenPos;
        var world = GetGlobalMousePosition();
        var x = Mathf.FloorToInt(world.X / TileSize);
        var y = Mathf.FloorToInt(world.Y / TileSize);
        return new GridPosition(x, y);
    }

    private void EnsureBuildingsLayer()
    {
        if (HasNode("Buildings"))
        {
            _buildingsLayer = GetNode<Node2D>("Buildings");
            return;
        }

        _buildingsLayer = new Node2D { Name = "Buildings", ZIndex = 3 };
        AddChild(_buildingsLayer);
        // Hide legacy scene Miner node if present — rebuilt dynamically.
        if (HasNode("Miner"))
        {
            GetNode("Miner").QueueFree();
        }
    }

    private void RebuildBuildingVisuals()
    {
        if (_slice is null || _buildingsLayer is null)
        {
            return;
        }

        foreach (var child in _buildingsLayer.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var miner in _slice.Miners)
        {
            var node = new Node2D
            {
                Name = $"Miner_{miner.Position.X}_{miner.Position.Y}",
                Position = FootprintCenter(miner.Position, MinerProducer.Size)
            };
            var sprite = new Sprite2D
            {
                Texture = GD.Load<Texture2D>("res://assets/miner.png"),
                Centered = true,
                Scale = Vector2.One
            };
            node.AddChild(sprite);
            _buildingsLayer.AddChild(node);
        }

        foreach (var smelter in _slice.Smelters)
        {
            var node = new StaticSmelter
            {
                Name = $"Smelter_{smelter.Position.X}_{smelter.Position.Y}",
                Position = FootprintCenter(smelter.Position, SmelterStub.Size)
            };
            _buildingsLayer.AddChild(node);
            node.EnsureSprite();
        }
    }

    private static Vector2 FootprintCenter(GridPosition origin, int size) =>
        new((origin.X + size * 0.5f) * TileSize, (origin.Y + size * 0.5f) * TileSize);

    private void EnsureBeltVisual()
    {
        var belts = GetNode<Node2D>("Belts");
        foreach (var child in belts.GetChildren())
        {
            child.QueueFree();
        }

        _beltVisual = new MindustryBeltVisual { Name = "MindustryBelt" };
        belts.AddChild(_beltVisual);
    }

    private void RebuildBeltVisual()
    {
        if (_slice is null || _beltVisual is null)
        {
            return;
        }

        _beltVisual.ConfigureFromGrid(
            _slice.Belts,
            _slice.BeltDefinition.RateItemsPerSecond,
            TileSize);
    }

    private void SyncItemSprites()
    {
        if (_slice is null || _itemsLayer is null)
        {
            return;
        }

        var live = new HashSet<long>();
        foreach (var cell in _slice.Belts.Cells.Values)
        {
            foreach (var item in cell.Items)
            {
                live.Add(item.Id);
                if (!_itemSprites.TryGetValue(item.Id, out var sprite))
                {
                    var tex = item.ItemId == "iron-plate" ? _plateTex : _oreTex;
                    sprite = new Sprite2D
                    {
                        Texture = tex,
                        Centered = true,
                        Scale = new Vector2(0.7f, 0.7f),
                        ZIndex = 5,
                        Modulate = new Color(1.15f, 1.05f, 0.95f)
                    };
                    _itemsLayer.AddChild(sprite);
                    _itemSprites[item.Id] = sprite;
                }
                else if (item.ItemId == "iron-plate" && _plateTex is not null)
                {
                    sprite.Texture = _plateTex;
                }

                var from = CellCenter(cell.Position);
                var to = CellCenter(cell.Position.Step(cell.Direction));
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
        var plateName = _slice.Content.DisplayName("iron-plate");
        var oreStock = _slice.Wallet.MaterialCount("iron-ore");
        var plateStock = _slice.Wallet.MaterialCount("iron-plate");
        var onBelt = _slice.Belts.Cells.Values.Sum(c => c.Items.Count);
        var scroll = _beltVisual?.ScrollTiles ?? 0f;
        var miner = _slice.Miner;
        var progressPct = miner is null ? 0 : (int)(miner.Progress * 100f);
        var crafted = _slice.Smelters.Sum(s => s.ItemsCrafted);
        var smeltProg = _slice.Smelters.Count > 0 ? (int)(_slice.Smelters[0].Progress * 100f) : 0;
        var toolIt = _tool switch
        {
            BuildTool.Belt => "Nastro",
            BuildTool.Miner => "Minatore",
            BuildTool.Smelter => "Forno",
            _ => "?"
        };
        var dirIt = _placeDir switch
        {
            Direction.North => "Nord",
            Direction.East => "Est",
            Direction.South => "Sud",
            Direction.West => "Ovest",
            _ => "?"
        };

        _hud.Text =
            $"tIndustry Godot — Phase C  |  tool={toolIt}  dir={dirIt}  |  Nastro={_slice.BeltDefinition.Id}\n" +
            $"Minatore×{_slice.Miners.Count} prog={progressPct}%  |  Forno×{_slice.Smelters.Count} craft={smeltProg}% prodotti={crafted}  |  nastro={onBelt}\n" +
            $"Core: {oreName}={oreStock}  {plateName}={plateStock}  (consegnati={_slice.CoreDeliveredItems})  scroll={scroll:0.00}\n" +
            "1=nastro · 2=minatore · 3=forno · R=ruota · click=piazza · destro=rimuovi · WASD=pan · nessun combat";
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

        var mapPath = Path.Combine(destDir, "godot-port-fulltile-flush-map.png");
        var err = img.SavePng(mapPath);
        GD.Print(err == Error.Ok ? $"Screenshot: {mapPath}" : $"Screenshot failed: {err}");
        // Keep Phase C names updated too.
        img.SavePng(Path.Combine(destDir, "godot-port-phase-c-map.png"));

        // Zoom 1, camera on corner (10,8): cell is 64×64 about viewport center.
        var vpW = img.GetWidth();
        var vpH = img.GetHeight();
        var cell = 64;
        var cornerX0 = (vpW / 2) - (cell / 2);
        var cornerY0 = (vpH / 2) - (cell / 2);
        AssertCellOpaque(img, cornerX0, cornerY0, cell, "corner(10,8)");
        AssertCellOpaque(img, cornerX0 - cell, cornerY0, cell, "straight(9,8)");
        AssertNorthEdgeFlush(img, cornerX0 - cell, cornerX0 + cell, cornerY0);

        var closeSize = cell * 5;
        var alignClose = img.GetRegion(new Rect2I(
            (vpW - closeSize) / 2, (vpH - closeSize) / 2, closeSize, closeSize));
        var flushClose = Path.Combine(destDir, "godot-port-fulltile-flush-close.png");
        err = alignClose.SavePng(flushClose);
        GD.Print(err == Error.Ok ? $"Screenshot: {flushClose}" : $"Flush close failed: {err}");
        alignClose.SavePng(Path.Combine(destDir, "godot-port-corner-align-close.png"));

        var mapSize = cell * 8;
        var alignMap = img.GetRegion(new Rect2I(
            (vpW - mapSize) / 2, (vpH - mapSize) / 2, mapSize, mapSize));
        var flushMap = Path.Combine(destDir, "godot-port-fulltile-flush-elbow.png");
        err = alignMap.SavePng(flushMap);
        GD.Print(err == Error.Ok ? $"Screenshot: {flushMap}" : $"Flush elbow failed: {err}");

        var cropW = Math.Min(880, vpW);
        var cropH = Math.Min(560, vpH);
        var crop = img.GetRegion(new Rect2I((vpW - cropW) / 2, (vpH - cropH) / 2, cropW, cropH));
        crop.SavePng(Path.Combine(destDir, "godot-port-phase-c-close.png"));
        crop.SavePng(Path.Combine(destDir, "godot-port-fulltile-flush-overview.png"));
    }

    private static bool IsTerrain(Color c) =>
        c.G > c.R + 5f / 255f && c.G >= c.B && c.B < 60f / 255f && c.R < 55f / 255f;

    private static void AssertCellOpaque(Image img, int x0, int y0, int cell, string label)
    {
        var terrain = 0;
        for (var y = y0; y < y0 + cell; y++)
        {
            for (var x = x0; x < x0 + cell; x++)
            {
                if (x < 0 || y < 0 || x >= img.GetWidth() || y >= img.GetHeight())
                {
                    continue;
                }

                if (IsTerrain(img.GetPixel(x, y)))
                {
                    terrain++;
                }
            }
        }

        var total = cell * cell;
        GD.Print($"PixelCheck {label}: terrain={terrain}/{total} ({100.0 * terrain / total:F2}%)");
        if (terrain > 0)
        {
            GD.PushError($"FULLTILE FAIL {label}: terrain pixels inside cell (want 0).");
        }
    }

    private static void AssertNorthEdgeFlush(Image img, int x0, int x1, int yTop)
    {
        // Row yTop must be belt; row yTop-1 mostly terrain (tile edge).
        var beltOnEdge = 0;
        var span = 0;
        for (var x = x0; x < x1; x++)
        {
            if (x < 0 || x >= img.GetWidth() || yTop < 1 || yTop >= img.GetHeight())
            {
                continue;
            }

            span++;
            if (!IsTerrain(img.GetPixel(x, yTop)))
            {
                beltOnEdge++;
            }
        }

        GD.Print($"PixelCheck north-edge flush: beltOnEdge={beltOnEdge}/{span}");
        if (span > 0 && beltOnEdge < span * 0.95)
        {
            GD.PushError("FULLTILE FAIL north edge: belt does not sit on tile edge.");
        }
    }
}
