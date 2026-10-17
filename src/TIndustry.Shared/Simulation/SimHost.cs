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
    /// <see cref="CommandRejected"/> subscriber never interrupts the simulation. Every subscriber is
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
        Remove remove => factory.TryRemoveBuildingAt(remove.Tile) || factory.TryRemoveBelt(remove.Tile)
            ? null
            : CommandReasons.NothingToRemove,
        UnlockStructure unlock => ApplyUnlock(unlock),
        Sell sell => ApplySell(sell),
        SetAutoSell autoSell => SetAutoSellAndAccept(autoSell.Enabled),
        UpgradeCore => ApplyUpgradeCore(),
        SetSorterFilter filter => ApplySetSorterFilter(filter),
        _ => CommandReasons.InvalidCommand
    };

    private string? ApplyPlaceBelt(PlaceBelt command)
    {
        var conveyorId = string.IsNullOrWhiteSpace(command.ConveyorId) ? "conveyor-basic" : command.ConveyorId;
        if (factory.Content.FindConveyor(conveyorId) is null)
        {
            return CommandReasons.InvalidCommand;
        }

        return CheckBuildable(conveyorId, CommandReasons.BeltToolName(conveyorId), multiplicity: 1)
            ?? PlacedOrOccupied(
                factory.PlaceInstant(BuildSpec.FromCommand(command), charge: true),
                command.Position);
    }

    private string? ApplyPlaceBuilding(PlaceBuilding command)
    {
        var structureId = BuildSpec.StructureIdFor(command.Kind);
        if (structureId is null)
        {
            return CommandReasons.InvalidCommand;
        }

        RecipeDefinition? recipe = null;
        if (command.RecipeId is { } recipeId
            && (command.Kind is BuildingKind.Smelter or BuildingKind.Assembler))
        {
            var available = command.Kind == BuildingKind.Smelter
                ? factory.Content.SmelterRecipes()
                : factory.Content.AssemblerRecipes();
            recipe = available.FirstOrDefault(r => string.Equals(r.Id, recipeId, StringComparison.Ordinal));
            if (recipe is null)
            {
                return CommandReasons.InvalidCommand;
            }
        }

        var multiplicity = command.Kind == BuildingKind.Bridge ? 2 : 1;
        var blocked = CheckBuildable(structureId, CommandReasons.ToolName(command.Kind), multiplicity);
        if (blocked is not null)
        {
            return blocked;
        }

        var placed = factory.PlaceInstant(BuildSpec.FromCommand(command), charge: true);
        return PlacedOrOccupied(placed, command.Origin);
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

    private string? PlacedOrOccupied(bool placed, GridPosition target) =>
        placed
            ? null
            : factory.CoreTiles.Contains(target) ? CommandReasons.OnCore : CommandReasons.Occupied;

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
