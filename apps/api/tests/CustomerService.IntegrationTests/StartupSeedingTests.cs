using CustomerService.Api.Data;
using CustomerService.Api.Domain;
using CustomerService.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerService.IntegrationTests;

[Collection(ApiCollection.Name)]
public class StartupSeedingTests(CustomerApiFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => factory.ResetDatabaseAsync();

    private static bool UsingSqlite =>
        string.Equals(Environment.GetEnvironmentVariable("INTEGRATION_TEST_DB"), "sqlite", StringComparison.OrdinalIgnoreCase);

    private static async Task<List<User>> UsersAfterStartupAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CustomerDbContext>().Users.AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task Startup_MigrateFlagOn_SeedsAdmin()
    {
        // Flag-on runs real migrations, so it only counts in the SQL Server run.
        if (UsingSqlite)
        {
            return;
        }

        using var host = factory.WithWebHostBuilder(b => b.UseSetting("Database:ApplyMigrationsOnStartup", "true"));
        using var client = host.CreateClient();

        var users = await UsersAfterStartupAsync(host);

        var admin = Assert.Single(users);
        Assert.Equal("admin", admin.Username);
        Assert.Equal(Role.Admin, admin.Role);
        Assert.True(admin.MustChangePassword);
    }

    [Fact]
    public async Task Startup_MigrateFlagOff_SeedsNothing()
    {
        using var host = factory.WithWebHostBuilder(b => b.UseSetting("Database:ApplyMigrationsOnStartup", "false"));
        using var client = host.CreateClient();

        var users = await UsersAfterStartupAsync(host);

        Assert.Empty(users);
    }
}
