using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// UI scale probe of the <c>ui</c> playtest (G4): at 100%, 125%, 150% and 200% every panel is opened and its controls are
/// checked (no visible text wider than its rectangle, every panel inside the window, HUD panels not on top of each
/// other); at 200% the world must keep its size and a mouse click must land on the cell under the cursor.
/// <c>TINDUSTRY_PLAYTEST_SCALA=&lt;percent&gt;</c> runs a whole playtest at that UI scale.
/// </summary>
public partial class World
{
    /// <summary>Env var: UI scale percent for a whole playtest (the temp profile otherwise has the default).</summary>
    private const string PlaytestScaleEnv = "TINDUSTRY_PLAYTEST_SCALA";

    /// <summary>The UI scales the probe goes through, in order (125: the old default many settings files keep).</summary>
    private static readonly int[] PlaytestScales = [100, 125, 150, 200];

    /// <summary>Scales at which every panel must fit a 1280×720 window with no text cut.</summary>
    private static bool MustFit(int percent) => percent <= 150;

    /// <summary>
    /// In a playtest, the UI scale of <see cref="PlaytestScaleEnv"/>, else 100% whatever the settings say (before the
    /// HUD is built), so captures stay comparable.
    /// </summary>
    private void ApplyPlaytestUiScale()
    {
        if (!PlaytestRequested)
        {
            return;
        }

        _settings.UiScalePercent = int.TryParse(OS.GetEnvironment(PlaytestScaleEnv), out var percent)
            ? ClientSettings.NormalizeUiScale(percent)
            : ClientSettings.UiScaleDefault;
        GD.Print($"PLAYTEST scala UI {_settings.UiScalePercent}%");
    }

    private async Task<bool> PlaytestUiScaleAsync(Func<string, Task> shot)
    {
        if (_settingsPanel is null || _hud is null || _view is null)
        {
            return Fail("scala UI: pannelli mancanti");
        }

        // Range, step and old values (100/125/150/200 stay as they are).
        foreach (var (json, expected) in new[] { (125, 125), (137, 135), (50, 75), (300, 200), (100, 100), (200, 200) })
        {
            if (ClientSettings.FromJson($"{{ \"uiScalePercent\": {json} }}").UiScalePercent != expected)
            {
                return Fail($"scala UI: {json}% letto come {ClientSettings.FromJson($"{{ \"uiScalePercent\": {json} }}").UiScalePercent}%, atteso {expected}%");
            }
        }

        var startPercent = _settings.UiScalePercent;
        var worldView = GetViewport().GetCanvasTransform();
        foreach (var percent in PlaytestScales)
        {
            if (!await PlaytestSetUiScaleAsync(percent))
            {
                return Fail($"scala UI {percent}%: non applicata (fattore {UiScale.Factor}, livello {GetNode<CanvasLayer>("Hud").Scale})");
            }

            // The world keeps its size and place: only the HUD layer is scaled.
            if (GetTree().Root.ContentScaleFactor != 1f || !GetViewport().GetCanvasTransform().IsEqualApprox(worldView))
            {
                return Fail($"scala UI {percent}%: il mondo cambia (content scale {GetTree().Root.ContentScaleFactor}, "
                    + $"vista {GetViewport().GetCanvasTransform()} invece di {worldView})");
            }

            if (!await PlaytestPanelsFitAsync(percent, shot))
            {
                return false;
            }

            if (percent == 200 && !await PlaytestClickAtScaleAsync(percent, shot))
            {
                return false;
            }
        }

        if (!await PlaytestSetUiScaleAsync(startPercent))
        {
            return Fail("scala UI: ripristino non riuscito");
        }

        return true;
    }

    /// <summary>Sets the UI scale through Impostazioni (slider and Applica), as a player would.</summary>
    private async Task<bool> PlaytestSetUiScaleAsync(int percent)
    {
        OpenSettings();
        _settingsPanel!.SetUiScaleDraft(percent);
        if (!PressSetting("Applica"))
        {
            return false;
        }

        _settingsPanel.Close();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var layerScale = GetNode<CanvasLayer>("Hud").Scale;
        return _settings.UiScalePercent == percent && ClientSettings.Load().UiScalePercent == percent
            && Math.Abs(UiScale.Factor - percent / 100f) < 1e-4f && Math.Abs(layerScale.X - percent / 100f) < 1e-4f
            && UiRoot.Size.IsEqualApprox(GetViewport().GetVisibleRect().Size / UiScale.Factor);
    }

    /// <summary>Every panel in turn at <paramref name="percent"/>: open, check the layout, capture, close.</summary>
    private async Task<bool> PlaytestPanelsFitAsync(int percent, Func<string, Task> shot)
    {
        async Task<bool> Check(string name, bool hudOnly = false)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var problems = PlaytestLayoutProblems(hudOnly);
            if (problems.Count > 0 && !MustFit(percent))
            {
                // Above 150% a 1280×720 window is too small for every panel (the plan asks 100% and 150%): noted only.
                GD.Print($"PLAYTEST scala {percent}% · {name}: {problems.Count} controlli fuori posto (ammesso sopra 150%), il primo: {problems[0]}");
            }
            else if (problems.Count > 0)
            {
                foreach (var p in problems.Take(8))
                {
                    GD.PrintErr($"  {p}");
                }

                return Fail($"scala UI {percent}% · {name}: {problems.Count} controlli fuori posto (il primo: {problems[0]})");
            }
            else
            {
                GD.Print($"PLAYTEST scala {percent}% · {name}: rettangoli ok");
            }

            await shot($"scala-{percent}-{name}");
            return true;
        }

        if (!await Check("hud", hudOnly: true))
        {
            return false;
        }

        _hud!.SetSelectedTool(Hud.ToolKind.Miner);
        var tutorialStep = TutorialPanel.Step.Miner;
        _tutorial?.Open(_settings, (int)tutorialStep);
        var ok = await Check("tutorial", hudOnly: true);
        _tutorial?.Close();
        _hud.ClearToolSelection();
        if (!ok)
        {
            return false;
        }

        // Ricerca: H shows the whole tree (zoom below the readable floor just for this view).
        ToggleResearch();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await PlaytestKey(Key.H);
        var wholeTree = _research?.WholeTreeInView == true;
        await shot($"scala-{percent}-ricerca-intera");
        ToggleResearch();
        if (!wholeTree && MustFit(percent))
        {
            return Fail($"scala UI {percent}%: H non mostra tutto l'albero della ricerca");
        }

        foreach (var (name, open, close) in new (string, Action, Action)[]
                 {
                     ("ricerca", ToggleResearch, ToggleResearch),
                     ("mercato", ToggleMercato, ToggleMercato),
                     ("campagna", ToggleCampaign, () => _campaignSelect?.Close()),
                     ("impostazioni", OpenSettingsAtInterface, () => _settingsPanel?.Close()),
                     ("salvataggi", OpenSavesManager, () => { _saves?.Close(); _home?.Close(); }),
                     ("nuova-partita", () => _newGame?.Open(PlaytestSeed), () => _newGame?.Close()),
                     ("home", () => _home?.Open(), () => _home?.Close())
                 })
        {
            open();
            ok = await Check(name);
            close();
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Impostazioni with its options scrolled to the end, where the Interfaccia section and the UI scale slider are.</summary>
    private void OpenSettingsAtInterface()
    {
        OpenSettings();
        if (_settingsPanel?.FindChild("Opzioni", recursive: true, owned: false) is ScrollContainer options)
        {
            Callable.From(() => options.ScrollVertical = (int)options.GetVScrollBar().MaxValue).CallDeferred();
        }
    }

    /// <summary>
    /// At <paramref name="percent"/>: a HUD button clicked with the mouse opens its panel, then the belt placed with a
    /// mouse click lands on the cell under the cursor (plan there) and a right click there takes the plan away.
    /// </summary>
    private async Task<bool> PlaytestClickAtScaleAsync(int percent, Func<string, Task> shot)
    {
        // HUD side: the Mercato button, clicked where it is drawn (its UI rect × the factor, in window pixels).
        if (_hud!.FindChildren("*", nameof(Button), recursive: true, owned: false)
                .OfType<Button>().FirstOrDefault(b => b.Text == "menu.mercato" && b.IsVisibleInTree()) is not { } mercato)
        {
            return Fail($"scala UI {percent}%: pulsante Mercato non trovato");
        }

        // Its tooltip (a popup sub-window) follows the UI scale too.
        var onButton = ViewportToWindow(UiToViewport(mercato.GetGlobalRect().GetCenter()));
        PlaytestMove(onButton, 0);
        await PlaytestWait(1.2);
        await shot($"scala-{percent}-suggerimento");
        await PlaytestClick(onButton);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (_mercato?.IsOpen != true)
        {
            return Fail($"scala UI {percent}%: il clic sul pulsante Mercato non lo apre");
        }

        ToggleMercato();

        // World side: belt (key 1) on a free cell near the Core, away from every HUD panel.
        await PlaytestKey(Key.Key1);
        if (PlaytestFreeCellOutsideHud(1) is not { } cell)
        {
            return Fail($"scala UI {percent}%: nessuna cella libera fuori dai pannelli");
        }

        var at = CellToScreen(cell);
        PlaytestMove(at, 0);
        // Long enough for the Mercato tooltip to go away (a click while it shows would only close it).
        await PlaytestWait(0.5);
        if (_hover != cell)
        {
            return Fail($"scala UI {percent}%: cursore su {cell} ({at}), cella sotto il mouse {_hover?.ToString() ?? "nessuna"}, controllo {GetViewport().GuiGetHoveredControl()?.GetPath().ToString() ?? "nessuno"}");
        }

        await PlaytestClick(at);
        // The command lands on the next simulation tick (a frame can be shorter than a tick).
        var view = _view!;
        bool PlacedThere() => view.IsPlannedTile(cell) || view.Sites.Any(site => site.Tiles.Contains(cell));
        if (!await PlaytestWaitUntil(PlacedThere, StepTimeout))
        {
            return Fail($"scala UI {percent}%: il clic su {cell} non ha messo il nastro lì (strumento {_tool}, "
                + $"sotto il mouse {GetViewport().GuiGetHoveredControl()?.GetPath().ToString() ?? "il mondo"}, piani {view.PlansOf(LocalPlayer).Count})");
        }

        GD.Print($"PLAYTEST scala {percent}%: clic sulla cella {cell} (finestra {at}), nastro pianificato lì");
        await shot($"scala-{percent}-clic-cella");
        await PlaytestKey(Key.Escape);

        // Right click on the same cell removes the plan (or marks the site): same cell under the cursor.
        await PlaytestRightClick(at);
        bool BuildPlannedThere() => view.PlansOf(LocalPlayer).Any(p => !p.Breaking && view.PlanTiles(p).Contains(cell));
        if (!await PlaytestWaitUntil(() => !BuildPlannedThere(), StepTimeout))
        {
            return Fail($"scala UI {percent}%: il clic destro su {cell} non toglie il piano");
        }

        return true;
    }

    /// <summary>
    /// Blank height of the Home card (UI units): its height less its frame and its buttons; measured with Home shown for
    /// two frames. About 0 when the card is sized to its content.
    /// </summary>
    private async Task<float> PlaytestHomeCardBlankAsync()
    {
        var wasOpen = _home?.IsOpen == true;
        _home?.Open();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var blank = float.MaxValue;
        if (_home?.GetNodeOrNull<PanelContainer>("HomeCard") is { } card
            && card.GetChildOrNull<ScrollContainer>(0)?.GetChildOrNull<Control>(0) is { } content)
        {
            blank = card.Size.Y - card.GetThemeStylebox("panel").GetMinimumSize().Y - content.GetCombinedMinimumSize().Y;
        }

        if (!wasOpen)
        {
            _home?.Close();
        }

        return blank;
    }

    /// <summary>
    /// A palette showing a category that loses its button (here Potenza, locked again in this level) switches to one
    /// still shown, instead of keeping an empty grid nobody can leave.
    /// </summary>
    private bool PlaytestPaletteCategoryFollowsLocks()
    {
        if (_hud is null || _view is null)
        {
            return Fail("palette: HUD mancante");
        }

        if (!_hud.IsToolLocked(Hud.ToolKind.Generator))
        {
            GD.Print("PLAYTEST palette: Generatore già sbloccato, controllo della categoria saltato");
            return true;
        }

        _hud.SetToolLocked(Hud.ToolKind.Generator, false);
        _hud.SetSelectedTool(Hud.ToolKind.Generator);
        var shown = _hud.PaletteCategory;
        SyncResearchLocks();
        var after = _hud.PaletteCategory;
        _hud.ClearToolSelection();
        if (shown != Hud.BuildCategory.Power || after == Hud.BuildCategory.Power)
        {
            return Fail($"palette: categoria {shown} poi {after} dopo aver bloccato di nuovo la Potenza");
        }

        GD.Print($"PLAYTEST palette: Potenza bloccata di nuovo, la palette passa a {after}");
        return true;
    }

    /// <summary>
    /// The build palette is a fixed grid (Mindustry): the same size early in a level, in every shown category and with
    /// every block unlocked. Captures both: <c>ui-palette-inizio</c> and <c>ui-palette-tutto</c>.
    /// </summary>
    private async Task<bool> PlaytestPaletteFitsAsync(Func<string, Task> shot)
    {
        if (_hud is null)
        {
            return Fail("palette: HUD mancante");
        }

        async Task<Vector2> SizeIn(Hud.BuildCategory category)
        {
            _hud.ShowPaletteCategory(category);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            return _hud.PaletteSize;
        }

        async Task<string?> SameInShownCategories(Vector2 expected)
        {
            foreach (var category in Enum.GetValues<Hud.BuildCategory>().Where(_hud.PaletteCategoryShown))
            {
                if (await SizeIn(category) is var size && !size.IsEqualApprox(expected))
                {
                    return $"{size} in {category} invece di {expected}";
                }
            }

            return null;
        }

        var start = _hud.PaletteCategory;
        var early = await SizeIn(start);
        await shot("palette-inizio");
        var earlyError = await SameInShownCategories(early);
        if (_hud.PaletteColumns != 4 || earlyError is not null)
        {
            return Fail($"palette a inizio livello: {_hud.PaletteColumns} colonne, {earlyError ?? "misura uguale"}");
        }

        foreach (var tool in Enum.GetValues<Hud.ToolKind>())
        {
            _hud.SetToolLocked(tool, false);
        }

        var full = await SizeIn(Hud.BuildCategory.Logistics);
        await shot("palette-tutto");
        var fullError = await SameInShownCategories(early);
        SyncResearchLocks();
        var back = await SizeIn(start);
        if (fullError is not null || !back.IsEqualApprox(early))
        {
            return Fail($"palette: {early} a inizio livello, con tutto sbloccato {fullError ?? full.ToString()}, {back} dopo");
        }

        GD.Print($"PLAYTEST palette a griglia fissa: {early} a inizio livello e con tutto sbloccato");
        return true;
    }

    private async Task PlaytestRightClick(Vector2 at)
    {
        PlaytestMove(at, 0);
        Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Right, Pressed = true, Position = at, GlobalPosition = at,
            ButtonMask = MouseButtonMask.Right, Device = PlaytestDevice
        });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Right, Pressed = false, Position = at, GlobalPosition = at, Device = PlaytestDevice
        });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>A point of the HUD (UI units) in viewport pixels.</summary>
    private static Vector2 UiToViewport(Vector2 ui) => ui * UiScale.Factor;

    /// <summary>
    /// Rectangles (UI units) of the HUD panels shown over the world: visible PanelContainers with no PanelContainer
    /// above them, outside modal panels.
    /// </summary>
    private List<(string Name, Rect2 Rect)> PlaytestHudPanelRects()
    {
        var rects = new List<(string, Rect2)>();
        void Walk(Node node)
        {
            foreach (var child in node.GetChildren())
            {
                if (child is not Control { Visible: true } control)
                {
                    continue;
                }

                // Panels and loose buttons (the top bar): both take the mouse.
                if (control is PanelContainer or BaseButton && control.Size.X > 0 && control.Size.Y > 0)
                {
                    rects.Add((control.Name, control.GetGlobalRect()));
                    continue;
                }

                Walk(control);
            }
        }

        if (_hud is not null)
        {
            Walk(_hud);
        }

        if (_tutorial is { IsOpen: true })
        {
            Walk(_tutorial);
        }

        return rects;
    }

    /// <summary>
    /// The free footprint of <paramref name="size"/> nearest to the Core (rings 3 to 10, fixed order) that no HUD panel
    /// covers, or null.
    /// </summary>
    private GridPosition? PlaytestFreeCellOutsideHud(int size)
    {
        if (_view is null || _view.CoreTiles.Count == 0)
        {
            return null;
        }

        var rects = PlaytestHudPanelRects();
        var centre = new GridPosition(_view.CoreTiles.Min(t => t.X), _view.CoreTiles.Min(t => t.Y));
        for (var r = 3; r <= 10; r++)
        {
            for (var dy = -r; dy <= r; dy++)
            {
                for (var dx = -r; dx <= r; dx++)
                {
                    var cell = new GridPosition(centre.X + dx, centre.Y + dy);
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && _view.CanOccupyFootprint(cell, size)
                        && !FootprintPlanned(cell, size) && !PlaytestCellUnderHud(cell, size, rects))
                    {
                        return cell;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>True when a cell of the footprint at <paramref name="origin"/> is under one of <paramref name="rects"/>.</summary>
    private bool PlaytestCellUnderHud(GridPosition origin, int size, List<(string Name, Rect2 Rect)> rects)
    {
        var canvas = GetViewport().GetCanvasTransform();
        var topLeft = UiScale.ToUi(canvas * new Vector2(origin.X * TileSize, origin.Y * TileSize));
        var bottomRight = UiScale.ToUi(canvas * new Vector2((origin.X + size) * TileSize, (origin.Y + size) * TileSize));
        var cells = new Rect2(topLeft, bottomRight - topLeft);
        return rects.Any(r => r.Rect.Intersects(cells));
    }

    /// <summary>
    /// Layout problems of the visible HUD: a Label or Button whose text is wider than its rectangle (or a wrapped
    /// Label with lines it cannot show), a control outside the window (except inside a scroll or clipping parent), and
    /// with <paramref name="hudOnly"/> two HUD panels on top of each other.
    /// </summary>
    private List<string> PlaytestLayoutProblems(bool hudOnly)
    {
        var problems = new List<string>();
        var root = UiRoot;
        var window = new Rect2(Vector2.Zero, root.Size).Grow(0.5f);

        void Walk(Node node, bool clipped)
        {
            foreach (var child in node.GetChildren())
            {
                if (child is not Control { Visible: true } control || control.Modulate.A < 0.01f || control.SelfModulate.A < 0.01f)
                {
                    continue;
                }

                var rect = control.GetGlobalRect();
                if (!clipped && rect.Size.X > 0 && rect.Size.Y > 0 && control is PanelContainer or Label or BaseButton
                    && !window.Encloses(rect))
                {
                    problems.Add($"{control.GetPath()} fuori dalla finestra: {rect} in {root.Size}");
                }

                if (TextOverflow(control) is { } overflow)
                {
                    problems.Add($"{control.GetPath()} {overflow}");
                }

                Walk(control, clipped || control is ScrollContainer || control.ClipContents);
            }
        }

        Walk(root, clipped: false);

        if (hudOnly)
        {
            var rects = PlaytestHudPanelRects();
            for (var i = 0; i < rects.Count; i++)
            {
                for (var j = i + 1; j < rects.Count; j++)
                {
                    if (rects[i].Rect.Grow(-1f).Intersects(rects[j].Rect.Grow(-1f)))
                    {
                        problems.Add($"pannelli sovrapposti: {rects[i].Name} {rects[i].Rect} e {rects[j].Name} {rects[j].Rect}");
                    }
                }
            }
        }

        return problems;
    }

    /// <summary>
    /// Why the text of <paramref name="control"/> does not fit its rectangle, or null. A text that is a key is measured
    /// as shown, translated.
    /// </summary>
    private static string? TextOverflow(Control control)
    {
        switch (control)
        {
            case Label label when !string.IsNullOrEmpty(label.Text):
            {
                var shown = Testi.T(label.Text);
                if (label.AutowrapMode != TextServer.AutowrapMode.Off)
                {
                    return label.MaxLinesVisible < 0 && label.GetVisibleLineCount() < label.GetLineCount()
                        ? $"testo a capo tagliato: {label.GetVisibleLineCount()} righe su {label.GetLineCount()} («{Short(shown)}»)"
                        : null;
                }

                var font = label.GetThemeFont("font");
                var size = label.GetThemeFontSize("font_size");
                var style = label.GetThemeStylebox("normal");
                var room = label.Size.X - style.GetMinimumSize().X;
                var widest = shown.Split('\n').Max(line => font.GetStringSize(line, HorizontalAlignment.Left, -1, size).X);
                return widest > room + 0.5f ? $"testo largo {widest:0} in {room:0} («{Short(shown)}»)" : null;
            }
            case Button button when !string.IsNullOrEmpty(button.Text) && button.AutowrapMode == TextServer.AutowrapMode.Off:
            {
                var font = button.GetThemeFont("font");
                var size = button.GetThemeFontSize("font_size");
                var style = button.GetThemeStylebox("normal");
                var shown = Testi.T(button.Text);
                var text = shown.Split('\n').Max(line => font.GetStringSize(line, HorizontalAlignment.Left, -1, size).X);
                var icon = 0f;
                var iconTexture = button is CheckButton or CheckBox ? button.GetThemeIcon("unchecked") : button.Icon;
                if (iconTexture is not null)
                {
                    var maxWidth = button.GetThemeConstant("icon_max_width");
                    var width = iconTexture.GetWidth();
                    icon = (maxWidth > 0 ? Math.Min(width, maxWidth) : width) + button.GetThemeConstant("h_separation");
                }

                var needed = text + icon + style.GetMinimumSize().X;
                return needed > button.Size.X + 0.5f ? $"pulsante: serve {needed:0} in {button.Size.X:0} («{Short(shown)}»)" : null;
            }
            default:
                return null;
        }
    }

    private static string Short(string text) => text.Length <= 40 ? text.Replace('\n', ' ') : text[..40].Replace('\n', ' ') + "…";
}
