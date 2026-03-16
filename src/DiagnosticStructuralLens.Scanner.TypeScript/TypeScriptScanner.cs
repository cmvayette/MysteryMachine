using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using DiagnosticStructuralLens.Core;

namespace DiagnosticStructuralLens.Scanner.TypeScript;

/// <summary>
/// Scans TypeScript/JavaScript codebases by invoking the Node.js dsl-scanner-typescript
/// as a subprocess and deserializing its JSON output into the shared ScanResult model.
/// </summary>
public class TypeScriptScanner : IScanner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Path to the scanner-typescript directory. 
    /// Defaults to scanning relative to the DSL install location.
    /// </summary>
    public string? ScannerPath { get; init; }

    public async Task<ScanResult> ScanAsync(string path, ScanOptions? options = null, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        options ??= new ScanOptions();

        if (!Directory.Exists(path))
        {
            return new ScanResult
            {
                Diagnostics = [new ScanDiagnostic(DiagnosticSeverity.Error, $"Path not found: {path}")]
            };
        }

        // Locate the scanner-typescript package
        var scannerDir = ResolveScannerDirectory();
        if (scannerDir == null)
        {
            return new ScanResult
            {
                Diagnostics = [new ScanDiagnostic(DiagnosticSeverity.Error,
                    "Cannot find scanner-typescript directory. Ensure it is installed alongside the DSL CLI or set ScannerPath.")]
            };
        }

        // Build the command
        var outputFile = Path.GetTempFileName();
        try
        {
            var result = await RunScannerProcess(scannerDir, path, outputFile, cancellationToken);
            
            if (result.exitCode != 0)
            {
                return new ScanResult
                {
                    Diagnostics = [new ScanDiagnostic(DiagnosticSeverity.Error,
                        $"TypeScript scanner exited with code {result.exitCode}: {result.stderr}")]
                };
            }

            // Parse the output JSON
            var scanResult = await DeserializeScanResult(outputFile);
            return scanResult with { Duration = DateTime.UtcNow - startTime };
        }
        finally
        {
            // Clean up temp file
            if (File.Exists(outputFile))
                File.Delete(outputFile);
        }
    }

    private async Task<(int exitCode, string stderr)> RunScannerProcess(
        string scannerDir, string repoPath, string outputFile, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "npx",
            Arguments = $"tsx src/index.ts --repo \"{repoPath}\" --output \"{outputFile}\"",
            WorkingDirectory = scannerDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            return (-1, "Failed to start TypeScript scanner process");
        }

        // Read stdout/stderr asynchronously
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        // Forward scanner console output for visibility
        if (!string.IsNullOrWhiteSpace(stdout))
        {
            foreach (var line in stdout.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                Console.WriteLine($"      [ts] {line}");
            }
        }

        return (process.ExitCode, stderr);
    }

    private static async Task<ScanResult> DeserializeScanResult(string jsonPath)
    {
        var json = await File.ReadAllTextAsync(jsonPath);
        var tsOutput = JsonSerializer.Deserialize<TypeScriptScanOutput>(json, JsonOptions);

        if (tsOutput == null)
        {
            return new ScanResult
            {
                Diagnostics = [new ScanDiagnostic(DiagnosticSeverity.Error, "Failed to deserialize TypeScript scanner output")]
            };
        }

        // Map the TS output to DSL's ScanResult model
        var codeAtoms = tsOutput.CodeAtoms.Select(MapCodeAtom).ToList();
        var links = tsOutput.Links.Select(MapLink).ToList();
        var diagnostics = tsOutput.Diagnostics.Select(MapDiagnostic).ToList();

        return new ScanResult
        {
            CodeAtoms = codeAtoms,
            SqlAtoms = [], // TS scanner doesn't produce SQL atoms
            Links = links,
            Diagnostics = diagnostics,
        };
    }

    private static CodeAtom MapCodeAtom(TsCodeAtom ts) => new()
    {
        Id = ts.Id,
        Name = ts.Name,
        Type = ParseAtomType(ts.Type),
        Namespace = ts.Namespace,
        Repository = ts.Repository,
        Signature = ts.Signature,
        FilePath = ts.FilePath,
        LineNumber = ts.LineNumber,
        LinesOfCode = ts.LinesOfCode,
        Language = ts.Language ?? "TypeScript",
        IsPublic = ts.IsPublic,
    };

    private static AtomLink MapLink(TsAtomLink ts) => new()
    {
        Id = ts.Id,
        SourceId = ts.SourceId,
        TargetId = ts.TargetId,
        Type = ParseLinkType(ts.Type),
        Confidence = ts.Confidence,
        Evidence = ts.Evidence,
    };

    private static ScanDiagnostic MapDiagnostic(TsScanDiagnostic ts) => new(
        Severity: ts.Severity switch
        {
            "Error" => DiagnosticSeverity.Error,
            "Warning" => DiagnosticSeverity.Warning,
            _ => DiagnosticSeverity.Info
        },
        Message: ts.Message,
        FilePath: ts.FilePath,
        Line: ts.Line
    );

    private static AtomType ParseAtomType(string type) => type switch
    {
        "Class" => AtomType.Class,
        "Interface" => AtomType.Interface,
        "Enum" => AtomType.Enum,
        "Method" => AtomType.Method,
        "Property" => AtomType.Property,
        "Field" => AtomType.Field,
        "Record" => AtomType.Record,
        "Dto" => AtomType.Dto,
        "TypeAlias" => AtomType.TypeAlias,
        "Module" => AtomType.Module,
        "Component" => AtomType.Component,
        _ => AtomType.Unknown
    };

    private static LinkType ParseLinkType(string type) => type switch
    {
        "Imports" => LinkType.Imports,
        "ReExports" => LinkType.ReExports,
        "WorkspaceDependency" => LinkType.WorkspaceDependency,
        "Inherits" => LinkType.Inherits,
        "Implements" => LinkType.Implements,
        "Calls" => LinkType.Calls,
        "References" => LinkType.References,
        "Contains" => LinkType.Contains,
        _ => LinkType.References
    };

    private string? ResolveScannerDirectory()
    {
        // 1. Explicit path
        if (ScannerPath != null && Directory.Exists(ScannerPath))
            return ScannerPath;

        // 2. Sibling to the running binary (production layout)
        var binDir = AppContext.BaseDirectory;
        var siblingPath = Path.Combine(binDir, "..", "..", "..", "..", "scanner-typescript");
        if (Directory.Exists(siblingPath))
            return Path.GetFullPath(siblingPath);

        // 3. Development: relative to the solution root
        var devPath = FindScannerFromSolutionRoot();
        if (devPath != null) return devPath;

        return null;
    }

    private static string? FindScannerFromSolutionRoot()
    {
        // Walk up from the binary directory looking for the solution root
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "scanner-typescript");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "package.json")))
                return Path.GetFullPath(candidate);

            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return null;
    }

    // ── JSON DTO types matching the TypeScript scanner output ──────────────────

    private record TypeScriptScanOutput
    {
        public List<TsCodeAtom> CodeAtoms { get; init; } = [];
        public List<TsAtomLink> Links { get; init; } = [];
        public List<TsScanDiagnostic> Diagnostics { get; init; } = [];
        public string Duration { get; init; } = "";
    }

    private record TsCodeAtom
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Type { get; init; } = "";
        public string Namespace { get; init; } = "";
        public string? Repository { get; init; }
        public string? Signature { get; init; }
        public string? FilePath { get; init; }
        public int? LineNumber { get; init; }
        public int? LinesOfCode { get; init; }
        public string? Language { get; init; }
        public bool IsPublic { get; init; }
    }

    private record TsAtomLink
    {
        public string Id { get; init; } = "";
        public string SourceId { get; init; } = "";
        public string TargetId { get; init; } = "";
        public string Type { get; init; } = "";
        public double Confidence { get; init; }
        public string? Evidence { get; init; }
    }

    private record TsScanDiagnostic
    {
        public string Severity { get; init; } = "Info";
        public string Message { get; init; } = "";
        public string? FilePath { get; init; }
        public int? Line { get; init; }
    }
}
