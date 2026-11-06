# Agent evals

These cases check that an agent working in this repo gets real tasks right, and how much the repo's skills (`.claude/skills/`) help. They run with Claude Code's `claude plugin eval`. Four replay real mistakes; two check that the agent reaches for the right skill. Numbering follows the original proposal, so there are gaps.

> **Status: not calibrated.** No case has completed a full run in both arms, and no grader verdict has been checked against a transcript yet. The one finished run (case 01 with skills) failed only its `builds` grader, which hasn't been investigated. Treat scores as unverified until a calibration run (`--runs 1`, every verdict read against its transcript) is done.

| Case | Task | What it catches |
|---|---|---|
| `01-validation-attribute` | Add an optional `CompanyName` (max 200) | A `[property: ...]` attribute target, which ASP.NET rejects at request time |
| `02-utc-timestamp` | Add `LastContactedAtUtc` | A `DateTime` column without the UTC converter, so JSON loses the `Z` |
| `03-readiness-health-check` | Add a disk-space health check | A dependency check that leaks into liveness and would restart every pod |
| `04-startup-inside-try` | Warm a cache at start-up | Start-up work outside the `try`, which hangs the container as PID 1 on failure |
| `08-endpoint-by-email` | Add `GET /api/customers/by-email` | An endpoint missing a layer, tests, or a documented status |
| `09-rename-column` | Rename `PhoneNumber` to `Phone` | A migration that drops and re-adds the column, losing data |

## Run

From the repo root:

```bash
git fetch origin tag evals-baseline-1   # the pinned code, if your clone doesn't have it
claude plugin eval .claude --eval-dir evals --scaffold --allow-tools Bash Edit Write \
  --model claude-sonnet-5 --no-publish
ps -eo pid,command | grep MSBuild.dll   # afterwards: stop any leftover build servers (see below)
```

- The target is `.claude`, and each case declares `plugins: [../../skills]`, which loads the repo's skills as the plugin under test. Without that line the tool finds no plugin (it looks for a `plugin.json` or `SKILL.md`), and both arms would run the same setup. The suite can't live inside `skills/` itself, since that folder is loaded as plugin content.
- `--scaffold` lets each case run its `scaffold.sh`, which copies the repo into the run's workspace. Without it, the agent starts in an empty folder.
- `--allow-tools Bash Edit Write` grants the tools the cases need to build, test and edit.
- Each case runs 3 times in two arms: with the skills loaded as a plugin, and without. The report shows both scores and the difference.
- `--case '08-*'` runs one case; `--runs 1` is useful while changing a case.
- Results go to `.claude/evals/results/` (gitignored).

The first run asks you to trust the plugin directory; `--trust-plugin` answers it for CI.

## How a run is set up

A scaffold script must sit inside its case folder, so each case's `scaffold.sh` calls the shared `stage-repo.sh`. That script runs `git archive` at a pinned commit into the workspace, deletes `.claude/skills` and `.claude/settings.json` from the copy, and restores the .NET tools and packages. Removing the skills from the copy is what makes the two arms differ: the "with" arm gets them from the plugin under test, the "without" arm has none. `CLAUDE.md` is kept in both arms.

The eval sandbox blocks binding local ports, and a `.claude/settings.json` in the workspace can't lift that. Stryker 5 always starts a local progress server, even with the HTML report off, so it crashes before testing any mutant. That's why there's no mutation-testing case (number 05 in the proposal).

**Known problem: leftover MSBuild build servers.** MSBuild keeps reusable build-server processes running after a build. They hold on to the run's home and temp folders, so a later run's restore connects to them and fails ("Unable to obtain lock file access on '/private/tmp/e-…'") once the eval tool seals or deletes those folders. `stage-repo.sh` switches reuse off for the scaffold (`MSBUILDDISABLENODEREUSE=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`), but nothing reaches the agents' own builds yet: a `case.yaml` can only set `EVAL_*` variables, and exporting them in the launching shell was tried and doesn't pass through. Until that's solved, check for leftover servers after each run with `ps -eo pid,command | grep MSBuild.dll` and stop them.

The eval sandbox doesn't load your user-level plugins, so the shift-workflow hooks (plan first, worktrees, signed commits) don't run here. These cases measure task knowledge, not those rules.

## Graders

Agents can't run `dotnet test` inside the eval sandbox: the .NET test runner always talks to its test host over a local TCP socket, which the sandbox blocks (`SocketException (13): Permission denied`, then `Test Run Aborted`). `dotnet build` and `dotnet ef` work. So each prompt asks the agent to write tests and check its work with `dotnet build`, and grading looks at the code:

- `regex` graders read named files, for example the validation attribute's position in `CustomerContracts.cs`.
- `llm` graders read a named file where a pattern isn't enough, for example whether the tests in `CustomersApiTests.cs` cover 200, 404 and 400.
- A `builds` grader checks from the transcript that the agent's last build after its final change succeeded.
- Each case has a `tool_used: Skill` grader with `arm: with-only`. It's reported as whether the skill fired, and doesn't count toward the score.

None of this proves the tests would pass. Run `sh .claude/pre-pr-checks` on a change outside the sandbox for that.

Each case has at least one scoring grader that fails on the code at the pinned commit, so a case can't pass without the change.

## Moving the pinned commit

`PIN` in `stage-repo.sh` is the git tag `evals-baseline-1`, on the commit the cases were written against. It's a tag rather than a commit hash because PRs are squash-merged: the branch's own commits never land on `main`, and the tag keeps the commit reachable. The graders depend on that code; for example, case 03 expects `/health/live` to be mapped exactly as it is there. To move it:

1. Tag the new commit (`evals-baseline-2`), push the tag, and set `PIN` to it.
2. Re-check every case's expectations against the new code.
3. Run the whole suite with `--runs 1` and read each failing verdict against its transcript before trusting the scores.

## Adding a case

Copy an existing case folder to `<NN>-<slug>/`, including its `scaffold.sh`, and edit `case.yaml`. Keep `plugins: [../../skills]`, and make sure at least one grader fails on the pinned code.
