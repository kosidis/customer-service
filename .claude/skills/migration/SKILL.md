---
name: migration
description: WHEN adding or changing an entity, column, index or constraint, which needs an EF Core migration against SQL Server, or when applying or rolling back migrations.
---

# Add or edit a model change and its migration

Migrations: `apps/api/src/CustomerService.Api/Data/Migrations/` (first one: `20261105113324_InitialCreate`). Tool: `dotnet-ef` 10.0.14 via `dotnet tool restore`. They're generated for SQL Server; SQLite test runs use `EnsureCreated` and skip migrations.

## Add (for example a nullable `CompanyName` column)

1. **Entity**: add the property to `Domain/Customer.cs`. New columns on an existing table should be nullable or have a default, or the migration fails on existing rows.
2. **Mapping** in `Data/CustomerDbContext.cs` `OnModelCreating`: `HasMaxLength` for strings, `IsRequired()` if needed, `HasIndex` for lookups. For a `DateTime`, add the UTC converter used by `CreatedAtUtc`.
3. **Generate**:
   ```bash
   dotnet ef migrations add AddCustomerCompanyName -p apps/api/src/CustomerService.Api -o Data/Migrations
   ```
   Read the generated `Up`/`Down` before going on. Check column types (`nvarchar(n)`, not `nvarchar(max)`), nullability and index names.
4. **Contracts and mapping**: add the field to the request/response records in `Contracts/CustomerContracts.cs`, set it in `CustomerManager.CreateAsync`/`UpdateAsync`, and add it to `ToResponse`.
5. **Tests**: repository test proving it persists; service tests for the mapping; integration test round-tripping it through the API.
6. **Apply and verify**:
   ```bash
   docker compose up -d sqlserver
   dotnet ef database update -p apps/api/src/CustomerService.Api
   dotnet ef migrations has-pending-model-changes -p apps/api/src/CustomerService.Api   # must say "No changes"
   dotnet test apps/api/tests/CustomerService.IntegrationTests                         # runs MigrateAsync on real SQL Server
   sh .claude/pre-pr-checks
   ```

## Edit

- **Migration not yet applied outside your machine:** roll back, remove, regenerate.
  ```bash
  dotnet ef database update <PreviousMigrationName> -p apps/api/src/CustomerService.Api
  dotnet ef migrations remove -p apps/api/src/CustomerService.Api
  # change the model, then `migrations add` again
  ```
- **Migration already applied elsewhere (merged or deployed): never edit it.** Add a new migration for the change.
- **Renames:** EF generates drop + add, which loses data. Edit the new migration to use `migrationBuilder.RenameColumn(...)` instead, and check `Down` mirrors it.
- **Tightening a column** (shorter length, non-null, new unique index): check existing data first with `sqlcmd` (see `sql-server`), and add a data fix-up with `migrationBuilder.Sql(...)` before the constraint if needed.
- Changing a mapped property ripples to `CustomerManager.ToResponse`, the contracts, the strict mocks in unit tests and the integration tests. Find uses with `tokensave_search` (`literal: true`) on the property name.

## Roll back

```bash
dotnet ef database update <TargetMigrationName> -p apps/api/src/CustomerService.Api   # `0` reverts everything
dotnet ef migrations script <From> <To> -p apps/api/src/CustomerService.Api --idempotent   # SQL for a DBA-run deploy
```
