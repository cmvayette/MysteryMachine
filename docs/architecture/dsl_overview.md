# Diagnostic Structural Lens (DSL) Overview: Quality Intelligence for the Enterprise

**Diagnostic Structural Lens** (DSL), formerly **Mystery Machine** and **Regression Radar**, is a "Portable Senior Engineer" and quality intelligence platform designed for complex, often airgapped, software ecosystems. It uncovers "hidden" regressions, architectural drift, and behavioral anomalies by modeling the **Software Truth** of a repository over time.

## The DSL Identity (Rebranded FEB 2026)

The name reflects the system's core value proposition:

- **D (Diagnostic)**: Acts as a pathology detection tool for architectures, making violations "obvious and annoying."
- **S (Structural)**: Reveals the shape, scale, and clustering (structure) of software across federated boundaries.
- **L (Lens)**: Provides a specific way of seeing that turns complex data into a clear signal within 60 seconds.

## Core Philosophy: Inference-First

DSL is designed with a "Zero-Config" philosophy. To minimize adoption friction, the system automatically learns the state of the codebase without requiring users to manually define contracts or patterns.

- **Run Once**: The system learns architectural patterns, public API contracts, and behavioral baselines.
- **Run Twice**: The system detects regressions, breaking changes, and performance drift against the established baseline.

## Beyond Static Analysis: The Software Truth Model (STM)

Unlike traditional analyzers that scan a single point-in-time snapshot, DSL maintains a persistent **Software Truth Model**. This model tracks:

1. **API Contracts**: Signatures and semantic promises (nullability, documentation constraints).
2. **System Invariants**: Structural rules that must always be true (e.g., "Persistence cannot reference UI").
3. **Behavioral Baselines**: Performance, execution surface, and input/output expectations derived from tests and profiling.

## Key Capabilities

- **Polyglot Intelligence**: Unified semantic modeling across C#, VB.NET, JavaScript, and TypeScript via the **Language-Agnostic Intermediate Model (LAIM)**.
- **Regression Intelligence**: Distinguishes between intentional evolution and accidental breaking changes.
- **Architectural Mapping (C4)**: Visualizes the system as a hierarchical graph (Containers, Components, Relationships).
- **Integration Intelligence**: Maps Data Platform (EF Core) and Runtime performance (Heatmaps) directly onto the architecture.
- **Standards and Compliance Intelligence**: AST-based enforcement of qualitative standards. Visualizes compliance health via standards-focused heatmaps.
- **Multi-Project Dashboard**: A hosted Vite + React portal with version timelines and interactive D3 visualizations.
- **Standards Enforcement**: Real-time detection of architectural and code-quality violations.
- **Quality Intelligence**: Blast Radius analysis, Dead Code detection, and Cognitive Complexity tracking.
- **Regression Defense**: Baseline comparison to prevent architectural drift.
- **Automation Ready**: First-class support for CI/CD history storage and reusable pipeline templates.

## Legacy & Rebranding

1. **Regression Radar**: Initial project focusing on regression detection.
2. **Mystery Machine (Feb 2026)**: Renamed to reflect its mission of "solving the mystery" of code behavior changes.
3. **Diagnostic Structural Lens (DSL) (Feb 2026)**: Finalized name to align with technical value (Diagnostic), architectural scope (Structural), and focused visibility (Lens).

## Project Evolution & Roadmap

### Series 1: Foundational Intelligence (Regression Radar Era) ✅ COMPLETE

- **Phase 1-4**: Runtime Intelligence, Data Platform Mapping, Verification Links, and Migration Linters.

### Series 2: Enterprise Scaling & Hardening ✅ COMPLETE

- **Phase 5-8**: Performance Optimization, Standards & Configuration, Policy as Code, and Airgap Hardening.

### Series 3: The DSL Evolution (FEB 2026 - Current) 🚀 IN PROGRESS

- **Phase 9-11**: Software Truth Model (STM) and Signature Inference. Finalized unified branding (DSL).
- **Phase 12-14**: CI/CD Persistence, Polyglot Analysis (TS kernel), and Premium Vite Dashboard.
- **Phase 19 (C4 Layering Compliance)**: Aligning the dashboard with C4 specifications (L1-L5). Refinement of "Container" abstraction to use Projects (.csproj) as the L2 tier. ✅ COMPLETE
- **Phase 20 (Testing & Refinement)**: Scaling verification, visual tuning, and deployment readiness. ✅ COMPLETE

### Series 4: Graph-Driven Intelligence (Strategic Arc) 🚀 IN PROGRESS

- **Phase 1: Foundation**: Knowledge Graph domain model and atomic extraction. ✅ COMPLETE
- **Phase 2: Query Engine**: Graph traversal, cycles, and centrality analytics. ✅ COMPLETE
- **Phase 3: Rule Engine**: Semantic architectural governance and rule loading. ✅ COMPLETE
- **Phase 4: Diff Strategy**: Semantic regression detection and structural impact diffing. ✅ COMPLETE
- **Phase 5: Dashboard Integration**: Verified end-to-end connectivity between GraphQL API and D3 visualizations. Finalized v2 branding and UI polish. ✅ COMPLETE
- **Phase 6: Persistence Layer**: Transition from in-memory/file-based state to Postgres with EF Core. Supports historical analysis and session persistence. ✅ COMPLETE
- **Phase 7: CLI-API Truth Chain**: Implementation of the `publish` command for automated snapshot uploads to the DSL central storage. ✅ COMPLETE

---

## Technical & Implementation Reference

- **[Master Technical Reference](./technical_reference/master_reference.md)**: CLI automation and CI/CD "Truth Chain".
- **[Implementation Master Reference](./implementation/master_reference.md)**: AST-based scan patterns and performance optimization.
- **[Persistence Architecture](./architecture/persistence_architecture.md)**: Postgres and EF Core integration strategy for Snapshots and Knowledge Graphs.
- **[Knowledge Graph Roadmap](./architecture/knowledge_graph_roadmap.md)**: Strategic plan for graph-driven intelligence (Traversal, Centrality, Rules).
- **[Core Engine Specification](./architecture/core_engine_spec.md)**: Consolidated technical specification for the graph data model and query engine.
- **[Visual Standards & UX Architecture](./dashboard/architecture_visuals_and_patterns.md)**: "Solar System" physics, interaction design, and C4 iconography.
- **[Unified Testing Architecture](../quality_test_architecture_stewardship/artifacts/architecture/dsl_comprehensive_testing_architecture.md)**: "Zero Trust" strategy (L0-L3) and integration infrastructure.
- **[Verification Vault](./quality/verification_vault.md)**: Consolidated quality protocols, value rubrics, hazard remediation, and validation history.
- **[Knowledge Graph Domain Model Implementation](./implementation/graph_domain_model.md)**: C# implementation details for node/edge ontology and structural classes.
- **[Branding & Logo Integration](./dashboard/branding_and_logo_integration.md)**: Standards for processed transparent assets, favicon management, and header implementation.
- **[Governance Guide](./governance/guide.md)**: Policy-as-Code definitions.
