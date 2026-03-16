namespace DiagnosticStructuralLens.Graph;

/// <summary>
/// Result of simulating a node move to a different namespace.
/// </summary>
public record SimulationResult(
    int BrokenEdgeCount,
    int NewCrossingCount,
    IReadOnlyList<GraphEdge> BrokenEdges,
    IReadOnlyList<GraphEdge> NewCrossings);

/// <summary>
/// API surface analysis: cross-references resolvers with domain services.
/// </summary>
public record ApiSurfaceAnalysis(
    int TotalResolvers,
    IReadOnlyList<string> UnbackedResolvers,
    int TotalDomainServices,
    IReadOnlyList<string> UnexposedServices);
