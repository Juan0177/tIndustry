using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Runs the scenario self-tests that live in <see cref="FactorySlice"/> (they throw on failure).
/// Replaces the legacy Raylib <c>--self-test</c> as the CI gate for the official sim.
/// </summary>
[Collection(SaveStoreCollection.Name)]
public sealed class FactorySliceSelfTests
{
    private static readonly string ContentPath = Path.Combine(AppContext.BaseDirectory, "data", "content.json");
    private static readonly string CampaignPath = Path.Combine(AppContext.BaseDirectory, "data", "campaign.json");

    public static TheoryData<string> Scenarios => new()
    {
        nameof(FactorySlice.SelfTest),
        nameof(FactorySlice.SelfTestPlaceCosts),
        nameof(FactorySlice.SelfTestPlaceable),
        nameof(FactorySlice.SelfTestT2Tools),
        nameof(FactorySlice.SelfTestSmelterLoop),
        nameof(FactorySlice.SelfTestMultiRecipe),
        nameof(FactorySlice.SelfTestAssemblerLoop),
        nameof(FactorySlice.SelfTestJunctionSplitter),
        nameof(FactorySlice.SelfTestPowerStub),
        nameof(FactorySlice.SelfTestFuelOrPower),
        nameof(FactorySlice.SelfTestPowerBuffer),
        nameof(FactorySlice.SelfTestAdjacentIo),
        nameof(FactorySlice.SelfTestMinerPower),
        nameof(FactorySlice.SelfTestSaveLoad),
        nameof(FactorySlice.SelfTestSaveManager),
        nameof(FactorySlice.SelfTestSorterBridge),
        nameof(FactorySlice.SelfTestResearch),
        nameof(FactorySlice.SelfTestMercato),
        nameof(FactorySlice.SelfTestCoreUpgrade),
        nameof(FactorySlice.SelfTestAutoSell),
    };

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Scenario(string name)
    {
        var method = typeof(FactorySlice).GetMethod(name, [typeof(string)])
            ?? throw new MissingMethodException(nameof(FactorySlice), name);
        try
        {
            method.Invoke(null, [ContentPath]);
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }

    [Fact]
    public void Campaign() => FactorySlice.SelfTestCampaign(ContentPath, CampaignPath);

    [Fact]
    public void ItemVisualColorsAreDistinct() => Assert.Null(ItemVisualColors.SelfTest());
}
