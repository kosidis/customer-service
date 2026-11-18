---
name: test-plan
description: WHEN asked for a QA test plan, test cases, boundary value analysis or a requirement traceability matrix for a PRD under `governance/prds/`, with or without its tech designs.
---

# Write a test plan from a PRD

Input: a PRD (`governance/prds/<prd-id>/prd.md`) and the tech designs whose frontmatter names it (`designs/*.md`, `prd: <prd-id>`). Output: `governance/prds/<prd-id>/test-plan.md`, built from `template.md` in this folder. Worked example: `governance/prds/prd-login-permissions/test-plan.md`.

The plan is written as a senior QA engineer would: black-box, from what a real user sees, with every requirement traced to tests.

## Steps

1. **Read the PRD.** Collect every requirement ID from its tables (`AUTH - 0010` is written `AUTH-0010` in the plan), the in-scope and out-of-scope lists, the assumptions and the success metrics.
2. **Read every tech design for the PRD.** Note the endpoints, status codes, pages, state changes and any rule stricter or looser than the PRD.
3. **Find the real input limits.** For each field a user types into, look up the limit in code if the code exists (`tokensave_search`): request contracts (`[StringLength]`, `[Required]`, `[Range]`), `HasMaxLength` in the `DbContext`, and `maxLength`/`required` on the web form. Check other worktrees under `.claude/worktrees/` when the feature is in flight. Write down each limit's source.
4. **Fill in `template.md`.** Group cases by user-visible area, with one ID prefix per area (`SI` sign in, `UM` user management, `BV` boundary values). Number them `<PREFIX>-01` upwards.
5. **Write the boundary tables** (rules below).
6. **Build the traceability matrix and the success-metrics table.**
7. **List open questions** wherever the PRD, a design and the code disagree, or the PRD is silent on something a user can do.
8. **Check traceability:** `bash .claude/skills/test-plan/scripts/check-traceability.sh <prd.md> <test-plan.md>`. It must print `OK`.

## What a case is

- Each case names one thing a user does and what they see. Steps use the named test data, never "a user" or "some value".
- Use the API level only where the PRD names a status code or a guarantee the UI can't show (for example "data unchanged", "stored as a hash"). Everything else goes through the UI.
- Expected results are observable: the page landed on, the message shown, the field marked, the status code, the row in the database.
- Write an expected result only from the PRD or a design. When neither says, add an open question and leave the expected result pointing to it.

Priority and level columns use these values; copy their definitions from the template.

| Priority | Meaning | Level | Meaning |
|---|---|---|---|
| P1 | Blocks release; the requirement's main promise or a security boundary | E2E | Full stack, nothing mocked |
| P2 | Edge case a user reaches in normal use | WEB | Web app, API mocked |
| P3 | Rare path or usability detail | API | API over HTTP, real database |
| | | MAN | Needs a restart, a long wait or reading logs |

## Edge cases to derive

Go through this list for every area. Write a case for each one that applies.

| Trigger in the requirement | Cases |
|---|---|
| A text input | Empty, whitespace only, leading and trailing spaces, different letter case, non-ASCII |
| A secret (password, token) | Never shown again in a form after an error, never in an API response, never in logs (use a unique marker value such as `LOGMARK-<uuid>` and search the logs) |
| A protected page or endpoint | No session, forged session, signed-out session replayed, each one per page and per endpoint |
| A role or permission | Allowed role succeeds; denied role sees no control, is refused on a direct URL, and gets the status code from the API with the data checked unchanged afterwards |
| A state (pending, active) | Before the change, the change failing, after the change, after signing in again |
| A uniqueness rule | Exact duplicate, case variant, padded variant, two requests at once |
| Persistence or "first start" | Restart of the service, restart with the state already present, empty store |
| Anything "must not expire" or "must not reveal" | Long idle with the clock advanced, browser restart, response time compared |
| Any form | Double submit, Back button after leaving, two tabs or two sessions |

## Boundary value analysis

- Use the two-value form: the last valid value and the first invalid one, at each end.
- The lower bound of a "not empty" rule is one non-whitespace character; test `""`, one space, mixed whitespace and one character.
- Take upper bounds from step 3. Give each boundary table a column for length.
- A value beyond a form's `maxLength` can't be typed. Test that the field stops at the limit (WEB) and send the longer value through the API (API).
- When two limits for the same value differ (PRD, contract, column, form), test both edges and raise an open question. A value one layer accepts and another rejects is the likeliest 500 in the system.

## Traceability rules

- Every requirement has at least one P1 case.
- Every case appears in the matrix; every ID in the matrix is a case.
- Every PRD success metric maps to cases.
- The matrix has one row per requirement, in PRD order, with a short summary and the highest level that covers it.

## Common mistakes

| Mistake | Fix |
|---|---|
| Cases that test internal methods or mocks | Rewrite as what the user does and sees; unit tests belong to the `test` skill |
| Inventing a limit or an error message | Cite the source from step 3, or raise an open question |
| Boundary tests only at the lower end | Every limit has both edges |
| A design rule that differs from the PRD tested silently | Test the design's rule and list the difference as an open question |
| Requirement covered only by P2 or P3 cases | Promote or add a P1 case |

Style: the house style in the output style, British English, tables for cases, prose only in the purpose, approach and open questions.
