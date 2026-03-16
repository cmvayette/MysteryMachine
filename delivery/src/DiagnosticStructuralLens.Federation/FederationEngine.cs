using DiagnosticStructuralLens.Core;
using DiagnosticStructuralLens.Linker;

namespace DiagnosticStructuralLens.Federation;

/// <summary>
/// Federates multiple repository snapshots into a unified global map.
/// </summary>
public class FederationEngine
{
    private readonly SemanticLinker _linker = new();

    /// <summary>
    /// Merge multiple snapshots into a federated snapshot.
    /// </summary>
    public FederatedSnapshot Merge(IEnumerable<Snapshot> snapshots, FederationOptions? options = null)
    {
        options ??= new FederationOptions();
        var snapshotList = snapshots.ToList();

        if (snapshotList.Count == 0)
            return CreateEmptyFederatedSnapshot();

        var allCodeAtoms = new List<FederatedAtom<CodeAtom>>();
        var allSqlAtoms = new List<FederatedAtom<SqlAtom>>();
        var allLinks = new List<FederatedLink>();
        var conflicts = new List<AtomConflict>();
        var repoMetadata = new Dictionary<string, RepoMetadata>();

        // Track seen atom IDs for conflict detection
        var seenCodeAtoms = new Dictionary<string, (CodeAtom Atom, string Repo)>();
        var seenSqlAtoms = new Dictionary<string, (SqlAtom Atom, string Repo)>();

        foreach (var snapshot in snapshotList)
        {
            var repoId = snapshot.Repository ?? snapshot.Id;
            
            // Track repo metadata
            repoMetadata[repoId] = new RepoMetadata
            {
                RepoId = repoId,
                Branch = snapshot.Branch,
                CommitSha = snapshot.CommitSha,
                ScannedAt = snapshot.ScannedAt,
                AtomCount = snapshot.CodeAtoms.Count + snapshot.SqlAtoms.Count
            };

            // Process code atoms
            foreach (var atom in snapshot.CodeAtoms)
            {
                if (seenCodeAtoms.TryGetValue(atom.Id, out var existing))
                {
                    // Potential conflict
                    var conflict = DetectCodeAtomConflict(atom, existing.Atom, repoId, existing.Repo, snapshot, options);
                    if (conflict != null)
                    {
                        conflicts.Add(conflict);
                        if (options.ConflictResolution == ConflictResolution.NewestWins)
                        {
                            // Replace if this one is newer
                            var existingRepo = repoMetadata[existing.Repo];
                            if (snapshot.ScannedAt > existingRepo.ScannedAt)
                            {
                                seenCodeAtoms[atom.Id] = (atom, repoId);
                            }
                        }
                        else if (options.ConflictResolution == ConflictResolution.PriorityOrder)
                        {
                            var existingPriority = options.RepoPriority.IndexOf(existing.Repo);
                            var newPriority = options.RepoPriority.IndexOf(repoId);
                            if (newPriority >= 0 && (existingPriority < 0 || newPriority < existingPriority))
                            {
                                seenCodeAtoms[atom.Id] = (atom, repoId);
                            }
                        }
                    }
                }
                else
                {
                    seenCodeAtoms[atom.Id] = (atom, repoId);
                }
            }

            // Process SQL atoms
            foreach (var atom in snapshot.SqlAtoms)
            {
                if (seenSqlAtoms.TryGetValue(atom.Id, out var existing))
                {
                    var conflict = DetectSqlAtomConflict(atom, existing.Atom, repoId, existing.Repo);
                    if (conflict != null)
                    {
                        conflicts.Add(conflict);
                        if (options.ConflictResolution == ConflictResolution.NewestWins)
                        {
                            var existingRepo = repoMetadata[existing.Repo];
                            if (snapshot.ScannedAt > existingRepo.ScannedAt)
                            {
                                seenSqlAtoms[atom.Id] = (atom, repoId);
                            }
                        }
                    }
                }
                else
                {
                    seenSqlAtoms[atom.Id] = (atom, repoId);
                }
            }

            // Process links with provenance
            foreach (var link in snapshot.Links)
            {
                allLinks.Add(new FederatedLink
                {
                    Link = link,
                    SourceRepo = repoId,
                    TargetRepo = repoId  // Same-repo link
                });
            }
        }

        // Build federated atoms from resolved conflicts
        foreach (var (id, (atom, repo)) in seenCodeAtoms)
        {
            allCodeAtoms.Add(new FederatedAtom<CodeAtom>
            {
                Atom = atom,
                SourceRepo = repo,
                ScannedAt = repoMetadata[repo].ScannedAt
            });
        }

        foreach (var (id, (atom, repo)) in seenSqlAtoms)
        {
            allSqlAtoms.Add(new FederatedAtom<SqlAtom>
            {
                Atom = atom,
                SourceRepo = repo,
                ScannedAt = repoMetadata[repo].ScannedAt
            });
        }

        // Create cross-repo links
        if (options.EnableCrossRepoLinking)
        {
            var crossRepoLinks = CreateCrossRepoLinks(allCodeAtoms, allSqlAtoms, options);
            allLinks.AddRange(crossRepoLinks);
        }

        return new FederatedSnapshot
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            FederatedAt = DateTimeOffset.UtcNow,
            CodeAtoms = allCodeAtoms,
            SqlAtoms = allSqlAtoms,
            Links = allLinks,
            Conflicts = conflicts,
            RepoMetadata = repoMetadata.Values.ToList(),
            Stats = new FederationStats
            {
                TotalRepos = repoMetadata.Count,
                TotalCodeAtoms = allCodeAtoms.Count,
                TotalSqlAtoms = allSqlAtoms.Count,
                TotalLinks = allLinks.Count,
                ConflictCount = conflicts.Count,
                CrossRepoLinkCount = allLinks.Count(l => l.SourceRepo != l.TargetRepo)
            }
        };
    }

    /// <summary>
    /// Detect conflicts between code atoms.
    /// </summary>
    private AtomConflict? DetectCodeAtomConflict(CodeAtom newAtom, CodeAtom existingAtom, 
        string newRepo, string existingRepo, Snapshot newSnapshot, FederationOptions options)
    {
        // Same signature = no conflict (shared via NuGet, etc.)
        if (AreCodeAtomsEquivalent(newAtom, existingAtom))
            return null;

        return new AtomConflict
        {
            AtomId = newAtom.Id,
            ConflictType = ConflictType.SignatureMismatch,
            Repo1 = existingRepo,
            Repo2 = newRepo,
            Description = $"Atom '{newAtom.Name}' has different signatures in {existingRepo} vs {newRepo}",
            Atom1Signature = GetCodeAtomSignature(existingAtom),
            Atom2Signature = GetCodeAtomSignature(newAtom)
        };
    }

    /// <summary>
    /// Detect conflicts between SQL atoms.
    /// </summary>
    private AtomConflict? DetectSqlAtomConflict(SqlAtom newAtom, SqlAtom existingAtom, string newRepo, string existingRepo)
    {
        if (AreSqlAtomsEquivalent(newAtom, existingAtom))
            return null;

        return new AtomConflict
        {
            AtomId = newAtom.Id,
            ConflictType = ConflictType.SignatureMismatch,
            Repo1 = existingRepo,
            Repo2 = newRepo,
            Description = $"SQL atom '{newAtom.Name}' differs between {existingRepo} and {newRepo}",
            Atom1Signature = $"{existingAtom.Type}:{existingAtom.Name}",
            Atom2Signature = $"{newAtom.Type}:{newAtom.Name}"
        };
    }

    /// <summary>
    /// Create cross-repository links between atoms.
    /// </summary>
    private List<FederatedLink> CreateCrossRepoLinks(
        List<FederatedAtom<CodeAtom>> codeAtoms,
        List<FederatedAtom<SqlAtom>> sqlAtoms,
        FederationOptions options)
    {
        var crossRepoLinks = new List<FederatedLink>();
        
        // Build lookup tables
        var codeAtomsByRepo = codeAtoms.ToLookup(a => a.SourceRepo);
        var sqlAtomsByRepo = sqlAtoms.ToLookup(a => a.SourceRepo);
        var repos = codeAtomsByRepo.Select(g => g.Key).Union(sqlAtomsByRepo.Select(g => g.Key)).ToList();

        // For each pair of repos, try to create cross-repo links
        for (int i = 0; i < repos.Count; i++)
        {
            for (int j = i + 1; j < repos.Count; j++)
            {
                var repoA = repos[i];
                var repoB = repos[j];

                // Link Repo A code atoms to Repo B SQL atoms
                var repoACodeAtoms = codeAtomsByRepo[repoA].Select(a => a.Atom).ToList();
                var repoBSqlAtoms = sqlAtomsByRepo[repoB].Select(a => a.Atom).ToList();
                
                if (repoACodeAtoms.Count > 0 && repoBSqlAtoms.Count > 0)
                {
                    var linkResult = _linker.LinkAtoms(repoACodeAtoms, repoBSqlAtoms);
                    foreach (var link in linkResult.Links)
                    {
                        // Reduce confidence for cross-repo links
                        crossRepoLinks.Add(new FederatedLink
                        {
                            Link = link with { Confidence = link.Confidence * options.CrossRepoConfidenceMultiplier },
                            SourceRepo = repoA,
                            TargetRepo = repoB
                        });
                    }
                }

                // Also link Repo B code atoms to Repo A SQL atoms
                var repoBCodeAtoms = codeAtomsByRepo[repoB].Select(a => a.Atom).ToList();
                var repoASqlAtoms = sqlAtomsByRepo[repoA].Select(a => a.Atom).ToList();

                if (repoBCodeAtoms.Count > 0 && repoASqlAtoms.Count > 0)
                {
                    var linkResult = _linker.LinkAtoms(repoBCodeAtoms, repoASqlAtoms);
                    foreach (var link in linkResult.Links)
                    {
                        crossRepoLinks.Add(new FederatedLink
                        {
                            Link = link with { Confidence = link.Confidence * options.CrossRepoConfidenceMultiplier },
                            SourceRepo = repoB,
                            TargetRepo = repoA
                        });
                    }
                }
            }
        }

        return crossRepoLinks;
    }

    /// <summary>
    /// Calculate delta between two snapshots.
    /// </summary>
    public SnapshotDelta CalculateDelta(Snapshot before, Snapshot after)
    {
        var beforeCodeIds = before.CodeAtoms.Select(a => a.Id).ToHashSet();
        var afterCodeIds = after.CodeAtoms.Select(a => a.Id).ToHashSet();
        
        var beforeSqlIds = before.SqlAtoms.Select(a => a.Id).ToHashSet();
        var afterSqlIds = after.SqlAtoms.Select(a => a.Id).ToHashSet();

        return new SnapshotDelta
        {
            SourceRepo = after.Repository ?? after.Id,
            BeforeCommit = before.CommitSha,
            AfterCommit = after.CommitSha,
            AddedCodeAtoms = after.CodeAtoms.Where(a => !beforeCodeIds.Contains(a.Id)).ToList(),
            RemovedCodeAtomIds = beforeCodeIds.Except(afterCodeIds).ToList(),
            AddedSqlAtoms = after.SqlAtoms.Where(a => !beforeSqlIds.Contains(a.Id)).ToList(),
            RemovedSqlAtomIds = beforeSqlIds.Except(afterSqlIds).ToList(),
            AddedLinks = after.Links.Where(l => !before.Links.Any(bl => bl.Id == l.Id)).ToList(),
            RemovedLinkIds = before.Links.Select(l => l.Id).Except(after.Links.Select(l => l.Id)).ToList()
        };
    }

    /// <summary>
    /// Apply a delta to an existing federated snapshot.
    /// </summary>
    public FederatedSnapshot ApplyDelta(FederatedSnapshot golden, SnapshotDelta delta)
    {
        // Remove old atoms
        var codeAtoms = golden.CodeAtoms
            .Where(a => !delta.RemovedCodeAtomIds.Contains(a.Atom.Id))
            .ToList();
        
        var sqlAtoms = golden.SqlAtoms
            .Where(a => !delta.RemovedSqlAtomIds.Contains(a.Atom.Id))
            .ToList();

        // Add new atoms
        foreach (var atom in delta.AddedCodeAtoms)
        {
            codeAtoms.Add(new FederatedAtom<CodeAtom>
            {
                Atom = atom,
                SourceRepo = delta.SourceRepo,
                ScannedAt = DateTimeOffset.UtcNow
            });
        }

        foreach (var atom in delta.AddedSqlAtoms)
        {
            sqlAtoms.Add(new FederatedAtom<SqlAtom>
            {
                Atom = atom,
                SourceRepo = delta.SourceRepo,
                ScannedAt = DateTimeOffset.UtcNow
            });
        }

        // Update links
        var links = golden.Links
            .Where(l => !delta.RemovedLinkIds.Contains(l.Link.Id))
            .ToList();

        foreach (var link in delta.AddedLinks)
        {
            links.Add(new FederatedLink
            {
                Link = link,
                SourceRepo = delta.SourceRepo,
                TargetRepo = delta.SourceRepo
            });
        }

        return golden with
        {
            FederatedAt = DateTimeOffset.UtcNow,
            CodeAtoms = codeAtoms,
            SqlAtoms = sqlAtoms,
            Links = links,
            Stats = golden.Stats with
            {
                TotalCodeAtoms = codeAtoms.Count,
                TotalSqlAtoms = sqlAtoms.Count,
                TotalLinks = links.Count
            }
        };
    }

    private bool AreCodeAtomsEquivalent(CodeAtom a, CodeAtom b)
    {
        return a.Name == b.Name && 
               a.Type == b.Type && 
               a.Namespace == b.Namespace;
    }

    private bool AreSqlAtomsEquivalent(SqlAtom a, SqlAtom b)
    {
        return a.Name == b.Name && 
               a.Type == b.Type &&
               a.ParentTable == b.ParentTable;
    }

    private string GetCodeAtomSignature(CodeAtom atom)
    {
        return $"{atom.Namespace}.{atom.Name} ({atom.Type})";
    }

    private FederatedSnapshot CreateEmptyFederatedSnapshot()
    {
        return new FederatedSnapshot
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            FederatedAt = DateTimeOffset.UtcNow,
            CodeAtoms = [],
            SqlAtoms = [],
            Links = [],
            Conflicts = [],
            RepoMetadata = [],
            Stats = new FederationStats()
        };
    }
}

#region Models

/// <summary>
/// A snapshot containing federated data from multiple repositories.
/// </summary>
public record FederatedSnapshot
{
    public required string Id { get; init; }
    public DateTimeOffset FederatedAt { get; init; }
    public required List<FederatedAtom<CodeAtom>> CodeAtoms { get; init; }
    public required List<FederatedAtom<SqlAtom>> SqlAtoms { get; init; }
    public required List<FederatedLink> Links { get; init; }
    public required List<AtomConflict> Conflicts { get; init; }
    public required List<RepoMetadata> RepoMetadata { get; init; }
    public required FederationStats Stats { get; init; }
}

/// <summary>
/// An atom with provenance information.
/// </summary>
public record FederatedAtom<T>
{
    public required T Atom { get; init; }
    public required string SourceRepo { get; init; }
    public DateTimeOffset ScannedAt { get; init; }
}

/// <summary>
/// A link with cross-repo provenance.
/// </summary>
public record FederatedLink
{
    public required AtomLink Link { get; init; }
    public required string SourceRepo { get; init; }
    public required string TargetRepo { get; init; }
}

/// <summary>
/// Metadata about a repository in the federation.
/// </summary>
public record RepoMetadata
{
    public required string RepoId { get; init; }
    public string? Branch { get; init; }
    public string? CommitSha { get; init; }
    public DateTimeOffset ScannedAt { get; init; }
    public int AtomCount { get; init; }
}

/// <summary>
/// A conflict between atoms from different repositories.
/// </summary>
public record AtomConflict
{
    public required string AtomId { get; init; }
    public ConflictType ConflictType { get; init; }
    public required string Repo1 { get; init; }
    public required string Repo2 { get; init; }
    public required string Description { get; init; }
    public string? Atom1Signature { get; init; }
    public string? Atom2Signature { get; init; }
}

/// <summary>
/// Types of conflicts.
/// </summary>
public enum ConflictType
{
    SignatureMismatch,
    DuplicateId,
    VersionMismatch
}

/// <summary>
/// Conflict resolution strategies.
/// </summary>
public enum ConflictResolution
{
    NewestWins,
    PriorityOrder,
    KeepBoth,
    Fail
}

/// <summary>
/// Options for federation.
/// </summary>
public record FederationOptions
{
    public ConflictResolution ConflictResolution { get; init; } = ConflictResolution.NewestWins;
    public List<string> RepoPriority { get; init; } = [];
    public bool EnableCrossRepoLinking { get; init; } = true;
    public double CrossRepoConfidenceMultiplier { get; init; } = 0.8;
}

/// <summary>
/// Statistics about a federated snapshot.
/// </summary>
public record FederationStats
{
    public int TotalRepos { get; init; }
    public int TotalCodeAtoms { get; init; }
    public int TotalSqlAtoms { get; init; }
    public int TotalLinks { get; init; }
    public int ConflictCount { get; init; }
    public int CrossRepoLinkCount { get; init; }
}

/// <summary>
/// Delta between two snapshots.
/// </summary>
public record SnapshotDelta
{
    public required string SourceRepo { get; init; }
    public string? BeforeCommit { get; init; }
    public string? AfterCommit { get; init; }
    public required List<CodeAtom> AddedCodeAtoms { get; init; }
    public required List<string> RemovedCodeAtomIds { get; init; }
    public required List<SqlAtom> AddedSqlAtoms { get; init; }
    public required List<string> RemovedSqlAtomIds { get; init; }
    public required List<AtomLink> AddedLinks { get; init; }
    public required List<string> RemovedLinkIds { get; init; }
}

#endregion
