using DiagnosticStructuralLens.Core;
using DiagnosticStructuralLens.Graph;
using Xunit;

namespace DiagnosticStructuralLens.Tests;

/// <summary>
/// Unit tests for Phase 1: Graph Foundation
/// Tests validate all Definition of Done criteria from the roadmap.
/// </summary>
public class KnowledgeGraphTests
{
    #region Data Model Tests
    
    [Fact]
    public void GraphNode_CanBeCreated()
    {
        var node = new GraphNode
        {
            Id = "node-1",
            Name = "UserService",
            Type = NodeType.Class,
            Properties = new Dictionary<string, object>
            {
                ["Namespace"] = "Company.Services",
                ["IsPublic"] = true
            }
        };

        Assert.Equal("node-1", node.Id);
        Assert.Equal("UserService", node.Name);
        Assert.Equal(NodeType.Class, node.Type);
        Assert.Equal("Company.Services", node.Namespace);
        Assert.True(node.IsPublic);
    }

    [Fact]
    public void GraphEdge_CanBeCreated()
    {
        var edge = new GraphEdge
        {
            Id = "edge-1",
            SourceId = "node-1",
            TargetId = "node-2",
            Type = EdgeType.DependsOn,
            Properties = new Dictionary<string, object>
            {
                ["Confidence"] = 0.95
            }
        };

        Assert.Equal("edge-1", edge.Id);
        Assert.Equal(EdgeType.DependsOn, edge.Type);
        Assert.Equal(0.95, edge.Confidence);
    }

    #endregion

    #region GraphBuilder Tests (DoD #1, #7)

    [Fact]
    public void GraphBuilder_EmptySnapshot_ReturnsEmptyGraph()
    {
        // Arrange
        var snapshot = CreateEmptySnapshot();
        var builder = new GraphBuilder();

        // Act
        var graph = builder.Build(snapshot);

        // Assert (DoD #7: empty snapshot scenario)
        Assert.NotNull(graph);
        Assert.Equal(0, graph.NodeCount);
        Assert.Equal(0, graph.EdgeCount);
        Assert.Equal("test-repo", graph.Repository);
    }

    [Fact]
    public void GraphBuilder_SingleProject_BuildsCorrectly()
    {
        // Arrange (DoD #7: single-project scenario)
        var snapshot = new Snapshot
        {
            Id = "snap-1",
            Repository = "MyProject",
            ScannedAt = DateTimeOffset.UtcNow,
            CodeAtoms =
            [
                new CodeAtom { Id = "c1", Name = "UserService", Type = AtomType.Class, Namespace = "MyProject.Services" },
                new CodeAtom { Id = "c2", Name = "IUserService", Type = AtomType.Interface, Namespace = "MyProject.Services" },
                new CodeAtom { Id = "c3", Name = "GetUser", Type = AtomType.Method, Namespace = "MyProject.Services" }
            ],
            Links =
            [
                new AtomLink { Id = "l1", SourceId = "c1", TargetId = "c2", Type = LinkType.Implements }
            ]
        };
        var builder = new GraphBuilder();

        // Act (DoD #1: GraphBuilder.Build returns valid KnowledgeGraph)
        var graph = builder.Build(snapshot);

        // Assert
        Assert.Equal(3, graph.NodeCount);
        Assert.Equal(1, graph.EdgeCount);
    }

    [Fact]
    public void GraphBuilder_MultiProject_BuildsCorrectly()
    {
        // Arrange (DoD #7: multi-project scenario)
        var snapshot = new Snapshot
        {
            Id = "snap-multi",
            Repository = "MultiProject",
            ScannedAt = DateTimeOffset.UtcNow,
            CodeAtoms =
            [
                // Project A
                new CodeAtom { Id = "a1", Name = "ServiceA", Type = AtomType.Class, Namespace = "ProjectA" },
                new CodeAtom { Id = "a2", Name = "ModelA", Type = AtomType.Record, Namespace = "ProjectA.Models" },
                // Project B
                new CodeAtom { Id = "b1", Name = "ServiceB", Type = AtomType.Class, Namespace = "ProjectB" },
                new CodeAtom { Id = "b2", Name = "IServiceB", Type = AtomType.Interface, Namespace = "ProjectB" }
            ],
            SqlAtoms =
            [
                new SqlAtom { Id = "sql1", Name = "Users", Type = SqlAtomType.Table },
                new SqlAtom { Id = "sql2", Name = "GetUsers", Type = SqlAtomType.StoredProcedure }
            ],
            Links =
            [
                new AtomLink { Id = "l1", SourceId = "a1", TargetId = "b1", Type = LinkType.References },
                new AtomLink { Id = "l2", SourceId = "b1", TargetId = "b2", Type = LinkType.Implements },
                new AtomLink { Id = "l3", SourceId = "a2", TargetId = "sql1", Type = LinkType.NameMatch }
            ]
        };
        var builder = new GraphBuilder();

        // Act
        var graph = builder.Build(snapshot);

        // Assert
        Assert.Equal(6, graph.NodeCount);  // 4 code + 2 SQL
        Assert.Equal(3, graph.EdgeCount);
    }

    #endregion

    #region Index Tests (DoD #2, #3, #4)

    [Fact]
    public void Graph_NodesById_ReturnsO1Lookup()
    {
        // Arrange (DoD #2: O(1) lookup by ID)
        var snapshot = CreateTestSnapshot();
        var builder = new GraphBuilder();
        var graph = builder.Build(snapshot);

        // Act
        var node = graph.GetNodeById("class-1");

        // Assert
        Assert.NotNull(node);
        Assert.Equal("UserService", node.Name);
        Assert.Equal(NodeType.Class, node.Type);
    }

    [Fact]
    public void Graph_NodesByType_FiltersCorrectly()
    {
        // Arrange (DoD #3: nodes by type, DoD #5: all node types mapped)
        var snapshot = new Snapshot
        {
            Id = "snap-types",
            Repository = "TypeTest",
            ScannedAt = DateTimeOffset.UtcNow,
            CodeAtoms =
            [
                new CodeAtom { Id = "c1", Name = "MyClass", Type = AtomType.Class, Namespace = "Test" },
                new CodeAtom { Id = "c2", Name = "MyClass2", Type = AtomType.Class, Namespace = "Test" },
                new CodeAtom { Id = "i1", Name = "IMyInterface", Type = AtomType.Interface, Namespace = "Test" },
                new CodeAtom { Id = "r1", Name = "MyRecord", Type = AtomType.Record, Namespace = "Test" },
                new CodeAtom { Id = "s1", Name = "MyStruct", Type = AtomType.Struct, Namespace = "Test" },
                new CodeAtom { Id = "e1", Name = "MyEnum", Type = AtomType.Enum, Namespace = "Test" },
                new CodeAtom { Id = "m1", Name = "GetData", Type = AtomType.Method, Namespace = "Test" },
                new CodeAtom { Id = "p1", Name = "Name", Type = AtomType.Property, Namespace = "Test" }
            ]
        };
        var builder = new GraphBuilder();
        var graph = builder.Build(snapshot);

        // Act & Assert (DoD #5: all node types mapped)
        Assert.Equal(2, graph.GetNodesByType(NodeType.Class).Count);
        Assert.Single(graph.GetNodesByType(NodeType.Interface));
        Assert.Single(graph.GetNodesByType(NodeType.Record));
        Assert.Single(graph.GetNodesByType(NodeType.Struct));
        Assert.Single(graph.GetNodesByType(NodeType.Enum));
        Assert.Single(graph.GetNodesByType(NodeType.Method));
        Assert.Single(graph.GetNodesByType(NodeType.Property));
    }

    [Fact]
    public void Graph_EdgesBySource_ReturnsOutbound()
    {
        // Arrange (DoD #4: outbound edges by source)
        var snapshot = CreateTestSnapshot();
        var builder = new GraphBuilder();
        var graph = builder.Build(snapshot);

        // Act
        var edges = graph.GetEdgesBySource("class-1");

        // Assert
        Assert.Single(edges);
        Assert.Equal("interface-1", edges[0].TargetId);
    }

    #endregion

    #region Edge Type Mapping Tests (DoD #6)

    [Fact]
    public void GraphBuilder_MapsAllEdgeTypes()
    {
        // Arrange (DoD #6: all edge types mapped)
        var snapshot = new Snapshot
        {
            Id = "snap-edges",
            Repository = "EdgeTest",
            ScannedAt = DateTimeOffset.UtcNow,
            CodeAtoms =
            [
                new CodeAtom { Id = "n1", Name = "N1", Type = AtomType.Class, Namespace = "Test" },
                new CodeAtom { Id = "n2", Name = "N2", Type = AtomType.Class, Namespace = "Test" }
            ],
            Links =
            [
                new AtomLink { Id = "e1", SourceId = "n1", TargetId = "n2", Type = LinkType.Contains },
                new AtomLink { Id = "e2", SourceId = "n1", TargetId = "n2", Type = LinkType.References },
                new AtomLink { Id = "e3", SourceId = "n1", TargetId = "n2", Type = LinkType.Implements },
                new AtomLink { Id = "e4", SourceId = "n1", TargetId = "n2", Type = LinkType.Inherits },
                new AtomLink { Id = "e5", SourceId = "n1", TargetId = "n2", Type = LinkType.Calls }
            ]
        };
        var builder = new GraphBuilder();

        // Act
        var graph = builder.Build(snapshot);

        // Assert all edge types are properly mapped
        var edges = graph.Edges.ToList();
        Assert.Contains(edges, e => e.Type == EdgeType.Contains);
        Assert.Contains(edges, e => e.Type == EdgeType.DependsOn);  // References maps to DependsOn
        Assert.Contains(edges, e => e.Type == EdgeType.Implements);
        Assert.Contains(edges, e => e.Type == EdgeType.Inherits);
        Assert.Contains(edges, e => e.Type == EdgeType.Calls);
    }

    #endregion

    #region Navigation Tests

    [Fact]
    public void Graph_NavigationProperties_ArePopulated()
    {
        // Arrange
        var snapshot = CreateTestSnapshot();
        var builder = new GraphBuilder();
        var graph = builder.Build(snapshot);

        // Act
        var classNode = graph.GetNodeById("class-1");
        var interfaceNode = graph.GetNodeById("interface-1");

        // Assert - edge navigation
        var edge = graph.Edges.First();
        Assert.NotNull(edge.Source);
        Assert.NotNull(edge.Target);
        Assert.Equal("UserService", edge.Source.Name);
        Assert.Equal("IUserService", edge.Target.Name);

        // Assert - node navigation
        Assert.NotNull(classNode);
        Assert.Single(classNode.OutboundEdges);
        Assert.Empty(classNode.InboundEdges);
        
        Assert.NotNull(interfaceNode);
        Assert.Empty(interfaceNode.OutboundEdges);
        Assert.Single(interfaceNode.InboundEdges);
    }

    #endregion

    #region Helper Methods

    private static Snapshot CreateEmptySnapshot() => new()
    {
        Id = "empty",
        Repository = "test-repo",
        ScannedAt = DateTimeOffset.UtcNow
    };

    private static Snapshot CreateTestSnapshot() => new()
    {
        Id = "test-snap",
        Repository = "TestRepo",
        ScannedAt = DateTimeOffset.UtcNow,
        Branch = "main",
        CommitSha = "abc1234",
        CodeAtoms =
        [
            new CodeAtom 
            { 
                Id = "class-1", 
                Name = "UserService", 
                Type = AtomType.Class, 
                Namespace = "TestRepo.Services",
                IsPublic = true
            },
            new CodeAtom 
            { 
                Id = "interface-1", 
                Name = "IUserService", 
                Type = AtomType.Interface, 
                Namespace = "TestRepo.Services",
                IsPublic = true
            }
        ],
        Links =
        [
            new AtomLink 
            { 
                Id = "link-1", 
                SourceId = "class-1", 
                TargetId = "interface-1", 
                Type = LinkType.Implements,
                Confidence = 1.0
            }
        ]
    };

    #endregion
}
