using DiagnosticStructuralLens.Core;
using DiagnosticStructuralLens.Linker;

namespace DiagnosticStructuralLens.Risk;

/// <summary>
/// Calculates risk scores for atoms based on blast radius, coupling, and cross-domain factors.
/// </summary>
public class RiskScorer
{
    private readonly SemanticLinker _linker = new();

    // Factor weights (must sum to 1.0)
    public const double BlastRadiusWeight = 0.40;
    public const double InboundCouplingWeight = 0.25;
    public const double OutboundCouplingWeight = 0.15;
    public const double CrossDomainWeight = 0.20;

    // Scaling factors for normalization
    private const int BlastRadiusMax = 50;      // 50+ dependents = max score
    private const int InboundCouplingMax = 30;  // 30+ incoming links = max score
    private const int OutboundCouplingMax = 20; // 20+ outgoing links = max score
    private const int CrossDomainMax = 10;      // 10+ cross-domain links = max score

    /// <summary>
    /// Score all atoms in a snapshot and generate a risk report.
    /// </summary>
    public RiskReport ScoreSnapshot(Snapshot snapshot)
    {
        var codeAtomIds = snapshot.CodeAtoms.Select(a => a.Id).ToHashSet();
        var sqlAtomIds = snapshot.SqlAtoms.Select(a => a.Id).ToHashSet();
        var allAtomIds = codeAtomIds.Union(sqlAtomIds).ToList();

        var scores = new List<AtomRiskScore>();

        foreach (var atomId in allAtomIds)
        {
            var score = ScoreAtom(atomId, snapshot.Links, codeAtomIds, sqlAtomIds);
            scores.Add(score);
        }

        // Sort by composite score descending
        scores = scores.OrderByDescending(s => s.CompositeScore).ToList();

        return new RiskReport
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            SnapshotId = snapshot.Id,
            TotalAtoms = allAtomIds.Count,
            Scores = scores,
            Hotspots = scores.Where(s => s.Level == RiskLevel.Critical || s.Level == RiskLevel.High).ToList(),
            Stats = new RiskStats
            {
                CriticalCount = scores.Count(s => s.Level == RiskLevel.Critical),
                HighCount = scores.Count(s => s.Level == RiskLevel.High),
                MediumCount = scores.Count(s => s.Level == RiskLevel.Medium),
                LowCount = scores.Count(s => s.Level == RiskLevel.Low),
                AverageScore = scores.Count > 0 ? scores.Average(s => s.CompositeScore) : 0
            }
        };
    }

    /// <summary>
    /// Calculate risk score for a single atom.
    /// </summary>
    public AtomRiskScore ScoreAtom(string atomId, IEnumerable<AtomLink> links, 
        HashSet<string>? codeAtomIds = null, HashSet<string>? sqlAtomIds = null)
    {
        var linkList = links.ToList();
        codeAtomIds ??= [];
        sqlAtomIds ??= [];

        // Calculate individual factors
        var blastRadius = CalculateBlastRadiusScore(atomId, linkList);
        var inboundCoupling = CalculateInboundCoupling(atomId, linkList);
        var outboundCoupling = CalculateOutboundCoupling(atomId, linkList);
        var crossDomain = CalculateCrossDomainScore(atomId, linkList, codeAtomIds, sqlAtomIds);

        // Calculate weighted composite
        var composite = 
            blastRadius * BlastRadiusWeight +
            inboundCoupling * InboundCouplingWeight +
            outboundCoupling * OutboundCouplingWeight +
            crossDomain * CrossDomainWeight;

        return new AtomRiskScore
        {
            AtomId = atomId,
            BlastRadiusScore = blastRadius,
            InboundCouplingScore = inboundCoupling,
            OutboundCouplingScore = outboundCoupling,
            CrossDomainScore = crossDomain,
            CompositeScore = composite,
            Level = ClassifyRisk(composite)
        };
    }

    /// <summary>
    /// Calculate blast radius score (0-100) based on transitive dependent count.
    /// </summary>
    public double CalculateBlastRadiusScore(string atomId, IEnumerable<AtomLink> links)
    {
        var linkList = links.ToList();
        var blastRadius = _linker.GetBlastRadius(atomId, linkList, maxDepth: 5);
        
        // Weight by depth: depth 1 = full weight, depth 5 = 20% weight
        var weightedCount = blastRadius.AffectedAtoms
            .Sum(a => 1.0 / a.Depth);

        return NormalizeScore(weightedCount, BlastRadiusMax);
    }

    /// <summary>
    /// Calculate inbound coupling score (0-100) based on incoming link count.
    /// </summary>
    public double CalculateInboundCoupling(string atomId, IEnumerable<AtomLink> links)
    {
        var inboundCount = links.Count(l => l.TargetId == atomId);
        return NormalizeScore(inboundCount, InboundCouplingMax);
    }

    /// <summary>
    /// Calculate outbound coupling score (0-100) based on outgoing link count.
    /// </summary>
    public double CalculateOutboundCoupling(string atomId, IEnumerable<AtomLink> links)
    {
        var outboundCount = links.Count(l => l.SourceId == atomId);
        return NormalizeScore(outboundCount, OutboundCouplingMax);
    }

    /// <summary>
    /// Calculate cross-domain score (0-100) based on links crossing C#↔SQL boundary.
    /// </summary>
    public double CalculateCrossDomainScore(string atomId, IEnumerable<AtomLink> links,
        HashSet<string> codeAtomIds, HashSet<string> sqlAtomIds)
    {
        var isCodeAtom = codeAtomIds.Contains(atomId);
        var isSqlAtom = sqlAtomIds.Contains(atomId);

        if (!isCodeAtom && !isSqlAtom)
            return 0;

        var crossDomainCount = 0;

        foreach (var link in links)
        {
            if (link.SourceId == atomId)
            {
                // Outgoing link - check if target is in different domain
                var targetIsCode = codeAtomIds.Contains(link.TargetId);
                var targetIsSql = sqlAtomIds.Contains(link.TargetId);

                if ((isCodeAtom && targetIsSql) || (isSqlAtom && targetIsCode))
                    crossDomainCount++;
            }
            else if (link.TargetId == atomId)
            {
                // Incoming link - check if source is in different domain
                var sourceIsCode = codeAtomIds.Contains(link.SourceId);
                var sourceIsSql = sqlAtomIds.Contains(link.SourceId);

                if ((isCodeAtom && sourceIsSql) || (isSqlAtom && sourceIsCode))
                    crossDomainCount++;
            }
        }

        return NormalizeScore(crossDomainCount, CrossDomainMax);
    }

    /// <summary>
    /// Classify a composite score into a risk level.
    /// </summary>
    public static RiskLevel ClassifyRisk(double score)
    {
        return score switch
        {
            > 75 => RiskLevel.Critical,
            > 50 => RiskLevel.High,
            > 25 => RiskLevel.Medium,
            _ => RiskLevel.Low
        };
    }

    /// <summary>
    /// Get the top N riskiest atoms from a report.
    /// </summary>
    public static List<AtomRiskScore> GetTopRisks(RiskReport report, int count = 10)
    {
        return report.Scores.Take(count).ToList();
    }

    private static double NormalizeScore(double value, double max)
    {
        if (value <= 0) return 0;
        if (value >= max) return 100;
        return (value / max) * 100;
    }
}

/// <summary>
/// Risk score for a single atom.
/// </summary>
public record AtomRiskScore
{
    public required string AtomId { get; init; }
    public double BlastRadiusScore { get; init; }
    public double InboundCouplingScore { get; init; }
    public double OutboundCouplingScore { get; init; }
    public double CrossDomainScore { get; init; }
    public double CompositeScore { get; init; }
    public RiskLevel Level { get; init; }
}

/// <summary>
/// Risk level classification.
/// </summary>
public enum RiskLevel
{
    Low,      // 0-25
    Medium,   // 26-50
    High,     // 51-75
    Critical  // 76-100
}

/// <summary>
/// Complete risk report for a snapshot.
/// </summary>
public record RiskReport
{
    public DateTimeOffset GeneratedAt { get; init; }
    public required string SnapshotId { get; init; }
    public int TotalAtoms { get; init; }
    public required List<AtomRiskScore> Scores { get; init; }
    public required List<AtomRiskScore> Hotspots { get; init; }
    public required RiskStats Stats { get; init; }
}

/// <summary>
/// Statistics summary for a risk report.
/// </summary>
public record RiskStats
{
    public int CriticalCount { get; init; }
    public int HighCount { get; init; }
    public int MediumCount { get; init; }
    public int LowCount { get; init; }
    public double AverageScore { get; init; }
}
