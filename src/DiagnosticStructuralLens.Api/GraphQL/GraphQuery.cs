using HotChocolate;
using HotChocolate.Types;
using DiagnosticStructuralLens.Graph;
using DiagnosticStructuralLens.Core;
using DiagnosticStructuralLens.Federation;
using DiagnosticStructuralLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

    /// <summary>
    /// Degree centrality for every node — identifies architectural hubs.
    /// </summary>
    public IEnumerable<NodeMetric> GetCentrality([Service] DiagnosticStructuralLensDataService data)
    {
         if (data.Graph == null) return Array.Empty<NodeMetric>();
         var engine = new GraphQueryEngine(data.Graph);
         return engine.CalculateCentrality();
    }

    /// <summary>
    /// Nodes with zero inbound edges — roots or potential dead code.
    /// </summary>
    public IEnumerable<OrphanNode> GetOrphans([Service] DiagnosticStructuralLensDataService data)
    {
         if (data.Graph == null) return Array.Empty<OrphanNode>();
         var engine = new GraphQueryEngine(data.Graph);
         return engine.FindOrphans()
             .Select(n => new OrphanNode
             {
                 Id = n.Id,
                 Name = n.Name,
                 Type = n.Type.ToString(),
                 Namespace = n.Namespace,
                 OutboundCount = n.OutboundEdges.Count
             });
    }

    /// <summary>
    /// Package-level architectural health metrics (Robert C. Martin's Instability/Abstractness).
    /// </summary>
    public IEnumerable<NamespaceMetrics> GetPackageMetrics(
        [Service] DiagnosticStructuralLensDataService data)
    {
         if (data.Graph == null) return Array.Empty<NamespaceMetrics>();
         var engine = new GraphQueryEngine(data.Graph);
         return engine.CalculatePackageMetrics();
    }

    /// <summary>
    /// Shortest dependency path between two nodes.
    /// </summary>
    public PathResultView? FindPath(
        [Service] DiagnosticStructuralLensDataService data,
        string fromId, string toId)
    {
         if (data.Graph == null) return null;
         var engine = new GraphQueryEngine(data.Graph);
         var result = engine.FindShortestPath(fromId, toId);
         if (result == null) return null;
         return new PathResultView
         {
             Length = result.Length,
             Steps = result.Steps.Select(s => new PathStepView
             {
                 FromId = s.From.Id,
                 FromName = s.From.Name,
                 ToId = s.To.Id,
                 ToName = s.To.Name,
                 EdgeType = s.Via.Type.ToString()
             }).ToList()
         };
    }

    /// <summary>
    /// Internal cohesion analysis for a namespace — identifies split candidates.
    /// </summary>
    public CohesionView? GetCohesion(
        [Service] DiagnosticStructuralLensDataService data,
        string ns)
    {
         if (data.Graph == null) return null;
         var engine = new GraphQueryEngine(data.Graph);
         var result = engine.AnalyzeCohesion(ns);
         return new CohesionView
         {
             Namespace = result.Namespace,
             InternalEdges = result.InternalEdges,
             ExternalEdges = result.ExternalEdges,
             CohesionRatio = result.CohesionRatio,
             ComponentCount = result.ConnectedComponents.Count,
             Components = result.ConnectedComponents
         };
    }

    /// <summary>
    /// Comparative namespace dashboard — all namespaces side-by-side.
    /// </summary>
    public IEnumerable<NamespaceSummary> GetNamespaceSummaries(
        [Service] DiagnosticStructuralLensDataService data,
        string? pattern = null)
    {
         if (data.Graph == null) return Array.Empty<NamespaceSummary>();
         var engine = new GraphQueryEngine(data.Graph);
         var metrics = engine.CalculatePackageMetrics().AsEnumerable();
         if (pattern != null)
             metrics = metrics.Where(m => m.Namespace.Contains(pattern));
         return metrics.Select(m => new NamespaceSummary
         {
             Namespace = m.Namespace,
             AtomCount = data.Graph.GetNodesByNamespace(m.Namespace).Count,
             Instability = m.Instability,
             Abstractness = m.Abstractness,
             Distance = m.DistanceFromMainSequence,
             Zone = m.Zone,
             Ca = m.AfferentCoupling,
             Ce = m.EfferentCoupling
         });
    }

    /// <summary>
    /// Simulate moving nodes to a different namespace — what would break?
    /// </summary>
    public SimulationResultView? SimulateMove(
        [Service] DiagnosticStructuralLensDataService data,
        List<string> nodeIds, string targetNamespace)
    {
         if (data.Graph == null) return null;
         var engine = new GraphQueryEngine(data.Graph);
         var result = engine.SimulateMove(nodeIds, targetNamespace);
         return new SimulationResultView
         {
             BrokenEdgeCount = result.BrokenEdgeCount,
             NewCrossingCount = result.NewCrossingCount,
             BrokenEdges = result.BrokenEdges.Select(e => new SimulatedBreak
             {
                 SourceName = e.Source?.Name ?? e.SourceId,
                 TargetName = e.Target?.Name ?? e.TargetId,
                 EdgeType = e.Type.ToString()
             }).ToList()
         };
    }

    /// <summary>
    /// API surface gap analysis — resolvers without services, services without API exposure.
    /// </summary>
    public ApiSurfaceView GetApiSurface([Service] DiagnosticStructuralLensDataService data)
    {
         if (data.Graph == null) return new ApiSurfaceView();
         var engine = new GraphQueryEngine(data.Graph);
         var result = engine.AnalyzeApiSurface();
         return new ApiSurfaceView
         {
             TotalResolvers = result.TotalResolvers,
             TotalDomainServices = result.TotalDomainServices,
             UnbackedResolvers = result.UnbackedResolvers.ToList(),
             UnexposedServices = result.UnexposedServices.ToList()
         };
    }

    /// <summary>
    /// Compare the two most recent snapshots for a repository to detect regression.
    /// </summary>
    public async Task<SnapshotComparisonView?> GetSnapshotComparison(
        [Service] DslDbContext db,
        string? repoId = null)
    {
         // Get the two most recent snapshots (optionally filtered by repo)
         var query = db.Snapshots.AsQueryable();
         if (!string.IsNullOrEmpty(repoId))
             query = query.Where(s => s.Repository == repoId);

         var snapshots = await query
             .OrderByDescending(s => s.CreatedAt)
             .Take(2)
             .ToListAsync();

         if (snapshots.Count < 2) return null;

         var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
         jsonOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());

         var currentSnap = JsonSerializer.Deserialize<Snapshot>(snapshots[0].Data, jsonOptions);
         var baselineSnap = JsonSerializer.Deserialize<Snapshot>(snapshots[1].Data, jsonOptions);
         if (currentSnap == null || baselineSnap == null) return null;

         // Build temporary graphs
         var builder = new GraphBuilder();
         var fedEngine = new FederationEngine();
         
         var currentGraph = builder.Build(fedEngine.Merge([currentSnap]));
         var baselineGraph = builder.Build(fedEngine.Merge([baselineSnap]));

         var currentEngine = new GraphQueryEngine(currentGraph);
         var baselineEngine = new GraphQueryEngine(baselineGraph);

         var currentMetrics = currentEngine.CalculatePackageMetrics().ToDictionary(m => m.Namespace);
         var baselineMetrics = baselineEngine.CalculatePackageMetrics().ToDictionary(m => m.Namespace);

         // Compute deltas
         var allNamespaces = currentMetrics.Keys.Union(baselineMetrics.Keys).ToList();
         var deltas = new List<NamespaceDelta>();

         foreach (var ns in allNamespaces)
         {
             var hasCurrent = currentMetrics.TryGetValue(ns, out var curr);
             var hasBaseline = baselineMetrics.TryGetValue(ns, out var baseline);

             string status;
             if (hasCurrent && !hasBaseline) status = "NEW";
             else if (!hasCurrent && hasBaseline) status = "REMOVED";
             else if (hasCurrent && hasBaseline)
             {
                 // Determine regression vs improvement based on zone + distance changes
                 var zoneRegressed = (baseline!.Zone == "Ideal" && curr!.Zone != "Ideal") ||
                                     (baseline.Zone != "Pain" && curr!.Zone == "Pain");
                 var zoneImproved = (baseline.Zone == "Pain" && curr!.Zone != "Pain") ||
                                    (baseline.Zone != "Ideal" && curr!.Zone == "Ideal");
                 var distanceWorse = curr!.DistanceFromMainSequence > baseline.DistanceFromMainSequence + 0.05;

                 if (zoneRegressed) status = "REGRESSED";
                 else if (zoneImproved) status = "IMPROVED";
                 else if (distanceWorse) status = "REGRESSED";
                 else status = "STABLE";
             }
             else status = "STABLE";

             deltas.Add(new NamespaceDelta
             {
                 Namespace = ns,
                 Status = status,
                 OldZone = hasBaseline ? baseline!.Zone : null,
                 NewZone = hasCurrent ? curr!.Zone : null,
                 DistanceDelta = (hasCurrent ? curr!.DistanceFromMainSequence : 0) -
                                 (hasBaseline ? baseline!.DistanceFromMainSequence : 0),
                 TypesDelta = (hasCurrent ? curr!.TotalTypes : 0) -
                              (hasBaseline ? baseline!.TotalTypes : 0),
                 CouplingDelta = (hasCurrent ? curr!.AfferentCoupling : 0) -
                                 (hasBaseline ? baseline!.AfferentCoupling : 0)
             });
         }

         // Aggregate zone counts
         int currentPain = currentMetrics.Values.Count(m => m.Zone == "Pain");
         int baselinePain = baselineMetrics.Values.Count(m => m.Zone == "Pain");
         int currentIdeal = currentMetrics.Values.Count(m => m.Zone == "Ideal");
         int baselineIdeal = baselineMetrics.Values.Count(m => m.Zone == "Ideal");
         int currentUseless = currentMetrics.Values.Count(m => m.Zone == "Uselessness");
         int baselineUseless = baselineMetrics.Values.Count(m => m.Zone == "Uselessness");

         return new SnapshotComparisonView
         {
             BaselineId = snapshots[1].Id,
             CurrentId = snapshots[0].Id,
             BaselineDate = snapshots[1].CreatedAt.ToString("yyyy-MM-dd HH:mm"),
             CurrentDate = snapshots[0].CreatedAt.ToString("yyyy-MM-dd HH:mm"),
             Repository = snapshots[0].Repository,
             BaselineAtomCount = baselineSnap.CodeAtoms.Count,
             CurrentAtomCount = currentSnap.CodeAtoms.Count,
             BaselineNamespaceCount = baselineMetrics.Count,
             CurrentNamespaceCount = currentMetrics.Count,
             PainZoneDelta = currentPain - baselinePain,
             IdealZoneDelta = currentIdeal - baselineIdeal,
             UselessZoneDelta = currentUseless - baselineUseless,
             AvgDistanceDelta = (currentMetrics.Values.Any() ? currentMetrics.Values.Average(m => m.DistanceFromMainSequence) : 0) -
                                (baselineMetrics.Values.Any() ? baselineMetrics.Values.Average(m => m.DistanceFromMainSequence) : 0),
             TotalCouplingDelta = currentMetrics.Values.Sum(m => m.AfferentCoupling) -
                                  baselineMetrics.Values.Sum(m => m.AfferentCoupling),
             NamespaceDeltas = deltas.OrderByDescending(d => d.Status == "REGRESSED")
                                     .ThenByDescending(d => d.Status == "NEW")
                                     .ThenByDescending(d => Math.Abs(d.DistanceDelta))
                                     .ToList()
         };
    }
}
