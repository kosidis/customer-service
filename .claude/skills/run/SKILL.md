---
name: run
description: WHEN starting the API or the web UI locally or in containers and calling its endpoints to check a change works for real.
---

# Run

## Full stack in containers (simplest)

```bash
docker compose up --build -d                                  # or: podman compose up -d --build --force-recreate
curl -s http://localhost:8080/health                          # API: Healthy
curl -s http://localhost:3000/api/health                      # web: Healthy
```

The API migrates the database on start-up in Compose. Open http://localhost:3000 for the UI; it redirects to `/customers`. The web container calls the API at `http://api:8080`, so the pages work once the API is healthy.

## API on the host, SQL Server in a container

```bash
docker compose up -d sqlserver
dotnet tool restore
dotnet ef database update -p apps/api/src/CustomerService.Api
dotnet run --project apps/api/src/CustomerService.Api                  # http://localhost:5062 (launchSettings "http" profile)
```

Or skip the `ef` step with `Database__ApplyMigrationsOnStartup=true dotnet run --project apps/api/src/CustomerService.Api`. The `Development` environment (set by `launchSettings.json`) serves OpenAPI at `/openapi/v1.json`.

## Web UI on the host

Start the API (Compose or `dotnet run`), then:

```bash
cd apps/web && npm ci            # first time
API_BASE_URL=http://localhost:8080 npm run dev     # use :5062 for `dotnet run`; UI on http://localhost:3000
```

Check list, add, edit, delete, search and paging. Try a duplicate email (field error on Email) and stop the API (the list shows the error screen with Try again).

## Exercise every operation

Set `BASE` to `http://localhost:8080` (Compose) or `http://localhost:5062` (`dotnet run`):

```bash
BASE=http://localhost:8080
curl -i -X POST $BASE/api/customers -H 'Content-Type: application/json' \
  -d '{"firstName":"Jane","lastName":"Doe","email":"jane@example.com","phoneNumber":"+44 20 7946 0000"}'   # 201 + Location
curl -s "$BASE/api/customers?page=1&pageSize=20&search=doe"                                              # 200 paged list
curl -i $BASE/api/customers/<id>                                                                         # 200 or 404
curl -i -X PUT $BASE/api/customers/<id> -H 'Content-Type: application/json' \
  -d '{"firstName":"Janet","lastName":"Doe","email":"janet@example.com"}'                                # 200
curl -i -X DELETE $BASE/api/customers/<id>                                                               # 204
```

Expect 400 for validation failures, 404 for unknown ids, 409 for a duplicate email, all as `application/problem+json`. Timestamps end in `Z`.

## Stop

`docker compose down` (add `-v` to wipe data), or Ctrl+C for `dotnet run` and `npm run dev`.
