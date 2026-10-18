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

        var core = new GridPosition(_view.CoreTiles.Min(t => t.X), _view.CoreTiles.Min(t => t.Y));
        _playtestSimHeld = true;
        try
        {
            foreach (var variante in new[] { VarianteColori.Normale, VarianteColori.Daltonici, VarianteColori.AltoContrasto })
            {
                if (!await PlaytestApplyColoursAsync(variante))
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

    /// <summary>Impostazioni → Colori_&lt;variant&gt; → Applica: colours, theme and saved setting follow at once.</summary>
    private async Task<bool> PlaytestApplyColoursAsync(VarianteColori variante)
    {
        OpenSettingsAtInterface();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!PressSetting($"Colori_{variante}") || !PressSetting("Applica"))
        {
            return Fail($"impostazioni: colori {variante} non trovati");
        }

        _settingsPanel?.Close();
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
}
