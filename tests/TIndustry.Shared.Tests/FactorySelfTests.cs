using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Runs the scenario self-tests that live in <see cref="Factory"/> (they throw on failure).
/// Replaces the legacy Raylib <c>--self-test</c> as the CI gate for the official sim.
/// </summary>
[Collection(SaveStoreCollection.Name)]
public sealed class FactorySelfTests
{
    private static readonly string ContentPath = Path.Combine(AppContext.BaseDirectory, "data", "content.json");
    private static readonly string CampaignPath = Path.Combine(AppContext.BaseDirectory, "data", "campaign.json");

    public static TheoryData<string> Scenarios => new()
    {
        nameof(FactoryScenarios.SelfTest),
        nameof(FactoryScenarios.SelfTestPlaceCosts),
        nameof(FactoryScenarios.SelfTestPlaceable),
        nameof(FactoryScenarios.SelfTestT2Tools),
        nameof(FactoryScenarios.SelfTestSmelterLoop),
        nameof(FactoryScenarios.SelfTestMultiRecipe),
        nameof(FactoryScenarios.SelfTestAssemblerLoop),
        nameof(FactoryScenarios.SelfTestJunctionSplitter),
        nameof(FactoryScenarios.SelfTestPowerStub),
        nameof(FactoryScenarios.SelfTestFuelOrPower),
        nameof(FactoryScenarios.SelfTestPowerBuffer),
        nameof(FactoryScenarios.SelfTestAdjacentIo),
        nameof(FactoryScenarios.SelfTestMinerPower),
        nameof(FactoryScenarios.SelfTestSaveLoad),
        nameof(FactoryScenarios.SelfTestSaveManager),
        nameof(FactoryScenarios.SelfTestSorterBridge),
        nameof(FactoryScenarios.SelfTestResearch),
        nameof(FactoryScenarios.SelfTestMercato),
        nameof(FactoryScenarios.SelfTestAutoSell),
    };

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Scenario(string name)
    {
        var method = typeof(FactoryScenarios).GetMethod(name, [typeof(string)])
            ?? throw new MissingMethodException(nameof(FactoryScenarios), name);
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
    public void Campaign() => FactoryScenarios.SelfTestCampaign(ContentPath, CampaignPath);

    [Fact]
    public void ItemVisualColorsAreDistinct() => Assert.Null(ItemVisualColors.SelfTest());
}
