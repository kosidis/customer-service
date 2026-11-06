---
name: testcontainers
description: WHEN running, writing or debugging integration tests in apps/api/tests/CustomerService.IntegrationTests, which host the API with WebApplicationFactory against SQL Server in a container or SQLite.
---

# Integration tests: WebApplicationFactory + Testcontainers

Packages: `Microsoft.AspNetCore.Mvc.Testing` 10.0.14, `Testcontainers.MsSql` 4.16.0, `Microsoft.EntityFrameworkCore.Sqlite` 10.0.14.

## How the host is built

`apps/api/tests/CustomerService.IntegrationTests/Infrastructure/CustomerApiFactory.cs`:

- `WebApplicationFactory<Program>` + `IAsyncLifetime`, shared by every test class through `[Collection(ApiCollection.Name)]`, so one database container serves the whole run.
- **Default mode:** starts `mcr.microsoft.com/mssql/server:2022-latest` with `new MsSqlBuilder(SqlServerImage).Build()`, sets `ConnectionStrings:CustomerDb` with `UseSetting`, then runs `MigrateAsync()`. This is the real thing; prefer it before merging data-access changes.
- **`INTEGRATION_TEST_DB=sqlite`:** opens an in-memory SQLite connection, swaps the DbContext in `ConfigureTestServices` (removing both `DbContextOptions<CustomerDbContext>` and `IDbContextOptionsConfiguration<CustomerDbContext>`, which EF Core 9+ needs), then `EnsureCreatedAsync()`. No container runtime needed; used by `.claude/pre-pr-checks`.
- `Database:ApplyMigrationsOnStartup` is forced to `false`; the factory migrates itself.
- `ResetDatabaseAsync()` deletes all customers. Each test class calls it in `InitializeAsync`, so tests never depend on order.

## Run

```bash
dotnet test apps/api/tests/CustomerService.IntegrationTests                         # SQL Server, needs Docker or Podman
INTEGRATION_TEST_DB=sqlite dotnet test apps/api/tests/CustomerService.IntegrationTests
```

With Podman, point Testcontainers at its socket first:

```bash
export DOCKER_HOST="unix://$(podman machine inspect --format '{{.ConnectionInfo.PodmanSocket.Path}}')"
```

To confirm a run really used SQL Server, add `--logger "console;verbosity=normal"` and look for `[testcontainers.org] Start Docker container` lines. The reported test duration excludes container start-up (about 20 seconds of wall time).

## Writing tests (`CustomersApiTests.cs`)

- Group by endpoint with `// POST /api/customers` comments; name tests `Action_Condition_Expected`.
- Use `_client.PostAsJsonAsync`, `GetFromJsonAsync<T>`, `PutAsJsonAsync`, `DeleteAsync` and the `CreateCustomerAsync` helper.
- Cover success **and** every documented error status for each operation: 400, 404 and 409 as they apply.
- Assert on raw JSON when the wire format matters (see `GetById_ReturnsTimestampsAsUtc`); typed deserialisation hides things like a missing `Z`.
- Invalid payloads go in `InvalidCreateRequests` as anonymous objects, so you can send shapes the record types can't express.

## Gotchas

- These tests are the only ones that exercise model binding and validation; a passing unit suite doesn't prove an endpoint accepts requests.
- SQLite mode can pass where SQL Server fails (string lengths, collation, migrations). Run the default mode before merging schema or query changes.
- On Apple Silicon, SQL Server needs Rosetta (see the `sql-server` skill), or the container segfaults and the fixture fails in `InitializeAsync`.
