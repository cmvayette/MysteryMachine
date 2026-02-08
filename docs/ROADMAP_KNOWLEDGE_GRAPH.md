# Knowledge Graph & Visualization Roadmap

**Goal**: Transform the DSL (Diagnostic Structural Lens) dashboard from static snapshot viewer to interactive query-driven architecture explorer.

---

## Phase Overview

| Phase | Focus                 | Sessions | Deliverable                                           |
| ----- | --------------------- | -------- | ----------------------------------------------------- |
| **1** | Graph Foundation      | 1-2      | `KnowledgeGraph` + `GraphBuilder`                     |
| **2** | Query Engine          | 2-3      | `Traverse()`, `FindCycles()`, `CalculateCentrality()` |
| **3** | Rule Engine           | 1-2      | `EvaluateRule()` + built-in rules                     |
| **4** | Dashboard Integration | 2-3      | API endpoints + React hooks                           |
| **5** | Orientation Features  | 2-3      | Breadcrumbs, Mini-map, Hub sizing                     |
| **6** | Advanced Layouts      | 2-3      | Hierarchical mode, Clustering                         |
| **7** | Diff & Time Travel    | 1-2      | `GraphDiff` + snapshot comparison                     |
| **8** | Documentation         | 1        | Update user guide, design docs, CLI help              |

---

## Phase 1: Graph Foundation

**Objective**: Build the core data structures and transform existing C4 snapshots.

### Must Be True (Definition of Done)

> [!IMPORTANT]
>
> 1. `GraphBuilder.Build(snapshot)` returns a valid `KnowledgeGraph` from any C4 `ArchitectureSnapshot`
> 2. `graph.NodesById[id]` returns the correct node in O(1)
> 3. `graph.NodesByType[NodeType.Class]` returns all classes
> 4. `graph.EdgesBySource[nodeId]` returns all outbound edges
> 5. All node types from spec are mapped: Solution, Project, Namespace, Class, Interface, Method, etc.
> 6. All edge types from spec are mapped: CONTAINS, REFERENCES, DEPENDS_ON, IMPLEMENTS, INHERITS, CALLS
> 7. Unit tests pass for: empty snapshot, single-project, multi-project scenarios

---

## Phase 2: Query Engine

**Objective**: Enable traversal and analysis queries.

### Must Be True (Definition of Done)

> [!IMPORTANT]
>
> 1. `Traverse("classId", Outbound, 3)` returns all dependencies up to 3 hops
> 2. `Traverse("classId", Inbound, 3)` returns all dependents (reverse direction)
> 3. `FindCycles()` detects and returns at least one cycle in a known-cyclic test graph
> 4. `CalculateCentrality()` returns `InDegree`, `OutDegree` for every node
> 5. The top-3 nodes by `TotalDegree` match manual inspection
> 6. `FindOrphans()` returns nodes with zero inbound edges (excluding test refs)
> 7. Query execution completes in < 100ms for graphs with 500+ nodes

---

## Phase 3: Rule Engine

**Objective**: Evaluate architectural constraints at query time.

### Must Be True (Definition of Done)

> [!IMPORTANT]
>
> 1. `EvaluateRule(NoControllerToRepository)` returns violations when Controller → Repository edge exists
> 2. `EvaluateRule(NoDomainToInfrastructure)` returns violations when Domain → Infrastructure edge exists
> 3. Rules can be loaded from JSON config file
> 4. `RuleViolation` contains: Rule, SourceNode, TargetNode, ViolatingEdge
> 5. Zero violations returned when no rule is broken
> 6. At least 3 built-in rules are implemented

---

## Phase 4: Dashboard Integration

**Objective**: Wire graph engine to React frontend.

### Must Be True (Definition of Done)

> [!IMPORTANT]
>
> 1. GraphQL query `{ graph { nodes { id, name, type }, edges { sourceId, targetId, type } } }` returns data
> 2. `POST /query/traverse` with `{ nodeId, direction, maxDepth }` returns traversal result
> 3. Dashboard loads nodes/edges from graph query, not static snapshot
> 4. Clicking a node triggers Impact Analysis panel showing traverse results
> 5. Centrality data is available to frontend (`node.inDegree`, `node.outDegree`)
> 6. No regression in current functionality (drill-down, selection, blast radius)

---

## Phase 5: Orientation Features

**Objective**: Implement research-driven UX improvements.

### Must Be True (Definition of Done)

> [!IMPORTANT]
>
> 1. Breadcrumb shows current path: `Federation / RepoName / NamespaceName`
> 2. Clicking breadcrumb segment navigates to that level
> 3. Mini-map renders in corner showing full graph with viewport indicator
> 4. Nodes scale in size proportionally to `TotalDegree` (hubs are larger)
> 5. Simulation stops (freezes) when D3 fires "end" event
> 6. No visible jitter after simulation settles

---

## Phase 6: Advanced Layouts

**Objective**: Offer layout alternatives based on context.

### Must Be True (Definition of Done)

> [!IMPORTANT]
>
> 1. Namespace clusters are visually distinct (translucent bubble or shared color)
> 2. Toggle exists to switch between Force-Directed and Hierarchical layouts
> 3. Hierarchical layout places nodes in clear top-down tiers
> 4. Top-3 hub nodes (by centrality) are pinned and don't move during simulation
> 5. Layout mode persists across drill-down navigation

---

## Phase 7: Diff & Time Travel

**Objective**: Compare snapshots and show evolution.

### Must Be True (Definition of Done)

> [!IMPORTANT]
>
> 1. `GraphDiff.Compare(baseline, current)` returns added, removed, modified nodes/edges
> 2. Added nodes/edges render in green
> 3. Removed nodes/edges render in red (or dashed)
> 4. Modified nodes render in yellow/amber
> 5. Time slider selects snapshot and updates diff visualization
> 6. "What Changed" panel lists changes with counts

---

## Phase 8: Documentation

**Objective**: Update all documentation to reflect new functionality and user experience.

### Must Be True (Definition of Done)

> [!IMPORTANT]
>
> 1. `user_guide.md` documents all new UX features (breadcrumbs, mini-map, layouts, impact analysis)
> 2. `DESIGN_SPEC_KNOWLEDGE_GRAPH.md` reflects implemented API and data model
> 3. CLI `--help` output matches implemented commands (`traverse`, `cycles`, `centrality`)
> 4. README includes quick-start for new features
> 5. Changelog documents breaking changes and new capabilities
> 6. Screenshots/diagrams updated to reflect current UI

---

## Phase 9: Live Telemetry (Future)

**Objective**: Overlay runtime data (Errors, Traffic, Latency) onto the static structure.

> [!NOTE]
> This is a "Post-V1" initiative to transform the graph into a live war room.

### Capabilities (Planned)

1.  **"The Heartbeat"**: Nodes pulse Red/Yellow based on error rates from Prometheus/AppInsights.
2.  **"The Traffic"**: Particle effects on edges to visualize throughput volume.
3.  **"The Cost"**: Node sizing based on resource consumption (CPU/Memory).

---

## Session Estimates

| Phase | Estimated Sessions | Dependencies |
| ----- | ------------------ | ------------ |
| 1     | 1-2                | None         |
| 2     | 2-3                | Phase 1      |
| 3     | 1-2                | Phase 2      |
| 4     | 2-3                | Phases 1-3   |
| 5     | 2-3                | Phase 4      |
| 6     | 2-3                | Phase 4      |
| 7     | 1-2                | Phases 1-4   |
| 8     | 1                  | Phases 5-7   |

**Total**: ~13-20 sessions

---

## Quick Wins (Can Do Anytime)

These don't require the full Knowledge Graph:

- [x] Alpha/Velocity decay (jitter fix) ✅
- [ ] Breadcrumbs in header
- [ ] Hub sizing from current edge count
- [ ] Namespace clustering by color

---

## Next Session Recommendation

**Start with Phase 1**: Build `KnowledgeGraph` and `GraphBuilder` in the .NET backend. This unlocks all downstream phases.
