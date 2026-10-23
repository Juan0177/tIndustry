using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

[Collection(SaveStoreCollection.Name)]
public sealed class SaveRoundTripTests
{
    private static readonly string ContentPath =
        Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    [Fact]
    public void NewGameSavesAndReloads()
    {
        var content = FactoryContent.Load(ContentPath);
        var factory = NewGame.Sandbox(content, new GridPosition(9, 14), seed: 7);
        Assert.True(factory.TryPlaceMiner(new GridPosition(2, 7), Direction.East));
        Assert.True(factory.TryPlaceBelt(new GridPosition(4, 8), Direction.East));

        FactorySaveStore.Save("roundtrip", new SimHost(factory));
        Assert.True(FactorySaveStore.TryLoad(content, "roundtrip", out var host, out _));
        var reloaded = (Factory)host!.State;

        Assert.Equal(factory.Miners.Count, reloaded.Miners.Count);
        Assert.Equal(factory.Belts.Count, reloaded.Belts.Count);
        Assert.Equal(factory.Wallet.Money, reloaded.Wallet.Money);
        Assert.NotEmpty(reloaded.Miners);
    }

    [Fact]
    public void OldSaveVersionIsRejectedWithClearMessage()
    {
        var content = FactoryContent.Load(ContentPath);
        var factory = NewGame.Sandbox(content, new GridPosition(9, 14), seed: 7);
        FactorySaveStore.Save("vecchio", new SimHost(factory));
        RewriteVersion("vecchio", FactorySaveData.MinReadableVersion - 1);

        Assert.False(FactorySaveStore.TryLoad(content, "vecchio", out _, out var error));
        Assert.Equal("Salvataggio di una versione precedente: non compatibile.", error);
        Assert.False(FactorySaveStore.TryLoadSlot(content, "vecchio", out _, out var failure));
        Assert.Equal(SaveLoadError.OlderVersion, failure!.Code);
    }

    [Fact]
    public void FutureSaveVersionIsRejected()
    {
        var content = FactoryContent.Load(ContentPath);
        var factory = NewGame.Sandbox(content, new GridPosition(9, 14), seed: 7);
        FactorySaveStore.Save("futuro", new SimHost(factory));
        RewriteVersion("futuro", FactorySaveData.CurrentVersion + 1);

        Assert.False(FactorySaveStore.TryLoad(content, "futuro", out _, out var error));
        // Technical version text must not reach the player.
        Assert.Equal("Salvataggio non leggibile.", error);
    }

    [Fact]
    public void TryLoadReportsTheErrorMessage()
    {
        var content = FactoryContent.Load(ContentPath);
        var factory = NewGame.Sandbox(content, new GridPosition(9, 14), seed: 7);
        FactorySaveStore.Save("vecchio2", new SimHost(factory));
        RewriteVersion("vecchio2", FactorySaveData.MinReadableVersion - 1);

        Assert.False(FactorySaveStore.TryLoad(content, "vecchio2", out var host, out var error));
        Assert.Null(host);
        Assert.Equal("Salvataggio di una versione precedente: non compatibile.", error);
    }

    [Fact]
    public void CorruptSaveReportsUnreadableMessage()
    {
        Directory.CreateDirectory(FactorySaveStore.SavesDirectory);
        File.WriteAllText(FactorySaveStore.SlotPath("rotto"), "{ not json");

        Assert.False(FactorySaveStore.TryLoad(LoadContent(), "rotto", out var host, out var error));
        Assert.Null(host);
        Assert.Equal("Salvataggio non leggibile.", error);
    }

    [Fact]
    public void MissingSaveHasNoErrorMessage()
    {
        Assert.False(FactorySaveStore.TryLoad(LoadContent(), "inesistente", out _, out var error));
        Assert.Null(error);
    }

    /// <summary>Researches the Core tier's structure and puts its build cost in the Core.</summary>
    private static void PrepareTier(Factory factory, int tier)
    {
        var definition = factory.Content.CoreTiers[tier];
        factory.Research.ForceUnlock(definition.Id);
        foreach (var entry in definition.BuildCost)
        {
            factory.Wallet.AddMaterial(entry.ItemId, entry.Amount);
        }
    }

    private static void NoAutoSell(Factory factory)
    {
        foreach (var item in factory.State.Exchange.Items)
        {
            item.AutoSell = false;
        }
    }

    private static void Apply(SimHost host, SimCommand command)
    {
        Refusal? refusal = null;
        void OnRejected(SimCommand c, Refusal r) => refusal = r;
        host.CommandRejected += OnRejected;
        host.Submit(command);
        host.AdvanceTicks(1);
        host.CommandRejected -= OnRejected;
        Assert.Null(refusal);
    }

    private static object ViewOf(IGameView view) => new
    {
        view.CoreTier,
        Core = string.Join(';', view.CoreTiles.OrderBy(t => t.Y).ThenBy(t => t.X)),
        Capacity = view.CoreCapacity("iron-ore"),
        Storages = string.Join(';', view.Storages.Select(s =>
            $"{s.Id}:{s.Kind}:{s.Origin}:{s.IsLinked}:{string.Join(',', s.ContentsInOrder())}")),
        Market = string.Join(';', view.MarketItems.Select(i =>
            $"{i.ItemId}:{i.PriceMilli}:{i.Phase}:{i.AutoSell}:{i.AutoSellWhenFull}:{string.Join(',', i.HistoryMilli)}"))
    };

    [Fact]
    public void RoundTrip_FoundationWithStoragesAndAutoSell()
    {
        var content = LoadContent();
        var origin = new GridPosition(60, 60);
        var factory = NewGame.Sandbox(
            content, origin, mapWidth: 120, mapHeight: 120, seed: 42, rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        factory.Research.ForceUnlock("container");
        factory.Research.ForceUnlock("vault");
        NoAutoSell(factory);
        PrepareTier(factory, 1);
        var host = new SimHost(factory);
        Apply(host, new PlaceBuilding(0, BuildingKind.CoreFoundation, origin, Direction.East));
        Assert.Equal(1, factory.CoreTier);
        // Linked vault on the foundation's east side, standalone container far away with 120 copper ore.
        Assert.True(factory.PlaceInstant(new BuildSpec("vault", BuildingKind.Vault, new GridPosition(64, 60), Direction.East), charge: false));
        Assert.True(factory.PlaceInstant(new BuildSpec("container", BuildingKind.Container, new GridPosition(80, 60), Direction.East), charge: false));
        var container = factory.Storages.Single(s => s.Kind == BuildingKind.Container);
        for (var i = 0; i < 120; i++)
        {
            Assert.True(container.TryAccept("copper-ore"));
        }

        var ore = factory.State.Exchange.Find("iron-ore")!;
        ore.AutoSell = true;
        ore.AutoSellWhenFull = true;
        host.AdvanceTicks(1);
        ore.AutoSellCarry = 30;
        Assert.True(factory.Storages.Single(s => s.Kind == BuildingKind.Vault).IsLinked);
        Assert.False(container.IsLinked);

        FactorySaveStore.Save("v15-foundation", host);
        Assert.True(FactorySaveStore.TryLoad(content, "v15-foundation", out var loaded, out var error), error);

        Assert.Equal(host.Checksum(), loaded!.Checksum());
        Assert.Equal(ViewOf(host.State), ViewOf(loaded.State));
        Assert.Equal(1, loaded.State.CoreTier);
        Assert.Equal(5000, loaded.State.CoreCapacity("iron-ore"));
        Assert.Equal([("copper-ore", 120)], loaded.State.Storages.Single(s => !s.IsLinked).ContentsInOrder());
        var back = ((Factory)loaded.State).State.Exchange.Find("iron-ore")!;
        Assert.Equal((true, true, 30), (back.AutoSell, back.AutoSellWhenFull, back.AutoSellCarry));

        host.AdvanceTicks(600);
        loaded.AdvanceTicks(600);
        Assert.Equal(host.Checksum(), loaded.Checksum());
    }

    [Fact]
    public void RoundTrip_DuringCoreUpgradeSite()
    {
        var content = LoadContent();
        var origin = new GridPosition(60, 60);
        var factory = NewGame.Sandbox(content, origin, mapWidth: 120, mapHeight: 120, seed: 42, rules: new GameRules());
        NoAutoSell(factory);
        factory.State.SetCore(origin, 1);
        PrepareTier(factory, 2);
        var cost = content.CoreTiers[2].BuildCost;
        var before = cost.Select(c => factory.Wallet.MaterialCount(c.ItemId)).ToList();
        var host = new SimHost(factory);
        var nucleus = factory.CoreUpgradeSpec()!;
        Apply(host, new PlaceBuilding(0, BuildingKind.CoreNucleus, nucleus.Origin, Direction.East));
        for (var i = 0; i < 100_000 && !(factory.Sites.Count > 0 && factory.Sites[0].Progress >= 0.4f); i++)
        {
            host.AdvanceTicks(1);
        }

        var site = Assert.Single(factory.Sites);
        Assert.InRange(site.Progress, 0.4f, 0.6f);
        Assert.Equal(1, factory.CoreTier);

        FactorySaveStore.Save("v15-nucleus", host);
        Assert.True(FactorySaveStore.TryLoad(content, "v15-nucleus", out var loaded, out var error), error);
        Assert.Equal(host.Checksum(), loaded!.Checksum());

        var upgrades = 0;
        var tier = loaded.State.CoreTier;
        for (var i = 0; i < 100_000 && loaded.State.Sites.Count > 0; i++)
        {
            loaded.AdvanceTicks(1);
            if (loaded.State.CoreTier != tier)
            {
                upgrades++;
                tier = loaded.State.CoreTier;
            }
        }

        loaded.AdvanceTicks(600);
        Assert.Equal(2, loaded.State.CoreTier);
        Assert.Equal(1, upgrades);
        Assert.Empty(loaded.State.Sites);
        var after = (Factory)loaded.State;
        Assert.Equal(
            before.Select((count, i) => count - cost[i].Amount),
            cost.Select(c => after.Wallet.MaterialCount(c.ItemId)));
    }

    private static FactoryContent LoadContent() => FactoryContent.Load(ContentPath);

    private static void RewriteVersion(string slotId, int version)
    {
        var path = FactorySaveStore.SlotPath(slotId);
        var json = File.ReadAllText(path);
        var current = $"\"version\": {FactorySaveData.CurrentVersion}";
        Assert.Contains(current, json);
        File.WriteAllText(path, json.Replace(current, $"\"version\": {version}"));
    }
}
