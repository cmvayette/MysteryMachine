namespace DiagnosticStructuralLens.Graph;

/// <summary>
/// A directed edge between two nodes in the knowledge graph.
/// </summary>
public class GraphEdge
{
    /// <summary>Unique identifier for this edge.</summary>
    public required string Id { get; init; }
    
    /// <summary>ID of the source node (edge origin).</summary>
    public required string SourceId { get; init; }
    
    /// <summary>ID of the target node (edge destination).</summary>
    public required string TargetId { get; init; }
    
    /// <summary>Classification of this relationship.</summary>
    public required EdgeType Type { get; init; }
    
    /// <summary>
    /// Optional metadata for this edge.
    /// Common keys: "Confidence", "Evidence"
    /// </summary>
    public Dictionary<string, object> Properties { get; init; } = new();
    
    /// <summary>Reference to the source node (populated by graph builder).</summary>
    public GraphNode? Source { get; internal set; }
    
    /// <summary>Reference to the target node (populated by graph builder).</summary>
    public GraphNode? Target { get; internal set; }
    
    // Convenience accessors
    
    /// <summary>Confidence score for this relationship (0.0 - 1.0).</summary>
    public double Confidence => Properties.GetValueOrDefault("Confidence") is double c ? c : 1.0;
    
    /// <summary>Evidence or reason for this relationship.</summary>
    public string? Evidence => Properties.GetValueOrDefault("Evidence") as string;
}
