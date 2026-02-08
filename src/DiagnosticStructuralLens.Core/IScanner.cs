namespace DiagnosticStructuralLens.Core;

/// <summary>
/// Interface for all language/platform scanners.
/// </summary>
public interface IScanner
{
    /// <summary>
    /// Scans a path and returns discovered atoms.
    /// </summary>
    Task<ScanResult> ScanAsync(string path, ScanOptions? options = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a scan operation.
/// </summary>
public record ScanResult
{
    public List<CodeAtom> CodeAtoms { get; init; } = [];
    public List<SqlAtom> SqlAtoms { get; init; } = [];
    public List<AtomLink> Links { get; init; } = [];
    public List<ScanDiagnostic> Diagnostics { get; init; } = [];
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Options for scanning operations.
/// </summary>
public class ScanOptions
{
    public List<string> IncludePatterns { get; init; } = ["**/*.cs", "**/*.sql"];
    public List<string> ExcludePatterns { get; init; } = ["**/obj/**", "**/bin/**", "**/node_modules/**"];
    public bool IncludePrivateMembers { get; init; } = false;
    public bool ExtractDocumentation { get; init; } = true;
}

/// <summary>
/// A diagnostic message from scanning.
/// </summary>
public record ScanDiagnostic(
    DiagnosticSeverity Severity,
    string Message,
    string? FilePath = null,
    int? Line = null
);

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}
