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

    /// <summary>Read-only view of the game for graphics and HUD.</summary>
    public IGameView State => factory;

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
    public int Advance(double frameSeconds)
    {
        var ticks = clock.Accumulate(frameSeconds);
        for (var i = 0; i < ticks; i++)
        {
            RunTick();
        }

        return ticks;
    }

    private void RunTick()
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
            var reason = Apply(command);
            if (reason is null)
            {
                CommandApplied?.Invoke(command);
            }
            else
            {
                CommandRejected?.Invoke(command, reason);
            }
        }

        factory.Step();
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
                factory.TryPlaceBelt(command.Position, command.Direction, conveyorId),
                command.Position);
    }

    private string? ApplyPlaceBuilding(PlaceBuilding command)
    {
        var structureId = StructureId(command.Kind);
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

        var origin = command.Origin;
        var dir = command.Direction;
        var placed = command.Kind switch
        {
            BuildingKind.Junction => factory.TryPlaceJunction(origin, dir),
            BuildingKind.Splitter => factory.TryPlaceSplitter(origin, dir),
            BuildingKind.Sorter => factory.TryPlaceSorter(origin, dir, command.FilterItemId),
            BuildingKind.Bridge => factory.TryPlaceBridge(origin, dir),
            BuildingKind.Miner => factory.TryPlaceMiner(origin, dir),
            BuildingKind.MinerAdvanced => factory.TryPlaceMiner(origin, dir, definitionId: Miner.AdvancedId),
            BuildingKind.Smelter => factory.TryPlaceSmelter(origin, dir, recipe),
            BuildingKind.Assembler => factory.TryPlaceAssembler(origin, dir, recipe),
            BuildingKind.Generator => factory.TryPlaceGenerator(origin, dir),
            BuildingKind.Extractor => factory.TryPlaceExtractor(
                origin, dir, command.FilterItemId ?? Extractor.DefaultFilterItemId),
            BuildingKind.PowerNode => factory.TryPlacePowerNode(origin),
            BuildingKind.PowerNodeT2 => factory.TryPlacePowerNode(origin, PowerNode.Tier2Id),
            _ => false
        };
        return PlacedOrOccupied(placed, origin);
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

    private static string? StructureId(BuildingKind kind) => kind switch
    {
        BuildingKind.Junction => "junction",
        BuildingKind.Splitter => "splitter",
        BuildingKind.Sorter => "sorter",
        BuildingKind.Bridge => "conveyor-bridge",
        BuildingKind.Miner => Miner.BasicId,
        BuildingKind.MinerAdvanced => Miner.AdvancedId,
        BuildingKind.Smelter => Crafter.SmelterBuildingId,
        BuildingKind.Assembler => Crafter.AssemblerBuildingId,
        BuildingKind.Generator => Generator.BuildingId,
        BuildingKind.Extractor => Extractor.BuildingId,
        BuildingKind.PowerNode => PowerNode.Tier1Id,
        BuildingKind.PowerNodeT2 => PowerNode.Tier2Id,
        _ => null
    };
}
