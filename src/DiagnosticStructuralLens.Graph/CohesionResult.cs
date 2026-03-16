namespace DiagnosticStructuralLens.Graph;

/// <summary>
/// Result of cohesion analysis for a namespace.
/// High CohesionRatio = healthy. Multiple ConnectedComponents = candidate for splitting.
/// </summary>
public record CohesionResult(
    string Namespace,
    int InternalEdges,
    int ExternalEdges,
    double CohesionRatio,
    List<List<string>> ConnectedComponents);
