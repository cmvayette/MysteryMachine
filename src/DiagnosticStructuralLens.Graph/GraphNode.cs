namespace DiagnosticStructuralLens.Graph;

/// <summary>
/// A single node in the knowledge graph representing a code element, SQL element, or structural unit.
/// </summary>
public class GraphNode
{
    /// <summary>Unique identifier for this node.</summary>
    public required string Id { get; init; }
    
    /// <summary>Display name of the element.</summary>
    public required string Name { get; init; }
    
    /// <summary>Classification of this node.</summary>
    public required NodeType Type { get; init; }
    
    /// <summary>Source file path, if applicable.</summary>
    public string? FilePath { get; init; }
    
    /// <summary>Line number in source file, if applicable.</summary>
    public int? LineNumber { get; init; }
    
    /// <summary>
    /// Flexible properties based on node type.
    /// Common keys: "Namespace", "Accessibility", "Signature", "Attributes", "IsPublic"
    /// </summary>
    public Dictionary<string, object> Properties { get; init; } = new();
    
    /// <summary>All edges pointing TO this node (populated by graph builder).</summary>
    public IReadOnlyList<GraphEdge> InboundEdges { get; internal set; } = [];
    
    /// <summary>All edges pointing FROM this node (populated by graph builder).</summary>
    public IReadOnlyList<GraphEdge> OutboundEdges { get; internal set; } = [];
    
    // Convenience accessors for common properties
    
    /// <summary>Namespace of the element, if applicable.</summary>
    public string? Namespace => Properties.GetValueOrDefault("Namespace") as string;
    
    /// <summary>Accessibility modifier (public, internal, private, etc.).</summary>
    public string? Accessibility => Properties.GetValueOrDefault("Accessibility") as string;
    
    /// <summary>Whether this element is publicly accessible.</summary>
    public bool IsPublic => Properties.GetValueOrDefault("IsPublic") is true;
    
    /// <summary>Method or property signature, if applicable.</summary>
    public string? Signature => Properties.GetValueOrDefault("Signature") as string;
}
