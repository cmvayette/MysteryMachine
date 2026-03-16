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
    
    /// <summary>
    /// When set, violations are suppressed if source and target share the same
    /// namespace group prefix. The prefix is extracted by splitting on the separator
    /// and taking the first N segments (depth).
    /// Example: separator="::", depth=1 means "same workspace" exclusion.
    ///          separator=".", depth=2 after "::" means "same domain" exclusion.
    /// </summary>
    public GroupExclusion? SameGroupExclusion { get; init; }
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

/// <summary>
/// Defines how to extract a "group" from a namespace for same-group exclusion.
/// The namespace is split on PrimarySeparator first, then the segment at SegmentIndex
/// is further split on SecondarySeparator and the first Depth parts are taken as the group.
/// </summary>
public record GroupExclusion
{
    /// <summary>Primary separator (e.g. "::" for TypeScript workspaces).</summary>
    public required string PrimarySeparator { get; init; }
    
    /// <summary>Which segment (after primary split) contains the group. 0=workspace, 1=subdir path.</summary>
    public int SegmentIndex { get; init; } = 1;
    
    /// <summary>Secondary separator within the segment (e.g. "." for subdirectory levels).</summary>
    public string SecondarySeparator { get; init; } = ".";
    
    /// <summary>Number of secondary segments to take as the group prefix.</summary>
    public int Depth { get; init; } = 1;
}
