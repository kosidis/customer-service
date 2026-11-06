---
name: test
description: WHEN running the test suites, checking coverage, or deciding which tests a change needs in this repo.
---

# Test

| Suite | Command | Needs |
|---|---|---|
| Unit + coverage gate | `dotnet test apps/api/tests/CustomerService.UnitTests /p:CollectCoverage=true` | nothing |
| Integration, SQL Server | `dotnet test apps/api/tests/CustomerService.IntegrationTests` | Docker or Podman (`DOCKER_HOST` for Podman) |
| Integration, SQLite | `INTEGRATION_TEST_DB=sqlite dotnet test apps/api/tests/CustomerService.IntegrationTests` | nothing |
| Everything the PR gate runs | `sh .claude/pre-pr-checks` | nothing |
| Mutation tests (unit suite) | `cd apps/api/tests/CustomerService.UnitTests && dotnet stryker` | nothing; never from `apps/api` (see the `mutation` skill) |

Filter: `--filter "FullyQualifiedName~<ClassOrMethod>"`.

## What a change needs

- **Service rule:** a case in `Services/CustomerManagerTests.cs` for each new branch, including the error path.
- **Query or persistence:** `Data/CustomerRepositoryTests.cs` on SQLite, then the SQL Server integration run.
- **Endpoint or contract:** a controller test in `Controllers/CustomersControllerTests.cs` and integration tests in `CustomersApiTests.cs` for the success status and every error status the endpoint documents. Only the integration tests exercise model binding and validation.
- **New error type:** a row in `GlobalExceptionHandlerTests.KnownExceptions`.

Write the test first (superpowers `test-driven-development`), watch it fail, then implement.

Details: `xunit-moq` (unit), `testcontainers` (integration), `coverlet` (gate).
