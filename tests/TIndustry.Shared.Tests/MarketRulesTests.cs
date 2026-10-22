using System.Text.Json.Nodes;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Market data: baseValue per item and the marketRules block of content.json (values of the spec).</summary>
public sealed class MarketRulesTests
{
    private static readonly string ContentPath = Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    [Fact]
    public void ShippedRulesMatchTheSpec()
    {
        var expected = new MarketRules(
            NormalMinMilli: 1000, NormalMaxMilli: 1600, NormalAverageMilli: 1300, NormalStepSeconds: 4,
            NormalPullPermille: 80, NormalNoiseMilli: 60,
            SpikeThresholdMilli: 2800, SpikeMaxMilli: 3000, SpikeRiseSeconds: 10, SpikeHoldSeconds: 10,
            SpikeFallSeconds: 30, SpikeIntervalMinMinutes: 10, SpikeIntervalMaxMinutes: 20,
            SaleImpactMicro: 400, SaleRecoverySeconds: 120,
            CrashPieces: 5000, CrashWindowSeconds: 120, CrashMinutes: 20, CrashDipMinutes: 5,
            CrashDipMinMilli: 600, CrashDipMaxMilli: 700, CrashDipEverySeconds: 10, CrashDipChanceBp: 2500,
            JackpotMinPieces: 400, JackpotBaseBp: 200, JackpotStepPieces: 200, JackpotStepBp: 100,
            JackpotCapBp: 5000, JackpotBonusPermille: 7770,
            PityPieces: 3000, PityStepBp: 2500, PityGuaranteeMisses: 3,
            MissCooldownSeconds: 300, MissSpikeDelaySeconds: 300,
            LostBoostBp: 2000, LostBoostMinutes: 5, LostPieces: 10000, HistorySeconds: 120);

        Assert.Equal(expected, FactoryContent.Load(ContentPath).MarketRules);
    }

    [Theory]
    [InlineData("iron-ore", 6)]
    [InlineData("iron-plate", 23)]
    [InlineData("copper-ore", 5)]
    [InlineData("copper-wire", 12)]
    [InlineData("coal", 3)]
    [InlineData("lead-ore", 4)]
    [InlineData("lead-plate", 14)]
    [InlineData("titanium-ore", 14)]
    [InlineData("titanium-plate", 52)]
    [InlineData("graphite", 12)]
    [InlineData("silicon", 32)]
    public void BaseValuesMatchTheSpec(string itemId, int expected)
    {
        var catalog = MarketCatalog.FromContent(FactoryContent.Load(ContentPath));
        Assert.Equal(expected, catalog.BaseValueOf(itemId));
        Assert.Equal(expected, MarketCatalog.CreateDefault().BaseValueOf(itemId));
    }

    [Fact]
    public void UnknownItemHasBaseValueOne() =>
        Assert.Equal(1, MarketCatalog.CreateDefault().BaseValueOf("nope"));

    [Fact]
    public void MissingBaseValueIsAnItalianError()
    {
        var error = LoadChanged(root => root["market"]![0]!.AsObject().Remove("baseValue"));
        Assert.Contains("baseValue", error.Message, StringComparison.Ordinal);
        Assert.Contains("iron-ore", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingRulesBlockIsAnItalianError()
    {
        var error = LoadChanged(root => root.Remove("marketRules"));
        Assert.Contains("marketRules", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("spikeThreshold", 3.5)]
    [InlineData("normalMin", 2.0)]
    [InlineData("jackpotCap", 1.5)]
    [InlineData("crashDipMin", 0.9)]
    [InlineData("normalStepSeconds", 0)]
    public void IncoherentRulesAreAnItalianError(string field, double value)
    {
        var error = LoadChanged(root => root["marketRules"]![field] = value);
        Assert.Contains(field, error.Message, StringComparison.Ordinal);
    }

    private static InvalidDataException LoadChanged(Action<JsonObject> change)
    {
        var root = JsonNode.Parse(File.ReadAllText(ContentPath))!.AsObject();
        change(root);
        var dir = Directory.CreateTempSubdirectory("tindustry-content-");
        try
        {
            var path = Path.Combine(dir.FullName, "content.json");
            File.WriteAllText(path, root.ToJsonString());
            return Assert.Throws<InvalidDataException>(() => FactoryContent.Load(path));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
