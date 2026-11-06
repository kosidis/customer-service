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

## Web Dockerfile (`apps/web/Dockerfile`)

- Stage `deps` (`node:24-alpine`): copies only `package.json` and `package-lock.json` and runs `npm ci`, so the dependency layer is cached until either changes.
- Stage `build`: copies `node_modules` and the source, creates an empty `public/` if there isn't one, and runs `npm run build`. `API_BASE_URL` is deliberately not set at build time; the server reads it at runtime, so one image works in every environment.
- Stage `runtime`: copies `.next/standalone` (`server.js` and traced `node_modules`), `.next/static` and `public/`, creates a fixed user and group 1001 (`nextjs:nodejs`), sets `PORT=3000` and `HOSTNAME=0.0.0.0`, pre-creates `.next/cache` owned by 1001, then `USER 1001`, `EXPOSE 3000`, `CMD ["node", "server.js"]`.
- The Helm chart's `runAsUser: 1001` and its `emptyDir` at `/app/.next/cache` depend on those two details. Keep them in step.
- `.dockerignore` keeps `node_modules`, `.next`, `coverage`, `helm`, `*.md` and `.env*` out of the build context. The image has no `HEALTHCHECK`; probe `GET /api/health`.

```bash
docker build -t customer-service-web:local apps/web
docker run --rm -p 3000:3000 -e API_BASE_URL=http://host.docker.internal:8080 customer-service-web:local
podman build -t customer-service-web:local apps/web
```

## Compose (`docker-compose.yml`)

- `sqlserver`: SQL Server 2022, `platform: linux/amd64`, healthcheck via `sqlcmd`, volume `sqlserver-data`. See the `sql-server` skill.
- `api`: built from the Dockerfile, `depends_on: sqlserver: condition: service_healthy`, `Database__ApplyMigrationsOnStartup=true` so it migrates on start, port `8080`.
- `web`: built from `./apps/web`, `depends_on: api` (waits for the container to start, not for it to be healthy), `API_BASE_URL=http://api:8080` (server-side only; the browser never calls the API), port `3000`. Pages that need the API show an error until the API has finished migrating; reload once `curl http://localhost:8080/health` says `Healthy`.
- `MSSQL_SA_PASSWORD` and `ASPNETCORE_ENVIRONMENT` come from the shell or a gitignored `.env`, with dev defaults.

```bash
docker compose up --build -d
curl -s http://localhost:8080/health         # API: Healthy
curl -s http://localhost:3000/api/health     # web: Healthy
docker compose logs -f api web
docker compose down                          # add -v to drop the database volume
```

## Podman

- `podman compose` needs a provider: `brew install podman-compose`.
- **After a rebuild, `podman-compose` keeps the running container on the old image.** Always use:
  `podman compose up -d --build --force-recreate api web` (name only the service you changed)
  Check with `podman inspect customer-service_api_1 --format '{{.Image}}'` against `podman images`.
- Apple Silicon needs Rosetta for SQL Server; see the `sql-server` skill.

## Gotchas

- `$$` in the compose healthcheck escapes `$` so the password is expanded inside the container, not by Compose.
- Container names differ: `customer-service-api-1` and `customer-service-web-1` under Docker Compose, `customer-service_api_1` and `customer-service_web_1` under podman-compose.
