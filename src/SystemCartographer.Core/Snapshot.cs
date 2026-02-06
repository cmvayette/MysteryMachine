namespace SystemCartographer.Core;

/// <summary>
/// A snapshot of all atoms and links from a single scan.
/// </summary>
public class Snapshot
{
    public required string Id { get; init; }
    public required string Repository { get; init; }
    public required DateTimeOffset ScannedAt { get; init; }
    public string? Branch { get; init; }
    public string? CommitSha { get; init; }
    
    public List<CodeAtom> CodeAtoms { get; init; } = [];
    public List<SqlAtom> SqlAtoms { get; init; } = [];
    public List<AtomLink> Links { get; init; } = [];
    
    public SnapshotMetadata Metadata { get; init; } = new();
}

public class SnapshotMetadata
{
    public int TotalCodeAtoms { get; set; }
    public int TotalSqlAtoms { get; set; }
    public int TotalLinks { get; set; }
    public int DtoCount { get; set; }
    public int InterfaceCount { get; set; }
    public int TableCount { get; set; }
    public int StoredProcedureCount { get; set; }
    public TimeSpan ScanDuration { get; set; }
}
