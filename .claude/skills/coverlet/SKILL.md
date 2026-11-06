---
name: coverlet
description: WHEN checking, enforcing or changing unit test coverage (Coverlet MSBuild with an 80% line-coverage gate).
---

# Coverage with Coverlet

`coverlet.msbuild` 10.1.0 in `apps/api/tests/CustomerService.UnitTests/CustomerService.UnitTests.csproj`. The settings sit in a `PropertyGroup` there, so the command line only needs `CollectCoverage`.

## Run

```bash
dotnet test apps/api/tests/CustomerService.UnitTests /p:CollectCoverage=true
```

The run **fails** when total line coverage drops below 80% (`Threshold=80`, `ThresholdType=line`, `ThresholdStat=total`). Reports go to `apps/api/tests/CustomerService.UnitTests/TestResults/coverage/` as `coverage.cobertura.xml` and `coverage.json` (gitignored).

## What's measured

- `Include`: `[CustomerService.Api]*` only. Test assemblies and packages are out.
- `Exclude`: `[CustomerService.Api]Program` (start-up wiring is covered by integration tests).
- `ExcludeByFile`: `**/Data/Migrations/*.cs` (generated).
- `ExcludeByAttribute`: `GeneratedCodeAttribute,CompilerGeneratedAttribute`.

Integration tests aren't counted toward the gate. Cover new code with unit tests.

## See per-class numbers

```bash
grep -o '<class name="[^"]*"[^>]*line-rate="[^"]*"' apps/api/tests/CustomerService.UnitTests/TestResults/coverage/coverage.cobertura.xml | sed 's/filename="[^"]*" //'
```

For an HTML report: `dotnet tool install -g dotnet-reportgenerator-globaltool`, then `reportgenerator -reports:apps/api/tests/CustomerService.UnitTests/TestResults/coverage/coverage.cobertura.xml -targetdir:coverage-report` (`coverage-report/` is gitignored).

## Rules

- Don't widen exclusions to get past the gate. Add an exclusion only for generated or pure wiring code, and say why in the PR.
- Coverage sits at 100% today; a drop in a PR usually means a new branch without a test.
