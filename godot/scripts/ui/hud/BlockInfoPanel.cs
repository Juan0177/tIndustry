using Godot;
using TIndustry.Shared;
using static TIndustry.Godot.HudStyle;
using ToolKind = TIndustry.Godot.FactoryHud.ToolKind;

namespace TIndustry.Godot;

/// <summary>
/// Reserved strip above the palette (fixed height, invisible when empty) showing the focused
/// block's name, hotkey and cost chips, plus the centered "?" detail overlay.
/// </summary>
public partial class BlockInfoPanel : Control
{
    /// <summary>Reserved height above the palette for the hover/select strip (empty = invisible).</summary>
    private const float InfoSlotHeight = 78f;

    private PanelContainer? _infoPanel;
    private Label? _infoName;
    private Label? _infoHotkey;
    private HBoxContainer? _infoCostRow;
    private PanelContainer? _detailOverlay;
    private Label? _detailBody;
    private FactoryContent? _content;
    private EconomyWallet? _wallet;

    /// <summary>The "?" button was clicked; the HUD answers with <see cref="OpenDetail"/>.</summary>
    public event Action? DetailRequested;

    /// <summary>Last shown stock and funds, used for costs when no wallet is bound.</summary>
    public CorePanel? Stock { get; set; }

    public BlockInfoPanel()
    {
        Name = "InfoSlot";
        CustomMinimumSize = new Vector2(FactoryHud.CornerStackWidth, InfoSlotHeight);
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

        var infoMargin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        infoMargin.AddThemeConstantOverride("margin_left", 8);
        infoMargin.AddThemeConstantOverride("margin_right", 8);
        infoMargin.AddThemeConstantOverride("margin_top", 6);
        infoMargin.AddThemeConstantOverride("margin_bottom", 6);
        _infoPanel.AddChild(infoMargin);

        var infoRoot = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        infoRoot.AddThemeConstantOverride("separation", 4);
        infoMargin.AddChild(infoRoot);

        var header = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        header.AddThemeConstantOverride("separation", 6);
        infoRoot.AddChild(header);

        _infoName = new Label
        {
            Text = "",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Center
        };
        _infoName.AddThemeColorOverride("font_color", TextPrimary);
        _infoName.AddThemeFontSizeOverride("font_size", 14);
        header.AddChild(_infoName);

        _infoHotkey = new Label { MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
        _infoHotkey.AddThemeColorOverride("font_color", TextMuted);
        _infoHotkey.AddThemeFontSizeOverride("font_size", 12);
        header.AddChild(_infoHotkey);

        var infoBtn = new PanelContainer
        {
            CustomMinimumSize = new Vector2(26, 26),
            MouseFilter = MouseFilterEnum.Stop,
            TooltipText = "Dettaglio blocco"
        };
        infoBtn.AddThemeStyleboxOverride("panel", MakeSlotStyle(SlotIdle, 1));
        header.AddChild(infoBtn);
        var infoCenter = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        infoBtn.AddChild(infoCenter);
        var q = new Label
        {
            Text = "?",
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        q.AddThemeColorOverride("font_color", SlotSelected);
        q.AddThemeFontSizeOverride("font_size", 13);
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
        _infoCostRow.AddThemeConstantOverride("separation", 4);
        infoRoot.AddChild(_infoCostRow);
    }

    /// <summary>Bind content (names, recipes, costs) and wallet (have / missing materials).</summary>
    public void Bind(FactoryContent? content, EconomyWallet? wallet)
    {
        _content = content;
        _wallet = wallet;
    }

    /// <summary>Fill the strip for <paramref name="focus"/>, or empty it when null / not a palette block.</summary>
    public void Show(ToolKind? focus, bool locked)
    {
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
        _infoName.Text = locked ? $"{name} · bloccato" : name;
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
            var free = new Label
            {
                Text = "—",
                MouseFilter = MouseFilterEnum.Ignore
            };
            free.AddThemeColorOverride("font_color", TextMuted);
            free.AddThemeFontSizeOverride("font_size", 12);
            _infoCostRow.AddChild(free);
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
        _detailOverlay = overlay;
        overlay.SetAnchorsPreset(LayoutPreset.Center);
        overlay.GrowHorizontal = GrowDirection.Both;
        overlay.GrowVertical = GrowDirection.Both;
        overlay.OffsetLeft = -220;
        overlay.OffsetRight = 220;
        overlay.OffsetTop = -140;
        overlay.OffsetBottom = 140;
        overlay.Visible = false;
        overlay.MouseFilter = MouseFilterEnum.Stop;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        overlay.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vbox);

        var title = new Label { Text = "Dettaglio blocco" };
        title.AddThemeColorOverride("font_color", SlotSelected);
        title.AddThemeFontSizeOverride("font_size", 16);
        vbox.AddChild(title);

        _detailBody = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _detailBody.AddThemeColorOverride("font_color", TextPrimary);
        _detailBody.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(_detailBody);

        var close = new PanelContainer
        {
            CustomMinimumSize = new Vector2(100, 32),
            MouseFilter = MouseFilterEnum.Stop
        };
        close.AddThemeStyleboxOverride("panel", MakeSlotStyle(SlotIdle, 1));
        vbox.AddChild(close);
        var cc = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        close.AddChild(cc);
        var cl = new Label { Text = "Chiudi", MouseFilter = MouseFilterEnum.Ignore };
        cl.AddThemeColorOverride("font_color", TextPrimary);
        cc.AddChild(cl);
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

        var name = BuildPalette.DisplayName(entry, _content);
        ResolveCost(entry.StructureId, out var money, out var materials);
        var costParts = materials
            .Where(m => m.Amount > 0)
            .Select(m =>
            {
                var have = _wallet?.MaterialCount(m.ItemId) ?? 0;
                var mark = have >= m.Amount ? "ok" : "manca";
                return $"{m.Amount}× {_content?.DisplayName(m.ItemId) ?? m.ItemId} ({mark})";
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

        _detailBody.Text =
            $"{name}\n\n{entry.HintIt}\n\n{ResolveIoLine(entry.StructureId)}\n\nCosto: {costLine}\n\n"
            + "Hotkey: " + entry.Hotkey
            + "\nR = ruota · destro = elimina · Esc = cursore";
        _detailOverlay.Visible = true;
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
                "miner" => "I/O · estrae dal deposito sotto",
                "generator" => "I/O · carbone → potenza",
                "conveyor-basic" or "junction" or "splitter" or "sorter" or "conveyor-bridge"
                    => "I/O · trasporto item",
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
                $"{a.Amount}×{_content.DisplayName(a.ItemId)}"));

        return $"I/O · {Fmt(recipe.Inputs)} → {Fmt(recipe.Outputs)} ({recipe.DurationSeconds:0.#}s)";
    }
}
