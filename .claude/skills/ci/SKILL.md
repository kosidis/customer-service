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

It runs, and fails if any step fails:

1. `dotnet build --nologo`
2. `dotnet ef migrations has-pending-model-changes -p src/CustomerService.Api --no-build`
3. `dotnet test tests/CustomerService.UnitTests /p:CollectCoverage=true` (80% line gate)
4. `INTEGRATION_TEST_DB=sqlite dotnet test tests/CustomerService.IntegrationTests`

It uses SQLite for speed. Before merging changes to queries, the model or migrations, also run the SQL Server suite by hand (`dotnet test tests/CustomerService.IntegrationTests`; see the `testcontainers` skill).

## Flow

1. Work in a worktree on a flat branch: `git worktree add .claude/worktrees/no-ticket-<slug> -b no-ticket-<slug>`.
2. Signed commits, message format `[<TICKET-ID>]: <summary>` (see the `commit-message` skill). No AI attribution.
3. `sh .claude/pre-pr-checks` until green.
4. Push the branch and open the PR through the `pr-author` agent. Never push to `main`.
5. There's no automated deploy. The deployable is the image built from `Dockerfile`.

## Adding a GitHub Actions pipeline

When a GitHub remote exists, mirror the gate so CI and local agree. A minimal `.github/workflows/ci.yml` runs on `ubuntu-latest` with `actions/setup-dotnet` for `10.0.x`, then:

```bash
dotnet tool restore
dotnet build --nologo
dotnet ef migrations has-pending-model-changes -p src/CustomerService.Api --no-build
dotnet test tests/CustomerService.UnitTests /p:CollectCoverage=true
dotnet test tests/CustomerService.IntegrationTests      # Docker is available on GitHub runners, so use real SQL Server here
docker build -t customer-service-api:ci .
```

Keep `.claude/pre-pr-checks` and the workflow in step: a check added to one belongs in the other. Use the `github-ci` skill to debug failing runs.
