---
id: test-plan-<prd-slug>
prd: <prd-id>
title: Test plan for <feature>
author: QA
created: <YYYY-MM-DD>
---

# Test plan: <feature>

## Purpose

<What the plan checks, from the user's side. Name the requirement ID ranges. Say which requirements are checked through the API, and why.>

## Scope

In scope:

- <One line per user-visible area.>

Out of scope, as the PRD excludes them: <list from the PRD's out-of-scope section>.

## Approach

<One paragraph: black-box, from the user's side, priority and level per case.>

| Priority | Meaning |
|---|---|
| P1 | Blocks release. Covers the requirement's main promise or a security boundary. |
| P2 | Edge case a user can reach in normal use. A failure needs a fix or a recorded decision before release. |
| P3 | Rare path or usability detail. |

| Level | Where it runs |
|---|---|
| E2E | Playwright against the full Compose stack (web, API, SQL Server). Nothing mocked. |
| WEB | Playwright against the web app with the API mocked through the Next.js test proxy (`next.onFetch`). |
| API | HTTP tests through `WebApplicationFactory` against SQL Server in Testcontainers. |
| MAN | Manual, for checks that need a restart, a long wait or reading container logs. |

<One paragraph on where the boundary limits come from.>

## Test environment and data

- <Stack, database state, browsers.>

| <Entity> | <Attribute> | State | <Secret or key value> |
|---|---|---|---|
| <name> | <value> | <value> | <value> |

## Test cases

### <Area>

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| <PREFIX>-01 | P1 | E2E | Happy path | <Steps with named data.> | <What the user sees.> |

## Boundary value analysis

<Where each limit comes from.>

### <Field> (<requirement IDs>)

| ID | Pri | Level | Value | Length | Expected |
|---|---|---|---|---|---|
| BV-01 | P1 | API | `""` | 0 | <Result.> |

## Traceability matrix

| Requirement | Summary | Test cases | Highest level |
|---|---|---|---|
| <REQ-0010> | <Short summary> | <IDs> | <Level> |

All <n> requirements have at least one P1 case.

### Success metrics

| PRD success metric | Test cases |
|---|---|
| <Metric> | <IDs> |

## Entry and exit criteria

Entry:

1. <Build and data preconditions.>

Exit:

1. Every P1 case passes.
2. No open defect of P1 or P2 severity remains without a recorded decision.
3. Every requirement in the traceability matrix has at least one passing case.
4. Every open question below has an answer, and the affected cases are updated to match.

## Open questions and risks

1. **<Short title> (<case IDs>).** <What the PRD, design and code each say, the likely failure, and who decides.>
