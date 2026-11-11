using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The shipped research tree is Mindustry-style: one branch per category under CORE
/// (Logistica · Produzione · Energia, plus the CORE upgrades), one parent per block, parent from the same branch. The
/// upgrades (kind <c>upgrade</c>) follow their group: the four drone ones hang from CORE, the others from the block they
/// improve; a few of them have extra prerequisites after the parent (dashed in the tree).
/// </summary>
public sealed class ResearchTreeContentTests
{
    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    public static TheoryData<string, string?> ExpectedParent => new()
    {
        // Logistica
        { "conveyor-basic", null },
        { "conveyor-fast", "conveyor-basic" },
        { "conveyor-express", "conveyor-fast" },
        { "conveyor-stack", "conveyor-express" },
        { "conveyor-armored", "conveyor-fast" },
        { "junction", "conveyor-basic" },
        { "router", "junction" },
        { "sorter", "junction" },
        { "sorter-inverted", "sorter" },
        { "overflow-gate", "router" },
        { "underflow-gate", "overflow-gate" },
        { "conveyor-bridge", "junction" },
        { "bridge-long", "conveyor-bridge" },
        // Produzione
        { "miner", null },
        { "miner-advanced", "miner" },
        { "smelter", "miner" },
        { "assembler", "smelter" },
        { "market-screen", "assembler" },
        // Ricerca allargata: machines and the circuit recipe (kind "recipe", nothing to place).
        { "graphite-press", "assembler" },
        { "smelter-advanced", "smelter" },
        { "separator", "smelter-advanced" },
        { "plasticizer", "graphite-press" },
        { "circuit", "assembler" },
        { "impact-drill", "miner-advanced" },
        // Finanza: modules researched in a row after the screen, nothing to place.
        { "finance-forecast", "market-screen" },
        { "finance-percent", "finance-forecast" },
        { "finance-odds", "finance-percent" },
        { "extractor", "miner" },
        // Energia
        { "generator", null },
        { "power-node", "generator" },
        { "power-node-t2", "power-node" },
        { "lamp", "power-node" },
        { "battery", "power-node" },
        { "solar", "power-node" },
        { "solar-large", "solar" },
        { "combustion-generator", "generator" },
        { "battery-large", "battery" },
        // CORE e magazzini
        { "container", "junction" },
        { "vault", "container" },
        { "core-foundation", null },
        { "core-nucleus", "core-foundation" },
        // Potenziamenti (plan 3): nothing to place; the first prerequisite is the parent.
        { "upgrade-drone-build", null },
        { "upgrade-drone-hold", null },
        { "upgrade-drone-range", null },
        { "upgrade-drone-mine", null },
        { "drone-boost", "upgrade-drone-build" },
        { "upgrade-miners-1", "miner-advanced" },
        { "upgrade-miners-2", "upgrade-miners-1" },
        { "upgrade-machines-1", "assembler" },
        { "upgrade-machines-2", "upgrade-machines-1" },
        { "upgrade-storage-1", "container" },
        { "upgrade-storage-2", "upgrade-storage-1" },
    };

    /// <summary>Prerequisites after the parent (dashed links in the tree); every other block has only its parent.</summary>
    private static readonly Dictionary<string, string[]> ExtraPrerequisites = new(StringComparer.Ordinal)
    {
        ["drone-boost"] = ["upgrade-drone-hold", "upgrade-drone-range", "upgrade-drone-mine"],
        ["upgrade-machines-2"] = ["smelter-advanced"],
        ["upgrade-storage-2"] = ["vault"],
    };

    [Theory]
    [MemberData(nameof(ExpectedParent))]
    public void BlockHasItsBranchParent(string id, string? parent)
    {
        var structure = Content.FindStructure(id);
        Assert.NotNull(structure);
        Assert.Equal(
            (parent is null ? Array.Empty<string>() : [parent]).Concat(ExtraPrerequisites.GetValueOrDefault(id, [])),
            structure.Prerequisites);
    }

    [Fact]
    public void EveryBlockIsCovered() =>
        Assert.Equal(
            ExpectedParent.Select(row => (string)row[0]).Order(),
            Content.Structures.Select(s => s.Id).Order());

    [Fact]
    public void CoreHasOneBranchPerCategory()
    {
        var graph = TechTreeLayout.Build(Content);
        var branches = graph.Edges
            .Where(e => e.FromId == TechTreeLayout.RootId)
            .Select(e => e.ToId)
            .Order();
        Assert.Equal(
            new[]
            {
                "conveyor-basic", "core-foundation", "generator", "miner", "upgrade-drone-build", "upgrade-drone-hold",
                "upgrade-drone-mine", "upgrade-drone-range"
            },
            branches);
        // The tree edge goes to the parent (the first prerequisite); only the extra prerequisites are dashed.
        Assert.All(graph.Edges.Where(e => e.FromId != TechTreeLayout.RootId), e => Assert.Equal(
            e.Primary,
            Content.RequireStructure(e.ToId).Prerequisites[0] == e.FromId));
        Assert.Equal(
            ExtraPrerequisites.SelectMany(p => p.Value.Select(from => (from, p.Key))).Order(),
            graph.Edges.Where(e => !e.Primary).Select(e => (e.FromId, e.ToId)).Order());
    }
}
