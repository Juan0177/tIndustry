using Godot;

namespace TIndustry.Godot;

/// <summary>
/// Real factory HUD: build toolbar + core stock panel + toast.
/// Built in code so Spike.tscn stays thin; Italian strings.
/// </summary>
public partial class FactoryHud : Control
{
    public enum ToolKind
    {
        Belt,
        Miner,
        Smelter,
        Assembler,
        Junction,
        Splitter,
        Generator
    }

    private static readonly Color PanelBg = new(0.10f, 0.12f, 0.13f, 0.94f);
    private static readonly Color PanelEdge = new(0.32f, 0.38f, 0.34f, 1f);
    private static readonly Color SlotBg = new(0.16f, 0.18f, 0.17f, 1f);
    private static readonly Color SlotIdle = new(0.40f, 0.46f, 0.42f, 1f);
    private static readonly Color SlotSelected = new(0.92f, 0.82f, 0.42f, 1f);
    private static readonly Color TextPrimary = new(0.93f, 0.95f, 0.90f, 1f);
    private static readonly Color TextMuted = new(0.70f, 0.76f, 0.70f, 1f);

    private readonly Dictionary<ToolKind, PanelContainer> _toolSlots = [];
    private readonly Dictionary<string, Label> _stockLabels = [];
    private Label? _dirLabel;
    private Label? _toastLabel;
    private Label? _hintLabel;
    private Label? _titleLabel;
    private ToolKind _selected = ToolKind.Belt;
    private Tween? _toastTween;

    public event Action<ToolKind>? ToolChosen;
    public event Action? RotateRequested;

    public override void _Ready()
    {
        Name = "FactoryHudRoot";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        BuildStockPanel();
        BuildToolbar();
        BuildToast();
        SetSelectedTool(ToolKind.Belt);
        SetDirectionLabel("Est");
    }

    public void SetSelectedTool(ToolKind tool)
    {
        _selected = tool;
        foreach (var (kind, slot) in _toolSlots)
        {
            var style = (StyleBoxFlat)slot.GetThemeStylebox("panel").Duplicate();
            style.BorderColor = kind == tool ? SlotSelected : SlotIdle;
            style.BorderWidthLeft = kind == tool ? 3 : 1;
            style.BorderWidthTop = kind == tool ? 3 : 1;
            style.BorderWidthRight = kind == tool ? 3 : 1;
            style.BorderWidthBottom = kind == tool ? 3 : 1;
            style.BgColor = kind == tool
                ? new Color(0.22f, 0.24f, 0.18f, 1f)
                : SlotBg;
            slot.AddThemeStyleboxOverride("panel", style);
        }

        if (_hintLabel is not null)
        {
            _hintLabel.Text = ToolHint(tool);
        }
    }

    public void SetDirectionLabel(string directionIt)
    {
        if (_dirLabel is not null)
        {
            _dirLabel.Text = directionIt;
        }
    }

    public void UpdateStock(
        string oreName, int ore,
        string plateName, int plate,
        string copperName, int copper,
        string wireName, int wire,
        long delivered,
        int onBelt,
        int generatorsLive = 0,
        int generatorsTotal = 0)
    {
        SetStock("iron-ore", oreName, ore);
        SetStock("iron-plate", plateName, plate);
        SetStock("copper-ore", copperName, copper);
        SetStock("copper-wire", wireName, wire);
        if (_titleLabel is not null)
        {
            var power = generatorsTotal == 0
                ? "potenza —"
                : generatorsLive > 0
                    ? $"potenza ON ({generatorsLive}/{generatorsTotal})"
                    : $"potenza off ({generatorsLive}/{generatorsTotal})";
            _titleLabel.Text = $"Core · consegnati {delivered} · nastro {onBelt} · {power}";
        }
    }

    public void ShowToast(string message)
    {
        if (_toastLabel is null)
        {
            return;
        }

        _toastLabel.Text = message;
        _toastLabel.Modulate = Colors.White;
        _toastTween?.Kill();
        _toastTween = CreateTween();
        _toastTween.TweenInterval(1.6);
        _toastTween.TweenProperty(_toastLabel, "modulate:a", 0f, 0.45);
    }

    private void SetStock(string id, string name, int count)
    {
        if (_stockLabels.TryGetValue(id, out var label))
        {
            label.Text = $"{name}\n{count}";
        }
    }

    private void BuildStockPanel()
    {
        var panel = MakePanel("StockPanel");
        panel.SetAnchorsPreset(LayoutPreset.TopRight);
        panel.GrowHorizontal = GrowDirection.Begin;
        panel.OffsetLeft = -320;
        panel.OffsetTop = 12;
        panel.OffsetRight = -12;
        panel.OffsetBottom = 148;
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vbox);

        _titleLabel = new Label
        {
            Text = "Core",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _titleLabel.AddThemeColorOverride("font_color", TextMuted);
        _titleLabel.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(_titleLabel);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(row);

        AddStockSlot(row, "iron-ore", "res://assets/iron-ore.png", "Ferro grezzo");
        AddStockSlot(row, "iron-plate", "res://assets/iron-plate.png", "Lastra ferro");
        AddStockSlot(row, "copper-ore", "res://assets/copper-ore.png", "Rame grezzo");
        AddStockSlot(row, "copper-wire", "res://assets/copper-wire.png", "Filo rame");
    }

    private void AddStockSlot(Control parent, string id, string texPath, string fallbackName)
    {
        var box = new VBoxContainer();
        box.CustomMinimumSize = new Vector2(68, 0);
        box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        parent.AddChild(box);

        var iconWrap = new PanelContainer();
        iconWrap.CustomMinimumSize = new Vector2(48, 48);
        iconWrap.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        iconWrap.AddThemeStyleboxOverride("panel", MakeSlotStyle(SlotIdle, 1));
        box.AddChild(iconWrap);

        var center = new CenterContainer();
        iconWrap.AddChild(center);
        var tex = GD.Load<Texture2D>(texPath);
        var icon = new TextureRect
        {
            Texture = tex,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(36, 36),
            Modulate = Colors.White
        };
        center.AddChild(icon);

        var label = new Label
        {
            Text = $"{fallbackName}\n0",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.AddThemeColorOverride("font_color", TextPrimary);
        label.AddThemeFontSizeOverride("font_size", 11);
        box.AddChild(label);
        _stockLabels[id] = label;
    }

    private void BuildToolbar()
    {
        var panel = MakePanel("Toolbar");
        panel.SetAnchorsPreset(LayoutPreset.BottomWide);
        panel.OffsetLeft = 12;
        panel.OffsetRight = -12;
        panel.OffsetTop = -118;
        panel.OffsetBottom = -12;
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var root = new HBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var tools = new HBoxContainer();
        tools.AddThemeConstantOverride("separation", 8);
        tools.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(tools);

        AddToolButton(tools, ToolKind.Belt, "1", "Nastro", "res://assets/conveyor-basic.png",
            new Color(0.55f, 0.62f, 0.48f));
        AddToolButton(tools, ToolKind.Miner, "2", "Minatore", "res://assets/miner.png",
            new Color(0.85f, 0.72f, 0.40f));
        AddToolButton(tools, ToolKind.Smelter, "3", "Forno", "res://assets/smelter.png",
            new Color(0.95f, 0.55f, 0.28f));
        AddToolButton(tools, ToolKind.Assembler, "4", "Assemblatore", "res://assets/assembler.png",
            new Color(0.45f, 0.78f, 0.95f));
        AddToolButton(tools, ToolKind.Junction, "5", "Giunzione", "res://assets/junction.png",
            new Color(0.85f, 0.88f, 0.90f));
        AddToolButton(tools, ToolKind.Splitter, "6", "Splitter", "res://assets/splitter.png",
            new Color(0.75f, 0.80f, 0.95f));
        AddToolButton(tools, ToolKind.Generator, "7", "Generatore", "res://assets/generator.png",
            new Color(0.95f, 0.78f, 0.35f));

        // Rotate as a same-size toolbar slot (reliable hit target).
        var rotateSlot = new PanelContainer
        {
            Name = "RotateSlot",
            CustomMinimumSize = new Vector2(84, 86),
            MouseFilter = MouseFilterEnum.Stop
        };
        rotateSlot.AddThemeStyleboxOverride("panel", MakeSlotStyle(SlotIdle, 1));
        tools.AddChild(rotateSlot);

        var rotMargin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        rotMargin.AddThemeConstantOverride("margin_left", 6);
        rotMargin.AddThemeConstantOverride("margin_right", 6);
        rotMargin.AddThemeConstantOverride("margin_top", 4);
        rotMargin.AddThemeConstantOverride("margin_bottom", 4);
        rotateSlot.AddChild(rotMargin);

        var rotV = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        rotV.AddThemeConstantOverride("separation", 2);
        rotMargin.AddChild(rotV);

        var rotKey = new Label { Text = "R", MouseFilter = MouseFilterEnum.Ignore };
        rotKey.AddThemeColorOverride("font_color", SlotSelected);
        rotKey.AddThemeFontSizeOverride("font_size", 12);
        rotV.AddChild(rotKey);

        var rotCenter = new CenterContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        rotV.AddChild(rotCenter);
        _dirLabel = new Label
        {
            Text = "Est",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _dirLabel.AddThemeColorOverride("font_color", TextPrimary);
        _dirLabel.AddThemeFontSizeOverride("font_size", 16);
        rotCenter.AddChild(_dirLabel);

        var rotName = new Label
        {
            Text = "Ruota",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        rotName.AddThemeColorOverride("font_color", TextPrimary);
        rotName.AddThemeFontSizeOverride("font_size", 11);
        rotV.AddChild(rotName);

        rotateSlot.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                RotateRequested?.Invoke();
                ShowToast("Direzione ruotata");
                AcceptEvent();
            }
        };

        var side = new VBoxContainer();
        side.CustomMinimumSize = new Vector2(200, 0);
        side.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        side.AddThemeConstantOverride("separation", 6);
        root.AddChild(side);

        _hintLabel = new Label
        {
            Text = ToolHint(ToolKind.Belt),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _hintLabel.AddThemeColorOverride("font_color", TextMuted);
        _hintLabel.AddThemeFontSizeOverride("font_size", 12);
        side.AddChild(_hintLabel);
    }

    private void AddToolButton(
        Control parent,
        ToolKind kind,
        string hotkey,
        string labelIt,
        string texPath,
        Color accent)
    {
        var slot = new PanelContainer();
        slot.CustomMinimumSize = new Vector2(84, 86);
        slot.MouseFilter = MouseFilterEnum.Stop;
        slot.AddThemeStyleboxOverride("panel", MakeSlotStyle(SlotIdle, 1));
        parent.AddChild(slot);
        _toolSlots[kind] = slot;

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 6);
        margin.AddThemeConstantOverride("margin_right", 6);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        slot.AddChild(margin);

        var vbox = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        vbox.AddThemeConstantOverride("separation", 2);
        margin.AddChild(vbox);

        var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        vbox.AddChild(top);

        var key = new Label { Text = hotkey, MouseFilter = MouseFilterEnum.Ignore };
        key.AddThemeColorOverride("font_color", accent);
        key.AddThemeFontSizeOverride("font_size", 12);
        top.AddChild(key);

        var center = new CenterContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        vbox.AddChild(center);
        var tex = GD.Load<Texture2D>(texPath);
        var icon = new TextureRect
        {
            Texture = tex,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(40, 40),
            Modulate = Colors.White,
            MouseFilter = MouseFilterEnum.Ignore
        };
        center.AddChild(icon);

        var name = new Label
        {
            Text = labelIt,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        name.AddThemeColorOverride("font_color", TextPrimary);
        name.AddThemeFontSizeOverride("font_size", 11);
        vbox.AddChild(name);

        slot.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                SetSelectedTool(kind);
                ToolChosen?.Invoke(kind);
                ShowToast($"{labelIt} selezionato");
                AcceptEvent();
            }
        };
    }

    private void BuildToast()
    {
        _toastLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _toastLabel.SetAnchorsPreset(LayoutPreset.CenterBottom);
        _toastLabel.GrowHorizontal = GrowDirection.Both;
        _toastLabel.OffsetLeft = -220;
        _toastLabel.OffsetRight = 220;
        _toastLabel.OffsetTop = -156;
        _toastLabel.OffsetBottom = -128;
        _toastLabel.AddThemeColorOverride("font_color", TextPrimary);
        _toastLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.75f));
        _toastLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        _toastLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        _toastLabel.AddThemeFontSizeOverride("font_size", 15);
        _toastLabel.Modulate = new Color(1, 1, 1, 0);
        AddChild(_toastLabel);
    }

    private static PanelContainer MakePanel(string name)
    {
        var panel = new PanelContainer
        {
            Name = name,
            MouseFilter = MouseFilterEnum.Stop
        };
        var style = new StyleBoxFlat
        {
            BgColor = PanelBg,
            BorderColor = PanelEdge,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 0,
            ContentMarginRight = 0,
            ContentMarginTop = 0,
            ContentMarginBottom = 0
        };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static StyleBoxFlat MakeSlotStyle(Color border, int width) => new()
    {
        BgColor = SlotBg,
        BorderColor = border,
        BorderWidthLeft = width,
        BorderWidthTop = width,
        BorderWidthRight = width,
        BorderWidthBottom = width,
        CornerRadiusTopLeft = 4,
        CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4,
        CornerRadiusBottomRight = 4,
        ContentMarginLeft = 4,
        ContentMarginRight = 4,
        ContentMarginTop = 4,
        ContentMarginBottom = 4
    };

    private static string ToolHint(ToolKind tool) => tool switch
    {
        ToolKind.Belt => "Click/trascina: piazza nastro",
        ToolKind.Miner => "Click: piazza minatore 2×2",
        ToolKind.Smelter => "Click: piazza forno 2×2",
        ToolKind.Assembler => "Click: piazza assemblatore 2×2",
        ToolKind.Junction => "Click/trascina: giunzione",
        ToolKind.Splitter => "Click/trascina: splitter",
        ToolKind.Generator => "Click: generatore 2×2 (carbone → potenza)",
        _ => ""
    };
}
