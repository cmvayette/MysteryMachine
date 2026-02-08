# Walkthrough: Git Root Repository Grouping Fix

## Problem

The L1 context layer showed 58 items (one per `.csproj` directory) instead of the 4 actual Git repositories. Root cause: `Snapshot.Repository` was set to the full filesystem path, so separate sub-directory scans produced distinct identifiers.

## Changes Made

### 1. CLI — Git Root Detection

[Program.cs](file:///Users/baxter/devProject/DiagnosticStructuralLens/src/DiagnosticStructuralLens.Cli/Program.cs#L905-L922)

Added `DetectGitRepoName()` — walks up from the scanned path to find `.git`, returns that directory's name as the canonical repo identifier. Falls back to the leaf directory name.

```diff
- Repository = fullRepoPath,
+ Repository = DetectGitRepoName(fullRepoPath),
```

### 2. Core Model

[Snapshot.cs](file:///Users/baxter/devProject/DiagnosticStructuralLens/src/DiagnosticStructuralLens.Core/Snapshot.cs#L9)

Changed `Repository` from `init` to `set` to allow API-side normalization of legacy data.

### 3. API — Legacy Path Normalization

[Program.cs](file:///Users/baxter/devProject/DiagnosticStructuralLens/src/DiagnosticStructuralLens.Api/Program.cs#L128-L136)

Added normalization in both the `/load` endpoint and DB hydration that strips filesystem paths to just the directory name. Uses `System.IO.Path` (qualified to avoid `HotChocolate.Path` conflict).

### 4. Frontend — Path-Safe Inference

[systemInference.ts](file:///Users/baxter/devProject/DiagnosticStructuralLens/dashboard/src/utils/systemInference.ts#L17-L23)

Updated `inferSystems()` to detect path-style repo names and extract the basename before applying the dot-segment grouping heuristic.

## Verification

| Check                | Result                      |
| -------------------- | --------------------------- |
| `dotnet build`       | ✅ 0 errors                 |
| `vitest run`         | ✅ 56/56 tests pass         |
| Manual context layer | Pending re-scan/API restart |

## Next Step

Re-scan repos with the updated CLI and restart the API to see the corrected context layer, or just restart the API to see normalization of existing DB entries.
