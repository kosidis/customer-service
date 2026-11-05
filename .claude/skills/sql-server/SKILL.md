---
name: sql-server
description: WHEN starting, connecting to, inspecting or troubleshooting the SQL Server 2022 database this service uses, locally or in containers.
---

# SQL Server in this repo

Image: `mcr.microsoft.com/mssql/server:2022-latest` (used by `docker-compose.yml` and by `CustomerApiFactory` via Testcontainers). Database name: `CustomerDb`. Table: `Customers`.

## Start it

```bash
docker compose up -d sqlserver        # or: podman compose up -d sqlserver
```

Compose exposes `1433`, stores data in the `sqlserver-data` volume, and runs a healthcheck with `sqlcmd ... -Q 'SELECT 1'`. The API service waits for `service_healthy`.

## Connection strings

- Host machine: `Server=localhost,1433;Database=CustomerDb;User Id=sa;Password=<MSSQL_SA_PASSWORD>;TrustServerCertificate=True` (the default in `appsettings.json` uses the dev password `Your_strong_Passw0rd`).
- Inside Compose: `Server=sqlserver,1433;...` (set via `ConnectionStrings__CustomerDb` in `docker-compose.yml`).
- `TrustServerCertificate=True` is needed because the container uses a self-signed cert. Don't use it against a real server.
- Override the password with `MSSQL_SA_PASSWORD` in a gitignored `.env`. It must meet SQL Server complexity rules or the container exits on start.

## Inspect data

```bash
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Your_strong_Passw0rd' -C \
  -d CustomerDb -Q "SELECT TOP 20 * FROM Customers; SELECT * FROM __EFMigrationsHistory;"
```

`-C` trusts the self-signed cert; tools live under `mssql-tools18`, not `mssql-tools`.

## Apple Silicon

There's no arm64 image. `docker-compose.yml` pins `platform: linux/amd64`. Under QEMU emulation SQL Server **segfaults** on start (`launch_sqlservr.sh: Segmentation fault`). Fix for Podman:

```bash
podman machine stop
podman machine set --memory 6144
printf '[machine]\nrosetta = true\n' >> ~/.config/containers/containers.conf
podman machine start
```

Podman 6 has no `--rosetta` flag; it's a `containers.conf` setting. SQL Server needs at least 2 GB of RAM for itself, so a 2 GiB VM isn't enough. Docker Desktop: enable "Use Rosetta for x86/amd64 emulation".

## Reset

```bash
docker compose down -v      # deletes the sqlserver-data volume
```
