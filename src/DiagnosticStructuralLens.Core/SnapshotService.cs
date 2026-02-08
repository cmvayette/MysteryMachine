using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiagnosticStructuralLens.Core;

public interface ISnapshotService
{
    Task<List<SnapshotMetadata>> GetSnapshotsAsync();
    Task<Snapshot?> GetSnapshotAsync(string id);
}

public class SnapshotService : ISnapshotService
{
    // In-memory mock storage for Phase 4 demonstration
    // In a real implementation, this would read from Blob Storage or a Database
    private readonly List<Snapshot> _snapshots = new();

    public SnapshotService()
    {
        // Seed some historical data
        var baseTime = DateTimeOffset.UtcNow.AddDays(-30);
        
        // Snapshot 1: 30 days ago (Baseline)
        _snapshots.Add(CreateMockSnapshot("snap-001", baseTime, "initial-commit", 100));

        // Snapshot 2: 20 days ago (Growth)
        _snapshots.Add(CreateMockSnapshot("snap-002", baseTime.AddDays(10), "feature/auth", 150));

        // Snapshot 3: 10 days ago (Refactor)
        _snapshots.Add(CreateMockSnapshot("snap-003", baseTime.AddDays(20), "fix/bugs", 160));

        // Snapshot 4: Today (Current HEAD)
        _snapshots.Add(CreateMockSnapshot("snap-head", DateTimeOffset.UtcNow, "main", 200));
    }

    public Task<List<SnapshotMetadata>> GetSnapshotsAsync()
    {
        var metadata = _snapshots
            .Select(s => new SnapshotMetadata
            {
                // We need to extend SnapshotMetadata to include ID/Date for the list view
                // For now, let's assume we map it or add fields to metadata
            })
            .ToList();
            
        // Actually, let's just return the headers we need.
        // The SnapshotMetadata class in Snapshot.cs serves a different purpose (stats).
        // Let's rely on the Query layer to shape this, or just return the Snapshots logic here.
        
        // For simplicity in this interface, let's return the full objects metadata projection
        return Task.FromResult(_snapshots.Select(s => s.Metadata).ToList());
    }
    
    // Helper to get full snapshot list with ID/Date (since Metadata class doesn't have it by default)
    // In reality we should refactor SnapshotMetadata, but let's stick to the current type
    // and query the Snapshot object directly in GraphQL.
    public Task<IEnumerable<Snapshot>> GetAllSnapshotsAsync()
    {
        return Task.FromResult(_snapshots.AsEnumerable());
    }

    public Task<Snapshot?> GetSnapshotAsync(string id)
    {
        var snapshot = _snapshots.FirstOrDefault(s => s.Id == id);
        return Task.FromResult(snapshot);
    }

    private Snapshot CreateMockSnapshot(string id, DateTimeOffset date, string branch, int atomCount)
    {
        return new Snapshot
        {
            Id = id,
            Repository = "MysteryMachine",
            ScannedAt = date,
            Branch = branch,
            CommitSha = Guid.NewGuid().ToString().Substring(0, 7),
            Metadata = new SnapshotMetadata
            {
                TotalCodeAtoms = atomCount,
                TotalLinks = atomCount * 2,
                ScanDuration = TimeSpan.FromSeconds(5)
            },
            CodeAtoms = new List<CodeAtom>(), // Empty for list view, populated if we needed details
            Links = new List<AtomLink>()
        };
    }
}
