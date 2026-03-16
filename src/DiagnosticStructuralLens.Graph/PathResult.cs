namespace DiagnosticStructuralLens.Graph;

/// <summary>
/// A single hop in a dependency path: From → (via edge) → To.
/// </summary>
public record PathStep(GraphNode From, GraphNode To, GraphEdge Via);

/// <summary>
/// Result of a shortest-path query between two nodes.
/// </summary>
public record PathResult(IReadOnlyList<PathStep> Steps, int Length);
