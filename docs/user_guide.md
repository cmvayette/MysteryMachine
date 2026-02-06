# System Cartographer User Guide

## Overview

System Cartographer calls your codebase, visualizing architecture, dependencies, and evolution over time.

## 1. Dashboard Navigation

The interactive dashboard is the primary way to explore your system.

### Controls

- **Pan**: Click and drag background.
- **Zoom**: Scroll wheel or pinch.
- **Select**: Click a node to focus (highlights connections).
- **Drill Down**: Double-click a node to see internal structure (L3 -> L4).

### Visual Language

- **Hexagons**: Repositories / Bounded Contexts (L1).
- **Rounded Rects**: Namespaces / Containers (L2).
- **Squares**: Classes / Components (L3).
- **Lines**:
  - **Solid**: Inheritance
  - **Dashed**: Implementation
  - **Dotted**: Usage/Reference

## 2. Time Travel (Snapshots)

Navigate through history to see how architecture evolved.

- **Timeline**: Use the slider at the bottom to jump to a specific commit.
- **Play/Pause**: Animate the evolution of the graph.
- **Diff Mode**: Toggle "Diff" to color-code changes:
  - 🟢 **Green**: Added
  - 🔴 **Red**: Removed
  - 🟡 **Yellow**: Modified

## 3. Governance Mode

Ensure architectural compliance.

- **Toggle**: Click the "Governance" button in the top bar.
- **Violations**: Forbidden dependencies appear as **pulsing red dashed lines**.
- **Focus**: Non-violating edges are dimmed to highlight problems.

## 4. CLI & CI/CD

Integrate analysis into your pipeline.

### Basic Scan

```bash
cartographer scan --repo /path/to/repo --output snapshot.json
```

### CI Mode

Use `--ci` to sanitize output for logs (removes emojis/interactive elements).

```bash
cartographer scan --repo . --ci
```

### Check for Breaking Changes

Compare current snapshot against a baseline. Returns exit code 1 if breaking changes found.

```bash
cartographer diff --baseline main.json --snapshot current.json
```
