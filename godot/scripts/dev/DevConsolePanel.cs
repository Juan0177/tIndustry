using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Dev console drawn across the screen under the speed bar (<c>TINDUSTRY_DEV=1</c>, debug builds only).
/// F1 toggles · Enter runs · ↑/↓ history · Tab completes the command name · Esc closes.
/// Commands are executed by the world through <see cref="CommandSubmitted"/>.
/// </summary>
public partial class DevConsolePanel : Control
{
    private const int MaxLogLines = 200;

    private readonly List<string> _history = [];
    private int _historyIndex;
    private RichTextLabel? _log;
    private LineEdit? _input;
    private int _logLines;

    public event Action<string>? CommandSubmitted;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        Name = "DevConsolePanel";
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        // Below the speed bar and status line, which stay visible and clickable.
        OffsetTop = 72;
        OffsetBottom = 372;
        Visible = false;
        BuildUi();
        Print("Console dev · scrivi aiuto per i comandi · F1 per chiudere");
    }

    public void Toggle()
    {
        if (Visible)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void Open()
    {
        Visible = true;
        _input?.CallDeferred(Control.MethodName.GrabFocus);
    }

    public void Close()
    {
        Visible = false;
        _input?.ReleaseFocus();
    }

    public void Print(string line)
    {
        if (_log is null)
        {
            return;
        }

        if (_logLines >= MaxLogLines)
        {
            _log.RemoveParagraph(0);
            _logLines--;
        }

        _log.AddText(line + "\n");
        _logLines++;
    }

    public void ClearLog()
    {
        _log?.Clear();
        _logLines = 0;
    }

    /// <summary>Before the GUI sees keys: F1 / Esc / Tab / history must not reach the LineEdit or the world.</summary>
    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true } key)
        {
            return;
        }

        if (key.Keycode == Key.F1 && !key.Echo)
        {
            Toggle();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!Visible || _input is null)
        {
            return;
        }

        switch (key.Keycode)
        {
            case Key.Escape:
                Close();
                break;
            case Key.Up:
                RecallHistory(-1);
                break;
            case Key.Down:
                RecallHistory(1);
                break;
            case Key.Tab:
                CompleteCommand();
                break;
            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    private void BuildUi()
    {
        var panel = HudStyle.MakePanel("DevConsole");
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(panel);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        panel.AddChild(column);

        _log = new RichTextLabel
        {
            ScrollFollowing = true,
            SelectionEnabled = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.None
        };
        _log.AddThemeColorOverride("default_color", HudStyle.TextPrimary);
        _log.AddThemeFontSizeOverride("normal_font_size", 14);
        column.AddChild(_log);

        _input = new LineEdit
        {
            PlaceholderText = "comando… (aiuto)",
            CaretBlink = true,
            ClearButtonEnabled = true,
            // Godot 4.4 leaves edit mode on Enter by default: the next command would be lost.
            KeepEditingOnTextSubmit = true
        };
        _input.AddThemeFontSizeOverride("font_size", 15);
        _input.TextSubmitted += OnSubmitted;
        column.AddChild(_input);
    }

    private void OnSubmitted(string text)
    {
        _input!.Clear();
        var line = text.Trim();
        if (line.Length == 0)
        {
            return;
        }

        if (_history.Count == 0 || _history[^1] != line)
        {
            _history.Add(line);
        }

        _historyIndex = _history.Count;
        Print("> " + line);
        CommandSubmitted?.Invoke(line);
    }

    private void RecallHistory(int direction)
    {
        if (_history.Count == 0 || _input is null)
        {
            return;
        }

        _historyIndex = Math.Clamp(_historyIndex + direction, 0, _history.Count);
        _input.Text = _historyIndex < _history.Count ? _history[_historyIndex] : "";
        _input.CaretColumn = _input.Text.Length;
    }

    private void CompleteCommand()
    {
        if (_input is null || _input.Text.Contains(' '))
        {
            return;
        }

        var matches = DevConsole.Complete(_input.Text);
        if (matches.Count == 1)
        {
            _input.Text = matches[0] + " ";
            _input.CaretColumn = _input.Text.Length;
        }
        else if (matches.Count > 1)
        {
            Print(string.Join("  ", matches));
        }
    }
}
