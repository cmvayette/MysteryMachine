using DiagnosticStructuralLens.Api.GraphQL;
using DiagnosticStructuralLens.Core;
using DiagnosticStructuralLens.Federation;
using DiagnosticStructuralLens.Linker;
using DiagnosticStructuralLens.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add HotChocolate GraphQL
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddType<FederationViewType>()
    .AddType<RepositoryNodeType>()
    .AddType<NamespaceNodeType>()
    .AddType<NamespaceViewType>()
    .AddType<InternalLinkType>()
    .AddType<AtomNodeType>()
    .AddType<AtomDetailType>()
    .AddType<BlastRadiusResultType>()
    .AddTypeExtension<GraphQuery>()
    .AddType<GraphNodeType>()
    .AddType<GraphEdgeType>()
    .AddType<TraversalResultType>()
    .AddType<TraversalLevelType>()
    .AddType<TraversalHitType>()
    .AddType<GraphCycleType>()
    .AddFiltering()
    .AddSorting()
    .AddProjections();



// Add Database
builder.Services.AddDbContext<DslDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add data services
builder.Services.AddSingleton<DiagnosticStructuralLensDataService>(); // Keep state between requests
builder.Services.AddSingleton<SnapshotService>();
builder.Services.AddSingleton<IGovernanceEngine, GovernanceEngine>();
builder.Services.AddSingleton<SemanticLinker>();

var app = builder.Build();



// Ensure Database Created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DslDbContext>();
    try 
    {
        // Simple strategy for now: EnsureCreated. 
        // In prod we would use Migrations.
        db.Database.EnsureCreated();
        Console.WriteLine("✅ Database ensured.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Database connection failed: {ex.Message}");
    }
}

app.MapGraphQL();

// Health check endpoint
app.MapGet("/health", () => "OK");

// Verify wwwroot exists and serve static files
app.UseDefaultFiles();
app.UseStaticFiles();

// Load snapshot endpoint
app.MapPost("/load", async (HttpRequest request, DiagnosticStructuralLensDataService dataService, DslDbContext db) =>
{
    try 
    {
        using var reader = new StreamReader(request.Body);
        var json = await reader.ReadToEndAsync();
        
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        
        var snapshot = JsonSerializer.Deserialize<Snapshot>(json, options);
        if (snapshot == null) return Results.BadRequest("Invalid snapshot JSON");
        
        // Persist to DB
        var entity = await db.Snapshots.FindAsync(snapshot.Id);
        if (entity == null)
        {
            entity = new SnapshotEntity 
            { 
                Id = snapshot.Id, 
                Repository = snapshot.Repository,
                CreatedAt = snapshot.ScannedAt,
                Data = json 
            };
            db.Snapshots.Add(entity);
        }
        else
        {
            // Update existing
            entity.Data = json;
            entity.CreatedAt = snapshot.ScannedAt;
        }
        await db.SaveChangesAsync();
        Console.WriteLine($"💾 Persisted snapshot {snapshot.Id} to DB.");
        
        // Federate ALL snapshots (Incremental Graph)
        // 1. Get latest snapshot for each repository
        var allSnapshotsEntities = await db.Snapshots
            .GroupBy(s => s.Repository)
            .Select(g => g.OrderByDescending(s => s.CreatedAt).FirstOrDefault())
            .ToListAsync();
            
        var snapshots = new List<Snapshot>();
        foreach (var s in allSnapshotsEntities)
        {
            if (s == null) continue;
            var snap = JsonSerializer.Deserialize<Snapshot>(s.Data, options);
            if (snap != null) snapshots.Add(snap);
        }
        
        Console.WriteLine($"🔄 Federating {snapshots.Count} snapshots for graph...");

        // Normalize legacy full-path repository names to just the directory name
        foreach (var snap in snapshots)
        {
            if (snap.Repository.Contains('/') || snap.Repository.Contains('\\'))
            {
                snap.Repository = System.IO.Path.GetFileName(snap.Repository.TrimEnd('/', '\\'));
            }
        }

        // 2. Federate
        var engine = new FederationEngine();
        var federated = engine.Merge(snapshots);
        dataService.LoadFederation(federated);
        
        return Results.Ok(new { 
            message = "Loaded and Persisted", 
            repositories = snapshots.Count,
            totalCodeAtoms = federated.Stats.TotalCodeAtoms,
            totalLinks = federated.Stats.TotalLinks
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error loading snapshot: {ex}");
        return Results.Problem($"Failed to load snapshot: {ex.Message}");
    }
});

// Hydrate from DB on startup if CLI args empty
// Load latest from EACH repo
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DslDbContext>();
    var dataService = scope.ServiceProvider.GetRequiredService<DiagnosticStructuralLensDataService>();
    
    try
    {
        var allSnapshotsEntities = await db.Snapshots
            .GroupBy(s => s.Repository)
            .Select(g => g.OrderByDescending(s => s.CreatedAt).FirstOrDefault())
            .ToListAsync();

        if (allSnapshotsEntities.Any())
        {
            Console.WriteLine($"🔄 Hydrating graph from {allSnapshotsEntities.Count} repositories...");
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            
            var snapshots = new List<Snapshot>();
            foreach (var s in allSnapshotsEntities)
            {
                if (s == null) continue;
                var snap = JsonSerializer.Deserialize<Snapshot>(s.Data, options);
                if (snap != null) snapshots.Add(snap);
            }

            if (snapshots.Count > 0)
            {
                // Normalize legacy full-path repository names
                foreach (var snap in snapshots)
                {
                    if (snap.Repository.Contains('/') || snap.Repository.Contains('\\'))
                    {
                        snap.Repository = System.IO.Path.GetFileName(snap.Repository.TrimEnd('/', '\\'));
                    }
                }

                var engine = new FederationEngine();
                var federated = engine.Merge(snapshots);
                dataService.LoadFederation(federated);
                Console.WriteLine($"   ✅ Graph ready: {federated.Stats.TotalCodeAtoms} atoms across {federated.Stats.TotalRepos} repos.");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Failed to hydrate from DB: {ex.Message}");
    }
}

// Auto-load snapshot from command line if provided (overrides DB)
var snapshotPath = args.FirstOrDefault(a => a.EndsWith(".json"));
if (!string.IsNullOrEmpty(snapshotPath) && File.Exists(snapshotPath))
{
    Console.WriteLine($"📂 Loading snapshot from CLI arg: {snapshotPath}");
    var json = await File.ReadAllTextAsync(snapshotPath);
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    var snapshot = JsonSerializer.Deserialize<Snapshot>(json, options);
    if (snapshot != null)
    {
        var dataService = app.Services.GetRequiredService<DiagnosticStructuralLensDataService>();
        // Just load this one for CLI mode
        
        var engine = new FederationEngine();
        var federated = engine.Merge([snapshot]);
        dataService.LoadFederation(federated);
        Console.WriteLine($"   ✅ Loaded {snapshot.CodeAtoms.Count} atoms, {snapshot.Links.Count} links");
    }
}

app.MapFallbackToFile("index.html");


app.Run();

public partial class Program { }

