using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CustomerService.IntegrationTests;

// Kubernetes restarts a pod whose liveness probe fails, so liveness must not depend on the
// database: an outage should take pods out of the Service (readiness), not restart them all.
public sealed class HealthProbeTests : IClassFixture<HealthProbeTests.UnreachableDatabaseFactory>
{
    private readonly HttpClient _client;

    public HealthProbeTests(UnreachableDatabaseFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Live_DatabaseUnreachable_Returns200()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_DatabaseUnreachable_Returns503()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    public sealed class UnreachableDatabaseFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
            // Port 1 refuses connections immediately, so the check fails fast.
            builder.UseSetting("ConnectionStrings:CustomerDb",
                "Server=127.0.0.1,1;Database=CustomerDb;User Id=sa;Password=unused;TrustServerCertificate=True;Connect Timeout=2");
        }
    }
}
