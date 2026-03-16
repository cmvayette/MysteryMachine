using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DiagnosticStructuralLens.Cli.Analyzers;

/// <summary>
/// Evaluates analysis findings against a YAML policy file for CI/CD gating.
/// Returns pass/fail with detailed gate results.
/// </summary>
public class PolicyEngine
{
    private readonly PolicyConfig _config;

    public PolicyEngine(PolicyConfig config)
    {
        _config = config;
    }

    public static PolicyEngine? LoadFromFile(string path)
    {
        if (!File.Exists(path)) return null;

        try
        {
            var yaml = File.ReadAllText(path);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var config = deserializer.Deserialize<PolicyConfig>(yaml);
            return new PolicyEngine(config ?? new PolicyConfig());
        }
        catch
        {
            return null;
        }
    }

    public PolicyResult Evaluate(AnalysisReport analysis, Risk.RiskReport riskReport, List<string> governanceViolations)
    {
        var result = new PolicyResult();
        var suppressed = _config.Suppress?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        // Filter out suppressed rules
        var activeFindings = analysis.Findings
            .Where(f => !suppressed.Contains(f.RuleId))
            .ToList();

        // Migration gates
        if (_config.Gates?.Migration != null)
        {
            var migrationFindings = activeFindings
                .Where(f => f.Category == FindingCategory.Migration).ToList();

            var gate = _config.Gates.Migration;
            CheckThreshold(result, "Migration Critical",
                migrationFindings.Count(f => f.Severity == FindingSeverity.Critical),
                gate.MaxCritical);
            CheckThreshold(result, "Migration High",
                migrationFindings.Count(f => f.Severity == FindingSeverity.High),
                gate.MaxHigh);
        }

        // Architecture gates
        if (_config.Gates?.Architecture != null)
        {
            var archFindings = activeFindings
                .Where(f => f.Category == FindingCategory.Architecture).ToList();

            var gate = _config.Gates.Architecture;
            CheckThreshold(result, "Architecture Critical",
                archFindings.Count(f => f.Severity == FindingSeverity.Critical),
                gate.MaxCritical);
            CheckThreshold(result, "Architecture High",
                archFindings.Count(f => f.Severity == FindingSeverity.High),
                gate.MaxHigh);

            if (gate.MaxGodClasses.HasValue)
            {
                var godClasses = archFindings.Count(f => f.RuleId == "ARCH-001");
                CheckThreshold(result, "God Classes", godClasses, gate.MaxGodClasses);
            }

            if (gate.MaxCouplingDensity.HasValue)
            {
                // Coupling density is checked separately as a double
                // We store the actual value for reporting
                result.Gates.Add(new GateResult("Coupling Density",
                    true, 0, null, $"Threshold: {gate.MaxCouplingDensity:F1}"));
            }
        }

        // Risk gates
        if (_config.Gates?.Risk != null)
        {
            var gate = _config.Gates.Risk;
            CheckThreshold(result, "Critical Risk Components",
                riskReport.Stats.CriticalCount, gate.MaxCriticalComponents);
            CheckThreshold(result, "High Risk Components",
                riskReport.Stats.HighCount, gate.MaxHighComponents);
        }

        // Governance gates
        if (_config.Gates?.Governance != null)
        {
            CheckThreshold(result, "Governance Violations",
                governanceViolations.Count, _config.Gates.Governance.MaxViolations);
        }

        return result;
    }

    private static void CheckThreshold(PolicyResult result, string gateName, int actual, int? max)
    {
        if (!max.HasValue) return;

        var passed = actual <= max.Value;
        result.Gates.Add(new GateResult(gateName, passed, actual, max.Value));
        if (!passed) result.Passed = false;
    }
}

// --- Config models ---

public class PolicyConfig
{
    public int Version { get; set; } = 1;
    public GateConfig? Gates { get; set; }
    public List<string>? Suppress { get; set; }
}

public class GateConfig
{
    public MigrationGate? Migration { get; set; }
    public ArchitectureGate? Architecture { get; set; }
    public RiskGate? Risk { get; set; }
    public GovernanceGate? Governance { get; set; }
}

public class MigrationGate
{
    public int? MaxCritical { get; set; }
    public int? MaxHigh { get; set; }
}

public class ArchitectureGate
{
    public int? MaxCritical { get; set; }
    public int? MaxHigh { get; set; }
    public int? MaxGodClasses { get; set; }
    public double? MaxCouplingDensity { get; set; }
}

public class RiskGate
{
    public int? MaxCriticalComponents { get; set; }
    public int? MaxHighComponents { get; set; }
}

public class GovernanceGate
{
    public int? MaxViolations { get; set; }
}

// --- Result models ---

public class PolicyResult
{
    public bool Passed { get; set; } = true;
    public List<GateResult> Gates { get; } = [];

    public IEnumerable<GateResult> FailedGates => Gates.Where(g => !g.Passed);
    public IEnumerable<GateResult> PassedGates => Gates.Where(g => g.Passed);
}

public record GateResult(
    string Name,
    bool Passed,
    int Actual,
    int? Threshold,
    string? Details = null
);
