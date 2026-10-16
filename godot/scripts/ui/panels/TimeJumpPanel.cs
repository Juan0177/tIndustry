using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Modal card shown while a time jump (+1h, +6h, <c>avanti</c>) runs: the game is paused behind a dimmed
/// screen and a progress bar fills as the simulation catches up. Swallows clicks so nothing is placed meanwhile.
/// </summary>
public partial class TimeJumpPanel : Control
{
    private Label? _title;
    private ProgressBar? _bar;
    private Label? _detail;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        Name = "TimeJumpPanel";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        BuildUi();
    }

    public void Open(TimeJump jump)
    {
        if (_title is not null)
        {
            _title.Text = $"Salto avanti di {TimeJump.FormatGameTime(jump.TotalTicks)}";
        }

        Visible = true;
        Refresh(jump);
    }

    public void Refresh(TimeJump jump)
    {
        if (_bar is not null)
        {
            _bar.Value = jump.Progress * 100;
        }

        if (_detail is not null)
        {
            _detail.Text = $"{TimeJump.FormatGameTime(jump.DoneTicks)} / {TimeJump.FormatGameTime(jump.TotalTicks)}";
        }
    }

    public void Close() => Visible = false;

    private void BuildUi()
    {
        AddChild(new ColorRect
        {
            Color = new Color(0, 0, 0, 0.45f),
            MouseFilter = MouseFilterEnum.Ignore,
            AnchorRight = 1,
            AnchorBottom = 1
        });

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var card = HudStyle.MakePanel("TimeJumpCard");
        var style = (StyleBoxFlat)card.GetThemeStylebox("panel");
        style.ContentMarginLeft = style.ContentMarginRight = 20;
        style.ContentMarginTop = style.ContentMarginBottom = 16;
        card.CustomMinimumSize = new Vector2(420, 0);
        center.AddChild(card);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        card.AddChild(column);

        _title = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _title.AddThemeColorOverride("font_color", HudStyle.SlotSelected);
        _title.AddThemeFontSizeOverride("font_size", 18);
        column.AddChild(_title);

        _bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            ShowPercentage = true,
            CustomMinimumSize = new Vector2(0, 22)
        };
        var fill = new StyleBoxFlat { BgColor = HudStyle.SlotSelected };
        fill.SetCornerRadiusAll(3);
        var back = new StyleBoxFlat { BgColor = HudStyle.SlotBg, BorderColor = HudStyle.PanelEdge };
        back.SetBorderWidthAll(1);
        back.SetCornerRadiusAll(3);
        _bar.AddThemeStyleboxOverride("fill", fill);
        _bar.AddThemeStyleboxOverride("background", back);
        _bar.AddThemeColorOverride("font_color", HudStyle.TextPrimary);
        _bar.AddThemeColorOverride("font_outline_color", Colors.Black);
        _bar.AddThemeConstantOverride("outline_size", 3);
        column.AddChild(_bar);

        _detail = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _detail.AddThemeColorOverride("font_color", HudStyle.TextMuted);
        _detail.AddThemeFontSizeOverride("font_size", 13);
        column.AddChild(_detail);
    }
}
