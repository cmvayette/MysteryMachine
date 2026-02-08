namespace DiagnosticStructuralLens.Graph;

/// <summary>
/// Implementation of the graph query engine.
/// </summary>
public class GraphQueryEngine : IGraphQueryEngine
{
    private readonly KnowledgeGraph _graph;

    public GraphQueryEngine(KnowledgeGraph graph)
    {
        _graph = graph;
    }

    /// <inheritdoc />
    public TraversalResult Traverse(string startNodeId, TraversalDirection direction, int maxDepth = 3)
    {
        var startNode = _graph.GetNodeById(startNodeId);
        if (startNode == null)
        {
            return new TraversalResult(
                null!, // Should ideally be nullable in record or threw, but following simple pattern
                new List<TraversalLevel>(), 
                0
            );
        }

        var levels = new List<TraversalLevel>();
        var visited = new HashSet<string> { startNodeId };
        var totalFound = 0;

        // Queue holds: (Node, Depth, ViaEdge, FromNode)
        var queue = new Queue<(GraphNode Node, int Depth, GraphEdge? ViaEdge, GraphNode? FromNode)>();
        
        // Initial expansion
        ExpandNode(startNode, 0, queue, direction);

        int currentDepthProcessing = 1;
        var currentLevelHits = new List<TraversalHit>();

        while (queue.Count > 0)
        {
            var (currentNode, depth, viaEdge, fromNode) = queue.Dequeue();

            // If we moved to next depth, commit previous level
            if (depth > currentDepthProcessing)
            {
                if (currentLevelHits.Count > 0)
                {
                    levels.Add(new TraversalLevel(currentDepthProcessing, currentLevelHits));
                    currentLevelHits = new List<TraversalHit>();
                }
                currentDepthProcessing = depth;
            }

            // Record this hit (skip root node self-reference in hits)
            if (depth > 0 && viaEdge != null && fromNode != null)
            {
                currentLevelHits.Add(new TraversalHit(currentNode, viaEdge, fromNode));
                totalFound++;
            }

            // Stop expanding if max depth reached
            if (depth >= maxDepth) continue;

            // Expand
            // Check if node has already been visited *as a source*? 
            // Actually BFS visited check usually happens on enqueue.
            // But we might want to visit the same node from different paths?
            // For simple "impact", we usually want distinct nodes. Let's stick to simple visited set.
            ExpandNode(currentNode, depth, queue, direction, visited);
        }

        // Commit final level
        if (currentLevelHits.Count > 0)
        {
            levels.Add(new TraversalLevel(currentDepthProcessing, currentLevelHits));
        }

        return new TraversalResult(startNode, levels, totalFound);
    }

    private void ExpandNode(
        GraphNode node, 
        int currentDepth, 
        Queue<(GraphNode, int, GraphEdge?, GraphNode?)> queue, 
        TraversalDirection direction,
        HashSet<string>? visited = null)
    {
        IEnumerable<GraphEdge> edges = direction switch
        {
            TraversalDirection.Outbound => node.OutboundEdges,
            TraversalDirection.Inbound => node.InboundEdges,
            TraversalDirection.Both => node.OutboundEdges.Concat(node.InboundEdges),
            _ => []
        };

        foreach (var edge in edges)
        {
            // Determine the "other" node
            var otherNode = edge.SourceId == node.Id ? edge.Target : edge.Source;
            if (otherNode == null) continue;

            if (visited == null || visited.Add(otherNode.Id))
            {
                queue.Enqueue((otherNode, currentDepth + 1, edge, node));
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<GraphCycle> FindCycles()
    {
        var cycles = new List<GraphCycle>();
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        var pathStack = new Stack<GraphNode>();

        // Only consider structural edges for cycles to avoid noise
        // e.g. purely structural: DependsOn, Calls, References, Inherits
        // We might want to filter EdgeType here.

        foreach (var node in _graph.Nodes)
        {
            if (visited.Contains(node.Id)) continue;
            
            FindCyclesDfs(node, visited, recursionStack, pathStack, cycles);
        }

        return cycles;
    }

    private void FindCyclesDfs(
        GraphNode node, 
        HashSet<string> visited, 
        HashSet<string> recursionStack, 
        Stack<GraphNode> pathStack,
        List<GraphCycle> cycles)
    {
        visited.Add(node.Id);
        recursionStack.Add(node.Id);
        pathStack.Push(node);

        foreach (var edge in node.OutboundEdges)
        {
            // Filter non-structural edges if needed, but for now take all
            var target = edge.Target;
            if (target == null) continue;

            if (!visited.Contains(target.Id))
            {
                FindCyclesDfs(target, visited, recursionStack, pathStack, cycles);
            }
            else if (recursionStack.Contains(target.Id))
            {
                // Cycle detected!
                // Extract cycle nodes from stack
                var cycleNodes = new List<GraphNode>();
                cycleNodes.Add(target); // The one we looped back to
                
                // Standard DFS cycle reconstruction:
                var stackArray = pathStack.ToArray(); // Stack enumerates LIFO (top to bottom)
                // Array: [Current, Parent, ..., Target, ...]
                
                var cyclePath = new List<GraphNode>();
                foreach (var stackNode in stackArray)
                {
                    cyclePath.Add(stackNode);
                    if (stackNode.Id == target.Id) break;
                }
                cyclePath.Reverse(); // Now: Target -> ... -> Parent -> Current
                // We want: Target -> ... -> Current -> Target
                // But the cycle object is just a list of nodes.
                
                cycles.Add(new GraphCycle(cyclePath, DetermineCycleSeverity(cyclePath)));
            }
        }

        recursionStack.Remove(node.Id);
        pathStack.Pop();
    }

    private CycleSeverity DetermineCycleSeverity(List<GraphNode> nodes)
    {
        // Simple heuristic:
        // Same Namespace? Info
        // Different Namespace / Same Project? Warning
        // Different Project? Error

        var projects = nodes
            .Select(n => n.Properties.GetValueOrDefault("Repository") as string) // Using Repository/Project logic strictly
             // Assuming Project is mapped to 'Repository' or we use properties. 
             // Phase 1 mapped 'Repository' property. Not explicit 'Project'.
             // Let's check Namespace.
            .Distinct()
            .ToList();

        // If defined across multiple "Repositories" (assuming these are Projects in our mock), it's bad.
        if (projects.Count > 1) return CycleSeverity.Error;
        
        // If all same project, check namespace.
        var namespaces = nodes
            .Select(n => n.Namespace)
            .Where(n => n != null)
            .Distinct()
            .ToList();
            
        if (namespaces.Count > 1) return CycleSeverity.Warning;
        
        return CycleSeverity.Info;
    }

    /// <inheritdoc />
    public IReadOnlyList<NodeMetric> CalculateCentrality()
    {
        // Simple Degree Centrality
        // In O(N) since degrees are pre-calculated properties of the graph structure
        return _graph.Nodes
            .Select(n => new NodeMetric(n, n.InboundEdges.Count, n.OutboundEdges.Count))
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<GraphNode> FindOrphans()
    {
        // Nodes with 0 inbound edges
        // Optional: Exclude tests? Start with simple implementation.
        return _graph.Nodes
            .Where(n => n.InboundEdges.Count == 0)
            .ToList();
    }

    /// <inheritdoc />
    public LayoutHint DetectTopology(string? scopeNamespace = null)
    {
        var detector = new PatternDetector();
        return detector.Detect(_graph, scopeNamespace);
    }
}
