using System.Net.Http.Headers;
using System.Net.Http.Json;
using CustomerService.Api.Contracts;
using CustomerService.Api.Data;
using CustomerService.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using CustomerService.Api.Auth;
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

    /// <summary>Name of the throwaway policy behind <c>GET /test/admin-only</c>.</summary>
    public const string TestAdminPolicy = "TestAdminOnly";

    /// <summary>Password given to every user created by <see cref="CreateSignedInClientAsync"/>.</summary>
    public const string TestPassword = "Test-Password-1!";

    /// <summary>Reads the API's JSON, where <see cref="Role"/> travels as a string.</summary>
    public static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

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
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        await dbContext.Customers.ExecuteDeleteAsync();
        // Sessions reference Users, so they go first.
        await dbContext.Sessions.ExecuteDeleteAsync();
        await dbContext.Users.ExecuteDeleteAsync();
    }

    /// <summary>
    /// Inserts a user, signs in through <c>POST /api/auth/login</c> and returns a client that sends
    /// the resulting bearer token.
    /// </summary>
    public async Task<HttpClient> CreateSignedInClientAsync(
        string username = "viewer", Role role = Role.Admin, bool mustChangePassword = false)
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            var user = new User
            {
                Username = username.Trim().ToLowerInvariant(),
                Role = role,
                MustChangePassword = mustChangePassword,
                CreatedAtUtc = DateTime.UtcNow
            };
            user.PasswordHash = hasher.HashPassword(user, TestPassword);
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        }

        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, TestPassword));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");

        // GET /test/admin-only (TestAdminController) stands in for the real AdminOnly endpoints, which
        // arrive later; it proves a named policy built with AuthPolicies.AddNamedPolicy turns away a
        // pending Admin.
        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(TestAdminController).Assembly);
            services.AddAuthorization(options => AuthPolicies.AddNamedPolicy(
                options, TestAdminPolicy, policy => policy.RequireRole(nameof(Role.Admin))));
        });

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

[Microsoft.AspNetCore.Mvc.ApiController]
[Microsoft.AspNetCore.Mvc.Route("test/admin-only")]
public class TestAdminController : Microsoft.AspNetCore.Mvc.ControllerBase
{
    [Microsoft.AspNetCore.Mvc.HttpGet]
    [Authorize(Policy = CustomerApiFactory.TestAdminPolicy)]
    public Microsoft.AspNetCore.Mvc.IActionResult Get() => Ok();
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<CustomerApiFactory>
{
    public const string Name = "Customer API";
}
