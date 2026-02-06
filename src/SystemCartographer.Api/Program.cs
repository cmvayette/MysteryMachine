using SystemCartographer.Api.GraphQL;
using SystemCartographer.Core;
using SystemCartographer.Federation;
using SystemCartographer.Linker;
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
    .AddFiltering()
    .AddSorting()
    .AddProjections();

// Add data services
builder.Services.AddSingleton<CartographerDataService>(); // Keep state between requests
builder.Services.AddSingleton<SnapshotService>();
builder.Services.AddSingleton<IGovernanceEngine, GovernanceEngine>();
builder.Services.AddSingleton<SemanticLinker>();

var app = builder.Build();

app.MapGraphQL();

// Health check endpoint
app.MapGet("/health", () => "OK");

// Verify wwwroot exists and serve static files
app.UseDefaultFiles();
app.UseStaticFiles();

// Load snapshot endpoint
app.MapPost("/load", async (HttpRequest request, CartographerDataService dataService) =>
{
    using var reader = new StreamReader(request.Body);
    var json = await reader.ReadToEndAsync();
    
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    
    var snapshot = JsonSerializer.Deserialize<Snapshot>(json, options);
    if (snapshot == null) return Results.BadRequest("Invalid snapshot JSON");
    
    // Federate the snapshot
    var engine = new FederationEngine();
    var federated = engine.Merge([snapshot]);
    dataService.LoadFederation(federated);
    
    return Results.Ok(new { 
        message = "Loaded", 
        codeAtoms = snapshot.CodeAtoms.Count,
        sqlAtoms = snapshot.SqlAtoms.Count,
        links = snapshot.Links.Count
    });
});

// Auto-load snapshot from command line if provided
var snapshotPath = args.FirstOrDefault(a => a.EndsWith(".json"));
if (!string.IsNullOrEmpty(snapshotPath) && File.Exists(snapshotPath))
{
    Console.WriteLine($"📂 Loading snapshot: {snapshotPath}");
    var json = await File.ReadAllTextAsync(snapshotPath);
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    var snapshot = JsonSerializer.Deserialize<Snapshot>(json, options);
    if (snapshot != null)
    {
        var dataService = app.Services.GetRequiredService<CartographerDataService>();
        var engine = new FederationEngine();
        var federated = engine.Merge([snapshot]);
        dataService.LoadFederation(federated);
        Console.WriteLine($"   ✅ Loaded {snapshot.CodeAtoms.Count} atoms, {snapshot.Links.Count} links");
    }
}

app.MapFallbackToFile("index.html");

app.Run();

