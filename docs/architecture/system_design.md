# DSL: System Architecture & Design Specification

Diagnostic Structural Lens (DSL) is an atomic-level codebase intelligence platform that transforms raw codebases into a deterministic Knowledge Graph for impact analysis, architectural governance, and multi-repo federation.

## 1. Core Engine: Atomic Extraction & Linking

The engine operates on the principle of **Atomic Extraction**, decomposing codebases into "Atoms" (Entities) and "Links" (Relationships).

### 1.1 Scanners and extraction

DSL utilizes domain-specific scanners to build the Software Truth Model:

- **C#/.NET (Roslyn)**: Traverses AST to extract DTOs, Interfaces, Classes, and Methods.
- **SQL (ScriptDOM)**: Parses T-SQL scripts for Tables, Columns, Stored Procedures, and Views.
- **TypeScript (tsc API)**: Identifies modules, exports, imports, and type usages.

### 1.2 Semantic Link Synthesis

The `SemanticLinker` resolves relationships using multiple strategies:

- **Attribute Binding**: Explicit linkage via code attributes (e.g., `[Table]`).
- **Name Matching**: Exact and fuzzy (pluralization, suffix stripping) matching.
- **Signature Analysis**: Linking interfaces to referenced types.
- **Query Tracing**: Extracting table references from inline SQL strings (Dapper detection).

## 2. Knowledge Graph Model

The Knowledge Graph is the analytical layer over the static snapshots.

### 2.1 Graph Ontology

- **Nodes**: `Solution`, `Project`, `Namespace`, `Class`, `Interface`, `Struct`, `Enum`, `Method`, `Property`, `Table`, `StoredProcedure`, `Column`.
- **Edges**: `CONTAINS` (Hierarchy), `REFERENCES` (Cross-project), `DEPENDS_ON` (Type-level), `CALLS` (Invocation), `IMPLEMENTS`, `INHERITS`.

### 2.2 Analytical Operations

- **Impact Analysis**: BFS traversal (`Traverse`) to determine dependents/dependencies.
- **Structural Integrity**: DFS-based cycle detection to identify circular "knots".
- **Centrality**: Identifies architectural "Hubs" using degree centrality and PageRank-inspired logic.
- **Governance**: Evaluates architectural constraints (rules) against the graph topology at query time.

### 2.3 C4 Model Alignment

DSL is structured to support the C4 Architecture Model through progressive drill-down:

- **L1 Context**: Federated view of all repositories.
- **L2 Container**: Individual Projects (.csproj) within a repository.
- **L3 Component**: Namespaces and logical clusters.
- **L4 Code**: Atomic details (Classes, Interfaces, SQL Tables).

See `artifacts/architecture/c4_abstraction_mapping.md` for the definitive mapping standards and current implementation gaps.

## 3. Persistence & Hydration Architecture

The system utilizes a **Persistence-First** design using PostgreSQL to enable historical analysis and state consistency.

### 3.1 The Gateway Pattern: CLI -> API -> DB

To ensure central validation and consistent hydration, all persistence is routed through the DSL API:

- **CLI**: Extracts snapshots and publishes them via `POST /load`.
- **API**: Receives snapshots, persists them as **JSONB** in PostgreSQL, and re-hydrates the global Knowledge Graph.
- **DB (PostgreSQL)**: Acts as the "Software Truth Model" storage.

### 3.2 Incremental Federation Pattern

On every snapshot upload, the backend automatically performs a "federated hydration":

1. Retrieves the latest snapshot for _every unique repository_ in the database.
2. Merges these snapshots using the `FederationEngine`.
3. Passes the result to `GraphBuilder` to rebuild the in-memory `KnowledgeGraph`.
4. This enables real-time cross-repository visibility while keeping individual scans lightweight.

## 4. Strategic Implementation (Phased Roadmap)

The platform is developed in phases to continuously deliver architectural value:

- **Phases 1-4 (Foundation & Intelligence)**: Graph model, Query Engine, Rule Engine, and Diff Engine (Baseline comparison).
- **Phase 5 (Visualization)**: Tiered C4 navigation, Hub-based node sizing, and UX settlement logic (Alpha/Velocity decay).
- **Phase 6 (Persistence)**: PostgreSQL integration, JSONB storage, and federated hydration logic.
- **Phase 7 (Workflows)**: CLI `scan --publish` optimization and truth chain verification.

## 5. Performance Targets

- **Memory Footprint**: Optimized for ~200MB for solutions with up to 10,000 types.
- **Search Latency**: Analytical queries (traversal, cycles) targeting <100ms on indexed graphs.
- **Hydration Speed**: Full federated graph reconstruction in <1s for mid-sized enterprise estates.
