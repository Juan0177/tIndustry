using System.Text.Json;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Command-driven host: commands queue for the next tick, are validated when applied
/// (in submit order) and report the outcome as events with today's Italian toast texts.
/// </summary>
public sealed class SimHostTests
{
    private static readonly string ContentPath =
        Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    private static FactoryContent LoadContent() => FactoryContent.Load(ContentPath);

    private static string Serialize(Factory factory) =>
        JsonSerializer.Serialize(factory.Capture(), FactorySaveStore.JsonOptions);

    /// <summary>Small sandbox: $180, 48 iron plates, 10 copper wire; only belts and miners unlocked.</summary>
    private static Factory NewSandbox(FactoryContent content) =>
        Factory.CreateSandboxSlice(content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120);

    [Fact]
    public void SubmittedCommandAppliesOnNextTick()
    {
        var factory = NewSandbox(LoadContent());
        var host = new SimHost(factory);
        var applied = new List<SimCommand>();
        host.CommandApplied += applied.Add;
        var cmd = new PlaceBelt(1, new GridPosition(10, 10), Direction.East, "conveyor-basic");

        host.Submit(cmd);

        Assert.False(host.State.Belts.ContainsKey(new GridPosition(10, 10)));
        Assert.Empty(applied);

        Assert.Equal(1, host.Advance(SimulationClock.TickSeconds));

        Assert.True(host.State.Belts.ContainsKey(new GridPosition(10, 10)));
        var single = Assert.Single(applied);
        Assert.Equal(cmd with { Tick = 1 }, single);
        Assert.Equal(1, host.State.CurrentTick);
    }

    [Fact]
    public void ManyCommandsInOneFrameApplyInOrder()
    {
        var factory = NewSandbox(LoadContent());
        var host = new SimHost(factory);
        var applied = new List<SimCommand>();
        var rejected = new List<(SimCommand, string)>();
        host.CommandApplied += applied.Add;
        host.CommandRejected += (c, r) => rejected.Add((c, r));
        var submitted = new List<SimCommand>();
        for (var x = 0; x < 30; x++)
        {
            var cmd = new PlaceBelt(1, new GridPosition(5 + x, 20), Direction.East, "conveyor-basic");
            submitted.Add(cmd);
            host.Submit(cmd);
        }

        Assert.Equal(1, host.Advance(SimulationClock.TickSeconds));

        Assert.Empty(rejected);
        for (var x = 0; x < 30; x++)
        {
            Assert.True(host.State.Belts.ContainsKey(new GridPosition(5 + x, 20)));
        }

        Assert.Equal(submitted.Select(c => c with { Tick = 1 }), applied);
    }

    [Fact]
    public void RejectedCommandLeavesStateUnchanged()
    {
        var factory = NewSandbox(LoadContent());
        var host = new SimHost(factory);
        var before = Serialize(factory);
        string? atRejection = null;
        string? reason = null;
        host.CommandRejected += (_, r) =>
        {
            reason = r;
            // Raised while the command is applied, before the tick's Step(): nothing may have changed.
            atRejection = Serialize(factory);
        };
        var applied = 0;
        host.CommandApplied += _ => applied++;

        host.Submit(new PlaceBuilding(1, BuildingKind.Smelter, new GridPosition(20, 20), Direction.East));
        host.Advance(SimulationClock.TickSeconds);

        Assert.Equal(0, applied);
        Assert.False(string.IsNullOrWhiteSpace(reason));
        Assert.Equal("Forno bloccato: sbloccalo in Ricerca (T).", reason);
        Assert.Equal(before, atRejection);
        Assert.Empty(host.State.Smelters);
    }

    [Fact]
    public void SecondCommandRejectedWhenFirstSpentTheMoney()
    {
        var content = LoadContent();
        // Junction costs $200 + 10 plates, generator $280 + 12 plates: enough for one only.
        var wallet = new EconomyWallet(
            300,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["iron-plate"] = 30 });
        var factory = new Factory(
            content,
            new BeltGrid(),
            CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), 2),
            wallet);
        var host = new SimHost(factory);
        var applied = new List<SimCommand>();
        var rejected = new List<(SimCommand Cmd, string Reason)>();
        host.CommandApplied += applied.Add;
        host.CommandRejected += (c, r) => rejected.Add((c, r));

        host.Submit(new UnlockStructure(1, "junction"));
        host.Submit(new UnlockStructure(2, "generator"));
        host.Advance(SimulationClock.TickSeconds);

        var ok = Assert.Single(applied);
        Assert.Equal("junction", ((UnlockStructure)ok).StructureId);
        var ko = Assert.Single(rejected);
        Assert.Equal("generator", ((UnlockStructure)ko.Cmd).StructureId);
        Assert.Equal("Risorse insufficienti per sbloccare.", ko.Reason);
        Assert.True(host.State.Research.IsUnlocked("junction"));
        Assert.False(host.State.Research.IsUnlocked("generator"));
        Assert.Equal(100, host.State.Wallet.Money);
    }

    [Fact]
    public void SellAndAutoSellAndUpgradeGoThroughCommands()
    {
        var content = LoadContent();
        Factory Make()
        {
            var f = NewSandbox(content);
            f.EnsureDemoBuildStock(ironPlates: 500, copperWire: 200, copperOre: 100, money: 100_000);
            foreach (var entry in content.CoreUpgrade.BuildCost)
            {
                f.Wallet.AddMaterial(entry.ItemId, entry.Amount);
            }

            return f;
        }

        var direct = Make();
        var commanded = Make();
        var host = new SimHost(commanded);
        var rejected = new List<string>();
        host.CommandRejected += (_, r) => rejected.Add(r);

        // Today's direct calls (applied before the tick), then one tick.
        Assert.True(direct.TrySellFromWallet("iron-plate", 7));
        direct.AutoSellAtCore = true;
        Assert.True(direct.TryUpgradeCore());
        direct.Step();

        host.Submit(new Sell(1, "iron-plate", 7));
        host.Submit(new SetAutoSell(1, true));
        host.Submit(new UpgradeCore(1));
        host.Advance(SimulationClock.TickSeconds);

        Assert.Empty(rejected);
        Assert.Equal(Serialize(direct), Serialize(commanded));
        Assert.True(host.State.AutoSellAtCore);
        Assert.Equal(1, host.State.CoreUpgradeLevel);

        // Selling beyond the stock is rejected with today's Mercato text; state unchanged.
        host.Submit(new Sell(1, "iron-plate", 1_000_000));
        host.Advance(SimulationClock.TickSeconds);
        Assert.Equal(["Vendita fallita (stock insufficiente)."], rejected);
    }

    [Fact]
    public void HostAdvanceRespectsTickCap()
    {
        var host = new SimHost(NewSandbox(LoadContent()));
        Assert.Equal(SimulationClock.MaxTicksPerFrame, host.Advance(1.0));
        Assert.Equal(5, host.State.CurrentTick);
    }
}
