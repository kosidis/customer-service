# CLAUDE.md

## What this is

A monorepo for customer management with two deployables:

- `apps/api`: a REST API for customer CRUD on .NET 10, ASP.NET Core controllers and EF Core 10 over SQL Server 2022 (`apps/api/src/CustomerService.Api`, plus two test projects).
- `apps/web`: a Next.js 16 (App Router, React 19, Tailwind 4, TypeScript) UI to list, search, add, edit and delete customers. It has no login and no database; it calls the API from the server only.

The browser never talks to the API. In Compose and Kubernetes only the web app is the public entry point (`API_BASE_URL` points it at the API).

API modules:

| Module | Responsibility |
|---|---|
| `apps/api/src/CustomerService.Api/Controllers/` | HTTP endpoints only; no logic beyond mapping results to status codes |
| `apps/api/src/CustomerService.Api/Contracts/` | Request/response records and validation attributes |
| `apps/api/src/CustomerService.Api/Errors/` | `NotFoundException`, `ConflictException`, `GlobalExceptionHandler` (exception → ProblemDetails) |
| `apps/api/src/CustomerService.Api/Services/` | Business rules (`CustomerManager` implements `ICustomerService`) |
| `apps/api/src/CustomerService.Api/Domain/` | The `Customer` entity |
| `apps/api/src/CustomerService.Api/Data/` | `CustomerDbContext`, `CustomerRepository` (implements `ICustomerRepository`), `Migrations/` |
| `apps/api/src/CustomerService.Api/Program.cs` | DI wiring, middleware, health checks, optional migrate-on-start-up |
| `apps/api/tests/CustomerService.UnitTests/` | xUnit + Moq; folders mirror the API's |
| `apps/api/tests/CustomerService.IntegrationTests/` | HTTP tests through `WebApplicationFactory` against a real database |
| `apps/api/helm/customer-service/` | Helm chart; `values-dev.yaml`, `values-staging.yaml`, `values-prod.yaml` per environment. See the `helm` skill |

Dependencies point one way: Controllers → `ICustomerService` → `ICustomerRepository` → `CustomerDbContext`. See the `presentation-layer`, `business-layer` and `data-layer` skills.

Web modules (all under `apps/web`):

| Module | Responsibility |
|---|---|
| `src/lib/api/` | The only place that calls the API; `import "server-only"`. `http.ts` (`apiFetch`, `ApiError`), `customers.ts` (one function per endpoint), `types.ts` (request/response types). See the `web-api-layer` skill |
| `src/app/customers/actions.ts` | Server actions (`"use server"`): create, update, delete; map `ApiError` to form state |
| `src/app/` | App Router pages, `layout.tsx`, `customers/error.tsx`, `customers/loading.tsx`, `api/health/route.ts` (`GET /api/health`, no backend call). See the `nextjs` skill |
| `src/components/` | `CustomerForm`, `CustomerTable`, `DeleteCustomerButton`, `Pagination`, `SearchBox` |
| `src/lib/form-state.ts` | `CustomerFormState` shape and `initialFormState` |
| `src/__tests__/` | Jest + React Testing Library, one file per area. See the `jest-rtl` skill |
| `helm/customer-service-web/` | Helm chart; `values-dev.yaml`, `values-staging.yaml`, `values-prod.yaml`. See the `helm` skill |
| `Dockerfile` | Multi-stage `node:24-alpine`, standalone output, UID 1001, port 3000. See the `docker` skill |

## Local setup

- .NET SDK 10.0.x. Run `dotnet tool restore` once to get `dotnet-ef` 10.0.14 from `dotnet-tools.json`.
- A container runtime (Docker, or Podman with a compose provider) for SQL Server and the Testcontainers integration run.
- Start SQL Server: `docker compose up -d sqlserver` (or `podman compose up -d sqlserver`).
- Settings: `ConnectionStrings__CustomerDb` (default in `appsettings.json` points at `localhost,1433` with the dev SA password) and `Database__ApplyMigrationsOnStartup` (`false` by default; Compose sets `true`). `MSSQL_SA_PASSWORD` overrides the dev password in Compose via a gitignored `.env`.
- Apple Silicon: SQL Server is x86-only and segfaults under QEMU. The Podman machine MUST have Rosetta on (`[machine] rosetta = true` in `~/.config/containers/containers.conf`) and at least 4 GiB of memory.

Web app:

- Node 24 and npm. Run `cd apps/web && npm ci` once.
- `API_BASE_URL` (required, read at request time, never at build time): the API's base URL, for example `http://localhost:8080`. The app throws a clear error on the first API call if it's unset.
- `apps/web/AGENTS.md` says this Next.js has breaking changes: read the matching guide in `apps/web/node_modules/next/dist/docs/` before changing Next.js APIs.

Full walkthrough: the `dev-setup` skill.

## Build / test / run

```bash
dotnet build apps/api
dotnet test apps/api/tests/CustomerService.UnitTests /p:CollectCoverage=true      # fails below 80% line coverage
dotnet test apps/api/tests/CustomerService.IntegrationTests                       # SQL Server via Testcontainers
INTEGRATION_TEST_DB=sqlite dotnet test apps/api/tests/CustomerService.IntegrationTests   # no container runtime needed
cd apps/api/tests/CustomerService.UnitTests && dotnet stryker                     # mutation tests; never from apps/api (see the `mutation` skill)
dotnet run --project apps/api/src/CustomerService.Api                             # needs SQL Server and a migrated DB
docker compose up --build                                                # API on http://localhost:8080, web UI on http://localhost:3000
```

Web app, from `apps/web`:

```bash
npm run lint                                  # ESLint (next core-web-vitals + TypeScript)
npm run typecheck                             # tsc --noEmit
npm test                                      # Jest + React Testing Library
npm run test:coverage                         # fails below 80% lines, branches, functions and statements
npm run build                                 # production build, output: "standalone"
API_BASE_URL=http://localhost:8080 npm run dev     # http://localhost:3000; needs the API running
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

Web app:

- **All HTTP goes through `src/lib/api`.** Components, pages and actions never call `fetch` or read `API_BASE_URL`. Every module there imports `server-only`, so a client component importing it fails the build.
- **Mutations are server actions** in `src/app/customers/actions.ts`. Call `revalidatePath` and `redirect` outside the `try/catch`: `redirect` throws, and a catch would swallow it.
- **The API is the only source of validation rules.** The web app shows the API's field errors (a 400's `errors`, keys matched case-insensitively onto the form fields listed in `KNOWN_FIELDS` in `apps/web/src/lib/api/http.ts`; add a new field there too) and never re-implements them. A 400 whose keys match no field, and every other non-409 failure, becomes the form-level error; a 5xx or network failure shows a generic message, never internal detail. A 409 becomes an email field error.
- **Form state shape:** `CustomerFormState` = `{ fieldErrors: Record<string, string[]>, formError: string | null, values }`. Actions return it with the submitted `values` echoed back so the form keeps what the user typed.
- **Pages that read the API set `export const dynamic = "force-dynamic"`**, and `apiFetch` sends `cache: "no-store"`. Never cache customer data.
- **Delete keeps `search` and `page`** when it returns to the list. `safeReturnPath` parses the target and keeps it only if it resolves to `/customers` or a path under `/customers/` on this origin; anything else becomes `/customers`. Never replace that with a prefix test: `/customers/../x` passes one.
- **Tests** are Jest + RTL in `src/__tests__`. `server-only` is mapped to an empty module in `jest.config.ts`. Mock `@/lib/api/customers`, `next/navigation` and `next/cache`; mock `fetch` only in `api.test.ts`. Coverage gate is 80% on all four metrics.

## Database changes

Migrations live in `apps/api/src/CustomerService.Api/Data/Migrations/` and target SQL Server.

```bash
dotnet ef migrations add <MigrationName> -p apps/api/src/CustomerService.Api -o Data/Migrations
dotnet ef database update -p apps/api/src/CustomerService.Api                     # apply
dotnet ef database update <PreviousMigrationName> -p apps/api/src/CustomerService.Api   # roll back
dotnet ef migrations remove -p apps/api/src/CustomerService.Api                   # drop the last, unapplied migration
dotnet ef migrations has-pending-model-changes -p apps/api/src/CustomerService.Api      # MUST report no changes before a PR
```

Never edit a migration that has been applied outside your machine; add a new one. Details: the `migration` skill.

## How to add new code

- **New or changed endpoint:** contract in `Contracts/` → service method on `ICustomerService` + `CustomerManager` → repository method if new data access → controller action → unit tests per layer → integration test in `CustomersApiTests`. Use the `endpoint` skill.
- **New or changed column/entity:** `Domain/` → `CustomerDbContext.OnModelCreating` → `dotnet ef migrations add` → contracts and mapping in `CustomerManager.ToResponse` → tests. Use the `migration` skill.

- **New or changed page or form (web):** API layer if the data is new → server action → component → page → tests → `npm run lint && npm run typecheck && npm run test:coverage && npm run build`. Use the `ui-page` skill.
- **New or changed API call (web):** type in `types.ts` → function in `customers.ts` → test in `src/__tests__/api.test.ts` with `fetch` mocked. Use the `web-api-layer` skill. If the API contract changed too, do the `endpoint` skill first.

Verify every change with `sh .claude/pre-pr-checks`.

## CI/CD

There's no CI pipeline and no git remote yet. `.claude/pre-pr-checks` is the only gate. For the API it runs build, unmigrated-model check, unit tests with the 80% coverage gate and integration tests on SQLite. For the web app it runs, from `apps/web`, `npm ci` (only when `node_modules` is missing), `npm run lint`, `npm run typecheck`, `npm run test:coverage` (80% threshold) and `npm run build`. It then runs `helm lint`/`helm template` for every chart under `apps/*/helm/*` in each environment. Run it locally before any PR. Before merging changes to data access, also run the integration tests against SQL Server. Deployment is the Docker images from `apps/api/Dockerfile` and `apps/web/Dockerfile`, installed with the two Helm charts (API first, then web); there's no automated deploy. A future GitHub Actions workflow needs a Node 24 job beside the .NET one. See the `ci` and `helm` skills.

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
