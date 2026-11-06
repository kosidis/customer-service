---
name: endpoint
description: WHEN adding a new API endpoint or changing an existing one's route, request, response, rules or status codes, end to end through all three layers.
---

# Add or edit an endpoint

An endpoint threads through all three layers. Layer rules: `presentation-layer`, `business-layer`, `data-layer`. Write tests first.

## Add

Worked shape: the five existing actions in `CustomersController`. Example: `PATCH /api/customers/{id}/email`.

1. **Contract** in `apps/api/src/CustomerService.Api/Contracts/CustomerContracts.cs`: a positional record with attributes on constructor parameters, for example `public record ChangeEmailRequest([Required, EmailAddress, StringLength(256)] string Email);`. Reuse `CustomerResponse` for the output where possible.
2. **Service interface**: add the method to `Services/ICustomerService.cs`, taking a `CancellationToken`.
3. **Service tests** in `apps/api/tests/CustomerService.UnitTests/Services/CustomerManagerTests.cs`: success, not found, and any conflict or rule. They fail to compile until step 4.
4. **Service implementation** in `Services/CustomerManager.cs`: reuse `FindOrThrowAsync`, `EnsureEmailIsUniqueAsync`, `NormaliseEmail`, `ToResponse`; stamp `UpdatedAtUtc` from `timeProvider`. Throw `NotFoundException`/`ConflictException` for errors.
5. **Repository**, only if you need new data access: add to `Data/ICustomerRepository.cs` and `Data/CustomerRepository.cs`, with a test in `Data/CustomerRepositoryTests.cs`. Updates to a tracked entity reuse `UpdateAsync`.
6. **Controller action** in `Controllers/CustomersController.cs`: route attribute with `{id:guid}`, `[ProducesResponseType]` for every outcome, one call to the service, return `Ok`/`NoContent`/`CreatedAtRoute`. A test in `Controllers/CustomersControllerTests.cs`.
7. **Integration tests** in `apps/api/tests/CustomerService.IntegrationTests/CustomersApiTests.cs` under a `// PATCH /api/customers/{id}/email` comment: success plus every error status (400, 404, 409).
8. **Docs**: the API table in `README.md`.
9. Verify: `sh .claude/pre-pr-checks`. If the change touches queries, also run `dotnet test apps/api/tests/CustomerService.IntegrationTests` against SQL Server.

## Edit

1. Find every caller first: `tokensave_callers` on the service method, plus `tokensave_search` with `literal: true` for the route string in tests.
2. **Changing a request or response record is a breaking API change.** Prefer adding an optional (nullable) member. Renaming or removing one breaks clients: call it out in the PR.
3. Changing a signature on `ICustomerService` or `ICustomerRepository` means updating the implementation, the strict Moq setups in `CustomerManagerTests` / `CustomersControllerTests` (they fail with `MockException` otherwise), and the integration tests.
4. Changing a status code: update `[ProducesResponseType]`, the controller test and the integration test together.
5. Changing a rule (for example email normalisation) can affect stored data. Check whether existing rows still satisfy it and whether a data migration is needed (see `migration`).
6. Verify: `sh .claude/pre-pr-checks`.
