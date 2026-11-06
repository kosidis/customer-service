---
name: dev-setup
description: WHEN setting up a fresh clone of this repo, or a new machine, to build, test and run the service.
---

# From zero to running

## 1. Prerequisites

- .NET SDK 10.0.x (`dotnet --list-sdks`; built with 10.0.403).
- Node 24 and npm for the web app (`node --version`).
- A container runtime: Docker Desktop, or Podman 5+ with `podman-compose` (`brew install podman podman-compose`).
- Apple Silicon only: Rosetta (`softwareupdate --install-rosetta`), and for Podman:
  ```bash
  podman machine init            # if no machine exists
  podman machine stop
  podman machine set --memory 6144
  printf '[machine]\nrosetta = true\n' >> ~/.config/containers/containers.conf
  podman machine start
  ```
  Without Rosetta, SQL Server segfaults on start.

## 2. Dependencies

```bash
dotnet tool restore            # dotnet-ef 10.0.14
dotnet restore apps/api
cd apps/web && npm ci        # web app dependencies from the lockfile
```

## 3. Configuration

No secrets are needed for local work. The repo reads:

| Setting | Default | Where set |
|---|---|---|
| `ConnectionStrings__CustomerDb` | `localhost,1433`, `sa` / `Your_strong_Passw0rd` | `apps/api/src/CustomerService.Api/appsettings.json`; Compose overrides it |
| `Database__ApplyMigrationsOnStartup` | `false` | `appsettings.json`; Compose sets `true` |
| `MSSQL_SA_PASSWORD` | `Your_strong_Passw0rd` | shell or a gitignored `.env` next to `docker-compose.yml` |
| `API_BASE_URL` | none (required) | web app only; the API's base URL, e.g. `http://localhost:8080`. Compose sets `http://api:8080`; Kubernetes sets it from the chart's `apiBaseUrl` |
| `INTEGRATION_TEST_DB` | unset (SQL Server) | set to `sqlite` to run integration tests without containers |
| `DOCKER_HOST` | unset | Podman users: `unix://$(podman machine inspect --format '{{.ConnectionInfo.PodmanSocket.Path}}')` |

If you change `MSSQL_SA_PASSWORD`, change the connection string to match.

## 4. Database

```bash
docker compose up -d sqlserver          # or podman compose up -d sqlserver
dotnet ef database update -p apps/api/src/CustomerService.Api
```

There's no seed data; create customers through the API or the web UI.

## 4b. Run both apps

```bash
dotnet run --project apps/api/src/CustomerService.Api           # terminal 1: http://localhost:5062
cd apps/web && API_BASE_URL=http://localhost:5062 npm run dev    # terminal 2: http://localhost:3000
```

Or run everything in containers: `docker compose up --build -d` gives the API on :8080 and the UI on :3000.

## 5. Verify

```bash
sh .claude/pre-pr-checks                # API build, migrations check, unit + integration (SQLite); web lint, typecheck, tests, build; Helm
dotnet test apps/api/tests/CustomerService.IntegrationTests   # against real SQL Server
docker compose up --build -d && curl -s http://localhost:8080/health && curl -s http://localhost:3000/api/health
```

## 6. Git

Commits must be signed (hook-enforced). Set up SSH signing once:

```bash
git config user.name "<your name>"
git config user.email "<your email>"
git config gpg.format ssh
git config user.signingkey ~/.ssh/<key>.pub
git config commit.gpgsign true
```

Register the same key on GitHub as a signing key.
