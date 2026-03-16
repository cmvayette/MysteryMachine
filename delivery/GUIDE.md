# Diagnostic Structural Lens — User Guide

A CLI tool that scans your codebase, maps component relationships, detects stored procedure boundaries, and generates architecture intelligence reports for quality gating.

---

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Commands](#commands)
  - [report](#report) — Full architecture report with analyzers and policy gating
  - [scan](#scan) — Generate a snapshot JSON from your repo
  - [interpret](#interpret) — Human-readable summary from a snapshot
  - [diff](#diff) — Compare two snapshots for drift detection
  - [blast](#blast) — Impact analysis for a specific component
  - [risk](#risk) — Risk scoring with text, JSON, or HTML output
  - [federate](#federate) — Merge snapshots from multiple repos
- [Workflows](#workflows)
  - [Release Branch Gating](#release-branch-gating)
  - [Pre-Change Impact Analysis](#pre-change-impact-analysis)
  - [Multi-Repo Architecture Map](#multi-repo-architecture-map)
- [Configuration](#configuration)
  - [governance.yaml](#governanceyaml)
  - [dsl-policy.yaml](#dsl-policyyaml)

---

## Installation

### Option A: Install as a .NET tool (recommended)

```bash
# From the tools/ directory in this repo
dotnet tool install --global --add-source ./tools DiagnosticStructuralLens.Cli

# Verify
dsl --version
```

### Option B: Run directly from source

```bash
dotnet run --project src/DiagnosticStructuralLens.Cli -- <command> [options]
```

All examples below use `dsl` assuming you installed the tool globally. If running from source, substitute `dotnet run --project src/DiagnosticStructuralLens.Cli --` for `dsl`.

---

## Quick Start

```bash
# Generate a full architecture report for your repo
dsl report --repo /path/to/your/repo

# This produces dsl-report.md in the repo root
```

That's it. Open `dsl-report.md` to see your architecture breakdown, risk scores, governance violations, and 29 analyzer findings.

---

## Commands

### `report`

**The main command.** Scans your repo, runs all analyzers, checks governance rules, evaluates policy gates, and produces a markdown report.

```bash
dsl report --repo .
```

#### Options

| Flag                   | Description                             | Default                  |
| ---------------------- | --------------------------------------- | ------------------------ |
| `--repo <path>`        | Path to repository **(required)**       | —                        |
| `--output <file>`      | Output report file                      | `<repo>/dsl-report.md`   |
| `--output-json <file>` | Structured JSON results for CI/CD       | —                        |
| `--policy <file>`      | Policy YAML for quality gates           | `<repo>/dsl-policy.yaml` |
| `--include-private`    | Include internal/private types          | `false`                  |
| `--top <n>`            | Show top N risky components             | `10`                     |
| `--ci`                 | CI mode — exit code 1 on policy failure | `false`                  |

#### Example: CI/CD quality gate

```bash
# In your pipeline — fails the build if policy rules are violated
dsl report --repo . --ci --policy dsl-policy.yaml --output-json results.json
```

#### What the report contains

1. **Vital Signs** — component counts, link density, complexity metrics
2. **Component Inventory** — breakdown by type (classes, interfaces, DTOs, records)
3. **Namespace Distribution** — where your code lives
4. **Connectivity Analysis** — most-connected components (your "central nervous system")
5. **Governance Violations** — broken architecture rules from `governance.yaml`
6. **Risk Scores** — top N riskiest components by coupling + complexity
7. **Database Boundary** — stored procedure calls detected from C# code
8. **Analyzer Findings** — 29 built-in analyzers covering naming, SOLID, layering, etc.

---

### `scan`

Scans a repository and produces a `snapshot.json` — the foundation for all other commands.

```bash
dsl scan --repo /path/to/your/repo --output snapshot.json
```

#### Options

| Flag                | Description                                  | Default         |
| ------------------- | -------------------------------------------- | --------------- |
| `--repo <path>`     | Path to repository **(required)**            | —               |
| `--output <file>`   | Output snapshot file                         | `snapshot.json` |
| `--include-private` | Include internal/private types               | `false`         |
| `--no-link`         | Skip semantic linking (faster but less data) | `false`         |

#### Example output

```
🔍 Scanning repository: /Users/you/myproject
📦    Scanning C# files...
      Found 247 components, 181 links
🗄️    Scanning SQL files...
      Found 0 database objects, 0 links
🔗    Running semantic linker...
      Created 10 semantic links
        References: 10
✅
Snapshot saved to: snapshot.json

Summary:
  Components:  247
    DTOs:      0
    Interfaces:4
  DB Objects:  0
    Tables:    0
    Procs:     0
  Links:       191
  Duration:    0.49s
```

The snapshot JSON contains every component, database object, and relationship found. It's the input for `diff`, `blast`, `risk`, `interpret`, and `federate`.

---

### `interpret`

Reads a snapshot and generates a human-readable narrative summary of your architecture. Think of it as the "explain this to me" command.

```bash
# Print to terminal
dsl interpret --snapshot snapshot.json

# Save to a file
dsl interpret --snapshot snapshot.json --output architecture-summary.md
```

#### Options

| Flag                | Description                           | Default |
| ------------------- | ------------------------------------- | ------- |
| `--snapshot <file>` | Snapshot file **(required)**          | —       |
| `--output <file>`   | Output file (omit to print to stdout) | —       |

#### Example output

```markdown
# 🗺️ System Interpretation Report

**Analyzed Repository**: `myproject`
**Date**: 2026-02-10 11:03

## 1. High-Level Vital Signs

- **Code Volume**: 247 components found.
- **Database Surface**: 0 SQL objects detected.
- **Connectivity**: 191 relationships identified.
- **Complexity Density**: 0.77 links per node.

## 2. Architecture Breakdown

### Top Namespaces (by Volume)

- **`MyProject.Core`**: 77 components
- **`MyProject.Federation`**: 56 components
- **`MyProject.Cli.Analyzers`**: 49 components
- **`MyProject.Risk`**: 23 components
- **`MyProject.Core.Governance`**: 22 components

### Component Taxonomy

- **Interfaces (Contracts)**: 4
- **DTOs (Data Carriers)**: 0
- **Classes (Logic)**: 35
- **Database Tables**: 0

## 3. Connectivity Analysis

### Central Nervous System (Most Connected Nodes)

These are likely your core domain entities or utility services.

- **`CodeAtom`**: 16 connections
- **`SnapshotMetadata`**: 12 connections
- **`Snapshot`**: 10 connections

## 4. Diagnostics & Recommendations

- ✅ **Good Abstraction**: Interfaces detected, suggesting a decoupled architecture.
```

#### When to use `interpret` vs `report`

|              | `interpret`                       | `report`                       |
| ------------ | --------------------------------- | ------------------------------ |
| **Input**    | A snapshot file                   | A repo path (scans first)      |
| **Speed**    | Instant (reads existing snapshot) | Slower (scans + analyzes)      |
| **Depth**    | High-level summary                | Detailed with all 29 analyzers |
| **Use case** | Quick architecture overview       | Full audit / CI gating         |

---

### `diff`

Compares two snapshots to detect what changed — added components, removed components, and the blast radius of removals.

```bash
# Compare a baseline snapshot against the current state
dsl diff --baseline baseline.json --snapshot current.json
```

#### Options

| Flag                | Description                      | Default |
| ------------------- | -------------------------------- | ------- |
| `--baseline <file>` | Baseline snapshot **(required)** | —       |
| `--snapshot <file>` | Current snapshot **(required)**  | —       |

#### Example output

```
Results:
  Code Components:
    Added:   12
    Removed: 3
  Database Objects:
    Added:   0
    Removed: 0
  Blast Radius: 7 components potentially affected

⚠️  Removed components (potentially breaking):
    - MyProject.Core.OldService (Class in MyProject.Core)
    - MyProject.Models.LegacyDto (DTO in MyProject.Models)
    - MyProject.Data.DeprecatedRepo (Class in MyProject.Data)
```

---

### `blast`

Given a specific component, shows what would break if you changed it. Useful for pre-change impact analysis.

```bash
dsl blast --snapshot snapshot.json --component UserService
```

#### Options

| Flag                | Description                                 | Default |
| ------------------- | ------------------------------------------- | ------- |
| `--snapshot <file>` | Snapshot file **(required)**                | —       |
| `--component <id>`  | Component ID or partial name **(required)** | —       |
| `--depth <n>`       | Max traversal depth                         | `5`     |

> **Note:** The `--atom` flag also works as an alias for `--component`.

#### Example output

```
💥 Calculating blast radius for: CodeAtom
   (Matched to: diagnosticstructurallens-federation-federatedsnapshot-codeatoms)

Blast Radius Results:
  Root Component: diagnosticstructurallens-federation-federatedsnapshot-codeatoms
  Total Affected:  1
  Max Depth:       1

Affected components by depth:

  Depth 1:
    - FederatedSnapshot
```

Component IDs support partial matching — you don't need the full qualified name.

---

### `risk`

Generates a risk report scoring each component by coupling complexity, fan-in/fan-out, and other heuristics.

```bash
# Text output (default)
dsl risk --snapshot snapshot.json --top 5

# JSON for programmatic use
dsl risk --snapshot snapshot.json --format json --output risk.json

# Styled HTML dashboard
dsl risk --snapshot snapshot.json --format html --output risk.html
```

#### Options

| Flag                | Description                   | Default |
| ------------------- | ----------------------------- | ------- |
| `--snapshot <file>` | Snapshot file **(required)**  | —       |
| `--format <type>`   | `text`, `json`, or `html`     | `text`  |
| `--output <file>`   | Output file (omit for stdout) | —       |
| `--top <n>`         | Show top N risky components   | `10`    |

#### Example output (text)

```
Risk Report - 2026-02-10 19:03
═══════════════════════════════════════════════

  Total: 247 | Critical: 0 | High: 0 | Medium: 0 | Low: 247

Top 5 Risky Components:
───────────────────────────────────────────────
  🟢 CodeAtom                        14.3 (Low)
  🟢 SnapshotMetadata                10.3 (Low)
  🟢 Snapshot                         8.8 (Low)
  🟢 AtomLink                         8.2 (Low)
  🟢 SnapshotDelta                    6.8 (Low)
```

The HTML format produces a dark-themed dashboard with color-coded risk levels that you can share with your team.

---

### `federate`

Merges snapshots from multiple repositories into a single global architecture map. Useful for microservice architectures.

```bash
dsl federate \
  --snapshots service-a.json,service-b.json,service-c.json \
  --output global-snapshot.json \
  --strategy newest
```

#### Options

| Flag                  | Description                                   | Default  |
| --------------------- | --------------------------------------------- | -------- |
| `--snapshots <files>` | Comma-separated snapshot files **(required)** | —        |
| `--output <file>`     | Output federated snapshot **(required)**      | —        |
| `--strategy <type>`   | Conflict resolution: `newest` or `priority`   | `newest` |
| `--priority <repos>`  | Repo priority for conflicts (comma-separated) | —        |

The federated snapshot can then be used with `interpret`, `risk`, or `blast` to analyze your entire system holistically.

---

## Workflows

### Release Branch Gating

Use DSL in your CI pipeline to gate releases on architecture quality.

```yaml
# azure-pipelines.yml
trigger:
  branches:
    include:
      - main
      - release/*

steps:
  - task: UseDotNet@2
    inputs:
      packageType: sdk
      version: "8.0.x"

  - script: dotnet tool install --global --add-source ./tools DiagnosticStructuralLens.Cli
    displayName: "Install DSL"

  - script: |
      dsl report --repo . --ci \
        --policy dsl-policy.yaml \
        --output $(Build.ArtifactStagingDirectory)/dsl-report.md \
        --output-json $(Build.ArtifactStagingDirectory)/dsl-results.json
    displayName: "Architecture Quality Gate"

  - publish: $(Build.ArtifactStagingDirectory)
    artifact: architecture-report
```

When `--ci` is set, DSL exits with code 1 if any policy rules are violated, failing the pipeline.

### Pre-Change Impact Analysis

Before making a change, check what could break:

```bash
# 1. Snapshot the current state
dsl scan --repo . --output before.json

# 2. Make your changes, then snapshot again
dsl scan --repo . --output after.json

# 3. See what changed
dsl diff --baseline before.json --snapshot after.json

# 4. Check impact of a specific component you modified
dsl blast --snapshot after.json --component UserService
```

### Multi-Repo Architecture Map

For microservice architectures, build a global view:

```bash
# Scan each repo
dsl scan --repo ../service-auth --output auth.json
dsl scan --repo ../service-orders --output orders.json
dsl scan --repo ../service-payments --output payments.json

# Merge into global map
dsl federate --snapshots auth.json,orders.json,payments.json --output global.json

# Analyze the whole system
dsl interpret --snapshot global.json --output system-overview.md
dsl risk --snapshot global.json --format html --output system-risk.html
```

---

## Configuration

### governance.yaml

Place a `governance.yaml` in your repo root to define architecture rules. Violations show up in the report.

```yaml
rules:
  - name: No direct DB access from controllers
    message: "Controllers should not directly reference repository classes"
    source:
      namespace: "*.Controllers"
    target:
      namespace: "*.Data"
    type: Inheritance
    action: deny

  - name: DTOs should not reference services
    message: "DTOs must remain pure data carriers"
    source:
      type: Dto
    target:
      type: Class
      namespace: "*.Services"
    action: deny
```

### dsl-policy.yaml

Define quality gates that can fail CI builds when `--ci` is used.

```yaml
# See dsl-policy.yaml.example for all available options
max_critical_risk: 0 # Fail if any critical-risk components
max_high_risk: 5 # Fail if more than 5 high-risk components
max_governance_violations: 0 # Fail if any governance rule violations
min_interface_ratio: 0.05 # At least 5% of components should be interfaces
```

---

## Need Help?

```bash
dsl --help      # Full option reference
dsl --version   # Show installed version
```
