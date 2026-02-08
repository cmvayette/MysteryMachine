using System.Text.RegularExpressions;

namespace DiagnosticStructuralLens.Graph;

/// <summary>
/// Engine for evaluating architectural rules against the knowledge graph.
/// </summary>
public class RuleEngine
{
    private readonly KnowledgeGraph _graph;

    public RuleEngine(KnowledgeGraph graph)
    {
        _graph = graph;
    }

    /// <summary>
    /// Evaluates a single rule against the graph and returns all violations.
    /// </summary>
    public IReadOnlyList<RuleViolation> EvaluateRule(ArchitectureRule rule)
    {
        var violations = new List<RuleViolation>();

        // 1. Identify Candidate Sources
        // Optimization: If Type is specified, use the index. Otherwise scan all nodes.
        IEnumerable<GraphNode> sourceCandidates = rule.Source.Type.HasValue
            ? _graph.GetNodesByType(rule.Source.Type.Value)
            : _graph.Nodes;

        // Compile regexes for patterns once per rule evaluation
        var sourceNameRegex = CreateGlobRegex(rule.Source.NamePattern);
        var sourceNamespaceRegex = CreateGlobRegex(rule.Source.NamespacePattern);
        
        var targetNameRegex = CreateGlobRegex(rule.Target.NamePattern);
        var targetNamespaceRegex = CreateGlobRegex(rule.Target.NamespacePattern);

        foreach (var sourceNode in sourceCandidates)
        {
            // Check Source constraints
            if (!MatchesQuery(sourceNode, rule.Source, sourceNameRegex, sourceNamespaceRegex))
                continue;

            // 2. Scan outbound edges for Forbidden Type
            foreach (var edge in sourceNode.OutboundEdges)
            {
                if (edge.Type != rule.ForbiddenEdge)
                    continue;

                var targetNode = edge.Target;
                if (targetNode == null) continue;

                // 3. Check Target constraints
                if (MatchesQuery(targetNode, rule.Target, targetNameRegex, targetNamespaceRegex))
                {
                    // Violation found!
                    violations.Add(new RuleViolation(rule, sourceNode, targetNode, edge));
                }
            }
        }

        return violations;
    }

    private bool MatchesQuery(GraphNode node, NodeQuery query, Regex? nameRegex, Regex? nsRegex)
    {
        if (query.Type.HasValue && node.Type != query.Type.Value)
            return false;

        if (nameRegex != null && !nameRegex.IsMatch(node.Name))
            return false;

        if (nsRegex != null)
        {
            // Namespace can be null on the node, but pattern expects string.
            // If pattern exists but namespace is null, does it match? No.
            var nsNode = node.Properties.GetValueOrDefault("Namespace") as string ?? "";
            if (!nsRegex.IsMatch(nsNode))
                return false;
        }
        
        if (query.IsPublic.HasValue)
        {
            if (node.Properties.TryGetValue("IsPublic", out var val) && val is bool isPublic)
            {
                if (isPublic != query.IsPublic.Value) return false;
            }
            // If property missing, what default? Assume mismatch for safety or match?
            // Let's assume mismatch if strict.
            else return false;
        }

        return true;
    }

    private Regex? CreateGlobRegex(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return null;
        
        // Convert glob to regex:
        // . -> \.
        // * -> .*
        // ? -> .
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
            
        return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}
