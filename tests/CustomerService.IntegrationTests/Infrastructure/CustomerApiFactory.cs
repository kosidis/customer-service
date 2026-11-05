using CustomerService.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MsSql;

namespace CustomerService.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the API in memory against a real database. By default it starts SQL Server 2022 in Docker
/// via Testcontainers and applies the EF migrations. Set INTEGRATION_TEST_DB=sqlite to run without
/// Docker against SQLite in-memory instead.
/// </summary>
public sealed class CustomerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly bool _useSqlite = string.Equals(
        Environment.GetEnvironmentVariable("INTEGRATION_TEST_DB"), "sqlite", StringComparison.OrdinalIgnoreCase);

    private MsSqlContainer? _sqlServer;
    private SqliteConnection? _sqlite;

    public async Task InitializeAsync()
    {
        if (_useSqlite)
        {
            _sqlite = new SqliteConnection("DataSource=:memory:");
            await _sqlite.OpenAsync();
        }
        else
        {
            _sqlServer = new MsSqlBuilder(SqlServerImage).Build();
            await _sqlServer.StartAsync();
        }

        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        if (_useSqlite)
        {
            await dbContext.Database.EnsureCreatedAsync();
        }
        else
        {
            await dbContext.Database.MigrateAsync();
        }
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CustomerDbContext>().Customers.ExecuteDeleteAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");

        if (_useSqlite)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<CustomerDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<CustomerDbContext>>();
                services.AddDbContext<CustomerDbContext>(options => options.UseSqlite(_sqlite!));
            });
        }
        else
        {
            builder.UseSetting("ConnectionStrings:CustomerDb", _sqlServer!.GetConnectionString());
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();

        if (_sqlServer is not null)
        {
            await _sqlServer.DisposeAsync();
        }

        if (_sqlite is not null)
        {
            await _sqlite.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<CustomerApiFactory>
{
    public const string Name = "Customer API";
}
