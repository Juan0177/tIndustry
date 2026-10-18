using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Accessibility probe of the <c>ui</c> playtest (G4 5/6): each colour variant chosen in Impostazioni and applied at
/// once (theme rebuilt, setting saved), captured on the HUD with the miner ghost valid and refused (X) and the removal
/// marks (hatched) of the seed line; then the miner ghost on a titanium-only deposit, refused by
/// <see cref="Factory.CanPlace"/> (red, X, hint). The simulation is held during the captures so the marks stay put.
/// </summary>
public partial class World
{
    /// <summary>True while a playtest holds the simulation (World._Process skips AdvanceHost).</summary>
    private bool _playtestSimHeld;

    private async Task<bool> PlaytestColourVariantsAsync(Func<string, Task> shot)
    {
        if (_view is null || GetNodeOrNull<Camera2D>("Camera") is not { } cam)
        {
            return Fail("varianti di colore: nessuna partita o telecamera");
        }

        // A settings file without the variant, or with an unknown one, keeps the normal colours.
        foreach (var (json, expected) in new[]
                 {
                     ("{}", VarianteColori.Normale), ("{ \"colori\": \"xx\" }", VarianteColori.Normale),
                     ("{ \"colori\": \"Daltonici\" }", VarianteColori.Daltonici)
                 })
        {
            if (ClientSettings.FromJson(json).Colori != expected)
            {
                return Fail($"impostazioni {json}: colori {ClientSettings.FromJson(json).Colori} invece di {expected}");
            }
        }

        if (!await PlaytestBridgeGhostOnPlanAsync())
        {
            return false;
        }

        var core = new GridPosition(_view.CoreTiles.Min(t => t.X), _view.CoreTiles.Min(t => t.Y));
        _playtestSimHeld = true;
        try
        {
            foreach (var variante in new[] { VarianteColori.Normale, VarianteColori.Daltonici, VarianteColori.AltoContrasto })
            {
                if (!await PlaytestApplyColoursAsync(variante, shot))
                {
                    return false;
                }

                var name = variante.ToString().ToLowerInvariant();
                // Removal marks of the seed line (hatched), the HUD around them.
                cam.Position = CellCenter(new GridPosition(core.X - 2, core.Y));
                cam.Zoom = new Vector2(1.6f, 1.6f);
                await shot($"colori-{name}-smontaggio");

                // The miner ghost on free ground and on the Core (refused: red outline and X), both in view.
                await PlaytestKey(Key.Key2);
                if (PlaytestFreeFootprint(Miner.Size) is not { } free)
                {
                    return Fail("varianti di colore: nessuna cella libera per il fantasma");
                }

                cam.Position = (CellCenter(free) + CellCenter(core)) / 2f;
                cam.Zoom = Vector2.One;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                PlaytestMove(CellToScreen(free), 0);
                await shot($"colori-{name}-valido");
                PlaytestMove(CellToScreen(core), 0);
                await shot($"colori-{name}-non-valido");
                await PlaytestKey(Key.Escape);
                FocusCameraOnCore();
            }

            // Back to the normal colours for the rest of the playtest.
            return await PlaytestTitaniumGhostAsync(shot, cam) && await PlaytestApplyColoursAsync(VarianteColori.Normale);
        }
        finally
        {
            _playtestSimHeld = false;
        }
    }

    /// <summary>
    /// Impostazioni → Colori_&lt;variant&gt; → Applica: colours, theme and saved setting follow at once (the open panel
    /// is captured in the new colours when <paramref name="shot"/> is given).
    /// </summary>
    private async Task<bool> PlaytestApplyColoursAsync(VarianteColori variante, Func<string, Task>? shot = null)
    {
        OpenSettingsAtInterface();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!PressSetting($"Colori_{variante}") || !PressSetting("Applica"))
        {
            return Fail($"impostazioni: colori {variante} non trovati");
        }

        if (shot is not null)
        {
            await shot($"colori-{variante.ToString().ToLowerInvariant()}-impostazioni");
        }

        _settingsPanel?.Close();
        // The "settings applied" toast would cover the removal marks of the next capture.
        _hud?.ClearToast();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var label = UiTheme.Theme.GetColor("font_color", "Label");
        var ghost = UiColors.FantasmaNonValido;
        if (UiColors.Variante != variante || ClientSettings.Load().Colori != variante || label != UiColors.Testo)
        {
            return Fail($"colori {variante}: in uso {UiColors.Variante}, salvati {ClientSettings.Load().Colori}, "
                + $"testo del tema {label} invece di {UiColors.Testo}");
        }

        // Valid and refused ghosts, building and taking apart never share a colour.
        if (UiColors.FantasmaValido == ghost || UiColors.Costruzione == UiColors.Smontaggio)
        {
            return Fail($"colori {variante}: significati con lo stesso colore");
        }

        GD.Print($"PLAYTEST colori {variante} applicati subito");
        return true;
    }

    /// <summary>
    /// The basic miner over the titanium-only footprint nearest to the Core (away from the map edge): the ghost is refused with
    /// <see cref="RefusalCode.NeedsAdvancedMiner"/> (from the simulation) and the hint says so.
    /// </summary>
    private async Task<bool> PlaytestTitaniumGhostAsync(Func<string, Task> shot, Camera2D cam)
    {
        var terrain = _view!.Terrain!;
        var core = new GridPosition(_view.CoreTiles.Min(t => t.X), _view.CoreTiles.Min(t => t.Y));
        GridPosition? found = null;
        var best = long.MaxValue;
        const int margin = 8;
        for (var y = margin; y + Miner.Size + margin <= terrain.Height; y++)
        {
            for (var x = margin; x + Miner.Size + margin <= terrain.Width; x++)
            {
                var at = new GridPosition(x, y);
                var d = (long)(x - core.X) * (x - core.X) + (long)(y - core.Y) * (y - core.Y);
                if (d < best
                    && terrain.CountDepositTiles(at, Miner.Size, DepositKind.Titanium) == Miner.Size * Miner.Size
                    && !_view.CanPlace(new BuildSpec(Miner.BasicId, BuildingKind.Miner, at, Direction.East), out var why)
                    && why == RefusalCode.NeedsAdvancedMiner)
                {
                    (found, best) = (at, d);
                }
            }
        }

        if (found is not { } titanium)
        {
            return Fail("nessun giacimento di solo titanio per il fantasma del Minatore T1");
        }

        cam.Position = CellCenter(titanium) + new Vector2(TileSize / 2f, TileSize / 2f);
        cam.Zoom = new Vector2(1.2f, 1.2f);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await PlaytestKey(Key.Key2);
        PlaytestMove(CellToScreen(titanium), 0);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var hint = (_hud?.FindChild("PlacementHint", recursive: true, owned: false) as Label) is { Visible: true } label
            ? label.Text
            : null;
        var expected = Testi.Rifiuto(RefusalCode.NeedsAdvancedMiner);
        var refusal = _hover is { } hover ? GhostRefusal(hover) : null;
        await shot("fantasma-titanio");
        await PlaytestKey(Key.Escape);
        if (refusal != RefusalCode.NeedsAdvancedMiner || hint != expected)
        {
            return Fail($"fantasma del Minatore T1 sul titanio {titanium}: rifiuto {refusal?.ToString() ?? "nessuno"}, "
                + $"suggerimento «{hint}» invece di «{expected}»");
        }

        GD.Print($"PLAYTEST step ok: fantasma del Minatore T1 rosso sul titanio {titanium}");
        FocusCameraOnCore();
        return true;
    }

    /// <summary>
    /// Home background: Impostazioni opened from the Home keep it (no camera input) and closing them goes back to the
    /// Home; with a forced drift speed the camera moves on its own, turns back at the map edges and stays inside.
    /// </summary>
    private async Task<bool> PlaytestHomeDriftAsync()
    {
        if (_home?.IsOpen != true || GetNodeOrNull<WorldCamera>("Camera") is not { } cam)
        {
            return Fail("sfondo della Home: Home chiusa o telecamera assente");
        }

        OpenSettings();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var inputUnderSettings = cam.InputEnabled;
        _settingsPanel?.Close();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (inputUnderSettings || _home?.IsOpen != true)
        {
            return Fail($"Impostazioni dalla Home: input della telecamera {inputUnderSettings}, Home riaperta {_home?.IsOpen}");
        }

        var start = cam.Position;
        var (zoom, last, lastStep) = (cam.Zoom, cam.Position, Vector2.Zero);
        var (turns, travelled) = (0, 0f);
        _playtestDriftSpeed = 6000f;
        try
        {
            // Until it has turned back twice, at most 4 s (frame rate independent).
            var deadline = Time.GetTicksMsec() + 4000;
            while (turns < 2 && Time.GetTicksMsec() < deadline)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var step = cam.Position - last;
                if (step.X * lastStep.X < 0f || step.Y * lastStep.Y < 0f)
                {
                    turns++;
                }

                travelled += step.Length();
                (last, lastStep) = (cam.Position, step == Vector2.Zero ? lastStep : step);
                if (cam.InputEnabled || cam.Position.X < 0f || cam.Position.Y < 0f
                    || cam.Position.X > cam.MapPixelWidth || cam.Position.Y > cam.MapPixelHeight)
                {
                    return Fail($"sfondo della Home: telecamera {cam.Position} fuori dalla mappa o con input {cam.InputEnabled}");
                }
            }
        }
        finally
        {
            _playtestDriftSpeed = null;
            cam.Position = start;
            cam.Zoom = zoom;
        }

        if (travelled < TileSize || turns == 0)
        {
            return Fail($"sfondo della Home: la telecamera ha percorso {travelled:0} px con {turns} rimbalzi");
        }

        GD.Print($"PLAYTEST step ok: sfondo della Home scorre ({travelled:0} px, {turns} rimbalzi), senza input");
        return true;
    }

    /// <summary>
    /// A bridge whose exit falls on a pending belt plan: the ghost is refused (Occupied), as the plan queue refuses the
    /// click, because the ghost checks plans over the whole footprint (entry and exit). The plan is taken back after.
    /// </summary>
    private async Task<bool> PlaytestBridgeGhostOnPlanAsync()
    {
        GridPosition? found = null;
        for (var r = 3; r <= 12 && found is null; r++)
        {
            if (PlaytestFreeFootprint(1, new GridPosition(_view!.CoreTiles.Min(t => t.X) - r, _view.CoreTiles.Max(t => t.Y) + r))
                    is { } cell
                && _view.CanOccupy(cell.Step(Direction.West, BeltGridCell.MinBridgeSpan))
                && !_view.IsPlannedTile(cell.Step(Direction.West, BeltGridCell.MinBridgeSpan)))
            {
                found = cell;
            }
        }

        if (found is not { } exit)
        {
            return Fail("ponte sul piano: nessuna cella libera");
        }

        var entry = exit.Step(Direction.West, BeltGridCell.MinBridgeSpan);
        SubmitCommand(new PlaceBelt(LocalPlayer, exit, Direction.East, BuildSpec.DefaultConveyorId));
        if (!await PlaytestWaitUntil(() => _view!.IsPlannedTile(exit), StepTimeout))
        {
            return Fail($"ponte sul piano: piano del nastro in {exit} non creato");
        }

        var (tool, dir) = (_tool, _placeDir);
        (_tool, _placeDir) = (BuildTool.Bridge, Direction.East);
        var refusal = GhostRefusal(entry);
        var span = GhostAt(entry).Spec is { } spec ? _view!.Footprint(spec) : [];
        (_tool, _placeDir) = (tool, dir);
        TryRemoveAt(exit, onlyMark: false);
        if (!await PlaytestWaitUntil(() => PendingCommands.Count == 0, StepTimeout))
        {
            return Fail("ponte sul piano: piano non tolto");
        }

        if (refusal != RefusalCode.Occupied || span is not [_, var to] || to != exit)
        {
            return Fail($"ponte con l'uscita su un piano: rifiuto {refusal?.ToString() ?? "nessuno"}, "
                + $"ingombro {string.Join(" ", span)} invece di Occupied fino a {exit}");
        }

        GD.Print("PLAYTEST step ok: fantasma del ponte rifiutato con l'uscita su un piano");
        return true;
    }
}
