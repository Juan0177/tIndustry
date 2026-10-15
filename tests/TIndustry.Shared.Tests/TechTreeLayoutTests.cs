using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

public sealed class TechTreeLayoutTests
{
    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static TechTreeLayout.Graph Graph => TechTreeLayout.Build(Content);

    [Fact]
    public void CoreIsTheSingleRootOnTop()
    {
        var root = Assert.Single(Graph.Nodes, n => n.Row == 0);
        Assert.True(root.IsRoot);
        Assert.Equal(Content.Structures.Count + 1, Graph.Nodes.Count);
    }

    [Fact]
    public void EveryStructureHangsBelowItsPrerequisites()
    {
        var byId = Graph.Nodes.ToDictionary(n => n.Structure.Id);
        foreach (var edge in Graph.Edges)
        {
            Assert.True(byId[edge.FromId].Row < byId[edge.ToId].Row, $"{edge.FromId} → {edge.ToId}");
        }

        // One primary (tree) edge per structure; the root has none.
        foreach (var node in Graph.Nodes.Where(n => !n.IsRoot))
        {
            Assert.Single(Graph.Edges, e => e.ToId == node.Structure.Id && e.Primary);
        }
    }

    [Fact]
    public void PrimaryParentIsTheDeepestPrerequisite()
    {
        // miner-advanced needs miner (row 1) and smelter (row 2): it hangs under the smelter.
        Assert.Contains(Graph.Edges, e => e is { FromId: "smelter", ToId: "miner-advanced", Primary: true });
        Assert.Contains(Graph.Edges, e => e is { FromId: "miner", ToId: "miner-advanced", Primary: false });
    }

    [Fact]
    public void NodesNeverOverlap()
    {
        foreach (var row in Graph.Nodes.GroupBy(n => n.Row))
        {
            var xs = row.Select(n => n.X).OrderBy(x => x).ToList();
            for (var i = 1; i < xs.Count; i++)
            {
                Assert.True(xs[i] - xs[i - 1] >= TechTreeLayout.NodeSize, $"row {row.Key} overlaps");
            }
        }
    }

    [Fact]
    public void RelatedIdsFollowPrerequisitesAndUnlocks()
    {
        var related = TechTreeLayout.CollectRelatedIds(Graph, "smelter");
        Assert.Contains("miner", related);          // prerequisite
        Assert.Contains("conveyor-express", related); // unlocked downstream
        Assert.DoesNotContain("splitter", related);   // other branch
    }
}
