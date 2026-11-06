---
name: mutation
description: WHEN running mutation tests with Stryker.NET, reading the mutation report, or adding tests to kill surviving mutants.
---

# Mutation testing with Stryker.NET

Stryker changes the API's code in small ways (a *mutant*: a flipped condition, a removed call, an emptied string) and runs the unit tests against each one. A mutant is **killed** when a test fails, and **survives** when none does. A survivor means the code can be wrong without any test noticing, even though line coverage says it ran.

Tool: `dotnet-stryker` 5.0.0 in `dotnet-tools.json`. Config: `apps/api/tests/CustomerService.UnitTests/stryker-config.json`.

## Run

Always from the unit test project's folder (a full run takes about 35 seconds):

```bash
dotnet tool restore
cd apps/api/tests/CustomerService.UnitTests
dotnet stryker
dotnet stryker --open-report        # same, and opens the HTML report live
```

**Never run it from `apps/api`.** Stryker 5 switches to solution mode whenever it finds `CustomerService.slnx` in the working folder (it lives in `apps/api`), then runs every test project, including the integration tests. Those start SQL Server through Testcontainers for each mutant, so the run hangs or takes many times longer. `test-projects` in the config doesn't prevent this.

Reports go to `apps/api/tests/CustomerService.UnitTests/StrykerOutput/<timestamp>/reports/` (gitignored): `mutation-report.html` to browse, `mutation-report.json` to script against. The run prints killed/survived totals and the final score.

## What the config does

- `project`: `CustomerService.Api.csproj` is the code being mutated; the unit test project in the working folder is the test project.
- `mutate`: everything except `Program.cs` and `Data/Migrations/*.cs`, the same exclusions as the coverage gate.
- `coverage-analysis: perTest`: each mutant only runs the tests that reach it.
- `thresholds`: high 80, low 60, `break` 0. Report only; nothing fails the build yet.

## Reading results

| Status | Meaning | Action |
|---|---|---|
| Killed | A test failed | Nothing |
| Survived | Tests passed with the change | Add or tighten an assertion, or accept it as equivalent |
| NoCoverage | No test reaches this code | Add a test |
| Timeout | The mutant made a test hang (for example an endless loop) | Counts as killed |
| CompileError / Ignored | Not runnable, or filtered by `mutate` | Not part of the score |

Score = (killed + timeout) / (killed + timeout + survived + no coverage).

## Kill a survivor

1. Open the HTML report, find the file, and click the red marker to see the exact change.
2. Ask what behaviour would differ if that change shipped, and write the assertion that checks it in the matching test class (`apps/api/tests/CustomerService.UnitTests/<Folder>/<Class>Tests.cs`, see `xunit-moq`).
3. Re-run `dotnet stryker` and confirm the mutant is now killed.
4. If no observable behaviour can differ (an *equivalent mutant*), leave it and say so in the PR. Don't write a test that pins implementation details just to raise the score.

## Known false survivors: `CustomerDbContext.OnModelCreating`

EF Core builds a context type's model once per process and caches it. Stryker runs every mutant in one test process, so `OnModelCreating` only runs for the first test that touches the database, and its mutants can't reach the tests that would catch them (`AddAsync_DuplicateEmail_ViolatesUniqueIndex` for the unique index, `AddAsync_PersistsCustomer` for the UTC converters). They report as survived even though removing them would fail those tests in a normal run. Mapping changes are also caught by `dotnet ef migrations has-pending-model-changes` in `.claude/pre-pr-checks`.

To make them killable, the repository tests' `CreateContext()` would need to bypass the model cache, for example by replacing `IModelCacheKeyFactory` with one that returns a new key per context instance.

## Baseline (2026-11-05)

70% (49 killed, 21 survived, 0 no coverage), 70 mutants tested. Survivors:

| Where | Mutant | Verdict |
|---|---|---|
| `CustomerDbContext.cs` 12–24 (10) | Each mapping statement removed | False survivors (model cache, above) |
| `Domain/Customer.cs` 6–8 (3) | `string.Empty` defaults → `"Stryker was here!"` | Equivalent: every code path sets these |
| `GlobalExceptionHandler.cs` 15–17 (3) | ProblemDetails `Title` strings → `""` | Gap: no test asserts the title |
| `GlobalExceptionHandler.cs` 20, 22 (3) | 500 logging condition flipped, `LogError` removed or emptied | Gap: tests use `NullLogger`, so logging is never checked |
| `CustomerManager.cs` 74 (1) | Conflict message → `""` | Gap: duplicate-email tests don't check the message |
| `CustomerRepository.cs` 26 (1) | Tie-break `ThenBy(c => c.Id)` → descending | Gap: no test has two customers with the same name |
