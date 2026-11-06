---
name: docker
description: WHEN building the container image, running the full stack with Compose, or changing the Dockerfile or docker-compose.yml, with Docker or Podman.
---

# Containers: Dockerfile and Compose

## API Dockerfile (`apps/api/Dockerfile`)

- Stage `build`: `mcr.microsoft.com/dotnet/sdk:10.0`. Copies only `apps/api/src/CustomerService.Api/CustomerService.Api.csproj` first and restores, so the package layer stays cached until the csproj changes; then copies `src/` and publishes Release to `/app/publish` with `/p:UseAppHost=false`.
- Stage `runtime`: `mcr.microsoft.com/dotnet/aspnet:10.0`, `ASPNETCORE_HTTP_PORTS=8080`, runs as non-root `USER $APP_UID`, `EXPOSE 8080`, entrypoint `dotnet CustomerService.Api.dll`.
- The runtime image has no `curl`, so there's no `HEALTHCHECK` in the Dockerfile. Probe `/health` from outside.
- `.dockerignore` keeps `tests/`, `bin/`, `obj/`, `.env` and the compose file out of the build context. If you add a new `src/` project the API references, add its csproj to the restore `COPY` step too.

```bash
docker build -t customer-service-api:local apps/api
podman build -t customer-service-api:local apps/api
```

## Compose (`docker-compose.yml`)

- `sqlserver`: SQL Server 2022, `platform: linux/amd64`, healthcheck via `sqlcmd`, volume `sqlserver-data`. See the `sql-server` skill.
- `api`: built from the Dockerfile, `depends_on: sqlserver: condition: service_healthy`, `Database__ApplyMigrationsOnStartup=true` so it migrates on start, port `8080`.
- `MSSQL_SA_PASSWORD` and `ASPNETCORE_ENVIRONMENT` come from the shell or a gitignored `.env`, with dev defaults.

```bash
docker compose up --build -d
curl -s http://localhost:8080/health         # Healthy
docker compose logs -f api
docker compose down                          # add -v to drop the database volume
```

## Podman

- `podman compose` needs a provider: `brew install podman-compose`.
- **After a rebuild, `podman-compose` keeps the running container on the old image.** Always use:
  `podman compose up -d --build --force-recreate api`
  Check with `podman inspect customer-service_api_1 --format '{{.Image}}'` against `podman images`.
- Apple Silicon needs Rosetta for SQL Server; see the `sql-server` skill.

## Gotchas

- `$$` in the compose healthcheck escapes `$` so the password is expanded inside the container, not by Compose.
- Container names differ: `customer-service-api-1` under Docker Compose, `customer-service_api_1` under podman-compose.
