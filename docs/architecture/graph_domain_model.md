# Knowledge Graph: Phase 1, 2 & 3 Implementation (C#)

This artifact documents the core C# implementation of the Knowledge Graph domain model (Phase 1), the Query Engine (Phase 2), and the Rule Engine (Phase 3).

## 1. Ontology: Node and Edge Types

The graph uses a rich ontology to categorize code and structural elements, enabling granular architectural queries.

### 1.1 Node Types (`NodeType`)

Implemented in `GraphTypes.cs`, these types cover the full spectrum of structural and semantic elements discovered during analysis.

```csharp
public enum NodeType
{
    // Solution structure
    Solution,
    Project,
    Namespace,

    // Type definitions
    Class,
    Interface,
    Struct,
    Record,
    Enum,
    Delegate,

    // Members
    Method,
    Property,
    Field,
    Event,

    // External dependencies
    ExternalPackage,

    // SQL elements
    Table,
    StoredProcedure,
    View,
    Column
}
```

### 1.2 Edge Types (`EdgeType`)

Defines the directed relationships between nodes.

```csharp
public enum EdgeType
{
    /// <summary>Parent-child containment (Solution→Project, Project→Namespace, etc.)</summary>
    Contains,

    /// <summary>Project-to-project reference</summary>
    References,

    /// <summary>Type dependency (field, parameter, return type)</summary>
    DependsOn,

    /// <summary>Interface implementation</summary>
    Implements,

    /// <summary>Class inheritance</summary>
    Inherits,

    /// <summary>Method invocation</summary>
    Calls,

    /// <summary>NuGet package usage</summary>
    UsesPackage,

    /// <summary>Stored procedure invocation</summary>
    CallsProc,

    /// <summary>Name-based match (e.g., DTO ↔ Table)</summary>
    NameMatch,

    /// <summary>Attribute-based binding</summary>
    AttributeBinding,

    /// <summary>Query trace relationship</summary>
    QueryTrace
}
```

## 2. Structural Elements

### 2.1 Graph Node (`GraphNode`)

The `GraphNode` is the atomic unit of the graph, supporting flexible metadata and navigation.

**Key Implementation Details:**

- **Flexible Properties**: Uses a `Dictionary<string, object> Properties` for type-specific data (e.g., Namespace, Accessibility, Signature) without requiring a rigid class hierarchy for every code element.
- **Navigation Properties**: `InboundEdges` and `OutboundEdges` are populated by the `KnowledgeGraph` to enable $O(1)$ traversal from any node.

### 2.2 Graph Edge (`GraphEdge`)

Represents a directed relationship. It stores references to both `Source` and `Target` nodes, allowing for bidirectional traversal (e.g., finding all callers of a method vs. finding all methods called by a class).

### 2.3 Knowledge Graph Container (`KnowledgeGraph`)

The `KnowledgeGraph` acts as the central repository and indexing engine for the structural model.

**Indexing Strategy (O(1) Lookups):**
To support real-time architectural queries and rule evaluation, the graph builds several internal indexes on load:

- **`NodesById`**: Primary dictionary for fast node retrieval.
- **`NodesByType`**: Groups nodes by their `NodeType` for fast filtering (e.g., "Find all Interfaces").
- **`EdgesBySource` / `EdgesByTarget`**: Accelerates graph traversal in both directions (Impact Analysis vs. Dependency Analysis).
- **`NodesByNamespace`**: Enables namespace-scoped analysis and clustering.

**Navigation Population**:
After the nodes and edges are added, the graph executes a `PopulateNavigation` pass to wire up cross-references, transforming cold IDs into direct object references (`Edge.Source` / `Node.InboundEdges`).

## 3. Transformation: `GraphBuilder`

The `GraphBuilder` transforms specialized `Snapshot` data into the generic `KnowledgeGraph`.

**Heuristic Mapping:**

- **Atoms to Nodes**:
  - `CodeAtom` is mapped to its closest `NodeType`. `Dto` atoms are promoted to `Class` nodes (preserving the distinction in metadata).
  - `SqlAtom` types are mapped to `Table`, `StoredProcedure`, `Column`, or `View`. `Function` types are normalized to `StoredProcedure`.
- **Links to Edges**:
  - `AtomLink` is mapped to `EdgeType`. Cross-domain links like `NameMatch` or `AttributeBinding` are preserved.
  - `ProjectReference` is mapped to `References`; `PackageDependency` is mapped to `UsesPackage`.

## 4. Implementation Project: `DiagnosticStructuralLens.Graph`

The implementation resides in a dedicated project to maintain separation of concerns:

- **Project File**: `DiagnosticStructuralLens.Graph.csproj`
- **Dependencies**: References `DiagnosticStructuralLens.Core` for the base `Snapshot` and `Atom` models.
- **Responsibility**: Houses the `KnowledgeGraph` container, `GraphBuilder` transformation logic, the `GraphQueryEngine`, and the `RuleEngine`.

### 4.1 Internal Test Accessibility

To support granular verification of internal indexing and building logic, the `DiagnosticStructuralLens.Graph` project documentation specifies the use of `InternalsVisibleTo` in the `.csproj` to expose internal state to the `DiagnosticStructuralLens.Tests` assembly without compromising the public API surface.

## 5. Query Engine: `GraphQueryEngine` (Phase 2)

The Query Engine provides the analytical layer over the knowledge graph, enabling traversal, cycle detection, and centrality analysis.

### 5.1 Query Result Types

To facilitate structured analysis, the engine uses several specialized record types:

- **`TraversalResult`**: Hierarchical representation of a graph traversal, containing `TraversalLevel` and `TraversalHit` objects.
- **`GraphCycle`**: Represents a detected circular dependency with an associated `CycleSeverity` (Info, Warning, Error).
- **`NodeMetric`**: Captures centrality rankings (In-Degree, Out-Degree) to identify architectural hubs.

### 5.2 Core Algorithms

- **Graph Traversal (BFS)**: Uses a queue-based Breadth-First Search to explore dependencies (`Outbound`) or dependents (`Inbound`) up to a specified `maxDepth`.
- **Cycle Detection (DFS)**: Employs a Depth-First Search with a recursion stack to identify circular references. Primary focus is on structural stability (breaking circularity in class and project dependencies).
- **Centrality & Orphans**: Uses the pre-calculated node navigation properties (`InboundEdges`, `OutboundEdges`) to perform $O(N)$ identification of system hubs and potentially dead code (orphans).

## 6. Verification Strategy (DoD Traceability)

The implementation is verified via comprehensive unit tests in `KnowledgeGraphTests.cs` (Phase 1) and `QueryEngineTests.cs` (Phase 2).

### 6.1 Phase 1 Checkpoints

| Requirement (DoD)          | Verification Method                                                 |
| :------------------------- | :------------------------------------------------------------------ |
| **Valid Graph Returns**    | `GraphBuilder_SingleProject_BuildsCorrectly`                        |
| **O(1) Lookup by ID**      | `Graph_NodesById_ReturnsO1Lookup`                                   |
| **Type-Based Filtering**   | `Graph_NodesByType_FiltersCorrectly`                                |
| **Outbound Edge Lookup**   | `Graph_EdgesBySource_ReturnsOutbound`                               |
| **Complete Node Mapping**  | Comprehensive mapping tests in `Graph_NodesByType_FiltersCorrectly` |
| **Complete Edge Mapping**  | `GraphBuilder_MapsAllEdgeTypes`                                     |
| **Empty Snapshot Support** | `GraphBuilder_EmptySnapshot_ReturnsEmptyGraph`                      |

### 6.2 Phase 2 Checkpoints

| Requirement (DoD)              | Verification Method                         |
| :----------------------------- | :------------------------------------------ |
| **Outbound Traversal**         | `Traverse_Outbound_ReturnsDependencies`     |
| **Inbound Traversal**          | `Traverse_Inbound_ReturnsDependents`        |
| **Cycle Detection**            | `FindCycles_DetectsKnownCycle`              |
| **Degree Calculation**         | `CalculateCentrality_ReturnsCorrectDegrees` |
| **Hub Identification**         | `CalculateCentrality_IdentifiesHubs`        |
| **Orphan Detection**           | `FindOrphans_ReturnsZeroInbound`            |
| **High-Performance Execution** | `Performance_LargeGraph_CompletesFast`      |

## 7. Rule Engine: `RuleEngine` (Phase 3)

The Rule Engine provides an automated governance layer, evaluating architectural constraints during query time and flagging structural violations.

### 7.1 Rule Definitions

- **`ArchitectureRule`**: Represents a constraint (e.g., "ARCH001: No Controller -> Repository").
- **`NodeQuery`**: A selection criteria for nodes using explicit types and glob pattern matching for names and namespaces.
- **`RuleViolation`**: A record that captures the specific source, target, and forbidden relationship found during evaluation.

### 7.2 Evaluation Logic

The `RuleEngine` evaluates rules through a three-stage predicate process:

1. **Source Discovery**: Identifies all nodes matching the `Rule.Source` criteria. Optimization: Uses the `NodesByType` index if a `NodeType` is specified in the query.
2. **Edge Scanning**: For each source, scans all `OutboundEdges` for the specified `ForbiddenEdge` type.
3. **Target Verification**: If a forbidden edge is found, evaluates whether the target node matches the `Rule.Target` criteria.

**Pattern Matching**: The engine translates glob patterns (`*`, `?`) into compiled regexes for efficient $O(N)$ string matching on names and namespaces.

### 7.3 Rule Loading & Overrides

The `RuleLoader` facilitates configuration-driven governance:

- **Default Set**: Includes built-in rules with segment-based wildcards:
  - **ARCH001**: `No Controller -> Repository` (Source: `*Controller`, Target: `*Repository`) enforces standard layered architecture.
  - **ARCH002**: `No Domain -> Infrastructure` (Source: `*.Domain*`, Target: `*.Infrastructure*`) enforces dependency inversion, catching both terminal and nested layer segments.
- **JSON Overrides**: Users can override default rules or add new ones via JSON configuration. The loader uses an $O(1)$ ID-based map to ensure configurations with matching IDs override built-ins, allowing for severity adjustments (e.g., setting `ARCH001` to `Warning` or `Info`) or pattern-tweakings without recompilation.

## 8. Verification Strategy (DoD Traceability)

The implementation is verified via comprehensive unit tests in `KnowledgeGraphTests.cs` (Phase 1), `QueryEngineTests.cs` (Phase 2), and `RuleEngineTests.cs` (Phase 3).

### 8.1 Phase 1 Checkpoints

| Requirement (DoD)          | Verification Method                                                 |
| :------------------------- | :------------------------------------------------------------------ |
| **Valid Graph Returns**    | `GraphBuilder_SingleProject_BuildsCorrectly`                        |
| **O(1) Lookup by ID**      | `Graph_NodesById_ReturnsO1Lookup`                                   |
| **Type-Based Filtering**   | `Graph_NodesByType_FiltersCorrectly`                                |
| **Outbound Edge Lookup**   | `Graph_EdgesBySource_ReturnsOutbound`                               |
| **Complete Node Mapping**  | Comprehensive mapping tests in `Graph_NodesByType_FiltersCorrectly` |
| **Complete Edge Mapping**  | `GraphBuilder_MapsAllEdgeTypes`                                     |
| **Empty Snapshot Support** | `GraphBuilder_EmptySnapshot_ReturnsEmptyGraph`                      |

### 8.2 Phase 2 Checkpoints

| Requirement (DoD)              | Verification Method                         |
| :----------------------------- | :------------------------------------------ |
| **Outbound Traversal**         | `Traverse_Outbound_ReturnsDependencies`     |
| **Inbound Traversal**          | `Traverse_Inbound_ReturnsDependents`        |
| **Cycle Detection**            | `FindCycles_DetectsKnownCycle`              |
| **Degree Calculation**         | `CalculateCentrality_ReturnsCorrectDegrees` |
| **Hub Identification**         | `CalculateCentrality_IdentifiesHubs`        |
| **Orphan Detection**           | `FindOrphans_ReturnsZeroInbound`            |
| **High-Performance Execution** | `Performance_LargeGraph_CompletesFast`      |

### 8.3 Phase 3 Checkpoints

| Requirement (DoD)               | Verification Method                      |
| :------------------------------ | :--------------------------------------- |
| **Source/Target Pattern Match** | `EvaluateRule_RespectsWildcards`         |
| **Layer Boundary Detection**    | `EvaluateRule_RespectsNamespacePatterns` |
| **Built-in Rule: ARCH001**      | `BuiltInRules_ControllerToRepo_Fails`    |
| **Built-in Rule: ARCH002**      | `BuiltInRules_DomainToInfra_Fails`       |
| **JSON Loader Overrides**       | `RuleLoader_OverridesBuiltIn`            |
| **Zero-Violation Integrity**    | `EvaluateRule_IgnoresAllowedEdges`       |

### 8.4 Phase 4 Checkpoints

| Requirement (DoD)            | Verification Method                 |
| :--------------------------- | :---------------------------------- |
| **Added Node Detection**     | `Compare_DetectsAddedNodesAndEdges` |
| **Removed Edge Detection**   | `Compare_DetectsRemovedEdges`       |
| **New Violation Isolation**  | `Compare_IdentifiesNewViolations`   |
| **Existing Issue Filtering** | `Compare_IgnoresExistingViolations` |
| **New Cycle Detection**      | `Compare_IdentifiesNewCycles`       |

## 9. Diff Strategy: `GraphDiffEngine` (Phase 4)

The Diff Strategy enables the automated detection of architectural regressions by comparing two knowledge graphs (Baseline vs. Current).

### 9.1 Diff Type Hierarchy

- **`GraphDiff` (Topology)**: Captures the raw "Set B - Set A" changes in nodes and edges.
- **`StructuralDiff` (Semantic)**: Provides a diagnostic view of the impact of those changes, specifically isolating _newly introduced_ regressions.

### 9.2 Semantic Comparison Logic

To isolate regressions from baseline noise, the engine uses a **Signature-Based Comparison**:

- **Violation Signatures**: `RuleId | SourceId | TargetId | EdgeId`. This ensures that if the same violation exists in both snapshots, it is not flagged as "New".
- **Cycle Signatures**: Node IDs involved in the cycle are **sorted** to create a canonical, rotation-invariant key. This prevents A->B->A from being seen as different from B->A->B.

### 10. Implementation Refinements & Gotchas

- **Index-First Requirement**: The `KnowledgeGraph` query indexes MUST be built via `BuildIndexes()` before `PopulateNavigation()` is called, as the latter uses the `EdgesBySource/Target` indexes to link nodes.
- **Inclusive Rule Patterns**: Built-in rules utilize inclusive glob patterns (e.g. `*.Domain*`) to ensure matches against both terminal namespaces and nested sub-namespaces, avoiding strict trailing dot requirements that cause false negatives in layer detection.

## 11. Hierarchical Aggregation Patterns

To support meaningful clustering in the UI, the Knowledge Graph applies aggregation heuristics that balance detail with cohesion.

### 11.1 Namespace Prefix Grouping

In large repositories, flat namespace listing often leads to "visual jitter." The system uses **Parent-Prefix Grouping** for L2 views:

- **Logic**: `group = Path.HasSuffix('.') ? Path.RemoveLast() : root`.
- **Visual Impact**: Ensures that related sub-namespaces (e.g., `DSL.Core`, `DSL.Persistence`) are treated as a single cluster by the `GroupCentroidForce`, creating identifiable "islands" of functionality rather than a scattered cloud of isolated nodes.

### 11.2 Link-Distance Weighting

The graph weights link distances based on the `EdgeType`:

- **Structural (Contains)**: Shorter distance ($k=0.5$) to pack child members tightly within their parent.
- **Semantic (DependsOn)**: Standard distance ($k=1.0$) to show flow.
- **Cross-Domain (ExternalPackage)**: Longer distance ($k=1.5$) to separate the internal "domain" from the "infrastructure."
