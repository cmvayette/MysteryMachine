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

    /// <summary>
    /// Calculate Robert C. Martin's Package Metrics per namespace.
    /// Computes Instability (I), Abstractness (A), and Distance from Main Sequence (D).
    /// </summary>
    public IReadOnlyList<NamespaceMetrics> CalculatePackageMetrics()
    {
        var results = new List<NamespaceMetrics>();
        var namespaces = _graph.Nodes
            .Where(n => n.Namespace != null)
            .GroupBy(n => n.Namespace!);

        foreach (var nsGroup in namespaces)
        {
            var ns = nsGroup.Key;
            var types = nsGroup.Where(n => IsTypeNode(n.Type)).ToList();
            var abstractions = types.Count(n =>
                n.Type is NodeType.Interface or NodeType.Enum or NodeType.TypeAlias);

            // Ca: edges coming INTO this namespace from outside
            int ca = nsGroup.Sum(n => n.InboundEdges
                .Count(e => e.Source?.Namespace != null && e.Source.Namespace != ns));

            // Ce: edges going OUT of this namespace to outside
            int ce = nsGroup.Sum(n => n.OutboundEdges
                .Count(e => e.Target?.Namespace != null && e.Target.Namespace != ns));

            double instability = (ca + ce) > 0 ? (double)ce / (ca + ce) : 0;
            double abstractness = types.Count > 0
                ? (double)abstractions / types.Count : 0;
            double distance = Math.Abs(abstractness + instability - 1);

            string zone = distance < 0.3 ? "Ideal"
                : (instability < 0.5 && abstractness < 0.5) ? "Pain"
                : "Uselessness";

            results.Add(new NamespaceMetrics
            {
                Namespace = ns,
                TotalTypes = types.Count,
                AbstractTypes = abstractions,
                AfferentCoupling = ca,
                EfferentCoupling = ce,
                Instability = Math.Round(instability, 3),
                Abstractness = Math.Round(abstractness, 3),
                DistanceFromMainSequence = Math.Round(distance, 3),
                Zone = zone
            });
        }
        return results;
    }

    private static bool IsTypeNode(NodeType type) =>
        type is NodeType.Class or NodeType.Interface or NodeType.Struct
            or NodeType.Record or NodeType.Enum or NodeType.Delegate
            or NodeType.TypeAlias;

    /// <summary>
    /// BFS shortest path between two nodes.
    /// Returns null if no path exists.
    /// </summary>
    public PathResult? FindShortestPath(string fromId, string toId)
    {
        var startNode = _graph.GetNodeById(fromId);
        var endNode = _graph.GetNodeById(toId);
        if (startNode == null || endNode == null) return null;

        var visited = new HashSet<string> { fromId };
        var queue = new Queue<(GraphNode Node, List<PathStep> Path)>();
        queue.Enqueue((startNode, new List<PathStep>()));

        while (queue.Count > 0)
        {
            var (current, path) = queue.Dequeue();
            foreach (var edge in current.OutboundEdges)
            {
                if (edge.Target == null) continue;
                var step = new PathStep(current, edge.Target, edge);
                var newPath = new List<PathStep>(path) { step };

                if (edge.Target.Id == toId)
                    return new PathResult(newPath, newPath.Count);

                if (visited.Add(edge.Target.Id))
                    queue.Enqueue((edge.Target, newPath));
            }
        }
        return null; // No path exists
    }

    /// <summary>
    /// Analyze internal cohesion of a namespace.
    /// Uses connected-component detection on internal edges only.
    /// Multiple components suggest the namespace could be split.
    /// </summary>
    public CohesionResult AnalyzeCohesion(string ns)
    {
        var nodes = _graph.GetNodesByNamespace(ns);
        if (nodes.Count == 0) return new CohesionResult(ns, 0, 0, 1.0, new());

        var nodeIds = nodes.Select(n => n.Id).ToHashSet();

        // Count internal vs external edges
        int internalEdges = nodes.Sum(n =>
            n.OutboundEdges.Count(e => nodeIds.Contains(e.TargetId)));
        int externalEdges = nodes.Sum(n =>
            n.OutboundEdges.Count(e => !nodeIds.Contains(e.TargetId)));

        // Connected components via union-find
        var parent = nodes.ToDictionary(n => n.Id, n => n.Id);
        string Find(string x)
        {
            while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
            return x;
        }

        foreach (var n in nodes)
        {
            foreach (var e in n.OutboundEdges.Concat(n.InboundEdges))
            {
                if (nodeIds.Contains(e.SourceId) && nodeIds.Contains(e.TargetId))
                {
                    var rx = Find(e.SourceId);
                    var ry = Find(e.TargetId);
                    if (rx != ry) parent[rx] = ry;
                }
            }
        }

        var components = nodes.GroupBy(n => Find(n.Id))
            .Select(g => g.Select(n => n.Name).ToList()).ToList();

        double cohesion = (internalEdges + externalEdges) > 0
            ? (double)internalEdges / (internalEdges + externalEdges) : 1.0;

        return new CohesionResult(ns, internalEdges, externalEdges,
            Math.Round(cohesion, 3), components);
    }

    /// <summary>
    /// Simulate moving nodes to a different namespace and report
    /// which existing edges would become cross-boundary violations.
    /// </summary>
    public SimulationResult SimulateMove(
        IEnumerable<string> nodeIds, string targetNamespace)
    {
        var movingIds = nodeIds.ToHashSet();
        var brokenEdges = new List<GraphEdge>();
        var newCrossings = new List<GraphEdge>();

        foreach (var nodeId in movingIds)
        {
            var node = _graph.GetNodeById(nodeId);
            if (node == null) continue;

            // Check inbound: consumers that expect the old namespace
            foreach (var edge in node.InboundEdges)
            {
                if (edge.Source != null && !movingIds.Contains(edge.Source.Id)
                    && edge.Source.Namespace != targetNamespace)
                    brokenEdges.Add(edge);
            }
            // Check outbound: existing internal edges that would become external
            foreach (var edge in node.OutboundEdges)
            {
                if (edge.Target != null && !movingIds.Contains(edge.Target.Id)
                    && edge.Target.Namespace != targetNamespace
                    && edge.Target.Namespace == node.Namespace)
                    newCrossings.Add(edge);
            }
        }
        return new SimulationResult(brokenEdges.Count, newCrossings.Count,
            brokenEdges, newCrossings);
    }

    /// <summary>
    /// Cross-reference GQL resolver atoms with their service call edges.
    /// Identifies resolvers with no backing service and services with no API exposure.
    /// </summary>
    public ApiSurfaceAnalysis AnalyzeApiSurface()
    {
        var resolvers = _graph.Nodes
            .Where(n => n.Name.StartsWith("GQL.")).ToList();

        var resolversWithCalls = resolvers
            .Where(r => r.OutboundEdges.Any(e => e.Type == EdgeType.Calls))
            .Select(r => r.Name).ToHashSet();

        var unbacked = resolvers
            .Where(r => !resolversWithCalls.Contains(r.Name))
            .Select(r => r.Name).ToList();

        // Domain services: classes ending in Manager or Service
        var services = _graph.Nodes
            .Where(n => n.Type == NodeType.Class &&
                (n.Name.EndsWith("Manager") || n.Name.EndsWith("Service")))
            .ToList();

        var calledServices = resolvers
            .SelectMany(r => r.OutboundEdges)
            .Where(e => e.Type == EdgeType.Calls && e.Target != null)
            .Select(e => e.Target!.Name).ToHashSet();

        var unexposed = services
            .Where(s => !calledServices.Contains(s.Name))
            .Select(s => s.Name).ToList();

        return new ApiSurfaceAnalysis(
            resolvers.Count, unbacked, services.Count, unexposed);
    }
}

