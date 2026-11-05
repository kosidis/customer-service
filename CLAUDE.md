# CLAUDE.md

## What this is

A REST API for customer CRUD on .NET 10, ASP.NET Core controllers and EF Core 10 over SQL Server 2022. It's one deployable (`src/CustomerService.Api`) with two test projects.

| Module | Responsibility |
|---|---|
| `src/CustomerService.Api/Controllers/` | HTTP endpoints only; no logic beyond mapping results to status codes |
| `src/CustomerService.Api/Contracts/` | Request/response records and validation attributes |
| `src/CustomerService.Api/Errors/` | `NotFoundException`, `ConflictException`, `GlobalExceptionHandler` (exception → ProblemDetails) |
| `src/CustomerService.Api/Services/` | Business rules (`CustomerManager` implements `ICustomerService`) |
| `src/CustomerService.Api/Domain/` | The `Customer` entity |
| `src/CustomerService.Api/Data/` | `CustomerDbContext`, `CustomerRepository` (implements `ICustomerRepository`), `Migrations/` |
| `src/CustomerService.Api/Program.cs` | DI wiring, middleware, health checks, optional migrate-on-start-up |
| `tests/CustomerService.UnitTests/` | xUnit + Moq; folders mirror the API's |
| `tests/CustomerService.IntegrationTests/` | HTTP tests through `WebApplicationFactory` against a real database |
| `deploy/helm/customer-service/` | Helm chart; `values-dev.yaml`, `values-staging.yaml`, `values-prod.yaml` per environment. See the `helm` skill |

Dependencies point one way: Controllers → `ICustomerService` → `ICustomerRepository` → `CustomerDbContext`. See the `presentation-layer`, `business-layer` and `data-layer` skills.

## Local setup

- .NET SDK 10.0.x. Run `dotnet tool restore` once to get `dotnet-ef` 10.0.14 from `dotnet-tools.json`.
- A container runtime (Docker, or Podman with a compose provider) for SQL Server and the Testcontainers integration run.
- Start SQL Server: `docker compose up -d sqlserver` (or `podman compose up -d sqlserver`).
- Settings: `ConnectionStrings__CustomerDb` (default in `appsettings.json` points at `localhost,1433` with the dev SA password) and `Database__ApplyMigrationsOnStartup` (`false` by default; Compose sets `true`). `MSSQL_SA_PASSWORD` overrides the dev password in Compose via a gitignored `.env`.
- Apple Silicon: SQL Server is x86-only and segfaults under QEMU. The Podman machine MUST have Rosetta on (`[machine] rosetta = true` in `~/.config/containers/containers.conf`) and at least 4 GiB of memory.

Full walkthrough: the `dev-setup` skill.

## Build / test / run

```bash
dotnet build
dotnet test tests/CustomerService.UnitTests /p:CollectCoverage=true      # fails below 80% line coverage
dotnet test tests/CustomerService.IntegrationTests                       # SQL Server via Testcontainers
INTEGRATION_TEST_DB=sqlite dotnet test tests/CustomerService.IntegrationTests   # no container runtime needed
dotnet run --project src/CustomerService.Api                             # needs SQL Server and a migrated DB
docker compose up --build                                                # full stack on http://localhost:8080
```

With Podman, export `DOCKER_HOST="unix://$(podman machine inspect --format '{{.ConnectionInfo.PodmanSocket.Path}}')"` before the Testcontainers run, and pass `--force-recreate` to `podman compose up` after a rebuild or the old container keeps running.

## Conventions

- **Interface + implementation, injected by constructor.** Every collaborator crosses a layer through an interface (`ICustomerService`, `ICustomerRepository`) registered as scoped in `Program.cs`. Use primary constructors, as in `CustomerManager(ICustomerRepository repository, TimeProvider timeProvider)`.
- **Errors are exceptions, not result types.** Services throw `NotFoundException` (404) or `ConflictException` (409); `GlobalExceptionHandler` turns them into ProblemDetails. Anything else becomes a 500 with no `Detail`, so internal messages never leak. Add a new status by adding an exception type and a case in the handler's `switch`.
- **Validation lives on contracts.** Put DataAnnotations on the record's constructor parameters, never with a `[property: ...]` target: ASP.NET throws on that at runtime and only the integration tests catch it. `[ApiController]` returns 400 automatically.
- **Time comes from `TimeProvider`**, never `DateTime.UtcNow` in services, so tests can fix the clock (`FixedTimeProvider` in `CustomerManagerTests`).
- **Normalise input in the service.** Names and phone are trimmed; email is trimmed and lower-cased before the uniqueness check.
- **DateTimes are UTC.** `CustomerDbContext` marks `CreatedAtUtc`/`UpdatedAtUtc` as `DateTimeKind.Utc` on read. Do the same for any new `DateTime` column, or JSON drops the `Z`.
- **Logging:** inject `ILogger<T>`, use message templates (`{Method} {Path}`), log only unexpected errors at `Error`.
- **Health endpoints:** `/health/live` runs no checks (liveness); `/health/ready` runs checks tagged `ready` (readiness). Tag any new dependency check `ready`; never add one to liveness, or an outage restarts every pod.
- **Start-up failures exit with code 1.** `Program.cs` catches and logs them because, as PID 1 in a container, an unhandled exception hangs the process instead of ending it. Keep new start-up work inside that `try`.
- **Tests:** name them `Method_Condition_Expected`. Unit tests use `MockBehavior.Strict`. Repository tests run on SQLite in-memory, not the EF in-memory provider.

## Database changes

Migrations live in `src/CustomerService.Api/Data/Migrations/` and target SQL Server.

```bash
dotnet ef migrations add <MigrationName> -p src/CustomerService.Api -o Data/Migrations
dotnet ef database update -p src/CustomerService.Api                     # apply
dotnet ef database update <PreviousMigrationName> -p src/CustomerService.Api   # roll back
dotnet ef migrations remove -p src/CustomerService.Api                   # drop the last, unapplied migration
dotnet ef migrations has-pending-model-changes -p src/CustomerService.Api      # MUST report no changes before a PR
```

Never edit a migration that has been applied outside your machine; add a new one. Details: the `migration` skill.

## How to add new code

- **New or changed endpoint:** contract in `Contracts/` → service method on `ICustomerService` + `CustomerManager` → repository method if new data access → controller action → unit tests per layer → integration test in `CustomersApiTests`. Use the `endpoint` skill.
- **New or changed column/entity:** `Domain/` → `CustomerDbContext.OnModelCreating` → `dotnet ef migrations add` → contracts and mapping in `CustomerManager.ToResponse` → tests. Use the `migration` skill.

Verify every change with `sh .claude/pre-pr-checks`.

## CI/CD

There's no CI pipeline and no git remote yet. `.claude/pre-pr-checks` is the only gate: build, unmigrated-model check, unit tests with the 80% coverage gate, integration tests on SQLite, and `helm lint`/`helm template` for each environment. Run it locally before any PR. Before merging changes to data access, also run the integration tests against SQL Server. Deployment is the Docker image from `Dockerfile`, installed with the Helm chart; there's no automated deploy. See the `ci` and `helm` skills.

## Superpowers (mandatory)

The `superpowers` skills below are mandatory directives: invoke the matching skill rather than improvising. `using-superpowers` is the entry skill that routes to the right one.

- **Plan before building.** Use `brainstorming` to explore the approach, `writing-plans` to write the plan, and `executing-plans` to carry out an approved plan. Required before multi-step or architecture-touching work.
- **Design visually.** When a plan, tech design, or story/task/bug solution describes a flow or an API interaction, invoke `design-diagrams`: mermaid in the design or plan markdown, never in a Jira description.
- **Build with discipline.** `test-driven-development` (tests first), `systematic-debugging` (structured root-causing), `verification-before-completion` (prove it works, for example with `sh .claude/pre-pr-checks`, before calling it done).
- **Isolate and parallelise.** Always work in a git worktree, even for a solo change; the guards below enforce it. Give each concurrent stream its own worktree and remove it once its branch lands. Use `using-git-worktrees` for the mechanics. Development work MUST be decomposed and dispatched across parallel subagents per `parallel-development`, which owns worktree placement under `.claude/worktrees/`, flat ticket-prefixed branches, dispatch routing, and the only two cases where serial work is allowed.
- **Review.** `requesting-code-review` before merging; `receiving-code-review` to act on feedback.
- **Wrap up.** `finishing-a-development-branch` to land a branch.
- **Author skills.** `writing-skills` (with the `skill-creator` plugin) when adding a repo skill.
- **Compound.** At the end of a branch or after a hard-won fix, invoke `distilling-lessons` to push the durable rule into the repo skill that produced it, or into `docs/lessons/LESSONS.md`. Route delegated work by tier with `model-routing`: default down, escalate only when the task earns it.

### Hook-enforced rules

Every rule below except the last two is enforced by a shift-workflow hook and can't be bypassed. Each guard is a no-op unless it positively matches, and blocks only then. The last two are `.claude/settings.json` defaults, not hooks.

- **Plan before implement.** Plan before editing code: plan mode (approval emits `ExitPlanMode`) or the superpowers planning skills. `plan-before-implement` blocks repo-code edits until the session shows one. `CLAUDE.md`, `.claude/*`, `.mcp.json`, `.gitignore` and edits outside the repo are exempt.
- **Require plan approval.** A plan or spec written under `docs/superpowers/` MUST be approved by a human before implementation. `require-plan-approval` blocks code edits while the transcript shows such a write with no genuine user turn after it. Approval is judged against the first plan write and persists; dispatching a subagent doesn't bypass it.
- **Flat branch names.** Branches MUST be flat and ticket-prefixed: `no-ticket-<slug>` without a ticket, else `<TICKET-ID>-<slug>` (for example `no-ticket-fix-readme`). `flat-branch-names` blocks creating or renaming to anything else.
- **Worktree path.** Worktrees MUST live under `.claude/worktrees/` (`git worktree add .claude/worktrees/<branch> -b <branch>`). `worktree-path` blocks `git worktree add` elsewhere.
- **Work in a worktree.** Repo-code edits MUST happen in a linked worktree, never the primary tree, with no solo opt-out. `require-worktree` blocks primary-tree edits and `require-worktree-git` the equivalent git mutations. Onboarding outputs, `.orchestrator.conf`, edits outside the repo, and edits already inside `.claude/worktrees/` are exempt.
- **Prefer subagents.** When `.claude/agents/` defines project agents, delegate repo-code changes to the matching agent via `Task`. `prefer-subagents` blocks main-thread edits (Edit/Write, Bash writes, tokensave edit tools) and names the agent. Dormant here until `.claude/agents/` exists.
- **No writes to main/master.** Never commit, integrate or push to `main`/`master`; push a branch and open a PR. `no-main-write` blocks it.
- **Pre-PR checks.** `pre-pr-checks` runs `.claude/pre-pr-checks` before `gh pr create`/`gh pr edit` and blocks the PR if it fails. Fix the failures and retry. Editing that script changes what's enforced.
- **PRs go through `pr-author`.** `require-pr-author` blocks `gh pr create`, and `gh pr edit` that changes title or body, everywhere except inside the `pr-author` agent. A bare `gh pr edit` (labels, reviewers) passes.
- **Tokensave first.** Explore code with the tokensave MCP tools (`tokensave_context`, `tokensave_search`, …), not read/grep sweeps. `tokensave-first` blocks `Explore` agents while `.tokensave/` exists; `tokensave-sync` re-syncs the graph after every edit. From a subfolder, scope queries to it with `path_include`; `tokensave-scope` blocks unscoped or out-of-scope calls.
- **Signed commits.** Every commit MUST be signed. `require-signed-commits` blocks unsigned commits and attempts to disable signing. Sign without a passphrase prompt: prefer SSH signing (`git config gpg.format ssh`, `git config user.signingkey <key>`, key in `ssh-agent` and registered on GitHub as a signing key). The key is per-developer and never committed.
- **No AI attribution in commits.** No `Co-Authored-By:` naming Claude/Anthropic, no generated-by line, no `noreply@anthropic.com`. `no-ai-attribution` blocks the commit; reword and retry.
- **Attribution settings.** The `attribution` block in `.claude/settings.json` suppresses Claude Code's auto-appended commit/PR byline and session URL. It complements `no-ai-attribution`, which still catches model-written trailers.
- **Model defaults.** `model`, `effortLevel` and `advisorModel` in `.claude/settings.json` start every session on the same cost-sane tier. Not hook-enforced.

_Generated by shift-core plugin version 0.3.0._
