using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Campagna: lista livelli IT con sblocco da progress.</summary>
public partial class CampaignSelectPanel : Control
{
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
        ShowHint(DefaultHint, UiTheme.TestoTenue);
        Refresh();
    }

    private const string DefaultHint = "Scegli un livello sbloccato · G apre / Esc chiude";

    /// <summary>Shows why a level could not start in place of the hint line (until the panel is opened again).</summary>
    public void ShowError(string message) => ShowHint(message, UiTheme.TestoRosso);

    private void ShowHint(string text, string look)
    {
        if (_hintLabel is not null)
        {
            _hintLabel.Text = text;
            _hintLabel.ThemeTypeVariation = look;
        }
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
            Color = UiColors.Oscuramento,
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var panel = new PanelContainer { Name = "CampaignCard", ThemeTypeVariation = UiTheme.Scheda };
        panel.SetAnchorsPreset(LayoutPreset.Center);
        panel.OffsetLeft = -420;
        panel.OffsetTop = -270;
        panel.OffsetRight = 420;
        panel.OffsetBottom = 270;
        panel.GrowHorizontal = GrowDirection.Both;
        panel.GrowVertical = GrowDirection.Both;
        AddChild(panel);

        var root = new VBoxContainer { ThemeTypeVariation = UiTheme.ColonnaLarga };
        panel.AddChild(root);

        var header = new HBoxContainer { ThemeTypeVariation = UiTheme.RigaAmpia };
        root.AddChild(header);

        var icon = HudStyle.MakeIcon(UiTheme.Icon("campagna"), 32, pixel: true);
        icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(icon);
        var title = new Label
        {
            Text = "Campagna",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ThemeTypeVariation = UiTheme.Titolo
        };
        header.AddChild(title);

        var closeBtn = new Button { Text = "Indietro · Esc" };
        closeBtn.Pressed += Close;
        header.AddChild(closeBtn);

        _hintLabel = new Label
        {
            Text = DefaultHint,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ThemeTypeVariation = UiTheme.TestoTenue
        };
        root.AddChild(_hintLabel);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 360),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        root.AddChild(scroll);

        _list = new VBoxContainer { ThemeTypeVariation = UiTheme.ColonnaLarga };
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

        var row = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 72),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Stop,
            ThemeTypeVariation = !unlocked ? UiTheme.VoceBloccata : completed ? UiTheme.VoceFatta : UiTheme.Voce
        };
        _list.AddChild(row);

        var v = new VBoxContainer();
        row.AddChild(v);

        var head = new HBoxContainer { ThemeTypeVariation = UiTheme.RigaLarga };
        v.AddChild(head);
        if (!unlocked)
        {
            // The lock beside the word: the state never rests on the colour alone.
            var lockIcon = HudStyle.MakeIcon(UiTheme.Icon("lucchetto", small: true), 16, pixel: true);
            lockIcon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            head.AddChild(lockIcon);
        }

        var status = !unlocked ? "BLOCCATO" : completed ? "COMPLETATO" : "GIOCA";
        head.AddChild(new Label
        {
            Text = $"{index}. {level.Name}  ·  {status}",
            ThemeTypeVariation = !unlocked ? UiTheme.TestoTenue : completed ? UiTheme.TestoVerde : UiTheme.TestoAccento
        });

        v.AddChild(new Label
        {
            Text = level.Description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ThemeTypeVariation = unlocked ? "" : UiTheme.TestoTenue
        });

        v.AddChild(new Label
        {
            Text = _catalog.ObjectiveSummary(level),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ThemeTypeVariation = UiTheme.TestoTenue
        });

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
