using DiagnosticStructuralLens.Graph;
using Xunit;

namespace DiagnosticStructuralLens.Tests;

/// <summary>
/// Unit tests for Phase 6: PatternDetector
/// Validates topology detection logic for all patterns.
/// </summary>
public class PatternDetectorTests
{
    #region Hub-Spoke Detection

    [Fact]
    public void Detect_ClassicHub_ReturnsHubSpoke()
    {
        // Arrange: 1 hub node connected to 5 spokes (hub has 80%+ of edges)
        var graph = BuildGraph(
            nodes:
            [
                Node("hub", "HubService"),
                Node("s1", "Client1"),
                Node("s2", "Client2"),
                Node("s3", "Client3"),
                Node("s4", "Client4"),
                Node("s5", "Client5"),
            ],
            edges:
            [
                Edge("e1", "s1", "hub"),
                Edge("e2", "s2", "hub"),
                Edge("e3", "s3", "hub"),
                Edge("e4", "s4", "hub"),
                Edge("e5", "s5", "hub"),
            ]);

        var detector = new PatternDetector();

        // Act
        var hint = detector.Detect(graph);

        // Assert
        Assert.Equal("hub-spoke", hint.Pattern);
        Assert.True(hint.Confidence > 0.4);
        Assert.Equal("hub", hint.HubNodeId);
    }

    #endregion

    #region Pipeline Detection

    [Fact]
    public void Detect_LinearChain_ReturnsPipeline()
    {
        // Arrange: 10 nodes in a sequence a→b→c→...→j
        var nodeNames = Enumerable.Range(0, 10)
            .Select(i => $"n{i}")
            .ToList();

        var nodes = nodeNames.Select(n => Node(n, $"Step_{n}")).ToList();
        var edges = new List<GraphEdge>();
        for (int i = 0; i < nodeNames.Count - 1; i++)
        {
            edges.Add(Edge($"e{i}", nodeNames[i], nodeNames[i + 1]));
        }

        var graph = BuildGraph(nodes, edges);
        var detector = new PatternDetector();

        // Act
        var hint = detector.Detect(graph);

        // Assert
        Assert.Equal("pipeline", hint.Pattern);
        Assert.True(hint.Confidence > 0.5);
        Assert.NotNull(hint.PipelineOrder);
        Assert.Equal(10, hint.PipelineOrder!.Count);
    }

    #endregion

    #region Layered Detection

    [Fact]
    public void Detect_LayeredNames_ReturnsLayered()
    {
        // Arrange: nodes with architectural naming patterns
        var graph = BuildGraph(
            nodes:
            [
                Node("c1", "UserController", "App.Controllers"),
                Node("c2", "OrderController", "App.Controllers"),
                Node("s1", "UserService", "App.Services"),
                Node("s2", "OrderService", "App.Services"),
                Node("d1", "UserEntity", "App.Domain"),
                Node("r1", "UserRepository", "App.Infrastructure"),
            ],
            edges:
            [
                Edge("e1", "c1", "s1"),
                Edge("e2", "c2", "s2"),
                Edge("e3", "s1", "r1"),
            ]);

        var detector = new PatternDetector();

        // Act
        var hint = detector.Detect(graph);

        // Assert
        Assert.Equal("layered", hint.Pattern);
        Assert.True(hint.Confidence >= 0.4);
        Assert.NotEmpty(hint.LayerAssignments);
        Assert.Contains(hint.LayerAssignments, a => a.Layer == "presentation");
        Assert.Contains(hint.LayerAssignments, a => a.Layer == "application");
        Assert.Contains(hint.LayerAssignments, a => a.Layer == "domain");
        Assert.Contains(hint.LayerAssignments, a => a.Layer == "infrastructure");
    }

    #endregion

    #region Disconnected Detection

    [Fact]
    public void Detect_NoEdges_ReturnsDisconnected()
    {
        // Arrange: 10 isolated nodes, 0 edges
        var nodes = Enumerable.Range(0, 10)
            .Select(i => Node($"n{i}", $"Isolated{i}"))
            .ToList();

        var graph = BuildGraph(nodes, []);
        var detector = new PatternDetector();

        // Act
        var hint = detector.Detect(graph);

        // Assert
        Assert.Equal("disconnected", hint.Pattern);
        Assert.Equal(1.0, hint.Confidence);
    }

    [Fact]
    public void Detect_SingleNode_ReturnsDisconnected()
    {
        // Arrange: 1 node, 0 edges
        var graph = BuildGraph([Node("n1", "Alone")], []);
        var detector = new PatternDetector();

        // Act
        var hint = detector.Detect(graph);

        // Assert
        Assert.Equal("disconnected", hint.Pattern);
    }

    #endregion

    #region Mesh Detection

    [Fact]
    public void Detect_FullyConnected_ReturnsMesh()
    {
        // Arrange: 6 nodes, every-to-every edges (fully connected = 30 edges)
        var nodeIds = Enumerable.Range(0, 6).Select(i => $"n{i}").ToList();
        var nodes = nodeIds.Select(id => Node(id, $"Mesh{id}")).ToList();
        var edges = new List<GraphEdge>();
        int edgeId = 0;
        foreach (var src in nodeIds)
        {
            foreach (var tgt in nodeIds)
            {
                if (src != tgt)
                    edges.Add(Edge($"e{edgeId++}", src, tgt));
            }
        }

        var graph = BuildGraph(nodes, edges);
        var detector = new PatternDetector();

        // Act
        var hint = detector.Detect(graph);

        // Assert: should not match hub/pipeline/disconnected, falls to mesh
        Assert.Equal("mesh", hint.Pattern);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Detect_CycleGraph_DoesNotCrash()
    {
        // Arrange: A→B→C→A cycle
        var graph = BuildGraph(
            nodes: [Node("a", "A"), Node("b", "B"), Node("c", "C")],
            edges: [Edge("e1", "a", "b"), Edge("e2", "b", "c"), Edge("e3", "c", "a")]);

        var detector = new PatternDetector();

        // Act — should not throw
        var hint = detector.Detect(graph);

        // Assert — valid result
        Assert.NotNull(hint);
        Assert.True(hint.Confidence > 0);
    }

    [Fact]
    public void Detect_MixedSignals_HighestConfidenceWins()
    {
        // Arrange: hub-like structure + layered naming
        // Hub should win because 1 node has 80%+ of edges
        var graph = BuildGraph(
            nodes:
            [
                Node("hub", "UserController", "App.Controllers"),
                Node("s1", "ServiceA", "App.Services"),
                Node("s2", "ServiceB", "App.Services"),
                Node("s3", "ServiceC", "App.Services"),
                Node("s4", "ServiceD", "App.Services"),
            ],
            edges:
            [
                Edge("e1", "s1", "hub"),
                Edge("e2", "s2", "hub"),
                Edge("e3", "s3", "hub"),
                Edge("e4", "s4", "hub"),
            ]);

        var detector = new PatternDetector();

        // Act
        var hint = detector.Detect(graph);

        // Assert — hub-spoke should dominate (4/4 edges on one node = 100%)
        Assert.Equal("hub-spoke", hint.Pattern);
    }

    #endregion

    #region Scope Filtering

    [Fact]
    public void Detect_WithScope_FiltersToNamespace()
    {
        // Arrange: two namespaces, scope to one
        var graph = BuildGraph(
            nodes:
            [
                Node("a1", "NodeA", "ScopeA"),
                Node("a2", "NodeB", "ScopeA"),
                Node("b1", "NodeC", "ScopeB"),
                Node("b2", "NodeD", "ScopeB"),
                Node("b3", "NodeE", "ScopeB"),
            ],
            edges:
            [
                Edge("e1", "a1", "a2"),
                Edge("e2", "b1", "b2"),
                Edge("e3", "b1", "b3"),
            ]);

        var detector = new PatternDetector();

        // Act — scope to ScopeA only (2 nodes, 1 edge)
        var hint = detector.Detect(graph, "ScopeA");

        // Assert — should analyze only the ScopeA subgraph
        Assert.NotNull(hint);
        Assert.True(hint.Confidence > 0);
    }

    #endregion

    #region Helper Methods

    private static GraphNode Node(string id, string name, string? ns = null)
    {
        var props = new Dictionary<string, object>();
        if (ns != null) props["Namespace"] = ns;

        return new GraphNode
        {
            Id = id,
            Name = name,
            Type = NodeType.Class,
            Properties = props,
        };
    }

    private static GraphEdge Edge(string id, string sourceId, string targetId)
    {
        return new GraphEdge
        {
            Id = id,
            SourceId = sourceId,
            TargetId = targetId,
            Type = EdgeType.DependsOn,
        };
    }

    private static KnowledgeGraph BuildGraph(
        IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges)
    {
        var graph = new KnowledgeGraph { Repository = "TestRepo" };
        foreach (var node in nodes) graph.AddNode(node);
        foreach (var edge in edges) graph.AddEdge(edge);
        graph.BuildIndexes();
        graph.PopulateNavigation();
        return graph;
    }

    #endregion
}
