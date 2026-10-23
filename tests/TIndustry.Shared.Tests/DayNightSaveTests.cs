using System.Text;
using System.Text.Json.Nodes;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Day and night in the campaign (every level's <c>dayNight</c> in campaign.json) and in the v16 save: the rule, the
/// first block's tick and the first attack already raised survive a save, a snapshot and the checksum.
/// </summary>
[Collection(SaveStoreCollection.Name)]
public sealed class DayNightSaveTests : IDisposable
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private static readonly FactoryContent Content = FactoryContent.Load(Path.Combine(DataDir, "content.json"));
    private static readonly GridPosition FreeCell = new(66, 61);

    private readonly string dir = Path.Combine(Path.GetTempPath(), $"tindustry-giorno-notte-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static Factory Sandbox(DayNightMode mode) =>
        NewGame.Sandbox(
            Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7,
            rules: new GameRules(InstantBuild: true, DayNight: mode));

    /// <summary>The game state behind a host (its view is the factory).</summary>
    private static GameState StateOf(SimHost host) => ((Factory)host.State).State;

    private static PlaceBelt Belt(GridPosition tile) => new(0, tile, Direction.East, "conveyor-basic");

    private static CampaignCatalog ShippedCampaign() => CampaignCatalog.Load(Path.Combine(DataDir, "campaign.json"));

    [Fact]
    public void EveryLevelChoosesItsTime()
    {
        var catalog = ShippedCampaign();
        var expected = new Dictionary<string, DayNightMode>(StringComparer.Ordinal)
        {
            ["level-01-primi-passi"] = DayNightMode.AlwaysDay,
            ["level-02-prima-fabbrica"] = DayNightMode.AlwaysDay,
            ["level-03-lastre"] = DayNightMode.AlwaysDay,
            ["level-04-fili"] = DayNightMode.Cycle,
            ["level-05-mercato"] = DayNightMode.Cycle,
            ["level-06-corrente"] = DayNightMode.AlwaysNight,
            ["level-07-piombo"] = DayNightMode.Cycle,
            ["level-08-logistica"] = DayNightMode.Cycle,
            ["level-09-titanio"] = DayNightMode.Cycle,
            ["level-10-espansione"] = DayNightMode.AlwaysNight
        };
        Assert.Equal(expected.Keys.ToArray(), catalog.Levels.Select(l => l.Id).ToArray());
        foreach (var level in catalog.Levels)
        {
            Assert.Equal(expected[level.Id], level.DayNight);
        }

        // Every level in the file says it: no level relies on a default.
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(DataDir, "campaign.json")))!;
        foreach (var level in json["levels"]!.AsArray())
        {
            Assert.Contains((string?)level!["dayNight"], new[] { "ciclo", "giorno", "notte" });
            Assert.Null(level["night"]);
        }
    }

    [Fact]
    public void CampaignGameTakesTheLevelsTime()
    {
        var catalog = ShippedCampaign();
        var night = NewGame.Campaign(
            Content, catalog, catalog.Find("level-06-corrente")!, mapWidth: 80, mapHeight: 80,
            rules: new GameRules(DayNight: DayNightMode.AlwaysDay));
        Assert.Equal(DayNightMode.AlwaysNight, night.State.Rules.DayNight);
        var day = NewGame.Campaign(Content, catalog, catalog.FirstLevel!, mapWidth: 80, mapHeight: 80);
        Assert.Equal(DayNightMode.AlwaysDay, day.State.Rules.DayNight);
        var cycle = NewGame.Campaign(Content, catalog, catalog.Find("level-04-fili")!, mapWidth: 80, mapHeight: 80);
        Assert.Equal(DayNightMode.Cycle, cycle.State.Rules.DayNight);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("\"sempre\"")]
    [InlineData("\"Cycle\"")]
    [InlineData("3")]
    public void LevelWithoutAValidTimeIsALoadError(string? value)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "campaign.json");
        var extra = value is null ? string.Empty : $", \"dayNight\": {value}";
        File.WriteAllText(path, $$"""
            {"levels": [
              {"id": "uno", "name": "Uno", "description": "", "seed": 1, "dayNight": "giorno"},
              {"id": "due-senza-tempo", "name": "Due", "description": "", "seed": 2{{extra}}}
            ]}
            """);

        var error = Assert.Throws<InvalidDataException>(() => CampaignCatalog.Load(path));
        Assert.Contains("due-senza-tempo", error.Message);
        Assert.Contains("dayNight", error.Message);
    }

    [Fact]
    public void SaveInTheMiddleOfDuskLoadsWithTheSameLightAndChecksum()
    {
        var host = new SimHost(Sandbox(DayNightMode.Cycle));
        host.AdvanceTicks(25);
        host.Submit(Belt(FreeCell));
        host.AdvanceTicks(1);
        var first = host.State.FirstBuildTick!.Value;
        host.AdvanceTicks((int)(first + 35_400 - host.State.CurrentTick));
        var view = host.State;
        Assert.Equal((DayNightPhase.Dusk, 500), (view.DayNightPhase, view.Light));

        FactorySaveStore.Save("v16-tramonto", host);
        Assert.True(FactorySaveStore.TryLoadSlot(Content, "v16-tramonto", out var loaded, out var failure), failure?.Detail);
        Assert.Equal(host.Checksum(), loaded!.Checksum());
        var loadedView = loaded.State;
        Assert.Equal(first, loaded.State.FirstBuildTick);
        Assert.Equal(DayNightMode.Cycle, loaded.State.Rules.DayNight);
        Assert.Equal((DayNightPhase.Dusk, 500), (loadedView.DayNightPhase, loadedView.Light));

        // Both go on alike into the night.
        host.AdvanceTicks(900);
        loaded.AdvanceTicks(900);
        Assert.Equal(host.Checksum(), loaded.Checksum());
        Assert.Equal(DayNightPhase.Night, loadedView.DayNightPhase);
    }

    [Theory]
    [InlineData(DayNightMode.Cycle, "Cycle")]
    [InlineData(DayNightMode.AlwaysDay, "AlwaysDay")]
    [InlineData(DayNightMode.AlwaysNight, "AlwaysNight")]
    public void RuleAndFirstBlockRoundTrip(DayNightMode mode, string name)
    {
        // Before the first block: no tick in the snapshot.
        var host = new SimHost(Sandbox(mode));
        host.AdvanceTicks(3);
        var before = Encoding.UTF8.GetString(host.CreateSnapshot());
        Assert.Contains($"\"dayNight\":\"{name}\"", before);
        Assert.DoesNotContain("firstBuildTick", before);
        Assert.DoesNotContain("\"night\"", before);
        var restored = SimHost.FromSnapshot(Content, host.CreateSnapshot());
        Assert.Null(restored.State.FirstBuildTick);
        Assert.Equal(mode, restored.State.Rules.DayNight);
        Assert.Equal(host.Checksum(), restored.Checksum());

        // After it: the tick is saved and is part of the checksum.
        host.Submit(Belt(FreeCell));
        host.AdvanceTicks(2);
        var first = host.State.FirstBuildTick!.Value;
        Assert.Contains($"\"firstBuildTick\":{first}", Encoding.UTF8.GetString(host.CreateSnapshot()));
        restored = SimHost.FromSnapshot(Content, host.CreateSnapshot());
        Assert.Equal(first, restored.State.FirstBuildTick);
        Assert.Equal(mode, restored.State.Rules.DayNight);
        Assert.Equal(host.Checksum(), restored.Checksum());

        var shifted = SimHost.FromSnapshot(Content, host.CreateSnapshot());
        StateOf(shifted).FirstBuildTick = first + 1;
        Assert.NotEqual(host.Checksum(), shifted.Checksum());
    }

    [Fact]
    public void UnknownRuleNameIsUnreadable()
    {
        var factory = Sandbox(DayNightMode.Cycle);
        var data = factory.Capture();
        data.Rules.DayNight = "notte";
        Assert.Throws<InvalidDataException>(() => Factory.Restore(Content, data));
        data.Rules.DayNight = null;
        Assert.Throws<InvalidDataException>(() => Factory.Restore(Content, data));
    }

    [Fact]
    public void FirstAttackIsNotRaisedAgainAfterASnapshotOrALoad()
    {
        var raised = 0;
        var host = new SimHost(Sandbox(DayNightMode.AlwaysNight));
        host.EventRaised += e => raised += e is FirstAttackDue ? 1 : 0;
        host.Submit(Belt(FreeCell));
        host.AdvanceTicks((int)DayNightClock.FirstAttackTicks + 100);
        Assert.Equal(1, raised);
        Assert.True(StateOf(host).FirstAttackRaised);
        Assert.Contains("\"firstAttackRaised\":true", Encoding.UTF8.GetString(host.CreateSnapshot()));

        var snapshotted = SimHost.FromSnapshot(Content, host.CreateSnapshot());
        Assert.Equal(host.Checksum(), snapshotted.Checksum());
        FactorySaveStore.Save("v16-primo-attacco", host);
        Assert.True(FactorySaveStore.TryLoadSlot(Content, "v16-primo-attacco", out var loaded, out var failure), failure?.Detail);
        Assert.Equal(host.Checksum(), loaded!.Checksum());

        foreach (var game in new[] { snapshotted, loaded })
        {
            var again = 0;
            game.EventRaised += e => again += e is FirstAttackDue ? 1 : 0;
            Assert.True(StateOf(game).FirstAttackRaised);
            // Keep building for 40 000 more ticks: one belt every 1 000 ticks.
            for (var i = 0; i < 40; i++)
            {
                game.Submit(Belt(new GridPosition(66 + (i % 20), 64 + i / 20)));
                game.AdvanceTicks(1_000);
            }

            Assert.Equal(0, again);
        }

        // A snapshot that forgot the raised attack would raise it again: the flag is what keeps it once.
        var forgot = SimHost.FromSnapshot(Content, host.CreateSnapshot());
        StateOf(forgot).FirstAttackRaised = false;
        Assert.NotEqual(host.Checksum(), forgot.Checksum());
        var raisedAgain = 0;
        forgot.EventRaised += e => raisedAgain += e is FirstAttackDue ? 1 : 0;
        forgot.AdvanceTicks(1);
        Assert.Equal(1, raisedAgain);
    }
}
