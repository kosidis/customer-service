---
name: ef-core
description: WHEN writing queries, changing the EF Core model or DbContext configuration, or debugging persistence behaviour in this repo.
---

# EF Core 10 in this repo

Packages: `Microsoft.EntityFrameworkCore.SqlServer` and `.Design` 10.0.14 in the API; `.Sqlite` 10.0.14 in both test projects.

## Where things live

- `apps/api/src/CustomerService.Api/Data/CustomerDbContext.cs`: one `DbSet<Customer>`, all mapping in `OnModelCreating` with the fluent API. No data annotations on entities.
- `apps/api/src/CustomerService.Api/Data/CustomerRepository.cs`: the only class that queries the context.
- `apps/api/src/CustomerService.Api/Data/Migrations/`: SQL Server migrations (see the `migration` skill).

## Registration (`Program.cs`)

```csharp
builder.Services.AddDbContext<CustomerDbContext>((services, options) =>
    options.UseSqlServer(
        services.GetRequiredService<IConfiguration>().GetConnectionString("CustomerDb"),
        sql => sql.EnableRetryOnFailure()));
```

- Scoped lifetime (one context per request).
- `EnableRetryOnFailure()` is on. If you ever need an explicit transaction, wrap it in `dbContext.Database.CreateExecutionStrategy().ExecuteAsync(...)`, or EF throws.

## Model conventions

- Explicit `HasMaxLength` on every string; `IsRequired()` on non-nullable ones.
- `Email` has a unique index (`IX_Customers_Email`). The service checks first and returns 409; the index is the safety net for races.
- `DateTime` columns get a converter that marks values UTC on read:
  `.HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))`. Add it to every new `DateTime` property, or the API emits timestamps without `Z`. Converters don't change the schema, so no migration is needed for this alone.
- Keys are client-generated `Guid`s (`Guid.NewGuid()` in `CustomerManager`).

## Query conventions

- Read-only lists use `AsNoTracking()`. Single-entity reads are tracked because updates rely on change tracking: `UpdateAsync` only calls `SaveChangesAsync`.
- Paged lists always order deterministically (`LastName`, `FirstName`, then `Id`) before `Skip`/`Take`.
- Search uses `string.Contains`, which translates to `LIKE` and follows the database collation (case-insensitive on SQL Server's default, case-sensitive on SQLite for non-ASCII).
- Pass the `CancellationToken` to every async EF call.

## Testing against EF

- Repository unit tests use SQLite in-memory (`DataSource=:memory:` with the connection held open) and `EnsureCreated()`; never the EF in-memory provider, which ignores unique indexes.
- Use a second context (`CreateContext()`) to verify what was persisted, so you're not reading the first context's tracked state.
- Integration tests run real migrations on SQL Server, or `EnsureCreated` on SQLite.

## Gotchas

- SQLite is lenient on types and won't catch SQL Server-only failures (length overflow, `datetime2` precision). Run the Testcontainers suite after model changes.
- After any model change, `dotnet ef migrations has-pending-model-changes -p apps/api/src/CustomerService.Api` must say "No changes".
