using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Client side of the command pipeline: every change to the game is a <see cref="SimCommand"/> sent to the
/// <see cref="SimHost"/>; graphics, toasts and panels react to the host's applied / rejected events.
/// </summary>
public partial class World
{
    /// <summary>The only player until co-op: id used on every command.</summary>
    private const int LocalPlayer = 0;

    // Commands of a released line whose position refusal ("occupied", "on the Core"…) stays silent (the line sweeps
    // over cells that are expected to be blocked). Counted per equal command.
    private readonly Dictionary<SimCommand, int> _quietRefusals = [];

    // Refusal codes a released line already toasted: one toast per code for the whole line (cleared at each release).
    private readonly HashSet<RefusalCode> _lineToasts = [];

    /// <summary>Starts a game on <paramref name="factory"/>: fresh host, so the tick clock restarts at 0.</summary>
    private void AttachGame(Factory factory) => AttachGame(new SimHost(factory));

    /// <summary>Makes <paramref name="host"/> the running game (new game, campaign level or loaded save).</summary>
    private void AttachGame(SimHost host)
    {
        if (_host is not null)
        {
            _host.CommandApplied -= OnCommandApplied;
            _host.CommandRejected -= OnCommandRejected;
            _host.EventRaised -= OnSimEvent;
        }

        _filterPanel?.Close(); // its block belonged to the old game
        EndJackpotSequence(aborted: true); // the sequence belonged to the old game too
        _jackpotPending = null;
        _jackpotResultFrom = null;
        _host = host;
        _host.CommandApplied += OnCommandApplied;
        _host.CommandRejected += OnCommandRejected;
        _host.EventRaised += OnSimEvent;
        ResetTimeControls(host);
        _quietRefusals.Clear();
        _linePlan = null;
        _selectedNode = null;
        CancelLine();
        // The held left button belonged to the old game: its mining and click end with it, nothing sent.
        _handMining = false;
        _clickDown = null;
        _miningHeldOutOfRange = null;
        _miningStallHinted = false;
        _effects?.Clear();
        // Site ids start over in the new game: a site-done event must not find an old game's rect.
        _constructionLayer?.Clear();
        _coreDeliveredSeen = -1;
        _structureCount = StructureCount();
        ConstructFinishedCount = 0;
    }

    /// <summary>Commands submitted but not yet applied (empty without a game).</summary>
    private IReadOnlyList<SimCommand> PendingCommands => _host?.Pending ?? [];

    /// <summary>Drops the running game (back to "no game"): the host stops reaching the world.</summary>
    private void DetachGame()
    {
        if (_host is not null)
        {
            _host.CommandApplied -= OnCommandApplied;
            _host.CommandRejected -= OnCommandRejected;
            _host.EventRaised -= OnSimEvent;
        }

        EndJackpotSequence(aborted: true);
        _jackpotPending = null;
        _jackpotResultFrom = null;
        _host = null;
        _quietRefusals.Clear();
        _effects?.Clear();
    }

    private void SubmitCommand(SimCommand command, bool quietWhenBlocked = false)
    {
        if (_host is null)
        {
            return;
        }

        if (quietWhenBlocked)
        {
            // Same stamp SimHost.Submit applies, so the event's command compares equal.
            var key = command with { Tick = _host.State.CurrentTick + 1 };
            _quietRefusals[key] = _quietRefusals.GetValueOrDefault(key) + 1;
        }

        _host.Submit(command);
    }

    /// <summary>Runs the host; a failing event handler is logged and never stops the game.</summary>
    private void AdvanceHost(double delta)
    {
        try
        {
            _host?.Advance(delta);
        }
        catch (AggregateException ex)
        {
            // The frame still ran completely (SimHost rethrows only after the last tick).
            foreach (var inner in ex.InnerExceptions)
            {
                GD.PushError($"SimHost handler: {inner}");
            }
        }
    }

    private bool TakeQuietRefusal(SimCommand command)
    {
        if (!_quietRefusals.TryGetValue(command, out var count))
        {
            return false;
        }

        if (count <= 1)
        {
            _quietRefusals.Remove(command);
        }
        else
        {
            _quietRefusals[command] = count - 1;
        }

        return true;
    }

    private void OnCommandApplied(SimCommand command)
    {
        _lineInputsVersion++;
        TakeQuietRefusal(command);
        switch (command)
        {
            case SetBlockFilter:
                _visualDirty = true;
                break;
            case PlaceBelt or PlaceBuilding or Remove:
                // Placing and removing only queue plans: the blocks change when a site finishes or a
                // deconstruction starts (OnSimEvent, SyncConstructionVisuals). A game saved with instant
                // build still changes blocks right here, which the structure count catches.
                if (StructureCount() != _structureCount)
                {
                    _visualDirty = true;
                    _buildingsDirty = true;
                }

                break;
            case UnlockStructure:
                _research?.OnCommandApplied(command);
                break;
            case Sell or SellAll or SetAutoSell or SetAutoSellWhenFull:
                _mercato?.OnCommandApplied(command);
                break;
        }
    }

    /// <summary>
    /// Construction events of the host. A finished site rebuilds only the visual layer of its block, with a flash
    /// and dust where it stood; a finished deconstruction crumbles; a dropped plan of the local player shows why.
    /// </summary>
    private void OnSimEvent(SimEvent simEvent)
    {
        _lineInputsVersion++;
        PlaySiteDoneEffects(simEvent);
        switch (simEvent)
        {
            case ConstructFinished finished:
                ConstructFinishedCount++;
                MarkStructuresDirty(_constructionLayer?.LayerOfSite(finished.SiteId)
                    ?? ConstructionLayer.StructureLayers.Both);
                break;
            case DeconstructFinished done when _constructionLayer?.HasSeenSite(done.SiteId) != true:
                // Started and finished between two frames: its block's removal was never drawn.
                MarkStructuresDirty(ConstructionLayer.StructureLayers.Both);
                break;
            case SaleResolved sale when sale.PlayerId == LocalPlayer:
                _mercato?.OnSaleResolved(sale);
                if (JackpotSequenceWanted(sale))
                {
                    // An attempt with the screen: the sequence replaces the toast (started after the frame's ticks).
                    _jackpotPending = sale;
                }
                else if (sale.Jackpot)
                {
                    _hud?.ShowToast(Testi.F("mercato.jackpot", sale.Income));
                }

                break;
            case PlanDropped dropped when dropped.PlayerId == LocalPlayer:
                _hud?.ShowToast(Testi.Rifiuto(dropped.Reason, _view));
                break;
        }
    }

    /// <summary>
    /// G3 effects of a finished site: <c>lampo-costruzione</c> and <c>polvere-costruzione</c> for a construction,
    /// <c>sbriciolamento</c> for a deconstruction, over the site's rect. The events carry no position: the rect comes
    /// from the site as the construction layer last drew it (a site that came and went between two frames has none).
    /// </summary>
    private void PlaySiteDoneEffects(SimEvent simEvent)
    {
        long? siteId = simEvent switch
        {
            ConstructFinished f => f.SiteId,
            DeconstructFinished d => d.SiteId,
            _ => null
        };
        if (siteId is null || _effects is null || _constructionLayer?.SiteBounds(siteId.Value) is not { } rect)
        {
            return;
        }

        var center = rect.GetCenter();
        var half = rect.Size * 0.4f;
        if (simEvent is ConstructFinished)
        {
            _effects.Play("lampo-costruzione", center);
            _effects.Play("polvere-costruzione", center, 0f, half);
        }
        else
        {
            _effects.Play("sbriciolamento", center, 0f, half);
        }
    }

    /// <summary>Sites finished in the running game, from 0 when it was attached (the tutorial playtest waits on it).</summary>
    private int ConstructFinishedCount { get; set; }

    // Blocks and belts standing at the end of the last frame: tells whether a command changed them directly.
    private int _structureCount;

    private int StructureCount() => _view is null
        ? 0
        : _view.Belts.Count + _view.Miners.Count + _view.Smelters.Count + _view.Assemblers.Count
            + _view.Generators.Count + _view.Extractors.Count + _view.PowerNodes.Count + _view.Lamps.Count
            + _view.Batteries.Count + _view.Storages.Count + _view.CoreTiles.Count + (_view.MarketScreen is null ? 0 : 1);

    private void OnCommandRejected(SimCommand command, Refusal reason)
    {
        var fromLine = TakeQuietRefusal(command);
        var quiet = fromLine
            && (reason.Code is RefusalCode.Occupied or RefusalCode.OnCore
                    or RefusalCode.Wall or RefusalCode.DeepWater or RefusalCode.NeedsAdvancedMiner
                || !_lineToasts.Add(reason.Code));
        switch (command)
        {
            case UnlockStructure:
                _research?.OnCommandRejected(command, reason);
                break;
            case Sell or SellAll or SetAutoSell or SetAutoSellWhenFull:
                _mercato?.OnCommandRejected(command, reason);
                break;
            case Remove:
                // Removing on an empty tile has always been silent.
                break;
            default:
                if (!quiet)
                {
                    _hud?.ShowToast(Testi.Rifiuto(reason, _view));
                }

                break;
        }
    }

    // --- The jackpot-attempt sequence (spec 2b) ----------------------------------------------------------------------

    private JackpotSequenceView? _jackpotView;

    /// <summary>An attempt resolved during the frame's ticks, started by <see cref="SyncJackpotSequence"/> after them.</summary>
    private SaleResolved? _jackpotPending;

    // The game and the screen the running sequence belongs to.
    private SimHost? _jackpotHost;
    private GridPosition _jackpotScreen;

    /// <summary>
    /// The sale tick of the screen's last result and the tick its 6 seconds on the glass start from: the end of its
    /// sequence (client side), not the sale.
    /// </summary>
    private (long SaleTick, long From)? _jackpotResultFrom;

    /// <summary>The window has the focus (lost: the sequence pauses, as with Space).</summary>
    private bool _windowFocused = true;

    /// <summary>
    /// Set by a playtest step that plays the sequence; the other playtests keep today's toast (their captures and
    /// checks do not expect the world to zoom).
    /// </summary>
    internal bool PlaytestJackpotSequence { get; set; }

    /// <summary>A jackpot-attempt sequence is running (the agents' bridge waits for its end).</summary>
    internal bool JackpotSequenceRunning => _jackpotView?.Running == true;

    /// <summary>The sequence hides the HUD (<see cref="HudVisible"/>).</summary>
    private bool JackpotHidesHud => _jackpotView?.Sequence is { HidesHud: true };

    /// <summary>The sequence blocks the world's commands: only the skip (click, Esc) and the pause (Space) are left.</summary>
    private bool JackpotBlocksInput => _jackpotView?.Sequence is { BlocksWorldInput: true };

    private void EnsureJackpotSequenceView()
    {
        _jackpotView = JackpotSequenceView.Create(UiRoot, TileSize);
        AddChild(_jackpotView);
    }

    /// <summary>The local player's attempt with a market screen in the game (and not a playtest that keeps the toast).</summary>
    private bool JackpotSequenceWanted(SaleResolved sale) =>
        sale.Attempt && _view?.MarketScreen is not null && _jackpotView is not null
        && (!PlaytestRequested || PlaytestJackpotSequence);

    /// <summary>
    /// Every frame of a game, after the ticks: starts an attempt that just resolved, closes the sequence when it cannot
    /// go on (Home, a panel, loading, another game, the screen gone), otherwise moves it on in real time (the frame's
    /// time without the engine's time scale: the dev speeds do not shorten it), paused with the player's pause or
    /// without the window's focus.
    /// </summary>
    private void SyncJackpotSequence(double delta)
    {
        if (_jackpotView is null)
        {
            return;
        }

        if (_jackpotPending is { } pending)
        {
            _jackpotPending = null;
            StartJackpotSequence(pending);
        }

        if (_jackpotView.Sequence is not { } sequence)
        {
            return;
        }

        if (!GameInPlay || PanelOpen || LoadingShown || _host != _jackpotHost
            || _view?.MarketScreen is not { } screen || !screen.Position.Equals(_jackpotScreen))
        {
            EndJackpotSequence(aborted: true);
            return;
        }

        // Agent games and playtests run in windows without the focus: there only the pause holds it.
        sequence.Paused = PlayerPaused || (!_windowFocused && !PlaytestRequested && !AgentBridgeActive);
        var scale = Engine.TimeScale;
        _jackpotView.Advance(scale > 0 ? delta / scale : delta);
        if (sequence.Finished)
        {
            EndJackpotSequence(aborted: false);
        }
    }

    /// <summary>
    /// Starts the sequence of <paramref name="sale"/>: the Mercato and the filter card close, every held input and drag is
    /// let go, the camera zooms on the screen. A running sequence gives way to the new one. Without a screen, or with
    /// another panel over the game, the toast of today is shown instead.
    /// </summary>
    private void StartJackpotSequence(SaleResolved sale)
    {
        var cam = GetNodeOrNull<WorldCamera>("Camera");
        if (_jackpotView is null || cam is null || _view?.MarketScreen is not { } screen || !GameInPlay || LoadingShown
            || _research?.IsOpen == true || _campaignSelect?.IsOpen == true || _settingsPanel?.IsOpen == true
            || _saves?.IsOpen == true)
        {
            if (sale.Jackpot)
            {
                _hud?.ShowToast(Testi.F("mercato.jackpot", sale.Income));
            }

            return;
        }

        if (_jackpotView.Sequence is not null)
        {
            // A second attempt (from an agent): the running one ends, the camera stays on the screen.
            _jackpotView.Stop();
        }

        _mercato?.Close(); // the scene is in the world: the result stays in the Mercato's status line
        _filterPanel?.Close();
        CancelDrags();
        _hover = null; // no ghost, tooltip or hover hint over the scene
        GetViewport().GuiReleaseFocus(); // Space must reach the pause, not the «Vendi tutto» button
        _hud?.SetSiteTooltip(null, default);
        _hud?.SetPowerTooltip(null, default);
        _hud?.SetPlacementHint(null);

        var centre = FootprintCenter(screen.Position, MarketScreen.Size);
        var (rest, coreStop) = JackpotBotPoints(centre);
        var result = screen.LastResult is { } last && last.ItemId == sale.ItemId ? last : null;
        _jackpotView.Start(sale, result, _view, centre, rest, coreStop);
        _jackpotHost = _host;
        _jackpotScreen = screen.Position;

        // The view frames the screen and the bot's flight to the CORE, closer than the usual play zoom.
        var half = MarketScreen.Size * TileSize / 2f;
        var min = new Vector2(Math.Min(centre.X - half, Math.Min(rest.X, coreStop.X) - TileSize),
            Math.Min(centre.Y - half, Math.Min(rest.Y, coreStop.Y) - TileSize));
        var max = new Vector2(Math.Max(centre.X + half, Math.Max(rest.X, coreStop.X) + TileSize),
            Math.Max(centre.Y + half, Math.Max(rest.Y, coreStop.Y) + TileSize));
        var viewport = cam.GetViewportRect().Size;
        var margin = TileSize * 1.5f;
        var zoom = Math.Min(JackpotMaxZoom,
            Math.Min(viewport.X / (max.X - min.X + 2 * margin), viewport.Y / (max.Y - min.Y + 2 * margin)));
        cam.BeginFocus((min + max) / 2f, zoom);
    }

    /// <summary>Closest zoom the sequence frames the screen with.</summary>
    private const float JackpotMaxZoom = 2f;

    /// <summary>
    /// The bot's two places: where it rests, just outside the screen on the side away from the CORE (so it never hides
    /// the glass and its flight crosses the screen), and where it fills the briefcase, over the edge of the CORE cell
    /// nearest to the screen, on the screen's side.
    /// </summary>
    private (Vector2 RestAt, Vector2 CoreStop) JackpotBotPoints(Vector2 screenCentre)
    {
        var outside = MarketScreen.Size * TileSize / 2f + TileSize / 2f;
        if (_view is null || _view.CoreTiles.Count == 0)
        {
            return (screenCentre + new Vector2(0, -outside), screenCentre + new Vector2(0, outside + TileSize));
        }

        var best = Vector2.Zero;
        var bestDistance = float.MaxValue;
        foreach (var tile in _view.CoreTiles)
        {
            var at = new Vector2((tile.X + 0.5f) * TileSize, (tile.Y + 0.5f) * TileSize);
            var distance = at.DistanceSquaredTo(screenCentre);
            // Ties broken by position, so the same game always picks the same cell.
            if (distance < bestDistance || (distance == bestDistance && (at.Y < best.Y || (at.Y == best.Y && at.X < best.X))))
            {
                best = at;
                bestDistance = distance;
            }
        }

        // Along the axis the screen lies on: half a cell from the CORE cell towards the screen, and just past the
        // screen's far side for the rest.
        var towards = screenCentre - best;
        var step = Math.Abs(towards.X) >= Math.Abs(towards.Y)
            ? new Vector2(Math.Sign(towards.X), 0)
            : new Vector2(0, Math.Sign(towards.Y));
        if (step == Vector2.Zero)
        {
            step = new Vector2(0, -1);
        }

        return (screenCentre + step * outside, best + step * (TileSize * 0.5f));
    }

    /// <summary>
    /// Ends the sequence (finished, skipped, or <paramref name="aborted"/>: Home, load, new game, a panel, the screen
    /// gone): HUD and commands come back, the camera goes back to the drone (at once when aborted), the result's 6 seconds
    /// on the glass start now.
    /// </summary>
    private void EndJackpotSequence(bool aborted)
    {
        if (_jackpotView?.Sequence is null)
        {
            return;
        }

        _jackpotView.Stop();
        GetNodeOrNull<WorldCamera>("Camera")?.EndFocus(instant: aborted);
        if (_host is not null && _host == _jackpotHost && _view?.MarketScreen?.LastResult is { } result)
        {
            _jackpotResultFrom = (result.Tick, _view.CurrentTick);
        }

        _jackpotHost = null;
    }

    /// <summary>The tick the screen's current result is shown from: the end of its sequence, else null (its sale tick).</summary>
    private long? JackpotResultFrom(MarketScreen screen) =>
        screen.LastResult is { } result && _jackpotResultFrom is { } from && from.SaleTick == result.Tick ? from.From : null;
}
