using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The shipped research tree is Mindustry-style: one branch per category under CORE
/// (Logistica · Produzione · Energia, plus the CORE upgrades), one parent per block, parent from the same branch.
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
        { "conveyor-armored", "conveyor-fast" },
        { "junction", "conveyor-basic" },
        { "router", "junction" },
        { "sorter", "junction" },
        { "sorter-inverted", "sorter" },
        { "overflow-gate", "router" },
        { "underflow-gate", "overflow-gate" },
        { "conveyor-bridge", "junction" },
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
        // CORE e magazzini
        { "container", "junction" },
        { "vault", "container" },
        { "core-foundation", null },
        { "core-nucleus", "core-foundation" },
    };

    [Theory]
    [MemberData(nameof(ExpectedParent))]
    public void BlockHasItsBranchParent(string id, string? parent)
    {
        var structure = Content.FindStructure(id);
        Assert.NotNull(structure);
        Assert.Equal(parent is null ? [] : [parent], structure.Prerequisites);
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
        Assert.Equal(new[] { "conveyor-basic", "core-foundation", "generator", "miner" }, branches);
        Assert.All(graph.Edges, e => Assert.True(e.Primary)); // no cross-branch (dashed) links
    }
}
