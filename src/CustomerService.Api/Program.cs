using CustomerService.Api.Data;
using CustomerService.Api.Errors;
using CustomerService.Api.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CustomerDbContext>((services, options) =>
    options.UseSqlServer(
        services.GetRequiredService<IConfiguration>().GetConnectionString("CustomerDb"),
        sql => sql.EnableRetryOnFailure()));

builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ICustomerService, CustomerManager>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<CustomerDbContext>("database", tags: [ReadinessTag]);

// Disposed on exit so the console logger flushes the failure below before the process ends.
await using var app = builder.Build();

// Fail fast with exit code 1 instead of letting the exception go unhandled: as PID 1 in a
// container the runtime's abort is ignored and the process hangs until something kills it.
try
{
    if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
    {
        await MigrateDatabaseAsync(app.Services);
    }

    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.MapControllers();
    app.MapHealthChecks("/health");
    // Liveness runs no checks, so a database outage never makes Kubernetes restart the pod.
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadinessTag) });

    await app.RunAsync();
    return 0;
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    app.Logger.LogCritical(exception, "Host terminated unexpectedly");
    return 1;
}

// Exposed so WebApplicationFactory<Program> can reach it from the integration tests.
public partial class Program
{
    private const string ReadinessTag = "ready";
    private const int SqlErrorDatabaseAlreadyExists = 1801;
    private const int MaxMigrationAttempts = 5;

    private static async Task MigrateDatabaseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<CustomerDbContext>().Database;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await database.MigrateAsync();
                return;
            }
            catch (SqlException exception) when (
                exception.Number == SqlErrorDatabaseAlreadyExists && attempt < MaxMigrationAttempts)
            {
                // Another replica is creating the database. EF's migration lock lives inside the
                // database, so it can't guard creation, and EF treats a database it can't open yet
                // as missing. Wait for creation to finish; after that the lock serialises migrations.
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
            }
        }
    }
}
