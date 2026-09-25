using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Campagna: lista livelli IT con sblocco da progress.</summary>
public partial class CampaignSelectPanel : Control
{
    private static readonly Color PanelBg = new(0.08f, 0.10f, 0.11f, 0.96f);
    private static readonly Color Accent = new(0.92f, 0.82f, 0.42f, 1f);
    private static readonly Color TextPrimary = new(0.93f, 0.95f, 0.90f, 1f);
    private static readonly Color TextMuted = new(0.70f, 0.76f, 0.70f, 1f);
    private static readonly Color RowPlayable = new(0.20f, 0.28f, 0.22f, 1f);
    private static readonly Color RowDone = new(0.16f, 0.26f, 0.30f, 1f);
    private static readonly Color RowLocked = new(0.14f, 0.14f, 0.14f, 1f);

    private CampaignCatalog? _catalog;
    private CampaignProgress? _progress;
    private VBoxContainer? _list;
    private Label? _hintLabel;

    public event Action? Closed;
    public event Action<CampaignLevelDefinition>? LevelChosen;

    public override void _Ready()
    {
        Name = "CampaignSelectPanel";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        BuildUi();
    }

    public void Open(CampaignCatalog catalog, CampaignProgress progress)
    {
        _catalog = catalog;
        _progress = progress;
        Visible = true;
        Refresh();
    }

    public void Close()
    {
        Visible = false;
        Closed?.Invoke();
    }

    public bool IsOpen => Visible;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible)
        {
            return;
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo
            && key.Keycode is Key.Escape or Key.G)
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildUi()
    {
        var dim = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.55f),
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var panel = new PanelContainer { Name = "CampaignCard" };
        panel.SetAnchorsPreset(LayoutPreset.Center);
        panel.OffsetLeft = -420;
        panel.OffsetTop = -300;
        panel.OffsetRight = 420;
        panel.OffsetBottom = 300;
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = PanelBg,
            BorderColor = Accent,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 14,
            ContentMarginBottom = 14
        });
        AddChild(panel);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        panel.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        root.AddChild(header);

        var title = new Label { Text = "Campagna", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeColorOverride("font_color", TextPrimary);
        title.AddThemeFontSizeOverride("font_size", 22);
        header.AddChild(title);

        var closeBtn = new Button { Text = "Indietro · Esc" };
        closeBtn.Pressed += Close;
        header.AddChild(closeBtn);

        _hintLabel = new Label
        {
            Text = "Scegli un livello sbloccato · G apre / Esc chiude",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _hintLabel.AddThemeColorOverride("font_color", TextMuted);
        _hintLabel.AddThemeFontSizeOverride("font_size", 12);
        root.AddChild(_hintLabel);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 480),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        root.AddChild(scroll);

        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 8);
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_list);
    }

    public void Refresh()
    {
        if (_catalog is null || _progress is null || _list is null)
        {
            return;
        }

        foreach (var child in _list.GetChildren())
        {
            child.QueueFree();
        }

        var index = 0;
        foreach (var level in _catalog.Levels)
        {
            index++;
            var unlocked = _progress.IsUnlocked(level, _catalog);
            var completed = _progress.IsCompleted(level.Id);
            AddCard(index, level, unlocked, completed);
        }
    }

    private void AddCard(int index, CampaignLevelDefinition level, bool unlocked, bool completed)
    {
        if (_list is null || _catalog is null)
        {
            return;
        }

        var bg = !unlocked ? RowLocked : completed ? RowDone : RowPlayable;
        var row = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 72),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Stop
        };
        row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = unlocked ? Accent : new Color(0.28f, 0.30f, 0.28f),
            BorderWidthLeft = unlocked ? 2 : 1,
            BorderWidthTop = unlocked ? 2 : 1,
            BorderWidthRight = unlocked ? 2 : 1,
            BorderWidthBottom = unlocked ? 2 : 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        });
        _list.AddChild(row);

        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 4);
        row.AddChild(v);

        var status = !unlocked ? "BLOCCATO" : completed ? "COMPLETATO" : "GIOCA";
        var title = new Label
        {
            Text = $"{index}. {level.Name}  ·  {status}"
        };
        title.AddThemeColorOverride("font_color", TextPrimary);
        title.AddThemeFontSizeOverride("font_size", 15);
        v.AddChild(title);

        var desc = new Label
        {
            Text = level.Description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        desc.AddThemeColorOverride("font_color", TextMuted);
        desc.AddThemeFontSizeOverride("font_size", 12);
        v.AddChild(desc);

        var objs = new Label
        {
            Text = _catalog.ObjectiveSummary(level),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        objs.AddThemeColorOverride("font_color", TextMuted);
        objs.AddThemeFontSizeOverride("font_size", 11);
        v.AddChild(objs);

        if (unlocked)
        {
            var captured = level;
            row.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                {
                    LevelChosen?.Invoke(captured);
                }
            };
        }
    }
}
