---
name: business-layer
description: WHEN adding or changing business rules, service methods or the domain entity, and deciding what logic belongs in the service layer.
---

# Business layer

**Directories:** `src/CustomerService.Api/Services/` (`ICustomerService`, `CustomerManager`) and `Domain/` (`Customer`).
**Entrypoint:** `ICustomerService`, injected into `CustomersController`.

## Belongs here

- Rules: email trimmed and lower-cased (`NormaliseEmail`), names trimmed, blank phone stored as `null` (`NormalisePhone`), email uniqueness (`EnsureEmailIsUniqueAsync`), existence checks (`FindOrThrowAsync`).
- Assigning ids (`Guid.NewGuid()`) and timestamps from the injected `TimeProvider`.
- Mapping entities to response records (`ToResponse`).
- Throwing `NotFoundException` / `ConflictException`.

## Doesn't belong here

- HTTP concepts: status codes, `IActionResult`, `HttpContext`.
- EF Core: no `CustomerDbContext`, `DbSet`, `IQueryable` or LINQ-to-database. Ask the repository.
- `DateTime.UtcNow`: always `timeProvider.GetUtcNow().UtcDateTime`.
- Format validation already done by contract attributes (required, length). Rules that need data (uniqueness) or cross-field logic go here.

## Boundary rules

- Depends on `ICustomerRepository` (never `CustomerRepository` directly), `Contracts`, `Domain` and `Errors`.
- Is depended on only by the presentation layer, through `ICustomerService`.
- `Customer` stays a plain class with no framework attributes; persistence mapping lives in `CustomerDbContext`.

## Add or edit within the layer

1. Add the method to `ICustomerService`, then implement it in `CustomerManager` with a primary-constructor dependency if it needs a new one (register that in `Program.cs`).
2. Tests in `tests/CustomerService.UnitTests/Services/CustomerManagerTests.cs` with strict `Mock<ICustomerRepository>` and `FixedTimeProvider`: one test per branch, including errors, plus `Verify(..., Times.Never)` that nothing persists on failure.
3. Changing a rule: check the integration tests that assert on normalised output (for example `Create_ValidRequest_Returns201WithLocationAndBody`) and whether stored data needs a migration.
4. Changing the `ICustomerService` signature ripples to `CustomersController` and `CustomersControllerTests`.

Cross-layer recipe: `endpoint`.
