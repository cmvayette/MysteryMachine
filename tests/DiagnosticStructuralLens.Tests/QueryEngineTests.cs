using System.Diagnostics;
using DiagnosticStructuralLens.Core;
using DiagnosticStructuralLens.Graph;
using Xunit;

namespace DiagnosticStructuralLens.Tests;

/// <summary>
/// Phase 2 Verification: Query Engine
/// </summary>
public class QueryEngineTests
{
    #region Traversal (DoD #1, #2)

    [Fact]
    public void Traverse_Outbound_ReturnsDependencies()
    {
        // A -> B -> C
        var graph = CreateChainGraph("A", "B", "C");
        var engine = new GraphQueryEngine(graph);

        // Act: Traverse from A outbound (max depth 3)
        var result = engine.Traverse("A", TraversalDirection.Outbound, 3);

        // Assert
        Assert.Equal("A", result.StartNode.Id);
        Assert.Equal(2, result.TotalNodesFound); // B and C
        Assert.Equal(2, result.Levels.Count);    // Level 1 (B), Level 2 (C)

        var level1 = result.Levels.First(l => l.Depth == 1);
        Assert.Single(level1.Hits);
        Assert.Equal("B", level1.Hits[0].Node.Id);
        Assert.Equal("A", level1.Hits[0].FromNode.Id);

        var level2 = result.Levels.First(l => l.Depth == 2);
        Assert.Single(level2.Hits);
        Assert.Equal("C", level2.Hits[0].Node.Id);
        Assert.Equal("B", level2.Hits[0].FromNode.Id);
    }

    [Fact]
    public void Traverse_Inbound_ReturnsDependents()
    {
        // A -> B -> C
        // We traverse INBOUND starting from C. Should find B then A.
        var graph = CreateChainGraph("A", "B", "C");
        var engine = new GraphQueryEngine(graph);

        // Act
        var result = engine.Traverse("C", TraversalDirection.Inbound, 3);

        // Assert
        Assert.Equal("C", result.StartNode.Id);
        Assert.Equal(2, result.TotalNodesFound); // B and A

        var level1 = result.Levels.First(l => l.Depth == 1);
        Assert.Equal("B", level1.Hits[0].Node.Id); // C is called by B

        var level2 = result.Levels.First(l => l.Depth == 2);
        Assert.Equal("A", level2.Hits[0].Node.Id); // B is called by A
    }

    #endregion

    #region Cycle Detection (DoD #3)

    [Fact]
    public void FindCycles_DetectsKnownCycle()
    {
        // A -> B -> C -> A
        var graph = CreateCycleGraph("A", "B", "C"); // C links back to A
        var engine = new GraphQueryEngine(graph);

        // Act
        var cycles = engine.FindCycles();

        // Assert
        Assert.NotEmpty(cycles);
        var cycle = cycles.First();
        Assert.Equal(3, cycle.Nodes.Count);
        // Cycle detection order might vary depending on DFS path, but should contain A, B, C
        Assert.Contains(cycle.Nodes, n => n.Id == "A");
        Assert.Contains(cycle.Nodes, n => n.Id == "B");
        Assert.Contains(cycle.Nodes, n => n.Id == "C");
    }

    [Fact]
    public void FindCycles_ReturnsEmptyForAcyclic()
    {
        // A -> B -> C
        var graph = CreateChainGraph("A", "B", "C");
        var engine = new GraphQueryEngine(graph);

        // Act
        var cycles = engine.FindCycles();

        // Assert
        Assert.Empty(cycles);
    }

    #endregion

    #region Centrality (DoD #4, #5)

    [Fact]
    public void CalculateCentrality_ReturnsCorrectDegrees()
    {
        // Hub -> Leaf1
        // Hub -> Leaf2
        // Hub -> Leaf3
        var graph = CreateHubGraph("Hub", "Leaf1", "Leaf2", "Leaf3");
        var engine = new GraphQueryEngine(graph);

        // Act
        var metrics = engine.CalculateCentrality();

        // Assert
        var hubMetric = metrics.First(m => m.Node.Id == "Hub");
        Assert.Equal(0, hubMetric.InDegree);
        Assert.Equal(3, hubMetric.OutDegree); // 3 outbound edges

        var leafMetric = metrics.First(m => m.Node.Id == "Leaf1");
        Assert.Equal(1, leafMetric.InDegree);  // 1 inbound from Hub
        Assert.Equal(0, leafMetric.OutDegree);
    }

    [Fact]
    public void CalculateCentrality_IdentifiesHubs()
    {
        // A -> B, A -> C, B -> D
        // Hubs sorted by total degree:
        // A (2 out, 0 in = 2)
        // B (1 out, 1 in = 2)
        // C (1 in = 1)
        // D (1 in = 1)
        // Actually A and B are tied.
        
        var graph = new KnowledgeGraph { Id = "test" };
        var a = CreateNode("A"); var b = CreateNode("B"); 
        var c = CreateNode("C"); var d = CreateNode("D");
        graph.AddNode(a); graph.AddNode(b); graph.AddNode(c); graph.AddNode(d);
        graph.AddEdge(CreateEdge(a, b));
        graph.AddEdge(CreateEdge(a, c));
        graph.AddEdge(CreateEdge(b, d));
        graph.BuildIndexes();
        graph.PopulateNavigation();

        var engine = new GraphQueryEngine(graph);

        // Act
        var topNodes = engine.CalculateCentrality()
            .OrderByDescending(m => m.TotalDegree)
            .Take(2)
            .ToList();

        // Assert
        Assert.Equal(2, topNodes.Count);
        Assert.Contains(topNodes, m => m.Node.Id == "A");
        Assert.Contains(topNodes, m => m.Node.Id == "B");
    }

    #endregion

    #region Orphans (DoD #6)

    [Fact]
    public void FindOrphans_ReturnsZeroInbound()
    {
        // A -> B
        // C (orphan)
        var graph = CreateChainGraph("A", "B");
        var c = CreateNode("C");
        graph.AddNode(c);
        graph.BuildIndexes(); // Rebuild to include C in index if needed (though list is direct)
        // Actually indexes are internal helpers, QueryEngine usually uses .Nodes property.
        // But Navigation properties (InboundEdges) MUST be populated.
        graph.PopulateNavigation(); 
        
        var engine = new GraphQueryEngine(graph);

        // Act
        var orphans = engine.FindOrphans();

        // Assert
        // A has 0 inbound (it's a root).
        // B has 1 inbound (from A).
        // C has 0 inbound (orphan).
        // So orphans should be A and C.
        
        Assert.Equal(2, orphans.Count);
        Assert.Contains(orphans, n => n.Id == "C");
        Assert.Contains(orphans, n => n.Id == "A");
    }

    #endregion

    #region Performance (DoD #7)

    [Fact]
    public void Performance_LargeGraph_CompletesFast()
    {
        // Create 1000 nodes connected in a line (worst case depth)
        // Or 1000 nodes star pattern.
        // Let's do 1000 nodes chain.
        var graph = new KnowledgeGraph { Id = "perf" };
        GraphNode? prev = null;
        for (int i = 0; i < 1000; i++)
        {
            var node = CreateNode($"N{i}");
            graph.AddNode(node);
            if (prev != null)
            {
                graph.AddEdge(CreateEdge(prev, node));
            }
            prev = node;
        }
        graph.BuildIndexes();
        graph.PopulateNavigation();

        var engine = new GraphQueryEngine(graph);
        var sw = Stopwatch.StartNew();

        // Act: Traverse from start to max depth 100
        engine.Traverse("N0", TraversalDirection.Outbound, 100);
        
        // Also run Centrality (O(N))
        engine.CalculateCentrality();

        sw.Stop();

        // Assert (< 100ms)
        // In CI environments this can be flaky, so be generous but strict enough for logic validation.
        // 1000 nodes is small for O(N). Should be < 10ms.
        Assert.True(sw.ElapsedMilliseconds < 250, $"Performance test took {sw.ElapsedMilliseconds}ms (limit 250ms)");
    }

    #endregion

    #region Helpers

    private KnowledgeGraph CreateChainGraph(params string[] ids)
    {
        var graph = new KnowledgeGraph { Id = "chain" };
        GraphNode? prev = null;
        foreach (var id in ids)
        {
            var node = CreateNode(id);
            graph.AddNode(node);
            if (prev != null)
            {
                graph.AddEdge(CreateEdge(prev, node));
            }
            prev = node;
        }
        graph.BuildIndexes(); // Important for O(1) Lookups if used
        graph.PopulateNavigation(); // Important for Traversals
        return graph;
    }

    private KnowledgeGraph CreateCycleGraph(params string[] ids)
    {
        var graph = CreateChainGraph(ids);
        // Link last back to first
        var first = graph.GetNodeById(ids[0]);
        var last = graph.GetNodeById(ids[^1]);
        if (first != null && last != null)
        {
            graph.AddEdge(CreateEdge(last, first));
        }
        graph.BuildIndexes();
        graph.PopulateNavigation();
        return graph;
    }

    private KnowledgeGraph CreateHubGraph(string hubId, params string[] leaves)
    {
        var graph = new KnowledgeGraph { Id = "hub" };
        var hub = CreateNode(hubId);
        graph.AddNode(hub);
        foreach (var leafId in leaves)
        {
            var leaf = CreateNode(leafId);
            graph.AddNode(leaf);
            graph.AddEdge(CreateEdge(hub, leaf));
        }
        graph.BuildIndexes();
        graph.PopulateNavigation();
        return graph;
    }

    private GraphNode CreateNode(string id) => new GraphNode
    {
        Id = id,
        Name = id,
        Type = NodeType.Class
    };

    private GraphEdge CreateEdge(GraphNode source, GraphNode target) => new GraphEdge
    {
        Id = $"{source.Id}->{target.Id}",
        SourceId = source.Id,
        TargetId = target.Id,
        Type = EdgeType.DependsOn
    };

    #endregion
}
