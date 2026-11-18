---
id: test-plan-android-app
prd: prd-android-app
title: Test plan for the Android app with login and role-based permissions
author: QA
created: 2026-11-18
---

# Test plan: Android app with login and role-based permissions

## Purpose

This plan checks that `prd-android-app` works as a person holding a phone sees it: signing in and out, the forced password change, what Viewers and Admins can see and do, managing customers and creating users. Every requirement in the PRD (`AAU-0010` to `AAU-0110`, `AAZ-0010` to `AAZ-0050`, `ACU-0010` to `ACU-0080`, `AUS-0010` to `AUS-0040`) maps to at least one test case in the [traceability matrix](#traceability-matrix).

Where a requirement depends on a status code the app can't show (`AAU-0060`, `AAZ-0040`, `AUS-0040`), the plan calls the API directly as well. Where a requirement depends on the phone itself (`AAU-0050`, `AAU-0070`, `AAU-0110`), the plan uses manual device checks.

## Scope

In scope:

- Sign-in, sign-out and session lifetime on the phone.
- The forced password change and the voluntary password change.
- Viewer and Admin behaviour on the customer screens.
- Listing, paging and searching customers.
- Adding, editing and deleting customers.
- The create-user screen.
- Errors from the API and from the network.
- Boundary values for every input the app sends.

Out of scope, as the PRD excludes them: tablet layouts and other platforms, offline use and caching, push notifications, biometric unlock and single sign-on, registration, password reset, listing, editing or deleting users, new roles, store publishing and inactivity expiry.

## Approach

Test cases are black-box and written from the user's side. Each case has a priority and a level. The login PRD's API tests already cover the API's own rules, so this plan repeats an API case only where the app's requirement depends on it.

| Priority | Meaning |
|---|---|
| P1 | Blocks release. Covers the requirement's main promise or a security boundary. |
| P2 | Edge case a user can reach in normal use. A failure needs a fix or a recorded decision before release. |
| P3 | Rare path or usability detail. |

| Level | Where it runs |
|---|---|
| E2E | Instrumented UI tests on an Android emulator against the full Compose stack (API and SQL Server). Nothing mocked. |
| UI | Instrumented UI tests on an emulator with the API replaced by a local mock server that returns scripted responses. |
| API | HTTP tests through `WebApplicationFactory` against SQL Server in Testcontainers, reused from the login PRD's suite. |
| MAN | Manual, on an emulator or a real device, for checks that need a reboot, a long wait, a network change, a proxy or reading `logcat`. |

Boundary value analysis follows the two-value form: for each boundary, the last valid value and the first invalid one. The customer field limits come from the API's request contracts, listed under [Boundary value analysis](#boundary-value-analysis). Where the app's form or the PRD sets no limit, the case points to an [open question](#open-questions-and-risks).

## Test environment and data

- The Compose stack from the repository root with `Database__ApplyMigrationsOnStartup=true`, on the login PRD's API.
- An emulator on a current Android release, plus one real device for the `MAN` cases.
- A debug build for the cases that inspect app storage, and a release build for everything else.
- A second emulator or device for the two-device cases.

| User | Role | State | Password |
|---|---|---|---|
| `admin` | Admin | Seeded at first start, change pending | `<default-admin-password>` |
| `ada` | Admin | Password changed | `Ada-Known-1` |
| `vic` | Viewer | Password changed | `Vic-Known-1` |
| `pat` | Viewer | Created by `ada`, change pending | `Pat-Initial-1` |

Customers: at least 45 rows, so the list has three pages, including `Grace Hopper` (`grace@example.com`) and `Ada Lovelace` (`lovelace@example.com`).

For the logging checks, use passwords that contain a unique marker, for example `LOGMARK-<uuid>`, so a search of `logcat` is unambiguous.

## Test cases

### Sign in

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| SI-01 | P1 | E2E | Happy path | Sign in as `vic` / `Vic-Known-1`. | The customer list shows. The app bar shows `vic` and a sign-out action. |
| SI-02 | P1 | E2E | Wrong password | Sign in as `vic` / `wrong-password`. | The sign-in screen stays with the generic error. No customer screen opens. |
| SI-03 | P1 | E2E | Unknown username | Sign in as `nobody` / `wrong-password`. | Exactly the same message, position and field state as SI-02. |
| SI-04 | P2 | UI | Empty fields | Tap **Sign in** with an empty username, then with an empty password. | No request goes out. The empty field is marked. |
| SI-05 | P2 | UI | Form after failure | Fail a sign-in as in SI-02. | The username is kept. The password field is empty. |
| SI-06 | P3 | UI | Double tap | Double-tap **Sign in**. | One request goes out. One navigation to the list. |
| SI-07 | P2 | E2E | Username padding | Sign in as `  vic  ` / `Vic-Known-1`. | Signs in as `vic`, as the login API does. |

### Sign out

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| SO-01 | P1 | E2E | Happy path | As `vic`, tap the sign-out action. | The sign-in screen shows. The app bar shows no username. |
| SO-02 | P1 | E2E | Back button | After SO-01, press the system **Back** button. | No customer data shows. The app closes or stays on the sign-in screen. |
| SO-03 | P1 | E2E | Relaunch after sign-out | After SO-01, force-stop the app and open it again. | The sign-in screen shows. |
| SO-04 | P1 | API | Replay old token | Sign in as `vic`, sign out, then call `GET /api/customers` with the old token. | 401. |
| SO-05 | P2 | E2E | Two devices | Sign in as `vic` on devices A and B. Sign out on A. Refresh the list on B. | A is signed out. B still works, because each sign-in is its own session. |

### Unauthenticated access

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| AC-01 | P1 | E2E | Fresh launch | Install the app and open it. | The sign-in screen shows. No customer data appears, even briefly. |
| AC-02 | P1 | UI | Deep link, no session | With no session, launch the app through an intent that targets the customer list, a customer detail and the add form. | Each lands on the sign-in screen. |
| AC-03 | P1 | UI | 401 on the list | As `vic`, script the mock server to answer the next list request with 401. | The app returns to the sign-in screen. |
| AC-04 | P1 | UI | 401 while saving | As `ada`, open the edit form, script a 401 for the save. | The app returns to the sign-in screen. The edit is not shown as saved. |
| AC-05 | P2 | UI | Sign in after 401 | After AC-03, sign in as `vic`. | The customer list shows. |

### Session persistence

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| SS-01 | P1 | E2E | App restart | Sign in as `vic`, force-stop the app, open it again. | The customer list shows with no sign-in prompt. |
| SS-02 | P1 | MAN | Device reboot | Sign in as `vic`, reboot the device, open the app. | The customer list shows with no sign-in prompt. |
| SS-03 | P2 | MAN | Process death | Sign in, put the app in the background, kill its process with `adb shell am kill`, reopen it. | The customer list shows with no sign-in prompt. |
| SS-04 | P2 | MAN | API restart | Sign in, restart the API container, refresh the list. | The list loads. No sign-in prompt. |
| SS-05 | P1 | MAN | Overnight idle | Sign in, leave the app idle for 24 hours, then open a customer. | The customer opens with no sign-in prompt. |
| SS-06 | P2 | E2E | Reinstall | Sign in, uninstall the app, install it again, open it. | The sign-in screen shows. |

### Session credential and transport

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| SC-01 | P1 | MAN | Storage | Sign in as `vic` on a release build. Try to read the app's storage with `adb shell run-as <package>`. On a debug build, search the app's files and preferences for the token. | The release build refuses access. The debug build holds no plain-text token. |
| SC-02 | P1 | MAN | Logs | Sign in with a password that holds `LOGMARK-<uuid>`, use the app, then search `adb logcat` output. | Neither the password marker nor the session token appears. |
| SC-03 | P1 | UI | Plain HTTP refused | Point a build at an `http://` base URL and try to sign in. | No request leaves the device in clear text. The app shows an error. |
| SC-04 | P1 | MAN | Traffic capture | Route the device through a capture proxy, then sign in and use every screen. | Every request to the API uses TLS. No request carries credentials in clear text. |
| SC-05 | P2 | MAN | Backup | Sign in, then run an Android backup of the app and inspect it. | The backup holds no session token. See open question 4. |

### Forced password change

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| FP-01 | P1 | E2E | Initial password | Sign in as `pat` / `Pat-Initial-1`. | Only the change-password screen and a sign-out action show. No customer list. |
| FP-02 | P1 | E2E | No way around it | As `pat`, press **Back**, then try the deep link from AC-02. | The app stays on the change-password screen. |
| FP-03 | P1 | API | API blocks `pat` | Call `GET /api/customers` and `POST /api/customers` with `pat`'s token. | 403 for each. |
| FP-04 | P1 | E2E | Change succeeds | As `pat`, change the password to `Pat-Known-1`. | The customer list shows for a Viewer, with no new sign-in. |
| FP-05 | P1 | E2E | Later sign-in | Sign out, then sign in as `pat` / `Pat-Known-1`. | The customer list shows with no redirect. |
| FP-06 | P2 | E2E | Kill mid-change | As `pat`, open the change screen, force-stop the app, open it again. | The change-password screen shows again. |
| FP-07 | P2 | E2E | Same as initial | As `pat`, set the new password to `Pat-Initial-1`. | A field error shows. The password is unchanged. |

### Change password

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| CP-01 | P1 | E2E | Happy path | As `ada`, open the change-password screen, enter `Ada-Known-1` and `Ada-Known-2`. | The change succeeds. Signing in as `ada` / `Ada-Known-2` works. |
| CP-02 | P1 | UI | Wrong current password | As `ada`, enter a wrong current password. | The API's field error shows on the current-password field. |
| CP-03 | P2 | UI | Empty new password | Submit with an empty new password. | The API's field error shows on the new-password field. |
| CP-04 | P2 | UI | Whitespace-only new password | Submit with `"   "` as the new password. | The API's field error shows. The password is unchanged. |
| CP-05 | P2 | UI | Fields after failure | Fail CP-02. | Both password fields are empty. |
| CP-06 | P3 | UI | Double tap | Double-tap the submit button. | One request goes out. |

### Viewer

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| VW-01 | P1 | E2E | No write controls | As `vic`, open the list and a customer. | No add, edit or delete control shows on any screen. |
| VW-02 | P1 | E2E | Read access | As `vic`, open the list, search `grace` and open `Grace Hopper`. | The list, the search result and the detail all show. |
| VW-03 | P1 | UI | 403 on a write | Script a 403 for a delete request while signed in as `vic`. | The app says the action isn't permitted. The list is unchanged. |
| VW-04 | P1 | API | Viewer write refused | Call `POST`, `PUT` and `DELETE` on the customer API with `vic`'s token. | 403 for each. A later read shows the customer unchanged. |
| VW-05 | P1 | UI | Role from the API | Script a sign-in response with role `Viewer`, then one with role `Admin`. | Write controls hide for `Viewer` and show for `Admin`. |
| VW-06 | P2 | MAN | Tampered role | On a debug build signed in as `vic`, edit the stored role to `Admin`, then try a delete. | The API answers 403. The app says the action isn't permitted. Nothing is deleted. |
| VW-07 | P1 | E2E | No create-user entry | As `vic`, open every menu and screen. | No entry for creating users shows. |

### Admin customers

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| CU-01 | P1 | E2E | Add | As `ada`, add `Katherine Johnson`, `katherine@example.com`. | The customer shows in the list afterwards. |
| CU-02 | P1 | E2E | Edit | As `ada`, open `Grace Hopper` for editing. | The form starts with the current values. After changing the phone number and saving, the new value shows. |
| CU-03 | P1 | E2E | Delete | As `ada`, delete `Katherine Johnson` and confirm. | The customer is gone from the list. |
| CU-04 | P1 | E2E | Cancel delete | As `ada`, start a delete and cancel. | The customer stays. |
| CU-05 | P1 | E2E | Delete keeps context | As `ada`, search `a`, go to page 2, delete a customer and confirm. | The list returns with the same search and page. |
| CU-06 | P1 | UI | Duplicate email, add | Script a 409 for adding `grace@example.com`. | An error shows on the email field. The typed values stay. |
| CU-07 | P2 | UI | Duplicate email, edit | Script a 409 when editing `Ada Lovelace` to `grace@example.com`. | An error shows on the email field. The typed values stay. |
| CU-08 | P1 | UI | Field errors | Script a 400 with errors for first name and email. | Each error shows next to its field. The typed values stay. |
| CU-09 | P2 | UI | Rotation | Fill half of the add form, rotate the phone. | The typed values stay. |
| CU-10 | P2 | MAN | Process death mid-form | Fill half of the add form, kill the process in the background, reopen. | See open question 5. |
| CU-11 | P3 | UI | Double tap save | Double-tap **Save** on the add form. | One customer is created. |
| CU-12 | P2 | E2E | Email variants | Add `Ada Two` with `LOVELACE@example.com`, then with ` lovelace@example.com `. | Both are rejected with an email field error, as the API normalises the address. |

### Search and paging

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| SP-01 | P1 | E2E | First page | Open the list with 45 customers. | The first page shows, and a way to reach the next page shows. |
| SP-02 | P1 | E2E | Move between pages | Go to page 2, then page 3, then back to page 1. | Each page shows different customers. No customer appears on two pages. |
| SP-03 | P1 | E2E | Search | Search `grace`. | Only customers matching `grace` show. |
| SP-04 | P2 | E2E | No match | Search `zzzz-no-match`. | An empty state shows. No error. |
| SP-05 | P2 | E2E | Search case and padding | Search `GRACE`, then `  grace  `. | Both show the same result as SP-03. See open question 6. |
| SP-06 | P2 | E2E | Search resets the page | On page 3, search `grace`. | The result starts at its first page. |
| SP-07 | P2 | E2E | Non-ASCII | Add a customer named `Γρηγόρης`, then search `Γρηγόρης`. | The customer is found. |
| SP-08 | P3 | E2E | Last page | Go to the last page. | There is no way to go further. |

### Errors from the network and the API

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| ER-01 | P1 | MAN | Airplane mode | Open the list, switch on airplane mode, pull to refresh, switch it off and retry. | A generic message and a retry action show. Retry loads the list. |
| ER-02 | P1 | UI | 500 on the list | Script a 500 with a body that holds an internal message. | A generic message shows. The internal message does not. |
| ER-03 | P1 | UI | 500 on save | Script a 500 while saving. | A generic message shows. The typed values stay. A retry works. |
| ER-04 | P1 | UI | Timeout | Script a response that never arrives. | The app stops waiting, shows the generic message and offers a retry. |
| ER-05 | P2 | UI | 503 | Script a 503 on the list. | The same generic message as ER-02. |
| ER-06 | P2 | UI | Sign in during an outage | Script no response for a sign-in. | A network error shows. It does not say the password is wrong. |
| ER-07 | P2 | UI | Malformed body | Script a 200 with a body that is not valid JSON. | The app shows the generic message and doesn't crash. |

### User management

| ID | Pri | Level | Scenario | Steps and data | Expected result |
|---|---|---|---|---|---|
| UM-01 | P1 | E2E | Create | As `ada`, open the create-user screen and create `sam`, `Sam-Initial-1`, Viewer. | The app confirms the user was created. |
| UM-02 | P1 | E2E | New user signs in | Sign in as `sam` in the app, then in the web app. | Both sign-ins succeed. Both show the forced password change. |
| UM-03 | P1 | E2E | New Admin | As `ada`, create `eve`, `Eve-Initial-1`, Admin. Sign in as `eve` and change the password. | `eve` sees the Admin controls and the create-user entry. |
| UM-04 | P1 | E2E | Duplicate username | As `ada`, create a user named `vic`. | A field error shows on the username. No second account exists. |
| UM-05 | P2 | E2E | Username case | As `ada`, create a user named `VIC`. | See open question 3. |
| UM-06 | P2 | E2E | Username padding | As `ada`, create a user named `  vic  `. | See open question 3. |
| UM-07 | P2 | E2E | Empty values | Submit with an empty username, then with an empty password, then with `"   "` as the password. | A field error shows each time. No account is created. |
| UM-08 | P2 | UI | Role choice | Open the create-user screen. | The role choice offers Viewer and Admin only. |
| UM-09 | P1 | API | Viewer refused | Call the create-user endpoint with `vic`'s token. | 403. No account is created. |
| UM-10 | P3 | UI | Double tap | Double-tap the submit button. | One account is created. |

## Boundary value analysis

The customer limits come from `CreateCustomerRequest` and `UpdateCustomerRequest` in `apps/api/src/CustomerService.Api/Contracts/CustomerContracts.cs`: first and last name 1 to 100 characters, email up to 256, phone number up to 32, domain up to 253, and search up to 100. `Required` also rejects whitespace-only text. These limits are on the API's roles branch, so the cases run at the API level and, where a form exists, through the app. The create-user contract isn't in that branch, so its limits are open questions.

### First and last name (ACU-0030, ACU-0040, ACU-0060)

| ID | Pri | Level | Value | Length | Expected |
|---|---|---|---|---|---|
| BV-01 | P1 | API | `""` | 0 | Rejected with a field error on the name. |
| BV-02 | P2 | API | `" "` | 1 | Rejected with a field error on the name. |
| BV-03 | P1 | API | `A` | 1 | Accepted. |
| BV-04 | P1 | API | 100 letters | 100 | Accepted. |
| BV-05 | P1 | API | 101 letters | 101 | Rejected with a field error on the name. |
| BV-06 | P2 | API | Last name `""` | 0 | Rejected with a field error on the last name. |
| BV-07 | P2 | API | Last name, 100 letters | 100 | Accepted. |
| BV-08 | P2 | API | Last name, 101 letters | 101 | Rejected with a field error on the last name. |

### Email (ACU-0060, ACU-0070)

| ID | Pri | Level | Value | Length | Expected |
|---|---|---|---|---|---|
| BV-09 | P1 | API | `""` | 0 | Rejected with a field error on the email. |
| BV-10 | P2 | API | A valid address of 256 characters | 256 | Accepted. |
| BV-11 | P2 | API | A valid-looking address of 257 characters | 257 | Rejected with a field error on the email. |
| BV-12 | P2 | UI | `no-at-sign` | 10 | A field error shows on the email. |

### Phone number (ACU-0060)

| ID | Pri | Level | Value | Length | Expected |
|---|---|---|---|---|---|
| BV-13 | P2 | API | Omitted | 0 | Accepted. |
| BV-14 | P2 | API | `+` followed by 31 digits | 32 | Accepted. |
| BV-15 | P2 | API | `+` followed by 32 digits | 33 | Rejected with a field error on the phone number. |

### Domain (ACU-0060)

| ID | Pri | Level | Value | Length | Expected |
|---|---|---|---|---|---|
| BV-16 | P2 | API | A 253-character domain | 253 | Accepted. |
| BV-17 | P2 | API | A 254-character domain | 254 | Rejected with a field error on the domain. |

### Search (ACU-0020)

| ID | Pri | Level | Value | Length | Expected |
|---|---|---|---|---|---|
| BV-18 | P2 | API | 100 letters | 100 | Accepted. The list shows the matches. |
| BV-19 | P2 | API | 101 letters | 101 | Rejected with 400. What the app shows is open question 1. |

### Username and password on the create-user screen (AUS-0010, AUS-0030)

| ID | Pri | Level | Value | Length | Expected |
|---|---|---|---|---|---|
| BV-20 | P1 | API | Username `a` | 1 | Accepted. |
| BV-21 | P1 | API | Username `""` | 0 | Rejected with a field error on the username. |
| BV-22 | P2 | API | Password `x` | 1 | Accepted, as the login PRD sets no rule beyond "not empty". |
| BV-23 | P2 | API | Password `" "` | 1 | Rejected with a field error on the password. |
| BV-24 | P2 | API | A 1,000-character password | 1000 | See open question 2. |

### Form limits in the app (ACU-0060)

| ID | Pri | Level | Value | Length | Expected |
|---|---|---|---|---|---|
| BV-25 | P2 | UI | Type 101 letters into the first name field | 101 | See open question 1. |

## Traceability matrix

| Requirement | Summary | Test cases | Highest level |
|---|---|---|---|
| AAU-0010 | Sign in with held credentials | SI-01, SI-04, SI-05, SI-06, SI-07 | E2E |
| AAU-0020 | Sign out ends access, also after restart | SO-01, SO-02, SO-03, SO-04, SO-05 | E2E |
| AAU-0030 | Launch with no session shows sign-in | AC-01, AC-02, SO-02 | E2E |
| AAU-0040 | Generic failed sign-in error | SI-02, SI-03, ER-06 | E2E |
| AAU-0050 | Session survives restart until sign-out | SS-01, SS-02, SS-03, SS-04, SS-05, SS-06 | E2E |
| AAU-0060 | A 401 returns the user to sign-in | AC-03, AC-04, AC-05 | UI |
| AAU-0070 | Credential stored privately, never logged | SC-01, SC-02, SC-05 | MAN |
| AAU-0080 | Change own password | CP-01, CP-02, CP-03, CP-04, CP-05, CP-06, FP-07 | E2E |
| AAU-0090 | Initial password limits the app | FP-01, FP-02, FP-03, FP-06 | E2E |
| AAU-0100 | Access matches role after the change | FP-04, FP-05 | E2E |
| AAU-0110 | HTTPS only | SC-03, SC-04 | MAN |
| AAZ-0010 | Viewers and Admins can read | VW-02 | E2E |
| AAZ-0020 | Admins can add, edit and delete | CU-01, CU-02, CU-03 | E2E |
| AAZ-0030 | No write controls for Viewers | VW-01 | E2E |
| AAZ-0040 | A 403 shows a message, data unchanged | VW-03, VW-04, VW-06 | API |
| AAZ-0050 | Role comes from the API | VW-05, VW-06 | UI |
| ACU-0010 | Paged list | SP-01, SP-02, SP-08 | E2E |
| ACU-0020 | Search | SP-03, SP-04, SP-05, SP-06, SP-07, BV-18, BV-19 | E2E |
| ACU-0030 | Add customer | CU-01, CU-11, BV-01, BV-02, BV-03, BV-04, BV-05 | E2E |
| ACU-0040 | Edit customer | CU-02, CU-07, CU-12, BV-06, BV-07, BV-08 | E2E |
| ACU-0050 | Delete after confirming, keep context | CU-03, CU-04, CU-05 | E2E |
| ACU-0060 | Field errors next to fields, values kept | CU-08, CU-09, CU-10, BV-09, BV-10, BV-11, BV-13, BV-14, BV-15, BV-16, BV-17, BV-25 | UI |
| ACU-0070 | Duplicate email shows on the email field | CU-06, BV-12 | UI |
| ACU-0080 | Generic message and retry on failure | ER-01, ER-02, ER-03, ER-04, ER-05, ER-07 | UI |
| AUS-0010 | Create-user screen | UM-01, UM-07, UM-08, UM-10, BV-20, BV-21, BV-22, BV-23, BV-24 | E2E |
| AUS-0020 | New user signs in at once | UM-02, UM-03 | E2E |
| AUS-0030 | Duplicate username refused | UM-04, UM-05, UM-06 | E2E |
| AUS-0040 | No create-user entry for Viewers | VW-07, UM-09 | E2E |

All 28 requirements have at least one P1 case.

### Success metrics

| PRD success metric | Test cases |
|---|---|
| Unauthenticated access: 0 customer screens reachable without a session | AC-01, AC-02, AC-03, SO-02, SO-03 |
| Viewer controls: 0 write or create-user controls for a Viewer | VW-01, VW-05, VW-07 |
| Admin workflow completes in 100% of runs | CU-01, CU-02, CU-03, SP-03, FP-04, FP-05 |
| Session persistence: restart keeps a signed-in user, drops a signed-out one | SS-01, SS-02, SO-03 |
| Forced password change blocks until the password changes | FP-01, FP-02, FP-03, FP-04 |
| Transport: 0 requests use plain HTTP | SC-03, SC-04 |

## Entry and exit criteria

Entry:

1. The login PRD's API work is on the build under test, and the default Admin exists.
2. The app builds as a debug and a release variant and installs on the emulator.
3. The Compose stack runs with the test data above, and an HTTPS address for the API exists for `SC-03` and `SC-04`.

Exit:

1. Every P1 case passes.
2. No open defect of P1 or P2 severity remains without a recorded decision.
3. Every requirement in the traceability matrix has at least one passing case.
4. Every open question below has an answer, and the affected cases are updated to match.

## Open questions and risks

1. **Form limits in the app (BV-19, BV-25).** The API limits a name to 100 characters and a search to 100. The PRD says nothing about the app's form limits. A form that lets a user type 101 characters, then shows a 400 error, is the likeliest rough edge. The architect decides whether the fields stop at the limit.
2. **Username and password limits (BV-24).** The create-user contract isn't on the API branch this plan was written against, and the login PRD sets no upper bound. A very long password is the likeliest 500. The architect decides on a limit or records that there is none.
3. **Username uniqueness (UM-05, UM-06).** `AUS-0030` says "a username that already exists". It doesn't say whether `VIC` or `  vic  ` counts as the same name. The login PRD's sign-in treats both as `vic`, so two accounts that differ only by case or padding would be confusing. The product owner decides.
4. **Android backup (SC-05).** `AAU-0070` says other apps mustn't read the credential. It doesn't mention device backup. The product owner decides whether backup must exclude the token.
5. **Unsaved forms after process death (CU-10).** The PRD requires typed values to stay after an API error, not after the system kills the app. The product owner decides whether a draft must survive.
6. **Search normalisation (SP-05).** The PRD doesn't say whether search ignores case or padding. The expected result follows the web app's behaviour. The product owner confirms.
7. **HTTPS in the test stack (SC-03, SC-04).** Compose serves the API over plain HTTP on port 8080, so the E2E level can't meet `AAU-0110` as it stands. The E2E suite needs a TLS front, or a debug-only exception. The architect decides, and the PRD's open question on how phones reach the API covers the same ground.
