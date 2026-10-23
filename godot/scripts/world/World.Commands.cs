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

        _host = host;
        _host.CommandApplied += OnCommandApplied;
        _host.CommandRejected += OnCommandRejected;
        _host.EventRaised += OnSimEvent;
        ResetTimeControls(host);
        _quietRefusals.Clear();
        _linePlan = null;
        CancelLine();
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
            case SetSorterFilter:
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
                if (sale.Jackpot)
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
            + _view.Storages.Count + _view.CoreTiles.Count;

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
}
