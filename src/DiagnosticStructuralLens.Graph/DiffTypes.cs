namespace DiagnosticStructuralLens.Graph;

/// <summary>
/// Represents the raw topological differences between two graphs.
/// </summary>
public record GraphDiff(
    IReadOnlyList<GraphNode> AddedNodes,
    IReadOnlyList<GraphNode> RemovedNodes,
    IReadOnlyList<GraphEdge> AddedEdges,
    IReadOnlyList<GraphEdge> RemovedEdges
);

/// <summary>
/// Represents the semantic structural impact of changes.
/// Focuses on "What broke?" rather than just "What changed?".
/// </summary>
public record StructuralDiff(
    GraphDiff Topology,
    
    /// <summary>
    /// Rule violations that exist in the Current graph but NOT in the Baseline.
    /// These represent architectural regressions.
    /// </summary>
    IReadOnlyList<RuleViolation> NewViolations,
    
    /// <summary>
    /// Cycles that exist in the Current graph but NOT in the Baseline.
    /// </summary>
    IReadOnlyList<GraphCycle> NewCycles
    
    // Potential Future: NewOrphans, SignificantCentralityShifts
);
