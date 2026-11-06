---
name: presentation-layer
description: WHEN working on HTTP-facing code (controllers, request/response contracts, validation, error mapping, Program.cs wiring) and deciding what belongs there.
---

# Presentation layer

**Directories:** `apps/api/src/CustomerService.Api/Controllers/`, `Contracts/`, `Errors/`, and `Program.cs`.
**Entrypoints:** `CustomersController` actions under `/api/customers`; `/health`; `/openapi/v1.json` in Development.

## Belongs here

- Routing, HTTP verbs, status codes, `[ProducesResponseType]`.
- Request/response records and their DataAnnotations (shape and format validation: required, length, email format).
- Mapping exceptions to ProblemDetails (`GlobalExceptionHandler`).
- DI registration and middleware order in `Program.cs`.

## Doesn't belong here

- Business rules (uniqueness, normalisation, timestamps): those are in `CustomerManager`.
- Any use of `CustomerDbContext`, `ICustomerRepository` or EF types.
- try/catch in actions: throw from the service and let the handler map it.
- Returning the `Customer` entity: always return a `Contracts` record.

## Boundary rules

- Controllers depend only on `ICustomerService` and `Contracts`.
- `Contracts` depend on nothing in the app (pure records), so the business layer can use them.
- `Errors/` exception types are shared: the business layer throws them, this layer maps them.

## Add or edit within the layer

- New action: see `endpoint`. Keep it to one service call and a result.
- New validation rule: attribute on the record's constructor parameter (never `[property: ...]`), plus an entry in `CustomersApiTests.InvalidCreateRequests` or a dedicated 400 test.
- New error status: exception type in `Errors/Exceptions.cs`, a `switch` arm in `GlobalExceptionHandler`, a row in `GlobalExceptionHandlerTests.KnownExceptions`.
- Changing a contract ripples to `CustomerManager` (mapping) and the integration tests, and is visible to API clients.

Tests: `apps/api/tests/CustomerService.UnitTests/Controllers/`, `Errors/`, `Contracts/`, and all of `CustomersApiTests`. See `aspnet-core` for framework details.
