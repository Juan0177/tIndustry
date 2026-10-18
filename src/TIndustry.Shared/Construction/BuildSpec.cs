namespace TIndustry.Shared;

/// <summary>
/// What to build and where: a building or logistics piece (<paramref name="Kind"/> set) or a plain
/// belt (<paramref name="Kind"/> null, <paramref name="StructureId"/> is then the conveyor id).
/// <paramref name="RecipeId"/> applies to forno / assembler (null = default recipe);
/// <paramref name="FilterItemId"/> to sorter / extractor (null = default filter).
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

    /// <summary>Content id of a building kind (null for an unknown kind).</summary>
    public static string? StructureIdFor(BuildingKind kind) => kind switch
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
        BuildingKind.Lamp => Lamp.BuildingId,
        _ => null
    };
}

/// <summary>One line of a build cost: <paramref name="ItemId"/> null means money.</summary>
public readonly record struct CostEntry(string? ItemId, int Amount);
