using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

public sealed class CrafterWorkingTests
{
    private static readonly string ContentPath = Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    private static Crafter Furnace()
    {
        var recipe = FactoryContent.Load(ContentPath).FindRecipe("smelt-iron")!;
        var f = new Crafter(new GridPosition(0, 0), Direction.East, recipe);
        Assert.True(f.TryAccept("iron-ore") && f.TryAccept("iron-ore"));
        return f;
    }

    [Fact]
    public void StalledFurnaceIsCraftingButNotWorking()
    {
        var f = Furnace();
        long next = 1;
        f.Tick(0.5f, new BeltGrid(), ref next, powerBp: 0);
        Assert.True(f.IsCrafting);
        Assert.False(f.IsWorking);
        Assert.Equal(0f, f.Progress);
    }

    [Fact]
    public void FurnaceWithCoalWorksAndAdvances()
    {
        var f = Furnace();
        long next = 1;
        f.SeedFuel(2);
        f.Tick(0.5f, new BeltGrid(), ref next, powerBp: 0);
        Assert.True(f.IsWorking);
        Assert.True(f.Progress > 0f);
    }

    [Fact]
    public void FurnaceWithPowerWorksAndAdvances()
    {
        var f = Furnace();
        long next = 1;
        f.Tick(0.5f, new BeltGrid(), ref next, powerBp: 10_000);
        Assert.True(f.IsWorking);
        Assert.True(f.Progress > 0f);
    }
}
