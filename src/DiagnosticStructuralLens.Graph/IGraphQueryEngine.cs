namespace DiagnosticStructuralLens.Graph;

/// <summary>
/// Engine for performing analysis and traversal queries on the knowledge graph.
/// </summary>
public interface IGraphQueryEngine
{
    /// <summary>
    /// Traverse the graph from a starting node.
    /// </summary>
    /// <param name="startNodeId">ID of the node to start from.</param>
    /// <param name="direction">Which direction to follow edges.</param>
    /// <param name="maxDepth">Maximum number of hops to traverse.</param>
    /// <returns>Hierarchical result of the traversal.</returns>
    TraversalResult Traverse(string startNodeId, TraversalDirection direction, int maxDepth = 3);
    
    /// <summary>
    /// Find all cycles in the graph.
    /// Focuses on structural dependencies (DependsOn, Calls, References).
    /// </summary>
    /// <returns>List of detected cycles.</returns>
    IReadOnlyList<GraphCycle> FindCycles();
    
    /// <summary>
    /// Calculate centrality metrics for all nodes to identify hubs.
    /// </summary>
    /// <returns>Metrics for every node in the graph.</returns>
    IReadOnlyList<NodeMetric> CalculateCentrality();
    
    /// <summary>
    /// Find nodes that have no inbound dependencies (potential dead code or roots).
    /// </summary>
    /// <returns>List of orphan nodes.</returns>
    IReadOnlyList<GraphNode> FindOrphans();
    
    /// <summary>
    /// Detect the dominant topology pattern for layout optimization.
    /// Optionally scoped to a specific namespace.
    /// </summary>
    /// <param name="scopeNamespace">Optional namespace to scope detection to.</param>
    /// <returns>Layout hint with pattern, confidence, and metadata.</returns>
    LayoutHint DetectTopology(string? scopeNamespace = null);
}
