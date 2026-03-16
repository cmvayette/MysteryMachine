using DiagnosticStructuralLens.Core;

namespace DiagnosticStructuralLens.Cli.Analyzers;

/// <summary>
/// Orchestrates all registered analyzers and collects findings.
/// </summary>
public class AnalyzerRunner
{
    private readonly List<IAnalyzer> _analyzers =
    [
        new MigrationAnalyzer(),
        new ArchitectureAnalyzer(),
        new ModernizationAnalyzer()
    ];

    public AnalysisReport Run(string repoPath, Snapshot snapshot, List<ScanDiagnostic> diagnostics)
    {
        var report = new AnalysisReport();

        foreach (var analyzer in _analyzers)
        {
            var findings = analyzer.Analyze(repoPath, snapshot, diagnostics);
            report.Findings.AddRange(findings);
        }

        // Sort: critical first, then by category
        report.Findings.Sort((a, b) =>
        {
            var sev = a.Severity.CompareTo(b.Severity);
            if (sev != 0) return sev;
            var cat = a.Category.CompareTo(b.Category);
            if (cat != 0) return cat;
            return b.Occurrences.CompareTo(a.Occurrences);
        });

        return report;
    }
}
