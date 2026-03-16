using System.Text.RegularExpressions;
using DiagnosticStructuralLens.Core;

namespace DiagnosticStructuralLens.Cli.Analyzers;

/// <summary>
/// Detects modernization opportunities — patterns that work but should be updated.
/// Rules MOD-001 through MOD-006.
/// </summary>
public partial class ModernizationAnalyzer : IAnalyzer
{
    public FindingCategory Category => FindingCategory.Modernization;

    public IReadOnlyList<AnalyzerFinding> Analyze(
        string repoPath, Snapshot snapshot, List<ScanDiagnostic> diagnostics)
    {
        var findings = new List<AnalyzerFinding>();

        ScanSourceFiles(repoPath, findings);
        CheckSyncDataAccess(snapshot, findings);

        return findings;
    }

    // MOD-001: Synchronous data access (methods in repository/service classes not returning Task)
    private static void CheckSyncDataAccess(Snapshot snapshot, List<AnalyzerFinding> findings)
    {
        var dataClasses = snapshot.CodeAtoms
            .Where(a => a.Type == AtomType.Class &&
                        (a.Name.Contains("Repository") || a.Name.Contains("Service") ||
                         a.Name.Contains("DataAccess") || a.Name.Contains("Dal")))
            .ToList();

        if (dataClasses.Count == 0) return;

        var dataClassIds = dataClasses.Select(a => a.Id).ToHashSet();

        // Check if methods in these classes return Task
        var methods = snapshot.CodeAtoms
            .Where(a => a.Type == AtomType.Method && a.Signature != null)
            .Where(a => snapshot.Links.Any(l => l.Type == LinkType.Contains &&
                        l.SourceId != null && dataClassIds.Contains(l.SourceId) &&
                        l.TargetId == a.Id))
            .ToList();

        var syncMethods = methods
            .Where(m => m.Signature != null &&
                        !m.Signature.Contains("Task") &&
                        !m.Signature.Contains("async") &&
                        !m.Name.StartsWith("Get") == false) // Only flag data access methods
            .ToList();

        // Simplified: check if any data class has zero async methods
        var asyncDataClasses = snapshot.CodeAtoms
            .Where(a => a.Type == AtomType.Method && a.Name.EndsWith("Async"))
            .Select(a => snapshot.Links
                .FirstOrDefault(l => l.Type == LinkType.Contains && l.TargetId == a.Id)?.SourceId)
            .Where(id => id != null)
            .ToHashSet();

        var syncOnlyClasses = dataClasses
            .Where(c => !asyncDataClasses.Contains(c.Id))
            .ToList();

        if (syncOnlyClasses.Count > 0)
        {
            findings.Add(new AnalyzerFinding(
                FindingCategory.Modernization, FindingSeverity.Medium,
                "MOD-001", "Synchronous Data Access",
                "Data access classes found with no async methods — async I/O is critical for scalability on .NET Core.",
                syncOnlyClasses.First().FilePath,
                syncOnlyClasses.First().LineNumber,
                syncOnlyClasses.Count));
        }
    }

    private static void ScanSourceFiles(string repoPath, List<AnalyzerFinding> findings)
    {
        var csFiles = Directory.EnumerateFiles(repoPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")
                     && !f.Contains("/bin/") && !f.Contains("\\bin\\")
                     && !f.Contains("/test") && !f.Contains("\\test")
                     && !f.Contains("Test.cs"));

        var modHits = new Dictionary<string, List<string>>();

        foreach (var file in csFiles)
        {
            try
            {
                var content = File.ReadAllText(file);

                // MOD-002: DataSet/DataTable
                if (content.Contains("DataSet") || content.Contains("DataTable") ||
                    content.Contains("SqlDataAdapter") || content.Contains("DataRow"))
                    AddHit(modHits, "MOD-002", file);

                // MOD-003: Hard-coded connection strings
                if (ConnectionStringPattern().IsMatch(content))
                    AddHit(modHits, "MOD-003", file);

                // MOD-004: SOAP / XML-first APIs
                if (content.Contains("SoapHttpClientProtocol") ||
                    (content.Contains("XmlSerializer") && content.Contains("WebRequest")))
                    AddHit(modHits, "MOD-004", file);

                // MOD-005: Output caching
                if (content.Contains("[OutputCache"))
                    AddHit(modHits, "MOD-005", file);

                // MOD-006: Thread.Sleep (not in test code)
                if (content.Contains("Thread.Sleep"))
                    AddHit(modHits, "MOD-006", file);
            }
            catch { /* skip */ }
        }

        EmitFindings(modHits, findings);
    }

    private static void AddHit(Dictionary<string, List<string>> hits, string ruleId, string file)
    {
        if (!hits.ContainsKey(ruleId))
            hits[ruleId] = [];
        hits[ruleId].Add(file);
    }

    private static void EmitFindings(Dictionary<string, List<string>> hits, List<AnalyzerFinding> findings)
    {
        foreach (var (ruleId, files) in hits)
        {
            var (severity, title, description) = GetRuleMetadata(ruleId);
            findings.Add(new AnalyzerFinding(
                FindingCategory.Modernization, severity, ruleId, title, description,
                files.First(), null, files.Count));
        }
    }

    private static (FindingSeverity, string, string) GetRuleMetadata(string ruleId) => ruleId switch
    {
        "MOD-002" => (FindingSeverity.Medium, "DataSet / DataTable Usage",
            "Legacy ADO.NET DataSet/DataTable — consider migrating to EF Core or Dapper for type-safe data access."),
        "MOD-003" => (FindingSeverity.High, "Hard-coded Connection Strings",
            "Connection strings embedded in source code — use IConfiguration, appsettings.json, or Azure Key Vault."),
        "MOD-004" => (FindingSeverity.Medium, "SOAP / XML-First APIs",
            "SOAP clients detected — migrate to REST/JSON (HttpClient) or gRPC for modern interop."),
        "MOD-005" => (FindingSeverity.Low, "Output Caching Attributes",
            "[OutputCache] is replaced by Response Caching middleware or IDistributedCache in .NET Core."),
        "MOD-006" => (FindingSeverity.Medium, "Thread.Sleep in Production Code",
            "Thread.Sleep blocks threads — use Task.Delay for async-friendly delays."),
        _ => (FindingSeverity.Medium, ruleId, "Modernization opportunity detected.")
    };

    [GeneratedRegex(@"(Data Source|Server\s*=|Initial Catalog|Integrated Security)\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionStringPattern();
}
