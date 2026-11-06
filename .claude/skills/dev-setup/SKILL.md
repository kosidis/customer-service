---
name: dev-setup
description: WHEN setting up a fresh clone of this repo, or a new machine, to build, test and run the service.
---

# From zero to running

## 1. Prerequisites

- .NET SDK 10.0.x (`dotnet --list-sdks`; built with 10.0.403).
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
dotnet restore
```

## 3. Configuration

No secrets are needed for local work. The repo reads:

| Setting | Default | Where set |
|---|---|---|
| `ConnectionStrings__CustomerDb` | `localhost,1433`, `sa` / `Your_strong_Passw0rd` | `src/CustomerService.Api/appsettings.json`; Compose overrides it |
| `Database__ApplyMigrationsOnStartup` | `false` | `appsettings.json`; Compose sets `true` |
| `MSSQL_SA_PASSWORD` | `Your_strong_Passw0rd` | shell or a gitignored `.env` next to `docker-compose.yml` |
| `INTEGRATION_TEST_DB` | unset (SQL Server) | set to `sqlite` to run integration tests without containers |
| `DOCKER_HOST` | unset | Podman users: `unix://$(podman machine inspect --format '{{.ConnectionInfo.PodmanSocket.Path}}')` |

If you change `MSSQL_SA_PASSWORD`, change the connection string to match.

## 4. Database

```bash
docker compose up -d sqlserver          # or podman compose up -d sqlserver
dotnet ef database update -p src/CustomerService.Api
```

There's no seed data; create customers through the API.

## 5. Verify

```bash
sh .claude/pre-pr-checks                # build, migrations check, unit + integration (SQLite)
dotnet test tests/CustomerService.IntegrationTests   # against real SQL Server
docker compose up --build -d && curl -s http://localhost:8080/health
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
