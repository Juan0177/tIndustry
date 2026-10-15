using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The shipped research tree is Mindustry-style: one branch per category under CORE
/// (Logistica · Produzione · Energia), one parent per block, parent from the same branch.
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
        { "junction", "conveyor-basic" },
        { "splitter", "junction" },
        { "sorter", "junction" },
        { "conveyor-bridge", "junction" },
        // Produzione
        { "miner", null },
        { "miner-advanced", "miner" },
        { "smelter", "miner" },
        { "assembler", "smelter" },
        { "extractor", "miner" },
        // Energia
        { "generator", null },
        { "power-node", "generator" },
        { "power-node-t2", "power-node" },
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
        Assert.Equal(new[] { "conveyor-basic", "generator", "miner" }, branches);
        Assert.All(graph.Edges, e => Assert.True(e.Primary)); // no cross-branch (dashed) links
    }
}
