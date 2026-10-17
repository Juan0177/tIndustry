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

        _constructionLayer = new ConstructionLayer { Name = "Construction", ZIndex = ConstructionZ };
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
    /// Hover on a site stalled for resources: "Mancano: {n} {oggetto}", n being what its limiting cost line is
    /// still short of (<see cref="ConstructSite.Missing"/>); money shows as "$n" like the other cost texts.
    /// </summary>
    private void SyncSiteTooltip()
    {
        if (_hud is null)
        {
            return;
        }

        if (_view is null
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
            _siteTipText = missing.ItemId is null
                ? $"Mancano: ${missing.Amount}"
                : $"Mancano: {missing.Amount} {_view.Content.DisplayName(missing.ItemId)}";
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
            var t2 = pn.DefinitionId == PowerNode.Tier2Id;
            var node = BuildingPad.Create(
                pn.Size,
                TileSize,
                fill: new Color(0.2f, 0.18f, 0.1f, 1f),
                border: new Color(0.95f, 0.85f, 0.35f, 1f),
                icon: GD.Load<Texture2D>(t2
                    ? "res://assets/power-node-t2.png"
                    : "res://assets/power-node.png"));
            node.Name = $"PowerNode_{pn.Position.X}_{pn.Position.Y}";
            node.Position = FootprintCenter(pn.Position, pn.Size);
            _buildingsLayer.AddChild(node);
        }
    }

    private void SyncBuildingVisuals(float delta)
    {
        if (_view is null || _buildingsLayer is null)
        {
            return;
        }

        foreach (var child in _buildingsLayer.GetChildren())
        {
            switch (child)
            {
                case MinerVisual minerVis:
                    foreach (var miner in _view.Miners)
                    {
                        if (miner.Position.Equals(minerVis.MinerOrigin))
                        {
                            minerVis.Sync(miner, delta);
                            break;
                        }
                    }

                    break;
                case ExtractorVisual exVis:
                    foreach (var ex in _view.Extractors)
                    {
                        if (ex.Position.Equals(exVis.Origin))
                        {
                            exVis.Sync(ex);
                            break;
                        }
                    }

                    break;
                case SmelterVisual smVis:
                    foreach (var sm in _view.Smelters)
                    {
                        if (sm.Position.Equals(smVis.Origin))
                        {
                            smVis.Sync(sm, delta);
                            break;
                        }
                    }

                    break;
                case AssemblerVisual asmVis:
                    foreach (var asm in _view.Assemblers)
                    {
                        if (asm.Position.Equals(asmVis.Origin))
                        {
                            asmVis.Sync(asm, delta);
                            break;
                        }
                    }

                    break;
                case GeneratorVisual genVis:
                    foreach (var gen in _view.Generators)
                    {
                        if (gen.Position.Equals(genVis.Origin))
                        {
                            genVis.Sync(gen, delta);
                            break;
                        }
                    }

                    break;
            }
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
        if (_view is null || _beltVisual is null)
        {
            return;
        }

        _beltVisual.ConfigureFromGrid(
            _view,
            _view.BeltDefinition.RateItemsPerSecond,
            TileSize);
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
                    // the chevrons and slide under the Core / machines at the hand-off.
                    var holder = new Node2D
                    {
                        Name = $"Item_{item.Id}",
                        ZIndex = ItemsZ
                    };
                    // Solid silhouette behind the icon: an outline ring plus an opaque fill, so the
                    // belt chevrons never show through the icon's see-through facets.
                    var solid = ItemSilhouette.Solid(ResolveItemTexture(item.ItemId));
                    var dark = ItemPalette.OutlineFor(item.ItemId).Darkened(0.35f);
                    holder.AddChild(new Sprite2D
                    {
                        Name = "Outline",
                        Texture = solid,
                        Centered = true,
                        Scale = new Vector2(ItemScale * 1.2f, ItemScale * 1.2f),
                        Modulate = dark
                    });
                    holder.AddChild(new Sprite2D
                    {
                        Name = "Fill",
                        Texture = solid,
                        Centered = true,
                        Scale = new Vector2(ItemScale, ItemScale),
                        Modulate = dark
                    });
                    var tex = ResolveItemTexture(item.ItemId);
                    sprite = new Sprite2D
                    {
                        Name = "Sprite",
                        Texture = tex,
                        Centered = true,
                        Scale = new Vector2(ItemScale, ItemScale),
                        Modulate = ItemPalette.ColorFor(item.ItemId)
                    };
                    holder.AddChild(sprite);
                    _itemsLayer.AddChild(holder);
                    _itemSprites[item.Id] = sprite;
                }
                else
                {
                    var tex = ResolveItemTexture(item.ItemId);
                    if (tex is not null)
                    {
                        sprite.Texture = tex;
                        var solidTex = ItemSilhouette.Solid(tex);
                        foreach (var name in new[] { "Outline", "Fill" })
                        {
                            if (sprite.GetParent()?.GetNodeOrNull<Sprite2D>(name) is { } layer)
                            {
                                layer.Texture = solidTex;
                            }
                        }
                    }

                    sprite.Modulate = ItemPalette.ColorFor(item.ItemId);
                }

                var from = CellCenter(cell.Position);
                var to = CellCenter(cell.Position.Step(cell.Direction));
                var pos = from.Lerp(to, Mathf.Clamp(item.Progress, 0f, 1f));
                var holderNode = sprite.GetParent() as Node2D ?? sprite;
                holderNode.Position = pos;
                // Corner = galleria: items disappear while inside the turn.
                holderNode.Visible = !_view.IsBeltCorner(cell.Position);
            }
        }

        var dead = _itemSprites.Keys.Where(id => !live.Contains(id)).ToList();
        foreach (var id in dead)
        {
            var sprite = _itemSprites[id];
            var parent = sprite.GetParent();
            if (parent is not null && parent != _itemsLayer)
            {
                parent.QueueFree();
            }
            else
            {
                sprite.QueueFree();
            }

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
            ShortName(_view.Content.DisplayName("iron-ore")),
            _view.Wallet.MaterialCount("iron-ore"),
            ShortName(_view.Content.DisplayName("iron-plate")),
            _view.Wallet.MaterialCount("iron-plate"),
            ShortName(_view.Content.DisplayName("copper-ore")),
            _view.Wallet.MaterialCount("copper-ore"),
            ShortName(_view.Content.DisplayName("copper-wire")),
            _view.Wallet.MaterialCount("copper-wire"),
            _view.CoreDeliveredItems,
            onBelt,
            _view.Wallet.Money,
            gensLive,
            _view.Generators.Count,
            _view.PowerBuffer,
            _view.PowerCapacity);
        _hud.SetDirectionLabel(DirectionIt(_placeDir));
        _hud.SyncBuildQueue(_view);
        if (_activeLevel is not null)
        {
            _hud.UpdateObjectives(
                _activeLevel,
                _view.Wallet,
                _view.Session,
                _view.Research,
                id => ShortName(_view.Content.DisplayName(id)));
        }
        else
        {
            _hud.ClearObjectives();
        }
    }

    private static string ShortName(string display)
    {
        // Compact stock labels: "Lastra di ferro" → "Lastra ferro"
        return display
            .Replace(" di ", " ", StringComparison.Ordinal)
            .Replace("grezzo", "grezzo", StringComparison.Ordinal);
    }

    private Texture2D? ResolveItemTexture(string itemId) => itemId switch
    {
        "iron-plate" => _plateTex,
        "copper-ore" => _copperOreTex,
        "copper-wire" => _copperWireTex,
        "coal" => _coalTex,
        "lead-ore" => _leadOreTex,
        "titanium-ore" => _titaniumOreTex,
        _ => _oreTex
    };

    private static Vector2 CellCenter(GridPosition cell) =>
        new((cell.X + 0.5f) * TileSize, (cell.Y + 0.5f) * TileSize);

    /// <summary>Belt items at ~half a tile, like Mindustry (texture is 64px = 1 tile).</summary>
    private const float ItemScale = 0.55f;

    /// <summary>Draw order: belts (0–5) &lt; items &lt; buildings (+ their own child layers).</summary>
    private const int ItemsZ = 7;
    private const int BuildingsZ = 8;

    /// <summary>Above buildings and their own child layers (≤ BuildingsZ + 6); the HUD is a CanvasLayer on top.</summary>
    private const int ConstructionZ = 20;

    /// <summary>CORE sprite + outline as a building node (z above belt items).</summary>
    private void AddCoreVisual()
    {
        if (_view is null || _buildingsLayer is null || _view.CoreTiles.Count == 0)
        {
            return;
        }

        var minX = _view.CoreTiles.Min(t => t.X);
        var minY = _view.CoreTiles.Min(t => t.Y);
        var size = new Vector2(
            (_view.CoreTiles.Max(t => t.X) - minX + 1) * TileSize,
            (_view.CoreTiles.Max(t => t.Y) - minY + 1) * TileSize);
        var root = new Node2D
        {
            Name = "Core",
            ZIndex = 3,
            Position = new Vector2(minX * TileSize, minY * TileSize)
        };
        if (_coreTex is not null)
        {
            root.AddChild(new Sprite2D
            {
                Texture = _coreTex,
                Centered = false,
                Scale = size / _coreTex.GetSize()
            });
        }
        else
        {
            root.AddChild(new Polygon2D
            {
                Color = new Color(0.18f, 0.32f, 0.48f, 1f),
                Polygon = [Vector2.Zero, new(size.X, 0), size, new(0, size.Y)]
            });
        }

        root.AddChild(new Line2D
        {
            Points = [Vector2.Zero, new(size.X, 0), size, new(0, size.Y)],
            Closed = true,
            Width = 2f,
            DefaultColor = new Color(0.35f, 0.78f, 0.9f, 1f)
        });
        _buildingsLayer.AddChild(root);
    }
}
