namespace TIndustry.Shared;

/// <summary>
/// Local server: queues player commands, runs fixed ticks and reports each command's outcome.
/// At the start of every tick it applies that tick's commands in arrival order, validating each
/// against the state at that moment (so in co-op a command can fail because another player
/// spent the resources first), then advances the simulation one <see cref="Factory.Step"/>.
/// </summary>
public sealed class SimHost
{
    private readonly Factory factory;
    private readonly SimulationClock clock = new();
    private readonly List<SimCommand> pending = [];

    public SimHost(Factory factory)
    {
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>Canonical snapshot bytes of the current state.</summary>
    public byte[] CreateSnapshot() => Snapshot.Write(factory.State);

    /// <summary>FNV-1a 64 of the canonical snapshot.</summary>
    public ulong Checksum() => Snapshot.Checksum(CreateSnapshot());

    /// <summary>Host resuming from a snapshot (empty command queue, fresh clock).</summary>
    public static SimHost FromSnapshot(FactoryContent content, byte[] snapshot) =>
        new(new Factory(Snapshot.Read(content, snapshot)));

    /// <summary>Read-only view of the game for graphics and HUD.</summary>
    public IGameView State => factory;

    /// <summary>
    /// Commands submitted but not yet applied, in submit order (read-only). The client uses it to
    /// derive the "effective" value of a toggle/cycle when the same input arrives twice in one tick.
    /// </summary>
    public IReadOnlyList<SimCommand> Pending => pending.AsReadOnly();

    /// <summary>
    /// Change in money caused by the command currently being reported by <see cref="CommandApplied"/>
    /// (its own effect only, e.g. one sale's proceeds). Meaningful inside that event only.
    /// </summary>
    public int LastCommandMoneyDelta { get; private set; }

    /// <summary>Raised when a command has been applied (before that tick's step).</summary>
    public event Action<SimCommand>? CommandApplied;

    /// <summary>Raised with the Italian reason when a command is refused; the state is unchanged.</summary>
    public event Action<SimCommand, string>? CommandRejected;

    /// <summary>
    /// Raised after each tick for every <see cref="SimEvent"/> the systems queued during it, in order
    /// (plans dropped, sites finished). Same handler-exception policy as <see cref="CommandApplied"/>.
    /// </summary>
    public event Action<SimEvent>? EventRaised;

    /// <summary>Queues <paramref name="command"/>; with <c>Tick == 0</c> it is applied on the next tick.</summary>
    public void Submit(SimCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Tick == 0)
        {
            command = command with { Tick = factory.CurrentTick + 1 };
        }

        pending.Add(command);
    }

    /// <summary>
    /// Accumulates frame time and runs the ticks due (at most <see cref="SimulationClock.MaxTicksPerFrame"/>).
    /// Returns the number of ticks run.
    /// </summary>
    /// <remarks>
    /// Handler-exception policy: an exception thrown by a <see cref="CommandApplied"/> /
    /// <see cref="CommandRejected"/> / <see cref="EventRaised"/> subscriber never interrupts the simulation. Every subscriber is
    /// called, every due command is applied and every tick of the frame runs; the exceptions are
    /// collected and rethrown together as one <see cref="AggregateException"/> when
    /// <see cref="Advance"/> has finished. The host stays consistent and usable afterwards.
    /// </remarks>
    public int Advance(double frameSeconds)
    {
        var ticks = clock.Accumulate(frameSeconds);
        List<Exception>? handlerErrors = null;
        for (var i = 0; i < ticks; i++)
        {
            RunTick(ref handlerErrors);
        }

        if (handlerErrors is not null)
        {
            throw new AggregateException("Un gestore di eventi di SimHost ha lanciato un'eccezione.", handlerErrors);
        }

        return ticks;
    }

    /// <summary>
    /// Runs exactly <paramref name="ticks"/> ticks now, ignoring frame time and the per-frame cap
    /// (scripted setup, tools). Same handler-exception policy as <see cref="Advance"/>.
    /// </summary>
    public int AdvanceTicks(int ticks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ticks);
        List<Exception>? handlerErrors = null;
        for (var i = 0; i < ticks; i++)
        {
            RunTick(ref handlerErrors);
        }

        if (handlerErrors is not null)
        {
            throw new AggregateException("Un gestore di eventi di SimHost ha lanciato un'eccezione.", handlerErrors);
        }

        return ticks;
    }

    private void RunTick(ref List<Exception>? handlerErrors)
    {
        var tick = factory.CurrentTick + 1;
        // Due commands in arrival order (late ones included); later ones stay queued.
        var due = new List<SimCommand>();
        pending.RemoveAll(cmd =>
        {
            if (cmd.Tick > tick)
            {
                return false;
            }

            due.Add(cmd);
            return true;
        });

        foreach (var command in due)
        {
            var moneyBefore = factory.Wallet.Money;
            var reason = Apply(command);
            if (reason is null)
            {
                LastCommandMoneyDelta = factory.Wallet.Money - moneyBefore;
                Raise(CommandApplied, h => h(command), ref handlerErrors);
            }
            else
            {
                Raise(CommandRejected, h => h(command, reason), ref handlerErrors);
            }
        }

        factory.Step();

        var events = factory.State.Events;
        if (events.Count == 0)
        {
            return;
        }

        // Drain first: handlers see a stable list and the next tick starts clean.
        var raised = events.ToArray();
        events.Clear();
        foreach (var simEvent in raised)
        {
            Raise(EventRaised, h => h(simEvent), ref handlerErrors);
        }
    }

    /// <summary>Calls each subscriber separately so one failing handler cannot skip the others.</summary>
    private static void Raise<THandler>(
        THandler? handlers, Action<THandler> invoke, ref List<Exception>? errors)
        where THandler : Delegate
    {
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                invoke((THandler)handler);
            }
            catch (Exception ex)
            {
                (errors ??= []).Add(ex);
            }
        }
    }

    /// <summary>Validates and applies one command; returns null when applied, else the reason.</summary>
    private string? Apply(SimCommand command) => command switch
    {
        PlaceBelt belt => ApplyPlaceBelt(belt),
        PlaceBuilding building => ApplyPlaceBuilding(building),
        Remove remove => ApplyRemove(remove),
        UnlockStructure unlock => ApplyUnlock(unlock),
        Sell sell => ApplySell(sell),
        SetAutoSell autoSell => SetAutoSellAndAccept(autoSell.Enabled),
        UpgradeCore => ApplyUpgradeCore(),
        SetSorterFilter filter => ApplySetSorterFilter(filter),
        _ => CommandReasons.InvalidCommand
    };

    private string? ApplyPlaceBelt(PlaceBelt command)
    {
        var spec = BuildSpec.FromCommand(command);
        if (!factory.IsValidSpec(spec))
        {
            return CommandReasons.InvalidCommand;
        }

        var toolName = CommandReasons.BeltToolName(spec.StructureId);
        if (factory.State.Rules.InstantBuild)
        {
            return CheckBuildable(spec.StructureId, toolName, multiplicity: 1)
                ?? PlacedOrRefused(factory.PlaceInstant(spec, charge: true), spec);
        }

        // A belt touching the Core turns to face it when built: the plan says so from the start, so the
        // plan, its site and the finished belt all point the same way.
        if (CoreStockSink.PreferDirectionIntoCore(spec.Origin, factory.CoreTiles) is { } intoCore)
        {
            spec = spec with { Direction = intoCore };
        }

        return QueuePlan(command.PlayerId, spec, toolName);
    }

    private string? ApplyPlaceBuilding(PlaceBuilding command)
    {
        if (BuildSpec.StructureIdFor(command.Kind) is null)
        {
            return CommandReasons.InvalidCommand;
        }

        // Unknown recipe for forno / assembler: invalid before the research and resource checks.
        var spec = BuildSpec.FromCommand(command);
        if (!factory.IsValidSpec(spec))
        {
            return CommandReasons.InvalidCommand;
        }

        var toolName = CommandReasons.ToolName(command.Kind);
        if (!factory.State.Rules.InstantBuild)
        {
            return QueuePlan(command.PlayerId, spec, toolName);
        }

        var multiplicity = command.Kind == BuildingKind.Bridge ? 2 : 1;
        return CheckBuildable(spec.StructureId, toolName, multiplicity)
            ?? PlacedOrRefused(factory.PlaceInstant(spec, charge: true), spec);
    }

    /// <summary>
    /// Queues a build plan on the player's drone: the structure must be unlocked and its cells free of
    /// blocks, belts, sites and other players' plans. Resources are not checked (the site waits for them).
    /// </summary>
    private string? QueuePlan(int playerId, BuildSpec spec, string toolName)
    {
        if (!factory.IsStructureUnlocked(spec.StructureId))
        {
            return CommandReasons.Locked(toolName);
        }

        if (!factory.CanPlace(spec, out var reason))
        {
            return reason;
        }

        foreach (var tile in factory.Footprint(spec))
        {
            if (factory.IsPlannedTile(tile))
            {
                return CommandReasons.Occupied;
            }
        }

        // A game without the player's drone cannot take plans (new games always have player 0's).
        var builder = factory.BuilderOf(playerId);
        if (builder is null)
        {
            return CommandReasons.InvalidCommand;
        }

        builder.PlanQueue.Add(new BuildPlan(spec, Breaking: false));
        return null;
    }

    /// <summary>
    /// Instant build: removes the building, else the belt piece, with today's refund. Otherwise, in order:
    /// cancels the player's not yet started plan covering the tile (a breaking plan from any cell of its
    /// block, but not with <see cref="Remove.OnlyMark"/>); cancels a site under construction (100% of what it
    /// spent comes back); leaves a site being taken apart alone (silent refusal); queues a breaking plan for a
    /// block or belt piece (once); refuses silently on an empty tile.
    /// </summary>
    private string? ApplyRemove(Remove command)
    {
        var tile = command.Tile;
        if (factory.State.Rules.InstantBuild)
        {
            return factory.TryRemoveBuildingAt(tile) || factory.TryRemoveBelt(tile)
                ? null
                : CommandReasons.NothingToRemove;
        }

        var builder = factory.BuilderOf(command.PlayerId);
        if (builder is not null && TryCancelQueuedPlan(builder, tile, cancelBreaking: !command.OnlyMark))
        {
            return null;
        }

        if (factory.TryGetSiteAt(tile, out var site))
        {
            if (site.Deconstructing)
            {
                // Already being taken apart: nothing changes.
                return CommandReasons.NothingToRemove;
            }

            Construction.Cancel(site);
            return null;
        }

        if (!factory.TryGetSpecAt(tile, out var spec, out _))
        {
            return CommandReasons.NothingToRemove;
        }

        if (builder is null)
        {
            return CommandReasons.InvalidCommand;
        }

        if (!IsBreakingPlanned(spec))
        {
            builder.PlanQueue.Add(new BuildPlan(spec with { Origin = tile }, Breaking: true));
        }

        return null;
    }

    /// <summary>
    /// Removes the first not yet started plan of <paramref name="builder"/> covering <paramref name="tile"/>
    /// (a breaking plan covers its whole block). A plan whose site already stands (the current one, or one
    /// sent to the back of the queue while stalled) is left to the site: cancelling the site takes it apart.
    /// </summary>
    private bool TryCancelQueuedPlan(BuilderUnit builder, GridPosition tile, bool cancelBreaking)
    {
        // The first plan has started once the drone has a site for it.
        var first = builder.CurrentSiteId is null ? 0 : 1;
        for (var i = first; i < builder.Plans.Count; i++)
        {
            var plan = builder.Plans[i];
            if (plan.Breaking ? !cancelBreaking : factory.SiteOfPlan(plan) is not null)
            {
                continue;
            }

            if (factory.PlanTiles(plan).Contains(tile))
            {
                builder.PlanQueue.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>True when a breaking plan of any player already targets the block <paramref name="spec"/> describes.</summary>
    private bool IsBreakingPlanned(BuildSpec spec)
    {
        foreach (var builder in factory.State.Builders)
        {
            foreach (var plan in builder.Plans)
            {
                if (plan.Breaking
                    && factory.TryGetSpecAt(plan.Spec.Origin, out var target, out _)
                    && target == spec)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Same checks, same order and same texts as the client's placement today.</summary>
    private string? CheckBuildable(string structureId, string toolName, int multiplicity)
    {
        if (!factory.IsStructureUnlocked(structureId))
        {
            return CommandReasons.Locked(toolName);
        }

        if (!factory.CanAffordStructure(structureId, multiplicity))
        {
            var need = factory.FormatNeedMessage(structureId, multiplicity);
            return string.IsNullOrEmpty(need) ? CommandReasons.InsufficientResources : need;
        }

        return null;
    }

    private string? PlacedOrRefused(bool placed, BuildSpec spec) =>
        placed ? null : factory.RefusalReason(factory.Footprint(spec));

    private string? ApplyUnlock(UnlockStructure command)
    {
        var structure = factory.Content.FindStructure(command.StructureId);
        if (structure is null)
        {
            return CommandReasons.InvalidCommand;
        }

        if (factory.Research.IsUnlocked(structure.Id))
        {
            return CommandReasons.AlreadyUnlocked;
        }

        if (!factory.Research.MeetsPrerequisites(structure))
        {
            return CommandReasons.MissingPrerequisites;
        }

        return factory.TryUnlockStructure(structure.Id) ? null : CommandReasons.UnlockUnaffordable;
    }

    private string? ApplySell(Sell command)
    {
        if (command.Amount <= 0 || string.IsNullOrWhiteSpace(command.ItemId))
        {
            return CommandReasons.InvalidCommand;
        }

        return factory.TrySellFromWallet(command.ItemId, command.Amount) ? null : CommandReasons.SellFailed;
    }

    private string? SetAutoSellAndAccept(bool enabled)
    {
        factory.AutoSellAtCore = enabled;
        return null;
    }

    private string? ApplySetSorterFilter(SetSorterFilter command)
    {
        if (!SorterFilters.IsValid(command.ItemId)
            || !factory.Belts.TryGet(command.Tile, out var cell)
            || cell.Kind != LogisticsKind.Sorter)
        {
            return CommandReasons.InvalidCommand;
        }

        cell.SetFilterItem(command.ItemId);
        return null;
    }

    private string? ApplyUpgradeCore()
    {
        // Already upgraded or short of resources: today's World.Session texts.
        var need = factory.FormatCoreUpgradeNeedMessage();
        if (!string.IsNullOrEmpty(need))
        {
            return need;
        }

        return factory.TryUpgradeCore() ? null : CommandReasons.InsufficientResources;
    }
}
