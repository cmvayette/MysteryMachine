using DiagnosticStructuralLens.Core;

namespace DiagnosticStructuralLens.Cli.Analyzers;

/// <summary>
/// Detects architectural anti-patterns and code smells.
/// Rules ARCH-001 through ARCH-007.
/// </summary>
public class ArchitectureAnalyzer : IAnalyzer
{
    public FindingCategory Category => FindingCategory.Architecture;

    private const int GodClassMethodThreshold = 30;
    private const int GodClassPropertyThreshold = 50;
    private const int MissingInterfaceFanInThreshold = 5;
    private const int ExcessiveStaticMethodThreshold = 10;
    private const int ControllerLocThreshold = 500;

    public IReadOnlyList<AnalyzerFinding> Analyze(
        string repoPath, Snapshot snapshot, List<ScanDiagnostic> diagnostics)
    {
        var findings = new List<AnalyzerFinding>();

        CheckGodClasses(snapshot, findings);
        CheckCircularNamespaceDeps(snapshot, findings);
        CheckMissingInterfaces(snapshot, findings);
        CheckControllerComplexity(repoPath, snapshot, findings);
        CheckExcessiveStatics(repoPath, findings);
        CheckSingletonAntiPattern(repoPath, findings);
        CheckServiceLocator(repoPath, findings);

        return findings;
    }

    // ARCH-001: God classes (too many methods or properties)
    private static void CheckGodClasses(Snapshot snapshot, List<AnalyzerFinding> findings)
    {
        var classMemberCounts = new Dictionary<string, (int Methods, int Properties, string? File)>();

        foreach (var atom in snapshot.CodeAtoms)
        {
            if (atom.Type == AtomType.Class || atom.Type == AtomType.Record)
            {
                // Count members via containment links
                var containedMethods = snapshot.Links
                    .Where(l => l.SourceId == atom.Id && l.Type == LinkType.Contains)
                    .Join(snapshot.CodeAtoms, l => l.TargetId, a => a.Id, (l, a) => a)
                    .ToList();

                var methodCount = containedMethods.Count(a => a.Type == AtomType.Method);
                var propertyCount = containedMethods.Count(a => a.Type == AtomType.Property);

                if (methodCount >= GodClassMethodThreshold || propertyCount >= GodClassPropertyThreshold)
                {
                    classMemberCounts[atom.Name] = (methodCount, propertyCount, atom.FilePath);
                }
            }
        }

        foreach (var (name, (methods, properties, file)) in classMemberCounts)
        {
            var detail = methods >= GodClassMethodThreshold
                ? $"{methods} methods"
                : $"{properties} properties";
            findings.Add(new AnalyzerFinding(
                FindingCategory.Architecture, FindingSeverity.High,
                "ARCH-001", $"God Class: {name}",
                $"Class has {detail} — consider extracting responsibilities into smaller, focused classes.",
                file, null));
        }
    }

    // ARCH-002: Circular namespace dependencies
    private static void CheckCircularNamespaceDeps(Snapshot snapshot, List<AnalyzerFinding> findings)
    {
        // Build namespace-level dependency graph from links
        var atomNamespaces = snapshot.CodeAtoms.ToDictionary(a => a.Id, a => a.Namespace);
        var namespaceDeps = new Dictionary<string, HashSet<string>>();

        foreach (var link in snapshot.Links.Where(l => l.Type != LinkType.Contains))
        {
            if (atomNamespaces.TryGetValue(link.SourceId, out var sourceNs) &&
                atomNamespaces.TryGetValue(link.TargetId, out var targetNs) &&
                !string.IsNullOrEmpty(sourceNs) && !string.IsNullOrEmpty(targetNs) &&
                sourceNs != targetNs)
            {
                if (!namespaceDeps.ContainsKey(sourceNs))
                    namespaceDeps[sourceNs] = [];
                namespaceDeps[sourceNs].Add(targetNs);
            }
        }

        // Find bidirectional dependencies (cycles)
        var reported = new HashSet<string>();
        foreach (var (ns, deps) in namespaceDeps)
        {
            foreach (var dep in deps)
            {
                if (namespaceDeps.TryGetValue(dep, out var reverseDeps) && reverseDeps.Contains(ns))
                {
                    var key = string.Compare(ns, dep, StringComparison.Ordinal) < 0
                        ? $"{ns}<>{dep}" : $"{dep}<>{ns}";
                    if (reported.Add(key))
                    {
                        findings.Add(new AnalyzerFinding(
                            FindingCategory.Architecture, FindingSeverity.High,
                            "ARCH-002", "Circular Namespace Dependency",
                            $"Bidirectional dependency between `{ns}` and `{dep}` — indicates tangled architecture that blocks modular migration.",
                            null, null));
                    }
                }
            }
        }
    }

    // ARCH-003: Missing interface abstractions on high-fan-in classes
    private static void CheckMissingInterfaces(Snapshot snapshot, List<AnalyzerFinding> findings)
    {
        var interfaces = snapshot.CodeAtoms
            .Where(a => a.Type == AtomType.Interface)
            .Select(a => a.Name.TrimStart('I'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Count inbound relationships per class
        var fanIn = new Dictionary<string, int>();
        foreach (var link in snapshot.Links.Where(l => l.Type != LinkType.Contains))
        {
            fanIn[link.TargetId] = fanIn.GetValueOrDefault(link.TargetId) + 1;
        }

        foreach (var atom in snapshot.CodeAtoms.Where(a => a.Type == AtomType.Class))
        {
            if (fanIn.GetValueOrDefault(atom.Id) >= MissingInterfaceFanInThreshold &&
                !interfaces.Contains(atom.Name))
            {
                findings.Add(new AnalyzerFinding(
                    FindingCategory.Architecture, FindingSeverity.Medium,
                    "ARCH-003", $"Missing Interface: {atom.Name}",
                    $"Class has {fanIn[atom.Id]} inbound dependencies but no corresponding interface — reduces testability and flexibility.",
                    atom.FilePath, atom.LineNumber));
            }
        }
    }

    // ARCH-004: Business logic in controllers (high LOC)
    private static void CheckControllerComplexity(string repoPath, Snapshot snapshot, List<AnalyzerFinding> findings)
    {
        var controllers = snapshot.CodeAtoms
            .Where(a => a.Type == AtomType.Class && a.Name.EndsWith("Controller") && a.FilePath != null);

        foreach (var ctrl in controllers)
        {
            try
            {
                var filePath = Path.IsPathRooted(ctrl.FilePath!)
                    ? ctrl.FilePath!
                    : Path.Combine(repoPath, ctrl.FilePath!);

                if (File.Exists(filePath))
                {
                    var lineCount = File.ReadAllLines(filePath).Length;
                    if (lineCount > ControllerLocThreshold)
                    {
                        findings.Add(new AnalyzerFinding(
                            FindingCategory.Architecture, FindingSeverity.Medium,
                            "ARCH-004", $"Complex Controller: {ctrl.Name}",
                            $"Controller has {lineCount} lines — extract business logic to service classes.",
                            ctrl.FilePath, ctrl.LineNumber));
                    }
                }
            }
            catch { /* skip */ }
        }
    }

    // ARCH-005: Excessive static methods
    private static void CheckExcessiveStatics(string repoPath, List<AnalyzerFinding> findings)
    {
        var csFiles = Directory.EnumerateFiles(repoPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")
                     && !f.Contains("/bin/") && !f.Contains("\\bin\\"));

        foreach (var file in csFiles)
        {
            try
            {
                var content = File.ReadAllText(file);
                if (content.Contains("static class"))
                {
                    var staticMethodCount = content.Split('\n')
                        .Count(l => l.Contains("public static") || l.Contains("internal static"));

                    if (staticMethodCount >= ExcessiveStaticMethodThreshold)
                    {
                        var className = Path.GetFileNameWithoutExtension(file);
                        findings.Add(new AnalyzerFinding(
                            FindingCategory.Architecture, FindingSeverity.Medium,
                            "ARCH-005", $"Excessive Statics: {className}",
                            $"Static class has {staticMethodCount} static methods — consider converting to injectable service.",
                            file, null));
                    }
                }
            }
            catch { /* skip */ }
        }
    }

    // ARCH-006: Singleton anti-pattern
    private static void CheckSingletonAntiPattern(string repoPath, List<AnalyzerFinding> findings)
    {
        var csFiles = Directory.EnumerateFiles(repoPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")
                     && !f.Contains("/bin/") && !f.Contains("\\bin\\"));

        var count = 0;
        string? firstFile = null;

        foreach (var file in csFiles)
        {
            try
            {
                var content = File.ReadAllText(file);
                if (content.Contains("static") && content.Contains("Instance") &&
                    (content.Contains("private") && content.Contains("new ")))
                {
                    count++;
                    firstFile ??= file;
                }
            }
            catch { /* skip */ }
        }

        if (count > 0)
        {
            findings.Add(new AnalyzerFinding(
                FindingCategory.Architecture, FindingSeverity.Medium,
                "ARCH-006", "Singleton Anti-Pattern",
                "Static Instance pattern detected — use dependency injection for better testability and lifecycle management.",
                firstFile, null, count));
        }
    }

    // ARCH-007: Service Locator anti-pattern
    private static void CheckServiceLocator(string repoPath, List<AnalyzerFinding> findings)
    {
        var csFiles = Directory.EnumerateFiles(repoPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")
                     && !f.Contains("/bin/") && !f.Contains("\\bin\\"));

        var count = 0;
        string? firstFile = null;

        foreach (var file in csFiles)
        {
            try
            {
                var content = File.ReadAllText(file);
                if (content.Contains("ServiceLocator.Current") ||
                    content.Contains("DependencyResolver.Current") ||
                    content.Contains("Container.Resolve"))
                {
                    count++;
                    firstFile ??= file;
                }
            }
            catch { /* skip */ }
        }

        if (count > 0)
        {
            findings.Add(new AnalyzerFinding(
                FindingCategory.Architecture, FindingSeverity.High,
                "ARCH-007", "Service Locator Anti-Pattern",
                "Service Locator hides dependencies — migrate to constructor injection with the built-in DI container.",
                firstFile, null, count));
        }
    }
}
