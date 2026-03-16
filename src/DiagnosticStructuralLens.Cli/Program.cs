using System.Text.Json;
using System.Text.Json.Serialization;
using DiagnosticStructuralLens.Cli.Analyzers;
using DiagnosticStructuralLens.Core;
using DiagnosticStructuralLens.Federation;
using DiagnosticStructuralLens.Linker;
using DiagnosticStructuralLens.Risk;
using DiagnosticStructuralLens.Scanner.CSharp;
using DiagnosticStructuralLens.Scanner.Sql;
using DiagnosticStructuralLens.Scanner.TypeScript;

namespace DiagnosticStructuralLens.Cli;

public class Program
{
    public static bool IsCiMode { get; private set; } = false;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<int> Main(string[] args)
    {
        // Check for CI flag
        if (args.Contains("--ci"))
        {
            IsCiMode = true;
            args = args.Where(a => a != "--ci").ToArray();
        }

        if (args.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        return command switch
        {
            "scan" => await ExecuteScan(args[1..]),
            "diff" => await ExecuteDiff(args[1..]),
            "blast" => await ExecuteBlast(args[1..]),
            "risk" => await ExecuteRisk(args[1..]),
            "federate" => await ExecuteFederate(args[1..]),
            "publish" => await ExecutePublish(args[1..]),
            "interpret" => await ExecuteInterpret(args[1..]),
            "report" => await ExecuteReport(args[1..]),
            "--help" or "-h" => PrintHelp(),
            "--version" or "-v" => PrintVersion(),
            _ => PrintUnknownCommand(command)
        };
    }

    private static int PrintHelp()
    {
        Console.WriteLine("""
            Diagnostic Structural Lens - Codebase architecture intelligence

            Usage: dsl <command> [options]

            Commands:
              report    Scan + analyze a repo and generate a markdown architecture report
              scan      Scan a repository for components and database objects
              diff      Compare snapshots and detect breaking changes
              blast     Calculate impact radius for a component
              risk      Generate risk report for a snapshot
              federate  Merge multiple snapshots into a global map
              publish   Publish a snapshot to the DSL API
              interpret Read a snapshot and generate a human-readable summary

            Report Options:
              --repo <path>       Path to repository (required)
              --output <file>     Output report file (default: <repo>/dsl-report.md)
              --include-private   Include internal/private types
              --top <n>           Show top N risky components (default: 10)

            Scan Options:
              --repo <path>       Path to repository (required)
              --output <file>     Output snapshot file (default: snapshot.json)
              --include-private   Include internal/private types
              --no-link           Skip semantic linking phase
              --publish           Automatically publish to the API after scanning
              --url <url>         API endpoint URL (default: http://localhost:8080/load)

            Diff Options:
              --baseline <file>   Baseline snapshot file (required)
              --snapshot <file>   Current snapshot file (required)
              --format <type>     Output format: text, json, markdown (default: text)

            Blast Options:
              --snapshot <file>   Snapshot file to analyze (required)
              --atom <id>         Component ID to calculate impact radius for (required)
              --depth <n>         Max depth to traverse (default: 5)

            Risk Options:
              --snapshot <file>   Snapshot file to analyze (required)
              --format <type>     Output format: text, json, html (default: text)
              --output <file>     Output file (optional, writes to stdout if omitted)
              --top <n>           Show top N risky components (default: 10)

            Publish Options:
              --file <file>       Snapshot file to publish (required)
              --url <url>         API endpoint URL (default: http://localhost:8080/load)

            Federate Options:
              --snapshots <files> Comma-separated list of snapshot files (required)
              --output <file>     Output federated snapshot file (required)
              --strategy <type>   Conflict resolution: newest, priority (default: newest)
              --priority <repos>  Repo priority for conflicts (comma-separated)

            Interpret Options:
              --snapshot <file>   Snapshot file to read (required)
              --output <file>     Output file (optional, writes to stdout if omitted)

            Examples:
              dsl report --repo .                              # Full architecture report
              dsl report --repo . --output docs/report.md      # Custom output path
              dsl scan --repo ./src --output ./snapshot.json
              dsl diff --baseline main.json --snapshot current.json
              dsl blast --snapshot ./snapshot.json --atom table:users
              dsl risk --snapshot ./snapshot.json --format html --output risk.html
              dsl publish --file ./snapshot.json
              dsl scan --repo . --publish
              dsl interpret --snapshot ./snapshot.json
            """);
        return 0;
    }

    private static int PrintVersion()
    {
        Console.WriteLine("Diagnostic Structural Lens v0.2.0");
        return 0;
    }

    private static int PrintUnknownCommand(string command)
    {
        Console.WriteLine($"Unknown command: {command}");
        Console.WriteLine("Run 'dsl --help' for usage information.");
        return 1;
    }

    private static void Log(string message, string? emoji = null)
    {
        if (IsCiMode || emoji == null)
            Console.WriteLine(message);
        else
            Console.WriteLine($"{emoji} {message}");
    }

    private static async Task<int> ExecuteScan(string[] args)
    {
        string? repoPath = null;
        string? outputPath = null;
        bool includePrivate = false;
        bool skipLinking = false;
        bool publish = false;
        string publishUrl = "http://localhost:8080/load";
        string language = "all"; // "all", "csharp", "typescript"

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--repo" when i + 1 < args.Length:
                    repoPath = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    outputPath = args[++i];
                    break;
                case "--include-private":
                    includePrivate = true;
                    break;
                case "--no-link":
                    skipLinking = true;
                    break;
                case "--publish":
                    publish = true;
                    break;
                case "--url" when i + 1 < args.Length:
                    publishUrl = args[++i];
                    break;
                case "--language" when i + 1 < args.Length:
                    language = args[++i].ToLowerInvariant();
                    break;
            }
        }

        if (string.IsNullOrEmpty(repoPath))
        {
            Console.WriteLine("Error: --repo is required");
            return 1;
        }

        var fullRepoPath = Path.GetFullPath(repoPath);
        if (!Directory.Exists(fullRepoPath))
        {
            Console.WriteLine($"Error: Repository path not found: {fullRepoPath}");
            return 1;
        }

        Log($"Scanning repository: {fullRepoPath}", "🔍");
        var startTime = DateTime.UtcNow;

        var options = new ScanOptions { IncludePrivateMembers = includePrivate };
        
        // Determine which languages to scan
        var scanCSharp = language is "all" or "csharp";
        var scanTypeScript = language is "all" or "typescript";

        // Auto-detect TypeScript if language is "all" and package.json with workspaces exists
        if (language == "all")
        {
            var packageJsonPath = Path.Combine(fullRepoPath, "package.json");
            scanTypeScript = File.Exists(packageJsonPath);
        }

        var allCodeAtoms = new List<CodeAtom>();
        var allSqlAtoms = new List<SqlAtom>();
        var allLinks = new List<AtomLink>();
        var allDiagnostics = new List<ScanDiagnostic>();

        // Run C# scanner
        if (scanCSharp)
        {
            Log("   Scanning C# files...", "📦");
            var csharpScanner = new CSharpScanner();
            var csharpResult = await csharpScanner.ScanAsync(fullRepoPath, options);
            Console.WriteLine($"      Found {csharpResult.CodeAtoms.Count} code atoms, {csharpResult.Links.Count} links");
            allCodeAtoms.AddRange(csharpResult.CodeAtoms);
            allLinks.AddRange(csharpResult.Links);
            allDiagnostics.AddRange(csharpResult.Diagnostics);

            // Run SQL scanner (only relevant for C# projects)
            Log("   Scanning SQL files...", "🗄️");
            var sqlScanner = new SqlScanner();
            var sqlResult = await sqlScanner.ScanAsync(fullRepoPath, options);
            Console.WriteLine($"      Found {sqlResult.SqlAtoms.Count} SQL atoms, {sqlResult.Links.Count} links");
            allSqlAtoms.AddRange(sqlResult.SqlAtoms);
            allLinks.AddRange(sqlResult.Links);
            allDiagnostics.AddRange(sqlResult.Diagnostics);
        }

        // Run TypeScript scanner
        if (scanTypeScript)
        {
            Log("   Scanning TypeScript files...", "💠");
            var tsScanner = new TypeScriptScanner();
            var tsResult = await tsScanner.ScanAsync(fullRepoPath, options);
            Console.WriteLine($"      Found {tsResult.CodeAtoms.Count} TS atoms, {tsResult.Links.Count} links");
            allCodeAtoms.AddRange(tsResult.CodeAtoms);
            allLinks.AddRange(tsResult.Links);
            allDiagnostics.AddRange(tsResult.Diagnostics);
        }

        // Run semantic linker
        var semanticLinks = new List<AtomLink>();
        if (!skipLinking && (allCodeAtoms.Count > 0 || allSqlAtoms.Count > 0))
        {
            Log("   Running semantic linker...", "🔗");
            var linker = new SemanticLinker();
            var linkResult = linker.LinkAtoms(allCodeAtoms, allSqlAtoms, allLinks);
            semanticLinks = linkResult.Links;
            Console.WriteLine($"      Created {semanticLinks.Count} semantic links");
            
            // Print link summary by type
            var linksByType = semanticLinks.GroupBy(l => l.Type)
                .OrderByDescending(g => g.Count())
                .Take(5);
            foreach (var group in linksByType)
            {
                Console.WriteLine($"        {group.Key}: {group.Count()}");
            }
        }

        // Merge all links
        var finalLinks = allLinks.Concat(semanticLinks).ToList();

        // RUN GOVERNANCE ENGINE
        var governancePath = Path.Combine(fullRepoPath, "governance.yaml");
        if (File.Exists(governancePath))
        {
            Log($"   Running governance check...", "⚖️");
            var governance = new GovernanceEngine(governancePath);
            // Only check CodeAtoms for now
            var atomMap = allCodeAtoms.DistinctBy(a => a.Id).ToDictionary(a => a.Id);
            
            foreach (var link in finalLinks)
            {
                if (atomMap.TryGetValue(link.SourceId, out var source) && 
                    atomMap.TryGetValue(link.TargetId, out var target))
                {
                    if (governance.IsViolation(link, source, target))
                    {
                        var reasons = governance.GetViolationReasons(link, source, target);
                        foreach (var reason in reasons)
                        {
                            allDiagnostics.Add(new ScanDiagnostic(
                                DiagnosticSeverity.Error, 
                                reason,
                                source.FilePath,
                                source.LineNumber
                            ));
                        }
                    }
                }
            }
        }

        // Create snapshot
        var snapshot = new Snapshot
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Repository = DetectGitRepoName(fullRepoPath),
            ScannedAt = DateTimeOffset.UtcNow,
            Branch = GetCurrentBranch(fullRepoPath),
            CommitSha = GetCurrentCommit(fullRepoPath),
            CodeAtoms = allCodeAtoms,
            SqlAtoms = allSqlAtoms,
            Links = finalLinks,
            Metadata = new SnapshotMetadata
            {
                TotalCodeAtoms = allCodeAtoms.Count,
                TotalSqlAtoms = allSqlAtoms.Count,
                TotalLinks = finalLinks.Count,
                DtoCount = allCodeAtoms.Count(a => a.Type == AtomType.Dto),
                InterfaceCount = allCodeAtoms.Count(a => a.Type == AtomType.Interface),
                TableCount = allSqlAtoms.Count(a => a.Type == SqlAtomType.Table),
                StoredProcedureCount = allSqlAtoms.Count(a => a.Type == SqlAtomType.StoredProcedure),
                TypeAliasCount = allCodeAtoms.Count(a => a.Type == AtomType.TypeAlias),
                ModuleCount = allCodeAtoms.Count(a => a.Type == AtomType.Module),
                ComponentCount = allCodeAtoms.Count(a => a.Type == AtomType.Component),
                ScanDuration = DateTime.UtcNow - startTime
            }
        };

        // Write output
        outputPath ??= Path.Combine(fullRepoPath, "snapshot.json");
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await File.WriteAllTextAsync(outputPath, json);

        // Print diagnostics (allDiagnostics already accumulated above)
        if (allDiagnostics.Count > 0)
        {
            Log($"\n{allDiagnostics.Count} diagnostics:", "⚠️");
            foreach (var diag in allDiagnostics.Take(10))
            {
                var icon = diag.Severity == DiagnosticSeverity.Error ? "❌" : 
                           diag.Severity == DiagnosticSeverity.Warning ? "⚠️" : "ℹ️";
                Log($"   {diag.Message}", icon);
            }
            if (allDiagnostics.Count > 10)
                Console.WriteLine($"   ... and {allDiagnostics.Count - 10} more");
        }

        Log($"\nSnapshot saved to: {outputPath}", "✅");
        
        Console.WriteLine($"""

            Summary:
              Code Atoms:  {snapshot.Metadata.TotalCodeAtoms:N0}
                DTOs:      {snapshot.Metadata.DtoCount:N0}
                Interfaces:{snapshot.Metadata.InterfaceCount:N0}
              SQL Atoms:   {snapshot.Metadata.TotalSqlAtoms:N0}
                Tables:    {snapshot.Metadata.TableCount:N0}
                Procs:     {snapshot.Metadata.StoredProcedureCount:N0}
              Links:       {snapshot.Metadata.TotalLinks:N0}
              Duration:    {snapshot.Metadata.ScanDuration.TotalSeconds:F2}s
            """);

        if (publish)
        {
            await PublishSnapshotAsync(outputPath, publishUrl);
        }

        return 0;
    }

    private static async Task<int> ExecuteDiff(string[] args)
    {
        string? baselinePath = null;
        string? snapshotPath = null;
        string format = "text";

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--baseline" when i + 1 < args.Length:
                    baselinePath = args[++i];
                    break;
                case "--snapshot" when i + 1 < args.Length:
                    snapshotPath = args[++i];
                    break;
                case "--format" when i + 1 < args.Length:
                    format = args[++i];
                    break;
            }
        }

        if (string.IsNullOrEmpty(baselinePath) || string.IsNullOrEmpty(snapshotPath))
        {
            Console.WriteLine("Error: --baseline and --snapshot are required");
            return 1;
        }

        Console.WriteLine($"📊 Comparing snapshots...");
        Console.WriteLine($"   Baseline: {baselinePath}");
        Console.WriteLine($"   Current:  {snapshotPath}");

        // Load snapshots
        var baselineJson = await File.ReadAllTextAsync(baselinePath);
        var snapshotJson = await File.ReadAllTextAsync(snapshotPath);
        var baseline = JsonSerializer.Deserialize<Snapshot>(baselineJson, JsonOptions)!;
        var snapshot = JsonSerializer.Deserialize<Snapshot>(snapshotJson, JsonOptions)!;

        // Compare code atoms
        var baselineAtomIds = baseline.CodeAtoms.Select(a => a.Id).ToHashSet();
        var snapshotAtomIds = snapshot.CodeAtoms.Select(a => a.Id).ToHashSet();

        var addedCode = snapshotAtomIds.Except(baselineAtomIds).ToList();
        var removedCode = baselineAtomIds.Except(snapshotAtomIds).ToList();

        // Compare SQL atoms
        var baselineSqlIds = baseline.SqlAtoms.Select(a => a.Id).ToHashSet();
        var snapshotSqlIds = snapshot.SqlAtoms.Select(a => a.Id).ToHashSet();

        var addedSql = snapshotSqlIds.Except(baselineSqlIds).ToList();
        var removedSql = baselineSqlIds.Except(snapshotSqlIds).ToList();

        // Calculate blast radius for removed items
        var linker = new SemanticLinker();
        var affectedByRemovals = new HashSet<string>();
        
        foreach (var removedId in removedCode.Concat(removedSql))
        {
            var blastRadius = linker.GetBlastRadius(removedId, baseline.Links);
            foreach (var affected in blastRadius.AffectedAtoms)
            {
                affectedByRemovals.Add(affected.AtomId);
            }
        }

        Console.WriteLine($"""

            Results:
              Code Atoms:
                Added:   {addedCode.Count:N0}
                Removed: {removedCode.Count:N0}
              SQL Atoms:
                Added:   {addedSql.Count:N0}
                Removed: {removedSql.Count:N0}
              Blast Radius: {affectedByRemovals.Count:N0} atoms potentially affected
            """);

        if (removedCode.Count > 0)
        {
            Console.WriteLine("\n⚠️  Removed code atoms (potentially breaking):");
            foreach (var id in removedCode.Take(10))
            {
                var atom = baseline.CodeAtoms.First(a => a.Id == id);
                Console.WriteLine($"   - {atom.Type}: {atom.Namespace}.{atom.Name}");
            }
            if (removedCode.Count > 10)
                Console.WriteLine($"   ... and {removedCode.Count - 10} more");
        }

        if (removedSql.Count > 0)
        {
            Console.WriteLine("\n⚠️  Removed SQL atoms (potentially breaking):");
            foreach (var id in removedSql.Take(10))
            {
                var atom = baseline.SqlAtoms.First(a => a.Id == id);
                Console.WriteLine($"   - {atom.Type}: {atom.Name}");
            }
            if (removedSql.Count > 10)
                Console.WriteLine($"   ... and {removedSql.Count - 10} more");
        }

        return (removedCode.Count + removedSql.Count) > 0 ? 1 : 0;
    }

    private static async Task<int> ExecuteBlast(string[] args)
    {
        string? snapshotPath = null;
        string? atomId = null;
        int depth = 5;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--snapshot" when i + 1 < args.Length:
                    snapshotPath = args[++i];
                    break;
                case "--atom" when i + 1 < args.Length:
                    atomId = args[++i];
                    break;
                case "--depth" when i + 1 < args.Length:
                    depth = int.Parse(args[++i]);
                    break;
            }
        }

        if (string.IsNullOrEmpty(snapshotPath) || string.IsNullOrEmpty(atomId))
        {
            Console.WriteLine("Error: --snapshot and --atom are required");
            return 1;
        }

        Console.WriteLine($"💥 Calculating blast radius for: {atomId}");

        // Load snapshot
        var json = await File.ReadAllTextAsync(snapshotPath);
        var snapshot = JsonSerializer.Deserialize<Snapshot>(json, JsonOptions)!;

        // Find the atom
        var codeAtom = snapshot.CodeAtoms.FirstOrDefault(a => a.Id == atomId);
        var sqlAtom = snapshot.SqlAtoms.FirstOrDefault(a => a.Id == atomId);

        if (codeAtom == null && sqlAtom == null)
        {
            // Try partial match
            codeAtom = snapshot.CodeAtoms.FirstOrDefault(a => a.Id.Contains(atomId, StringComparison.OrdinalIgnoreCase));
            sqlAtom = snapshot.SqlAtoms.FirstOrDefault(a => a.Id.Contains(atomId, StringComparison.OrdinalIgnoreCase));
            
            if (codeAtom == null && sqlAtom == null)
            {
                Console.WriteLine($"Error: Atom '{atomId}' not found in snapshot");
                Console.WriteLine("\nAvailable atoms (sample):");
                foreach (var a in snapshot.CodeAtoms.Take(5))
                    Console.WriteLine($"  - {a.Id}");
                foreach (var a in snapshot.SqlAtoms.Take(5))
                    Console.WriteLine($"  - {a.Id}");
                return 1;
            }
            
            atomId = codeAtom?.Id ?? sqlAtom?.Id;
            Console.WriteLine($"   (Matched to: {atomId})");
        }

        // Calculate blast radius
        var linker = new SemanticLinker();
        var blastRadius = linker.GetBlastRadius(atomId!, snapshot.Links, depth);

        Console.WriteLine($"""

            Blast Radius Results:
              Root Atom:     {atomId}
              Total Affected: {blastRadius.TotalAffected}
              Max Depth:      {blastRadius.MaxDepth}
            """);

        if (blastRadius.TotalAffected > 0)
        {
            Console.WriteLine("\nAffected atoms by depth:");
            
            var byDepth = blastRadius.AffectedAtoms
                .GroupBy(a => a.Depth)
                .OrderBy(g => g.Key);

            foreach (var depthGroup in byDepth)
            {
                Console.WriteLine($"\n  Depth {depthGroup.Key}:");
                foreach (var affected in depthGroup.Take(10))
                {
                    // Try to get atom name
                    var name = snapshot.CodeAtoms.FirstOrDefault(a => a.Id == affected.AtomId)?.Name
                            ?? snapshot.SqlAtoms.FirstOrDefault(a => a.Id == affected.AtomId)?.Name
                            ?? affected.AtomId;
                    Console.WriteLine($"    - {name}");
                }
                if (depthGroup.Count() > 10)
                    Console.WriteLine($"    ... and {depthGroup.Count() - 10} more");
            }
        }
        else
        {
            Console.WriteLine("\n✅ No dependents found - changes to this atom have minimal impact.");
        }

        return 0;
    }

    private static async Task<bool> PublishSnapshotAsync(string filePath, string url)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: File not found: {filePath}");
            return false;
        }

        Console.WriteLine($"🚀 Publishing snapshot to {url}...");
        Console.WriteLine($"   File: {filePath}");

        try
        {
            using var client = new HttpClient();
            using var fileStream = File.OpenRead(filePath);
            var content = new StreamContent(fileStream);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            var response = await client.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine("✅ Publish successful!");
                Console.WriteLine($"   Response: {responseBody}");
                return true;
            }
            else
            {
                Console.WriteLine($"❌ Publish failed. Status: {response.StatusCode}");
                var errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"   Error: {errorBody}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception during publish: {ex.Message}");
            return false;
        }
    }

    private static async Task<int> ExecuteFederate(string[] args)
    {
        string? snapshotsArg = null;
        string? outputPath = null;
        string strategy = "newest";
        string? priorityArg = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--snapshots" when i + 1 < args.Length:
                    snapshotsArg = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    outputPath = args[++i];
                    break;
                case "--strategy" when i + 1 < args.Length:
                    strategy = args[++i].ToLowerInvariant();
                    break;
                case "--priority" when i + 1 < args.Length:
                    priorityArg = args[++i];
                    break;
            }
        }

        if (string.IsNullOrEmpty(snapshotsArg) || string.IsNullOrEmpty(outputPath))
        {
            Console.WriteLine("Error: --snapshots and --output are required");
            return 1;
        }

        Console.WriteLine("🔗 Federating snapshots...");

        // Load all snapshots
        var snapshotFiles = snapshotsArg.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var snapshots = new List<Snapshot>();

        foreach (var file in snapshotFiles)
        {
            var path = file.Trim();
            if (!File.Exists(path))
            {
                Console.WriteLine($"Error: Snapshot not found: {path}");
                return 1;
            }
            var json = await File.ReadAllTextAsync(path);
            var snapshot = JsonSerializer.Deserialize<Snapshot>(json, JsonOptions)!;
            snapshots.Add(snapshot);
            Console.WriteLine($"  📂 Loaded: {path} ({snapshot.CodeAtoms.Count} code, {snapshot.SqlAtoms.Count} SQL atoms)");
        }

        // Configure options
        var options = new FederationOptions
        {
            ConflictResolution = strategy == "priority" ? ConflictResolution.PriorityOrder : ConflictResolution.NewestWins,
            RepoPriority = priorityArg?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? []
        };

        // Federate
        var engine = new FederationEngine();
        var federated = engine.Merge(snapshots, options);

        // Output summary
        Console.WriteLine($"\n📊 Federation Summary:");
        Console.WriteLine($"   Repos:       {federated.Stats.TotalRepos}");
        Console.WriteLine($"   Code Atoms:  {federated.Stats.TotalCodeAtoms}");
        Console.WriteLine($"   SQL Atoms:   {federated.Stats.TotalSqlAtoms}");
        Console.WriteLine($"   Total Links: {federated.Stats.TotalLinks} ({federated.Stats.CrossRepoLinkCount} cross-repo)");

        if (federated.Conflicts.Count > 0)
        {
            Console.WriteLine($"\n⚠️  Conflicts Detected: {federated.Conflicts.Count}");
            foreach (var conflict in federated.Conflicts.Take(5))
            {
                Console.WriteLine($"   - {conflict.AtomId}: {conflict.Repo1} vs {conflict.Repo2}");
            }
            if (federated.Conflicts.Count > 5)
                Console.WriteLine($"   ... and {federated.Conflicts.Count - 5} more");
        }

        // Save
        var outputJson = JsonSerializer.Serialize(federated, JsonOptions);
        await File.WriteAllTextAsync(outputPath, outputJson);
        Console.WriteLine($"\n✅ Federated snapshot saved to: {outputPath}");

        return 0;
    }

    private static async Task<int> ExecuteRisk(string[] args)
    {
        string? snapshotPath = null;
        string? outputPath = null;
        string format = "text";
        int topN = 10;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--snapshot" when i + 1 < args.Length:
                    snapshotPath = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    outputPath = args[++i];
                    break;
                case "--format" when i + 1 < args.Length:
                    format = args[++i].ToLowerInvariant();
                    break;
                case "--top" when i + 1 < args.Length:
                    topN = int.Parse(args[++i]);
                    break;
            }
        }

        if (string.IsNullOrEmpty(snapshotPath))
        {
            Console.WriteLine("Error: --snapshot is required");
            return 1;
        }

        Console.WriteLine($"📊 Generating risk report...");

        var json = await File.ReadAllTextAsync(snapshotPath);
        var snapshot = JsonSerializer.Deserialize<Snapshot>(json, JsonOptions)!;

        var scorer = new RiskScorer();
        var report = scorer.ScoreSnapshot(snapshot);

        string output = format switch
        {
            "json" => JsonSerializer.Serialize(report, JsonOptions),
            "html" => GenerateHtmlReport(report, snapshot, topN),
            _ => GenerateTextReport(report, snapshot, topN)
        };

        if (!string.IsNullOrEmpty(outputPath))
        {
            await File.WriteAllTextAsync(outputPath, output);
            Console.WriteLine($"✅ Risk report saved to: {outputPath}");
        }
        else
        {
            Console.WriteLine(output);
        }

        return 0;
    }

    private static string GenerateTextReport(RiskReport report, Snapshot snapshot, int topN)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"\nRisk Report - {report.GeneratedAt:yyyy-MM-dd HH:mm}");
        sb.AppendLine("═══════════════════════════════════════════════\n");
        sb.AppendLine($"  Total: {report.TotalAtoms} | Critical: {report.Stats.CriticalCount} | High: {report.Stats.HighCount} | Medium: {report.Stats.MediumCount} | Low: {report.Stats.LowCount}\n");
        sb.AppendLine($"Top {topN} Risky Atoms:");
        sb.AppendLine("───────────────────────────────────────────────");

        foreach (var score in report.Scores.Take(topN))
        {
            var name = snapshot.CodeAtoms.FirstOrDefault(a => a.Id == score.AtomId)?.Name
                    ?? snapshot.SqlAtoms.FirstOrDefault(a => a.Id == score.AtomId)?.Name
                    ?? score.AtomId;
            var icon = score.Level switch { RiskLevel.Critical => "🔴", RiskLevel.High => "🟠", RiskLevel.Medium => "🟡", _ => "🟢" };
            sb.AppendLine($"  {icon} {name,-30} {score.CompositeScore,5:F1} ({score.Level})");
        }
        return sb.ToString();
    }

    private static string GenerateHtmlReport(RiskReport report, Snapshot snapshot, int topN)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Risk Report</title>");
        sb.AppendLine("<style>body{font-family:system-ui;margin:40px;background:#0f1419;color:#e7e9ea}");
        sb.AppendLine("h1{color:#1d9bf0}.stats{display:flex;gap:16px;margin:20px 0}.stat{background:#1c2732;padding:16px;border-radius:8px;text-align:center}");
        sb.AppendLine(".stat-val{font-size:2em;font-weight:bold}.critical{color:#f4212e}.high{color:#ff7a00}.medium{color:#ffd400}.low{color:#00ba7c}");
        sb.AppendLine("table{width:100%;border-collapse:collapse}th,td{padding:12px;text-align:left;border-bottom:1px solid #2f3336}th{background:#1c2732}");
        sb.AppendLine(".badge{padding:4px 8px;border-radius:4px;font-size:.8em}</style></head><body>");
        sb.AppendLine($"<h1>🗺️ Risk Report</h1><p>Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm}</p>");
        sb.AppendLine($"<div class=\"stats\"><div class=\"stat\"><div class=\"stat-val\">{report.TotalAtoms}</div>Total</div>");
        sb.AppendLine($"<div class=\"stat\"><div class=\"stat-val critical\">{report.Stats.CriticalCount}</div>Critical</div>");
        sb.AppendLine($"<div class=\"stat\"><div class=\"stat-val high\">{report.Stats.HighCount}</div>High</div>");
        sb.AppendLine($"<div class=\"stat\"><div class=\"stat-val medium\">{report.Stats.MediumCount}</div>Medium</div>");
        sb.AppendLine($"<div class=\"stat\"><div class=\"stat-val low\">{report.Stats.LowCount}</div>Low</div></div>");
        sb.AppendLine($"<h2>Top {topN} Risky Atoms</h2><table><thead><tr><th>Atom</th><th>Risk</th><th>Score</th></tr></thead><tbody>");

        foreach (var score in report.Scores.Take(topN))
        {
            var name = snapshot.CodeAtoms.FirstOrDefault(a => a.Id == score.AtomId)?.Name
                    ?? snapshot.SqlAtoms.FirstOrDefault(a => a.Id == score.AtomId)?.Name
                    ?? score.AtomId;
            var cls = score.Level.ToString().ToLowerInvariant();
            sb.AppendLine($"<tr><td>{System.Net.WebUtility.HtmlEncode(name)}</td><td class=\"{cls}\">{score.Level}</td><td>{score.CompositeScore:F1}</td></tr>");
        }
        sb.AppendLine("</tbody></table></body></html>");
        return sb.ToString();
    }

    private static async Task<int> ExecuteInterpret(string[] args)
    {
        string? snapshotPath = null;
        string? outputPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--snapshot" when i + 1 < args.Length:
                    snapshotPath = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    outputPath = args[++i];
                    break;
            }
        }

        if (string.IsNullOrEmpty(snapshotPath))
        {
            Console.WriteLine("Error: --snapshot is required");
            return 1;
        }

        if (!File.Exists(snapshotPath))
        {
            Console.WriteLine($"Error: Snapshot file not found: {snapshotPath}");
            return 1;
        }

        // Load snapshot
        var json = await File.ReadAllTextAsync(snapshotPath);
        var snapshot = JsonSerializer.Deserialize<Snapshot>(json, JsonOptions)!;

        // Generate Human-Readable Summary
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"# 🗺️ System Interpretation Report");
        sb.AppendLine($"**Analyzed Repository**: `{snapshot.Repository}`");
        sb.AppendLine($"**Date**: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();

        sb.AppendLine("## 1. High-Level Vital Signs");
        sb.AppendLine($"- **Code Volume**: {snapshot.Metadata.TotalCodeAtoms:N0} structured code units found.");
        sb.AppendLine($"- **Database Surface**: {snapshot.Metadata.TotalSqlAtoms:N0} SQL objects detected.");
        sb.AppendLine($"- **Connectivity**: {snapshot.Metadata.TotalLinks:N0} relationships identified.");
        sb.AppendLine($"- **Complexity Density**: {((double)snapshot.Metadata.TotalLinks / (Math.Max(1, snapshot.Metadata.TotalCodeAtoms + snapshot.Metadata.TotalSqlAtoms))):F2} links per node.");
        sb.AppendLine();

        sb.AppendLine("## 2. Architecture Breakdown");

        // Breakdown by Namespace (Top 5)
        var topNamespaces = snapshot.CodeAtoms
            .GroupBy(a => a.Namespace)
            .OrderByDescending(g => g.Count())
            .Take(5);

        sb.AppendLine("### Top Namespaces (by Volume)");
        foreach (var ns in topNamespaces)
        {
            sb.AppendLine($"- **`{ns.Key}`**: {ns.Count():N0} atoms");
        }
        sb.AppendLine();

        // Breakdown by Type
        sb.AppendLine("### Component Taxonomy");
        sb.AppendLine($"- **Interfaces (Contracts)**: {snapshot.Metadata.InterfaceCount:N0}");
        sb.AppendLine($"- **DTOs (Data Carriers)**: {snapshot.Metadata.DtoCount:N0}");
        sb.AppendLine($"- **Classes (Logic)**: {snapshot.CodeAtoms.Count(a => a.Type == AtomType.Class):N0}");
        sb.AppendLine($"- **Database Tables**: {snapshot.Metadata.TableCount:N0}");
        sb.AppendLine();

        sb.AppendLine("## 3. Connectivity Analysis");
        
        // Find most connected nodes (Fan-In + Fan-Out)
        var linkCounts = new Dictionary<string, int>();
        foreach (var link in snapshot.Links)
        {
            linkCounts[link.SourceId] = linkCounts.GetValueOrDefault(link.SourceId) + 1;
            linkCounts[link.TargetId] = linkCounts.GetValueOrDefault(link.TargetId) + 1;
        }

        var topNodes = linkCounts.OrderByDescending(kv => kv.Value).Take(10);
        
        sb.AppendLine("### Central Nervous System (Most Connected Nodes)");
        sb.AppendLine("These are likely your core domain entities or utility services.");
        foreach (var node in topNodes)
        {
            var atomName = snapshot.CodeAtoms.FirstOrDefault(a => a.Id == node.Key)?.Name
                        ?? snapshot.SqlAtoms.FirstOrDefault(a => a.Id == node.Key)?.Name
                        ?? node.Key;
            
            sb.AppendLine($"- **`{atomName}`**: {node.Value:N0} connections");
        }
        sb.AppendLine();

        sb.AppendLine("## 4. Diagnostics & Recommendations");
        
        if (snapshot.Metadata.InterfaceCount == 0)
        {
            sb.AppendLine("- ⚠️ **Low Abstraction**: No interfaces found. Consider introducing interfaces for better testability and decoupling.");
        }
        else
        {
            sb.AppendLine("- ✅ **Good Abstraction**: Interfaces detected, suggesting a decoupled architecture.");
        }

        if (snapshot.Metadata.DtoCount > snapshot.Metadata.TotalCodeAtoms * 0.5)
        {
            sb.AppendLine("- ℹ️ **Data-Heavy**: High ratio of DTOs. This looks like a data transformations pipeline or CRUD app.");
        }

        // Output logic
        var markdown = sb.ToString();
        if (!string.IsNullOrEmpty(outputPath))
        {
            await File.WriteAllTextAsync(outputPath, markdown);
            Console.WriteLine($"✅ Interpretation report saved to: {outputPath}");
        }
        else
        {
            Console.WriteLine(markdown);
        }

        return 0;
    }

    /// <summary>
    /// Walk up from the scanned path to find the Git repository root,
    /// then return its directory name as the canonical repository identifier.
    /// Falls back to the leaf directory name if no .git is found.
    /// </summary>
    private static string DetectGitRepoName(string path)
    {
        var dir = new DirectoryInfo(path);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.Name;
            dir = dir.Parent;
        }
        // No .git found — fall back to directory name
        return new DirectoryInfo(path).Name;
    }

    private static string? GetCurrentBranch(string repoPath)
    {
        try
        {

            var headPath = Path.Combine(repoPath, ".git", "HEAD");
            if (File.Exists(headPath))
            {
                var content = File.ReadAllText(headPath).Trim();
                if (content.StartsWith("ref: refs/heads/"))
                    return content["ref: refs/heads/".Length..];
            }
        }
        catch { }
        return null;
    }

    private static string? GetCurrentCommit(string repoPath)
    {
        try
        {
            var headPath = Path.Combine(repoPath, ".git", "HEAD");
            if (File.Exists(headPath))
            {
                var content = File.ReadAllText(headPath).Trim();
                if (content.StartsWith("ref: "))
                {
                    var refPath = Path.Combine(repoPath, ".git", content[5..].Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(refPath))
                        return File.ReadAllText(refPath).Trim()[..7];
                }
                else if (content.Length >= 7)
                {
                    return content[..7];
                }
            }
        }
        catch { }
        return null;
    }

    private static async Task<int> ExecutePublish(string[] args)
    {
        string? filePath = null;
        string url = "http://localhost:8080/load";

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file" when i + 1 < args.Length:
                    filePath = args[++i];
                    break;
                case "--url" when i + 1 < args.Length:
                    url = args[++i];
                    break;
            }
        }

        if (string.IsNullOrEmpty(filePath))
        {
            Console.WriteLine("Error: --file is required");
            return 1;
        }

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: File not found: {filePath}");
            return 1;
        }

        var success = await PublishSnapshotAsync(filePath, url);
        return success ? 0 : 1;
    }

    private static async Task<int> ExecuteReport(string[] args)
    {
        string? repoPath = null;
        string? outputPath = null;
        string? policyPath = null;
        string? jsonOutputPath = null;
        bool includePrivate = false;
        int topN = 10;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--repo" when i + 1 < args.Length:
                    repoPath = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    outputPath = args[++i];
                    break;
                case "--policy" when i + 1 < args.Length:
                    policyPath = args[++i];
                    break;
                case "--output-json" when i + 1 < args.Length:
                    jsonOutputPath = args[++i];
                    break;
                case "--include-private":
                    includePrivate = true;
                    break;
                case "--top" when i + 1 < args.Length:
                    topN = int.Parse(args[++i]);
                    break;
            }
        }

        if (string.IsNullOrEmpty(repoPath))
        {
            Console.WriteLine("Error: --repo is required");
            return 1;
        }

        var fullRepoPath = Path.GetFullPath(repoPath);
        if (!Directory.Exists(fullRepoPath))
        {
            Console.WriteLine($"Error: Repository path not found: {fullRepoPath}");
            return 1;
        }

        Log($"Analyzing repository: {fullRepoPath}", "🔍");
        var startTime = DateTime.UtcNow;

        // --- Phase 1: Scan ---
        var options = new ScanOptions { IncludePrivateMembers = includePrivate };

        var allCodeAtoms = new List<CodeAtom>();
        var allSqlAtoms = new List<SqlAtom>();
        var allLinks = new List<AtomLink>();
        var allDiagnostics = new List<ScanDiagnostic>();

        // C# scanner
        Log("   Scanning C# files...", "📦");
        var csharpScanner = new CSharpScanner();
        var csharpResult = await csharpScanner.ScanAsync(fullRepoPath, options);
        allCodeAtoms.AddRange(csharpResult.CodeAtoms);
        allLinks.AddRange(csharpResult.Links);
        allDiagnostics.AddRange(csharpResult.Diagnostics);

        // SQL scanner
        Log("   Scanning SQL files...", "🗄️");
        var sqlScanner = new SqlScanner();
        var sqlResult = await sqlScanner.ScanAsync(fullRepoPath, options);
        allSqlAtoms.AddRange(sqlResult.SqlAtoms);
        allLinks.AddRange(sqlResult.Links);
        allDiagnostics.AddRange(sqlResult.Diagnostics);

        // TypeScript scanner (auto-detect)
        var packageJsonPath = Path.Combine(fullRepoPath, "package.json");
        if (File.Exists(packageJsonPath))
        {
            Log("   Scanning TypeScript files...", "💠");
            var tsScanner = new TypeScriptScanner();
            var tsResult = await tsScanner.ScanAsync(fullRepoPath, options);
            allCodeAtoms.AddRange(tsResult.CodeAtoms);
            allLinks.AddRange(tsResult.Links);
            allDiagnostics.AddRange(tsResult.Diagnostics);
        }

        // --- Phase 2: Link ---
        Log("   Running semantic linker...", "🔗");
        var linker = new SemanticLinker();
        var linkResult = linker.LinkAtoms(allCodeAtoms, allSqlAtoms, allLinks);
        var finalLinks = allLinks.Concat(linkResult.Links).ToList();

        // --- Phase 3: Governance ---
        var governanceViolations = new List<string>();
        var governancePath = Path.Combine(fullRepoPath, "governance.yaml");
        if (File.Exists(governancePath))
        {
            Log("   Running governance check...", "⚖️");
            var governance = new GovernanceEngine(governancePath);
            var atomMap = allCodeAtoms.DistinctBy(a => a.Id).ToDictionary(a => a.Id);

            foreach (var link in finalLinks)
            {
                if (atomMap.TryGetValue(link.SourceId, out var source) &&
                    atomMap.TryGetValue(link.TargetId, out var target))
                {
                    if (governance.IsViolation(link, source, target))
                    {
                        var reasons = governance.GetViolationReasons(link, source, target);
                        governanceViolations.AddRange(reasons);
                    }
                }
            }
        }

        // --- Phase 4: Build Snapshot ---
        var snapshot = new Snapshot
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Repository = DetectGitRepoName(fullRepoPath),
            ScannedAt = DateTimeOffset.UtcNow,
            Branch = GetCurrentBranch(fullRepoPath),
            CommitSha = GetCurrentCommit(fullRepoPath),
            CodeAtoms = allCodeAtoms,
            SqlAtoms = allSqlAtoms,
            Links = finalLinks,
            Metadata = new SnapshotMetadata
            {
                TotalCodeAtoms = allCodeAtoms.Count,
                TotalSqlAtoms = allSqlAtoms.Count,
                TotalLinks = finalLinks.Count,
                DtoCount = allCodeAtoms.Count(a => a.Type == AtomType.Dto),
                InterfaceCount = allCodeAtoms.Count(a => a.Type == AtomType.Interface),
                TableCount = allSqlAtoms.Count(a => a.Type == SqlAtomType.Table),
                StoredProcedureCount = allSqlAtoms.Count(a => a.Type == SqlAtomType.StoredProcedure),
                TypeAliasCount = allCodeAtoms.Count(a => a.Type == AtomType.TypeAlias),
                ModuleCount = allCodeAtoms.Count(a => a.Type == AtomType.Module),
                ComponentCount = allCodeAtoms.Count(a => a.Type == AtomType.Component),
                ScanDuration = DateTime.UtcNow - startTime
            }
        };

        // --- Phase 5: Risk Scoring ---
        Log("   Calculating risk scores...", "📊");
        var scorer = new RiskScorer();
        var riskReport = scorer.ScoreSnapshot(snapshot);

        // --- Phase 5.5: Analyzers ---
        Log("   Running architecture analyzers...", "🔬");
        var analyzerRunner = new AnalyzerRunner();
        var analysisReport = analyzerRunner.Run(fullRepoPath, snapshot, allDiagnostics);

        // --- Phase 5.6: Policy Evaluation ---
        PolicyResult? policyResult = null;
        policyPath ??= Path.Combine(fullRepoPath, "dsl-policy.yaml");
        var policyEngine = PolicyEngine.LoadFromFile(policyPath);
        if (policyEngine != null)
        {
            Log("   Evaluating policy gates...", "🛡️");
            policyResult = policyEngine.Evaluate(analysisReport, riskReport, governanceViolations);
        }

        // --- Phase 6: Generate Report ---
        Log("   Generating architecture report...", "📝");
        var markdown = ReportGenerator.GenerateMarkdownReport(
            snapshot, riskReport, allDiagnostics, governanceViolations,
            analysisReport, policyResult, topN);

        // Write markdown report
        outputPath ??= Path.Combine(fullRepoPath, "dsl-report.md");
        await File.WriteAllTextAsync(outputPath, markdown);

        // Write JSON results (for ADO test publishing)
        if (jsonOutputPath != null)
        {
            var jsonResults = System.Text.Json.JsonSerializer.Serialize(new
            {
                repository = snapshot.Repository,
                branch = snapshot.Branch,
                commit = snapshot.CommitSha,
                timestamp = snapshot.ScannedAt,
                summary = new
                {
                    components = allCodeAtoms.Count,
                    databaseObjects = allSqlAtoms.Count,
                    relationships = finalLinks.Count,
                    riskCritical = riskReport.Stats.CriticalCount,
                    riskHigh = riskReport.Stats.HighCount,
                    governanceViolations = governanceViolations.Count
                },
                findings = analysisReport.Findings.Select(f => new
                {
                    ruleId = f.RuleId,
                    category = f.Category.ToString(),
                    severity = f.Severity.ToString(),
                    title = f.Title,
                    description = f.Description,
                    filePath = f.FilePath,
                    lineNumber = f.LineNumber,
                    occurrences = f.Occurrences
                }),
                policy = policyResult != null ? new
                {
                    passed = policyResult.Passed,
                    gates = policyResult.Gates.Select(g => new
                    {
                        name = g.Name,
                        passed = g.Passed,
                        actual = g.Actual,
                        threshold = g.Threshold
                    })
                } : null
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            await File.WriteAllTextAsync(jsonOutputPath, jsonResults);
            Console.WriteLine($"   JSON results saved to: {jsonOutputPath}");
        }

        // CLI summary
        var duration = DateTime.UtcNow - startTime;
        var findingSummary = analysisReport.Findings.Count > 0
            ? $"\n              Findings:          {analysisReport.CriticalCount} critical, {analysisReport.HighCount} high, {analysisReport.MediumCount} medium"
            : "";
        var policyLine = policyResult != null
            ? $"\n              Policy:            {(policyResult.Passed ? "✅ PASSED" : "❌ FAILED")} ({policyResult.Gates.Count} gates)"
            : "";

        Console.WriteLine($"\n{(policyResult?.Passed == false ? "❌" : "✅")} Architecture report saved to: {outputPath}");
        Console.WriteLine($"""

            Summary:
              Components:       {allCodeAtoms.Count:N0}
              Database Objects:  {allSqlAtoms.Count:N0}
              Relationships:     {finalLinks.Count:N0}
              Risk: {riskReport.Stats.CriticalCount} critical, {riskReport.Stats.HighCount} high, {riskReport.Stats.MediumCount} medium
              Governance:        {governanceViolations.Count} violation(s){findingSummary}{policyLine}
              Duration:          {duration.TotalSeconds:F2}s
            """);

        // Exit code: non-zero if policy failed and in CI mode
        if (policyResult != null && !policyResult.Passed && IsCiMode)
        {
            Console.WriteLine("\n❌ Pipeline gate FAILED — see report for details.");
            return 1;
        }

        return 0;
    }
}
