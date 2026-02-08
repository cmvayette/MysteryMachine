using DiagnosticStructuralLens.Core;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DiagnosticStructuralLens.Api.Persistence;

/// <summary>
/// Database context for the DSL application.
/// </summary>
public class DslDbContext : DbContext
{
    public DslDbContext(DbContextOptions<DslDbContext> options) : base(options) { }

    public DbSet<SnapshotEntity> Snapshots { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SnapshotEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(100);
            
            // Persist the Snapshot object as a JSONB column
            entity.Property(e => e.Data)
                .HasColumnType("jsonb");
        });
    }
}

/// <summary>
/// Entity wrapper for storing Snapshots in the database.
/// </summary>
public class SnapshotEntity
{
    public required string Id { get; set; }
    public required string Repository { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    
    // We store the full snapshot as a JSON document
    public required string Data { get; set; }
}
