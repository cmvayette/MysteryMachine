# DSL: C4 Model Abstraction Mapping Standards

**Status**: Refined Feb 6, 2026  
**Context**: Architectural Legibility & C4 Compliance

## 1. The Challenge: Abstraction Mismatch

The Diagnostic Structural Lens (DSL) provides deep visibility into codebases, but ensuring this visibility aligns with the **C4 Model** (Context, Containers, Components, Code) requires precise mapping of physical and logical units.

An initial implementation gap was identified where the system jumped from **Repository** (System) directly to **Namespaces** (Components), skipping the **Project/Container** layer. In .NET ecosystems, the "Container" in C4 terms is best represented by the `Project` (.csproj) as it typically represents a deployable or bounded functional unit.

## 2. Definitive Mapping Standard

To achieve full C4 compliance, DSL adheres to the following abstraction hierarchy:

| C4 Level          | DSL Entity                       | Mapping Logic                                            | Scope              |
| :---------------- | :------------------------------- | :------------------------------------------------------- | :----------------- |
| **L1: Context**   | **Federation**                   | The entire system landscape / suite of applications.     | Multi-Repo         |
| **L2: Container** | **Project** (.csproj/.sqlproj)   | Deployable units, datastores, and executable boundaries. | Repo-Internal      |
| **L3: Component** | **Namespace**                    | Logical groupings within a container.                    | Project-Internal   |
| **L4: Code**      | **Atom** (Class/Interface/Table) | The individual implementation units.                     | Namespace-Internal |

### 2.1 The Role of the "Repository"

In a single-repo system, the **Repository** effectively maps to the **System** (L1 Context). In a multi-repo federation, the Repository is a high-level grouping that may contain multiple **Containers** (Projects).

## 3. Implementation Requirements for Compliance

### 3.1 Data Exposure (The "Project" Layer)

To maintain C4 structural integrity without requiring immediate backend schema refactoring, the "Project" (Container) layer is implemented as a **Frontend Navigation Tier**.

- **Heuristic Container Inference**: Namespaces are dynamically grouped into Projects based on their naming convention.
  - **Logic**: Use the first two segments of the dotted namespace path (e.g., `DiagnosticStructuralLens.Api.Controllers` -> `DiagnosticStructuralLens.Api`).
  - **Fallback**: If the path has only one segment, that segment becomes the Container name.
- **Dashboard Navigation**:
  - The navigation state utilizes a `path` array (e.g., `['RepoId', 'ProjectPrefix', 'NamespacePath']`) to maintain context through the drill-down.
  - Clicking a Repository drills into a **Project View** (showing inferred containers).
  - Clicking a Container node drills into the **Namespace View** for that specific prefix.

### 3.2 Visual Distinctions

Containers (Projects) should be visually distinct from logical Components (Namespaces).

- **Iconography**: Containers use "box" or "package" icons; Components use "folder" icons.
- **Physics**: Projects act as the primary "cohesion anchors" for namespaces in the force-directed layout.

## 4. Current Gaps & Remediation

- **Gap**: `App.tsx` navigation previously skipped the Project level.
- **Gap**: `Types.cs` / `Query.cs` does not currently expose a `ProjectNode`.
- **Remediation (Applied)**: Implemented "Level 2: Container" via client-side inference in `App.tsx`.
- **Long-term Fix**: Update backend scanners (Roslyn/ScriptDOM) to extract project-level metadata (`.csproj`/`.sqlproj`) and expose via a formal `Project` type in GraphQL.
