---
name: xunit-moq
description: WHEN writing or changing unit tests in apps/api/tests/CustomerService.UnitTests (xUnit 2 with Moq and SQLite in-memory).
---

# xUnit and Moq in this repo

`apps/api/tests/CustomerService.UnitTests` uses xUnit 2.9.3, Moq 4.21.0 and `Microsoft.EntityFrameworkCore.Sqlite`. `Xunit` is a global using in the csproj. Assertions are plain `Assert.*`; there's no FluentAssertions.

## Layout

Test folders mirror the API: `Services/CustomerManagerTests.cs`, `Data/CustomerRepositoryTests.cs`, `Controllers/CustomersControllerTests.cs`, `Errors/GlobalExceptionHandlerTests.cs`, `Contracts/PagedResponseTests.cs`. A new class in `apps/api/src/CustomerService.Api/<Folder>/` gets `apps/api/tests/CustomerService.UnitTests/<Folder>/<Class>Tests.cs`.

## Conventions

- Name tests `Method_Condition_Expected`, for example `UpdateAsync_EmailTakenByAnotherCustomer_ThrowsConflict`.
- The subject is `_sut`, built in the constructor.
- Mocks are `new Mock<T>(MockBehavior.Strict)`: every call must be set up, so a new repository call in the service fails the existing tests until you add a setup. That's intended.
- Capture arguments with `.Callback<T, CancellationToken>((x, _) => saved = x)`.
- Assert side effects with `Verify(..., Times.Once)` and absence with `Times.Never`.
- Time: inject `FixedTimeProvider` (a private `TimeProvider` subclass in `CustomerManagerTests`) and assert against its `Now`.
- Data-driven cases: `[Theory]` with `[InlineData]`, or `[MemberData]` returning `TheoryData<...>`.
- Controller tests assert result types (`OkObjectResult`, `CreatedAtRouteResult`, `NoContentResult`) and `Assert.Same` on the value.

## Repository tests on SQLite

`CustomerRepositoryTests` opens a `SqliteConnection("DataSource=:memory:")`, keeps it open for the test, calls `EnsureCreated()`, and implements `IDisposable` to close it. Verify persistence through a second context from `CreateContext()`. Use this pattern, not `UseInMemoryDatabase`, so unique indexes and SQL translation are real.

## Run

```bash
dotnet test apps/api/tests/CustomerService.UnitTests
dotnet test apps/api/tests/CustomerService.UnitTests --filter "FullyQualifiedName~CustomerManagerTests"
dotnet test apps/api/tests/CustomerService.UnitTests /p:CollectCoverage=true   # with the 80% gate, see the coverlet skill
```
