# Customer service

A monorepo for managing customers: a REST API and a web UI.

- `apps/api`: the REST API. It runs on .NET 10 and ASP.NET Core, and stores data in SQL Server through EF Core 10.
- `apps/web`: a Next.js 16 web app to list, search, add, edit and delete customers. It calls the API from the server.

## API

| Method | Route | Success | Errors |
|---|---|---|---|
| `GET` | `/api/customers?page=1&pageSize=20&search=<text>` | 200, paged list | 400 |
| `GET` | `/api/customers/{id}` | 200 | 404 |
| `POST` | `/api/customers` | 201 with `Location` header | 400, 409 |
| `PUT` | `/api/customers/{id}` | 200 | 400, 404, 409 |
| `DELETE` | `/api/customers/{id}` | 204 | 404 |
| `GET` | `/health` | 200 `Healthy` (all checks) | 503 |
| `GET` | `/health/live` | 200 `Healthy` (process only, for liveness probes) | none |
| `GET` | `/health/ready` | 200 `Healthy` (includes the database, for readiness probes) | 503 |

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
docker-compose.yml   SQL Server, the API and the web app
apps/api/
  Dockerfile
  src/CustomerService.Api/
    Controllers/   HTTP endpoints
    Services/      business rules (CustomerManager)
    Data/          DbContext, repository and EF migrations
    Contracts/     request and response models with validation
    Domain/        the Customer entity
    Errors/        exception types and the global exception handler
  tests/CustomerService.UnitTests/         xUnit and Moq, coverage gate at 80% line
  tests/CustomerService.IntegrationTests/  WebApplicationFactory against a real database
  helm/customer-service/                   Helm chart, with values for dev, staging and prod
apps/web/
  Dockerfile
  src/app/         App Router pages, server actions (customers/actions.ts), /api/health
  src/components/  form, table, pagination, search and delete components
  src/lib/api/     the only code that calls the API (server-only)
  src/__tests__/   Jest and React Testing Library, coverage gate at 80%
  helm/customer-service-web/               Helm chart, with values for dev, staging and prod
```

## Web app

The web app in `apps/web` lists customers with search and pagination, and has forms to add and edit a customer and a button to delete one. There's no login. The browser only talks to the web app: pages, server actions and the API client (`src/lib/api`) run on the server and call the API, so validation errors and conflicts come from the API's rules. `GET /api/health` returns `200 Healthy` without calling the API.

You need Node 24. Start the API first (for example `docker compose up -d sqlserver api`), then:

```bash
cd apps/web
npm ci
API_BASE_URL=http://localhost:8080 npm run dev     # http://localhost:3000
```

| Variable | Required | Description |
|---|---|---|
| `API_BASE_URL` | yes | Base URL of the API, for example `http://localhost:8080`. Read at request time, so one image works in every environment. |

Checks, all from `apps/web`:

```bash
npm run lint
npm run typecheck
npm test
npm run test:coverage    # fails below 80% lines, branches, functions and statements
npm run build
```

More in `apps/web/README.md`.

## Run with Docker

You need Docker with Compose. From the repository root:

```bash
docker compose up --build
```

This starts SQL Server 2022, the API on `http://localhost:8080` and the web app on `http://localhost:3000`. The web app reaches the API at `http://api:8080` (`API_BASE_URL`). The API applies EF migrations at start-up. To change the SA password, set `MSSQL_SA_PASSWORD` in a `.env` file next to `docker-compose.yml`; it must meet SQL Server's complexity rules.

### Podman

The same files work with Podman. `podman compose` needs a provider such as `podman-compose` (`brew install podman-compose`). On Apple Silicon, SQL Server has no arm64 image and crashes under QEMU emulation, so the Podman machine needs Rosetta and at least 4 GiB of memory:

```bash
podman machine stop
podman machine set --memory 6144
printf '[machine]\nrosetta = true\n' >> ~/.config/containers/containers.conf
podman machine start
podman compose up -d --build
```

`podman-compose` rebuilds the image but keeps a running container, so after a code change run `podman compose up -d --build --force-recreate api web` (name only the service you changed). Open `http://localhost:3000` for the UI.

Try it:

```bash
curl -i -X POST http://localhost:8080/api/customers \
  -H "Content-Type: application/json" \
  -d '{"firstName":"Jane","lastName":"Doe","email":"jane@example.com"}'
```

## Deploy to Kubernetes with Helm

Each app has its own chart: `apps/api/helm/customer-service` for the API and `apps/web/helm/customer-service-web` for the web app. The sections below cover the API chart, then the web chart. The API chart creates a Deployment, a `ClusterIP` Service and a ServiceAccount, plus an Ingress when `ingress.enabled` is true. Prod also gets a HorizontalPodAutoscaler and a PodDisruptionBudget.

| | dev | staging | prod |
|---|---|---|---|
| Values file | `values-dev.yaml` | `values-staging.yaml` | `values-prod.yaml` |
| Replicas | 1 | 2 | HPA, 3 to 10 at 70% CPU |
| CPU request / limit | 50m / 250m | 100m / 500m | 250m / 1000m |
| Memory request / limit | 128Mi / 256Mi | 192Mi / 384Mi | 256Mi / 512Mi |
| Ingress | off | off | off |

The API is reached only inside the cluster through its `ClusterIP` Service (port 80); the web chart's ingress owns the public host. Turn the API ingress on with `--set ingress.enabled=true` only if you need to call the API from outside.

1. Push the image to your registry:

    ```bash
    docker build -t <registry>/customer-service-api:<tag> apps/api
    docker push <registry>/customer-service-api:<tag>
    ```

2. Create the Secret with the SQL Server connection string. The chart never stores credentials:

    ```bash
    kubectl create secret generic customer-service-db -n <namespace> \
      --from-literal=connection-string='Server=<host>,1433;Database=CustomerDb;User Id=<user>;Password=<password>;TrustServerCertificate=False'
    ```

3. Install or upgrade, choosing the environment's values file:

    ```bash
    helm upgrade --install customer-service apps/api/helm/customer-service -n <namespace> \
      -f apps/api/helm/customer-service/values-<env>.yaml \
      --set image.repository=<registry>/customer-service-api --set image.tag=<tag>
    ```

The ingress hosts, TLS Secret names and the `nginx` ingress class in the values files are placeholders; set your own.

The pods run migrations at start-up (`database.applyMigrationsOnStartup`). Several replicas can start at once: EF Core serialises migrations with a lock, and the app retries while another replica is creating the database. Probes use `/health/live` for startup and liveness, and `/health/ready` for readiness, so a database outage takes pods out of the Service without restarting them. `values.schema.json` rejects a values file without cpu and memory requests and limits, or without a database Secret.

Check a change before deploying:

```bash
helm lint apps/api/helm/customer-service -f apps/api/helm/customer-service/values-<env>.yaml
helm template customer-service apps/api/helm/customer-service -f apps/api/helm/customer-service/values-<env>.yaml
```

### Web chart

The web chart creates a Deployment, a `ClusterIP` Service (port 80 to container port 3000) and a ServiceAccount, plus an Ingress when `ingress.enabled` is true. Prod also gets a HorizontalPodAutoscaler and a PodDisruptionBudget. It has no Secret: the app holds no credentials.

| | dev | staging | prod |
|---|---|---|---|
| Values file | `values-dev.yaml` | `values-staging.yaml` | `values-prod.yaml` |
| Replicas | 1 | 2 | HPA, 3 to 10 at 70% CPU |
| CPU request / limit | 50m / 250m | 100m / 500m | 250m / 1000m |
| Memory request / limit | 128Mi / 256Mi | 192Mi / 384Mi | 256Mi / 512Mi |
| Ingress | off | on | on |

The browser only talks to the web app, so the web ingress is the public entry point. The web pods call the API at `apiBaseUrl` (the `API_BASE_URL` env var), which defaults to `http://customer-service`: the API chart's Service in the same namespace. Set `--set apiBaseUrl=...` if you named the API release differently or installed it elsewhere. All three probes use `/api/health`, which never calls the API, so an API outage doesn't restart or unready the web pods.

Install the API first, then the web app:

```bash
docker build -t <registry>/customer-service-web:<tag> apps/web
docker push <registry>/customer-service-web:<tag>

helm upgrade --install customer-service-web apps/web/helm/customer-service-web -n <namespace> \
  -f apps/web/helm/customer-service-web/values-<env>.yaml \
  --set image.repository=<registry>/customer-service-web --set image.tag=<tag>
```

In dev the ingress is off; use `kubectl port-forward svc/customer-service-web 3000:80 -n <namespace>`. Check the chart as for the API:

```bash
helm lint apps/web/helm/customer-service-web -f apps/web/helm/customer-service-web/values-<env>.yaml
helm template customer-service-web apps/web/helm/customer-service-web -f apps/web/helm/customer-service-web/values-<env>.yaml
```

## Run locally

Start SQL Server (for example `docker compose up sqlserver`), then:

```bash
dotnet tool restore
dotnet ef database update -p apps/api/src/CustomerService.Api
dotnet run --project apps/api/src/CustomerService.Api
```

The connection string lives in `ConnectionStrings:CustomerDb` in `appsettings.json`. Override it with the `ConnectionStrings__CustomerDb` environment variable. Set `Database__ApplyMigrationsOnStartup=true` to migrate at start-up instead of running `dotnet ef`.

To run the UI against this API, see [Web app](#web-app) and use `API_BASE_URL=http://localhost:5062` (the `dotnet run` port) or `http://localhost:8080` (Compose).

## Tests

The web app's tests are under [Web app](#web-app). `sh .claude/pre-pr-checks` runs both suites, the web build and the Helm checks for both charts.

API unit tests with the coverage gate (the run fails below 80% line coverage):

```bash
dotnet test apps/api/tests/CustomerService.UnitTests /p:CollectCoverage=true
```

The Cobertura report goes to `apps/api/tests/CustomerService.UnitTests/TestResults/coverage/`.

Integration tests start SQL Server 2022 in Docker through Testcontainers, apply the migrations and call every endpoint over HTTP:

```bash
dotnet test apps/api/tests/CustomerService.IntegrationTests
```

With Podman, point Testcontainers at the Podman socket first:

```bash
export DOCKER_HOST="unix://$(podman machine inspect --format '{{.ConnectionInfo.PodmanSocket.Path}}')"
dotnet test apps/api/tests/CustomerService.IntegrationTests
```

Without a container runtime, run the same suite against SQLite in-memory:

```bash
INTEGRATION_TEST_DB=sqlite dotnet test apps/api/tests/CustomerService.IntegrationTests
```

Mutation tests with [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) check that the unit tests would catch real bugs, not only that they run the code. Stryker makes small changes to the API code and reports each one no test catches:

```bash
dotnet tool restore
cd apps/api/tests/CustomerService.UnitTests
dotnet stryker
```

Run it from the unit test folder, not `apps/api`: there, Stryker picks up the solution and runs the integration tests too. Settings are in `apps/api/tests/CustomerService.UnitTests/stryker-config.json`. The HTML report goes to `apps/api/tests/CustomerService.UnitTests/StrykerOutput/<timestamp>/reports/mutation-report.html`.

## Adding a migration

```bash
dotnet ef migrations add <MigrationName> -p apps/api/src/CustomerService.Api -o Data/Migrations
```

## Notes

`System.Security.Cryptography.Pkcs` is pinned to 10.0.14 in the API project because `Microsoft.Data.SqlClient` 6.1.7 brings in 9.0.11, which has known vulnerabilities ([GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7), [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx)). Remove the pin once SqlClient depends on a patched version.
