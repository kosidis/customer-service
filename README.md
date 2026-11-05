# Customer service

A REST API for creating, reading, updating and deleting customers. It runs on .NET 10 and ASP.NET Core, and stores data in SQL Server through EF Core 10.

## API

| Method | Route | Success | Errors |
|---|---|---|---|
| `GET` | `/api/customers?page=1&pageSize=20&search=<text>` | 200, paged list | 400 |
| `GET` | `/api/customers/{id}` | 200 | 404 |
| `POST` | `/api/customers` | 201 with `Location` header | 400, 409 |
| `PUT` | `/api/customers/{id}` | 200 | 400, 404, 409 |
| `DELETE` | `/api/customers/{id}` | 204 | 404 |
| `GET` | `/health` | 200 `Healthy` | 503 |

Errors use [RFC 7807 problem details](https://www.rfc-editor.org/rfc/rfc7807). Email addresses are trimmed, stored in lower case and must be unique, so a duplicate returns 409. `pageSize` accepts 1 to 100, and `search` matches first name, last name or email. In the `Development` environment the OpenAPI document is served at `/openapi/v1.json`.

Request body for `POST` and `PUT`:

```json
{
  "firstName": "Jane",
  "lastName": "Doe",
  "email": "jane.doe@example.com",
  "phoneNumber": "+44 20 7946 0000"
}
```

## Project layout

```
src/CustomerService.Api/
  Controllers/   HTTP endpoints
  Services/      business rules (CustomerManager)
  Data/          DbContext, repository and EF migrations
  Contracts/     request and response models with validation
  Domain/        the Customer entity
  Errors/        exception types and the global exception handler
tests/CustomerService.UnitTests/         xUnit and Moq, coverage gate at 80% line
tests/CustomerService.IntegrationTests/  WebApplicationFactory against a real database
```

## Run with Docker

You need Docker with Compose. From the repository root:

```bash
docker compose up --build
```

This starts SQL Server 2022 and the API on `http://localhost:8080`. The API applies EF migrations at start-up. To change the SA password, set `MSSQL_SA_PASSWORD` in a `.env` file next to `docker-compose.yml`; it must meet SQL Server's complexity rules.

### Podman

The same files work with Podman. `podman compose` needs a provider such as `podman-compose` (`brew install podman-compose`). On Apple Silicon, SQL Server has no arm64 image and crashes under QEMU emulation, so the Podman machine needs Rosetta and at least 4 GiB of memory:

```bash
podman machine stop
podman machine set --memory 6144
printf '[machine]\nrosetta = true\n' >> ~/.config/containers/containers.conf
podman machine start
podman compose up -d --build
```

`podman-compose` rebuilds the image but keeps a running container, so after a code change run `podman compose up -d --build --force-recreate api`.

Try it:

```bash
curl -i -X POST http://localhost:8080/api/customers \
  -H "Content-Type: application/json" \
  -d '{"firstName":"Jane","lastName":"Doe","email":"jane@example.com"}'
```

## Run locally

Start SQL Server (for example `docker compose up sqlserver`), then:

```bash
dotnet tool restore
dotnet ef database update -p src/CustomerService.Api
dotnet run --project src/CustomerService.Api
```

The connection string lives in `ConnectionStrings:CustomerDb` in `appsettings.json`. Override it with the `ConnectionStrings__CustomerDb` environment variable. Set `Database__ApplyMigrationsOnStartup=true` to migrate at start-up instead of running `dotnet ef`.

## Tests

Unit tests with the coverage gate (the run fails below 80% line coverage):

```bash
dotnet test tests/CustomerService.UnitTests /p:CollectCoverage=true
```

The Cobertura report goes to `tests/CustomerService.UnitTests/TestResults/coverage/`.

Integration tests start SQL Server 2022 in Docker through Testcontainers, apply the migrations and call every endpoint over HTTP:

```bash
dotnet test tests/CustomerService.IntegrationTests
```

With Podman, point Testcontainers at the Podman socket first:

```bash
export DOCKER_HOST="unix://$(podman machine inspect --format '{{.ConnectionInfo.PodmanSocket.Path}}')"
dotnet test tests/CustomerService.IntegrationTests
```

Without a container runtime, run the same suite against SQLite in-memory:

```bash
INTEGRATION_TEST_DB=sqlite dotnet test tests/CustomerService.IntegrationTests
```

## Adding a migration

```bash
dotnet ef migrations add <MigrationName> -p src/CustomerService.Api -o Data/Migrations
```

## Notes

`System.Security.Cryptography.Pkcs` is pinned to 10.0.14 in the API project because `Microsoft.Data.SqlClient` 6.1.7 brings in 9.0.11, which has known vulnerabilities ([GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7), [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx)). Remove the pin once SqlClient depends on a patched version.
