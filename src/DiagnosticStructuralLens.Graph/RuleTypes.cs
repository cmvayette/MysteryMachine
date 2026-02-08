namespace DiagnosticStructuralLens.Graph;

public enum RuleSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Defines an architectural rule that constraints the graph structure.
/// Typically: "Source nodes matching X must not have edges of type Y to Target nodes matching Z".
/// </summary>
public record ArchitectureRule
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public RuleSeverity Severity { get; init; } = RuleSeverity.Error;
    
    // The "If Source matches..." part
    public required NodeQuery Source { get; init; }
    
    // The "...must not have edge type..." part
    public required EdgeType ForbiddenEdge { get; init; }
    
    // "...to Target matching..." part
    public required NodeQuery Target { get; init; }
}

/// <summary>
/// Criteria for selecting a set of nodes in the graph.
/// All non-null properties must match (AND logic).
/// </summary>
public record NodeQuery
{
    public NodeType? Type { get; init; }
    
    /// <summary>
    /// Glob pattern for node name (e.g. "*Controller", "Order*").
    /// </summary>
    public string? NamePattern { get; init; }
    
    /// <summary>
    /// Glob pattern for namespace (e.g. "*.Domain", "System.*").
    /// </summary>
    public string? NamespacePattern { get; init; }
    
    public bool? IsPublic { get; init; }
}

/// <summary>
/// Represents a specific violation of an architecture rule found in the graph.
/// </summary>
public record RuleViolation(
    ArchitectureRule Rule,
    GraphNode Source,
    GraphNode Target,
    GraphEdge Edge
);
