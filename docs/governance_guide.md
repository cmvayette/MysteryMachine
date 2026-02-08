# Diagnostic Structural Lens: Comprehensive Governance Guide

This document defines how to navigate the **Diagnostic Structural Lens** (DSL) dashboard and configure architectural governance rules.

## 1. Dashboard Navigation & Visualization

DSL supports 4 levels of the C4 model for architectural exploration:

- **L1 (Federation)**: Cross-repository relationships.
- **L2 (Repository)**: Namespaces and projects within a repo.
- **L3 (Namespace)**: Classes and components within a namespace.
- **L4 (Code)**: Method-level dependencies and implementation details.

### Interaction Patterns

- **Pan/Zoom**: Lodge labels based on zoom level (LOD) to reduce noise.
- **Drill Down**: Double-click a node to descend into its structure.
- **Selection**: Click a node to focus and highlight its neighborhood.
- **Governance Mode**: Visualize pulsing red violations against dimmed normal edges.

## 2. Temporal Exploration (Time Travel)

- **Timeline Slider**: Scrub through snapshots to see the graph at different commit points.
- **Diff Mode**: Visualize structural evolution (Additions/Removals/Changes) between snapshots.

## 3. Rule Specification (YAML)

Configuration is stored in `governance.yaml` at the repository root.

### 3.1 Example Configuration

```yaml
version: 1.0
definitions:
  domain: { namespace: "MyProject.Domain.*" }
rules:
  - type: layering
    mode: strict
    layers: ["@api", "@infrastructure", "@application", "@domain"]
```

### 3.2 Selectors

- **namespace**: Matches namespace prefix (supports `*`).
- **pattern**: Regex matching the Atom Name.
- **type**: Matches Atom type (`Class`, `Interface`, `Dto`).

### 3.3 Rule Types

- **Forbidden**: Absolute "No-Touch" policy.
- **Layering**: Topological ordering (Strict or Relaxed).
- **Visibility**: Enforces encapsulation via `allowed_consumers`.

## 4. CLI & CI/CD Integration

- **Scan**: `dsl scan --repo /path/to/repo --output snapshot.json`
- **Diff**: `dsl diff --baseline main.json --snapshot current.json`
- **CI Mode**: `--ci` for clean automation logs.
