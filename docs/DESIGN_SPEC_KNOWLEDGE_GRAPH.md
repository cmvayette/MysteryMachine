# Design Specification: Knowledge Graph for Diagnostic Structural Lens

**Version:** 1.0  
**Status:** Draft  
**Author:** Architecture Team  
**Date:** February 2025

---

## Executive Summary

This document specifies the addition of a **Knowledge Graph** capability to Diagnostic Structural Lens. The knowledge graph provides a queryable representation of codebase architecture that enables impact analysis, architectural rule enforcement, and regression risk detection.

This design intentionally **excludes machine learning or vector embeddings**. All capabilities described here are deterministic, explainable, and require no external services or models.

---

## Problem Statement

Development teams need to answer questions like:

- "What breaks if I change this class?"
- "Are there architectural violations in our codebase?"
- "Where is risk concentrated?"
- "What changed between these two versions and what's the impact?"

The current C4 model extraction captures the raw data but doesn't provide an efficient query mechanism for these questions.

---

## Goals

1. **Impact Analysis**: Determine blast radius of any proposed change
2. **Rule Enforcement**: Detect architectural violations automatically
3. **Risk Visibility**: Surface high-risk areas (hubs, cycles, coupling)
4. **Change Tracking**: Compare graph snapshots to identify regression risks
5. **Query Interface**: Enable both programmatic and UI-based exploration

## Non-Goals

1. Semantic/natural language search (future enhancement)
2. AI-powered suggestions (future enhancement)
3. Real-time code monitoring (batch analysis only)
4. Cross-solution analysis (single solution scope)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                      Diagnostic Structural Lens                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────┐      ┌─────────────────────────────────┐  │
│  │                 │      │        Knowledge Graph           │  │
│  │     Roslyn      │─────▶│  ┌─────────┐    ┌───────────┐   │  │
│  │    Analyzer     │      │  │  Nodes  │───▶│  Indexes  │   │  │
│  │   (existing)    │      │  └─────────┘    └───────────┘   │  │
│  │                 │      │  ┌─────────┐    ┌───────────┐   │  │
│  └─────────────────┘      │  │  Edges  │───▶│  Queries  │   │  │
│           │               │  └─────────┘    └───────────┘   │  │
│           │               └─────────────────────────────────┘  │
│           │                            │                        │
│           ▼                            ▼                        │
│  ┌─────────────────┐      ┌─────────────────────────────────┐  │
│  │   C4 Snapshot   │      │         Query Engine            │  │
│  │   (existing)    │      │  ┌─────────────────────────┐    │  │
│  └─────────────────┘      │  │  - Traversal            │    │  │
│                           │  │  - Pattern Matching     │    │  │
│                           │  │  - Rule Evaluation      │    │  │
│                           │  │  - Diff Analysis        │    │  │
│                           │  └─────────────────────────┘    │  │
│                           └─────────────────────────────────┘  │
│                                        │                        │
│                                        ▼                        │
│                           ┌─────────────────────────────────┐  │
│                           │       Analysis Results          │  │
│                           │  - Impact Reports               │  │
│                           │  - Violations                   │  │
│                           │  - Risk Assessments             │  │
│                           └─────────────────────────────────┘  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## Data Model

### Node Types

| Type | Description | Key Properties |
|------|-------------|----------------|
| `Solution` | Root node for the solution | `Name`, `Path`, `GitBranch`, `GitCommit` |
| `Project` | A .csproj within the solution | `Name`, `Path`, `TargetFramework`, `OutputType`, `ProjectType` |
| `Namespace` | Logical grouping of types | `FullName`, `ProjectId` |
| `Class` | Class definition | `Name`, `Namespace`, `Accessibility`, `IsAbstract`, `IsStatic`, `Attributes[]` |
| `Interface` | Interface definition | `Name`, `Namespace`, `Accessibility`, `Attributes[]` |
| `Struct` | Struct definition | `Name`, `Namespace`, `Accessibility`, `Attributes[]` |
| `Record` | Record definition | `Name`, `Namespace`, `Accessibility`, `Attributes[]` |
| `Enum` | Enum definition | `Name`, `Namespace`, `Accessibility`, `Values[]` |
| `Method` | Method/function definition | `Name`, `Signature`, `Accessibility`, `IsStatic`, `IsAsync`, `ReturnType` |
| `Property` | Property definition | `Name`, `Type`, `Accessibility`, `HasGetter`, `HasSetter` |
| `ExternalPackage` | NuGet dependency | `Name`, `Version`, `IsTransitive` |
| `StoredProcedure` | Database stored procedure reference | `Name`, `CalledFrom[]` |

### Edge Types

| Type | From | To | Description |
|------|------|-----|-------------|
| `CONTAINS` | Solution | Project | Solution contains project |
| `CONTAINS` | Project | Namespace | Project contains namespace |
| `CONTAINS` | Namespace | Class/Interface/etc | Namespace contains type |
| `CONTAINS` | Class | Method/Property | Type contains member |
| `REFERENCES` | Project | Project | Project reference |
| `DEPENDS_ON` | Class | Class | Type dependency (field, parameter, return type) |
| `IMPLEMENTS` | Class | Interface | Interface implementation |
| `INHERITS` | Class | Class | Class inheritance |
| `CALLS` | Method | Method | Method invocation |
| `USES_PACKAGE` | Project | ExternalPackage | NuGet dependency |
| `CALLS_PROC` | Method | StoredProcedure | Stored procedure invocation |

### Core Data Structures

```csharp
namespace RegressionRadar.Graph;

/// <summary>
/// Represents the complete knowledge graph for a solution
/// </summary>
public class KnowledgeGraph
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string SolutionPath { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string? GitCommit { get; init; }
    public string? GitBranch { get; init; }
    
    // Core storage
    internal Dictionary<string, GraphNode> NodesById { get; } = new();
    internal List<GraphEdge> AllEdges { get; } = new();
    
    // Indexes (built on load)
    internal Dictionary<NodeType, List<GraphNode>> NodesByType { get; } = new();
    internal Dictionary<string, List<GraphEdge>> EdgesBySource { get; } = new();
    internal Dictionary<string, List<GraphEdge>> EdgesByTarget { get; } = new();
    internal Dictionary<string, List<GraphNode>> NodesByNamespace { get; } = new();
    
    // Public accessors
    public IReadOnlyCollection<GraphNode> Nodes => NodesById.Values;
    public IReadOnlyCollection<GraphEdge> Edges => AllEdges;
}

/// <summary>
/// A single node in the knowledge graph
/// </summary>
public class GraphNode
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required NodeType Type { get; init; }
    
    // Location info
    public string? FilePath { get; init; }
    public int? LineNumber { get; init; }
    
    // Flexible properties based on node type
    public Dictionary<string, object> Properties { get; init; } = new();
    
    // Navigation (populated by graph)
    public IReadOnlyList<GraphEdge> InboundEdges { get; internal set; } = [];
    public IReadOnlyList<GraphEdge> OutboundEdges { get; internal set; } = [];
    
    // Convenience accessors
    public string? Namespace => Properties.GetValueOrDefault("Namespace") as string;
    public string? Accessibility => Properties.GetValueOrDefault("Accessibility") as string;
    public IReadOnlyList<string> Attributes => 
        Properties.GetValueOrDefault("Attributes") as List<string> ?? [];
}

/// <summary>
/// A directed edge between two nodes
/// </summary>
public class GraphEdge
{
    public required string Id { get; init; }
    public required string SourceId { get; init; }
    public required string TargetId { get; init; }
    public required EdgeType Type { get; init; }
    
    // Optional metadata
    public Dictionary<string, object> Properties { get; init; } = new();
    
    // Navigation (populated by graph)
    public GraphNode? Source { get; internal set; }
    public GraphNode? Target { get; internal set; }
}

public enum NodeType
{
    Solution,
    Project,
    Namespace,
    Class,
    Interface,
    Struct,
    Record,
    Enum,
    Delegate,
    Method,
    Property,
    Field,
    Event,
    ExternalPackage,
    StoredProcedure
}

public enum EdgeType
{
    Contains,
    References,
    DependsOn,
    Implements,
    Inherits,
    Calls,
    UsesPackage,
    CallsProc
}
```

---

## Graph Builder

The `GraphBuilder` transforms an existing `ArchitectureSnapshot` (from Roslyn analysis) into a `KnowledgeGraph`.

```csharp
namespace RegressionRadar.Graph;

public class GraphBuilder
{
    /// <summary>
    /// Build a knowledge graph from an architecture snapshot
    /// </summary>
    public KnowledgeGraph Build(ArchitectureSnapshot snapshot)
    {
        var graph = new KnowledgeGraph
        {
            SolutionPath = snapshot.SolutionPath,
            GitCommit = snapshot.GitCommitHash,
            GitBranch = snapshot.GitBranch
        };
        
        // 1. Create solution root node
        AddSolutionNode(graph, snapshot);
        
        // 2. Create project nodes and CONTAINS edges
        foreach (var container in snapshot.Containers)
        {
            AddProjectNode(graph, container, snapshot.Id);
        }
        
        // 3. Create type nodes (classes, interfaces, etc.)
        foreach (var component in snapshot.Components)
        {
            AddTypeNode(graph, component);
        }
        
        // 4. Create member nodes (methods, properties)
        foreach (var element in snapshot.CodeElements)
        {
            AddMemberNode(graph, element);
        }
        
        // 5. Create external package nodes
        foreach (var external in snapshot.SystemContext.ExternalSystems)
        {
            if (external.Type == ExternalSystemType.NuGetPackage)
            {
                AddPackageNode(graph, external);
            }
        }
        
        // 6. Create edges from relationships
        foreach (var relationship in snapshot.Relationships)
        {
            AddEdge(graph, relationship);
        }
        
        // 7. Build indexes
        BuildIndexes(graph);
        
        // 8. Populate navigation properties
        PopulateNavigation(graph);
        
        return graph;
    }
    
    // ... implementation details ...
}
```

---

## Query Engine

The query engine provides methods for common graph operations.

### Interface

```csharp
namespace RegressionRadar.Graph;

public interface IGraphQueryEngine
{
    // === Traversal ===
    
    /// <summary>
    /// Find all nodes reachable from a starting node within maxDepth hops
    /// </summary>
    TraversalResult Traverse(
        string startNodeId, 
        TraversalDirection direction, 
        int maxDepth = 3,
        Func<GraphEdge, bool>? edgeFilter = null);
    
    /// <summary>
    /// Find all paths between two nodes
    /// </summary>
    IReadOnlyList<GraphPath> FindPaths(
        string fromNodeId, 
        string toNodeId, 
        int maxDepth = 5);
    
    /// <summary>
    /// Find cycles in the graph (optionally filtered by node type)
    /// </summary>
    IReadOnlyList<GraphCycle> FindCycles(NodeType? nodeTypeFilter = null);
    
    // === Pattern Matching ===
    
    /// <summary>
    /// Find nodes matching criteria
    /// </summary>
    IReadOnlyList<GraphNode> FindNodes(NodeQuery query);
    
    /// <summary>
    /// Find edges matching criteria
    /// </summary>
    IReadOnlyList<GraphEdge> FindEdges(EdgeQuery query);
    
    /// <summary>
    /// Find patterns in the graph (e.g., "Class that IMPLEMENTS Interface and DEPENDS_ON Repository")
    /// </summary>
    IReadOnlyList<PatternMatch> MatchPattern(GraphPattern pattern);
    
    // === Analysis ===
    
    /// <summary>
    /// Calculate centrality metrics for nodes (identify hubs)
    /// </summary>
    IReadOnlyList<NodeMetric> CalculateCentrality(NodeType? nodeTypeFilter = null);
    
    /// <summary>
    /// Find orphan nodes (nodes with no inbound edges except from tests)
    /// </summary>
    IReadOnlyList<GraphNode> FindOrphans(bool excludeTestReferences = true);
    
    /// <summary>
    /// Find nodes that violate a rule
    /// </summary>
    IReadOnlyList<RuleViolation> EvaluateRule(ArchitectureRule rule);
    
    // === Diff ===
    
    /// <summary>
    /// Compare two graphs and return differences
    /// </summary>
    GraphDiff Compare(KnowledgeGraph baseline, KnowledgeGraph current);
}

public enum TraversalDirection
{
    Outbound,   // Follow edges from source to target
    Inbound,    // Follow edges from target to source (dependents)
    Both        // Follow edges in both directions
}
```

### Supporting Types

```csharp
/// <summary>
/// Result of a graph traversal operation
/// </summary>
public class TraversalResult
{
    public GraphNode StartNode { get; init; } = null!;
    public IReadOnlyList<TraversalLevel> Levels { get; init; } = [];
    public int TotalNodesFound { get; init; }
}

public class TraversalLevel
{
    public int Depth { get; init; }
    public IReadOnlyList<TraversalHit> Hits { get; init; } = [];
}

public class TraversalHit
{
    public GraphNode Node { get; init; } = null!;
    public GraphEdge ViaEdge { get; init; } = null!;
    public GraphNode FromNode { get; init; } = null!;
}

/// <summary>
/// A path through the graph
/// </summary>
public class GraphPath
{
    public IReadOnlyList<GraphNode> Nodes { get; init; } = [];
    public IReadOnlyList<GraphEdge> Edges { get; init; } = [];
    public int Length => Edges.Count;
}

/// <summary>
/// A cycle detected in the graph
/// </summary>
public class GraphCycle
{
    public IReadOnlyList<GraphNode> Nodes { get; init; } = [];
    public CycleSeverity Severity { get; init; }
}

public enum CycleSeverity
{
    Info,       // Cycle within same project (often OK)
    Warning,    // Cycle between projects
    Error       // Cycle between layers that shouldn't have cycles
}

/// <summary>
/// Query for finding nodes
/// </summary>
public class NodeQuery
{
    public NodeType? Type { get; init; }
    public string? NamePattern { get; init; }          // Supports wildcards: "Order*", "*Service"
    public string? NamespacePattern { get; init; }
    public string? HasAttribute { get; init; }          // e.g., "DTO", "Service"
    public string? InProject { get; init; }
    public Accessibility? MinAccessibility { get; init; }
}

/// <summary>
/// An architectural rule that can be evaluated
/// </summary>
public class ArchitectureRule
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required RuleSeverity Severity { get; init; }
    
    // Rule definition
    public required NodeQuery SourceQuery { get; init; }
    public required EdgeType ForbiddenEdgeType { get; init; }
    public required NodeQuery TargetQuery { get; init; }
}

public enum RuleSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// A violation of an architectural rule
/// </summary>
public class RuleViolation
{
    public required ArchitectureRule Rule { get; init; }
    public required GraphNode SourceNode { get; init; }
    public required GraphNode TargetNode { get; init; }
    public required GraphEdge ViolatingEdge { get; init; }
}

/// <summary>
/// Centrality metric for a node
/// </summary>
public class NodeMetric
{
    public required GraphNode Node { get; init; }
    public int InDegree { get; init; }              // Number of inbound edges
    public int OutDegree { get; init; }             // Number of outbound edges
    public int TotalDegree => InDegree + OutDegree;
    public double PageRank { get; init; }           // Importance score
}
```

---

## Built-in Architecture Rules

The system includes predefined rules that can be enabled/customized.

```csharp
public static class BuiltInRules
{
    /// <summary>
    /// Controllers should not directly reference repositories
    /// </summary>
    public static ArchitectureRule NoControllerToRepository => new()
    {
        Id = "ARCH001",
        Name = "No Controller to Repository",
        Description = "Controllers should access data through services, not directly through repositories",
        Severity = RuleSeverity.Warning,
        SourceQuery = new NodeQuery 
        { 
            Type = NodeType.Class, 
            NamePattern = "*Controller",
            // Or: HasAttribute = "ApiController"
        },
        ForbiddenEdgeType = EdgeType.DependsOn,
        TargetQuery = new NodeQuery 
        { 
            Type = NodeType.Class, 
            NamePattern = "*Repository" 
        }
    };
    
    /// <summary>
    /// Domain layer should not reference infrastructure
    /// </summary>
    public static ArchitectureRule NoDomainToInfrastructure => new()
    {
        Id = "ARCH002",
        Name = "No Domain to Infrastructure",
        Description = "Domain classes should not depend on infrastructure concerns",
        Severity = RuleSeverity.Error,
        SourceQuery = new NodeQuery 
        { 
            Type = NodeType.Class, 
            NamespacePattern = "*.Domain.*" 
        },
        ForbiddenEdgeType = EdgeType.DependsOn,
        TargetQuery = new NodeQuery 
        { 
            Type = NodeType.Class, 
            NamespacePattern = "*.Infrastructure.*" 
        }
    };
    
    /// <summary>
    /// Interfaces should have at least one implementation
    /// </summary>
    public static ArchitectureRule InterfacesMustBeImplemented => new()
    {
        Id = "ARCH003",
        Name = "Unimplemented Interface",
        Description = "Interfaces should have at least one implementing class",
        Severity = RuleSeverity.Info,
        // Special rule - uses custom evaluation logic
    };
    
    /// <summary>
    /// No circular project references
    /// </summary>
    public static ArchitectureRule NoProjectCycles => new()
    {
        Id = "ARCH004",
        Name = "No Project Cycles",
        Description = "Projects should not have circular reference chains",
        Severity = RuleSeverity.Error,
        // Special rule - uses cycle detection
    };
    
    /// <summary>
    /// Public classes should not expose internal types
    /// </summary>
    public static ArchitectureRule NoInternalLeakage => new()
    {
        Id = "ARCH005",
        Name = "No Internal Leakage",
        Description = "Public classes should not expose internal types in their public API",
        Severity = RuleSeverity.Warning,
        // Special rule - uses custom evaluation logic
    };
}
```

### Custom Rules via Configuration

Teams can define rules in a JSON configuration file:

```json
{
  "rules": [
    {
      "id": "CUSTOM001",
      "name": "Services must be in Services namespace",
      "description": "Classes marked with [Service] attribute must be in a *.Services namespace",
      "severity": "Warning",
      "source": {
        "type": "Class",
        "hasAttribute": "Service"
      },
      "condition": "namespaceNotMatches",
      "pattern": "*.Services.*"
    },
    {
      "id": "CUSTOM002",
      "name": "DTOs should not have dependencies",
      "description": "Classes marked with [DTO] should not depend on other domain classes",
      "severity": "Error",
      "source": {
        "type": "Class",
        "hasAttribute": "DTO"
      },
      "forbiddenEdge": "DependsOn",
      "target": {
        "type": "Class",
        "namespacePattern": "*.Domain.*"
      }
    }
  ]
}
```

---

## Graph Diff

Comparing two graphs to detect changes.

```csharp
public class GraphDiff
{
    public required KnowledgeGraph Baseline { get; init; }
    public required KnowledgeGraph Current { get; init; }
    
    // Node changes
    public IReadOnlyList<GraphNode> AddedNodes { get; init; } = [];
    public IReadOnlyList<GraphNode> RemovedNodes { get; init; } = [];
    public IReadOnlyList<NodeChange> ModifiedNodes { get; init; } = [];
    
    // Edge changes
    public IReadOnlyList<GraphEdge> AddedEdges { get; init; } = [];
    public IReadOnlyList<GraphEdge> RemovedEdges { get; init; } = [];
    
    // Computed impacts
    public IReadOnlyList<RegressionRisk> Risks { get; init; } = [];
    
    // Summary
    public DiffSummary Summary { get; init; } = new();
}

public class NodeChange
{
    public required GraphNode BaselineNode { get; init; }
    public required GraphNode CurrentNode { get; init; }
    public IReadOnlyList<PropertyChange> Changes { get; init; } = [];
}

public class PropertyChange
{
    public required string PropertyName { get; init; }
    public object? OldValue { get; init; }
    public object? NewValue { get; init; }
    public bool IsBreaking { get; init; }
}
```

### Impact Analysis from Diff

When edges are added or removed, compute impact:

```csharp
public class ImpactAnalyzer
{
    public ImpactReport Analyze(GraphDiff diff)
    {
        var report = new ImpactReport();
        
        // For each removed public node, find all dependents
        foreach (var removed in diff.RemovedNodes.Where(IsPublicApi))
        {
            var dependents = _queryEngine.Traverse(
                removed.Id, 
                TraversalDirection.Inbound, 
                maxDepth: 5);
            
            report.AddImpact(new Impact
            {
                Type = ImpactType.RemovedPublicApi,
                Source = removed,
                AffectedNodes = dependents.Levels.SelectMany(l => l.Hits.Select(h => h.Node)).ToList(),
                Severity = ImpactSeverity.High
            });
        }
        
        // For each new dependency edge, check if it introduces coupling
        foreach (var added in diff.AddedEdges.Where(e => e.Type == EdgeType.DependsOn))
        {
            // Check if this creates a new cross-project dependency
            var source = diff.Current.NodesById[added.SourceId];
            var target = diff.Current.NodesById[added.TargetId];
            
            if (GetProject(source) != GetProject(target))
            {
                report.AddImpact(new Impact
                {
                    Type = ImpactType.NewCrossProjectDependency,
                    Source = source,
                    Target = target,
                    Severity = ImpactSeverity.Medium
                });
            }
        }
        
        // Check for new cycles
        var currentCycles = _queryEngine.FindCycles();
        var baselineCycles = _baselineQueryEngine.FindCycles();
        var newCycles = currentCycles.Except(baselineCycles);
        
        foreach (var cycle in newCycles)
        {
            report.AddImpact(new Impact
            {
                Type = ImpactType.NewCycle,
                AffectedNodes = cycle.Nodes.ToList(),
                Severity = ImpactSeverity.High
            });
        }
        
        return report;
    }
}
```

---

## Storage

Graphs are serialized to JSON for persistence.

```csharp
public interface IGraphStore
{
    Task SaveGraphAsync(KnowledgeGraph graph, CancellationToken ct = default);
    Task<KnowledgeGraph?> LoadGraphAsync(string id, CancellationToken ct = default);
    Task<KnowledgeGraph?> LoadLatestGraphAsync(string solutionPath, CancellationToken ct = default);
    Task<IReadOnlyList<GraphInfo>> ListGraphsAsync(string solutionPath, CancellationToken ct = default);
    Task DeleteGraphAsync(string id, CancellationToken ct = default);
}

public class FileGraphStore : IGraphStore
{
    private readonly string _basePath;
    
    // Stores as: {basePath}/graphs/{solutionHash}/{graphId}.json
    // Index at: {basePath}/graphs/{solutionHash}/index.json
}
```

---

## UI Integration

### Impact Explorer Component

When a user selects a node in the graph visualization:

1. Show the node details panel
2. Provide "Analyze Impact" button
3. Display traversal results in collapsible tree:

```
📁 OrderService (selected)
├── 📂 Direct Dependents (depth 1)
│   ├── OrderController [CALLS]
│   ├── OrderServiceTests [TESTS]
│   └── CheckoutWorkflow [DEPENDS_ON]
├── 📂 Indirect Dependents (depth 2)
│   ├── ApiController [CALLS OrderController]
│   └── IntegrationTests [TESTS CheckoutWorkflow]
└── 📂 Indirect Dependents (depth 3)
    └── E2ETests [TESTS ApiController]
```

### Rule Violations Panel

Show violations grouped by rule:

```
⚠️ Architecture Violations (3)

❌ ARCH001: No Controller to Repository
   └── OrderController → OrderRepository
       "Controllers should access data through services"
       📍 OrderController.cs:47
   
❌ ARCH002: No Domain to Infrastructure  
   └── Order → SqlConnectionFactory
       "Domain classes should not depend on infrastructure"
       📍 Order.cs:12
   └── Customer → EmailService
       📍 Customer.cs:89
```

### Diff Visualization

In the graph view, overlay changes:

- **Green nodes/edges**: Added
- **Red nodes/edges**: Removed (shown as ghosts)
- **Orange nodes**: Modified
- **Pulsing border**: Has associated risk

Sidebar shows:
```
📊 Changes Since [baseline date]

➕ Added (12)
   5 classes, 3 interfaces, 4 methods

➖ Removed (3)
   2 classes, 1 method ⚠️ Breaking

✏️ Modified (8)
   4 signature changes ⚠️ Breaking

⚠️ Risks Detected (4)
   2 High, 1 Medium, 1 Low
   [View Details]
```

---

## CLI Commands

Extend the CLI with graph operations:

```bash
# Build graph from solution
dsl graph build <solution-path>

# Run impact analysis
dsl graph impact <node-name> --depth 3

# Check architecture rules
dsl graph rules [--config rules.json]

# Find cycles
dsl graph cycles [--project-level]

# Compare two snapshots
dsl graph diff <baseline-id> <current-id>

# Find hubs (high-risk concentration)
dsl graph hubs --top 10

# Find orphans (potentially dead code)
dsl graph orphans

# Query nodes
dsl graph query --type Class --name "*Service" --namespace "*.Domain.*"

# Export graph to DOT format (for Graphviz)
dsl graph export <graph-id> --format dot

# Export graph to JSON
dsl graph export <graph-id> --format json
```

---

## Performance Considerations

### Indexing Strategy

Build these indexes on graph load:

| Index | Purpose |
|-------|---------|
| `NodesByType` | Fast filtering by node type |
| `EdgesBySource` | Fast outbound traversal |
| `EdgesByTarget` | Fast inbound traversal |
| `NodesByNamespace` | Fast namespace queries |
| `NodesByProject` | Fast project-scoped queries |
| `NodesByName` | Fast name lookups |

### Large Graph Handling

For solutions with 10,000+ types:

1. **Lazy loading**: Load node details on-demand
2. **Pagination**: Limit traversal results
3. **Caching**: Cache frequently-accessed paths
4. **Background indexing**: Build indexes async after initial load

### Memory Estimates

| Solution Size | Estimated Memory |
|---------------|------------------|
| Small (50 projects, 500 types) | ~10 MB |
| Medium (100 projects, 2000 types) | ~50 MB |
| Large (200 projects, 10000 types) | ~200 MB |

---

## Testing Strategy

### Unit Tests

- Graph building from mock snapshots
- Each query operation in isolation
- Rule evaluation logic
- Diff computation

### Integration Tests

- Round-trip: build graph → save → load → query
- Real solution analysis → graph building
- CLI command execution

### Test Fixtures

Create sample solutions with known structures:

```
TestSolutions/
├── CleanArchitecture/      # Well-structured, should pass all rules
├── CyclicDependencies/     # Contains project cycles
├── LayerViolations/        # Contains ARCH001, ARCH002 violations
├── LargeScale/             # Performance testing (generated)
└── MinimalApi/             # Small solution for quick tests
```

---

## Implementation Phases

### Phase 1: Core Graph (Week 1-2)
- [ ] Data model implementation
- [ ] GraphBuilder from ArchitectureSnapshot
- [ ] Basic storage (save/load)
- [ ] Unit tests for data model

### Phase 2: Query Engine (Week 2-3)
- [ ] Traversal operations
- [ ] Path finding
- [ ] Cycle detection
- [ ] Node/edge queries
- [ ] Index building

### Phase 3: Rules & Analysis (Week 3-4)
- [ ] Rule evaluation engine
- [ ] Built-in rules
- [ ] Custom rule configuration
- [ ] Impact analyzer
- [ ] Centrality metrics

### Phase 4: Diff & Comparison (Week 4-5)
- [ ] Graph diff algorithm
- [ ] Change classification
- [ ] Risk detection from diff
- [ ] Breaking change identification

### Phase 5: UI Integration (Week 5-6)
- [ ] Impact explorer component
- [ ] Rule violations panel
- [ ] Diff overlay in graph visualization
- [ ] CLI commands

### Phase 6: Polish & Documentation (Week 6)
- [ ] Performance optimization
- [ ] Error handling
- [ ] User documentation
- [ ] API documentation

---

## Future Enhancements (Out of Scope)

These are explicitly deferred:

1. **Vector embeddings for semantic search** - Requires ML model deployment
2. **Natural language queries** - Requires NLP processing
3. **Cross-solution analysis** - Scope limited to single solution
4. **Real-time monitoring** - Batch analysis only
5. **Git integration for historical analysis** - Use explicit snapshots instead

---

## Open Questions

1. **Stored procedure tracking**: How do we reliably detect stored procedure calls? 
   - Pattern match on `SqlCommand`, `ExecuteStoredProcedure`?
   - Require `[StoredProc("name")]` attribute on calling methods?

2. **Test project identification**: How do we reliably identify test projects?
   - Name convention (`*.Tests`, `*.Test`)?
   - Presence of test framework references?
   - Explicit configuration?

3. **Attribute-based classification**: What attributes should we recognize by default?
   - `[DTO]`, `[Entity]`, `[Service]`, `[Repository]`?
   - Or require explicit configuration?

---

## Appendix: Example Queries

### "What depends on OrderService?"

```csharp
var result = queryEngine.Traverse(
    startNodeId: "OrderService",
    direction: TraversalDirection.Inbound,
    maxDepth: 3
);

// Returns all callers/dependents up to 3 hops away
```

### "Find all controllers that bypass the service layer"

```csharp
var violations = queryEngine.EvaluateRule(BuiltInRules.NoControllerToRepository);

// Returns list of Controller → Repository direct dependencies
```

### "What's the path from API to database?"

```csharp
var paths = queryEngine.FindPaths(
    fromNodeId: "OrderController",
    toNodeId: "OrderRepository",
    maxDepth: 5
);

// Returns all paths showing how data flows
```

### "Find the riskiest classes to change"

```csharp
var hubs = queryEngine.CalculateCentrality(NodeType.Class)
    .OrderByDescending(m => m.InDegree)
    .Take(10);

// Returns classes with most dependents
```

### "What changed that might break things?"

```csharp
var diff = queryEngine.Compare(baselineGraph, currentGraph);

var breakingChanges = diff.Risks
    .Where(r => r.Level >= RiskLevel.High);

// Returns high-severity risks from changes
```
