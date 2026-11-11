namespace TIndustry.Shared;

/// <summary>
/// The upgrades of the research (spec «Ricerca allargata», «Potenziamenti»): research nodes of kind <see cref="Kind"/>
/// with nothing to place, read here as integer bonuses (like the Finance modules, read with
/// <see cref="IResearchView.IsUnlocked"/>). Each group has two levels and level 2 <b>replaces</b> level 1 (never added);
/// <see cref="DroneBoostId"/> is level 2 of the four drone groups at once. Without upgrades every bonus is 0, and every
/// reader applies it so that 0 leaves today's numbers exactly as they were: capacities and maxima as
/// <see cref="Scale(int, int)"/> (integers, rounded down), the floats of today times <see cref="Factor"/> (exactly 1 at 0).
/// The node ids and the values of the spec live here and nowhere else.
/// </summary>
public static class Upgrades
{
    /// <summary>Kind of the upgrade nodes in content.json (nothing to place).</summary>
    public const string Kind = "upgrade";

    public const string DroneBuildId = "upgrade-drone-build";
    public const string DroneHoldId = "upgrade-drone-hold";
    public const string DroneRangeId = "upgrade-drone-range";
    public const string DroneMineId = "upgrade-drone-mine";

    /// <summary>Level 2 of the four drone groups together (the reward of the campaign's chapter 4).</summary>
    public const string DroneBoostId = "drone-boost";

    public const string MinersOneId = "upgrade-miners-1";
    public const string MinersTwoId = "upgrade-miners-2";
    public const string MachinesOneId = "upgrade-machines-1";
    public const string MachinesTwoId = "upgrade-machines-2";
    public const string StorageOneId = "upgrade-storage-1";
    public const string StorageTwoId = "upgrade-storage-2";

    /// <summary>Every upgrade node, in the order of the spec (content.json must have each, of kind <see cref="Kind"/>).</summary>
    public static IReadOnlyList<string> NodeIds { get; } =
    [
        DroneBuildId, DroneHoldId, DroneRangeId, DroneMineId, DroneBoostId,
        MinersOneId, MinersTwoId, MachinesOneId, MachinesTwoId, StorageOneId, StorageTwoId
    ];

    /// <summary>Drone build speed, percent: +25 / +50.</summary>
    public static int DroneBuildPercent(IResearchView research) => Level(research, DroneBuildId, 25, DroneBoostId, 50);

    /// <summary>Drone hold, percent of its pieces: +50 / +100.</summary>
    public static int DroneHoldPercent(IResearchView research) => Level(research, DroneHoldId, 50, DroneBoostId, 100);

    /// <summary>Drone build (and mining) range, cells: +5 / +10.</summary>
    public static int DroneRangeCells(IResearchView research) => Level(research, DroneRangeId, 5, DroneBoostId, 10);

    /// <summary>Drone hand-mining rate, percent: +25 / +50.</summary>
    public static int DroneMinePercent(IResearchView research) => Level(research, DroneMineId, 25, DroneBoostId, 50);

    /// <summary>Speed of every miner (T1, T2, impact drill), percent: +10 / +20.</summary>
    public static int MinerPercent(IResearchView research) => Level(research, MinersOneId, 10, MinersTwoId, 20);

    /// <summary>Speed of the forno, the advanced smelter and the assembler (<see cref="GetsMachineBonus"/>), percent: +15 / +30.</summary>
    public static int MachinePercent(IResearchView research) => Level(research, MachinesOneId, 15, MachinesTwoId, 30);

    /// <summary>Maximum per item of the Core (tier plus linked blocks) and of standalone storage blocks, percent: +25 / +50.</summary>
    public static int StoragePercent(IResearchView research) => Level(research, StorageOneId, 25, StorageTwoId, 50);

    /// <summary>True for the machines of the machine group: forno, advanced smelter, assembler.</summary>
    public static bool GetsMachineBonus(string definitionId) =>
        definitionId is Crafter.SmelterBuildingId or Crafter.SmelterAdvancedId or Crafter.AssemblerBuildingId;

    /// <summary><c>value × (100 + percent) / 100</c> in integers, rounded down: <paramref name="value"/> itself at 0.</summary>
    public static int Scale(int value, int percent) => (int)((long)value * (100 + percent) / 100);

    /// <summary><c>value × (100 + percent) / 100</c> in integers, rounded down: <paramref name="value"/> itself at 0.</summary>
    public static long Scale(long value, int percent) => value * (100 + percent) / 100;

    /// <summary><c>(100 + percent) / 100f</c>: the factor on today's floats, exactly 1 at 0.</summary>
    public static float Factor(int percent) => (100 + percent) / 100f;

    /// <summary>
    /// <paramref name="unit"/> with the drone upgrades of <paramref name="research"/>: build speed × <see cref="Factor"/>,
    /// range + cells, hold <see cref="Scale(int, int)"/>, hand-mining rate in whole thousandths
    /// (<see cref="UnitDefinition.MineRatePercent"/>). The same instance when no drone upgrade is unlocked.
    /// </summary>
    public static UnitDefinition Apply(UnitDefinition unit, IResearchView research)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(research);
        return Apply(unit, DroneBuildPercent(research), DroneHoldPercent(research), DroneRangeCells(research),
            DroneMinePercent(research));
    }

    internal static UnitDefinition Apply(UnitDefinition unit, int build, int hold, int range, int mine) =>
        build == 0 && hold == 0 && range == 0 && mine == 0
            ? unit
            : unit with
            {
                BuildSpeed = unit.BuildSpeed * Factor(build),
                BuildRange = unit.BuildRange + range,
                HoldCapacity = Scale(unit.HoldCapacity, hold),
                MineRatePercent = 100 + mine
            };

    // Level 2 replaces level 1: the bonus of the highest level unlocked, 0 without either.
    private static int Level(IResearchView research, string levelOne, int one, string levelTwo, int two) =>
        research.IsUnlocked(levelTwo) ? two : research.IsUnlocked(levelOne) ? one : 0;
}
