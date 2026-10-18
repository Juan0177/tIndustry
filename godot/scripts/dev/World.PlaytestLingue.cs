using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Language probe of the <c>ui</c> playtest (G4): a refusal toast in the run's language, then the other language chosen
/// in Impostazioni and applied at once (open panel and HUD rewritten), then back. <c>TINDUSTRY_PLAYTEST_LINGUA=en</c>
/// runs a whole playtest in English (the temp profile otherwise starts in Italian).
/// </summary>
public partial class World
{
    /// <summary>Env var: language code (<see cref="Testi.Lingue"/>) for a whole playtest.</summary>
    private const string PlaytestLanguageEnv = "TINDUSTRY_PLAYTEST_LINGUA";

    /// <summary>In a playtest, the language of <see cref="PlaytestLanguageEnv"/>, else Italian whatever the settings say.</summary>
    private void ApplyPlaytestLanguage()
    {
        if (!PlaytestRequested)
        {
            return;
        }

        var code = OS.GetEnvironment(PlaytestLanguageEnv);
        _settings.Lingua = Testi.Lingue.Contains(code) ? code : Testi.LinguaPredefinita;
        GD.Print($"PLAYTEST lingua {_settings.Lingua}");
    }

    /// <summary>A click with the miner on the Core is refused: the toast says why, in the current language.</summary>
    private async Task<bool> PlaytestRefusalToastAsync(Func<string, Task> shot)
    {
        var core = new GridPosition(_view!.CoreTiles.Min(t => t.X), _view.CoreTiles.Min(t => t.Y));
        await PlaytestKey(Key.Key2);
        await PlaytestClick(CellToScreen(core));
        await PlaytestWait(0.2);
        var expected = Testi.Rifiuto(RefusalCode.OnCore);
        var toast = _hud?.ToastText;
        await shot("rifiuto");
        await PlaytestKey(Key.Escape);
        return toast == expected || Fail($"rifiuto sul Core: toast «{toast}» invece di «{expected}»");
    }

    /// <summary>
    /// Impostazioni → the other language → Applica: the locale, the saved setting, a key and a composed label of the
    /// open panel (UI scale line) change at once; then the run's language comes back the same way.
    /// </summary>
    private async Task<bool> PlaytestLanguageSwitchAsync(Func<string, Task> shot)
    {
        // A settings file without the language, or with an unknown one, plays in Italian.
        foreach (var (json, expected) in new[] { ("{}", "it"), ("{ \"lingua\": \"xx\" }", "it"), ("{ \"lingua\": \"en\" }", "en") })
        {
            if (ClientSettings.FromJson(json).Lingua != expected)
            {
                return Fail($"impostazioni {json}: lingua {ClientSettings.FromJson(json).Lingua} invece di {expected}");
            }
        }

        // Texts that must follow the switch too: the info strip (miner selected), the "?" and funds tooltips and a
        // Market status line written before it (one sale).
        ToggleMercato();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (_mercato?.FindChildren("*", "Button", recursive: true, owned: false).OfType<Button>()
                .FirstOrDefault(b => b.Text == "mercato.vendi.uno") is not { } sellOne)
        {
            return Fail("mercato: nessun pulsante Vendi 1 per il cambio lingua");
        }

        sellOne.EmitSignal(BaseButton.SignalName.Pressed);
        if (!await PlaytestWaitUntil(() => _mercato.StatusText.Length > 0, StepTimeout))
        {
            return Fail("mercato: nessuna riga di stato dopo Vendi 1");
        }

        ToggleMercato();
        // After the Market: opening a panel goes back to the cursor.
        _hud?.SetSelectedTool(Hud.ToolKind.Miner);
        var start = _settings.Lingua;
        var other = Testi.Lingue.First(code => code != start);
        foreach (var code in new[] { other, start })
        {
            OpenSettingsAtInterface();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!PressSetting($"Lingua_{code}") || !PressSetting("Applica"))
            {
                return Fail($"impostazioni: lingua {code} non trovata");
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var scaleLine = (_settingsPanel?.FindChild("ScalaUiValore", recursive: true, owned: false) as Label)?.Text;
            var expectedLine = Testi.F("impostazioni.scala.ui", ClientSettings.UiScaleLabel(_settings.UiScalePercent));
            var research = Testi.T("menu.ricerca");
            if (TranslationServer.GetLocale() != code || ClientSettings.Load().Lingua != code
                || research != (code == "en" ? "Research" : "Ricerca") || scaleLine != expectedLine)
            {
                return Fail($"lingua {code}: locale {TranslationServer.GetLocale()}, salvata {ClientSettings.Load().Lingua}, "
                    + $"«{research}», riga scala «{scaleLine}» invece di «{expectedLine}»");
            }

            var en = code == "en";
            var (strip, detail, funds) = _hud!.InfoTexts;
            var status = _mercato!.StatusText;
            if (!strip.StartsWith(Testi.Struttura(Miner.BasicId), StringComparison.Ordinal)
                || detail != (en ? "Block details" : "Dettaglio blocco") || funds != (en ? "Funds" : "Fondi")
                || !status.StartsWith(en ? "Sold" : "Venduti", StringComparison.Ordinal))
            {
                return Fail($"lingua {code}: testi non riscritti · striscia «{strip}», ? «{detail}», fondi «{funds}», mercato «{status}»");
            }

            GD.Print($"PLAYTEST lingua {code} applicata subito");
            if (code == other)
            {
                await shot($"impostazioni-{code}");
            }

            _settingsPanel?.Close();
        }

        _hud?.ClearToolSelection();
        return true;
    }
}
