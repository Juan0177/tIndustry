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
        // Layout rule for content with several prerequisites (the shipped tree has none).
        static StructureDefinition S(string id, params string[] requires) =>
            new(id, id, "building", UnlockedByDefault: false, Unlock: null, Prerequisites: requires);
        var graph = TechTreeLayout.Build([S("a"), S("b", "a"), S("c", "a", "b")]);

        Assert.Contains(graph.Edges, e => e is { FromId: "b", ToId: "c", Primary: true });
        Assert.Contains(graph.Edges, e => e is { FromId: "a", ToId: "c", Primary: false });
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
        Assert.Contains("miner", related);                // prerequisite
        Assert.Contains("assembler", related);            // unlocked downstream
        Assert.DoesNotContain("conveyor-express", related); // other branch (Logistica)
    }
}
