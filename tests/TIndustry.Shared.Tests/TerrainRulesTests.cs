using System.Text.Json.Nodes;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Walls and deep water block every placement; shallow water does not; bridges span anything.</summary>
public sealed class TerrainRulesTests
{
    private const int Size = 40;

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly TerrainTile Grass = new(FloorKind.Grass, WallKind.None, DepositKind.None);

    private static readonly TerrainTile Deep = new(FloorKind.DeepWater, WallKind.None, DepositKind.None);

    private static readonly TerrainTile Shallow = new(FloorKind.ShallowWater, WallKind.None, DepositKind.None);

    private static TerrainTile Wall(WallKind kind = WallKind.StoneWall) => new(FloorKind.Stone, kind, DepositKind.None);

    /// <summary>40×40 grass with the Core at (30,30), everything unlocked and the given cells changed.</summary>
    private static Factory Sandbox(params (int X, int Y, TerrainTile Tile)[] cells)
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(30, 30), mapWidth: Size, mapHeight: Size,
            rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        return WithTerrain(factory, cells);
    }

    private static Factory WithTerrain(Factory factory, (int X, int Y, TerrainTile Tile)[] cells)
    {
        var tiles = new TerrainTile[Size, Size];
        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                tiles[x, y] = Grass;
            }
        }

        foreach (var (x, y, tile) in cells)
        {
            tiles[x, y] = tile;
        }

        factory.Terrain = TerrainMap.FromTiles(tiles);
        return factory;
    }

    /// <summary>One spec per placeable kind (belt included) with its origin at <paramref name="origin"/>.</summary>
    private static IEnumerable<BuildSpec> EverySpec(GridPosition origin)
    {
        yield return new BuildSpec(BuildSpec.DefaultConveyorId, null, origin, Direction.East);
        // The Core blocks stand only over the Core (CoreUpgradeTests).
        foreach (var kind in Enum.GetValues<BuildingKind>().Where(k => !BuildSpec.IsCoreUpgrade(k)))
        {
            var recipe = kind switch
            {
                BuildingKind.Smelter => Content.SmelterRecipes()[0].Id,
                BuildingKind.Assembler => Content.AssemblerRecipes()[0].Id,
                _ => null
            };
            yield return new BuildSpec(BuildSpec.StructureIdFor(kind)!, kind, origin, Direction.East, recipe);
        }
    }

    private static RefusalCode? Submit(Factory factory, SimCommand command)
    {
        var host = new SimHost(factory);
        RefusalCode? reason = null;
        host.CommandRejected += (_, r) => reason = r.Code;
        host.Submit(command);
        host.AdvanceTicks(1);
        return reason;
    }

    [Fact]
    public void WallBlocksEveryBlockKind()
    {
        foreach (var wall in Enum.GetValues<WallKind>().Where(k => k != WallKind.None))
        {
            // Wall on the origin.
            var factory = Sandbox((10, 10, Wall(wall)));
            foreach (var spec in EverySpec(new GridPosition(10, 10)))
            {
                Assert.True(factory.IsValidSpec(spec), spec.StructureId);
                Assert.False(factory.CanPlace(spec, out var reason), $"{spec.StructureId} on {wall}");
                Assert.Equal(RefusalCode.Wall, reason);
            }

            // Wall under the far corner of a 2×2 footprint.
            var big = EverySpec(new GridPosition(9, 9)).Where(s => factory.Footprint(s).Count == 4).ToList();
            Assert.NotEmpty(big);
            foreach (var spec in big)
            {
                Assert.False(factory.CanPlace(spec, out var reason), $"{spec.StructureId} corner on {wall}");
                Assert.Equal(RefusalCode.Wall, reason);
            }

            Assert.False(factory.CanOccupy(new GridPosition(10, 10)));
            Assert.False(factory.TryPlaceBelt(new GridPosition(10, 10), Direction.East));
        }

        // Through the commands: instant build and drone plans give the same reason.
        var instant = Sandbox((10, 10, Wall()));
        Assert.Equal(RefusalCode.Wall,
            Submit(instant, new PlaceBelt(0, new GridPosition(10, 10), Direction.East, "conveyor-basic")));
        Assert.Equal(RefusalCode.Wall,
            Submit(instant, new PlaceBuilding(0, BuildingKind.Miner, new GridPosition(9, 9), Direction.East)));
        Assert.Equal(0, instant.Belts.Count);
        Assert.Empty(instant.Miners);

        var planned = WithTerrain(
            NewGame.Sandbox(Content, new GridPosition(30, 30), mapWidth: Size, mapHeight: Size),
            [(10, 10, Wall())]);
        Assert.Equal(RefusalCode.Wall,
            Submit(planned, new PlaceBelt(0, new GridPosition(10, 10), Direction.East, "conveyor-basic")));
        Assert.Empty(planned.BuilderOf(0)!.Plans);
    }

    [Fact]
    public void DeepWaterBlocksBeltsButNotBridgeSpan()
    {
        // A river two cells wide (x = 10..11) and a wall at (12, 6).
        var cells = new List<(int, int, TerrainTile)>();
        for (var y = 0; y < Size; y++)
        {
            for (var x = 10; x <= 11; x++)
            {
                cells.Add((x, y, Deep));
            }
        }

        cells.Add((12, 6, Wall()));
        var factory = Sandbox([.. cells]);

        Assert.False(factory.CanPlace(
            new BuildSpec("conveyor-basic", null, new GridPosition(11, 5), Direction.East), out var reason));
        Assert.Equal(RefusalCode.DeepWater, reason);
        Assert.Equal(RefusalCode.DeepWater,
            Submit(factory, new PlaceBelt(0, new GridPosition(11, 5), Direction.East, "conveyor-basic")));

        // The bridge jumps the river: the exit is the first buildable cell past it.
        Assert.Null(Submit(factory, new PlaceBuilding(0, BuildingKind.Bridge, new GridPosition(9, 5), Direction.East)));
        Assert.True(factory.Belts.TryGet(new GridPosition(9, 5), out var entry));
        Assert.Equal(new GridPosition(12, 5), entry.BridgePartner);

        // An exit on a wall is skipped too.
        Assert.Null(Submit(factory, new PlaceBuilding(0, BuildingKind.Bridge, new GridPosition(9, 6), Direction.East)));
        Assert.True(factory.Belts.TryGet(new GridPosition(9, 6), out var walled));
        Assert.Equal(new GridPosition(13, 6), walled.BridgePartner);

        // A bridge cannot start in the river.
        Assert.False(factory.CanPlace(
            new BuildSpec(BuildSpec.StructureIdFor(BuildingKind.Bridge)!, BuildingKind.Bridge, new GridPosition(11, 7), Direction.East), out reason));
        Assert.Equal(RefusalCode.DeepWater, reason);
    }

    [Fact]
    public void BridgeWithoutExitSaysWhy()
    {
        // From (9, y) going East the exit candidates are x = 11..13 (spans 2..4).
        var cells = new List<(int, int, TerrainTile)>();
        for (var x = 10; x <= 15; x++)
        {
            cells.Add((x, 5, Deep));
            cells.Add((x, 8, Wall()));
        }

        // Row 11: two candidates are deep water, the others belts — the water is still why there is no exit.
        cells.Add((11, 11, Deep));
        cells.Add((12, 11, Deep));
        var factory = Sandbox([.. cells]);
        foreach (var x in new[] { 13, 14 })
        {
            Assert.Null(Submit(factory, new PlaceBelt(0, new GridPosition(x, 11), Direction.East, "conveyor-basic")));
        }

        var bridgeId = BuildSpec.StructureIdFor(BuildingKind.Bridge)!;
        foreach (var (y, expected) in new[]
                 {
                     (5, RefusalCode.DeepWater), (8, RefusalCode.Wall), (11, RefusalCode.DeepWater)
                 })
        {
            var spec = new BuildSpec(bridgeId, BuildingKind.Bridge, new GridPosition(9, y), Direction.East);
            Assert.False(factory.CanPlace(spec, out var reason));
            Assert.Equal(expected, reason);
            Assert.Equal(expected,
                Submit(factory, new PlaceBuilding(0, BuildingKind.Bridge, new GridPosition(9, y), Direction.East)));
        }

        // Only belts in the way: still "occupied".
        foreach (var x in new[] { 11, 12, 13, 14 })
        {
            Assert.Null(Submit(factory, new PlaceBelt(0, new GridPosition(x, 20), Direction.East, "conveyor-basic")));
        }

        Assert.Equal(RefusalCode.Occupied,
            Submit(factory, new PlaceBuilding(0, BuildingKind.Bridge, new GridPosition(9, 20), Direction.East)));
    }

    [Fact]
    public void ShallowWaterIsBuildable()
    {
        var factory = Sandbox((10, 10, Shallow), (11, 10, Shallow), (10, 11, Shallow), (11, 11, Shallow));
        foreach (var spec in EverySpec(new GridPosition(10, 10)))
        {
            Assert.True(factory.CanPlace(spec, out var reason), $"{spec.StructureId}: {reason}");
        }

        Assert.Null(Submit(factory, new PlaceBuilding(0, BuildingKind.Miner, new GridPosition(10, 10), Direction.East)));
        Assert.Single(factory.Miners);
    }

    [Fact]
    public void OutsideMapIsOccupied()
    {
        var factory = Sandbox();
        foreach (var origin in new[] { new GridPosition(Size, 5), new GridPosition(5, Size), new GridPosition(-1, 5) })
        {
            Assert.False(factory.CanPlace(new BuildSpec("conveyor-basic", null, origin, Direction.East), out var reason));
            Assert.Equal(RefusalCode.Occupied, reason);
        }

        // A 2×2 hanging over the right edge.
        Assert.False(factory.CanPlace(
            new BuildSpec("miner", BuildingKind.Miner, new GridPosition(Size - 1, 5), Direction.East), out var edge));
        Assert.Equal(RefusalCode.Occupied, edge);
        Assert.True(factory.CanOccupyFootprint(new GridPosition(Size - 2, 5), 2));

        // A bridge whose every exit is outside the map has no exit.
        Assert.False(factory.CanPlace(
            new BuildSpec(BuildSpec.StructureIdFor(BuildingKind.Bridge)!, BuildingKind.Bridge, new GridPosition(Size - 2, 5), Direction.East), out _));

        // Without a terrain (demo factory) nothing blocks, out of map included.
        var demo = NewGame.Demo(Content);
        Assert.Null(demo.Terrain);
        Assert.True(demo.CanOccupy(new GridPosition(5000, 5000)));
    }

    [Fact]
    public void ReasonsAreWallAndDeepWater()
    {

        // Order: Core, wall, deep water, occupied. The Core is at (30,30)-(31,31).
        var factory = Sandbox((29, 29, Wall()), (20, 20, Wall()), (21, 20, Deep), (25, 25, Deep));
        Assert.False(factory.CanPlace(
            new BuildSpec("miner", BuildingKind.Miner, new GridPosition(29, 29), Direction.East), out var reason));
        Assert.Equal(RefusalCode.OnCore, reason);
        Assert.False(factory.CanPlace(
            new BuildSpec("miner", BuildingKind.Miner, new GridPosition(20, 19), Direction.East), out reason));
        Assert.Equal(RefusalCode.Wall, reason);
        Assert.False(factory.CanPlace(
            new BuildSpec("miner", BuildingKind.Miner, new GridPosition(24, 24), Direction.East), out reason));
        Assert.Equal(RefusalCode.DeepWater, reason);
        Assert.True(factory.TryPlaceBelt(new GridPosition(5, 5), Direction.East));
        Assert.False(factory.CanPlace(
            new BuildSpec("miner", BuildingKind.Miner, new GridPosition(4, 4), Direction.East), out reason));
        Assert.Equal(RefusalCode.Occupied, reason);
    }

    [Fact]
    public void TerrainTableCoversEveryKind()
    {
        var table = Content.Terrain;
        foreach (var kind in Enum.GetValues<FloorKind>())
        {
            var floor = table.Floor(kind);
            Assert.NotNull(floor);
            Assert.Equal(kind != FloorKind.DeepWater, floor.Buildable);
            Assert.Equal(
                kind == FloorKind.DeepWater,
                new TerrainTile(kind, WallKind.None, DepositKind.None).BlocksBuilding);
        }

        foreach (var kind in Enum.GetValues<WallKind>().Where(k => k != WallKind.None))
        {
            Assert.NotNull(table.Wall(kind));
            Assert.True(new TerrainTile(FloorKind.Grass, kind, DepositKind.None).BlocksBuilding);
        }

        Assert.Equal(Enum.GetValues<FloorKind>().Length, table.Floors.Count);
        Assert.Equal(Enum.GetValues<WallKind>().Length - 1, table.Walls.Count);
        Assert.Equal("deep-water", table.Floor(FloorKind.DeepWater)!.Id);
        Assert.Equal("shrub-wall", table.Wall(WallKind.ShrubWall)!.Id);

        // Every row has its own colour (campaign map files are drawn with them).
        var colours = table.Floors.Select(f => f.Color).Concat(table.Walls.Select(w => w.Color)).ToList();
        Assert.Equal(colours.Count, colours.Distinct().Count());

        // The classic map keeps its colours.
        Assert.Equal("#2E4E36", table.Floor(FloorKind.Grass)!.Color);
        Assert.Equal("#6C583A", table.Floor(FloorKind.Soil)!.Color);
        Assert.Equal("#5C6260", table.Floor(FloorKind.Stone)!.Color);
        Assert.Equal("#2A4E62", table.Floor(FloorKind.ShallowWater)!.Color);
    }

    [Fact]
    public void EveryFloorHasEdgePriority()
    {
        // Edge order (G2b): the floor with the higher priority draws its jagged edge over the
        // lower neighbour; every floor has its own value and water is below every land floor.
        var floors = Content.Terrain.Floors;
        Assert.Equal(floors.Count, floors.Select(f => f.EdgePriority).Distinct().Count());
        Assert.All(floors, f => Assert.True(f.EdgePriority >= 0, f.Id));
        var water = new[] { FloorKind.ShallowWater, FloorKind.DeepWater };
        var land = floors.Where(f => !water.Contains(f.Kind)).Min(f => f.EdgePriority);
        Assert.All(floors.Where(f => water.Contains(f.Kind)), f => Assert.True(f.EdgePriority < land, f.Id));
        Assert.Equal(
            floors.Min(f => f.EdgePriority),
            Content.Terrain.Floor(FloorKind.DeepWater)!.EdgePriority);
    }

    [Fact]
    public void DeepWaterWinsOverOccupiedInAMixedFootprint()
    {
        // A 2×2 miner over an existing belt (9, 9) and deep water (10, 10).
        var factory = Sandbox((10, 10, Deep));
        Assert.True(factory.TryPlaceBelt(new GridPosition(9, 9), Direction.East));
        Assert.False(factory.CanPlace(
            new BuildSpec("miner", BuildingKind.Miner, new GridPosition(9, 9), Direction.East), out var reason));
        Assert.Equal(RefusalCode.DeepWater, reason);
        Assert.Equal(RefusalCode.DeepWater,
            Submit(factory, new PlaceBuilding(0, BuildingKind.Miner, new GridPosition(9, 9), Direction.East)));
        Assert.Empty(factory.Miners);
    }

    [Theory]
    [InlineData("missing", "manca la sezione terrain")]
    [InlineData("unknown-floor", "id sconosciuto 'lava'")]
    [InlineData("unknown-wall", "id sconosciuto 'lava-wall'")]
    [InlineData("none-wall", "non deve contenere 'none'")]
    [InlineData("deep-buildable", "buildable deve essere false")]
    [InlineData("shallow-not-buildable", "buildable deve essere true")]
    [InlineData("bad-colour", "non nel formato #RRGGBB")]
    [InlineData("short-colour", "non nel formato #RRGGBB")]
    [InlineData("duplicate-colour", "colore diverso")]
    [InlineData("duplicate-row", "deve avere una riga per 'Stone' (trovate 2)")]
    [InlineData("missing-row", "deve avere una riga per 'IceWall' (trovate 0)")]
    [InlineData("unknown-biome", "bioma 'vulcano' sconosciuto")]
    [InlineData("missing-edge-priority", "'sand': manca edgePriority")]
    [InlineData("negative-edge-priority", "'sand': edgePriority deve essere >= 0")]
    [InlineData("duplicate-edge-priority", "edgePriority diversa per ogni suolo")]
    [InlineData("water-above-land", "l'acqua deve avere edgePriority più bassa")]
    public void TerrainSectionIsValidated(string change, string message)
    {
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "content.json")))!;
        var terrain = root["terrain"]!;
        var floors = terrain["floors"]!.AsArray();
        var walls = terrain["walls"]!.AsArray();
        JsonNode Floor(string id) => floors.First(f => (string?)f!["id"] == id)!;
        switch (change)
        {
            case "missing": root.AsObject().Remove("terrain"); break;
            case "unknown-floor": Floor("mud")["id"] = "lava"; break;
            case "unknown-wall": walls[0]!["id"] = "lava-wall"; break;
            case "none-wall": walls[0]!["id"] = "none"; break;
            case "deep-buildable": Floor("deep-water")["buildable"] = true; break;
            case "shallow-not-buildable": Floor("shallow-water")["buildable"] = false; break;
            case "bad-colour": Floor("sand")["color"] = "#12345G"; break;
            case "short-colour": Floor("sand")["color"] = "#123"; break;
            case "duplicate-colour": Floor("sand")["color"] = (string?)Floor("snow")["color"]; break;
            case "duplicate-row": floors.Add(JsonNode.Parse("""{ "id": "stone", "biome": "rocce", "color": "#010203", "edgePriority": 50 }""")); break;
            case "missing-row": walls.RemoveAt(walls.ToList().FindIndex(w => (string?)w!["id"] == "ice-wall")); break;
            case "unknown-biome": Floor("gravel")["biome"] = "vulcano"; break;
            case "missing-edge-priority": Floor("sand").AsObject().Remove("edgePriority"); break;
            case "negative-edge-priority": Floor("sand")["edgePriority"] = -1; break;
            case "duplicate-edge-priority": Floor("sand")["edgePriority"] = (int)Floor("snow")["edgePriority"]!; break;
            case "water-above-land": Floor("shallow-water")["edgePriority"] = 99; break;
        }

        var dir = Directory.CreateTempSubdirectory("tindustry-content-");
        try
        {
            var path = Path.Combine(dir.FullName, "content.json");
            File.WriteAllText(path, root.ToJsonString());
            var error = Assert.Throws<InvalidDataException>(() => FactoryContent.Load(path));
            Assert.Contains(message, error.Message, StringComparison.Ordinal);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
