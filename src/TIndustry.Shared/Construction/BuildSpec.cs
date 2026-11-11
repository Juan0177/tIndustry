namespace TIndustry.Shared;

/// <summary>
/// What to build and where: a building or logistics piece (<paramref name="Kind"/> set) or a plain
/// belt (<paramref name="Kind"/> null, <paramref name="StructureId"/> is then the conveyor id).
/// <paramref name="RecipeId"/> applies to forno / assembler (null = default recipe);
/// <paramref name="FilterItemId"/> to the sorters / extractor (null = no filter, on an extractor too).
/// </summary>
public sealed record BuildSpec(
    string StructureId,
    BuildingKind? Kind,
    GridPosition Origin,
    Direction Direction,
    string? RecipeId = null,
    string? FilterItemId = null)
{
    public const string DefaultConveyorId = "conveyor-basic";

    /// <summary>Spec of a <see cref="PlaceBuilding"/> command.</summary>
    public static BuildSpec FromCommand(PlaceBuilding command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var structureId = StructureIdFor(command.Kind)
            ?? throw new ArgumentOutOfRangeException(nameof(command), command.Kind, "Tipo di edificio sconosciuto.");
        return new BuildSpec(
            structureId,
            command.Kind,
            command.Origin,
            command.Direction,
            command.RecipeId,
            command.FilterItemId);
    }

    /// <summary>Spec of a <see cref="PlaceBelt"/> command (blank conveyor id = basic belt).</summary>
    public static BuildSpec FromCommand(PlaceBelt command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var conveyorId = string.IsNullOrWhiteSpace(command.ConveyorId) ? DefaultConveyorId : command.ConveyorId;
        return new BuildSpec(conveyorId, null, command.Position, command.Direction);
    }

    /// <summary>Building kind placed as <paramref name="structureId"/> (null for a plain belt or an unknown id).</summary>
    public static BuildingKind? KindFor(string structureId)
    {
        foreach (var kind in Enum.GetValues<BuildingKind>())
        {
            if (StructureIdFor(kind) == structureId)
            {
                return kind;
            }
        }

        return null;
    }

    /// <summary>Content id of a building kind (null for an unknown kind).</summary>
    public static string? StructureIdFor(BuildingKind kind) => kind switch
    {
        BuildingKind.Junction => "junction",
        BuildingKind.Router => "router",
        BuildingKind.Sorter => "sorter",
        BuildingKind.SorterInverted => "sorter-inverted",
        BuildingKind.Bridge => "conveyor-bridge",
        BuildingKind.Miner => Miner.BasicId,
        BuildingKind.MinerAdvanced => Miner.AdvancedId,
        BuildingKind.Smelter => Crafter.SmelterBuildingId,
        BuildingKind.Assembler => Crafter.AssemblerBuildingId,
        BuildingKind.Generator => Generator.BuildingId,
        BuildingKind.Extractor => Extractor.BuildingId,
        BuildingKind.PowerNode => PowerNode.Tier1Id,
        BuildingKind.PowerNodeT2 => PowerNode.Tier2Id,
        BuildingKind.Lamp => Lamp.BuildingId,
        BuildingKind.Battery => Battery.BuildingId,
        BuildingKind.CoreFoundation => "core-foundation",
        BuildingKind.CoreNucleus => "core-nucleus",
        BuildingKind.Container => "container",
        BuildingKind.Vault => "vault",
        BuildingKind.MarketScreen => MarketScreen.BuildingId,
        BuildingKind.SmelterAdvanced => Crafter.SmelterAdvancedId,
        BuildingKind.GraphitePress => Crafter.GraphitePressId,
        BuildingKind.Separator => Crafter.SeparatorId,
        BuildingKind.Plasticizer => Crafter.PlasticizerId,
        BuildingKind.ImpactDrill => Miner.ImpactDrillId,
        BuildingKind.OverflowGate => Factory.OverflowGateId,
        BuildingKind.UnderflowGate => Factory.UnderflowGateId,
        BuildingKind.BridgeLong => Factory.BridgeLongId,
        _ => null
    };

    /// <summary>
    /// True for the craft machines (<see cref="Crafter"/>): forno, assembler and the machines of the «Ricerca allargata».
    /// </summary>
    public static bool IsCrafter(BuildingKind? kind) => kind is BuildingKind.Smelter or BuildingKind.Assembler
        or BuildingKind.SmelterAdvanced or BuildingKind.GraphitePress or BuildingKind.Separator or BuildingKind.Plasticizer;

    /// <summary>
    /// True for the bridges (<see cref="BuildingKind.Bridge"/>, <see cref="BuildingKind.BridgeLong"/>): two ends, paid twice,
    /// the exit on the first free cell in reach of the definition (<see cref="ConveyorDefinition.MaxSpan"/>).
    /// </summary>
    public static bool IsBridge(BuildingKind? kind) => kind is BuildingKind.Bridge or BuildingKind.BridgeLong;

    /// <summary>True for the miners (<see cref="Miner"/>): Miner T1, Miner T2 and the impact drill.</summary>
    public static bool IsMiner(BuildingKind? kind) =>
        kind is BuildingKind.Miner or BuildingKind.MinerAdvanced or BuildingKind.ImpactDrill;

    /// <summary>Core tier a Core block builds (1 foundation, 2 nucleus); -1 for every other kind.</summary>
    public static int CoreTierOf(BuildingKind? kind) => kind switch
    {
        BuildingKind.CoreFoundation => 1,
        BuildingKind.CoreNucleus => 2,
        _ => -1
    };

    /// <summary>True for the blocks that upgrade the Core (<see cref="BuildingKind.CoreFoundation"/>, <see cref="BuildingKind.CoreNucleus"/>).</summary>
    public static bool IsCoreUpgrade(BuildingKind? kind) => CoreTierOf(kind) > 0;

    /// <summary>True for the storage blocks (<see cref="BuildingKind.Container"/>, <see cref="BuildingKind.Vault"/>).</summary>
    public static bool IsStorage(BuildingKind? kind) => kind is BuildingKind.Container or BuildingKind.Vault;
}

/// <summary>One line of a build cost: <paramref name="ItemId"/> null means money.</summary>
public readonly record struct CostEntry(string? ItemId, int Amount);
