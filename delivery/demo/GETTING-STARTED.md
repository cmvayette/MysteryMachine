# DSL Getting Started Guide — For Teams Managing 60+ .NET Systems

This guide walks you through using Diagnostic Structural Lens (DSL) to map, analyze, and govern a large portfolio of .NET systems that share common data sources.

We use 4 real open-source repositories as stand-ins for your systems. By the end, you'll know how to scan your own repos, federate them into a global map, enforce governance rules, and track architectural drift.

---

## What's in this demo package

```
delivery/demo/
  GETTING-STARTED.md        <- You are here
  run-demo.sh               <- Automated walkthrough script
  governance.yaml            <- Sample architectural rules
  snapshots/
    clean-architecture.json  <- Small layered app (224 components)
    eshop.json               <- Microservices federation (1,301 components)
    nopcommerce.json         <- Large monolith (18,745 components)
    bitwarden-server.json    <- DB-heavy mixed stack (11,925 code + 2,101 DB)
    federated-global.json    <- All 4 merged into a single map
  reports/
    clean-architecture-report.md
    eshop-report.md
    nopcommerce-report.md
    bitwarden-server-report.md
```

---

## Prerequisites

- .NET 8 SDK
- Node.js 18+ (for TypeScript scanner and dashboard)
- Git

## Quick start

```bash
# Build the CLI
dotnet build src/DiagnosticStructuralLens.Cli/DiagnosticStructuralLens.Cli.csproj

# Run the interactive demo (walks through every command)
cd delivery/demo
./run-demo.sh

# Or run everything automatically
./run-demo.sh --all
```

---

## The Walkthrough

### 1. Scan a single repository

The `scan` command is the foundation. It parses your codebase and produces a snapshot JSON file containing every component (class, interface, DTO, enum, stored procedure, table, view, etc.) and the relationships between them.

```bash
dsl scan --repo /path/to/your-system --output snapshots/your-system.json
```

**What it extracts:**

| Language | Components detected |
|----------|-------------------|
| C# | Classes, Interfaces, Records, Structs, Enums, Methods, Properties, DTOs (by naming convention + attributes) |
| T-SQL | Tables, Columns, Stored Procedures, Views, Functions, Foreign Keys |
| TypeScript | Classes, Interfaces, Enums, Functions, Type Aliases, Modules, Components |

**Special detection:**
- EF Core attributes (`[Table]`, `[Column]`, `[Key]`, `[ForeignKey]`)
- Dapper inline SQL patterns and stored procedure calls
- ADO.NET `SqlCommand` invocations

Try it with the demo repos:
```bash
# Small (< 1 second)
dsl scan --repo validation/CleanArchitecture --output snapshots/clean-architecture.json

# Large (~ 10 seconds for 18k components)
dsl scan --repo validation/nopCommerce --output snapshots/nopcommerce.json
```

### 2. Generate an architecture report

The `report` command scans + analyzes in one step, producing a markdown report with:

- **Vital signs** — component count, DB objects, relationships, coupling density
- **Component taxonomy** — breakdown by type (classes, DTOs, stored procedures, etc.)
- **Architecture breakdown** — top 15 namespaces by component density
- **Central hub nodes** — components with highest fan-in + fan-out (change these = ripple)
- **Risk assessment** — components ranked by risk score
- **Architecture findings** — God classes, circular deps, complex controllers, missing interfaces
- **Migration readiness** — .NET Framework patterns detected (System.Web, etc.)
- **Database boundary** — stored procedure to code mapping

```bash
dsl report --repo validation/bitwarden-server --output reports/bitwarden-report.md
```

Open the generated markdown to review. For your team, the most immediately useful sections will be:
- **Central Nodes** — shows which components are change bottlenecks
- **Architecture Findings** — God classes and circular dependencies highlight maintenance risk
- **Database Boundary** — reveals which stored procedures are called from code

### 3. Federate multiple systems into a global map

This is the key feature for your team. When you have 60+ systems sharing databases, no single repo tells the full story. Federation merges all snapshots into one navigable graph.

```bash
# Scan each system
dsl scan --repo /path/to/system-a --output snapshots/system-a.json
dsl scan --repo /path/to/system-b --output snapshots/system-b.json
dsl scan --repo /path/to/system-c --output snapshots/system-c.json

# Federate into one map
dsl federate \
    --snapshots snapshots/system-a.json,snapshots/system-b.json,snapshots/system-c.json \
    --output snapshots/global-map.json
```

**What federation reveals:**
- Two systems both calling `dbo.Orders`? Now that's a visible shared edge.
- A stored procedure modified by System A but read by Systems B, C, D? Blast radius is clear.
- Which system "owns" each table? The one with the most write operations.

Try it with the demo:
```bash
dsl federate \
    --snapshots snapshots/clean-architecture.json,snapshots/eshop.json,snapshots/nopcommerce.json,snapshots/bitwarden-server.json \
    --output snapshots/federated-global.json
```

### 4. Calculate blast radius

"What breaks if I change this table?" — the question your team asks every day.

```bash
dsl blast --snapshot snapshots/bitwarden-server.json --atom table:dbo.User --depth 3
```

This traverses the dependency graph outward from the specified component and lists everything downstream. Use this before making schema changes to any shared data source.

### 5. Risk scoring

Prioritize which systems need attention. The risk scorer evaluates every component based on complexity, coupling, fan-in/fan-out, and maintainability.

```bash
# Text summary (quick triage)
dsl risk --snapshot snapshots/nopcommerce.json --format text --top 15

# HTML report (share with stakeholders)
dsl risk --snapshot snapshots/bitwarden-server.json --format html --output reports/risk.html --top 20
```

### 6. Track drift with snapshot diffing

Run `dsl diff` between releases to catch architectural regressions:

```bash
# Compare last release to current
dsl diff --baseline snapshots/system-v1.0.json --snapshot snapshots/system-v1.1.json --format markdown
```

The diff detects:
- New components added
- Components removed
- Relationships changed
- Breaking changes (removed public interfaces, renamed entities)

### 7. Enforce governance rules

Copy `governance.yaml` to your repo root and customize the rules. DSL checks every link in the snapshot against these rules during report generation.

The demo `governance.yaml` includes:

| Rule | What it enforces |
|------|-----------------|
| `LAYER-001` | Dependencies flow API > Service > Domain > Infrastructure (strict) |
| `FORBID-001` | Domain cannot depend on Infrastructure (DDD inversion) |
| `FORBID-002` | Controllers cannot directly access Repositories |
| `FORBID-003` | DTOs cannot leak into Domain layer |
| `VIS-001` | Entity classes only accessible by Services and Repositories |

**To add governance to CI:**
```bash
# In your CI pipeline
dsl report --repo . --ci
# Exit code is non-zero if critical governance violations exist
```

### 8. Launch the interactive dashboard

The dashboard provides visual exploration of all the data above.

**No database required** — the API can load snapshots directly from a folder:

```bash
# Terminal 1: Start the API pointing at your snapshots folder
DSL_SNAPSHOTS_DIR=$(pwd)/snapshots dotnet run --project src/DiagnosticStructuralLens.Api --no-launch-profile

# Terminal 2: Start the dashboard
cd dashboard && npm install && npm run dev
```

That's it — two commands, no PostgreSQL, no Docker. The API federates all `.json` files in the snapshots directory on startup.

> **With PostgreSQL (optional):** If you want persistence and time-travel across sessions,
> set the connection string: `ConnectionStrings__DefaultConnection="Host=localhost;Database=dsl;..."`
> as an environment variable, or use `docker compose up` which wires everything together.

Open http://localhost:3000 and explore:

- **C4 navigation** — Drill from Federation (L1) > Repository (L2) > Namespace (L3) > Class (L4)
- **Graph view** — Force-directed layout showing component relationships
- **Treemap** — Hierarchical size visualization
- **Governance mode** — Toggle to see rule violations as pulsing red edges
- **Blast radius** — Click any component to see its downstream impact
- **Time travel** — Upload multiple snapshots to animate architecture evolution

---

## Applying this to your 60+ systems

### Recommended rollout

| Phase | Action | Outcome |
|-------|--------|---------|
| Week 1 | Scan all 60+ repos, generate snapshots | Baseline visibility |
| Week 1 | Federate into global map, load dashboard | First-ever unified view |
| Week 2 | Review reports, identify top 10 riskiest systems | Prioritized backlog |
| Week 2 | Map shared database dependencies | "Who touches what" clarity |
| Week 3 | Define governance rules for your architecture | Codified standards |
| Week 3 | Add `dsl scan` + `dsl report --ci` to pipelines | Automated enforcement |
| Ongoing | Diff snapshots each sprint/release | Track architectural drift |

### Batch scanning script

For your 60+ systems, use a script like this:

```bash
#!/bin/bash
REPOS_DIR="/path/to/all/repos"
SNAPSHOTS_DIR="./snapshots"
mkdir -p "$SNAPSHOTS_DIR"

for repo in "$REPOS_DIR"/*/; do
    name=$(basename "$repo")
    echo "Scanning $name..."
    dsl scan --repo "$repo" --output "$SNAPSHOTS_DIR/$name.json" --ci
done

# Federate all snapshots
snapshots=$(ls "$SNAPSHOTS_DIR"/*.json | tr '\n' ',' | sed 's/,$//')
dsl federate --snapshots "$snapshots" --output "$SNAPSHOTS_DIR/global.json"

echo "Done. Load global.json into the dashboard."
```

### CI pipeline integration

```yaml
# Example: Azure DevOps / GitHub Actions step
- name: DSL Architecture Check
  run: |
    dsl report --repo . --output dsl-report.md --ci
    # Upload report as build artifact
    # Non-zero exit = governance violation → fail the build
```

---

## Quick reference

| Command | Purpose | Key flags |
|---------|---------|-----------|
| `dsl scan` | Extract components + relationships | `--repo`, `--output`, `--include-private`, `--language` |
| `dsl report` | Scan + full architecture analysis | `--repo`, `--output`, `--top N` |
| `dsl federate` | Merge multiple snapshots | `--snapshots` (comma-sep), `--output`, `--strategy` |
| `dsl blast` | Impact radius for a component | `--snapshot`, `--atom`, `--depth` |
| `dsl risk` | Risk scoring report | `--snapshot`, `--format` (text/json/html), `--top N` |
| `dsl diff` | Compare two snapshots | `--baseline`, `--snapshot`, `--format` (text/json/markdown) |
| `dsl publish` | Send snapshot to dashboard API | `--file`, `--url` |
| `dsl interpret` | Human-readable snapshot summary | `--snapshot`, `--output` |
