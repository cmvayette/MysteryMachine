using HotChocolate;
using HotChocolate.Types;
using DiagnosticStructuralLens.Graph;

namespace DiagnosticStructuralLens.Api.GraphQL;

[ExtendObjectType(typeof(Query))]
public class GraphQuery
{
    /// <summary>
    /// Expose the raw graph for client-side visualization if needed.
    /// </summary>
    public KnowledgeGraph? GetGraph([Service] DiagnosticStructuralLensDataService data) => data.Graph;

    /// <summary>
    /// Server-side Graph Traversal (e.g. for Impact Analysis).
    /// </summary>
    public TraversalResult? Traverse(
        [Service] DiagnosticStructuralLensDataService data,
        string nodeId,
        TraversalDirection direction = TraversalDirection.Outbound,
        int depth = 1)
    {
         if (data.Graph == null) return null;
         var engine = new GraphQueryEngine(data.Graph);
         return engine.Traverse(nodeId, direction, depth);
    }

    /// <summary>
    /// Server-side Cycle Detection.
    /// </summary>
    public IEnumerable<GraphCycle> GetCycles([Service] DiagnosticStructuralLensDataService data)
    {
         if (data.Graph == null) return Array.Empty<GraphCycle>();
         var engine = new GraphQueryEngine(data.Graph);
         return engine.FindCycles();
    }

    /// <summary>
    /// Server-side Rule Evaluation.
    /// </summary>
    public IEnumerable<RuleViolation> EvaluateRules([Service] DiagnosticStructuralLensDataService data)
    {
         if (data.Graph == null) return Array.Empty<RuleViolation>();
         var engine = new RuleEngine(data.Graph);
         
         // In a real application, rules would vary by context/configuration
         var violations = new List<RuleViolation>();
         foreach(var rule in BuiltInRules.All)
         {
             violations.AddRange(engine.EvaluateRule(rule));
         }
         return violations;
    }

    /// <summary>
    /// Server-side topology detection for layout optimization.
    /// </summary>
    public LayoutHint? GetLayoutHint(
        [Service] DiagnosticStructuralLensDataService data,
        string? scopeId = null)
    {
         if (data.Graph == null) return null;
         var engine = new GraphQueryEngine(data.Graph);
         return engine.DetectTopology(scopeId);
    }
}
