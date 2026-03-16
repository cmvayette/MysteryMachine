using System.Text.RegularExpressions;
using DiagnosticStructuralLens.Core;

namespace DiagnosticStructuralLens.Cli.Analyzers;

/// <summary>
/// Detects .NET Framework patterns that require attention for .NET Core migration.
/// Rules MIG-001 through MIG-016.
/// </summary>
public partial class MigrationAnalyzer : IAnalyzer
{
    public FindingCategory Category => FindingCategory.Migration;

    public IReadOnlyList<AnalyzerFinding> Analyze(
        string repoPath, Snapshot snapshot, List<ScanDiagnostic> diagnostics)
    {
        var findings = new List<AnalyzerFinding>();

        // AST-based detection (scan .cs files for using directives + patterns)
        ScanSourceFiles(repoPath, findings);

        // File-based detection (check for Framework-specific files)
        ScanFilePatterns(repoPath, findings);

        return findings;
    }

    private static void ScanSourceFiles(string repoPath, List<AnalyzerFinding> findings)
    {
        var csFiles = Directory.EnumerateFiles(repoPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")
                     && !f.Contains("/bin/") && !f.Contains("\\bin\\"));

        // Track occurrences per rule across all files
        var ruleHits = new Dictionary<string, List<(string File, int Line)>>();

        foreach (var file in csFiles)
        {
            try
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    CheckLine(line, file, i + 1, ruleHits);
                }
            }
            catch
            {
                // Skip unreadable files
            }
        }

        // Convert aggregated hits to findings
        EmitFindings(ruleHits, findings);
    }

    private static void CheckLine(string line, string file, int lineNum,
        Dictionary<string, List<(string File, int Line)>> hits)
    {
        // MIG-001: System.Web usage
        if (line.StartsWith("using System.Web") || line.Contains("HttpContext.Current"))
            AddHit(hits, "MIG-001", file, lineNum);

        // MIG-002: WCF service contracts
        if (line.Contains("[ServiceContract") || line.Contains("[OperationContract"))
            AddHit(hits, "MIG-002", file, lineNum);

        // MIG-005: .NET Remoting
        if (line.Contains("MarshalByRefObject") || line.Contains("RemotingConfiguration"))
            AddHit(hits, "MIG-005", file, lineNum);

        // MIG-006: Global.asax lifecycle
        if (line.Contains("Application_Start") || line.Contains("Application_End")
            || line.Contains("Session_Start") || line.Contains("Application_BeginRequest"))
            AddHit(hits, "MIG-006", file, lineNum);

        // MIG-007: ConfigurationManager
        if (line.Contains("ConfigurationManager.AppSettings") ||
            line.Contains("ConfigurationManager.ConnectionStrings") ||
            line.Contains("ConfigurationManager.GetSection"))
            AddHit(hits, "MIG-007", file, lineNum);

        // MIG-008: HttpModule / HttpHandler
        if (line.Contains("IHttpModule") || line.Contains("IHttpHandler"))
            AddHit(hits, "MIG-008", file, lineNum);

        // MIG-009: System.Drawing
        if (line.StartsWith("using System.Drawing"))
            AddHit(hits, "MIG-009", file, lineNum);

        // MIG-010: AppDomain
        if (line.Contains("AppDomain.CreateDomain") || line.Contains("AppDomain.Unload"))
            AddHit(hits, "MIG-010", file, lineNum);

        // MIG-011: FormsAuthentication
        if (line.Contains("FormsAuthentication") || line.Contains("FormsIdentity"))
            AddHit(hits, "MIG-011", file, lineNum);

        // MIG-012: Session state
        if (SessionPattern().IsMatch(line))
            AddHit(hits, "MIG-012", file, lineNum);

        // MIG-013: Entity Framework 6 (using EntityFramework, not Microsoft.EntityFrameworkCore)
        if (line == "using System.Data.Entity;" || line.StartsWith("using System.Data.Entity."))
            AddHit(hits, "MIG-013", file, lineNum);

        // MIG-015: COM Interop / P/Invoke
        if (line.Contains("[DllImport") || line.Contains("[ComImport"))
            AddHit(hits, "MIG-015", file, lineNum);

        // MIG-016: Assembly reflection loading
        if (line.Contains("Assembly.LoadFrom") || line.Contains("Assembly.LoadFile")
            || line.Contains("Assembly.Load("))
            AddHit(hits, "MIG-016", file, lineNum);
    }

    private static void ScanFilePatterns(string repoPath, List<AnalyzerFinding> findings)
    {
        // MIG-003: ASMX web services
        var asmxFiles = Directory.EnumerateFiles(repoPath, "*.asmx", SearchOption.AllDirectories).ToList();
        if (asmxFiles.Count > 0)
        {
            findings.Add(new AnalyzerFinding(
                FindingCategory.Migration, FindingSeverity.Critical,
                "MIG-003", "ASMX Web Services",
                "ASMX web services have no equivalent in .NET Core. Must rewrite as Web API controllers.",
                asmxFiles.First(), null, asmxFiles.Count));
        }

        // MIG-004: WebForms
        var aspxFiles = Directory.EnumerateFiles(repoPath, "*.aspx", SearchOption.AllDirectories).ToList();
        var ascxFiles = Directory.EnumerateFiles(repoPath, "*.ascx", SearchOption.AllDirectories).ToList();
        var webformsCount = aspxFiles.Count + ascxFiles.Count;
        if (webformsCount > 0)
        {
            findings.Add(new AnalyzerFinding(
                FindingCategory.Migration, FindingSeverity.Critical,
                "MIG-004", "ASP.NET WebForms",
                "WebForms (.aspx/.ascx) have no equivalent in .NET Core. Requires full rewrite to Razor Pages or Blazor.",
                aspxFiles.FirstOrDefault() ?? ascxFiles.First(), null, webformsCount));
        }

        // MIG-006: Global.asax file existence
        var globalAsax = Directory.EnumerateFiles(repoPath, "Global.asax", SearchOption.AllDirectories).ToList();
        if (globalAsax.Count > 0)
        {
            findings.Add(new AnalyzerFinding(
                FindingCategory.Migration, FindingSeverity.High,
                "MIG-006", "Global.asax Lifecycle",
                "Global.asax is replaced by Program.cs and middleware pipeline in .NET Core.",
                globalAsax.First(), null, globalAsax.Count));
        }

        // MIG-014: packages.config
        var packageConfigs = Directory.EnumerateFiles(repoPath, "packages.config", SearchOption.AllDirectories).ToList();
        if (packageConfigs.Count > 0)
        {
            findings.Add(new AnalyzerFinding(
                FindingCategory.Migration, FindingSeverity.Medium,
                "MIG-014", "packages.config NuGet Format",
                "Must convert from packages.config to PackageReference format for .NET Core.",
                packageConfigs.First(), null, packageConfigs.Count));
        }

        // WCF .svc files (supplements MIG-002)
        var svcFiles = Directory.EnumerateFiles(repoPath, "*.svc", SearchOption.AllDirectories).ToList();
        if (svcFiles.Count > 0)
        {
            findings.Add(new AnalyzerFinding(
                FindingCategory.Migration, FindingSeverity.Critical,
                "MIG-002", "WCF Service Files",
                "WCF .svc files have no server-side equivalent in .NET Core. Migrate to gRPC or Web API.",
                svcFiles.First(), null, svcFiles.Count));
        }

        // web.config detection
        var webConfigs = Directory.EnumerateFiles(repoPath, "web.config", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")).ToList();
        if (webConfigs.Count > 0)
        {
            // Scan web.config for WCF endpoints
            foreach (var wc in webConfigs)
            {
                try
                {
                    var content = File.ReadAllText(wc);
                    if (content.Contains("<system.serviceModel"))
                    {
                        findings.Add(new AnalyzerFinding(
                            FindingCategory.Migration, FindingSeverity.Critical,
                            "MIG-002", "WCF Configuration in web.config",
                            "system.serviceModel section in web.config contains WCF endpoint configuration that must be migrated.",
                            wc, null, 1));
                    }
                }
                catch { /* skip unreadable */ }
            }
        }
    }

    private static void AddHit(Dictionary<string, List<(string File, int Line)>> hits,
        string ruleId, string file, int line)
    {
        if (!hits.ContainsKey(ruleId))
            hits[ruleId] = [];
        hits[ruleId].Add((file, line));
    }

    private static void EmitFindings(Dictionary<string, List<(string File, int Line)>> hits,
        List<AnalyzerFinding> findings)
    {
        foreach (var (ruleId, locations) in hits)
        {
            var (severity, title, description) = GetRuleMetadata(ruleId);
            var firstHit = locations.First();
            findings.Add(new AnalyzerFinding(
                FindingCategory.Migration, severity, ruleId, title, description,
                firstHit.File, firstHit.Line, locations.Count));
        }
    }

    private static (FindingSeverity, string, string) GetRuleMetadata(string ruleId) => ruleId switch
    {
        "MIG-001" => (FindingSeverity.Critical, "System.Web Usage",
            "System.Web has no equivalent in .NET Core. All HttpContext.Current, Request/Response access must be rewritten using ASP.NET Core abstractions."),
        "MIG-002" => (FindingSeverity.Critical, "WCF Service Contracts",
            "Server-side WCF is not available in .NET Core. Migrate to gRPC, Web API, or CoreWCF."),
        "MIG-005" => (FindingSeverity.Critical, ".NET Remoting",
            ".NET Remoting is removed entirely in .NET Core. No migration path — requires architectural redesign."),
        "MIG-006" => (FindingSeverity.High, "Global.asax Lifecycle Hooks",
            "Application lifecycle hooks must move to Program.cs / Startup.cs middleware pipeline."),
        "MIG-007" => (FindingSeverity.High, "ConfigurationManager Usage",
            "Replace ConfigurationManager with IConfiguration / IOptions pattern and appsettings.json."),
        "MIG-008" => (FindingSeverity.High, "HttpModule / HttpHandler",
            "IHttpModule and IHttpHandler are replaced by ASP.NET Core middleware."),
        "MIG-009" => (FindingSeverity.High, "System.Drawing Usage",
            "System.Drawing is Windows-only. Use SkiaSharp or ImageSharp for cross-platform image processing."),
        "MIG-010" => (FindingSeverity.High, "AppDomain Usage",
            "AppDomain.CreateDomain is not supported in .NET Core. Use separate processes or AssemblyLoadContext."),
        "MIG-011" => (FindingSeverity.High, "Forms Authentication",
            "FormsAuthentication is replaced by ASP.NET Core Identity, cookie authentication, or JWT."),
        "MIG-012" => (FindingSeverity.High, "Session State Usage",
            "Session state requires explicit setup in .NET Core and doesn't work in stateless deployments without distributed caching."),
        "MIG-013" => (FindingSeverity.Medium, "Entity Framework 6",
            "EF6 DbContext must migrate to EF Core. Most patterns translate but there are API differences."),
        "MIG-015" => (FindingSeverity.High, "COM Interop / P/Invoke",
            "COM interop and P/Invoke are platform-specific and won't work in Linux containers."),
        "MIG-016" => (FindingSeverity.Medium, "Assembly Reflection Loading",
            "Assembly.LoadFrom/Load behavior differs in .NET Core. Use AssemblyLoadContext for isolation."),
        _ => (FindingSeverity.Medium, ruleId, "Unknown migration pattern detected.")
    };

    [GeneratedRegex(@"Session\[""")]
    private static partial Regex SessionPattern();
}
