using Godot;
using TIndustry.Shared;
using static TIndustry.Godot.HudStyle;
using ToolKind = TIndustry.Godot.Hud.ToolKind;

namespace TIndustry.Godot;

/// <summary>
/// Reserved strip above the palette (fixed height, invisible when empty) showing the focused
/// block's name, hotkey and cost chips, plus the centered "?" detail overlay.
/// </summary>
public partial class BlockInfoPanel : Control
{
    /// <summary>Reserved height above the palette for the hover/select strip (empty = invisible).</summary>
    private const float InfoSlotHeight = 88f;

    private PanelContainer? _infoPanel;
    private Label? _infoName;
    private Control? _detailButton;
    private Label? _infoHotkey;
    private HBoxContainer? _infoCostRow;
    private PanelContainer? _detailOverlay;
    private Label? _detailBody;
    private FactoryContent? _content;
    private IWalletView? _wallet;
    private ToolKind? _detailFocus;
    private (ToolKind? Focus, bool Locked) _shown;

    /// <summary>The "?" button was clicked; the HUD answers with <see cref="OpenDetail"/>.</summary>
    public event Action? DetailRequested;

    /// <summary>The strip's name line and the "?" tooltip as shown (playtests).</summary>
    internal string StripName => _infoName?.Text ?? "";

    internal string DetailTooltip => Testi.T(_detailButton?.TooltipText ?? "");

    /// <summary>Last shown stock and funds, used for costs when no wallet is bound.</summary>
    public CorePanel? Stock { get; set; }

    public BlockInfoPanel()
    {
        Name = "InfoSlot";
        CustomMinimumSize = new Vector2(Hud.CornerStackWidth, InfoSlotHeight);
        SizeFlagsHorizontal = SizeFlags.Fill;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Ready()
    {
        _infoPanel = MakePanel("BlockInfo");
        _infoPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _infoPanel.Visible = false;
        _infoPanel.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_infoPanel);

        var infoRoot = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _infoPanel.AddChild(infoRoot);

        var header = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, ThemeTypeVariation = UiTheme.RigaLarga };
        infoRoot.AddChild(header);

        _infoName = MakeLabel();
        _infoName.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(_infoName);

        _infoHotkey = MakeLabel("", UiTheme.TestoTenue);
        header.AddChild(_infoHotkey);

        var infoBtn = new PanelContainer
        {
            CustomMinimumSize = new Vector2(28, 28),
            MouseFilter = MouseFilterEnum.Stop,
            TooltipText = "blocco.dettaglio",
            ThemeTypeVariation = UiTheme.Slot
        };
        header.AddChild(infoBtn);
        _detailButton = infoBtn;
        var infoCenter = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        infoBtn.AddChild(infoCenter);
        var q = MakeLabel("?", UiTheme.TestoAccento);
        q.HorizontalAlignment = HorizontalAlignment.Center;
        infoCenter.AddChild(q);
        infoBtn.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                DetailRequested?.Invoke();
                AcceptEvent();
            }
        };

        _infoCostRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        infoRoot.AddChild(_infoCostRow);
    }

    /// <summary>Bind content (names, recipes, costs) and wallet (have / missing materials).</summary>
    public void Bind(FactoryContent? content, IWalletView? wallet)
    {
        _content = content;
        _wallet = wallet;
    }

    /// <summary>Fill the strip for <paramref name="focus"/>, or empty it when null / not a palette block.</summary>
    public void Show(ToolKind? focus, bool locked)
    {
        _shown = (focus, locked);
        if (_infoPanel is null || _infoName is null || _infoCostRow is null)
        {
            return;
        }

        var entry = focus is null ? null : BuildPalette.FindEntry(focus.Value);
        if (entry is null)
        {
            SetInfoSlotFilled(false);
            return;
        }

        SetInfoSlotFilled(true);
        var name = BuildPalette.DisplayName(entry, _content);
        _infoName.Text = locked ? Testi.F("blocco.bloccato", name) : name;
        if (_infoHotkey is not null)
        {
            _infoHotkey.Text = string.IsNullOrEmpty(entry.Hotkey) ? "" : $"[{entry.Hotkey}]";
        }

        foreach (var child in _infoCostRow.GetChildren())
        {
            child.QueueFree();
        }

        ResolveCost(entry.StructureId, out var money, out var materials);
        if (money <= 0 && materials.Count == 0)
        {
            _infoCostRow.AddChild(MakeLabel("—", UiTheme.TestoTenue));
            return;
        }

        foreach (var mat in materials.Where(m => m.Amount > 0))
        {
            var have = _wallet?.MaterialCount(mat.ItemId)
                ?? Stock?.StockCount(mat.ItemId) ?? 0;
            var ok = have >= mat.Amount;
            _infoCostRow.AddChild(MakeCostChip(mat.ItemId, mat.Amount, ok, _content));
        }

        if (money > 0)
        {
            var cashOk = (_wallet?.Money ?? Stock?.Money ?? 0) >= money;
            _infoCostRow.AddChild(MakeMoneyChip(money, cashOk));
        }
    }

    /// <summary>Builds the centered detail overlay; the HUD adds it to its own root.</summary>
    public PanelContainer BuildDetailOverlay()
    {
        var overlay = MakePanel("BlockDetail");
        overlay.ThemeTypeVariation = UiTheme.Scheda;
        _detailOverlay = overlay;
        CardLayout.Center(overlay, new Vector2(520, 360));
        overlay.Visible = false;
        overlay.MouseFilter = MouseFilterEnum.Stop;

        var vbox = new VBoxContainer { ThemeTypeVariation = UiTheme.ColonnaLarga };
        overlay.AddChild(vbox);

        vbox.AddChild(MakeLabel("blocco.dettaglio", UiTheme.Titolo));

        _detailBody = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        vbox.AddChild(_detailBody);

        var close = new PanelContainer
        {
            CustomMinimumSize = new Vector2(100, 32),
            MouseFilter = MouseFilterEnum.Stop,
            ThemeTypeVariation = UiTheme.Slot
        };
        vbox.AddChild(close);
        var cc = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        close.AddChild(cc);
        cc.AddChild(MakeLabel("comune.chiudi"));
        close.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                overlay.Visible = false;
                AcceptEvent();
            }
        };

        return overlay;
    }

    /// <summary>Open the detail overlay for <paramref name="focus"/> (no-op if it is not a palette block).</summary>
    public void OpenDetail(ToolKind focus)
    {
        if (_detailOverlay is null || _detailBody is null)
        {
            return;
        }

        var entry = BuildPalette.FindEntry(focus);
        if (entry is null)
        {
            return;
        }

        _detailFocus = focus;
        var name = BuildPalette.DisplayName(entry, _content);
        ResolveCost(entry.StructureId, out var money, out var materials);
        var costParts = materials
            .Where(m => m.Amount > 0)
            .Select(m =>
            {
                var have = _wallet?.MaterialCount(m.ItemId) ?? 0;
                var mark = Testi.T(have >= m.Amount ? "blocco.costo.ok" : "blocco.costo.manca");
                return $"{m.Amount}× {Testi.Oggetto(m.ItemId, _content)} ({mark})";
            });
        var costLine = string.Join(", ", costParts);
        if (money > 0)
        {
            costLine = string.IsNullOrEmpty(costLine) ? $"${money}" : $"{costLine}, ${money}";
        }

        if (string.IsNullOrEmpty(costLine))
        {
            costLine = "—";
        }

        _detailBody.Text = Testi.F("blocco.dettaglio.testo",
            name, BuildPalette.Hint(entry, _content), ResolveIoLine(entry.StructureId), costLine, entry.Hotkey);
        _detailOverlay.Visible = true;
    }

    public override void _Notification(int what)
    {
        if (what != NotificationTranslationChanged)
        {
            return;
        }

        // New language: the strip and the open detail are written again.
        Show(_shown.Focus, _shown.Locked);
        if (_detailOverlay is { Visible: true } && _detailFocus is { } focus)
        {
            OpenDetail(focus);
        }
    }

    private void SetInfoSlotFilled(bool filled)
    {
        if (_infoPanel is null)
        {
            return;
        }

        _infoPanel.Visible = filled;
        _infoPanel.MouseFilter = filled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
    }

    private void ResolveCost(string structureId, out int money, out IReadOnlyList<ResourceAmount> materials)
    {
        money = 0;
        materials = [];
        if (_content is null || string.IsNullOrEmpty(structureId))
        {
            return;
        }

        var building = _content.FindBuilding(structureId);
        if (building is not null)
        {
            money = building.MoneyCost;
            materials = building.BuildCost;
            return;
        }

        var conveyor = _content.FindConveyor(structureId);
        if (conveyor is not null)
        {
            money = conveyor.MoneyCost;
            materials = conveyor.EffectiveBuildCost;
        }
    }

    private string ResolveIoLine(string structureId)
    {
        if (_content is null || string.IsNullOrEmpty(structureId))
        {
            return "";
        }

        var recipe = DefaultRecipe(structureId, _content);
        if (recipe is null)
        {
            if (BlockIoLine(structureId, _content) is { Length: > 0 } line)
            {
                return line;
            }

            return structureId switch
            {
                "miner" or Miner.ImpactDrillId => Testi.T("blocco.io.minatore"),
                "conveyor-basic" or "junction" or "router" or "sorter" or "conveyor-bridge"
                    => Testi.T("blocco.io.trasporto"),
                _ => ""
            };
        }

        string Fmt(IReadOnlyList<ResourceAmount> list) =>
            string.Join("+", list.Select(a =>
                $"{a.Amount}×{Testi.Oggetto(a.ItemId, _content)}"));

        var outputs = recipe.RandomOutput ? RandomOutputs(recipe, _content, "/") : Fmt(recipe.Outputs);
        return $"I/O · {Fmt(recipe.Inputs)} → {outputs} ({Testi.Secondi(RecipeSeconds(recipe, structureId, _content))})";
    }

    /// <summary>
    /// The I/O line of a block that crafts nothing but has numbers of its own (the block info and the research tree
    /// show it): what the gates do, the stack conveyor's pack and speed, the long bridge's reach and power, the solar
    /// panels' output by day, what the generators burn and make, the batteries' capacity; all from the content.
    /// Empty for any other block.
    /// </summary>
    internal static string BlockIoLine(string structureId, FactoryContent content)
    {
        static string Number(double value) =>
            Testi.Decimale(value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));

        if (content.FindConveyor(structureId) is { } conveyor)
        {
            return conveyor.Kind switch
            {
                LogisticsKind.Gate => Testi.T(conveyor.Inverted ? "blocco.io.gate.underflow" : "blocco.io.gate.overflow"),
                LogisticsKind.Stack => Testi.F(
                    "blocco.io.pacchi", BeltGrid.StackPackSize, Number(conveyor.RateItemsPerSecond)),
                LogisticsKind.Bridge when conveyor.IsPoweredBridge => Testi.F(
                    "blocco.io.ponte", conveyor.MaxSpan, Number(conveyor.PowerUse)),
                _ => ""
            };
        }

        if (Generator.IsGeneratorId(structureId))
        {
            var fuels = string.Join(" / ", Generator.FuelsOf(content, structureId).Select(f =>
                Testi.F("blocco.io.carburante", Testi.Oggetto(f.ItemId, content), Testi.Secondi(f.Seconds))));
            return Testi.F("blocco.io.generatore", fuels, Number(Generator.OutputRateOf(content, structureId) / 1000.0));
        }

        if (SolarPanel.IsSolarId(structureId) && content.FindBuilding(structureId) is { } panel)
        {
            return Testi.F("blocco.io.solare", Number(panel.PowerOutput));
        }

        if (Battery.IsBatteryId(structureId))
        {
            return Testi.F(
                "blocco.io.batteria", Battery.CapacityOf(content, structureId) / BuildingDefinition.ChargePerUnit);
        }

        return "";
    }

    /// <summary>
    /// The recipe whose I/O the block info and the research tree show for <paramref name="structureId"/>: the one a
    /// new machine starts on (the forno's iron, the assembler's copper wire, the first recipe of any other craft
    /// machine, as <c>Factory.PlaceCrafter</c> picks it), the recipe a recipe node unlocks (the circuit); null for
    /// a block that crafts nothing.
    /// </summary>
    internal static RecipeDefinition? DefaultRecipe(string structureId, FactoryContent content)
    {
        if (structureId == Crafter.SmelterBuildingId)
        {
            return content.FindRecipe("smelt-iron");
        }

        if (structureId == Crafter.AssemblerBuildingId)
        {
            return content.FindRecipe("craft-copper-wire");
        }

        if (content.FindBuilding(structureId) is { IsCrafter: true })
        {
            return content.RecipesFor(structureId).FirstOrDefault();
        }

        // A research node of kind "recipe" (no block): the recipe it gates.
        return content.FindStructure(structureId)?.Kind == FactoryContent.RecipeKind
            ? content.Recipes.FirstOrDefault(r => r.Research == structureId)
            : null;
    }

    /// <summary>
    /// Seconds per craft of <paramref name="recipe"/> in <paramref name="structureId"/> at its own speed
    /// (<see cref="BuildingDefinition.CraftSpeedPercent"/>: the advanced smelter takes half the forno's time).
    /// </summary>
    internal static double RecipeSeconds(RecipeDefinition recipe, string structureId, FactoryContent content)
    {
        var speed = content.FindBuilding(structureId) is { IsCrafter: true } building ? building.CraftSpeedPercent : 100;
        return recipe.DurationSeconds * 100.0 / Math.Max(1, speed);
    }

    /// <summary>
    /// The outputs of a recipe that makes one piece at random (the separator): each item with its chance
    /// (<c>blocco.io.caso</c>), from the weights of the content, joined by <paramref name="separator"/>.
    /// </summary>
    internal static string RandomOutputs(RecipeDefinition recipe, FactoryContent content, string separator)
    {
        var total = Math.Max(1, recipe.Outputs.Sum(o => o.Amount));
        return string.Join(separator, recipe.Outputs.Select(o =>
            Testi.F("blocco.io.caso", Testi.Oggetto(o.ItemId, content), o.Amount * 100 / total)));
    }
}
