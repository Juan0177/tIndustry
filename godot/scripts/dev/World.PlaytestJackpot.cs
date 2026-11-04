using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// The jackpot-attempt sequence in the <c>ui</c> playtest (spec 2b), its last step so the earlier captures stay as they
/// were: a market screen placed touching the CORE, an item held in a spike with 500 pieces and the roll forced to a
/// miss, then two real «Vendi tutto». The first plays whole, held still for a capture in «Calcolo» and one in «Risultato»
/// (rollers stopped); the second is skipped with a left click after <see cref="JackpotSequence.SkipGuardSeconds"/>
/// (to the result) and closed by a second click. HUD, camera input and world input must be back after each. Screen,
/// wallet, market and roll are put back after.
/// </summary>
public partial class World
{
    /// <summary>Pieces of the sold item: over the attempt's minimum (400) and under the yellow LED (10,000).</summary>
    private const int PlaytestJackpotPieces = 500;

    /// <summary>Longest real wait for a whole sequence (12 s) in the playtest.</summary>
    private const double PlaytestJackpotTimeout = 20;

    private async Task<bool> PlaytestJackpotSequenceAsync(Func<string, Task> shot)
    {
        if (_view is not Factory factory || factory.Content.MarketRules is not { } rules || _jackpotView is null
            || GetNodeOrNull<WorldCamera>("Camera") is not { } cam)
        {
            return Fail("sequenza-jackpot: nessuna partita, nessun mercato o nessuna telecamera");
        }

        if (factory.State.MarketScreen is not null)
        {
            return Fail("sequenza-jackpot: c'è già uno schermo di borsa nella partita");
        }

        const string item = "iron-plate";
        var exchange = factory.State.Exchange;
        if (exchange.Find(item) is not { } market)
        {
            return Fail($"sequenza-jackpot: {item} non è nel mercato");
        }

        DeselectBlock(toast: false);
        _hud?.ClearToast();
        var (money, materials) = (factory.Wallet.Money, factory.Wallet.MaterialsSnapshot());

        // The screen on one side of the CORE, edge to edge (the first side with free ground), placed at once.
        var (minX, minY) = (factory.CoreTiles.Min(t => t.X), factory.CoreTiles.Min(t => t.Y));
        var (maxX, maxY) = (factory.CoreTiles.Max(t => t.X), factory.CoreTiles.Max(t => t.Y));
        GridPosition? placed = null;
        foreach (var origin in new[]
                 {
                     new GridPosition(minX - MarketScreen.Size, minY), new GridPosition(maxX + 1, minY),
                     new GridPosition(minX, maxY + 1), new GridPosition(minX, minY - MarketScreen.Size)
                 })
        {
            if (factory.PlaceInstant(new BuildSpec(MarketScreen.BuildingId, BuildingKind.MarketScreen, origin, Direction.East), charge: false))
            {
                if (factory.State.MarketScreen is { Active: true })
                {
                    placed = origin;
                    break;
                }

                factory.RemoveInstant(origin, refund: false);
            }
        }

        if (placed is not { } screenAt)
        {
            return Fail("sequenza-jackpot: nessun posto per lo schermo di borsa a contatto col CORE");
        }

        _buildingsDirty = true;
        _visualDirty = true;
        PlaytestJackpotSequence = true;
        factory.State.ForPlaytestForceJackpotRoll(false);
        PlaytestHeld = true;
        try
        {
            // A spike with a known chart: the glass shows it during «Corsa» and «Ritorno».
            void Spike()
            {
                static int Wave(int i) => 1250 + (int)Math.Round(30 * Math.Sin(i / 7.0));
                var points = Enumerable.Range(0, rules.HistorySeconds)
                    .Select(i => i < 100 ? Wave(i) : i < 110 ? 1250 + 170 * (i - 100) : 2950).ToArray();
                market.ForPlaytestSetHistory(points);
                market.Phase = MarketPhase.SpikeHold;
                market.PriceMilli = market.TrendMilli = market.StepFromMilli = market.StepTargetMilli = points[^1];
                market.PhaseStartTick = factory.CurrentTick - SimulationClock.TickRate;
                market.SpikeFromMilli = 1250;
                market.SpikeTopMilli = 2950;
                market.SaleDropMicro = 0;
                Array.Clear(market.WindowPieces);
                exchange.SellAllBlockedUntilTick = 0; // the wait after the first miss
                factory.Wallet.AddMaterial(item, PlaytestJackpotPieces - factory.Wallet.MaterialCount(item));
            }

            // One real «Vendi tutto», as the Mercato's button sends it; true once its sequence runs.
            async Task<bool> Attempt(string label)
            {
                Spike();
                var starts = JackpotSequenceStarts;
                SubmitCommand(new SellAll(LocalPlayer, item));
                PlaytestHeld = false;
                var started = await PlaytestWaitUntil(() => JackpotSequenceStarts > starts && JackpotSequenceRunning, StepTimeout);
                PlaytestHeld = true; // the world stands still: the sequence runs on real time all the same
                if (!started || _jackpotView.Sequence?.Sale is not { Attempt: true, Jackpot: false, Pieces: PlaytestJackpotPieces })
                {
                    return Fail($"sequenza-jackpot {label}: la sequenza di un tentativo perso di {PlaytestJackpotPieces} pezzi non parte "
                        + $"(stato «{_mercato?.StatusText}»)");
                }

                if (_hud?.ChromeVisible != false || cam.InputEnabled || _mercato?.IsOpen == true)
                {
                    return Fail($"sequenza-jackpot {label}: HUD visibile, telecamera con input o Mercato aperto durante la sequenza");
                }

                return true;
            }

            // Back to play: no sequence, the HUD and the camera's input back, the camera on the drone again.
            async Task<bool> Ended(string label)
            {
                if (!await PlaytestWaitUntil(() => !JackpotSequenceRunning, PlaytestJackpotTimeout))
                {
                    return Fail($"sequenza-jackpot {label}: la sequenza non finisce (passo {_jackpotView.Sequence?.Step})");
                }

                var back = await PlaytestWaitUntil(() => !cam.Focusing, 3);
                await PlaytestWait(0.1);
                if (!back || _hud?.ChromeVisible != true || !cam.InputEnabled || JackpotBlocksInput)
                {
                    return Fail($"sequenza-jackpot {label}: dopo la fine telecamera ancora sullo schermo ({!back}), "
                        + $"HUD {_hud?.ChromeVisible}, input della telecamera {cam.InputEnabled}");
                }

                if (factory.State.MarketScreen?.LastResult is not { Jackpot: false } result || result.ItemId != item
                    || factory.Wallet.MaterialCount(item) != 0)
                {
                    return Fail($"sequenza-jackpot {label}: esito sullo schermo o scorta di {item} sbagliati");
                }

                return true;
            }

            // The sequence held still at a moment of its timeline for a capture (frozen like the player's pause).
            async Task<bool> HoldShot(double at, JackpotStep step, string label)
            {
                if (!await PlaytestWaitUntil(() => _jackpotView.Sequence is not { } s || s.Elapsed >= at, PlaytestJackpotTimeout)
                    || _jackpotView.Sequence?.Step != step)
                {
                    return Fail($"sequenza-jackpot: passo {_jackpotView.Sequence?.Step.ToString() ?? "nessuno"} invece di {step}");
                }

                PlaytestJackpotHold = true;
                try
                {
                    await shot(label);
                }
                finally
                {
                    PlaytestJackpotHold = false;
                }

                return true;
            }

            // 1) A whole lost attempt: «calcolo…» on the glass, then the income rolled in (rollers stop at 70% of the step).
            if (!await Attempt("intera")
                || !await HoldShot(7.0, JackpotStep.Calcolo, "jackpot-calcolo")
                || !await HoldShot(11.5, JackpotStep.Risultato, "jackpot-risultato")
                || !await Ended("intera"))
            {
                return false;
            }

            // The outcome stays on the glass for 6 s from the end; the camera is back on its own zoom by then.
            await PlaytestWait(1.5);
            await shot("jackpot-dopo");

            // 2) Another lost attempt skipped: a click after the guard goes to the result, a second click closes it.
            var centre = ViewportToWindow(GetViewport().GetVisibleRect().GetCenter());
            if (!await Attempt("saltata")
                || !await PlaytestWaitUntil(() => _jackpotView.Sequence is not { } s || s.Elapsed >= JackpotSequence.SkipGuardSeconds + 0.3, StepTimeout))
            {
                return false;
            }

            await PlaytestClick(centre);
            if (_jackpotView.Sequence is not { Step: JackpotStep.Risultato })
            {
                return Fail($"sequenza-jackpot: il clic non salta al risultato (passo {_jackpotView.Sequence?.Step.ToString() ?? "nessuno"})");
            }

            await PlaytestWait(0.3);
            await PlaytestClick(centre);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (JackpotSequenceRunning)
            {
                return Fail("sequenza-jackpot: il secondo clic non chiude la sequenza");
            }

            if (!await Ended("saltata"))
            {
                return false;
            }

            GD.Print("PLAYTEST step ok: sequenza-jackpot (tentativo perso intero con catture, tentativo saltato con due clic)");
            return true;
        }
        finally
        {
            PlaytestJackpotHold = false;
            if (JackpotSequenceRunning)
            {
                EndJackpotSequence(aborted: true);
            }

            PlaytestJackpotSequence = false;
            factory.State.ForPlaytestForceJackpotRoll(null);
            factory.RemoveInstant(screenAt, refund: false);
            _buildingsDirty = true;
            _visualDirty = true;
            PlaytestRestoreWallet(factory, money, materials);
            exchange.Reseed(rules, factory.Terrain?.Seed ?? 0, factory.CurrentTick);
            PlaytestHeld = false;
            FocusCameraOnCore();
            await PlaytestWait(0.1);
        }
    }
}
