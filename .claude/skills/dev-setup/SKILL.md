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

## 4c. Android app (optional)

Only needed to build or test `apps/android`.

- JDK 21 (`java -version`) and an Android SDK. Install the SDK platform `android-37.0` (compileSdk is 37; targetSdk 36, minSdk 26) and build-tools through Android Studio or `sdkmanager "platforms;android-37.0"`.
- Tell Gradle where the SDK is: `echo "sdk.dir=$HOME/Library/Android/sdk" > apps/android/local.properties` (gitignored), or export `ANDROID_HOME`.
- Generate the local dev CA and server certificate. This writes the gitignored `dev-certs/` and copies the public CA to `apps/android/app/src/debug/res/raw/dev_ca.pem`, which the debug build trusts:

  ```bash
  sh apps/android/scripts/dev-tls.sh
  ```

  Re-running reuses the CA and re-issues the server certificate. Delete `dev-certs/` to rotate the CA, then rebuild the debug app.
- Start the API behind the TLS proxy: `docker compose --profile mobile up --build -d` (or `podman compose --profile mobile up --build -d`). The `tls-proxy` serves `https://localhost:8443`.
- The debug build's `API_BASE_URL` is `https://10.0.2.2:8443`: `10.0.2.2` is how the emulator reaches the host's localhost, so the proxy is reachable without extra setup.
- For the kind cluster, the dev ingress routes `/api` on `customer-api.localhost` (127.0.0.1) and `api.10-0-2-2.nip.io` (resolves to `10.0.2.2`); the certificate covers both names. Create the `customer-service-api-tls` secret from `dev-certs/server.crt` and `server.key` as `dev-tls.sh` prints at the end.
- Unit tests and lint:

  ```bash
  cd apps/android && ./gradlew :app:testDebugUnitTest
  cd apps/android && ./gradlew :app:lintDebug
  ```

  The first run downloads Gradle and dependencies, so it needs network. Instrumented tests need an emulator and aren't part of `pre-pr-checks`.

## 5. Verify

```bash
sh .claude/pre-pr-checks                # API build, migrations check, unit + integration (SQLite); web lint, typecheck, tests, build; Android unit tests + lint when JDK and SDK are present; Helm
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
