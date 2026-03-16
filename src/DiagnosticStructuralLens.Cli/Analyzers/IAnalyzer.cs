using DiagnosticStructuralLens.Core;

namespace DiagnosticStructuralLens.Cli.Analyzers;

/// <summary>
/// Interface for all architecture analyzers.
/// </summary>
public interface IAnalyzer
{
    FindingCategory Category { get; }
    IReadOnlyList<AnalyzerFinding> Analyze(
        string repoPath,
        Snapshot snapshot,
        List<ScanDiagnostic> diagnostics
    );
}
