using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Building/belt/item node visuals and HUD text sync.</summary>
public partial class World
{
    private void EnsureBuildingsLayer()
    {
        if (HasNode("Buildings"))
        {
            _buildingsLayer = GetNode<Node2D>("Buildings");
            return;
        }

        _buildingsLayer = new Node2D { Name = "Buildings", ZIndex = BuildingsZ };
        AddChild(_buildingsLayer);
    }

    private void EnsureConstructionLayer()
    {
        _constructionLayer = GetNodeOrNull<ConstructionLayer>("Construction");
        if (_constructionLayer is not null)
        {
            return;
        }

        _constructionLayer = new ConstructionLayer { Name = "Construction", ZIndex = ConstructionZ, Effects = _effects };
        AddChild(_constructionLayer);
    }

    /// <summary>
    /// Plans, sites and drones. A deconstruction that starts takes its block away: only the layer of that
    /// block is rebuilt. Finished sites rebuild their layer through the host events (see OnSimEvent).
    /// </summary>
    private void SyncConstructionVisuals(float delta)
    {
        if (_constructionLayer is not null)
        {
            MarkStructuresDirty(_constructionLayer.Sync(_view, delta));
        }
    }

    // Last stalled site under the mouse and its shortfall: the tooltip text is rebuilt only when they change.
    private (long SiteId, CostEntry Missing)? _siteTipKey;
    private string? _siteTipText;

    /// <summary>
    /// The cell under the mouse from its last known position and the current camera, every frame: a zoom or a
    /// pan moves the world under a still mouse.
    /// </summary>
    private void SyncHover()
    {
        if (_hover is null)
        {
            return;
        }

        var cell = ScreenToCell(_hoverScreen);
        if (!_hover.Equals(cell))
        {
            _hover = cell;
        }
    }

    /// <summary>
    /// Hover on a site stalled for resources (not while the mouse is over the HUD): "Mancano: {n} {oggetto}", n being what its limiting cost line is
    /// still short of (<see cref="ConstructSite.Missing"/>); money shows as "$n" like the other cost texts.
    /// </summary>
    private void SyncSiteTooltip()
    {
        if (_hud is null)
        {
            return;
        }

        // The ghost is red on a wall, on deep water or on an ore its miner cannot dig: the bottom hint says why.
        _hud.SetPlacementHint(_view is not null
            && GetViewport().GuiGetHoveredControl() is null
            && _hover is { } ghostAt
            && GhostRefusal(ghostAt) is { } why
            && why is RefusalCode.Wall or RefusalCode.DeepWater or RefusalCode.NeedsAdvancedMiner
            ? Testi.Rifiuto(why)
            : null);

        if (_view is null
            || GetViewport().GuiGetHoveredControl() is not null
            || _hover is not { } hover
            || !_view.TryGetSiteAt(hover, out var site)
            || site.Deconstructing
            || site.Missing is not { } missing)
        {
            _siteTipKey = null;
            _hud.SetSiteTooltip(null, default);
            return;
        }

        if (_siteTipKey != (site.Id, missing))
        {
            _siteTipKey = (site.Id, missing);
            _siteTipText = Testi.F("cantiere.mancano", missing.ItemId is null
                ? $"${missing.Amount}"
                : $"{missing.Amount}× {Testi.Oggetto(missing.ItemId, _view.Content)}");
        }

        _hud.SetSiteTooltip(_siteTipText, _hoverScreen);
    }

    /// <summary>Asks for a rebuild of just the belt and/or building visuals named by <paramref name="layers"/>.</summary>
    private void MarkStructuresDirty(ConstructionLayer.StructureLayers layers)
    {
        _visualDirty |= layers.HasFlag(ConstructionLayer.StructureLayers.Belts);
        _buildingsDirty |= layers.HasFlag(ConstructionLayer.StructureLayers.Buildings);
    }

    private void RebuildBuildingVisuals()
    {
        if (_view is null || _buildingsLayer is null)
        {
            return;
        }

        foreach (var child in _buildingsLayer.GetChildren())
        {
            child.QueueFree();
        }

        AddCoreVisual();

        foreach (var miner in _view.Miners)
        {
            var node = MinerVisual.Create(miner, TileSize);
            node.Position = FootprintCenter(miner.Position, Miner.Size);
            _buildingsLayer.AddChild(node);
        }

        foreach (var smelter in _view.Smelters)
        {
            var node = SmelterVisual.Create(smelter, TileSize);
            node.Position = FootprintCenter(smelter.Position, Crafter.Size);
            _buildingsLayer.AddChild(node);
        }

        foreach (var assembler in _view.Assemblers)
        {
            var node = AssemblerVisual.Create(assembler, TileSize);
            node.Position = FootprintCenter(assembler.Position, Crafter.Size);
            _buildingsLayer.AddChild(node);
        }

        foreach (var gen in _view.Generators)
        {
            var node = GeneratorVisual.Create(gen, TileSize);
            node.Position = FootprintCenter(gen.Position, Generator.Size);
            _buildingsLayer.AddChild(node);
        }

        foreach (var ex in _view.Extractors)
        {
            var node = ExtractorVisual.Create(ex, TileSize);
            node.Position = FootprintCenter(ex.Position, Extractor.Size);
            _buildingsLayer.AddChild(node);
        }

        foreach (var pn in _view.PowerNodes)
        {
            var node = BuildingPad.Create(pn.DefinitionId, pn.Size, TileSize);
            node.Name = $"PowerNode_{pn.Position.X}_{pn.Position.Y}";
            node.Position = FootprintCenter(pn.Position, pn.Size);
            _buildingsLayer.AddChild(node);
        }

        foreach (var lamp in _view.Lamps)
        {
            var node = LampVisual.Create(lamp, TileSize);
            node.Position = FootprintCenter(lamp.Position, Lamp.Size);
            _buildingsLayer.AddChild(node);
        }
    }

    private void SyncBuildingVisuals(float delta)
    {
        if (_view is null || _buildingsLayer is null)
        {
            return;
        }

        // "Animazioni dei blocchi" off: the looping animations (drills, rotors, furnace mouths, Core light) get no
        // time and hold their frame; state changes (assembler press) still show.
        if (!_blockAnimations || ClientSettings.PlaytestForcesEffectsOff)
        {
            // Playtests too (like their other clocks): drills and rotors hold their first frame in the captures.
            delta = 0f;
        }

        if (!PlaytestRequested)
        {
            _coreLightClock = (_coreLightClock + delta) % 3600f;
        }

        if (_coreLight is not null && IsInstanceValid(_coreLight))
        {
            _coreLight.Texture = CoreLightFrame();
        }

        if (_coreEdge is not null && IsInstanceValid(_coreEdge))
        {
            _coreEdge.DefaultColor = UiColors.Core;
        }

        SyncCoreDust();

        foreach (var child in _buildingsLayer.GetChildren())
        {
            // A rebuild frees the old visuals at the end of the frame: they must not keep their emitters alive.
            if (child.IsQueuedForDeletion())
            {
                continue;
            }

            switch (child)
            {
                case MinerVisual minerVis:
                    foreach (var miner in _view.Miners)
                    {
                        if (miner.Position.Equals(minerVis.MinerOrigin))
                        {
                            minerVis.Sync(miner, delta, _effects);
                            break;
                        }
                    }

                    break;
                case ExtractorVisual exVis:
                    foreach (var ex in _view.Extractors)
                    {
                        if (ex.Position.Equals(exVis.Origin))
                        {
                            exVis.Sync(ex, _effects);
                            break;
                        }
                    }

                    break;
                case SmelterVisual smVis:
                    foreach (var sm in _view.Smelters)
                    {
                        if (sm.Position.Equals(smVis.Origin))
                        {
                            smVis.Sync(sm, delta, _effects);
                            break;
                        }
                    }

                    break;
                case AssemblerVisual asmVis:
                    foreach (var asm in _view.Assemblers)
                    {
                        if (asm.Position.Equals(asmVis.Origin))
                        {
                            asmVis.Sync(asm, delta, _effects);
                            break;
                        }
                    }

                    break;
                case LampVisual lampVis:
                    foreach (var lamp in _view.Lamps)
                    {
                        if (lamp.Position.Equals(lampVis.Origin))
                        {
                            lampVis.Sync(lamp);
                            break;
                        }
                    }

                    break;
                case GeneratorVisual genVis:
                    foreach (var gen in _view.Generators)
                    {
                        if (gen.Position.Equals(genVis.Origin))
                        {
                            genVis.Sync(gen, delta, _effects);
                            break;
                        }
                    }

                    break;
            }
        }
    }

    /// <summary>Shortest time between two dust puffs of the Core, however many items it takes in.</summary>
    private const double CoreDustSeconds = 0.35;

    // Items the Core had taken in at the last frame (-1 = not read yet in this game) and when it may puff again.
    private long _coreDeliveredSeen = -1;
    private double _nextCoreDust;

    /// <summary>
    /// Dust over the Core when items enter it (G3 effect <c>polvere-core</c>): the simulation raises no event for a
    /// delivery, so the client watches the <see cref="IGameView.CoreDeliveredItems"/> counter and puffs, rate-limited,
    /// when it grows.
    /// </summary>
    private void SyncCoreDust()
    {
        if (_view is null || _effects is null || _view.CoreTiles.Count == 0)
        {
            return;
        }

        var delivered = _view.CoreDeliveredItems;
        var grew = _coreDeliveredSeen >= 0 && delivered > _coreDeliveredSeen;
        _coreDeliveredSeen = delivered;
        if (!grew || _effects.Clock < _nextCoreDust)
        {
            return;
        }

        _nextCoreDust = _effects.Clock + CoreDustSeconds;
        var tiles = _view.CoreTiles;
        var min = new Vector2(tiles.Min(t => t.X), tiles.Min(t => t.Y)) * TileSize;
        var max = new Vector2(tiles.Max(t => t.X) + 1, tiles.Max(t => t.Y) + 1) * TileSize;
        _effects.Play("polvere-core", (min + max) * 0.5f, 0f, (max - min) * 0.45f);
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
        if (_view is null || _beltVisual is null)
        {
            return;
        }

        _beltVisual.ConfigureFromGrid(_view, TileSize);
    }

    private void SyncItemSprites()
    {
        if (_view is null || _itemsLayer is null)
        {
            return;
        }

        var live = new HashSet<long>();
        foreach (var cell in _view.Belts.Values)
        {
            foreach (var item in cell.Items)
            {
                live.Add(item.Id);
                if (!_itemSprites.TryGetValue(item.Id, out var sprite))
                {
                    // Above every belt piece (≤5), below buildings (BuildingsZ + 3): items ride on top of
                    // the chevrons and slide under the Core / machines at the hand-off. The belt sprite is
                    // drawn in its own colours with its own dark outline, at the world art scale.
                    sprite = new Sprite2D
                    {
                        Name = $"Item_{item.Id}",
                        Texture = Sprites.BeltItem(item.ItemId),
                        Centered = true,
                        Scale = Vector2.One * BeltItemScale,
                        ZIndex = ItemsZ
                    };
                    _itemsLayer.AddChild(sprite);
                    _itemSprites[item.Id] = sprite;
                }

                var from = CellCenter(cell.Position);
                var to = CellCenter(cell.Position.Step(cell.Direction));
                var pos = from.Lerp(to, Mathf.Clamp(item.Progress, 0f, 1f));
                sprite.Position = pos;
                // Corner and T = galleria: items disappear while inside the turn or the merge.
                sprite.Visible = _view.GetBeltShape(cell.Position) == BeltShape.Straight;
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
        if (_hud is null || _view is null)
        {
            return;
        }

        var onBelt = _view.Belts.Values.Sum(c => c.Items.Count);
        var gensLive = _view.Generators.Count(g => g.IsGenerating);
        _hud.BindEconomy(_view.Content, _view.Wallet);
        _hud.SyncCoreUpgrade(_view);
        _hud.UpdateStock(
            _view.Wallet.MaterialCount("iron-ore"),
            _view.Wallet.MaterialCount("iron-plate"),
            _view.Wallet.MaterialCount("copper-ore"),
            _view.Wallet.MaterialCount("copper-wire"),
            _view.CoreDeliveredItems,
            onBelt,
            _view.Wallet.Money,
            gensLive,
            _view.Generators.Count,
            _view.PowerBuffer,
            _view.PowerCapacity);
        _hud.SetDirectionLabel(_placeDir);
        _hud.SyncBuildQueue(_view);
        if (_activeLevel is not null)
        {
            _hud.UpdateObjectives(
                _activeLevel,
                _view.Wallet,
                _view.Session,
                _view.Research);
        }
        else
        {
            _hud.ClearObjectives();
        }
    }


    internal static Vector2 CellCenter(GridPosition cell) =>
        new((cell.X + 0.5f) * TileSize, (cell.Y + 0.5f) * TileSize);

    /// <summary>Belt item sprites (10 px) at the world art scale: about 20 px on a 64 px tile.</summary>
    private const float BeltItemScale = (float)TileSize / Sprites.PixelsPerCell;

    /// <summary>Draw order: belts (0–5) &lt; items &lt; buildings (+ their own child layers).</summary>
    private const int ItemsZ = 7;
    private const int BuildingsZ = 8;

    /// <summary>Above buildings and their own child layers (≤ BuildingsZ + 6); the HUD is a CanvasLayer on top.</summary>
    private const int ConstructionZ = 20;

    /// <summary>The Core in game is tier 1 (2×2); tiers 2–4 exist only as sprites until the tier sub-project.</summary>
    private const string CoreSpriteId = "core-1";

    /// <summary>Slow pulse of the nucleus light: 4 frames, looped.</summary>
    private const float CoreLightFramesPerSecond = 2.5f;

    /// <summary>
    /// World clock of the nucleus light: it outlives <see cref="RebuildBuildingVisuals"/>, so the pulse does not
    /// restart on every build. It stays at 0 in playtests, so screenshots always show the same frame.
    /// </summary>
    private float _coreLightClock;

    private Sprite2D? _coreLight;
    // The Core's outline, in UiColors.Core (set every frame: a new colour variant shows at once).
    private Line2D? _coreEdge;

    /// <summary>Start phase of the nucleus light from the Core's origin cell (0 in playtests).</summary>
    private float _coreLightPhase;

    /// <summary>
    /// CORE as a building node (z above belt items): the <c>core-1</c> body, its nucleus light looping slowly on top
    /// (each frame is an opaque disc that replaces the nucleus) and the cyan outline.
    /// </summary>
    private void AddCoreVisual()
    {
        if (_view is null || _buildingsLayer is null || _view.CoreTiles.Count == 0)
        {
            return;
        }

        var minX = _view.CoreTiles.Min(t => t.X);
        var minY = _view.CoreTiles.Min(t => t.Y);
        _coreLightPhase = BuildingPad.Phase(new GridPosition(minX, minY));
        var size = new Vector2(
            (_view.CoreTiles.Max(t => t.X) - minX + 1) * TileSize,
            (_view.CoreTiles.Max(t => t.Y) - minY + 1) * TileSize);
        var root = new Node2D
        {
            Name = "Core",
            ZIndex = 3,
            Position = new Vector2(minX * TileSize, minY * TileSize)
        };
        var body = Sprites.For(CoreSpriteId);
        BuildingPad.AddShadow(root, body, size / body.GetSize());
        root.AddChild(new Sprite2D
        {
            Name = "Body",
            Texture = body,
            Centered = false,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            Scale = size / body.GetSize()
        });
        _coreLight = new Sprite2D
        {
            Name = "Light",
            Texture = CoreLightFrame(),
            Centered = false,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            Scale = size / body.GetSize(),
            SelfModulate = new Color(BuildingPad.GlowBoost, BuildingPad.GlowBoost, BuildingPad.GlowBoost)
        };
        root.AddChild(_coreLight);

        _coreEdge = new Line2D
        {
            Points = [Vector2.Zero, new(size.X, 0), size, new(0, size.Y)],
            Closed = true,
            Width = 2f,
            DefaultColor = UiColors.Core
        };
        root.AddChild(_coreEdge);
        _buildingsLayer.AddChild(root);
    }

    /// <summary>
    /// Frame of the nucleus light for the world clock plus the Core's phase (held on frame index 0 in playtests:
    /// clock and phase stay 0, so captures are stable).
    /// </summary>
    private Texture2D CoreLightFrame()
    {
        var frames = Sprites.Frames(CoreSpriteId + "-light");
        return frames[(int)((_coreLightClock + _coreLightPhase) * CoreLightFramesPerSecond) % frames.Length];
    }
}
