namespace DiagnosticStructuralLens.Graph;

/// <summary>
/// The complete knowledge graph for a repository/solution.
/// Provides O(1) lookups via indexed dictionaries.
/// </summary>
public class KnowledgeGraph
{
    /// <summary>Unique identifier for this graph instance.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();
    
    /// <summary>Repository or solution path that was analyzed.</summary>
    public string Repository { get; init; } = string.Empty;
    
    /// <summary>When this graph was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    
    /// <summary>Git commit SHA, if available.</summary>
    public string? GitCommit { get; init; }
    
    /// <summary>Git branch name, if available.</summary>
    public string? GitBranch { get; init; }
    
    // === Core Storage ===
    
    private readonly Dictionary<string, GraphNode> _nodesById = new();
    private readonly List<GraphEdge> _allEdges = [];
    
    // === Indexes (built by GraphBuilder) ===
    
    private readonly Dictionary<NodeType, List<GraphNode>> _nodesByType = new();
    private readonly Dictionary<string, List<GraphEdge>> _edgesBySource = new();
    private readonly Dictionary<string, List<GraphEdge>> _edgesByTarget = new();
    private readonly Dictionary<string, List<GraphNode>> _nodesByNamespace = new();
    
    // === Public Accessors ===
    
    /// <summary>All nodes in the graph.</summary>
    public IReadOnlyCollection<GraphNode> Nodes => _nodesById.Values;
    
    /// <summary>All edges in the graph.</summary>
    public IReadOnlyCollection<GraphEdge> Edges => _allEdges;
    
    /// <summary>Total node count.</summary>
    public int NodeCount => _nodesById.Count;
    
    /// <summary>Total edge count.</summary>
    public int EdgeCount => _allEdges.Count;
    
    // === O(1) Lookup Methods ===
    
    /// <summary>
    /// Get a node by its ID. O(1) lookup.
    /// </summary>
    public GraphNode? GetNodeById(string id)
    {
        return _nodesById.GetValueOrDefault(id);
    }
    
    /// <summary>
    /// Get all nodes of a specific type. O(1) lookup.
    /// </summary>
    public IReadOnlyList<GraphNode> GetNodesByType(NodeType type)
    {
        return _nodesByType.GetValueOrDefault(type) ?? [];
    }
    
    /// <summary>
    /// Get all outbound edges from a node. O(1) lookup.
    /// </summary>
    public IReadOnlyList<GraphEdge> GetEdgesBySource(string nodeId)
    {
        return _edgesBySource.GetValueOrDefault(nodeId) ?? [];
    }
    
    /// <summary>
    /// Get all inbound edges to a node. O(1) lookup.
    /// </summary>
    public IReadOnlyList<GraphEdge> GetEdgesByTarget(string nodeId)
    {
        return _edgesByTarget.GetValueOrDefault(nodeId) ?? [];
    }
    
    /// <summary>
    /// Get all nodes in a namespace. O(1) lookup.
    /// </summary>
    public IReadOnlyList<GraphNode> GetNodesByNamespace(string ns)
    {
        return _nodesByNamespace.GetValueOrDefault(ns) ?? [];
    }
    
    // === Internal Methods (used by GraphBuilder) ===
    
    internal void AddNode(GraphNode node)
    {
        _nodesById[node.Id] = node;
    }
    
    internal void AddEdge(GraphEdge edge)
    {
        _allEdges.Add(edge);
    }
    
    internal void BuildIndexes()
    {
        // Index nodes by type
        _nodesByType.Clear();
        foreach (var node in _nodesById.Values)
        {
            if (!_nodesByType.TryGetValue(node.Type, out var list))
            {
                list = [];
                _nodesByType[node.Type] = list;
            }
            list.Add(node);
        }
        
        // Index nodes by namespace
        _nodesByNamespace.Clear();
        foreach (var node in _nodesById.Values)
        {
            if (node.Namespace is { } ns)
            {
                if (!_nodesByNamespace.TryGetValue(ns, out var list))
                {
                    list = [];
                    _nodesByNamespace[ns] = list;
                }
                list.Add(node);
            }
        }
        
        // Index edges by source
        _edgesBySource.Clear();
        foreach (var edge in _allEdges)
        {
            if (!_edgesBySource.TryGetValue(edge.SourceId, out var list))
            {
                list = [];
                _edgesBySource[edge.SourceId] = list;
            }
            list.Add(edge);
        }
        
        // Index edges by target
        _edgesByTarget.Clear();
        foreach (var edge in _allEdges)
        {
            if (!_edgesByTarget.TryGetValue(edge.TargetId, out var list))
            {
                list = [];
                _edgesByTarget[edge.TargetId] = list;
            }
            list.Add(edge);
        }
    }
    
    internal void PopulateNavigation()
    {
        // Populate edge navigation (Source/Target references)
        foreach (var edge in _allEdges)
        {
            edge.Source = _nodesById.GetValueOrDefault(edge.SourceId);
            edge.Target = _nodesById.GetValueOrDefault(edge.TargetId);
        }
        
        // Populate node navigation (InboundEdges/OutboundEdges)
        foreach (var node in _nodesById.Values)
        {
            node.InboundEdges = GetEdgesByTarget(node.Id).ToList();
            node.OutboundEdges = GetEdgesBySource(node.Id).ToList();
        }
    }
}
