---
name: ci
description: WHEN preparing a change for a PR, reproducing the required checks locally, or adding a CI pipeline to this repo.
---

# CI and how a change ships

## Today

There's **no CI pipeline** (no `.github/workflows/`) and no git remote yet. The only enforced gate is `.claude/pre-pr-checks`, which shift-workflow's `pre-pr-checks` hook runs before `gh pr create`/`gh pr edit`. A failure blocks the PR.

```bash
sh .claude/pre-pr-checks
```

It needs `dotnet` (.NET 10) and `npm` (Node 24), and fails if any step fails:

1. `dotnet build apps/api --nologo`
2. `dotnet ef migrations has-pending-model-changes -p apps/api/src/CustomerService.Api --no-build`
3. `dotnet test apps/api/tests/CustomerService.UnitTests /p:CollectCoverage=true` (80% line gate)
4. `INTEGRATION_TEST_DB=sqlite dotnet test apps/api/tests/CustomerService.IntegrationTests`
5. Web, in `apps/web`: `npm ci` (only when `node_modules` is missing), then `npm run lint`, `npm run typecheck`, `npm run test:coverage` (80% threshold) and `npm run build`.
6. Android, in `apps/android`: `./gradlew :app:testDebugUnitTest :app:lintDebug`. Runs only when `apps/android` exists, `java` is on PATH and an Android SDK is found (`ANDROID_HOME`, `ANDROID_SDK_ROOT` or `sdk.dir` in `apps/android/local.properties`); otherwise it prints a skip notice to stderr and the script still passes. It is not run with `--offline`, so a cold Gradle cache needs network. The SDK needs platform `android-37.0`.
7. Helm: `helm lint` and `helm template` for every chart under `apps/*/helm/*` (`customer-service` and `customer-service-web`) with each environment's values file. Skipped with a warning if `helm` isn't installed.

Android gaps: there is still no CI pipeline for the app (a future workflow needs a JDK 21 job with an Android SDK, platform `android-37.0`). `lintDebug` could not run offline here (the androidTest classpath needs `androidx.core:core-ktx:1.13.0` artifacts that weren't cached); it needs network on a cold cache. Instrumented tests (`connectedAndroidTest`) need an emulator and are not part of `pre-pr-checks`.

It uses SQLite for speed. Before merging changes to queries, the model or migrations, also run the SQL Server suite by hand (`dotnet test apps/api/tests/CustomerService.IntegrationTests`; see the `testcontainers` skill).

## Flow

1. Work in a worktree on a flat branch: `git worktree add .claude/worktrees/no-ticket-<slug> -b no-ticket-<slug>`.
2. Signed commits, message format `[<TICKET-ID>]: <summary>` (see the `commit-message` skill). No AI attribution.
3. `sh .claude/pre-pr-checks` until green.
4. Push the branch and open the PR through the `pr-author` agent. Never push to `main`.
5. There's no automated deploy. The deployables are the images built from `apps/api/Dockerfile` and `apps/web/Dockerfile`.

## Adding a GitHub Actions pipeline

When a GitHub remote exists, mirror the gate so CI and local agree. A minimal `.github/workflows/ci.yml` runs on `ubuntu-latest` with `actions/setup-dotnet` for `10.0.x`, then:

```bash
dotnet tool restore
dotnet build apps/api --nologo
dotnet ef migrations has-pending-model-changes -p apps/api/src/CustomerService.Api --no-build
dotnet test apps/api/tests/CustomerService.UnitTests /p:CollectCoverage=true
dotnet test apps/api/tests/CustomerService.IntegrationTests      # Docker is available on GitHub runners, so use real SQL Server here
docker build -t customer-service-api:ci apps/api
```

For the web app add a separate job (it can run in parallel with the .NET one) with `actions/setup-node` for Node 24 and `cache: npm` (`cache-dependency-path: apps/web/package-lock.json`), working directory `apps/web`:

```bash
npm ci
npm run lint
npm run typecheck
npm run test:coverage
npm run build
docker build -t customer-service-web:ci apps/web
```

For the Android app add a job with `actions/setup-java` (Temurin 21), `android-actions/setup-android` plus `sdkmanager "platforms;android-37.0"`, and Gradle caching, running `./gradlew :app:testDebugUnitTest :app:lintDebug` in `apps/android`.

Add a Helm job (or steps) that loops over `apps/*/helm/*` as the script does.

Keep `.claude/pre-pr-checks` and the workflow in step: a check added to one belongs in the other. Use the `github-ci` skill to debug failing runs.
