using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Nuova partita: map size (Media, Grande, Enorme), seed (a random one already filled in, editable, with
/// "Nuovo seme" and "Copia") and "Notte" (the game is played at night, G3), then "Inizia".
/// </summary>
public partial class NewGamePanel : Control
{
    private static readonly MapSize[] Sizes = [MapSize.Media, MapSize.Grande, MapSize.Enorme];

    private readonly Random _seeds = new();
    private readonly List<Button> _sizeButtons = [];
    private MapSize _size = MapSize.Media;
    private LineEdit? _seedEdit;
    private CheckButton? _night;
    private Label? _status;

    /// <summary>The player pressed Inizia with a valid seed: size, seed and night.</summary>
    public event Action<MapSize, int, bool>? StartRequested;

    /// <summary>Indietro or Esc: back to Home.</summary>
    public event Action? Cancelled;

    public bool IsOpen => Visible;

    /// <summary>Italian name of a map size.</summary>
    public static string SizeLabel(MapSize size) => size switch
    {
        MapSize.Media => "Media",
        MapSize.Grande => "Grande",
        MapSize.Enorme => "Enorme",
        _ => size.ToString()
    };

    public override void _Ready()
    {
        Name = "NewGamePanel";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        BuildUi();
    }

    /// <summary>Opens with Media and a fresh random seed, or <paramref name="seed"/> when given (playtests).</summary>
    public void Open(int? seed = null)
    {
        SelectSize(MapSize.Media);
        SetSeed(seed ?? NewSeed());
        _night?.SetPressedNoSignal(false);
        ShowStatus("");
        Visible = true;
    }

    public void Close() => Visible = false;

    /// <summary>A new game that could not start: the reason in red on the panel, which stays open.</summary>
    public void ShowError(string message) => ShowStatus(message, error: true);

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible)
        {
            return;
        }

        // During a playtest only its synthetic events count (World._Input drops the others first; this guards too).
        if (GameDataPaths.IsPlaytest && @event.Device != World.PlaytestDevice)
        {
            return;
        }

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close();
            Cancelled?.Invoke();
            GetViewport().SetInputAsHandled();
        }
    }

    private int NewSeed() => _seeds.Next(1, int.MaxValue);

    private void SetSeed(int seed)
    {
        if (_seedEdit is not null)
        {
            _seedEdit.Text = seed.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private void SelectSize(MapSize size)
    {
        _size = size;
        for (var i = 0; i < _sizeButtons.Count; i++)
        {
            _sizeButtons[i].SetPressedNoSignal(Sizes[i] == size);
        }
    }

    private void ShowStatus(string text, bool error = false)
    {
        if (_status is null)
        {
            return;
        }

        _status.Text = text;
        _status.ThemeTypeVariation = error ? UiTheme.TestoRosso : UiTheme.TestoTenue;
    }

    /// <summary>The seed in the field, or null (with the reason shown) when it is not a whole number.</summary>
    private int? ReadSeed()
    {
        var text = _seedEdit?.Text.Trim() ?? "";
        if (int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var seed))
        {
            return seed;
        }

        ShowStatus(
            $"Il seme deve essere un numero intero tra {int.MinValue.ToString(System.Globalization.CultureInfo.InvariantCulture)} "
            + $"e {int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture)}.",
            error: true);
        return null;
    }

    private void OnStartPressed()
    {
        if (ReadSeed() is { } seed)
        {
            StartRequested?.Invoke(_size, seed, _night?.ButtonPressed == true);
        }
    }

    private void OnCopyPressed()
    {
        if (ReadSeed() is { } seed)
        {
            DisplayServer.ClipboardSet(seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
            ShowStatus("Seme copiato.");
        }
    }

    private void BuildUi()
    {
        var dim = new ColorRect { Color = UiColors.Oscuramento, MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var card = new PanelContainer { Name = "NewGameCard", ThemeTypeVariation = UiTheme.Scheda };
        AddChild(card);
        var scroll = new ScrollContainer();
        card.AddChild(scroll);
        var root = new VBoxContainer { ThemeTypeVariation = UiTheme.ColonnaLarga, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(root);
        CardLayout.CenterToContent(card, scroll, root, 580);

        root.AddChild(new Label
        {
            Text = "Nuova partita",
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = UiTheme.Titolo
        });

        root.AddChild(MutedLabel("Misura della mappa"));
        var sizes = new HBoxContainer { ThemeTypeVariation = UiTheme.RigaLarga };
        root.AddChild(sizes);
        var group = new ButtonGroup();
        foreach (var size in Sizes)
        {
            var edge = (int)size;
            var button = new Button
            {
                Text = $"{SizeLabel(size)}\n{edge}×{edge}",
                ToggleMode = true,
                ButtonGroup = group,
                CustomMinimumSize = new Vector2(0, 52),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            button.Pressed += () => SelectSize(size);
            _sizeButtons.Add(button);
            sizes.AddChild(button);
        }

        root.AddChild(MutedLabel("Seme (la stessa misura e lo stesso seme danno la stessa mappa)"));
        var seedRow = new HBoxContainer { ThemeTypeVariation = UiTheme.RigaLarga };
        root.AddChild(seedRow);
        _seedEdit = new LineEdit
        {
            Name = "SeedEdit",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 36),
            MaxLength = 11
        };
        _seedEdit.TextSubmitted += _ => OnStartPressed();
        seedRow.AddChild(_seedEdit);
        seedRow.AddChild(MakeButton("Nuovo seme", () =>
        {
            SetSeed(NewSeed());
            ShowStatus("");
        }));
        seedRow.AddChild(MakeButton("Copia", OnCopyPressed));

        _night = new CheckButton
        {
            Name = "NightCheck",
            Text = "Notte (mappa al buio: contano le luci dei blocchi e le Lampade)",
            ButtonPressed = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_night);

        _status = MutedLabel("");
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        root.AddChild(_status);

        var actions = new HBoxContainer { ThemeTypeVariation = UiTheme.RigaLarga };
        root.AddChild(actions);
        var back = MakeButton("Indietro", () =>
        {
            Close();
            Cancelled?.Invoke();
        });
        back.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actions.AddChild(back);
        var start = MakeButton("Inizia", OnStartPressed);
        start.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        start.ThemeTypeVariation = UiTheme.PulsantePrimario;
        actions.AddChild(start);
    }

    private static Label MutedLabel(string text)
    {
        return new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, ThemeTypeVariation = UiTheme.TestoTenue };
    }

    private static Button MakeButton(string text, Action onClick)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 36) };
        button.Pressed += onClick;
        return button;
    }
}
