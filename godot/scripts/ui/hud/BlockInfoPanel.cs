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

        var recipeId = structureId switch
        {
            "smelter" => "smelt-iron",
            "assembler" => "craft-copper-wire",
            "generator" => null,
            _ => null
        };
        if (recipeId is null)
        {
            return structureId switch
            {
                "miner" => Testi.T("blocco.io.minatore"),
                "generator" => Testi.T("blocco.io.generatore"),
                "conveyor-basic" or "junction" or "splitter" or "sorter" or "conveyor-bridge"
                    => Testi.T("blocco.io.trasporto"),
                _ => ""
            };
        }

        var recipe = _content.FindRecipe(recipeId);
        if (recipe is null)
        {
            return "";
        }

        string Fmt(IReadOnlyList<ResourceAmount> list) =>
            string.Join("+", list.Select(a =>
                $"{a.Amount}×{Testi.Oggetto(a.ItemId, _content)}"));

        return $"I/O · {Fmt(recipe.Inputs)} → {Fmt(recipe.Outputs)} ({Testi.Secondi(recipe.DurationSeconds)})";
    }
}
