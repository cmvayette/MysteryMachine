using HotChocolate;
using DiagnosticStructuralLens.Core;
using DiagnosticStructuralLens.Federation;

namespace DiagnosticStructuralLens.Api.GraphQL;

/// <summary>
/// Root Query type for the GraphQL API.
/// Implements C4 model navigation: Context → Container → Component → Code
/// </summary>
public class Query
{
    /// <summary>
    /// Phase 4: Time Travel - Get all available historical snapshots.
    /// </summary>
    public async Task<IEnumerable<SnapshotSummary>> GetSnapshots([Service] SnapshotService snapshotService)
    {
        var snapshots = await snapshotService.GetAllSnapshotsAsync();
        return snapshots.Select(s => new SnapshotSummary 
        { 
            Id = s.Id, 
            ScannedAt = s.ScannedAt, 
            Branch = s.Branch, 
            AtomCount = s.Metadata.TotalCodeAtoms 
        });
    }

    /// <summary>
    /// Phase 4: Time Travel - Get a specific snapshot graph.
    /// </summary>
    public async Task<SnapshotGraph?> GetSnapshot(string id, [Service] SnapshotService snapshotService)
    {
        var snapshot = await snapshotService.GetSnapshotAsync(id);
        if (snapshot == null) return null;

        // Map internal Snapshot model to GraphQL Graph view model
        // For now, returning a simplified graph structure
        return new SnapshotGraph
        {
            Id = snapshot.Id,
            Nodes = snapshot.CodeAtoms.Select(a => new AtomNode 
            { 
                 Id = a.Id, 
                 Name = a.Name, 
                 Type = a.Type.ToString(),
                 LinesOfCode = a.LinesOfCode,
                 IsPublic = a.IsPublic,
                 ChurnScore = new Random().Next(0, 100),
                 MaintenanceCost = new Random().Next(0, 100)
            }).ToList(),
            Links = snapshot.Links.Select(l => new AtomLinkView 
            { 
                 Source = l.SourceId, 
                 Target = l.TargetId, 
                 Type = l.Type.ToString(),
                 // Governance Check would happen here if we had the resolved atoms
                 IsViolation = false // Placeholder until we link atoms
            }).ToList()
        };
    }

    /// <summary>
    /// L1: Context - Get the federated view of all repositories.
    /// </summary>
    public FederationView? GetFederation([Service] DiagnosticStructuralLensDataService dataService)
    {
        var federation = dataService.GetFederation();
        if (federation == null) return null;

        return new FederationView
        {
            Id = federation.Id,
            FederatedAt = federation.FederatedAt,
            Repositories = federation.RepoMetadata.Select(r => new RepositoryNode
            {
                Id = r.RepoId,
                Name = r.RepoId,
                Branch = r.Branch,
                ScannedAt = r.ScannedAt,
                AtomCount = federation.CodeAtoms.Count(a => a.SourceRepo == r.RepoId) +
                           federation.SqlAtoms.Count(a => a.SourceRepo == r.RepoId),
                RiskScore = 0, // TODO: Calculate aggregate risk
                Namespaces = dataService.GetNamespacesForRepo(r.RepoId).ToList(),
                Owner = new Owner 
                { 
                    Name = "Team " + r.RepoId, 
                    Email = "team@company.com", 
                    TeamName = "Platform Engineering", 
                    AvatarUrl = "https://ui-avatars.com/api/?name=" + r.RepoId 
                },
                QualityMetrics = new QualityMetrics
                {
                    CoveragePercent = 85.5,
                    SonarRating = "A",
                    CyclomaticComplexity = 12.4
                },
                ChurnScore = new Random().Next(0, 100),
                MaintenanceCost = new Random().Next(0, 100)
            }).ToList(),
            CrossRepoLinks = federation.Links
                .Where(l => l.SourceRepo != l.TargetRepo)
                .Select(l => new CrossRepoLink
                {
                    SourceAtomId = l.Link.SourceId,
                    TargetAtomId = l.Link.TargetId,
                    SourceRepo = l.SourceRepo,
                    TargetRepo = l.TargetRepo,
                    LinkType = l.Link.Type.ToString(),
                    Confidence = l.Link.Confidence,
                    IsViolation = CheckLinkViolation(l.Link, dataService),
                    ViolationDetails = CheckLinkViolation(l.Link, dataService) ? new ViolationDetails 
                    { 
                        RuleId = "LAYER_VIOLATION", 
                        Severity = "Critical", 
                        Message = "Cross-repo dependency violation", 
                        RemediationSuggestion = "Decouple repositories" 
                    } : null
                }).ToList(),
            Stats = new FederationStatsView
            {
                TotalRepos = federation.Stats.TotalRepos,
                TotalCodeAtoms = federation.Stats.TotalCodeAtoms,
                TotalSqlAtoms = federation.Stats.TotalSqlAtoms,
                TotalLinks = federation.Stats.TotalLinks,
                CrossRepoLinkCount = federation.Stats.CrossRepoLinkCount
            }
        };
    }

    /// <summary>
    /// L2: Container - Get details for a specific repository.
    /// </summary>
    public RepositoryView? GetRepository(string id, [Service] DiagnosticStructuralLensDataService dataService)
    {
        var repoMeta = dataService.GetRepository(id);
        if (repoMeta == null) return null;

        var federation = dataService.GetFederation();
        if (federation == null) return null;

        // Get all atoms for this repo to build a lookup for namespace resolution
        var atomsInRepo = federation.CodeAtoms
            .Where(a => a.SourceRepo == id)
            .ToDictionary(a => a.Atom.Id, a => a.Atom.Namespace);

        var namespaces = dataService.GetNamespacesForRepo(id)
            .Select(ns => new NamespaceNode
            {
                Path = ns,
                AtomCount = federation.CodeAtoms.Count(a => a.SourceRepo == id && a.Atom.Namespace == ns),
                DtoCount = federation.CodeAtoms.Count(a => a.SourceRepo == id && a.Atom.Namespace == ns && a.Atom.Type == AtomType.Dto),
                InterfaceCount = federation.CodeAtoms.Count(a => a.SourceRepo == id && a.Atom.Namespace == ns && a.Atom.Type == AtomType.Interface)
            }).ToList();

        var sqlSchemas = federation.SqlAtoms
            .Where(a => a.SourceRepo == id && a.Atom.Type == SqlAtomType.Table)
            .GroupBy(a => a.Atom.ParentTable?.Split('.').FirstOrDefault() ?? "dbo")
            .Select(g => new SqlSchemaNode
            {
                Schema = g.Key,
                TableCount = g.Count()
            }).ToList();

        // Calculate Namespace Links
        // Filter links where both source and target are in this repo
        var internalLinks = federation.Links
            .Where(l => l.SourceRepo == id && l.TargetRepo == id)
            .ToList();

        var namespaceLinks = internalLinks
            .Select(l => new 
            { 
                SourceNs = atomsInRepo.GetValueOrDefault(l.Link.SourceId),
                TargetNs = atomsInRepo.GetValueOrDefault(l.Link.TargetId)
            })
            .Where(x => x.SourceNs != null && x.TargetNs != null && x.SourceNs != x.TargetNs) // inter-namespace only
            .GroupBy(x => (x.SourceNs, x.TargetNs))
            .Select(g => new NamespaceLink
            {
                SourceNamespace = g.Key.SourceNs!,
                TargetNamespace = g.Key.TargetNs!,
                LinkCount = g.Count()
            })
            .ToList();

        return new RepositoryView
        {
            Id = id,
            Name = id,
            Branch = repoMeta.Branch,
            Namespaces = namespaces,
            SqlSchemas = sqlSchemas,
            NamespaceLinks = namespaceLinks,
            InboundLinks = federation.Links
                .Where(l => l.TargetRepo == id && l.SourceRepo != id)
                .Select(l => MapCrossRepoLink(l, dataService)).ToList(),
            OutboundLinks = federation.Links
                .Where(l => l.SourceRepo == id && l.TargetRepo != id)
                .Select(l => MapCrossRepoLink(l, dataService)).ToList()
        };
    }

    /// <summary>
    /// L3: Component - Get atoms in a specific namespace.
    /// </summary>
    public NamespaceView? GetNamespace(string repoId, string path, [Service] DiagnosticStructuralLensDataService dataService)
    {
        var atoms = dataService.GetAtomsInNamespace(repoId, path);
        var atomIds = atoms.Select(a => a.Atom.Id).ToHashSet();
        
        var atomNodes = atoms.Select(a => new AtomNode
        {
            Id = a.Atom.Id,
            Name = a.Atom.Name,
            Type = a.Atom.Type.ToString(),
            RiskScore = 0, // TODO: Calculate from RiskScorer
            ConsumerCount = dataService.GetConsumerCount(a.Atom.Id),

            LinesOfCode = a.Atom.LinesOfCode,
            Language = a.Atom.Language,
            IsPublic = (a.Atom as CodeAtom)?.IsPublic ?? true,
            ChurnScore = new Random().Next(0, 100),
            MaintenanceCost = new Random().Next(0, 100)
        }).ToList();

        // Get links between atoms in this namespace
        var internalLinks = dataService.GetLinksInNamespace(atomIds);

        return new NamespaceView
        {
            Path = path,
            Atoms = atomNodes,
            InternalLinks = internalLinks.Select(l => new InternalLink
            {
                SourceAtomId = l.Link.SourceId,
                TargetAtomId = l.Link.TargetId,
                LinkType = l.Link.Type.ToString(),
                IsViolation = CheckLinkViolation(l.Link, dataService),
                ViolationDetails = CheckLinkViolation(l.Link, dataService) ? new ViolationDetails 
                { 
                    RuleId = "LAYER_VIOLATION", 
                    Severity = "Critical", 
                    Message = "Domain layer should not depend on Infrastructure", 
                    RemediationSuggestion = "Invert dependency using interfaces" 
                } : null
            }).ToList()
        };
    }

    /// <summary>
    /// L4: Code - Get detailed information for a specific atom.
    /// </summary>
    public AtomDetail? GetAtom(string id, [Service] DiagnosticStructuralLensDataService dataService)
    {
        var (atom, sourceRepo) = dataService.GetAtomById(id);
        if (atom == null) return null;

        var (inbound, outbound) = dataService.GetLinksForAtom(id);

        return atom switch
        {
            CodeAtom codeAtom => new AtomDetail
            {
                Id = codeAtom.Id,
                Name = codeAtom.Name,
                Type = codeAtom.Type.ToString(),
                Namespace = codeAtom.Namespace,
                FilePath = codeAtom.FilePath,
                Repository = sourceRepo!,
                LinesOfCode = codeAtom.LinesOfCode,
                Language = codeAtom.Language,
                IsPublic = codeAtom.IsPublic,
                Members = GetMembers(codeAtom.Id, outbound, dataService),
                InboundLinks = inbound.Select(l => new LinkInfo { AtomId = l.Link.SourceId, LinkType = l.Link.Type.ToString() }).ToList(),
                OutboundLinks = outbound.Select(l => new LinkInfo { AtomId = l.Link.TargetId, LinkType = l.Link.Type.ToString() }).ToList()
            },
            SqlAtom sqlAtom => new AtomDetail
            {
                Id = sqlAtom.Id,
                Name = sqlAtom.Name,
                Type = sqlAtom.Type.ToString(),
                ParentTable = sqlAtom.ParentTable,
                DataType = sqlAtom.DataType,
                Repository = sourceRepo!,
                IsPublic = true,
                InboundLinks = inbound.Select(l => new LinkInfo { AtomId = l.Link.SourceId, LinkType = l.Link.Type.ToString() }).ToList(),
                OutboundLinks = outbound.Select(l => new LinkInfo { AtomId = l.Link.TargetId, LinkType = l.Link.Type.ToString() }).ToList()
            },
            _ => null
        };
    }

    /// <summary>
    /// Calculate blast radius for an atom.
    /// </summary>
    public BlastRadiusResult GetBlastRadius([Service] DiagnosticStructuralLensDataService dataService, string atomId, int maxDepth = 5)
    {
        var blastRadius = dataService.CalculateBlastRadius(atomId, maxDepth);

        return new BlastRadiusResult
        {
            SourceAtomId = atomId,
            AffectedAtoms = blastRadius.AffectedAtoms.Select(a => new AffectedAtomInfo
            {
                AtomId = a.AtomId,
                Depth = a.Depth
            }).ToList(),
            TotalAffected = blastRadius.AffectedAtoms.Count,
            ByDepth = blastRadius.AffectedAtoms
                .GroupBy(a => a.Depth)
                .Select(g => new DepthGroup { Depth = g.Key, Count = g.Count() })
                .OrderBy(d => d.Depth)
                .ToList()
        };
    }

    /// <summary>
    /// Search for atoms across all repositories.
    /// </summary>
    public List<SearchResult> Search(string query, [Service] DiagnosticStructuralLensDataService dataService)
    {
        return dataService.SearchAtoms(query)
            .Select(r => new SearchResult
            {
                AtomId = r.atom switch { CodeAtom c => c.Id, SqlAtom s => s.Id, _ => "" },
                Name = r.atom switch { CodeAtom c => c.Name, SqlAtom s => s.Name, _ => "" },
                Type = r.atom switch { CodeAtom c => c.Type.ToString(), SqlAtom s => s.Type.ToString(), _ => "" },
                Repository = r.sourceRepo
            })
            .ToList();
    }

    private static CrossRepoLink MapCrossRepoLink(FederatedLink l, DiagnosticStructuralLensDataService dataService) => new()
    {
        SourceAtomId = l.Link.SourceId,
        TargetAtomId = l.Link.TargetId,
        SourceRepo = l.SourceRepo,
        TargetRepo = l.TargetRepo,
        LinkType = l.Link.Type.ToString(),
        Confidence = l.Link.Confidence,
        IsViolation = CheckLinkViolation(l.Link, dataService)
    };

    private static bool CheckLinkViolation(AtomLink link, DiagnosticStructuralLensDataService dataService)
    {
        var (source, _) = dataService.GetAtomById(link.SourceId);
        var (target, _) = dataService.GetAtomById(link.TargetId);

        if (source != null && target != null)
        {
            return dataService.CheckViolation(source, target, link.Type);
        }
        return false;
    }
    
    private static List<MemberInfo> GetMembers(string parentId, List<FederatedLink> outbound, DiagnosticStructuralLensDataService dataService)
    {
        // Find links of type 'Contains'
        var containedIds = outbound
            .Where(l => l.Link.Type == LinkType.Contains)
            .Select(l => l.Link.TargetId)
            .ToList();
            
        if (!containedIds.Any()) return [];

        // We need to fetch the actual atoms to get their details
        // This is a bit inefficient but works for now. 
        // Ideally DataService would support GetAtomsByIds
        var members = new List<MemberInfo>();
        
        foreach (var id in containedIds)
        {
            var (atom, _) = dataService.GetAtomById(id);
            if (atom is CodeAtom ca)
            {
                members.Add(new MemberInfo
                {
                    Id = ca.Id,
                    Name = ca.Name,
                    Type = ca.Type.ToString(),
                    Signature = ca.Signature ?? ca.Name,
                    IsPublic = ca.IsPublic
                });
            }
        }
        
        return members;
    }
}

#region View Models

public class FederationView
{
    public required string Id { get; set; }
    public DateTimeOffset FederatedAt { get; set; }
    public List<RepositoryNode> Repositories { get; set; } = [];
    public List<CrossRepoLink> CrossRepoLinks { get; set; } = [];
    public FederationStatsView Stats { get; set; } = new();
}

public class RepositoryNode
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Branch { get; set; }
    public DateTimeOffset ScannedAt { get; set; }
    public int AtomCount { get; set; }
    public double RiskScore { get; set; }
    public List<string> Namespaces { get; set; } = [];
    public Owner? Owner { get; set; }
    public QualityMetrics? QualityMetrics { get; set; }
    public double ChurnScore { get; set; }
    public double MaintenanceCost { get; set; }
}

public class Owner
{
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string TeamName { get; set; }
    public required string AvatarUrl { get; set; }
}

public class QualityMetrics
{
    public double CoveragePercent { get; set; }
    public required string SonarRating { get; set; }
    public double CyclomaticComplexity { get; set; }
}

public class RepositoryView
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Branch { get; set; }
    public List<NamespaceNode> Namespaces { get; set; } = [];
    public List<SqlSchemaNode> SqlSchemas { get; set; } = [];
    public List<NamespaceLink> NamespaceLinks { get; set; } = [];
    public List<CrossRepoLink> InboundLinks { get; set; } = [];
    public List<CrossRepoLink> OutboundLinks { get; set; } = [];
}

public class NamespaceLink
{
    public required string SourceNamespace { get; set; }
    public required string TargetNamespace { get; set; }
    public int LinkCount { get; set; }
}

public class NamespaceNode
{
    public required string Path { get; set; }
    public int AtomCount { get; set; }
    public int DtoCount { get; set; }
    public int InterfaceCount { get; set; }
}

public class SqlSchemaNode
{
    public required string Schema { get; set; }
    public int TableCount { get; set; }
}

public class SnapshotSummary
{
    public required string Id { get; set; }
    public DateTimeOffset ScannedAt { get; set; }
    public string? Branch { get; set; }
    public int AtomCount { get; set; }
}

public class SnapshotGraph
{
    public required string Id { get; set; }
    public List<AtomNode> Nodes { get; set; } = [];
    public List<AtomLinkView> Links { get; set; } = [];
}

public class AtomLinkView
{
    public required string Source { get; set; }
    public required string Target { get; set; }
    public string? Type { get; set; }
    public bool IsViolation { get; set; }
    public List<string>? ViolationReasons { get; set; }
    public bool CrossRepo { get; set; }
}

public record NamespaceView
{
    public required string Path { get; set; }
    public List<AtomNode> Atoms { get; set; } = [];
    public List<InternalLink> InternalLinks { get; set; } = [];
}

public class InternalLink
{
    public required string SourceAtomId { get; set; }
    public required string TargetAtomId { get; set; }
    public required string LinkType { get; set; }
    public bool IsViolation { get; set; }
    public ViolationDetails? ViolationDetails { get; set; }
}

public class ViolationDetails
{
    public required string RuleId { get; set; }
    public required string Severity { get; set; }
    public required string Message { get; set; }
    public required string RemediationSuggestion { get; set; }
}

public class AtomNode
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public double RiskScore { get; set; }
    public int ConsumerCount { get; set; }
    public int? LinesOfCode { get; set; }
    public string? Language { get; set; }
    public bool IsPublic { get; set; }
    public double ChurnScore { get; set; }
    public double MaintenanceCost { get; set; }
}

public class AtomDetail
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public string? Namespace { get; set; }
    public string? FilePath { get; set; }
    public string? ParentTable { get; set; }
    public string? DataType { get; set; }
    public required string Repository { get; set; }
    public int? LinesOfCode { get; set; }
    public string? Language { get; set; }
    public bool IsPublic { get; set; }
    public List<MemberInfo> Members { get; set; } = [];
    public List<LinkInfo> InboundLinks { get; set; } = [];
    public List<LinkInfo> OutboundLinks { get; set; } = [];
}

public class LinkInfo
{
    public required string AtomId { get; set; }
    public required string LinkType { get; set; }
}

public class CrossRepoLink
{
    public required string SourceAtomId { get; set; }
    public required string TargetAtomId { get; set; }
    public required string SourceRepo { get; set; }
    public required string TargetRepo { get; set; }
    public required string LinkType { get; set; }
    public bool IsViolation { get; set; }
    public ViolationDetails? ViolationDetails { get; set; }
    public double Confidence { get; set; }
}

public class FederationStatsView
{
    public int TotalRepos { get; set; }
    public int TotalCodeAtoms { get; set; }
    public int TotalSqlAtoms { get; set; }
    public int TotalLinks { get; set; }
    public int CrossRepoLinkCount { get; set; }
}

public class BlastRadiusResult
{
    public required string SourceAtomId { get; set; }
    public List<AffectedAtomInfo> AffectedAtoms { get; set; } = [];
    public int TotalAffected { get; set; }
    public List<DepthGroup> ByDepth { get; set; } = [];
}

public class AffectedAtomInfo
{
    public required string AtomId { get; set; }
    public int Depth { get; set; }
}

public class DepthGroup
{
    public int Depth { get; set; }
    public int Count { get; set; }
}

public class SearchResult
{
    public required string AtomId { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required string Repository { get; set; }
}

public class MemberInfo
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public string? Signature { get; set; }
    public bool IsPublic { get; set; }
}

#endregion
