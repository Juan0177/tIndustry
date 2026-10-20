using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The armored belt (Mindustry's armored conveyor): takes items from plain belts on any side but from buildings and
/// other logistics only from behind, and is drawn straight beside a building (no blend), so a line runs past a row
/// of machines without picking anything up.
/// </summary>
public sealed class ArmoredBeltTests
{
    private const string Armored = "conveyor-armored";
    private const string Fast = "conveyor-fast";
    private const int Size = 40;

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    // An armored line heading North on x = 20, from (20,22) to (20,18); (20,20) is the cell under test.
    private static readonly GridPosition P = new(20, 20);

    /// <summary>40×40, Core at (30,30), everything unlocked and paid for; iron everywhere unless <paramref name="generated"/>.</summary>
    private static Factory Sandbox(bool generated = false)
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(30, 30), mapWidth: Size, mapHeight: Size,
            startingMoney: 100_000, rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        factory.Research.ForceUnlock(Armored);
        foreach (var item in new[] { "iron-plate", "copper-wire", "lead-plate", "titanium-plate", "graphite" })
        {
            factory.Wallet.AddMaterial(item, 1_000);
        }

        if (generated)
        {
            return factory; // a save regenerates the terrain and checks it
        }

        var tiles = new TerrainTile[Size, Size];
        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                tiles[x, y] = new TerrainTile(FloorKind.Stone, WallKind.None, DepositKind.Iron);
            }
        }

        factory.Terrain = TerrainMap.FromTiles(tiles);
        return factory;
    }

    private static Factory WithLine(string conveyorId = Armored, bool generated = false)
    {
        var factory = Sandbox(generated);
        for (var y = 22; y >= 18; y--)
        {
            Assert.True(factory.TryPlaceBelt(new GridPosition(20, y), Direction.North, conveyorId));
        }

        return factory;
    }

    private static int ItemsOnLine(Factory factory) =>
        Enumerable.Range(18, 5).Sum(y => factory.Belts.Cells[new GridPosition(20, y)].Items.Count);

    private static void Run(Factory factory, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            factory.Step();
        }
    }

    [Fact]
    public void ContentMatchesTheFastBelt()
    {
        var armored = Content.RequireConveyor(Armored);
        var fast = Content.RequireConveyor(Fast);
        Assert.True(armored.Armored);
        Assert.False(fast.Armored);
        Assert.Equal(fast.Tier, armored.Tier);
        Assert.Equal(fast.RateItemsPerSecond, armored.RateItemsPerSecond);
        Assert.Equal(fast.Capacity, armored.Capacity);
        Assert.Equal(fast.ItemSpacing, armored.ItemSpacing);
        Assert.Equal(Armored, Factory.StructureIdForTool("belt-armored"));
        // The agents' piazza {tier: 2} takes the first tier-2 belt: still the fast one.
        Assert.Equal(Fast, Content.Conveyors.First(c => c.Tier == 2 && BuildSpec.KindFor(c.Id) is null).Id);
    }

    [Fact]
    public void ABuildingOnTheSideIsRefused()
    {
        var factory = WithLine();
        Assert.True(factory.TryPlaceMiner(new GridPosition(18, 20), Direction.East)); // x 18-19: west side of the line
        Run(factory, 600);
        Assert.Equal(0, ItemsOnLine(factory));
        Assert.Equal(0, factory.Miners[0].ItemsProduced);

        // The same miner beside a fast line feeds it.
        var fast = WithLine(Fast);
        Assert.True(fast.TryPlaceMiner(new GridPosition(18, 20), Direction.East));
        Run(fast, 600);
        Assert.True(fast.Miners[0].ItemsProduced > 0);
    }

    [Fact]
    public void ABuildingBehindIsAccepted()
    {
        var factory = Sandbox();
        Assert.True(factory.TryPlaceBelt(new GridPosition(20, 19), Direction.North, Armored));
        Assert.True(factory.TryPlaceBelt(new GridPosition(20, 18), Direction.North, Armored));
        Assert.True(factory.TryPlaceMiner(new GridPosition(20, 20), Direction.East)); // y 20-21: behind (20,19)
        Run(factory, 600);
        Assert.True(factory.Miners[0].ItemsProduced > 0);
    }

    [Theory]
    [InlineData(Armored)]
    [InlineData(Fast)]
    [InlineData("conveyor-basic")]
    public void ABeltOnTheSideIsAccepted(string sideBelt)
    {
        var factory = WithLine();
        var side = new GridPosition(19, 20);
        Assert.True(factory.TryPlaceBelt(side, Direction.East, sideBelt));
        Assert.Equal(BeltShape.JoinLeft, factory.GetBeltShape(P));

        Assert.True(factory.Belts.TryInsert(side, new TransportedItem(9_000, "iron-ore"), Direction.East));
        Run(factory, 300);
        Assert.True(ItemsOnLine(factory) > 0);
        Assert.Empty(factory.Belts.Cells[side].Items);

        var right = WithLine();
        Assert.True(right.TryPlaceBelt(new GridPosition(21, 20), Direction.West, sideBelt));
        Assert.Equal(BeltShape.JoinRight, right.GetBeltShape(P));
    }

    [Fact]
    public void ShapeStaysStraightBesideADrill()
    {
        var factory = WithLine();
        Assert.True(factory.TryPlaceMiner(new GridPosition(18, 20), Direction.East)); // west of (20,20) and (20,21)
        Assert.True(factory.TryPlaceGenerator(new GridPosition(21, 18), Direction.North)); // east of (20,18) and (20,19)
        for (var y = 18; y <= 22; y++)
        {
            Assert.Equal(BeltShape.Straight, factory.GetBeltShape(new GridPosition(20, y)));
        }

        // A fast line in the same place blends with both.
        var fast = WithLine(Fast);
        Assert.True(fast.TryPlaceMiner(new GridPosition(18, 20), Direction.East));
        Assert.True(fast.TryPlaceGenerator(new GridPosition(21, 18), Direction.North));
        Assert.Equal(BeltShape.JoinLeft, fast.GetBeltShape(P));
        Assert.Equal(BeltShape.JoinRight, fast.GetBeltShape(new GridPosition(20, 18)));
    }

    [Fact]
    public void ShapeStaysStraightBesideTheCore()
    {
        // Core covers (30..31, 30..31): an armored line along its east edge heading North. Placing turns belts that
        // touch the Core into it, so the test turns them back on the grid.
        var factory = Sandbox();
        var beside = new GridPosition(32, 31);
        var ahead = new GridPosition(32, 30);
        Assert.True(factory.TryPlaceBelt(beside, Direction.North, Armored));
        Assert.True(factory.TryPlaceBelt(ahead, Direction.North, Armored));
        Assert.True(factory.Belts.TryOrient(beside, Direction.North));
        Assert.True(factory.Belts.TryOrient(ahead, Direction.North));
        Assert.Equal(BeltShape.Straight, factory.GetBeltShape(beside));
        Assert.Equal(BeltShape.Straight, factory.GetBeltShape(ahead));
        Assert.True(factory.TryGetBeltIncomingDirection(ahead, out var incoming)); // fed from behind only
        Assert.Equal(Direction.North, incoming);
    }

    /// <summary>
    /// Places a logistics source of <paramref name="kind"/> on <paramref name="at"/> that sends items toward
    /// <paramref name="flow"/>; returns where to drop an item so that it goes there.
    /// </summary>
    private static GridPosition PlaceSource(Factory factory, string kind, GridPosition at, Direction flow)
    {
        switch (kind)
        {
            case "splitter":
                Assert.True(factory.TryPlaceSplitter(at, DirectionMath.Right(flow))); // its left exit is flow
                return at;
            case "sorter":
                Assert.True(factory.TryPlaceSorter(at, flow, "iron-ore")); // the item matches: straight on
                return at;
            case "bridge":
                var entry = at.Step(DirectionMath.Opposite(flow), 2);
                Assert.True(factory.TryPlaceBridge(entry, flow)); // its exit lands on at
                Assert.Equal(at, factory.Belts.Cells[entry].BridgePartner);
                return entry;
            default:
                Assert.True(factory.TryPlaceJunction(at, flow));
                var feeder = at.Step(DirectionMath.Opposite(flow));
                Assert.True(factory.TryPlaceBelt(feeder, flow));
                return feeder;
        }
    }

    [Theory]
    [InlineData("splitter")]
    [InlineData("sorter")]
    [InlineData("bridge")]
    [InlineData("junction")]
    public void ArmoredRefusesNonBeltLogisticsFromTheSide(string kind)
    {
        var side = WithLine();
        var drop = PlaceSource(side, kind, new GridPosition(19, 20), Direction.East);
        Assert.True(side.Belts.TryInsert(drop, new TransportedItem(9_000, "iron-ore")));
        Run(side, 300);
        Assert.Equal(0, ItemsOnLine(side));

        var behind = WithLine();
        drop = PlaceSource(behind, kind, new GridPosition(20, 23), Direction.North);
        Assert.True(behind.Belts.TryInsert(drop, new TransportedItem(9_000, "iron-ore")));
        Run(behind, 300);
        Assert.Equal(1, ItemsOnLine(behind));
    }

    [Fact]
    public void AnInsertOfUnknownOriginIsRefused()
    {
        var factory = WithLine();
        Assert.False(factory.Belts.TryInsert(P, new TransportedItem(9_000, "iron-ore")));
        Assert.True(factory.Belts.TryInsert(P, new TransportedItem(9_001, "iron-ore"), source: new GridPosition(20, 21)));
    }

    [Fact]
    public void ArmoredSavesAndLoads()
    {
        var factory = WithLine(generated: true);
        Assert.True(factory.Belts.TryInsert(P, new TransportedItem(9_000, "iron-ore"), Direction.North));
        Run(factory, 10);

        var restored = Factory.Restore(Content, factory.Capture());
        Assert.Equal(new SimHost(factory).Checksum(), new SimHost(restored).Checksum());
        Assert.Equal(Armored, restored.Belts.Cells[P].Definition.Id);
        Assert.True(restored.Belts.Cells[P].Definition.Armored);
    }

    [Fact]
    public void FastToArmoredIsATierChange()
    {
        var factory = WithLine(Fast);
        Assert.Equal(BeltOverBelt.Replace, factory.BeltOverBeltAt(P, Direction.North, Armored));

        var armored = WithLine();
        Assert.Equal(BeltOverBelt.Replace, armored.BeltOverBeltAt(P, Direction.North, Fast));
    }
}
