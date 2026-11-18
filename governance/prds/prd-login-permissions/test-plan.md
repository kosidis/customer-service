---
id: test-plan-login-permissions
prd: prd-login-permissions
title: Test plan for login and role-based permissions
author: QA
created: 2026-11-17
---

# Test plan: login and role-based permissions

## Purpose

This plan checks that `prd-login-permissions` works as a real user sees it: signing in and out, the forced password change, what Viewers and Admins can see and do, and creating users. Every requirement in the PRD (`AUTH-0010` to `AUTH-0120`, `AUTHZ-0010` to `AUTHZ-0090`, `USR-0010` to `USR-0070`) maps to at least one test case in the [traceability matrix](#traceability-matrix).

Where the PRD names an API status code (`AUTH-0040`, `AUTH-0100`, `AUTHZ-0050`, `USR-0050`), the test calls the API directly, because a user can't see that code through the web app.

## Scope

In scope:

- Sign-in, sign-out and session lifetime.
- Redirects and API rejections for unauthenticated users.
- Changing a password, and the forced change at first sign-in.
- Viewer and Admin behaviour on the customer screens and the customer API.
- The default Admin at first start and after a restart.
- The create-user page and its API endpoint.
- Boundary values for every input these features add, and for the customer fields an Admin uses.

Out of scope, as the PRD excludes them: registration, password reset, email verification, listing, editing or deleting users, password rules beyond "not empty", audit logging, single sign-on, login throttling and inactivity expiry.

## Approach

Test cases are black-box and written from the user's side. Each case has a priority and a level:

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

Boundary value analysis follows the two-value form: for each boundary, the last valid value and the first invalid one. The input limits come from the PRD where it sets one, and from the database and form limits where it doesn't. Where those two sources disagree, the case is listed under [Open questions](#open-questions-and-risks) as well.

## Test environment and data

- The Compose stack from the repository root, with `Database__ApplyMigrationsOnStartup=true`.
- An empty database for the default Admin cases (`DA-*`). A seeded database for everything else.
- Chromium, plus a second browser context for the cases that need two sessions.

| User | Role | State | Password |
|---|---|---|---|
| `admin` | Admin | Seeded at first start, change pending | `<default-admin-password>` |
| `ada` | Admin | Password changed | `Ada-Known-1` |
| `vic` | Viewer | Password changed | `Vic-Known-1` |
| `pat` | Viewer | Created by `ada`, change pending | `Pat-Initial-1` |

Customers: at least 25 rows, so the list has a second page, including `Grace Hopper` (`grace@example.com`).

For the logging checks, use passwords that contain a unique marker, for example `LOGMARK-<uuid>`, so a search of the logs is unambiguous.

## Test cases

### Sign in

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| SI-01 | P1 | E2E | Happy path | Sign in as `vic` / `Vic-Known-1`. | Lands on `/customers`. The header shows `vic` and a **Sign out** button. |
| SI-02 | P1 | E2E | Wrong password | Sign in as `vic` / `wrong-password`. | Stays on the sign-in page with the generic error. No session cookie is set. |
| SI-03 | P1 | E2E | Unknown username | Sign in as `nobody` / `wrong-password`. | Exactly the same message, position and field state as SI-02. |
| SI-04 | P2 | API | Username case | Sign in as `VIC` / `Vic-Known-1`. | Signs in as `vic`. |
| SI-05 | P2 | API | Username padding | Sign in as `  vic  ` / `Vic-Known-1`. | Signs in as `vic`. |
| SI-06 | P2 | API | Password case | Sign in as `vic` / `VIC-KNOWN-1`. | Rejected with the generic error. Passwords are case-sensitive. |
| SI-07 | P2 | API | Password padding | Sign in as `vic` / `Vic-Known-1 ` (trailing space). | Rejected with the generic error. Passwords aren't trimmed. |
| SI-08 | P2 | WEB | Empty fields | Submit with an empty username, then with an empty password. | The browser blocks the submit and marks the empty field. |
| SI-09 | P2 | WEB | Form after failure | Fail a sign-in as in SI-02. | The username is kept. The password field is empty. |
| SI-10 | P3 | WEB | Double submit | Double-click **Sign in**. | The button is disabled while signing in. One redirect, no error. |
| SI-11 | P3 | MAN | Response time | Time 20 attempts each for SI-02 and SI-03. | Median times differ by less than 100 ms, so timing doesn't reveal whether the account exists. |

### Sign out

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| SO-01 | P1 | E2E | Happy path | As `vic`, click **Sign out**. | Lands on `/login`. The header shows no username. |
| SO-02 | P1 | E2E | Back button | After SO-01, press the browser **Back** button. | No customer data shows. The user is sent to `/login`. |
| SO-03 | P1 | E2E | Replay old session | Save the session cookie, sign out, then restore the cookie and open `/customers`. | Redirected to `/login`. |
| SO-04 | P1 | API | Replay old token | Sign in, sign out, call `GET /api/customers` with the old token. | 401. |
| SO-05 | P2 | E2E | Two sessions | Sign in as `vic` in contexts A and B. Sign out in A. Reload in B. | A is signed out. B still works, because each sign-in is its own session. |
| SO-06 | P3 | WEB | Stale tab | Open two tabs, sign out in the first, click **Sign out** in the second. | Lands on `/login` with no error page. |

### Unauthenticated access

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| AC-01 | P1 | WEB | Customer pages | With no session, open each of `/`, `/customers`, `/customers?search=grace&page=2`, `/customers/new`, `/customers/<id>`, `/customers/<id>/edit`. | Each redirects to `/login`. No customer data appears, even briefly. |
| AC-02 | P1 | WEB | Forged cookie | Set the session cookie to `not-a-real-token` and open `/customers`. | Redirected to `/login`. |
| AC-03 | P1 | API | Customer API, no token | Call `GET /api/customers`, `GET /api/customers/<id>`, `POST /api/customers`, `PUT /api/customers/<id>`, `DELETE /api/customers/<id>` with no `Authorization` header. | 401 for each. The body holds no customer data. |
| AC-04 | P1 | API | Customer API, bad token | Repeat AC-03 with `Bearer ` (empty), `Basic dmljOnB3`, and `Bearer <random-64-chars>`. | 401 for each. |
| AC-05 | P1 | API | Writes have no effect | After AC-03, read the customer with a valid session. | The customer is unchanged and still exists. |
| AC-06 | P2 | WEB | Public pages | With no session, open `/login` and `/api/health`. | Both load with 200. No redirect loop. |

### Session lifetime

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| SS-01 | P1 | API | Long inactivity | Sign in, advance the API clock by 365 days, call `GET /api/customers`. | 200. |
| SS-02 | P1 | MAN | Overnight idle | Sign in, leave the browser idle for 24 hours, then open a customer. | The customer opens with no sign-in prompt. |
| SS-03 | P2 | MAN | Browser restart | Sign in, quit and reopen the browser, open `/customers`. | Still signed in. |
| SS-04 | P2 | MAN | Service restart | Sign in, restart the API and web containers, reload. | Still signed in. |

### Password storage and logging

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| PW-01 | P1 | API | Hash, not plain text | Create a user with password `LOGMARK-<uuid>`, then read its `Users` row. | `PasswordHash` doesn't contain the password and isn't a plain digest of it. |
| PW-02 | P1 | API | Salted | Create two users with the same password. | Their `PasswordHash` values differ. |
| PW-03 | P1 | MAN | Never logged | With marker passwords, do a successful sign-in, a failed sign-in, a password change and a user creation. Search the API and web container logs. | No log line contains any marker. |
| PW-04 | P2 | API | Never returned | Inspect the responses from sign-in, `GET /api/auth/me` and user creation. | No password or hash in any body. |
| PW-05 | P2 | WEB | Never echoed | Trigger an error on the sign-in, change-password and create-user forms. | Every password field is empty after the error. |

### Change password

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| CP-01 | P1 | E2E | Happy path | As `vic`, open the change-password page from the header, enter `Vic-Known-1` and `Vic-New-2`. Sign out, sign in with `Vic-New-2`. | Success. Signing in with `Vic-Known-1` is then rejected. |
| CP-02 | P1 | WEB | Wrong current password | Enter `wrong` as the current password and a valid new one. | Field error under **Current password**. The password is unchanged. |
| CP-03 | P1 | API | Empty new password | Send `newPassword: ""`. | 400 with a `newPassword` field error. |
| CP-04 | P1 | API | Whitespace-only new password | Send `" "`, `"   "` and `"\t"`. | 400 with a `newPassword` field error for each. |
| CP-05 | P1 | E2E | New equals initial | As `pat`, enter `Pat-Initial-1` as both current and new password. | Field error under **New password**. Still forced to change. |
| CP-06 | P2 | API | New equals current, later change | As `vic` (already changed), set the new password equal to the current one. | 400 with a `newPassword` field error. See open question 2. |
| CP-07 | P2 | API | Other sessions survive | Sign in as `vic` twice, change the password using session A. | Session B still returns 200. |
| CP-08 | P2 | WEB | Signed-out visit | With no session, open `/account/password`. | Redirected to `/login`. |
| CP-09 | P3 | API | Padded new password | Set the new password to `  pad  `. | Accepted. Signing in works with `  pad  ` and fails with `pad`. |
| CP-10 | P3 | API | Non-ASCII new password | Set the new password to `pässwörd-日本`. | Accepted. Signing in with it works. |

### Forced password change

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| FC-01 | P1 | E2E | First sign-in | Sign in as `pat` / `Pat-Initial-1`. | Lands on the change-password page, not the customer list. |
| FC-02 | P1 | WEB | Every page redirects | As `pat`, open `/`, `/customers`, `/customers/<id>`, `/customers/new`, `/customers/<id>/edit`, `/users/new`. | Each redirects to the change-password page. |
| FC-03 | P1 | E2E | Sign out allowed | As `pat`, click **Sign out** on the change-password page. | Signs out and lands on `/login`. |
| FC-04 | P1 | API | API blocked | With `pat`'s token, call every customer endpoint and `POST /api/users`. | 403 for each. Customer data is unchanged. |
| FC-05 | P1 | API | Account endpoints open | With `pat`'s token, call `GET /api/auth/me`, `POST /api/auth/change-password`, `POST /api/auth/logout`. | 200, 204 and 204. `me` reports the change as pending. |
| FC-06 | P1 | E2E | After the change | As `pat`, change the password to `Pat-New-2`. | Lands on the customer list with Viewer access. No further redirect in the same session. |
| FC-07 | P1 | E2E | Later sign-in | After FC-06, sign out and sign in with `Pat-New-2`. | Lands on `/customers` directly. Viewer controls only. |
| FC-08 | P2 | WEB | Failed change keeps the block | As `pat`, fail the change (wrong current password), then open `/customers`. | Still redirected to the change-password page. |
| FC-09 | P2 | API | Pending Admin | With the seeded `admin`'s token, call `POST /api/customers` and `POST /api/users`. | 403. The Admin role doesn't bypass the forced change. |
| FC-10 | P2 | E2E | Sign in again before changing | As `pat`, sign out without changing, then sign in with `Pat-Initial-1`. | Forced to the change-password page again. |

### Roles on customer screens

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| RL-01 | P1 | E2E | Viewer reads | As `vic`, list customers, search `grace`, open Grace Hopper, go to page 2. | All work. |
| RL-02 | P1 | E2E | Admin reads | Repeat RL-01 as `ada`. | All work. |
| RL-03 | P1 | E2E | Admin adds | As `ada`, add `Alan Turing`, `alan@example.com`. | The customer appears in the list. |
| RL-04 | P1 | E2E | Admin edits | As `ada`, change Grace Hopper's last name to `Hopper-Murray`. | The list shows the new name. |
| RL-05 | P1 | E2E | Admin deletes | As `ada`, search `alan`, go to the results page, delete Alan Turing. | The customer is gone. The list keeps the search and page. |
| RL-06 | P1 | WEB | Viewer sees no write controls | As `vic`, view the list and a customer's detail page. | No **Add customer**, **Edit** or **Delete** control anywhere. |
| RL-07 | P1 | WEB | Viewer direct visit | As `vic`, open `/customers/new` and `/customers/<id>/edit` by URL. | Refused. No form renders. |
| RL-08 | P1 | API | Viewer writes via API | With `vic`'s token, call `POST`, `PUT` and `DELETE` on customers. | 403 for each. A follow-up `GET` shows the data unchanged and the customer still present. |
| RL-09 | P1 | API | One role per user | Call `GET /api/auth/me` for `vic` and `ada`. | Each returns exactly one role, `Viewer` or `Admin`. |
| RL-10 | P2 | API | Invalid role on create | As `ada`, create a user with `role` missing, `""`, `SuperAdmin`, and `["Viewer","Admin"]`. | 400 with a `role` field error for each. No account is created. |

### Default Admin

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| DA-01 | P1 | E2E | First start | Start the stack on an empty database. Sign in as `admin` / `<default-admin-password>`. | Sign-in works with no manual database step, and the user is forced to change the password. |
| DA-02 | P1 | MAN | Restart after change | After DA-01, change the password to `Admin-New-2`. Restart the API. | `Admin-New-2` works. The default password is rejected. There is exactly one `admin` row. |
| DA-03 | P1 | API | Restart while pending | Start on an empty database, then start again without signing in. | Exactly one `admin` row, still pending. The default password still works. |
| DA-04 | P3 | API | Store not empty | Start against a database that holds only `vic`. | No `admin` user is created. |

### User management

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| UM-01 | P1 | E2E | Open the page | As `ada`, click **Users** in the header. | The create-user page shows **Username**, **Password**, and a **Role** choice of Viewer or Admin. |
| UM-02 | P1 | E2E | Create a Viewer | Create `newview` / `NewView-1` / Viewer. Sign out, sign in as `newview`, change the password. | Confirmation shows and the form empties. The new user signs in straight away, is forced to change the password, then has Viewer access. |
| UM-03 | P1 | E2E | Create an Admin | Create `newadmin` / `NewAdmin-1` / Admin and repeat UM-02. | As UM-02, ending with Admin controls visible. |
| UM-04 | P1 | WEB | Duplicate username | Create `vic` again. | Field error under **Username**. No second account. |
| UM-05 | P1 | API | Duplicate, other case | Create `VIC`. | 409 or a `username` field error. No second account. |
| UM-06 | P2 | API | Duplicate with padding | Create `  vic  `. | As UM-05. |
| UM-07 | P1 | WEB | Form after error | Trigger UM-04. | The username and role are kept. The password is empty. |
| UM-08 | P1 | MAN | Survives restart | Create `persist` / Viewer, restart the API and the database container, sign in as `persist`. | Signs in with the Viewer role. |
| UM-09 | P1 | WEB | Viewer refused | As `vic`, open `/users/new` by URL. | A 403 page. No form. |
| UM-10 | P1 | API | Viewer refused, API | With `vic`'s token, call `POST /api/users`. | 403. No account is created. |
| UM-11 | P1 | WEB | Signed out, page | With no session, open `/users/new`. | Redirected to `/login`. |
| UM-12 | P1 | API | Signed out, API | Call `POST /api/users` with no token. | 401. |
| UM-13 | P1 | WEB | Viewer sees no link | As `vic`, look at the header on every page. | No **Users** link. |
| UM-14 | P2 | API | Concurrent duplicates | As `ada`, send two `POST /api/users` for `race` at the same moment. | One 201 and one 409. Never a 500. One account. |
| UM-15 | P2 | API | Mixed-case username | Create `NewUser`, then sign in as `newuser`. | Sign-in works. |

## Boundary value analysis

The PRD's only rule for usernames and passwords is "not empty, not whitespace only" (`AUTH-0080`, `USR-0070`). The lower boundary is therefore one non-whitespace character. The upper limits below come from the database and the web forms, not the PRD.

### Username (create user, `USR-0070`)

| ID | Pri | Level | Value | Length | Expected |
|---|---|---|---|---|---|
| BV-01 | P1 | API | `""` | 0 | Rejected, `username` field error. |
| BV-02 | P1 | API | `" "` | 1, whitespace | Rejected, `username` field error. |
| BV-03 | P1 | API | `"\t \t"` | 3, whitespace | Rejected, `username` field error. |
| BV-04 | P1 | API | `"a"` | 1 | Accepted. Signs in as `a`. |
| BV-05 | P2 | API | `" a "` | 3, padded | Accepted, stored as `a`. |
| BV-06 | P2 | API | 100 × `u` | 100 (column limit) | Accepted. Signs in. |
| BV-07 | P1 | API | 101 × `u` | 101 | A clear `username` field error, never a 500. See open question 1. |

### Password (create user and new password, `USR-0070`, `AUTH-0080`)

| ID | Pri | Level | Value | Length | Expected |
|---|---|---|---|---|---|
| BV-08 | P1 | API | `""` | 0 | Rejected, field error. |
| BV-09 | P1 | API | `" "` | 1, whitespace | Rejected, field error. |
| BV-10 | P1 | API | `"\t\n "` | 3, whitespace | Rejected, field error. |
| BV-11 | P1 | API | `"x"` | 1 | Accepted. Signs in with `x`. |
| BV-12 | P2 | E2E | 256 × `p` | 256 (form limit) | Accepted. Signing in through the form with it works. |
| BV-13 | P2 | WEB | Type 257 × `p` into the sign-in or change-password field | 257 | The field stops at 256 characters. |
| BV-14 | P1 | API | 257 × `p` on create user | 257 | Rejected with a field error, or the user could never type it into the sign-in form. See open question 3. |

### Sign-in fields (`AUTH-0010`, `AUTH-0050`)

| ID | Pri | Level | Value | Expected |
|---|---|---|---|---|
| BV-15 | P2 | WEB | Username of 256 characters, any password | The generic sign-in error, no error page. |
| BV-16 | P2 | WEB | Type 257 characters into **Username** | The field stops at 256 characters. |

### Customer fields as an Admin (`AUTHZ-0030`, `AUTHZ-0090`)

Run each case on both the add and the edit form. Values beyond the form's `maxLength` are sent through the API with `ada`'s token, because the form doesn't let a user type them.

| ID | Pri | Level | Field | Below | At lower bound | At upper bound | Above |
|---|---|---|---|---|---|---|---|
| BV-17 | P2 | WEB, API | First name (required, 1 to 100) | `""` rejected | 1 char accepted | 100 accepted | 101 rejected (API) |
| BV-18 | P2 | WEB, API | Last name (required, 1 to 100) | `""` rejected | 1 char accepted | 100 accepted | 101 rejected (API) |
| BV-19 | P2 | WEB, API | Email (required, valid, up to 256) | `""` rejected | `a@b.co` accepted | 256 accepted | 257 rejected (API) |
| BV-20 | P3 | WEB, API | Phone (optional, up to 32) | n/a | empty accepted | 32 accepted | 33 rejected (API) |
| BV-21 | P3 | WEB, API | Domain (optional, up to 253) | n/a | empty accepted | 253 accepted | 254 rejected (API) |
| BV-22 | P2 | WEB | First name `"   "` (whitespace only) | n/a | n/a | n/a | Rejected with a field error |

### Search (`AUTHZ-0020`)

| ID | Pri | Level | Value | Expected |
|---|---|---|---|---|
| BV-23 | P2 | E2E | Empty | The full first page. |
| BV-24 | P3 | WEB | `g` (1 character) | Matching customers only. |
| BV-25 | P3 | WEB | 100 characters | An empty result, no error. |
| BV-26 | P2 | WEB | 101 characters | A clear message, never the error page. See open question 4. |

## Traceability matrix

| Requirement | Summary | Test cases | Highest level |
|---|---|---|---|
| AUTH-0010 | Sign in with own credentials | SI-01, SI-04, SI-05, SI-06, SI-07, SI-08, SI-09, SI-10, BV-15, BV-16 | E2E |
| AUTH-0020 | Sign out; old session gives no access | SO-01, SO-02, SO-03, SO-04, SO-05, SO-06, FC-03 | E2E |
| AUTH-0030 | Signed-out page visits redirect to sign-in | AC-01, AC-02, AC-06, CP-08, UM-11 | WEB |
| AUTH-0040 | Signed-out API calls get 401 | AC-03, AC-04, AC-05, SO-04, UM-12 | API |
| AUTH-0050 | Generic sign-in error | SI-02, SI-03, SI-11, BV-15 | E2E |
| AUTH-0060 | No inactivity expiry | SS-01, SS-02, SS-03, SS-04 | MAN |
| AUTH-0070 | Salted hashes, never logged | PW-01, PW-02, PW-03, PW-04, PW-05 | API, MAN |
| AUTH-0080 | Change own password; new not empty | CP-01, CP-02, CP-03, CP-04, CP-07, CP-08, CP-09, CP-10, BV-08, BV-09, BV-10, BV-11, BV-12, BV-13 | E2E |
| AUTH-0090 | Pending user redirected to change password | FC-01, FC-02, FC-03, FC-08, FC-10 | E2E |
| AUTH-0100 | Pending user gets 403 from the API | FC-04, FC-05, FC-09 | API |
| AUTH-0110 | New password differs from initial | CP-05, CP-06 | E2E |
| AUTH-0120 | Normal access after the change | FC-06, FC-07, UM-02, UM-03 | E2E |
| AUTHZ-0010 | One role per user | RL-09, RL-10, UM-01 | E2E |
| AUTHZ-0020 | Viewers and Admins read customers | RL-01, RL-02, BV-23, BV-24, BV-25, BV-26 | E2E |
| AUTHZ-0030 | Admins edit | RL-04, BV-17, BV-18, BV-19, BV-20, BV-21, BV-22 | E2E |
| AUTHZ-0040 | Admins delete | RL-05 | E2E |
| AUTHZ-0050 | Viewer writes get 403, data unchanged | RL-08 | API |
| AUTHZ-0060 | No write controls or pages for Viewers | RL-06, RL-07 | WEB |
| AUTHZ-0070 | Default Admin on first start | DA-01, DA-03 | E2E |
| AUTHZ-0080 | Restart doesn't recreate or reset the Admin | DA-02, DA-03, DA-04 | MAN, API |
| AUTHZ-0090 | Admins add | RL-03, BV-17, BV-18, BV-19, BV-20, BV-21, BV-22 | E2E |
| USR-0010 | Create-user page | UM-01, UM-02, UM-03 | E2E |
| USR-0020 | New user signs in straight away with the chosen role | UM-02, UM-03, UM-15, BV-04, BV-06 | E2E |
| USR-0030 | Duplicate username rejected | UM-04, UM-05, UM-06, UM-07, UM-14 | WEB, API |
| USR-0040 | Users survive a restart | UM-08 | MAN |
| USR-0050 | Viewer gets 403, signed-out gets 401 or a redirect | UM-09, UM-10, UM-11, UM-12 | WEB, API |
| USR-0060 | No create-user link for Viewers | UM-13 | WEB |
| USR-0070 | Empty or whitespace username or password rejected | BV-01, BV-02, BV-03, BV-04, BV-05, BV-07, BV-08, BV-09, BV-10, BV-11, BV-14 | API |

All 28 requirements have at least one P1 case.

### Success metrics

| PRD success metric | Test cases |
|---|---|
| Unauthenticated access: 0 requests succeed across all customer endpoints | AC-03, AC-04, AC-05 |
| Viewer write attempts: 100% rejected | RL-07, RL-08 |
| Admin workflow regression: existing list, search, add, edit and delete pass for an Admin | RL-02, RL-03, RL-04, RL-05, and the existing customer suites run as `ada` |
| User creation: a new user signs in with the chosen role on the first attempt | UM-02, UM-03 |
| Forced change: 100% blocked before, 0% after | FC-02, FC-04, FC-06, FC-07 |

## Entry and exit criteria

Entry:

1. The build under test runs in Compose with migrations applied.
2. The test users and customers in [Test environment and data](#test-environment-and-data) exist, except for the `DA-*` cases, which start on an empty database.

Exit:

1. Every P1 case passes.
2. No open defect of P1 or P2 severity remains without a recorded decision.
3. Every requirement in the traceability matrix has at least one passing case.
4. Every open question below has an answer, and the affected cases are updated to match.

## Open questions and risks

1. **Usernames longer than 100 characters (BV-07).** The PRD says no username rule applies beyond "not empty", but the `Users.Username` column holds 100 characters and the request contract sets no length. A 101-character username probably fails at the database and returns a 500. The product owner needs to choose between a field error at 100 and a wider column.
2. **New password equal to the current one (CP-06).** `AUTH-0110` only forbids reusing the initial password. `tech-design-1` rejects reuse of the current password on every change. CP-06 follows the design; confirm that is the intended rule.
3. **Password longer than 256 characters (BV-14).** The sign-in and change-password fields stop at 256 characters. If the create-user page or the API accepts a longer password, that user can never sign in through the form. The create-user form and the API need the same 256 limit, or the sign-in form needs a higher one.
4. **Search over 100 characters (BV-26).** The search box has no length limit, while the API rejects a search over 100 characters with a 400. The expected screen for this case isn't specified anywhere.
5. **Signing out one session (SO-05).** The PRD says "a signed-out session must no longer grant access" and doesn't say whether signing out ends the user's other sessions. SO-05 expects they survive, matching `tech-design-1`.
6. **Visiting `/login` while signed in.** The PRD doesn't say what happens. No case asserts a behaviour until that's decided.
7. **No throttling.** The PRD excludes lockout and throttling, so nothing here tests repeated failed sign-ins. That is a known risk if the web app is ever exposed publicly.
