namespace DiagnosticStructuralLens.Graph;

/// <summary>
/// Direction to traverse the graph.
/// </summary>
public enum TraversalDirection
{
    /// <summary>Follow outbound edges (dependencies).</summary>
    Outbound,
    
    /// <summary>Follow inbound edges (dependents).</summary>
    Inbound,
    
    /// <summary>Follow edges in both directions.</summary>
    Both
}

/// <summary>
/// Result of a graph traversal operation.
/// </summary>
public record TraversalResult(
    GraphNode StartNode, 
    IReadOnlyList<TraversalLevel> Levels, 
    int TotalNodesFound
);

/// <summary>
/// A collection of nodes found at a specific depth.
/// </summary>
public record TraversalLevel(
    int Depth, 
    IReadOnlyList<TraversalHit> Hits
);

/// <summary>
/// A single node found during traversal, including how we got there.
/// </summary>
public record TraversalHit(
    GraphNode Node, 
    GraphEdge ViaEdge, 
    GraphNode FromNode
);

/// <summary>
/// A cycle detected in the graph.
/// </summary>
public record GraphCycle(
    IReadOnlyList<GraphNode> Nodes, 
    CycleSeverity Severity
);

/// <summary>
/// Severity of a detected cycle.
/// </summary>
public enum CycleSeverity
{
    /// <summary>Cycle within the same project/container (often acceptable).</summary>
    Info,
    
    /// <summary>Cycle between different projects (usually bad).</summary>
    Warning,
    
    /// <summary>Cycle between architectural layers (critical violation).</summary>
    Error
}

/// <summary>
/// Centrality metrics for a single node.
/// </summary>
public record NodeMetric(
    GraphNode Node, 
    int InDegree, 
    int OutDegree
)
{
    public int TotalDegree => InDegree + OutDegree;
}
