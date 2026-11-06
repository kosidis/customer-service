---
name: aspnet-core
description: WHEN adding or changing controllers, request validation, error responses, middleware, OpenAPI or health checks in this ASP.NET Core 10 API.
---

# ASP.NET Core in this repo

ASP.NET Core 10 with MVC controllers (not minimal APIs). All wiring is in `src/CustomerService.Api/Program.cs`.

## Wiring in `Program.cs`

- `AddControllers()` + `MapControllers()`: attribute-routed controllers.
- `AddProblemDetails()` + `AddExceptionHandler<GlobalExceptionHandler>()` + `UseExceptionHandler()`: every error is RFC 7807 `application/problem+json`.
- `UseStatusCodePages()`: bare 404s from routing (for example `/api/customers/not-a-guid`) also get a body.
- `AddOpenApi()` + `MapOpenApi()` in `Development` only: document at `/openapi/v1.json`.
- `AddHealthChecks().AddDbContextCheck<CustomerDbContext>("database", tags: [ReadinessTag])`, mapped three ways: `/health` (all checks), `/health/live` (`Predicate = _ => false`, for liveness probes) and `/health/ready` (checks tagged `ready`, for readiness probes). Tag new dependency checks `ready`; liveness must never depend on the database. `HealthProbeTests` proves live stays 200 while ready returns 503 with the database down.
- Everything after `Build()` runs inside a `try` that logs at `Critical` and returns exit code 1. As PID 1 in a container an unhandled exception hangs instead of exiting, so keep start-up work inside it. `HostAbortedException` is let through because `dotnet ef` and `WebApplicationFactory` use it to stop the host. `await using var app` makes disposal flush the console logger, or the critical log is lost.
- `MigrateDatabaseAsync` (in the `partial class Program`) retries SQL error 1801 with a short backoff, because replicas starting together race to create the database.
- `public partial class Program` at the end exists so `WebApplicationFactory<Program>` can see it. Keep it.

## Controller conventions (`Controllers/CustomersController.cs`)

- `[ApiController]`, `[Route("api/customers")]`, `[Produces("application/json")]`, primary-constructor injection of `ICustomerService`.
- Route constraints on ids: `{id:guid}`. A non-GUID never reaches the action and returns 404.
- Every action takes a `CancellationToken` and passes it down.
- Declare outcomes with `[ProducesResponseType<T>(StatusCodes.StatusXxx)]` so OpenAPI is accurate.
- Create returns `CreatedAtRoute(nameof(GetById), new { id }, body)`; the GET has `Name = nameof(GetById)`.
- Actions contain no logic and no try/catch. Errors are exceptions from the service.

## Validation

- `[ApiController]` returns a 400 `ValidationProblemDetails` automatically when the model is invalid.
- Positional records (`CreateCustomerRequest`, `UpdateCustomerRequest`): put attributes on the constructor parameter, `[Required, StringLength(100)] string FirstName`. **Never** `[property: Required]`: MVC throws `InvalidOperationException` at request time ("validation metadata defined on property ... will be ignored"), which surfaces as a 500. Unit tests don't run model binding, so only `CustomersApiTests` catches this.
- Query models (`CustomerQuery`) are classes/records with init properties, bound with `[FromQuery]`, validated with `[Range]`/`[StringLength]`.
- Non-nullable `string` parameters are treated as required by MVC because `<Nullable>enable</Nullable>` is on.

## Errors (`Errors/GlobalExceptionHandler.cs`)

- `NotFoundException` → 404, `ConflictException` → 409, anything else → 500 with `Detail = null` and an `Error` log.
- To add a status: add an exception in `Errors/Exceptions.cs`, add a `switch` arm, add a case to `GlobalExceptionHandlerTests.KnownExceptions`.

## Gotchas

- The connection string is read lazily inside `AddDbContext((services, options) => ...)` so test hosts can override it with `UseSetting`. Don't read `builder.Configuration` eagerly there.
- No HTTPS redirection: the container listens on plain HTTP 8080 and TLS belongs at the ingress.
