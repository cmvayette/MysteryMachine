using DiagnosticStructuralLens.Core;
using DiagnosticStructuralLens.Graph;
using Xunit;

namespace DiagnosticStructuralLens.Tests;

/// <summary>
/// Phase 4 Verification: Diff Engine
/// </summary>
public class DiffEngineTests
{
    private readonly GraphDiffEngine _diffEngine;

    public DiffEngineTests()
    {
        _diffEngine = new GraphDiffEngine();
    }

    [Fact]
    public void Compare_DetectsAddedNodesAndEdges()
    {
        // Baseline: A
        var baseline = new KnowledgeGraph { Id = "Base" };
        var a = CreateNode(baseline, "A", "NodeA", NodeType.Class);
        baseline.BuildIndexes();

        // Current: A -> B
        var current = new KnowledgeGraph { Id = "Curr" };
        var a2 = CreateNode(current, "A", "NodeA", NodeType.Class);
        var b = CreateNode(current, "B", "NodeB", NodeType.Class);
        AddEdge(current, a2, b, EdgeType.DependsOn);
        current.BuildIndexes();
        current.PopulateNavigation();

        // Act
        var result = _diffEngine.Compare(baseline, current, new List<ArchitectureRule>());

        // Assert Topology
        Assert.Single(result.Topology.AddedNodes);
        Assert.Equal("B", result.Topology.AddedNodes[0].Id);
        
        Assert.Single(result.Topology.AddedEdges);
        Assert.Equal("A->B", result.Topology.AddedEdges[0].Id);
        
        Assert.Empty(result.Topology.RemovedNodes);
    }

    [Fact]
    public void Compare_DetectsRemovedEdges()
    {
        // Baseline: A -> B
        var baseline = new KnowledgeGraph { Id = "Base" };
        var a = CreateNode(baseline, "A", "NodeA", NodeType.Class);
        var b = CreateNode(baseline, "B", "NodeB", NodeType.Class);
        AddEdge(baseline, a, b, EdgeType.DependsOn);
        baseline.BuildIndexes();
        baseline.PopulateNavigation();

        // Current: A, B (No edge)
        var current = new KnowledgeGraph { Id = "Curr" };
        var a2 = CreateNode(current, "A", "NodeA", NodeType.Class);
        var b2 = CreateNode(current, "B", "NodeB", NodeType.Class);
        current.BuildIndexes(); // No edge added

        // Act
        var result = _diffEngine.Compare(baseline, current, new List<ArchitectureRule>());

        // Assert
        Assert.Single(result.Topology.RemovedEdges);
        Assert.Equal("A->B", result.Topology.RemovedEdges[0].Id);
    }

    [Fact]
    public void Compare_IdentifiesNewViolations()
    {
        // Rule: No A->B
        var rule = new ArchitectureRule
        {
            Id = "NO_A_TO_B",
            Name = "No A->B",
            Description = "Test",
            Severity = RuleSeverity.Error,
            Source = new NodeQuery { NamePattern = "NodeA" },
            ForbiddenEdge = EdgeType.DependsOn,
            Target = new NodeQuery { NamePattern = "NodeB" }
        };

        // Baseline: A (Compliant)
        var baseline = new KnowledgeGraph { Id = "Base" };
        var a = CreateNode(baseline, "A", "NodeA", NodeType.Class);
        baseline.BuildIndexes();
        baseline.PopulateNavigation();

        // Current: A -> B (Violation)
        var current = new KnowledgeGraph { Id = "Curr" };
        var a2 = CreateNode(current, "A", "NodeA", NodeType.Class);
        var b = CreateNode(current, "B", "NodeB", NodeType.Class);
        AddEdge(current, a2, b, EdgeType.DependsOn);
        current.BuildIndexes();
        current.PopulateNavigation(); // Required for RuleEngine!

        // Act
        var result = _diffEngine.Compare(baseline, current, new[] { rule });

        // Assert
        Assert.Single(result.NewViolations);
        Assert.Equal("NO_A_TO_B", result.NewViolations[0].Rule.Id);
    }

    [Fact]
    public void Compare_IgnoresExistingViolations()
    {
        // Rule: No A->B
        var rule = new ArchitectureRule
        {
            Id = "R1",
            Name = "No A->B",
            Description = "desc",
            Severity = RuleSeverity.Error,
            Source = new NodeQuery { NamePattern = "NodeA" },
            ForbiddenEdge = EdgeType.DependsOn,
            Target = new NodeQuery { NamePattern = "NodeB" }
        };

        // Baseline: A -> B (Already failing)
        var baseline = new KnowledgeGraph { Id = "Base" };
        var a = CreateNode(baseline, "A", "NodeA", NodeType.Class);
        var b = CreateNode(baseline, "B", "NodeB", NodeType.Class);
        AddEdge(baseline, a, b, EdgeType.DependsOn);
        baseline.BuildIndexes();
        baseline.PopulateNavigation();

        // Current: A -> B (Still failing)
        var current = new KnowledgeGraph { Id = "Curr" };
        var a2 = CreateNode(current, "A", "NodeA", NodeType.Class);
        var b2 = CreateNode(current, "B", "NodeB", NodeType.Class);
        AddEdge(current, a2, b2, EdgeType.DependsOn);
        current.BuildIndexes();
        current.PopulateNavigation();

        // Act
        var result = _diffEngine.Compare(baseline, current, new[] { rule });

        // Assert: NewViolations should be empty because it existed in baseline
        Assert.Empty(result.NewViolations);
    }

    [Fact]
    public void Compare_IdentifiesNewCycles()
    {
        // Baseline: A -> B
        var baseline = new KnowledgeGraph();
        var a = CreateNode(baseline, "A", "A", NodeType.Class);
        var b = CreateNode(baseline, "B", "B", NodeType.Class);
        AddEdge(baseline, a, b, EdgeType.DependsOn);
        baseline.BuildIndexes(); baseline.PopulateNavigation();

        // Current: A -> B -> A (Cycle)
        var current = new KnowledgeGraph();
        var a2 = CreateNode(current, "A", "A", NodeType.Class);
        var b2 = CreateNode(current, "B", "B", NodeType.Class);
        AddEdge(current, a2, b2, EdgeType.DependsOn);
        AddEdge(current, b2, a2, EdgeType.DependsOn); // Back edge
        current.BuildIndexes(); current.PopulateNavigation();

        // Act
        var result = _diffEngine.Compare(baseline, current, new List<ArchitectureRule>());

        // Assert
        Assert.Single(result.NewCycles);
    }

    // Helpers
    private GraphNode CreateNode(KnowledgeGraph g, string id, string name, NodeType type)
    {
        var node = new GraphNode { Id = id, Name = name, Type = type };
        g.AddNode(node);
        // BuildIndexes called by test setup usually, but fine to do here if needed
        return node;
    }

    private void AddEdge(KnowledgeGraph g, GraphNode s, GraphNode t, EdgeType type)
    {
        var edge = new GraphEdge { Id = $"{s.Id}->{t.Id}", SourceId = s.Id, TargetId = t.Id, Type = type };
        g.AddEdge(edge);
    }
}
