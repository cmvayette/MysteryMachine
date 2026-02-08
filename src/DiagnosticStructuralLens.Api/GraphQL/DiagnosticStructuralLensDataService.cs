using DiagnosticStructuralLens.Core;
using DiagnosticStructuralLens.Federation;
using DiagnosticStructuralLens.Linker;
using DiagnosticStructuralLens.Graph;
using DiagnosticStructuralLens.Risk;
using LinkerBlastRadius = DiagnosticStructuralLens.Linker.BlastRadiusResult;

namespace DiagnosticStructuralLens.Api.GraphQL;

/// <summary>
/// Service that provides data to GraphQL resolvers.
/// Currently uses in-memory data; can be extended to use SQL Server storage.
/// </summary>
public class DiagnosticStructuralLensDataService
{
    private FederatedSnapshot? _currentFederation;
    private readonly SemanticLinker _linker = new();
    private readonly RiskScorer _riskScorer;

    /// <summary>
    /// The current knowledge graph (built from Federation).
    /// </summary>
    public KnowledgeGraph? Graph { get; private set; }

    public DiagnosticStructuralLensDataService()
    {
        _riskScorer = new RiskScorer();
    }

    /// <summary>
    /// Load a federated snapshot (from file or database).
    /// </summary>
    public void LoadFederation(FederatedSnapshot federation)
    {
        _currentFederation = federation;
        
        // Build Knowledge Graph
        var builder = new GraphBuilder();
        Graph = builder.Build(federation);
    }

    /// <summary>
    /// Get the current federation view.
    /// </summary>
    public FederatedSnapshot? GetFederation() => _currentFederation;

    /// <summary>
    /// Get repository by ID.
    /// </summary>
    public RepoMetadata? GetRepository(string repoId)
    {
        return _currentFederation?.RepoMetadata.FirstOrDefault(r => r.RepoId == repoId);
    }

    /// <summary>
    /// Get atoms for a specific namespace path.
    /// </summary>
    public IEnumerable<FederatedAtom<CodeAtom>> GetAtomsInNamespace(string repoId, string namespacePath)
    {
        if (_currentFederation == null) return [];
        
        return _currentFederation.CodeAtoms
            .Where(a => a.SourceRepo == repoId && 
                       (a.Atom.Namespace?.StartsWith(namespacePath) ?? false));
    }

    /// <summary>
    /// Get atom details by ID.
    /// </summary>
    public (object? atom, string? sourceRepo) GetAtomById(string atomId)
    {
        if (_currentFederation == null) return (null, null);

        var codeAtom = _currentFederation.CodeAtoms.FirstOrDefault(a => a.Atom.Id == atomId);
        if (codeAtom != null) return (codeAtom.Atom, codeAtom.SourceRepo);

        var sqlAtom = _currentFederation.SqlAtoms.FirstOrDefault(a => a.Atom.Id == atomId);
        if (sqlAtom != null) return (sqlAtom.Atom, sqlAtom.SourceRepo);

        return (null, null);
    }

    /// <summary>
    /// Calculate blast radius for an atom.
    /// </summary>
    public LinkerBlastRadius CalculateBlastRadius(string atomId, int maxDepth = 5)
    {
        if (_currentFederation == null) 
            return new LinkerBlastRadius { RootAtomId = atomId };

        // Collect all links from federated snapshot
        var allLinks = _currentFederation.Links.Select(l => l.Link).ToList();
        
        return _linker.GetBlastRadius(atomId, allLinks, maxDepth);
    }

    /// <summary>
    /// Search atoms by name pattern.
    /// </summary>
    public IEnumerable<(object atom, string sourceRepo)> SearchAtoms(string query, IEnumerable<AtomType>? types = null)
    {
        if (_currentFederation == null) yield break;

        var queryLower = query.ToLowerInvariant();

        foreach (var codeAtom in _currentFederation.CodeAtoms)
        {
            if (types != null && !types.Contains(codeAtom.Atom.Type)) continue;
            if (codeAtom.Atom.Name.ToLowerInvariant().Contains(queryLower))
            {
                yield return (codeAtom.Atom, codeAtom.SourceRepo);
            }
        }

        // Also search SQL atoms if no type filter or if matching types
        foreach (var sqlAtom in _currentFederation.SqlAtoms)
        {
            if (sqlAtom.Atom.Name.ToLowerInvariant().Contains(queryLower))
            {
                yield return (sqlAtom.Atom, sqlAtom.SourceRepo);
            }
        }
    }

    /// <summary>
    /// Get all namespaces for a repository.
    /// </summary>
    public IEnumerable<string> GetNamespacesForRepo(string repoId)
    {
        if (_currentFederation == null) return [];

        return _currentFederation.CodeAtoms
            .Where(a => a.SourceRepo == repoId && a.Atom.Namespace != null)
            .Select(a => a.Atom.Namespace!)
            .Distinct()
            .OrderBy(ns => ns);
    }

    /// <summary>
    /// Get links for a specific atom.
    /// </summary>
    public (List<FederatedLink> inbound, List<FederatedLink> outbound) GetLinksForAtom(string atomId)
    {
        if (_currentFederation == null) return ([], []);

        var inbound = _currentFederation.Links
            .Where(l => l.Link.TargetId == atomId)
            .ToList();

        var outbound = _currentFederation.Links
            .Where(l => l.Link.SourceId == atomId)
            .ToList();

        return (inbound, outbound);
    }

    /// <summary>
    /// Count consumers for an atom (how many things depend on it).
    /// </summary>
    public int GetConsumerCount(string atomId)
    {
        return _currentFederation?.Links.Count(l => l.Link.TargetId == atomId) ?? 0;
    }

    /// <summary>
    /// Get links where both source and target are in the given set of atom IDs.
    /// This is used for showing relationships between atoms in the same namespace.
    /// </summary>
    public IEnumerable<FederatedLink> GetLinksInNamespace(HashSet<string> atomIds)
    {
        if (_currentFederation == null) return [];

        return _currentFederation.Links
            .Where(l => atomIds.Contains(l.Link.SourceId) && atomIds.Contains(l.Link.TargetId));
    }

    /// <summary>
    /// Check if a link violates architectural rules.
    /// </summary>
    public bool CheckViolation(object source, object target, LinkType type)
    {
        // Most rules apply to CodeAtoms (C# code)
        if (source is CodeAtom s && target is CodeAtom t)
        {
            // Rule 1: Layering - Domain cannot depend on Infrastructure or Web
            if (IsDomain(s) && (IsInfrastructure(t) || IsWeb(t)))
            {
                return true;
            }

            // Rule 2: Explicit Blocklist - Controllers cannot bypass Service layer to access Repositories
            if (s.Name.EndsWith("Controller") && t.Name.EndsWith("Repository"))
            {
                return true;
            }

            // Rule 3: Vertical Slicing
            if (GetRootModule(s) != GetRootModule(t) && type == LinkType.Calls) // Use Calls for stricter check
            {
                 // return false; // Disabled for V1
            }
        }

        return false;
    }

    private bool IsDomain(CodeAtom atom) => atom.Namespace?.Contains(".Domain") ?? false;
    private bool IsInfrastructure(CodeAtom atom) => atom.Namespace?.Contains(".Infrastructure") ?? false;
    private bool IsWeb(CodeAtom atom) => atom.Namespace?.Contains(".Web") ?? atom.Namespace?.Contains(".Api") ?? false;

    private string GetRootModule(CodeAtom atom)
    {
        // optimization: assumption that namespace structure is Company.Module.Layer
        var parts = atom.Namespace?.Split('.');
        if (parts?.Length >= 2) return parts[1];
        return "Common";
    }
}
