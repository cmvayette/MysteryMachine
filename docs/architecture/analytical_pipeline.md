# DSL: Analytical Pipeline & Information Extraction

The DSL analytical pipeline is the engine that transforms source code and SQL into a structured, federated Knowledge Graph. It operates through three main stages: Scanning, Linking, and Snapshot Generation.

## 1. Multi-Language Scanning (L0)

The CLI employs language-specific scanners that leverage high-fidelity parsing APIs.

### 1.1 C# Scanner (Roslyn)

- **Engine**: `Microsoft.CodeAnalysis.CSharp`.
- **Logic**: Traverses the AST for Types, Enums, and Members.
- **Classification**: Uses naming conventions (e.g., `*Dto`) and attributes (e.g., `[DataContract]`) to distinguish DTOs from standard Classes.
- **Dapper Detection**: Scans method bodies for SQL invocation patterns to extract inline queries for cross-layer tracing.

### 1.2 SQL Scanner (ScriptDOM)

- **Engine**: `Microsoft.SqlServer.TransactSql.ScriptDom`.
- **Logic**: Parses T-SQL scripts to extract Tables, Columns, Stored Procedures, and Functions.
- **Normalization**: Schema/Object names are normalized to support clean linkage.

## 2. ID Generation Strategy

Consistency in IDs is the "glue" of the DSL ecosystem. If IDs differ between the stage where an Atom is created and where a Link references it, the edge will be lost during GraphQL resolution.

- **Primary Pattern**: `namespace.name` (normalized).
- **Transformation**:
  1.  Convert to lowercase.
  2.  Replace dots (`.`) with dashes (`-`).
  - _Example_: `DSL.Core.Snapshot` -> `dsl-core-snapshot`.
- **Member IDs**: `{ParentId}-{MemberName}` (e.g., `dsl-core-snapshot-id`).

## 3. Semantic Link Synthesis

The `SemanticLinker` runs after scanners provide the raw atoms.

| Strategy        | Logic                                                                         | Confidence |
| :-------------- | :---------------------------------------------------------------------------- | :--------- |
| **Attribute**   | Links via `[Table]` or `[Column]` attribute values.                           | 1.0 (High) |
| **Exact Match** | Direct case-insensitive match between Class and Table.                        | 0.95       |
| **Fuzzy Match** | Pluralization/Singularization (e.g., `User` -> `Users`) and suffix stripping. | 0.85       |
| **Signature**   | Matches method return/parameter types to known DTOs or Entities.              | 1.0        |
| **Query Trace** | Links code to tables found in extracted Dapper SQL strings.                   | 0.85       |

## 4. Pipeline Integration (CLI)

The `ExecuteScan` method in `Program.cs` orchestrates the results:

1.  Run Scanners (CSharp + SQL).
2.  Run `SemanticLinker.LinkAtoms`.
3.  Run `GovernanceEngine` (Rule validation).
4.  Generate `Snapshot` metadata (Atoms/Links counts, SHA, Branch).
5.  Serialize to `snapshot.json`.

## 5. Scan Control & Scope

To manage accuracy and visual noise, the scanner's search scope must be carefully controlled.

- **Directory Isolation**: Scanning the root of a monorepo can inadvertently pull in `validation/` or `tests/` folders containing massive external codebases (e.g., eShop, nopCommerce). This results in "Scale Pollution" (e.g., jumping from 400 to 20,000 atoms).
- **Targeted Scanning**: Use the `--repo <path>` flag to target only the production source (e.g., `--repo src`).
- **Exclusion Patterns**: The scanner defaults to excluding `obj`, `bin`, and `node_modules`, but custom patterns should be used for locally cached datasets or benchmark artifacts.
