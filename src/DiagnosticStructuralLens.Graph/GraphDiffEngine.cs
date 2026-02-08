namespace DiagnosticStructuralLens.Graph;

public class GraphDiffEngine
{
    // Engines are instantiated per-comparison to bind to specific graph instances.
    
    // Compare two full graphs
    public StructuralDiff Compare(KnowledgeGraph baseline, KnowledgeGraph current, IReadOnlyList<ArchitectureRule> rules)
    {
        // 1. Topology Diff
        var topology = DiffTopology(baseline, current);

        // 2. Semantic Diff (Regressions)
        var newViolations = DiffViolations(baseline, current, rules);
        var newCycles = DiffCycles(baseline, current);

        return new StructuralDiff(topology, newViolations, newCycles);
    }

    private GraphDiff DiffTopology(KnowledgeGraph baseline, KnowledgeGraph current)
    {
        // Nodes
        var currentIds = current.Nodes.Select(n => n.Id).ToHashSet();
        var baselineIds = baseline.Nodes.Select(n => n.Id).ToHashSet();

        var addedNodes = current.Nodes.Where(n => !baselineIds.Contains(n.Id)).ToList();
        var removedNodes = baseline.Nodes.Where(n => !currentIds.Contains(n.Id)).ToList();

        // Edges
        var currentEdgeIds = current.Edges.Select(e => e.Id).ToHashSet();
        var baselineEdgeIds = baseline.Edges.Select(e => e.Id).ToHashSet();
        
        var addedEdges = current.Edges.Where(e => !baselineEdgeIds.Contains(e.Id)).ToList();
        var removedEdges = baseline.Edges.Where(e => !currentEdgeIds.Contains(e.Id)).ToList();

        return new GraphDiff(addedNodes, removedNodes, addedEdges, removedEdges);
    }

    private IReadOnlyList<RuleViolation> DiffViolations(
        KnowledgeGraph baseline, 
        KnowledgeGraph current, 
        IReadOnlyList<ArchitectureRule> rules)
    {
        // We need engines for both
        var baselineEngine = new RuleEngine(baseline);
        var currentEngine = new RuleEngine(current);

        var baselineViolations = new HashSet<string>();
        foreach (var rule in rules)
        {
            var violations = baselineEngine.EvaluateRule(rule);
            foreach (var v in violations)
            {
                // Unique signature for violation: RuleId + SourceId + TargetId + EdgeId
                baselineViolations.Add(GetViolationSignature(v));
            }
        }

        var newViolations = new List<RuleViolation>();
        foreach (var rule in rules)
        {
            var violations = currentEngine.EvaluateRule(rule);
            foreach (var v in violations)
            {
                if (!baselineViolations.Contains(GetViolationSignature(v)))
                {
                    newViolations.Add(v);
                }
            }
        }

        return newViolations;
    }

    private IReadOnlyList<GraphCycle> DiffCycles(KnowledgeGraph baseline, KnowledgeGraph current)
    {
        var baselineEngine = new GraphQueryEngine(baseline);
        var currentEngine = new GraphQueryEngine(current);
        
        var baselineCycles = baselineEngine.FindCycles()
            .Select(GetCycleSignature)
            .ToHashSet();
            
        var currentCycles = currentEngine.FindCycles();
        var newCycles = new List<GraphCycle>();

        foreach (var cycle in currentCycles)
        {
            if (!baselineCycles.Contains(GetCycleSignature(cycle)))
            {
                newCycles.Add(cycle);
            }
        }

        return newCycles;
    }

    private string GetViolationSignature(RuleViolation v)
    {
        return $"{v.Rule.Id}|{v.Source.Id}|{v.Target.Id}|{v.Edge.Id}";
    }

    private string GetCycleSignature(GraphCycle c)
    {
        // Cycle signature needs to be canonical (rotation invariant)
        // A->B->C->A is same as B->C->A->B
        // Sort IDs to make set signature? Or use canonical serialization.
        // Simple approach: Sort IDs of nodes involved.
        var ids = c.Nodes.Select(n => n.Id).OrderBy(id => id);
        return string.Join("|", ids);
    }
}
