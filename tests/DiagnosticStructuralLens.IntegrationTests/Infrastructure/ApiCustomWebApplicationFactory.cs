using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DiagnosticStructuralLens.IntegrationTests.Infrastructure;

public class ApiCustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Here we can replace services with mocks if needed
            // For now, we want the real thing but maybe with InMemory DB or specific config
        });

        builder.UseEnvironment("Development");
    }
}
