namespace DiagnosticStructuralLens.Cli.Analyzers;

public enum FindingCategory
{
    Migration,
    Architecture,
    Modernization,
    Security
}

public enum FindingSeverity
{
    Critical,
    High,
    Medium,
    Low,
    Info
}

/// <summary>
/// A single finding from an analyzer rule.
/// </summary>
public record AnalyzerFinding(
    FindingCategory Category,
    FindingSeverity Severity,
    string RuleId,
    string Title,
    string Description,
    string? FilePath,
    int? LineNumber,
    int Occurrences = 1
);

/// <summary>
/// Aggregated results from all analyzers.
/// </summary>
public class AnalysisReport
{
    public List<AnalyzerFinding> Findings { get; } = [];

    public int CriticalCount => Findings.Count(f => f.Severity == FindingSeverity.Critical);
    public int HighCount => Findings.Count(f => f.Severity == FindingSeverity.High);
    public int MediumCount => Findings.Count(f => f.Severity == FindingSeverity.Medium);
    public int LowCount => Findings.Count(f => f.Severity == FindingSeverity.Low);

    public IEnumerable<AnalyzerFinding> ByCategory(FindingCategory category)
        => Findings.Where(f => f.Category == category);

    public bool HasCategory(FindingCategory category)
        => Findings.Any(f => f.Category == category);

    public int TotalOccurrences => Findings.Sum(f => f.Occurrences);
    public int AffectedFiles => Findings.Where(f => f.FilePath != null).Select(f => f.FilePath).Distinct().Count();
}
