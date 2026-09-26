using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Mindustry-dark home: Continua / Campagna / Nuova partita / Esci.
/// Shown over Spike until the player picks a route.
/// </summary>
public partial class HomePanel : Control
{
    private static readonly Color Dim = new(0.04f, 0.05f, 0.05f, 0.82f);
    private static readonly Color CardBg = new(0.08f, 0.10f, 0.11f, 0.96f);
    private static readonly Color Accent = new(0.92f, 0.78f, 0.28f, 1f);
    private static readonly Color TextPrimary = new(0.93f, 0.95f, 0.90f, 1f);
    private static readonly Color TextMuted = new(0.68f, 0.74f, 0.68f, 1f);

    private Button? _continuaBtn;
    private Label? _hint;

    public event Action? ContinuaChosen;
    public event Action? CampaignChosen;
    public event Action? NewGameChosen;
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

    public void RefreshContinua()
    {
        var has = FactorySliceSaveStore.Exists(FactorySliceSaveStore.ContinueSlotId);
        if (_continuaBtn is not null)
        {
            _continuaBtn.Disabled = !has;
            _continuaBtn.Text = has ? "Continua" : "Continua (nessun salvataggio)";
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
            Color = Dim,
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var card = new PanelContainer { Name = "HomeCard" };
        card.SetAnchorsPreset(LayoutPreset.Center);
        card.OffsetLeft = -220;
        card.OffsetTop = -230;
        card.OffsetRight = 220;
        card.OffsetBottom = 230;
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = CardBg,
            BorderColor = Accent,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 24,
            ContentMarginRight = 24,
            ContentMarginTop = 20,
            ContentMarginBottom = 20
        });
        AddChild(card);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        card.AddChild(root);

        var brand = new Label
        {
            Text = "tINDUSTRY",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        brand.AddThemeColorOverride("font_color", Accent);
        brand.AddThemeFontSizeOverride("font_size", 32);
        root.AddChild(brand);

        var sub = new Label
        {
            Text = "Fabbrica · Logistica · Ricerca",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        sub.AddThemeColorOverride("font_color", TextMuted);
        sub.AddThemeFontSizeOverride("font_size", 13);
        root.AddChild(sub);

        root.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

        _continuaBtn = MakeMenuButton("Continua", () => ContinuaChosen?.Invoke());
        root.AddChild(_continuaBtn);
        root.AddChild(MakeMenuButton("Campagna", () => CampaignChosen?.Invoke()));
        root.AddChild(MakeMenuButton("Nuova partita", () => NewGameChosen?.Invoke()));
        root.AddChild(MakeMenuButton("Impostazioni", () => SettingsChosen?.Invoke()));
        root.AddChild(MakeMenuButton("Esci", () => QuitChosen?.Invoke()));

        _hint = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _hint.AddThemeColorOverride("font_color", TextMuted);
        _hint.AddThemeFontSizeOverride("font_size", 12);
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
