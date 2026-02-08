using System.Text.RegularExpressions;

namespace DiagnosticStructuralLens.Graph;

/// <summary>
/// Detects the dominant topology pattern of a (sub)graph for layout optimization.
/// Port of the frontend detectTopology() to leverage server-side KnowledgeGraph indexes.
/// </summary>
public partial class PatternDetector
{
    // ── Layer-name patterns (mirrors frontend inferArchitecturalLayer) ────────

    private static readonly (string Pattern, string Layer)[] LayerPatterns =
    [
        // Presentation
        ("controller", "presentation"),
        ("api", "presentation"),
        ("endpoint", "presentation"),
        ("view", "presentation"),
        ("page", "presentation"),
        ("component", "presentation"),
        
        // Application
        ("service", "application"),
        ("handler", "application"),
        ("usecase", "application"),
        ("manager", "application"),
        ("orchestrat", "application"),
        ("mediator", "application"),
        
        // Domain
        ("domain", "domain"),
        ("entity", "domain"),
        ("aggregate", "domain"),
        ("valueobject", "domain"),
        ("model", "domain"),
        
        // Infrastructure
        ("repository", "infrastructure"),
        ("context", "infrastructure"),
        ("adapter", "infrastructure"),
        ("gateway", "infrastructure"),
        ("client", "infrastructure"),
        ("persistence", "infrastructure"),
        ("migration", "infrastructure"),
        ("dbcontext", "infrastructure"),
        
        // External
        ("test", "external"),
        ("spec", "external"),
        ("mock", "external"),
        ("fixture", "external"),
    ];

    /// <summary>
    /// Analyze the graph (or a namespace-scoped subgraph) and return a LayoutHint.
    /// </summary>
    public LayoutHint Detect(KnowledgeGraph graph, string? scopeNamespace = null)
    {
        // Scope the analysis to a namespace if provided
        ICollection<GraphNode> nodes;
        ICollection<GraphEdge> edges;

        if (!string.IsNullOrEmpty(scopeNamespace))
        {
            var scopedNodes = graph.GetNodesByNamespace(scopeNamespace);
            var scopedNodeIds = new HashSet<string>(scopedNodes.Select(n => n.Id));
            nodes = scopedNodes.ToList();
            edges = graph.Edges
                .Where(e => scopedNodeIds.Contains(e.SourceId) || scopedNodeIds.Contains(e.TargetId))
                .ToList();
        }
        else
        {
            nodes = graph.Nodes.ToList();
            edges = graph.Edges.ToList();
        }

        if (nodes.Count == 0)
        {
            return new LayoutHint("disconnected", 1.0, null, null, []);
        }

        // Run detectors in priority order (same as frontend)
        var candidates = new List<TopologyPattern>
        {
            DetectDisconnected(nodes, edges),
            DetectHubSpoke(nodes, edges),
            DetectPipeline(nodes, edges),
            DetectLayered(nodes),
        };

        // Pick highest confidence; filter out zeros
        var best = candidates
            .Where(c => c.Confidence > 0)
            .OrderByDescending(c => c.Confidence)
            .FirstOrDefault();

        if (best == null || best.Confidence < 0.3)
        {
            // Mesh fallback
            best = new TopologyPattern("mesh", 0.3);
        }

        // Build layer assignments regardless of pattern
        var layerAssignments = InferLayers(nodes);

        return new LayoutHint(
            best.Name,
            best.Confidence,
            best.HubNodeId,
            best.PipelineOrder,
            layerAssignments);
    }

    // ── Individual detectors ─────────────────────────────────────────────────

    internal TopologyPattern DetectDisconnected(
        ICollection<GraphNode> nodes, ICollection<GraphEdge> edges)
    {
        if (edges.Count == 0)
            return new TopologyPattern("disconnected", 1.0);

        double ratio = (double)edges.Count / nodes.Count;
        if (ratio < 0.3)
            return new TopologyPattern("disconnected", 1.0 - ratio);

        return new TopologyPattern("disconnected", 0);
    }

    internal TopologyPattern DetectHubSpoke(
        ICollection<GraphNode> nodes, ICollection<GraphEdge> edges)
    {
        if (edges.Count == 0)
            return new TopologyPattern("hub-spoke", 0);

        // Count degree per node
        var degree = new Dictionary<string, int>();
        foreach (var edge in edges)
        {
            degree[edge.SourceId] = degree.GetValueOrDefault(edge.SourceId) + 1;
            degree[edge.TargetId] = degree.GetValueOrDefault(edge.TargetId) + 1;
        }

        var maxEntry = degree.MaxBy(kv => kv.Value);
        double maxDegreeRatio = (double)maxEntry.Value / edges.Count;

        if (maxDegreeRatio >= 0.4)
        {
            double confidence = Math.Min(maxDegreeRatio * 1.2, 1.0);
            return new TopologyPattern("hub-spoke", confidence, HubNodeId: maxEntry.Key);
        }

        return new TopologyPattern("hub-spoke", 0);
    }

    internal TopologyPattern DetectPipeline(
        ICollection<GraphNode> nodes, ICollection<GraphEdge> edges)
    {
        if (nodes.Count < 3 || edges.Count == 0)
            return new TopologyPattern("pipeline", 0);

        // Build adjacency list (outbound only)
        var adj = new Dictionary<string, List<string>>();
        var inDegree = new Dictionary<string, int>();
        
        foreach (var node in nodes)
        {
            adj[node.Id] = [];
            inDegree[node.Id] = 0;
        }
        
        foreach (var edge in edges)
        {
            if (adj.ContainsKey(edge.SourceId) && adj.ContainsKey(edge.TargetId))
            {
                adj[edge.SourceId].Add(edge.TargetId);
                inDegree[edge.TargetId] = inDegree.GetValueOrDefault(edge.TargetId) + 1;
            }
        }

        // Find source nodes (in-degree 0)
        var sources = inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key).ToList();
        if (sources.Count == 0)
            sources = [nodes.First().Id]; // Fallback: start from first node

        // BFS for longest path
        var longestPath = new List<string>();
        foreach (var source in sources)
        {
            var path = BfsLongestPath(source, adj, nodes.Count);
            if (path.Count > longestPath.Count)
                longestPath = path;
        }

        double coverage = (double)longestPath.Count / nodes.Count;
        double branchFactor = nodes.Count > 1 ? (double)edges.Count / (nodes.Count - 1) : 0;

        if (coverage >= 0.6 && branchFactor <= 1.5)
        {
            double confidence = coverage * 0.7 + (1.0 - Math.Min(branchFactor, 1.5) / 1.5) * 0.3;
            return new TopologyPattern("pipeline", confidence, PipelineOrder: longestPath);
        }

        return new TopologyPattern("pipeline", 0);
    }

    internal TopologyPattern DetectLayered(ICollection<GraphNode> nodes)
    {
        var assignments = InferLayers(nodes);
        int matched = assignments.Count(a => a.Layer != "unknown");
        double ratio = nodes.Count > 0 ? (double)matched / nodes.Count : 0;

        if (ratio >= 0.4)
        {
            return new TopologyPattern("layered", ratio);
        }

        return new TopologyPattern("layered", 0);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static List<string> BfsLongestPath(
        string startId, Dictionary<string, List<string>> adj, int maxNodes)
    {
        var visited = new HashSet<string> { startId };
        var queue = new Queue<(string Id, List<string> Path)>();
        queue.Enqueue((startId, [startId]));
        var longest = new List<string> { startId };

        while (queue.Count > 0)
        {
            var (current, path) = queue.Dequeue();
            if (path.Count > longest.Count)
                longest = path;

            if (visited.Count >= maxNodes) continue;
            if (!adj.TryGetValue(current, out var neighbors)) continue;
            foreach (var next in neighbors)
            {
                if (visited.Add(next))
                {
                    var nextPath = new List<string>(path) { next };
                    queue.Enqueue((next, nextPath));
                }
            }
        }

        return longest;
    }

    private static List<LayerAssignment> InferLayers(ICollection<GraphNode> nodes)
    {
        var assignments = new List<LayerAssignment>();
        foreach (var node in nodes)
        {
            string name = (node.Name ?? node.Id).ToLowerInvariant();
            string ns = (node.Namespace ?? "").ToLowerInvariant();
            string combined = $"{ns}.{name}";

            string layer = "unknown";
            foreach (var (pattern, layerName) in LayerPatterns)
            {
                if (combined.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    layer = layerName;
                    break;
                }
            }

            assignments.Add(new LayerAssignment(node.Id, layer));
        }
        return assignments;
    }
}
