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

// Database is optional — only register if connection string is configured
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var hasDatabase = !string.IsNullOrEmpty(connectionString);

if (hasDatabase)
{
    builder.Services.AddDbContext<DslDbContext>(options =>
        options.UseNpgsql(connectionString));
}
else
{
    // Register a no-op DbContext with in-memory provider so DI doesn't break
    builder.Services.AddDbContext<DslDbContext>(options =>
        options.UseInMemoryDatabase("dsl-fallback"));
    Console.WriteLine("ℹ️  No database configured — running in file-only mode.");
    Console.WriteLine("   Use --snapshots-dir <path> to load snapshots from a folder.");
}

// Add data services
builder.Services.AddSingleton<DiagnosticStructuralLensDataService>(); // Keep state between requests
builder.Services.AddSingleton<SnapshotService>();
builder.Services.AddSingleton<IGovernanceEngine, GovernanceEngine>();
builder.Services.AddSingleton<SemanticLinker>();

var app = builder.Build();

// Shared JSON options
var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
jsonOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());

// Ensure Database Created (only if real DB configured)
if (hasDatabase)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DslDbContext>();
    try
    {
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

// Load snapshot endpoint — works with or without DB
app.MapPost("/load", async (HttpRequest request, DiagnosticStructuralLensDataService dataService, DslDbContext db) =>
{
    try
    {
        using var reader = new StreamReader(request.Body);
        var json = await reader.ReadToEndAsync();

        var snapshot = JsonSerializer.Deserialize<Snapshot>(json, jsonOptions);
        if (snapshot == null) return Results.BadRequest("Invalid snapshot JSON");

        // Persist to DB if available
        if (hasDatabase)
        {
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
                entity.Data = json;
                entity.CreatedAt = snapshot.ScannedAt;
            }
            await db.SaveChangesAsync();
            Console.WriteLine($"💾 Persisted snapshot {snapshot.Id} to DB.");

            // Federate ALL snapshots from DB
            var allSnapshotsEntities = await db.Snapshots
                .GroupBy(s => s.Repository)
                .Select(g => g.OrderByDescending(s => s.CreatedAt).FirstOrDefault())
                .ToListAsync();

            var snapshots = new List<Snapshot>();
            foreach (var s in allSnapshotsEntities)
            {
                if (s == null) continue;
                var snap = JsonSerializer.Deserialize<Snapshot>(s.Data, jsonOptions);
                if (snap != null) snapshots.Add(snap);
            }

            Console.WriteLine($"🔄 Federating {snapshots.Count} snapshots for graph...");
            NormalizeRepoNames(snapshots);

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
        else
        {
            // No DB — just load this single snapshot into the in-memory graph
            NormalizeRepoNames([snapshot]);
            var engine = new FederationEngine();
            var federated = engine.Merge([snapshot]);
            dataService.LoadFederation(federated);
            Console.WriteLine($"📂 Loaded snapshot in-memory: {snapshot.CodeAtoms.Count} atoms");

            return Results.Ok(new {
                message = "Loaded (in-memory only, no database)",
                repositories = 1,
                totalCodeAtoms = federated.Stats.TotalCodeAtoms,
                totalLinks = federated.Stats.TotalLinks
            });
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error loading snapshot: {ex}");
        return Results.Problem($"Failed to load snapshot: {ex.Message}");
    }
});

// --- Startup data loading ---
// Priority: --snapshots-dir > single .json arg > DB hydration

// Support both CLI args and environment variable
var snapshotsDir = GetArgValue(args, "--snapshots-dir")
    ?? Environment.GetEnvironmentVariable("DSL_SNAPSHOTS_DIR")
    ?? app.Configuration["SnapshotsDir"];
var snapshotPath = args.FirstOrDefault(a => a.EndsWith(".json"));

if (!string.IsNullOrEmpty(snapshotsDir) && Directory.Exists(snapshotsDir))
{
    // Load all .json files from the directory and federate them
    var files = Directory.GetFiles(snapshotsDir, "*.json");
    if (files.Length > 0)
    {
        Console.WriteLine($"📂 Loading {files.Length} snapshot(s) from: {snapshotsDir}");
        var snapshots = new List<Snapshot>();
        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var snapshot = JsonSerializer.Deserialize<Snapshot>(json, jsonOptions);
                if (snapshot != null)
                {
                    snapshots.Add(snapshot);
                    Console.WriteLine($"   ✅ {System.IO.Path.GetFileName(file)}: {snapshot.CodeAtoms.Count} code atoms, {snapshot.SqlAtoms.Count} SQL atoms");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️ Skipping {System.IO.Path.GetFileName(file)}: {ex.Message}");
            }
        }

        if (snapshots.Count > 0)
        {
            NormalizeRepoNames(snapshots);
            var engine = new FederationEngine();
            var federated = engine.Merge(snapshots);
            var dataService = app.Services.GetRequiredService<DiagnosticStructuralLensDataService>();
            dataService.LoadFederation(federated);
            Console.WriteLine($"   🗺️ Federated: {federated.Stats.TotalCodeAtoms} atoms across {federated.Stats.TotalRepos} repos, {federated.Stats.TotalLinks} links");
        }
    }
    else
    {
        Console.WriteLine($"⚠️ No .json files found in: {snapshotsDir}");
    }
}
else if (!string.IsNullOrEmpty(snapshotPath) && File.Exists(snapshotPath))
{
    // Load a single snapshot file
    Console.WriteLine($"📂 Loading snapshot from CLI arg: {snapshotPath}");
    var json = await File.ReadAllTextAsync(snapshotPath);
    var snapshot = JsonSerializer.Deserialize<Snapshot>(json, jsonOptions);
    if (snapshot != null)
    {
        var dataService = app.Services.GetRequiredService<DiagnosticStructuralLensDataService>();
        var engine = new FederationEngine();
        var federated = engine.Merge([snapshot]);
        dataService.LoadFederation(federated);
        Console.WriteLine($"   ✅ Loaded {snapshot.CodeAtoms.Count} atoms, {snapshot.Links.Count} links");
    }
}
else if (hasDatabase)
{
    // Hydrate from DB on startup
    using var scope = app.Services.CreateScope();
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

            var snapshots = new List<Snapshot>();
            foreach (var s in allSnapshotsEntities)
            {
                if (s == null) continue;
                var snap = JsonSerializer.Deserialize<Snapshot>(s.Data, jsonOptions);
                if (snap != null) snapshots.Add(snap);
            }

            if (snapshots.Count > 0)
            {
                NormalizeRepoNames(snapshots);
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

app.MapFallbackToFile("index.html");


app.Run();

// --- Helper functions ---

static void NormalizeRepoNames(List<Snapshot> snapshots)
{
    foreach (var snap in snapshots)
    {
        if (snap.Repository.Contains('/') || snap.Repository.Contains('\\'))
        {
            snap.Repository = System.IO.Path.GetFileName(snap.Repository.TrimEnd('/', '\\'));
        }
    }
}

static string? GetArgValue(string[] args, string flag)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == flag) return args[i + 1];
    }
    return null;
}

public partial class Program { }
