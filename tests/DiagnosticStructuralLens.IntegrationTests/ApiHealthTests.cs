using DiagnosticStructuralLens.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DiagnosticStructuralLens.IntegrationTests;

public class ApiHealthTests : IClassFixture<ApiCustomWebApplicationFactory>
{
    private readonly ApiCustomWebApplicationFactory _factory;

    public ApiHealthTests(ApiCustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("OK", content);
    }
}
