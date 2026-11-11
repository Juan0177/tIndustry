using System.Text.Json.Nodes;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// New items and recipes of the «Ricerca allargata» (sand, glass, plastic, circuit) and the crafter profile of
/// content.json: which recipes a machine runs (<c>recipes</c> prefix), how it uses power (<c>powerMode</c>) and how
/// fast it is (<c>craftSpeed</c>).
/// </summary>
public sealed class CrafterProfileTests
{
    private const float Dt = 1f / SimulationClock.TickRate;

    private static readonly string ContentPath = Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    private static readonly FactoryContent Content = FactoryContent.Load(ContentPath);

    [Fact]
    public void ContentHasTheNewItemsAndRecipes()
    {
        Assert.Equal(2, Content.FindMarketItem("sand")?.BaseValue);
        Assert.Equal(9, Content.FindMarketItem("glass")?.BaseValue);
        Assert.Equal(20, Content.FindMarketItem("plastic")?.BaseValue);
        Assert.Equal(90, Content.FindMarketItem("circuit")?.BaseValue);

        AssertRecipe("smelt-glass", 2f, [new("sand", 2)], [new("glass", 1)]);
        AssertRecipe("press-graphite", 1.5f, [new("coal", 3)], [new("graphite", 2)]);
        AssertRecipe("plasticize-plastic", 2f, [new("coal", 2)], [new("plastic", 1)]);
        var separate = AssertRecipe(
            "separate-sand", 2f, [new("sand", 2)],
            [new("iron-ore", 30), new("copper-ore", 30), new("lead-ore", 30), new("titanium-ore", 10)]);
        Assert.True(separate.RandomOutput);
        Assert.False(Content.FindRecipe("smelt-glass")!.RandomOutput);

        var smelter = Content.FindBuilding(Crafter.SmelterBuildingId)!;
        Assert.Equal(("smelt-", CraftPowerMode.CoalOrPower, 100), (smelter.RecipePrefix, smelter.PowerMode, smelter.CraftSpeedPercent));
        var assembler = Content.FindBuilding(Crafter.AssemblerBuildingId)!;
        Assert.Equal(("craft-", CraftPowerMode.Boost, 100), (assembler.RecipePrefix, assembler.PowerMode, assembler.CraftSpeedPercent));
        Assert.Null(Content.FindBuilding(Miner.BasicId)!.RecipePrefix);
    }

    [Fact]
    public void RecipesForSmelterIncludeGlass()
    {
        Assert.Equal(
            ["smelt-iron", "smelt-lead", "smelt-titanium", "smelt-glass"],
            Content.RecipesFor(Crafter.SmelterBuildingId).Select(r => r.Id));
        Assert.Equal(
            Content.RecipesFor(Crafter.AssemblerBuildingId).Select(r => r.Id),
            Content.AssemblerRecipes().Select(r => r.Id));
        Assert.All(Content.RecipesFor(Crafter.AssemblerBuildingId), r => Assert.StartsWith("craft-", r.Id));
        Assert.Empty(Content.RecipesFor(Miner.BasicId));
        Assert.Empty(Content.RecipesFor("no-such-building"));
    }

    [Fact]
    public void SmelterMakesGlassFromSand()
    {
        var factory = NewGame.Sandbox(Content, new GridPosition(30, 30), mapWidth: 40, mapHeight: 40,
            rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        Assert.True(factory.TryPlaceSmelter(new GridPosition(4, 4), Direction.East, Content.FindRecipe("smelt-iron")!));
        var forno = factory.Smelters[0];
        Assert.Equal(CraftPowerMode.CoalOrPower, forno.PowerMode);
        Assert.Contains(forno.AvailableRecipes, r => r.Id == "smelt-glass");

        forno.SeedFuel(2);
        var ticks = TicksToCraft(forno, powerBp: 0, ["sand", "sand"]);

        Assert.Equal("smelt-glass", forno.Recipe.Id);
        Assert.Equal(["glass"], forno.OutputQueue);
        Assert.InRange(ticks, 2 * SimulationClock.TickRate - 1, 2 * SimulationClock.TickRate + 1);
    }

    [Fact]
    public void SmelterAndAssemblerSpeedsUnchanged()
    {
        // Tick counts of the code before the profile (forno 1.2× with full power, 1× on coal; assembler 1× without
        // power, 1 + 0.2 × s with it).
        var factory = NewGame.Sandbox(Content, new GridPosition(30, 30), mapWidth: 40, mapHeight: 40,
            rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        var iron = Content.FindRecipe("smelt-iron")!;
        var wire = Content.FindRecipe("craft-copper-wire")!;
        Assert.True(factory.TryPlaceSmelter(new GridPosition(4, 4), Direction.East, iron));
        Assert.True(factory.TryPlaceSmelter(new GridPosition(8, 4), Direction.East, iron));
        Assert.True(factory.TryPlaceAssembler(new GridPosition(4, 8), Direction.East, wire));
        Assert.True(factory.TryPlaceAssembler(new GridPosition(8, 8), Direction.East, wire));
        Assert.True(factory.TryPlaceAssembler(new GridPosition(12, 8), Direction.East, wire));
        Assert.Equal(CraftPowerMode.Boost, factory.Assemblers[0].PowerMode);
        Assert.True(factory.Assemblers[0].IsAssembler);

        Assert.Equal(101, TicksToCraft(factory.Smelters[0], 10_000, ["iron-ore", "iron-ore"]));
        factory.Smelters[1].SeedFuel(2);
        Assert.Equal(121, TicksToCraft(factory.Smelters[1], 0, ["iron-ore", "iron-ore"]));
        Assert.Equal(90, TicksToCraft(factory.Assemblers[0], 0, ["copper-ore", "iron-plate"]));
        Assert.Equal(76, TicksToCraft(factory.Assemblers[1], 10_000, ["copper-ore", "iron-plate"]));
        Assert.Equal(82, TicksToCraft(factory.Assemblers[2], 5_000, ["copper-ore", "iron-plate"]));
    }

    [Fact]
    public void RequiredPowerScalesWithSatisfactionAndNeverBurnsCoal()
    {
        var iron = Content.FindRecipe("smelt-iron")!;

        // Speed 2: a 2 s recipe in 1 s at full power, in 2 s at half power.
        Assert.InRange(TicksToCraft(Required(iron), 10_000, ["iron-ore", "iron-ore"]), 60, 61);
        Assert.InRange(TicksToCraft(Required(iron), 5_000, ["iron-ore", "iron-ore"]), 120, 121);

        var stalled = Required(iron);
        Assert.False(stalled.CanAcceptFuel("coal"));
        Assert.False(stalled.TryAcceptFuel("coal"));
        stalled.SeedFuel(4);
        Assert.Equal(0, stalled.FuelBuffer);
        Assert.False(stalled.HasCoal);
        Assert.False(stalled.UsesCoalOrPower);
        Assert.Equal(-1, TicksToCraft(stalled, 0, ["iron-ore", "iron-ore"], limit: 600));
        Assert.True(stalled.IsCrafting);
        Assert.True(stalled.WantsPower);
        Assert.False(stalled.IsWorking);
        Assert.Equal(0f, stalled.Progress);
    }

    [Fact]
    public void NoPowerModeWorksAtItsSpeedAndNeverAsks()
    {
        var iron = Content.FindRecipe("smelt-iron")!;
        var press = new Crafter(new GridPosition(0, 0), Direction.East, iron, "test-press",
            powerMode: CraftPowerMode.None, craftSpeedPercent: 150);

        Assert.InRange(TicksToCraft(press, 0, ["iron-ore", "iron-ore"]), 80, 81);
        Assert.False(press.IsPowered);
        Assert.False(press.CanAcceptFuel("coal"));

        var busy = new Crafter(new GridPosition(0, 0), Direction.East, iron, "test-press",
            powerMode: CraftPowerMode.None, craftSpeedPercent: 150);
        Assert.True(busy.TryAccept("iron-ore") && busy.TryAccept("iron-ore"));
        busy.TakeInputs(new BeltGrid());
        Assert.True(busy.IsCrafting);
        Assert.False(busy.WantsPower);

        // Power given anyway changes nothing.
        Assert.InRange(TicksToCraft(new Crafter(new GridPosition(0, 0), Direction.East, iron, "test-press",
            powerMode: CraftPowerMode.None, craftSpeedPercent: 150), 10_000, ["iron-ore", "iron-ore"]), 80, 81);
    }

    [Theory]
    [InlineData("""{ "powerMode": "turbo" }""", "powerMode")]
    [InlineData("""{ "recipes": "nothing-" }""", "recipes")]
    [InlineData("""{ "craftSpeed": 0 }""", "craftSpeed")]
    public void BadSmelterProfileIsRefused(string change, string expected)
    {
        var message = LoadMutated(root =>
        {
            var smelter = root["buildings"]!.AsArray().First(b => (string?)b!["id"] == Crafter.SmelterBuildingId)!.AsObject();
            foreach (var (key, value) in JsonNode.Parse(change)!.AsObject())
            {
                smelter[key] = value?.DeepClone();
            }
        });
        Assert.Contains(expected, message);
    }

    [Fact]
    public void UnknownPowerModeIsRefused()
    {
        var message = LoadMutated(root =>
            root["buildings"]!.AsArray().First(b => (string?)b!["id"] == Crafter.AssemblerBuildingId)!["powerMode"] = "boostt");
        Assert.Contains("powerMode", message);
        Assert.Contains("boostt", message);
    }

    [Fact]
    public void ProfileWithoutRecipesIsRefused()
    {
        var message = LoadMutated(root =>
            root["buildings"]!.AsArray().First(b => (string?)b!["id"] == Miner.BasicId)!["powerMode"] = "none");
        Assert.Contains("recipes", message);
    }

    [Fact]
    public void RandomOutputNeedsPositiveWeights()
    {
        var message = LoadMutated(root =>
        {
            var recipe = root["recipes"]!.AsArray().First(r => (string?)r!["id"] == "separate-sand")!;
            recipe["outputs"]![3]!["amount"] = 0;
        });
        Assert.Contains("separate-sand", message);
        Assert.Contains("randomOutput", message);
    }

    private static Crafter Required(RecipeDefinition recipe) =>
        new(new GridPosition(0, 0), Direction.East, recipe, "test-required",
            powerMode: CraftPowerMode.Required, craftSpeedPercent: 200);

    /// <summary>Feeds <paramref name="inputs"/> and ticks until a piece is out (no belts around): -1 after <paramref name="limit"/> ticks.</summary>
    private static int TicksToCraft(Crafter crafter, int powerBp, string[] inputs, int limit = 10_000)
    {
        foreach (var item in inputs)
        {
            Assert.True(crafter.TryAccept(item), item);
        }

        var belts = new BeltGrid();
        long next = 1;
        for (var tick = 1; tick <= limit; tick++)
        {
            crafter.Tick(Dt, belts, ref next, powerBp);
            if (crafter.OutputQueue.Count > 0)
            {
                return tick;
            }
        }

        return -1;
    }

    private static RecipeDefinition AssertRecipe(
        string id, float seconds, ResourceAmount[] inputs, ResourceAmount[] outputs)
    {
        var recipe = Content.FindRecipe(id);
        Assert.NotNull(recipe);
        Assert.Equal(seconds, recipe.DurationSeconds);
        Assert.Equal(inputs, recipe.Inputs);
        Assert.Equal(outputs, recipe.Outputs);
        return recipe;
    }

    /// <summary>Loads content.json changed by <paramref name="mutate"/> and returns the error message.</summary>
    private static string LoadMutated(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(File.ReadAllText(ContentPath))!.AsObject();
        mutate(root);
        var path = Path.Combine(Path.GetTempPath(), $"tindustry-content-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, root.ToJsonString());
        try
        {
            return Assert.Throws<InvalidDataException>(() => FactoryContent.Load(path)).Message;
        }
        finally
        {
            File.Delete(path);
        }
    }
}
