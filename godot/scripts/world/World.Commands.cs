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

    // Commands submitted while drag-placing whose "occupied / on the Core" refusal must stay silent
    // (the cursor sweeps over cells that are expected to be blocked). Counted per equal command.
    private readonly Dictionary<SimCommand, int> _quietRefusals = [];

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
        _quietRefusals.Clear();
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
            case Sell or SetAutoSell:
                _mercato?.OnCommandApplied(command, _host?.LastCommandMoneyDelta ?? 0);
                break;
            case UpgradeCore when _view is not null:
                _hud?.ShowToast($"Core potenziato: +{_view.CoreSaleBonusPercent}% vendite!");
                _hud?.SyncCoreUpgrade(_view);
                UpdateHud();
                break;
        }
    }

    /// <summary>
    /// Construction events of the host. A finished site rebuilds only the visual layer of its block;
    /// a dropped plan of the local player shows why.
    /// </summary>
    private void OnSimEvent(SimEvent simEvent)
    {
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
            case PlanDropped dropped when dropped.PlayerId == LocalPlayer:
                _hud?.ShowToast(dropped.Reason);
                break;
        }
    }

    /// <summary>Sites finished in the running game, from 0 when it was attached (the tutorial playtest waits on it).</summary>
    private int ConstructFinishedCount { get; set; }

    // Blocks and belts standing at the end of the last frame: tells whether a command changed them directly.
    private int _structureCount;

    private int StructureCount() => _view is null
        ? 0
        : _view.Belts.Count + _view.Miners.Count + _view.Smelters.Count + _view.Assemblers.Count
            + _view.Generators.Count + _view.Extractors.Count + _view.PowerNodes.Count;

    private void OnCommandRejected(SimCommand command, string reason)
    {
        var quiet = TakeQuietRefusal(command)
            && reason is CommandReasons.Occupied or CommandReasons.OnCore
                or CommandReasons.Wall or CommandReasons.DeepWater;
        switch (command)
        {
            case UnlockStructure:
                _research?.OnCommandRejected(command, reason);
                break;
            case Sell or SetAutoSell:
                _mercato?.OnCommandRejected(command, reason);
                break;
            case Remove:
                // Removing on an empty tile has always been silent.
                break;
            default:
                if (!quiet)
                {
                    _hud?.ShowToast(reason);
                }

                break;
        }
    }
}
