namespace DiagnosticStructuralLens.Api.GraphQL;

/// <summary>
/// View model for snapshot comparison results — shows regression/improvement deltas.
/// </summary>
public class SnapshotComparisonView
{
    public required string BaselineId { get; init; }
    public required string CurrentId { get; init; }
    public required string BaselineDate { get; init; }
    public required string CurrentDate { get; init; }
    public required string Repository { get; init; }
    
    // Aggregate stats
    public int BaselineAtomCount { get; init; }
    public int CurrentAtomCount { get; init; }
    public int BaselineNamespaceCount { get; init; }
    public int CurrentNamespaceCount { get; init; }
    
    // Zone deltas
    public int PainZoneDelta { get; init; }
    public int IdealZoneDelta { get; init; }
    public int UselessZoneDelta { get; init; }
    
    // Key metric deltas
    public double AvgDistanceDelta { get; init; }
    public int TotalCouplingDelta { get; init; }
    
    // Per-namespace breakdown
    public List<NamespaceDelta> NamespaceDeltas { get; init; } = [];
}

/// <summary>
/// Per-namespace change between baseline and current snapshot.
/// </summary>
public class NamespaceDelta
{
    public required string Namespace { get; init; }
    
    /// <summary>"NEW", "REMOVED", "REGRESSED", "IMPROVED", "STABLE"</summary>
    public required string Status { get; init; }
    
    public string? OldZone { get; init; }
    public string? NewZone { get; init; }
    public double DistanceDelta { get; init; }
    public int TypesDelta { get; init; }
    public int CouplingDelta { get; init; }  // Ca delta
}
