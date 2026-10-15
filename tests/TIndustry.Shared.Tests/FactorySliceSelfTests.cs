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
        nameof(FactorySliceScenarios.SelfTest),
        nameof(FactorySliceScenarios.SelfTestPlaceCosts),
        nameof(FactorySliceScenarios.SelfTestPlaceable),
        nameof(FactorySliceScenarios.SelfTestT2Tools),
        nameof(FactorySliceScenarios.SelfTestSmelterLoop),
        nameof(FactorySliceScenarios.SelfTestMultiRecipe),
        nameof(FactorySliceScenarios.SelfTestAssemblerLoop),
        nameof(FactorySliceScenarios.SelfTestJunctionSplitter),
        nameof(FactorySliceScenarios.SelfTestPowerStub),
        nameof(FactorySliceScenarios.SelfTestFuelOrPower),
        nameof(FactorySliceScenarios.SelfTestPowerBuffer),
        nameof(FactorySliceScenarios.SelfTestAdjacentIo),
        nameof(FactorySliceScenarios.SelfTestMinerPower),
        nameof(FactorySliceScenarios.SelfTestSaveLoad),
        nameof(FactorySliceScenarios.SelfTestSaveManager),
        nameof(FactorySliceScenarios.SelfTestSorterBridge),
        nameof(FactorySliceScenarios.SelfTestResearch),
        nameof(FactorySliceScenarios.SelfTestMercato),
        nameof(FactorySliceScenarios.SelfTestCoreUpgrade),
        nameof(FactorySliceScenarios.SelfTestAutoSell),
    };

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Scenario(string name)
    {
        var method = typeof(FactorySliceScenarios).GetMethod(name, [typeof(string)])
            ?? throw new MissingMethodException(nameof(FactorySliceScenarios), name);
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
    public void Campaign() => FactorySliceScenarios.SelfTestCampaign(ContentPath, CampaignPath);

    [Fact]
    public void ItemVisualColorsAreDistinct() => Assert.Null(ItemVisualColors.SelfTest());
}
