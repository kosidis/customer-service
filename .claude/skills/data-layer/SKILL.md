---
name: data-layer
description: WHEN adding or changing repository methods, queries, the DbContext mapping or migrations, and deciding what belongs in the data layer.
---

# Data layer

**Directory:** `src/CustomerService.Api/Data/`: `ICustomerRepository`, `CustomerRepository`, `CustomerDbContext`, `Migrations/`.
**Entrypoint:** `ICustomerRepository`, injected into `CustomerManager`.

## Belongs here

- All EF Core usage: queries, filtering, ordering, paging, `SaveChangesAsync`.
- Mapping configuration in `CustomerDbContext.OnModelCreating` (lengths, indexes, converters).
- Migrations.

## Doesn't belong here

- Business rules: normalising, deciding conflicts, throwing `NotFoundException`. Return `null`/`bool` and let the service decide. `GetByIdAsync` returns `Customer?`; `EmailExistsAsync` returns `bool`.
- Response mapping or contracts: the repository speaks `Domain.Customer` only (`ListAsync` takes plain `page`, `pageSize`, `search` arguments, not `CustomerQuery`).
- Leaking `IQueryable` out of the repository.

## Boundary rules

- Depends on EF Core and `Domain` only.
- Used only by the business layer, through `ICustomerRepository`. Controllers never touch it.

## Add or edit within the layer

1. Add the method to `ICustomerRepository`, implement it in `CustomerRepository`. Use `AsNoTracking()` for read-only lists; keep single-entity reads tracked when the caller will update them; order deterministically before `Skip`/`Take`; pass the `CancellationToken`.
2. Test in `tests/CustomerService.UnitTests/Data/CustomerRepositoryTests.cs` on SQLite in-memory, verifying through a second context.
3. Run `dotnet test tests/CustomerService.IntegrationTests` against SQL Server: SQLite translates some LINQ differently (collation, string functions).
4. Changing a signature ripples to `CustomerManager` and its strict mock setups in `CustomerManagerTests`.
5. Model changes: follow `migration`.

Framework detail: `ef-core`. Database operations: `sql-server`.
