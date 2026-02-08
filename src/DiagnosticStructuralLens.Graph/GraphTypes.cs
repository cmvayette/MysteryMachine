namespace DiagnosticStructuralLens.Graph;

/// <summary>
/// Classification of nodes in the knowledge graph.
/// </summary>
public enum NodeType
{
    // Solution structure
    Solution,
    Project,
    Namespace,
    
    // Type definitions
    Class,
    Interface,
    Struct,
    Record,
    Enum,
    Delegate,
    
    // Members
    Method,
    Property,
    Field,
    Event,
    
    // External dependencies
    ExternalPackage,
    
    // SQL elements
    Table,
    StoredProcedure,
    View,
    Column
}

/// <summary>
/// Classification of edges (relationships) in the knowledge graph.
/// </summary>
public enum EdgeType
{
    /// <summary>Parent-child containment (Solution→Project, Project→Namespace, etc.)</summary>
    Contains,
    
    /// <summary>Project-to-project reference</summary>
    References,
    
    /// <summary>Type dependency (field, parameter, return type)</summary>
    DependsOn,
    
    /// <summary>Interface implementation</summary>
    Implements,
    
    /// <summary>Class inheritance</summary>
    Inherits,
    
    /// <summary>Method invocation</summary>
    Calls,
    
    /// <summary>NuGet package usage</summary>
    UsesPackage,
    
    /// <summary>Stored procedure invocation</summary>
    CallsProc,
    
    /// <summary>Name-based match (e.g., DTO ↔ Table)</summary>
    NameMatch,
    
    /// <summary>Attribute-based binding</summary>
    AttributeBinding,
    
    /// <summary>Query trace relationship</summary>
    QueryTrace
}

// ── Layout Hint Records (Phase 6) ───────────────────────────────────────────

/// <summary>
/// Server-side topology detection result for layout optimization.
/// </summary>
public record LayoutHint(
    string Pattern,
    double Confidence,
    string? HubNodeId,
    IReadOnlyList<string>? PipelineOrder,
    IReadOnlyList<LayerAssignment> LayerAssignments);

/// <summary>
/// Maps a node to an architectural layer (presentation, application, domain, infrastructure, external).
/// </summary>
public record LayerAssignment(string NodeId, string Layer);

/// <summary>
/// Internal intermediate result from a single pattern detector.
/// </summary>
internal record TopologyPattern(
    string Name,
    double Confidence,
    string? HubNodeId = null,
    IReadOnlyList<string>? PipelineOrder = null);
