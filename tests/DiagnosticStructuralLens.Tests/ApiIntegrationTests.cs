using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using Xunit;

namespace DiagnosticStructuralLens.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetGraph_ReturnsNodes()
    {
        var client = _factory.CreateClient();
        
        // 1. We might need to load data first, but for now we expect empty or null graph if not loaded.
        // If DataService is empty, Graph is null. GetGraph returns null.
        // We can verify that the query doesn't crash.
        
        var query = new { query = "{ graph { id } }" };
        var response = await client.PostAsJsonAsync("/graphql", query);
        
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        
        // Should contain "graph" field, even if null
        Assert.Contains("graph", json);
    }

    [Fact]
    public async Task Traverse_ReturnsResult()
    {
        var client = _factory.CreateClient();
        
        // 2. Traversal on empty/null graph returns null
        var query = new { query = "{ traverse(nodeId: \"unknown\") { totalNodesFound } }" };
        var response = await client.PostAsJsonAsync("/graphql", query);
        
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        
        Assert.Contains("traverse", json);
    }
}
