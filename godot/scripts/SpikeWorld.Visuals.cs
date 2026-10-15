using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Building/belt/item node visuals and HUD text sync.</summary>
public partial class SpikeWorld
{
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
            var node = MinerVisual.Create(miner, TileSize);
            node.Position = FootprintCenter(miner.Position, MinerProducer.Size);
            _buildingsLayer.AddChild(node);
        }

        foreach (var smelter in _slice.Smelters)
        {
            var node = SmelterVisual.Create(smelter, TileSize);
            node.Position = FootprintCenter(smelter.Position, SmelterStub.Size);
            _buildingsLayer.AddChild(node);
        }

        foreach (var assembler in _slice.Assemblers)
        {
            var node = AssemblerVisual.Create(assembler, TileSize);
            node.Position = FootprintCenter(assembler.Position, SmelterStub.Size);
            _buildingsLayer.AddChild(node);
        }

        foreach (var gen in _slice.Generators)
        {
            var node = GeneratorVisual.Create(gen, TileSize);
            node.Position = FootprintCenter(gen.Position, GeneratorStub.Size);
            _buildingsLayer.AddChild(node);
        }

        foreach (var ex in _slice.Extractors)
        {
            var node = ExtractorVisual.Create(ex, TileSize);
            node.Position = FootprintCenter(ex.Position, ExtractorStub.Size);
            _buildingsLayer.AddChild(node);
        }

        foreach (var pn in _slice.PowerNodes)
        {
            var t2 = pn.DefinitionId == PowerNodeStub.Tier2Id;
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
        if (_slice is null || _buildingsLayer is null)
        {
            return;
        }

        foreach (var child in _buildingsLayer.GetChildren())
        {
            switch (child)
            {
                case MinerVisual minerVis:
                    foreach (var miner in _slice.Miners)
                    {
                        if (miner.Position.Equals(minerVis.MinerOrigin))
                        {
                            minerVis.Sync(miner, delta);
                            break;
                        }
                    }

                    break;
                case ExtractorVisual exVis:
                    foreach (var ex in _slice.Extractors)
                    {
                        if (ex.Position.Equals(exVis.Origin))
                        {
                            exVis.Sync(ex);
                            break;
                        }
                    }

                    break;
                case SmelterVisual smVis:
                    foreach (var sm in _slice.Smelters)
                    {
                        if (sm.Position.Equals(smVis.Origin))
                        {
                            smVis.Sync(sm, delta);
                            break;
                        }
                    }

                    break;
                case AssemblerVisual asmVis:
                    foreach (var asm in _slice.Assemblers)
                    {
                        if (asm.Position.Equals(asmVis.Origin))
                        {
                            asmVis.Sync(asm, delta);
                            break;
                        }
                    }

                    break;
                case GeneratorVisual genVis:
                    foreach (var gen in _slice.Generators)
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
                    var holder = new Node2D
                    {
                        Name = $"Item_{item.Id}",
                        ZIndex = 5
                    };
                    // Opaque disc so PNG alpha never shows the belt through the item.
                    var back = new Polygon2D
                    {
                        Name = "Back",
                        Color = new Color(0.1f, 0.12f, 0.14f, 1f),
                        Polygon =
                        [
                            new(-14, -14), new(14, -14), new(14, 14), new(-14, 14)
                        ]
                    };
                    holder.AddChild(back);
                    var tex = ResolveItemTexture(item.ItemId);
                    sprite = new Sprite2D
                    {
                        Name = "Sprite",
                        Texture = tex,
                        Centered = true,
                        Scale = new Vector2(0.85f, 0.85f),
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
                    }

                    sprite.Modulate = ItemPalette.ColorFor(item.ItemId);
                }

                var from = CellCenter(cell.Position);
                var to = CellCenter(cell.Position.Step(cell.Direction));
                var pos = from.Lerp(to, Mathf.Clamp(item.Progress, 0f, 1f));
                var holderNode = sprite.GetParent() as Node2D ?? sprite;
                holderNode.Position = pos;
                // Corner = galleria: items disappear while inside the turn.
                holderNode.Visible = !_slice.Belts.IsCorner(cell.Position);
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
        if (_hud is null || _slice is null)
        {
            return;
        }

        var onBelt = _slice.Belts.Cells.Values.Sum(c => c.Items.Count);
        var gensLive = _slice.Generators.Count(g => g.IsGenerating);
        _hud.BindEconomy(_slice.Content, _slice.Wallet);
        _hud.SyncCoreUpgrade(_slice);
        _hud.UpdateStock(
            ShortName(_slice.Content.DisplayName("iron-ore")),
            _slice.Wallet.MaterialCount("iron-ore"),
            ShortName(_slice.Content.DisplayName("iron-plate")),
            _slice.Wallet.MaterialCount("iron-plate"),
            ShortName(_slice.Content.DisplayName("copper-ore")),
            _slice.Wallet.MaterialCount("copper-ore"),
            ShortName(_slice.Content.DisplayName("copper-wire")),
            _slice.Wallet.MaterialCount("copper-wire"),
            _slice.CoreDeliveredItems,
            onBelt,
            _slice.Wallet.Money,
            gensLive,
            _slice.Generators.Count,
            _slice.PowerBuffer,
            _slice.PowerCapacity);
        _hud.SetDirectionLabel(DirectionIt(_placeDir));
        if (_activeLevel is not null)
        {
            _hud.UpdateObjectives(
                _activeLevel,
                _slice.Wallet,
                _slice.Session,
                _slice.Research,
                id => ShortName(_slice.Content.DisplayName(id)));
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
}
