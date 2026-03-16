# DiagnosticStructuralLens

**Architecture analysis CLI for .NET codebases.** Scans C#, SQL, and TypeScript source code to produce a comprehensive architecture report with migration readiness assessment, risk scoring, and CI/CD pipeline gating.

## Quick Start

```bash
# Clone and build
git clone <repo-url> DiagnosticStructuralLens
cd DiagnosticStructuralLens
dotnet build

# Run against a repository
dotnet run --project src/DiagnosticStructuralLens.Cli -- report --repo /path/to/your/repo
```

This generates a `dsl-report.md` in the target repo with:

- Component inventory and taxonomy
- Architecture breakdown by namespace
- Centrality analysis (highest-impact nodes)
- Risk scoring
- Governance violations
- **Architecture findings** (god classes, circular deps, missing interfaces)
- **Migration readiness** (.NET Framework → .NET Core patterns)
- **Modernization opportunities** (sync I/O, legacy data access)

---

## Installation

### Option A: Install as a .NET Tool (recommended)

```bash
# From a local NuGet package
dotnet pack src/DiagnosticStructuralLens.Cli -c Release -o ./nupkg
dotnet tool install --global --add-source ./nupkg DiagnosticStructuralLens.Cli

# Now available globally
dsl report --repo /path/to/your/repo
```

### Option B: Run from source

```bash
dotnet run --project src/DiagnosticStructuralLens.Cli -- report --repo /path/to/your/repo
```

---

## Commands

### `dsl report` — Full Architecture Report

```bash
dsl report --repo /path/to/repo [options]
```

| Flag                   | Description                                    | Default                  |
| ---------------------- | ---------------------------------------------- | ------------------------ |
| `--repo <path>`        | Repository to analyze (required)               | —                        |
| `--output <path>`      | Output markdown file path                      | `<repo>/dsl-report.md`   |
| `--output-json <path>` | Structured JSON results (for CI/CD)            | —                        |
| `--policy <path>`      | Policy file for quality gates                  | `<repo>/dsl-policy.yaml` |
| `--ci`                 | CI mode: returns exit code 1 on policy failure | off                      |
| `--include-private`    | Include private members in scan                | off                      |
| `--top <n>`            | Number of items in top-N lists                 | 10                       |

### Other Commands

| Command         | Description                            |
| --------------- | -------------------------------------- |
| `dsl scan`      | Scan only — produces a snapshot JSON   |
| `dsl diff`      | Compare two snapshots                  |
| `dsl risk`      | Risk analysis from a snapshot          |
| `dsl blast`     | Calculate impact radius for an element |
| `dsl interpret` | Natural language interpretation        |

---

## Analyzer Rules

### Migration (`MIG-*`) — .NET Framework → .NET Core

| Rule      | Pattern                                   | Severity    |
| --------- | ----------------------------------------- | ----------- |
| `MIG-001` | `System.Web` / `HttpContext.Current`      | 🔴 Critical |
| `MIG-002` | WCF `[ServiceContract]` / `.svc` files    | 🔴 Critical |
| `MIG-003` | ASMX `.asmx` web services                 | 🔴 Critical |
| `MIG-004` | WebForms `.aspx` / `.ascx`                | 🔴 Critical |
| `MIG-005` | .NET Remoting / `MarshalByRefObject`      | 🔴 Critical |
| `MIG-006` | `Global.asax` lifecycle hooks             | 🟠 High     |
| `MIG-007` | `ConfigurationManager.AppSettings`        | 🟠 High     |
| `MIG-008` | `IHttpModule` / `IHttpHandler`            | 🟠 High     |
| `MIG-009` | `System.Drawing`                          | 🟠 High     |
| `MIG-010` | `AppDomain.CreateDomain`                  | 🟠 High     |
| `MIG-011` | `FormsAuthentication`                     | 🟠 High     |
| `MIG-012` | `Session["key"]` state access             | 🟠 High     |
| `MIG-013` | Entity Framework 6 (`System.Data.Entity`) | 🟡 Medium   |
| `MIG-014` | `packages.config` file                    | 🟡 Medium   |
| `MIG-015` | COM Interop / `[DllImport]`               | 🟠 High     |
| `MIG-016` | `Assembly.LoadFrom` / `Assembly.Load`     | 🟡 Medium   |

### Architecture (`ARCH-*`)

| Rule       | Pattern                                    | Severity  |
| ---------- | ------------------------------------------ | --------- |
| `ARCH-001` | God class (30+ methods or 50+ properties)  | 🟠 High   |
| `ARCH-002` | Circular namespace dependencies            | 🟠 High   |
| `ARCH-003` | Missing interface on high-fan-in class     | 🟡 Medium |
| `ARCH-004` | Controller with 500+ LOC                   | 🟡 Medium |
| `ARCH-005` | Static class with 10+ methods              | 🟡 Medium |
| `ARCH-006` | Singleton anti-pattern (static `Instance`) | 🟡 Medium |
| `ARCH-007` | Service Locator (`ServiceLocator.Current`) | 🟠 High   |

### Modernization (`MOD-*`)

| Rule      | Pattern                                    | Severity  |
| --------- | ------------------------------------------ | --------- |
| `MOD-001` | Synchronous data access (no async methods) | 🟡 Medium |
| `MOD-002` | `DataSet` / `DataTable` usage              | 🟡 Medium |
| `MOD-003` | Hard-coded connection strings              | 🟠 High   |
| `MOD-004` | SOAP / `XmlSerializer` + `WebRequest`      | 🟡 Medium |
| `MOD-005` | `[OutputCache]` attribute                  | 🟢 Low    |
| `MOD-006` | `Thread.Sleep` in production code          | 🟡 Medium |

---

## CI/CD Integration (Azure DevOps)

### 1. Add a policy file to your repo

Copy `dsl-policy.yaml.example` to your repo root as `dsl-policy.yaml` and adjust thresholds:

```yaml
# dsl-policy.yaml
version: 1

gates:
  migration:
    max_critical: 0
    max_high: 10
  architecture:
    max_critical: 0
    max_high: 5
    max_god_classes: 0
  risk:
    max_critical_components: 3
    max_high_components: 10
  governance:
    max_violations: 0

# Suppress rules you're tracking separately
suppress:
  - MIG-014 # packages.config conversion tracked in backlog
```

### 2. Add the pipeline

See `azure-pipelines.yml.example` for a ready-to-use pipeline configuration that:

- Runs on every PR to `main` / `develop`
- Installs `dsl` as a .NET tool
- Runs the architecture report with policy gating
- Publishes the report as a build artifact
- Returns exit code 1 on policy failure (blocks the PR)

### Exit Codes

| Code | Meaning            | Pipeline Effect         |
| ---- | ------------------ | ----------------------- |
| `0`  | All gates pass     | ✅ Continue             |
| `1`  | Policy gate failed | ❌ Block merge          |
| `2`  | Scan error         | ❌ Infrastructure issue |

---

## Governance Rules

Place a `governance.yaml` in your repo root to define architectural constraints:

```yaml
layers:
  - name: Controllers
    pattern: "*.Controllers.*"
  - name: Services
    pattern: "*.Services.*"
  - name: Data
    pattern: "*.Repositories.*"

rules:
  - from: Controllers
    to: Data
    deny: true
    reason: "Controllers must not access repositories directly — use services"
```

---

## Requirements

- **.NET 8 SDK** (for building and running)
- **Git** (for branch/commit metadata in reports)

---

## Project Structure

```
src/
├── DiagnosticStructuralLens.Cli/          # CLI entry point + report generator + analyzers
│   └── Analyzers/                         # Migration, architecture, modernization analyzers
├── DiagnosticStructuralLens.Core/         # Shared models, governance engine
├── DiagnosticStructuralLens.Scanner.CSharp/   # Roslyn-based C# scanner
├── DiagnosticStructuralLens.Scanner.Sql/      # SQL file scanner
├── DiagnosticStructuralLens.Scanner.TypeScript/ # TypeScript scanner
├── DiagnosticStructuralLens.Linker/       # Semantic relationship linker
├── DiagnosticStructuralLens.Risk/         # Risk scoring engine
└── DiagnosticStructuralLens.Federation/   # Multi-repo federation
```

## License

MIT
