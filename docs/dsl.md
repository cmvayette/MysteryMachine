# Diagnostic Structural Lens: Design Document

> **Purpose**: A greenfield atomic-level codebase intelligence platform for enterprise legacy modernization, enabling teams to understand, link, and safely evolve interconnected .NET and SQL assets.

---

## 1. System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        CLI Interface                            │
│         scan | diff | blast | risk | update                     │
└─────────────────────────────────────────────────────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
   ┌──────────────┐    ┌──────────────┐    ┌──────────────┐
   │ C# Scanner   │    │ SQL Scanner  │    │ TS Scanner   │
   │   (Roslyn)   │    │ (ScriptDOM)  │    │  (tsc API)   │
   └──────────────┘    └──────────────┘    └──────────────┘
          │                   │                   │
          └───────────────────┼───────────────────┘
                              ▼
                    ┌─────────────────┐
                    │   Atomic Model  │
                    │  (Core Engine)  │
                    └─────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
   ┌──────────────┐    ┌──────────────┐    ┌──────────────┐
   │   Semantic   │    │    Risk      │    │  Federation  │
   │    Linker    │    │   Scorer     │    │    Engine    │
   └──────────────┘    └──────────────┘    └──────────────┘
          │                   │                   │
          └───────────────────┼───────────────────┘
                              ▼
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
   ┌──────────────┐    ┌──────────────┐    ┌──────────────┐
   │ GraphQL API  │    │  Dashboard   │    │  CLI Tools   │
   └──────────────┘    └──────────────┘    └──────────────┘
```

---

## 2. Scanner Architecture: "Atomic Extraction"

### 2.1 Code Scanner (C#/.NET)

**Implementation**: Use Roslyn (`Microsoft.CodeAnalysis`) for AST traversal.

| Atom Type              | Extraction Logic                                                      | Why It Matters                                      |
| ---------------------- | --------------------------------------------------------------------- | --------------------------------------------------- |
| **DTO Classes**        | Heuristics: naming (`*DTO`, `*Request`), `[DataContract]`, no methods | These are the "Contracts" – changes break consumers |
| **Interfaces**         | Extract public interface + method signatures                          | API surface definition                              |
| **Namespace Clusters** | Group types by namespace                                              | Reveals "implicit modules" in monoliths             |

#### DTO Classification Heuristics (priority order):

1. `[DataContract]`, `[Serializable]` attributes
2. Naming convention: `*DTO`, `*Request`, `*Response`, `*ViewModel`
3. Behavioral: Only auto-properties, no logic methods
4. Location: Lives in `*.Contracts`, `*.Models` namespaces

### 2.2 SQL Scanner

**Implementation**: Use `Microsoft.SqlServer.TransactSql.ScriptDom` (open-source, NuGet).

| Atom Type             | Extraction Logic                                         |
| --------------------- | -------------------------------------------------------- |
| **Tables**            | Parse `CREATE TABLE` statements                          |
| **Columns**           | Extract column name, data type, nullability, constraints |
| **Stored Procedures** | Parse proc body for CRUD actions                         |
| **Views**             | Track underlying table dependencies                      |

#### Stored Procedure Analysis:

```csharp
// Using TSqlFragmentVisitor
public class CrudVisitor : TSqlFragmentVisitor
{
    public List<string> Reads { get; } = new();  // SELECT
    public List<string> Writes { get; } = new(); // INSERT/UPDATE/DELETE

    public override void Visit(SelectStatement node) =>
        Reads.AddRange(ExtractTableNames(node));

    public override void Visit(InsertStatement node) =>
        Writes.Add(node.InsertSpecification.Target.ToString());
}
```

#### Input Sources:

- `.sql` files (DDL/DML scripts)
- `.dacpac` packages (SQL Server Data-Tier)
- Live connection (optional): `INFORMATION_SCHEMA` introspection

### 2.3 Attribute Decoder

Extract ORM mapping annotations:

| Framework            | Attributes to Extract                                          |
| -------------------- | -------------------------------------------------------------- |
| **EF Core**          | `[Table("Name")]`, `[Column("Name")]`, `[Key]`, `[ForeignKey]` |
| **Dapper**           | Inline SQL detection: `Query<T>("SELECT ...")` patterns        |
| **Data Annotations** | `[Required]`, `[StringLength]`, `[Range]`                      |

---

## 3. Semantic Linker: "The Thread"

The linker creates cross-domain relationships between code atoms and SQL atoms.

### 3.1 Link Types & Resolution Strategies

| Strategy              | Confidence | Example                                               |
| --------------------- | ---------- | ----------------------------------------------------- |
| **Attribute Binding** | 1.0        | `[Table("Users")]` on `UserEntity` class              |
| **Exact Name Match**  | 0.95       | `User` class ↔ `User` table                           |
| **Fuzzy Name Match**  | 0.7        | `UserDto` ↔ `Users` (singularize/pluralize)           |
| **Query Trace**       | 0.85       | `SELECT Name FROM Users` found in `UserRepository.cs` |
| **Package Manifest**  | 1.0        | NuGet source repo from `.nuspec` repository URL       |

### 3.2 NuGet → Source Repo Bridge

**Research Finding**: Use NuGet Package Source Mapping + `dotnet list package --include-transitive` to build the dependency graph.

```typescript
interface PackageLink {
  packageId: string; // "Company.Common.Models"
  version: string; // "2.1.0"
  sourceRepo?: string; // "https://dev.azure.com/org/CommonModels"
  sourceCommit?: string; // Git SHA for exact traceability
}
```

#### Resolution Strategy:

1. Parse `.nuspec` for `<repository>` element
2. Check internal package registry metadata
3. Fallback: name-based heuristic matching

---

## 4. Risk Scoring ($R_s$) 2.0

### 4.1 Formula

$$R_s = (C_{ext} \times W_{ext}) + (V_{churn} \times W_{vol}) + (F_{delta} \times W_{age})$$

| Factor                 | Symbol      | How to Calculate                                                         |
| ---------------------- | ----------- | ------------------------------------------------------------------------ |
| **External Contracts** | $C_{ext}$   | Count of repos/projects consuming this atom                              |
| **Churn Volatility**   | $V_{churn}$ | Git log: commits touching this file/symbol in last 90 days               |
| **Framework Delta**    | $F_{delta}$ | Target framework version gap (e.g., `net48` vs `net8.0` = 4 generations) |

### 4.2 Version Friction Score

**Research Finding**: Use .NET Portability Analyzer API + target framework parsing from `.csproj`.

```csharp
public class VersionFriction
{
    public string TargetFramework { get; set; }       // "net48"
    public int GenerationGap { get; set; }            // 4 (net48 -> net8.0)
    public List<string> BlockingDependencies { get; set; } // Packages not available on modern .NET
    public double PortabilityScore { get; set; }      // 0.0 - 1.0
}
```

### 4.3 Risk Tiers

| Score Range | Tier     | UI Color  | Meaning                        |
| ----------- | -------- | --------- | ------------------------------ |
| 0-25        | Low      | 🟢 Green  | Safe to change                 |
| 26-50       | Medium   | 🟡 Yellow | Requires review                |
| 51-75       | High     | 🟠 Orange | Cross-team coordination needed |
| 76-100      | Critical | 🔴 Red    | Breaking change likely         |

---

## 5. User Experience: Knowledge Graph & Visual Strategy

We map user questions to specific "Zoom Levels" (L1-L4) in the UI to prevent data overload and provide relevant insights at each altitude.

### 5.1 Layer 1: Federation (The CTO / Architect View)

_Scope: Multi-Repository / Portfolio_

| Core Question      | "Where is the complexity concentrated in my organization?"                                                 |
| :----------------- | :--------------------------------------------------------------------------------------------------------- |
| **Nodes**          | **Repositories** (e.g., `PatientPortal`, `BillingService`)                                                 |
| **Edges**          | **Cross-Repo Links** (API Calls, Shared Packages, Database Links)                                          |
| **Insights**       | Identify "God Repos", detect "Hidden Monoliths" (cyclic repo dependencies), and visualize team boundaries. |
| **UI Interaction** | **Node Size** proportional to complexity; **Color** by Risk Score. Click for high-level stats.             |

### 5.2 Layer 2: System / Repository (The Tech Lead View)

_Scope: Single Repository (High Level)_

| Core Question      | "How is this application vertically sliced?"                                                                             |
| :----------------- | :----------------------------------------------------------------------------------------------------------------------- |
| **Nodes**          | **Containers** (Logical Projects/Modules, e.g., `API`, `Domain`, `Infra`)                                                |
| **Edges**          | **Project References** & Logical Dependencies                                                                            |
| **Insights**       | Verify Clean Architecture (e.g., `Domain` !-> `Infra`), identify circular project refs, see the "Shape" of the monorepo. |
| **UI Interaction** | **Governance Mode** highlights illegal edges in Red. **Clustering** groups containers by functional area.                |

### 5.3 Layer 3: Component / Namespace (The Senior Dev View)

_Scope: Functional Area_

| Core Question      | "What logic exists in this module and how is it organized?"                 |
| :----------------- | :-------------------------------------------------------------------------- |
| **Nodes**          | **Namespaces** (e.g., `Services.Orders`, `Data.Repositories`)               |
| **Edges**          | **Namespace Imports** / Usage                                               |
| **Insights**       | Detect "Utils" dumping grounds (High In-degree) and access visual cohesion. |
| **UI Interaction** | **Drill Down** (Double-click) enters specific namespace.                    |

### 5.4 Layer 4: Atom / Class (The Developer View)

_Scope: Class / Interface / Method_

| Core Question      | "What connects to THIS specific class?"                                                   |
| :----------------- | :---------------------------------------------------------------------------------------- |
| **Nodes**          | **Atoms** (Classes, Interfaces, Enums, Methods)                                           |
| **Edges**          | **Type Dependencies** (Inheritance, Usage, Fields)                                        |
| **Insights**       | **Blast Radius** (Ripple effect), **Refactoring Candidates**, **Orphans** (Dead code).    |
| **UI Interaction** | **Blast Radius Toggle** dims unrelated nodes; **Side Panel** shows granular code details. |

### 5.5 Special Feature Overlays

These modes cut across layers to answer specific types of questions.

#### 💥 Blast Radius Mode

- **Goal:** Impact Analysis.
- **UI:** Select a Node -> Highlights downstream dependents (transitive dependencies) up to N hops.
- **Visuals:** Red "Ripple" effect.
- **Layer:** Available at **L4 (Atom)** primarily, but aggregates impact at L1/L2.

#### 📜 Governance / Rules Mode

- **Goal:** Architectural Enforcement.
- **UI:** User toggles "Governance".
- **Visuals:**
  - **Green Edges**: Compliant with architectural rules.
  - **Red Edges**: Violations (e.g., Circular Dependency, Layer Bypass).
  - **Thick Edges**: High volume of violations.

#### ⏱️ Time Travel / Diff Mode

- **Goal:** Historical Evolution.
- **UI:** Timeline slider at bottom.
- **Visuals:**
  - **Green Nodes/Edges**: Added in this snapshot.
  - **Red Nodes/Edges**: Deleted in this snapshot.
  - **Yellow Nodes/Edges**: Modified (Signature change).

---

## 6. Federation: Multi-Team Workflows

### 6.1 Namespace Reconciliation

When multiple teams scan different repos with shared namespaces:

```typescript
interface ReconciliationResult {
  namespace: string; // "Company.Common.Data"
  sources: string[]; // ["RepoA", "RepoB"]
  atoms: Array<{
    name: string;
    definitions: Array<{
      repo: string;
      signature: string;
      hash: string; // For exact comparison
    }>;
    status: "Identical" | "Conflict" | "Unique";
  }>;
}
```

#### Merge Rules:

1. **Identical signature + hash** → Auto-merge
2. **Different signatures** → Flag as conflict, require manual resolution
3. **Unique to one repo** → Add to global map

### 6.2 Staging & Diff

Before adding a scan to the "Global Map":

```typescript
interface StagingReport {
  snapshotId: string;
  proposedChanges: {
    added: AtomSummary[];
    modified: AtomSummary[];
    removed: AtomSummary[];
  };
  breakingChanges: Array<{
    atom: AtomId;
    reason: string; // "Method signature changed"
    affectedConsumers: number;
  }>;
  recommendation: "AutoMerge" | "ReviewRequired" | "BlockMerge";
}
```

---

## 7. Technology Stack (Recommended)

| Layer            | Technology                           | Rationale                                             |
| ---------------- | ------------------------------------ | ----------------------------------------------------- |
| **Code Scanner** | Roslyn (C#), TypeScript Compiler API | First-party AST access                                |
| **SQL Scanner**  | ScriptDOM (T-SQL)                    | Microsoft-supported, open-source, prod-proven         |
| **Storage**      | SQL Server (existing infra)          | Leverage team expertise + graph table support (2017+) |
| **API**          | GraphQL (HotChocolate for .NET)      | Flexible querying for UI                              |
| **Dashboard**    | React + D3.js or Vite + ECharts      | Interactive visualizations                            |
| **CLI**          | .NET CLI (`System.CommandLine`)      | CI/CD integration                                     |

### 7.1 SQL Server Graph Tables

SQL Server 2017+ supports native graph database capabilities via `NODE` and `EDGE` tables – ideal for dependency modeling without introducing new infrastructure:

```sql
-- Node tables (atoms)
CREATE TABLE dbo.CodeAtom (
    AtomId INT PRIMARY KEY,
    Name NVARCHAR(500),
    AtomType NVARCHAR(50),        -- 'DTO', 'Interface', 'Class', 'Method'
    Namespace NVARCHAR(500),
    Repository NVARCHAR(200),
    Signature NVARCHAR(MAX),
    TargetFramework NVARCHAR(50),
    RiskScore DECIMAL(5,2)
) AS NODE;

CREATE TABLE dbo.SqlAtom (
    AtomId INT PRIMARY KEY,
    Name NVARCHAR(500),
    AtomType NVARCHAR(50),        -- 'Table', 'Column', 'StoredProcedure'
    ParentTable NVARCHAR(500),
    DataType NVARCHAR(100),
    IsNullable BIT
) AS NODE;

-- Edge tables (relationships)
CREATE TABLE dbo.DependsOn (
    LinkType NVARCHAR(50),        -- 'Calls', 'Inherits', 'References'
    Confidence DECIMAL(3,2)
) AS EDGE;

CREATE TABLE dbo.MapsTo (
    LinkType NVARCHAR(50),        -- 'NameMatch', 'Attribute', 'QueryTrace'
    Confidence DECIMAL(3,2)
) AS EDGE;
```

#### Graph Query Example (Blast Radius – find all consumers of a DTO):

```sql
SELECT Consumer.Name, Consumer.Repository, COUNT(*) AS HopCount
FROM dbo.CodeAtom AS Target, dbo.DependsOn, dbo.CodeAtom AS Consumer
WHERE MATCH(Consumer-(DependsOn)->Target)
  AND Target.Name = 'UserDTO'
GROUP BY Consumer.Name, Consumer.Repository
ORDER BY HopCount DESC;
```

#### Multi-hop Traversal (transitive dependencies):

```sql
-- Find "friend of friend" style relationships
SELECT Target.Name, Intermediate.Name, Consumer.Name
FROM dbo.CodeAtom AS Target,
     dbo.DependsOn AS D1,
     dbo.CodeAtom AS Intermediate,
     dbo.DependsOn AS D2,
     dbo.CodeAtom AS Consumer
WHERE MATCH(Consumer-(D2)->Intermediate-(D1)->Target)
  AND Target.Name = 'CoreDataModel';
```

---

## 8. Deliverables & Dependencies

### 8.1 Deliverable Summary

```
┌─────────────────────────────────────────────────────────────────┐
│  Phase 4: Scale                                                 │
├─────────────────────────────────────────────────────────────────┤
│  Phase 3: Experience                                            │
├─────────────────────────────────────────────────────────────────┤
│  Phase 2: Intelligence                                          │
├─────────────────────────────────────────────────────────────────┤
│  Phase 1: Foundation                                            │
└─────────────────────────────────────────────────────────────────┘

 Phase 1: Foundation          Phase 2: Intelligence
 ┌──────────────────┐         ┌──────────────────┐
 │ D1 Core Library  │────────▶│ D5 Semantic      │
 └────────┬─────────┘         │    Linker        │
          │                   └────────┬─────────┘
    ┌─────┴─────┐                      │
    ▼           ▼                      ▼
 ┌──────┐   ┌──────┐          ┌──────────────────┐
 │ D2   │   │ D3   │          │ D6 Risk Scorer   │
 │ C#   │   │ SQL  │          └────────┬─────────┘
 └──┬───┘   └──┬───┘                   │
    └─────┬────┘               Phase 3: Experience
          ▼                            ▼
    ┌──────────┐              ┌──────────────────┐
    │ D4 CLI   │              │ D7 GraphQL API   │
    └──────────┘              └────────┬─────────┘
                                       ▼
                              ┌──────────────────┐
                              │ D8 Web Dashboard │
                              └──────────────────┘

 Phase 4: Scale
 ┌──────────────────┐
 │ D9 Federation    │◀──── (from D5 Semantic Linker)
 └──────────────────┘
```

### 8.2 Deliverables Detail

| ID  | Deliverable       | Description                                                | Est. Effort |
| --- | ----------------- | ---------------------------------------------------------- | ----------- |
| D1  | Core Library      | Atomic Model types, storage abstractions, shared utilities | 1 week      |
| D2  | C# Scanner        | Roslyn-based DTO/Interface/Namespace extraction            | 2 weeks     |
| D3  | SQL Scanner       | ScriptDOM-based table/column/proc extraction               | 2 weeks     |
| D4  | CLI Tool          | `dsl scan`, `link`, `diff` commands                        | 1 week      |
| D5  | Semantic Linker   | Name matching, attribute decoding, query tracing           | 2 weeks     |
| D6  | Risk Scorer       | Blast radius, churn, version friction calculation          | 1.5 weeks   |
| D7  | GraphQL API       | HotChocolate server exposing atoms/links/risk              | 1.5 weeks   |
| D8  | Web Dashboard     | React + D3 visualizations (Ripple, Heatmap)                | 3 weeks     |
| D9  | Federation Engine | Multi-repo merge, namespace reconciliation                 | 2 weeks     |

> **Total Estimate**: ~16 weeks (4 months) for full scope

### 8.3 External Dependencies (NuGet Packages)

| Package                                     | Version | Purpose                       |
| ------------------------------------------- | ------- | ----------------------------- |
| `Microsoft.CodeAnalysis.CSharp`             | 4.x     | Roslyn AST for C# parsing     |
| `Microsoft.SqlServer.TransactSql.ScriptDom` | 161.x   | T-SQL parsing (open-source)   |
| `System.CommandLine`                        | 2.x     | CLI framework                 |
| `HotChocolate`                              | 14.x    | GraphQL server for .NET       |
| `Dapper`                                    | 2.x     | Lightweight SQL Server access |
| `Microsoft.Data.SqlClient`                  | 5.x     | SQL Server connectivity       |
| `Humanizer`                                 | 2.x     | Pluralization/singularization |

### 8.4 Internal Dependencies (Build Order)

```
DiagnosticStructuralLens.Core ← No dependencies (foundation types)
        │
        ├──────────────────────┬────────────────────────┐
        ▼                      ▼                        ▼
DiagnosticStructuralLens       DiagnosticStructuralLens      DiagnosticStructuralLens
  .Scanner.CSharp          .Scanner.Sql             .Linker
  ← Core + Roslyn          ← Core + ScriptDOM       ← Core + Humanizer
        │                      │                        │
        └──────────┬───────────┘                        │
                   ▼                                    │
           DiagnosticStructuralLens.Linker ◀──────────────────┘
                   │
                   ▼
           DiagnosticStructuralLens.Risk
           ← Core + Linker
                   │
        ┌──────────┴──────────┐
        ▼                     ▼
DiagnosticStructuralLens      DiagnosticStructuralLens.Cli
  .Api                  ← Core + Scanners + Linker + Risk
  ← Core + Risk +
    HotChocolate
        │
        ▼
DiagnosticStructuralLens.Web
← API (frontend, consumes GraphQL)

DiagnosticStructuralLens.Federation
← Core + Linker (multi-repo merge)
```

### 8.5 Infrastructure Dependencies

| Dependency       | Required By    | Notes                                            |
| ---------------- | -------------- | ------------------------------------------------ |
| SQL Server 2017+ | Storage        | Graph tables require 2017+; ensure compatibility |
| ADO/GitHub repos | Federation     | Access to scan multiple repositories             |
| NuGet feeds      | Package Bridge | Internal feed + optionally NuGet.org             |
| Git CLI          | Churn analysis | For `git log` integration (optional Phase 2)     |

### 8.6 Deliverable Dependencies Matrix

| Deliverable          | Depends On | Blocks                     |
| -------------------- | ---------- | -------------------------- |
| D1 Core Library      | —          | D2, D3, D4, D5, D6, D7, D9 |
| D2 C# Scanner        | D1         | D4, D5                     |
| D3 SQL Scanner       | D1         | D4, D5                     |
| D4 CLI Tool          | D1, D2, D3 | (User-facing, no blockers) |
| D5 Semantic Linker   | D1, D2, D3 | D6, D9                     |
| D6 Risk Scorer       | D1, D5     | D7                         |
| D7 GraphQL API       | D1, D6     | D8                         |
| D8 Web Dashboard     | D7         | (User-facing, no blockers) |
| D9 Federation Engine | D1, D5     | (User-facing, no blockers) |

### 8.7 Implementation Status (Feb 2026)

> **Note**: Project rebranded from "Diagnostic Structural Lens" to **Diagnostic Structural Lens** in Feb 2026.

| Feature                       | Status      |
| ----------------------------- | ----------- |
| C# DTO/Interface scanning     | ✅ Complete |
| SQL Table/Column extraction   | ✅ Complete |
| CLI scan command              | ✅ Complete |
| Name-based linking (C# ↔ SQL) | ✅ Complete |
| Attribute-based linking       | ✅ Complete |
| Risk scoring                  | ✅ Complete |
| GraphQL API (HotChocolate 14) | ✅ Complete |
| Web Dashboard (React + D3)    | ✅ Complete |
| Federation Engine             | ✅ Complete |
| TypeScript Scanner            | ✅ Complete |

#### Dashboard Evolution (Tier 4 - Feb 2026)

- **Muted Flat Palette**: Desaturated tones to reduce cognitive strain
- **Geometric Node Shapes**: Circles, Hexagons, Diamonds, Pentagons for instant type recognition
- **C4 Drill-Down Navigation**: L1 (Federation) → L2 (Repository) → L3 (Namespace) → L4 (Atom)
- **Floating Controls**: On-graph Blast Radius and Link toggles
- **Auto-Drill**: Defaults to L2 (Repository view) on load for immediate value

---

## 9. Resolved Decisions

| Question       | Decision               | Rationale                                   |
| -------------- | ---------------------- | ------------------------------------------- |
| SQL Dialect    | T-SQL only             | Team uses SQL Server exclusively            |
| Storage        | SQL Server (existing)  | Leverage existing infra + graph tables      |
| Package Bridge | Internal packages only | Reduces scope; public packages add noise    |
| Federation     | Central golden source  | Single SQL Server DB, simplest for 20+ team |
| Deployment     | Self-hosted            | Airgapped/enterprise environment            |

---

## 10. CI/CD Integration: PR-Based Scanning

The CLI will run on every PR to keep the global map current.

### 10.1 Pipeline Flow

```
┌──────────────┐     ┌───────────────────┐     ┌─────────────────────────┐     ┌────────────────┐
│ Pull Request │────▶│ dsl scan │────▶│ dsl diff --base│────▶│ Comment on PR  │
└──────────────┘     └───────────────────┘     │          main           │     └────────────────┘
                                               └─────────────────────────┘

┌──────────┐     ┌─────────────────────────┐     ┌──────────────┐
│  Merge   │────▶│ dsl update --db│────▶│  SQL Server  │
└──────────┘     └─────────────────────────┘     └──────────────┘
```

### 10.2 CLI Commands for CI/CD

```bash
# 1. Scan the repo (produces snapshot JSON)
dsl scan --repo ./src --output ./snapshot.json

# 2. Diff against baseline (detect breaking changes)
dsl diff --baseline main --snapshot ./snapshot.json --output ./diff.json

# 3. Post-merge: Update central database
dsl update --snapshot ./snapshot.json --connection "Server=..."
```

### 10.3 Azure DevOps Pipeline Example

```yaml
trigger:
  branches:
    include: [main]

pr:
  branches:
    include: [main]

stages:
  - stage: Scan
    jobs:
      - job: DiagnosticStructuralLens
        steps:
          - task: UseDotNet@2
            inputs:
              version: "8.x"

          - script: |
              dotnet tool install --global DiagnosticStructuralLens.Cli
              dsl scan --repo $(Build.SourcesDirectory) --output $(Build.ArtifactStagingDirectory)/snapshot.json
            displayName: "Scan Repository"

          - script: |
              dsl diff --baseline $(Build.SourceBranch) --snapshot $(Build.ArtifactStagingDirectory)/snapshot.json
            displayName: "Detect Breaking Changes"
            condition: eq(variables['Build.Reason'], 'PullRequest')

          - script: |
              dsl update --snapshot $(Build.ArtifactStagingDirectory)/snapshot.json --connection "$(DiagnosticStructuralLensDbConnection)"
            displayName: "Update Global Map"
            condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
```

### 10.4 PR Comment Output

The `dsl diff` command will output a markdown summary suitable for PR comments:

```markdown
## 🗺️ Diagnostic Structural Lens Report

### Breaking Changes Detected: 2

| Atom                            | Type              | Impact                                               |
| ------------------------------- | ----------------- | ---------------------------------------------------- |
| `UserDTO.EmailAddress`          | Field Removed     | 3 consumers in `OrderService`, `NotificationService` |
| `IPaymentGateway.ProcessRefund` | Signature Changed | 1 consumer in `RefundProcessor`                      |

### New Contracts: 5

- `CustomerPreferencesDTO` (DTO)
- `IInventoryService.CheckStock` (Interface Method)
- ...

### SQL Schema Changes: 1

- `Orders.ShippingStatus` column added (NVARCHAR(50))
```
