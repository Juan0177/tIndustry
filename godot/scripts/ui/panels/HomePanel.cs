using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Mindustry-dark home: Continua / Campagna / Nuova partita / Esci.
/// Shown over Main until the player picks a route.
/// </summary>
public partial class HomePanel : Control
{
    private Button? _continuaBtn;
    private Label? _hint;

    public event Action? ContinuaChosen;
    public event Action? CampaignChosen;
    public event Action? NewGameChosen;
    public event Action? SavesChosen;
    public event Action? SettingsChosen;
    public event Action? QuitChosen;

    public override void _Ready()
    {
        Name = "HomePanel";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = true;
        BuildUi();
        RefreshContinua();
    }

    public void Open()
    {
        Visible = true;
        RefreshContinua();
    }

    public void Close() => Visible = false;

    public bool IsOpen => Visible;

    /// <summary>True while the Continua button is shown (there is a save to continue).</summary>
    public bool ContinuaShown => _continuaBtn?.Visible == true;

    /// <summary>Shows a load error in place of the hint line (until the next refresh).</summary>
    public void ShowError(string message)
    {
        if (_hint is not null)
        {
            _hint.Text = message;
            _hint.ThemeTypeVariation = UiTheme.TestoRosso;
        }
    }

    public void RefreshContinua()
    {
        if (_hint is not null)
        {
            _hint.ThemeTypeVariation = UiTheme.TestoTenue;
        }

        var has = FactorySaveStore.Exists(FactorySaveStore.ContinueSlotId);
        if (_continuaBtn is not null)
        {
            // No save to continue: no button at all (not a disabled one).
            _continuaBtn.Visible = has;
        }

        if (_hint is not null)
        {
            _hint.Text = has
                ? "Riprendi l'ultima partita salvata."
                : "Nessuna continua — scegli Campagna o Nuova partita.";
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible)
        {
            return;
        }

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            QuitChosen?.Invoke();
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

        var card = new PanelContainer { Name = "HomeCard", ThemeTypeVariation = UiTheme.Scheda };
        card.SetAnchorsPreset(LayoutPreset.Center);
        card.OffsetLeft = -220;
        card.OffsetTop = -270;
        card.OffsetRight = 220;
        card.OffsetBottom = 270;
        card.GrowHorizontal = GrowDirection.Both;
        card.GrowVertical = GrowDirection.Both;
        AddChild(card);

        var root = new VBoxContainer { ThemeTypeVariation = UiTheme.ColonnaLarga };
        card.AddChild(root);

        var brand = new Label
        {
            Text = "tINDUSTRY",
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = UiTheme.Logo
        };
        root.AddChild(brand);

        var sub = new Label
        {
            Text = "Fabbrica · Logistica · Ricerca",
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = UiTheme.TestoTenue
        };
        root.AddChild(sub);

        root.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

        _continuaBtn = MakeMenuButton("Continua", () => ContinuaChosen?.Invoke());
        _continuaBtn.Name = "Continua";
        root.AddChild(_continuaBtn);
        root.AddChild(MakeMenuButton("Campagna", () => CampaignChosen?.Invoke()));
        root.AddChild(MakeMenuButton("Nuova partita", () => NewGameChosen?.Invoke()));
        root.AddChild(MakeMenuButton("Gestione salvataggi", () => SavesChosen?.Invoke()));
        root.AddChild(MakeMenuButton("Impostazioni", () => SettingsChosen?.Invoke()));
        root.AddChild(MakeMenuButton("Esci", () => QuitChosen?.Invoke()));

        _hint = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = UiTheme.TestoTenue
        };
        root.AddChild(_hint);
    }

    private static Button MakeMenuButton(string label, Action onClick)
    {
        var btn = new Button
        {
            Text = label,
            CustomMinimumSize = new Vector2(0, 40)
        };
        btn.Pressed += onClick;
        return btn;
    }
}
