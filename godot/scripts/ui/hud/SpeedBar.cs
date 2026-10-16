using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Speed bar in the top-right corner: segmented pause / ×1 / ×10 / ×60, time jumps +1h / +6h, and in dev
/// mode a button for the console. The active speed is highlighted; <see cref="Refresh"/> keeps it in sync
/// with Space, hotkeys and console commands.
/// </summary>
public partial class SpeedBar : Control
{
    private static readonly Color ActiveBg = new(0.30f, 0.26f, 0.10f, 0.95f);
    private static readonly Color HoverBg = new(0.16f, 0.17f, 0.15f, 0.95f);

    private readonly List<(float Speed, Button Button)> _speedButtons = [];
    private Button? _pauseButton;

    public event Action<float>? SpeedChosen;
    public event Action? PauseToggled;
    public event Action<int>? FastForwardRequested;
    public event Action? ConsoleRequested;

    /// <summary>Set before adding to the tree: shows the console button (dev mode only).</summary>
    public bool ShowConsoleButton { get; init; }

    public override void _Ready()
    {
        Name = "SpeedBar";
        MouseFilter = MouseFilterEnum.Ignore;
        BuildUi();
    }

    /// <summary>Highlight the running speed, or the pause button while paused.</summary>
    public void Refresh(float speed, bool paused)
    {
        if (_pauseButton is not null)
        {
            SetActive(_pauseButton, paused);
        }

        foreach (var (s, button) in _speedButtons)
        {
            SetActive(button, !paused && Math.Abs(s - speed) < 0.001f);
        }
    }

    private void BuildUi()
    {
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 6);
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight, LayoutPresetMode.KeepSize);
        row.GrowHorizontal = GrowDirection.Begin;
        AddChild(row);

        var speeds = Group(row);
        _pauseButton = MakeButton("⏸", "Pausa / riprendi (Spazio)", () => PauseToggled?.Invoke());
        speeds.AddChild(_pauseButton);
        foreach (var speed in GameSpeed.BarSpeeds)
        {
            var label = $"×{speed:0.##}";
            var button = MakeButton(label, $"Velocità {label}", () => SpeedChosen?.Invoke(speed));
            speeds.AddChild(button);
            _speedButtons.Add((speed, button));
        }

        foreach (var (label, seconds) in new[] { ("+1h", 3600), ("+6h", 6 * 3600) })
        {
            var group = Group(row);
            group.AddChild(MakeButton(label, $"Salta avanti di {label[1..]} di gioco", () => FastForwardRequested?.Invoke(seconds)));
        }

        if (ShowConsoleButton)
        {
            var console = Group(row);
            console.AddChild(MakeButton("console", "Console dev (F1)", () => ConsoleRequested?.Invoke()));
        }
    }

    /// <summary>A bordered pill holding buttons side by side (one segment per button).</summary>
    private static HBoxContainer Group(Control parent)
    {
        var panel = HudStyle.MakePanel("Group");
        var style = (StyleBoxFlat)panel.GetThemeStylebox("panel");
        style.ContentMarginLeft = style.ContentMarginRight = 2;
        style.ContentMarginTop = style.ContentMarginBottom = 2;
        parent.AddChild(panel);

        var inner = new HBoxContainer();
        inner.AddThemeConstantOverride("separation", 0);
        panel.AddChild(inner);
        return inner;
    }

    private static Button MakeButton(string text, string tooltip, Action onClick)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip,
            // No keyboard focus: Space/Enter must keep going to the world and the console.
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(0, 28)
        };
        button.AddThemeFontSizeOverride("font_size", 14);
        SetActive(button, false);
        button.Pressed += onClick;
        return button;
    }

    private static void SetActive(Button button, bool active)
    {
        var text = active ? HudStyle.SlotSelected : HudStyle.TextMuted;
        button.AddThemeColorOverride("font_color", text);
        button.AddThemeColorOverride("font_hover_color", active ? HudStyle.SlotSelected : HudStyle.TextPrimary);
        button.AddThemeColorOverride("font_pressed_color", HudStyle.SlotSelected);
        foreach (var state in new[] { "normal", "hover", "pressed" })
        {
            var style = new StyleBoxFlat
            {
                BgColor = active ? ActiveBg : state == "hover" ? HoverBg : Colors.Transparent,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                ContentMarginLeft = 10,
                ContentMarginRight = 10,
                ContentMarginTop = 2,
                ContentMarginBottom = 2
            };
            button.AddThemeStyleboxOverride(state, style);
        }
    }
}
