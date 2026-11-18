---
id: prd-android-app
title: Android app with login and role-based permissions
owner: Konstantinos Sidiropoulos
approvers: [kosidis]
min-approvals: 1
initiative:
epics: {}
---

# Android app with login and role-based permissions

## Stakeholders

| Name | Area | Role |
|---|---|---|
| Konstantinos Sidiropoulos | Product / Architecture | Owner, Approver |

## Related Documents

| Description | Link |
|---|---|
| Login and role-based permissions PRD (web app and API) | `governance/prds/prd-login-permissions/prd.md` |
| Project overview and conventions | `CLAUDE.md` in the repository root |

## Definitions

| Term | Description |
|---|---|
| User | A person with an account who signs in to the Android app. Accounts are the same ones the web app uses. |
| Role | The permission level of a user: Viewer or Admin, as defined in the login PRD. |
| Viewer | A signed-in user who can see customers but cannot change them. |
| Admin | A signed-in user who can see, add, edit and delete customers and create users. |
| Initial password | The password a user holds before their first change: the default Admin's standard password, or the one an Admin set at creation. |
| Signed in | The user has proved who they are and the app holds an active session. |

## Introduction

### Overview
Customers are managed only through the web app. Staff who work away from a desk need the same customer list on a phone. The web app is not built for that, and the API has no mobile client.

This PRD adds an Android app with the same capabilities as the web app: sign in and out, a forced change of the initial password, customer list, search, add, edit and delete, and user creation for Admins. Roles work as in the login PRD. Every signed-in user can see customers; only Admins change them. The app talks to the existing API, and the API stays the single place that enforces identity and permissions.

### In Scope

- An Android app for phones, installable as a standard Android application package.
- Sign in and sign out with the existing accounts.
- Forced replacement of the initial password at first sign-in, and voluntary password change afterwards.
- Listing, searching and viewing customers for every signed-in user.
- Adding, editing and deleting customers for Admins.
- A screen where Admins create users from a username, a password and a role.
- Screens that adapt to the role: Viewers do not see add, edit, delete or create-user controls.
- Showing the API's validation and conflict errors next to the fields they belong to.
- Keeping the session across app restarts until the user signs out.

### Out of Scope

- Tablet-specific layouts and other platforms such as iOS.
- Offline use, local caching of customer data and background sync.
- Push notifications.
- Biometric unlock, single sign-on and any sign-in method other than username and password.
- Self-service registration, password reset, and listing, editing or deleting users.
- New roles, new permissions or any change to the roles the login PRD defines.
- Publishing to a public app store.
- Session expiry from inactivity.

## Codebase Context

*Advisory — gathered 2026-11-18. Informs Impacted Systems, Definitions, and Assumptions; no gate depends on this section.*

| System | What exists today |
|---|---|
| customer-service (this repository) | A .NET API with customer CRUD and a Next.js web app. The login PRD adds Bearer-token sign-in, two roles and forced password change to the API; that work is still on open pull requests and is not on the default branch. There is no Android code. Only the web app calls the API today, from its server. |

## Impacted Systems

| System | Expected Impact |
|---|---|
| Android app (new) | Sign-in, session handling, customer screens, user-creation screen and role-aware controls. |
| API | Becomes reachable from a mobile client. Needs a transport-secure public address for the app and must keep returning the 401, 403 and field-error responses the login PRD defines. |
| Web app | No change expected. |
| Deployment (Compose, Helm) | The API must be reachable by phones, which the current setup does not provide. |

## Assumptions

- The login PRD's API work (sign-in, roles, forced password change, user creation) is delivered before or alongside this PRD. This PRD adds no new account or permission rules.
- The Android app calls the API directly. The earlier assumption that only the web app calls the API no longer holds.
- The app is served by the same accounts and roles as the web app; a user created in either place can sign in to both.
- Phones reach the API over HTTPS.
- A supported phone runs a currently maintained Android release; the exact minimum version is set in the tech design.
- The API pages the customer list, as it does for the web app.

## Requirements

### Android authentication

| Req. ID | Product | Description |
|---|---|---|
| AAU - 0010 | Customer Service Android | Users must be able to sign in to the app with the username and password they hold. |
| AAU - 0020 | Customer Service Android | Users must be able to sign out, and a signed-out session must no longer grant access, including after the app restarts. |
| AAU - 0030 | Customer Service Android | An app launch with no valid session must show the sign-in screen, and no customer screen may be reachable until the user signs in. |
| AAU - 0040 | Customer Service Android | A failed sign-in must show a generic error that does not reveal whether the account exists. |
| AAU - 0050 | Customer Service Android | A signed-in session must survive closing and reopening the app, and end only when the user signs out or the API rejects it. |
| AAU - 0060 | Customer Service Android | When the API rejects the session with a 401 response, the app must return the user to the sign-in screen. |
| AAU - 0070 | Customer Service Android | The session credential must be stored so that other apps on the phone cannot read it, and must never be logged. |
| AAU - 0080 | Customer Service Android | A signed-in user must be able to change their own password by entering their current password and a new one, and the API's field errors must show on the form. |
| AAU - 0090 | Customer Service Android | While a signed-in user still holds the initial password, the app must show only the change-password and sign-out actions until the password is changed. |
| AAU - 0100 | Customer Service Android | After a user changes their password, the app must give access matching their role without asking them to sign in again. |
| AAU - 0110 | Customer Service Android | The app must send credentials and the session credential only over HTTPS. |

### Android authorisation

| Req. ID | Product | Description |
|---|---|---|
| AAZ - 0010 | Customer Service Android | Viewers and Admins must be able to list, search and view customers in the app. |
| AAZ - 0020 | Customer Service Android | Admins must be able to add, edit and delete customers in the app. |
| AAZ - 0030 | Customer Service Android | The app must not show add, edit or delete controls to Viewers. |
| AAZ - 0040 | Customer Service Android | When the API answers an action with a 403 response, the app must tell the user the action isn't permitted and leave the customer data on screen unchanged. |
| AAZ - 0050 | Customer Service Android | The app must take the user's role from the API at sign-in and never decide permissions on its own; the API remains the only enforcement point. |

### Android customer screens

| Req. ID | Product | Description |
|---|---|---|
| ACU - 0010 | Customer Service Android | The app must show customers as a paged list, with a way to move between pages. |
| ACU - 0020 | Customer Service Android | Users must be able to search customers, and the list must show only matching customers. |
| ACU - 0030 | Customer Service Android | Admins must be able to add a customer on a form, and the new customer must appear in the list afterwards. |
| ACU - 0040 | Customer Service Android | Admins must be able to edit a customer on a form that starts with the customer's current values. |
| ACU - 0050 | Customer Service Android | Admins must be able to delete a customer after confirming, and the app must keep the user's search and page when it returns to the list. |
| ACU - 0060 | Customer Service Android | The app must show the API's field errors next to the matching form fields and keep what the user typed. |
| ACU - 0070 | Customer Service Android | A duplicate email rejected by the API must show as an error on the email field. |
| ACU - 0080 | Customer Service Android | When the API is unreachable or answers with a server error, the app must show a generic message without internal detail and let the user retry. |

### Android user management

| Req. ID | Product | Description |
|---|---|---|
| AUS - 0010 | Customer Service Android | Admins must be able to open a screen that creates a user from a username, a password and a role (Viewer or Admin). |
| AUS - 0020 | Customer Service Android | A user created on that screen must be able to sign in to the app and the web app straight away, with the chosen role. |
| AUS - 0030 | Customer Service Android | A username that already exists must show as a field error, and no second account must be created. |
| AUS - 0040 | Customer Service Android | The app must not show the create-user entry to Viewers. |

## Success metrics

- Unauthenticated access: 0 customer screens reachable without a valid session across the app's automated UI tests.
- Viewer controls: 0 add, edit, delete or create-user controls visible to a Viewer in 100% of automated UI test runs.
- Admin workflow: an Admin completes sign in, forced password change, add, edit, search and delete on a phone in 100% of automated test runs.
- Session persistence: 100% of automated restarts keep a signed-in user signed in, and 0% keep a signed-out user signed in.
- Forced password change: 100% of attempts to reach customer screens with an initial password are blocked until the password changes.
- Transport: 0 requests from the app use plain HTTP, checked by an automated test.

## Open questions

- Which minimum Android version does the app support?
- How does the phone reach the API in each environment, given that today only the web app is public?
- How is a debug or pre-release build distributed to testers before any store listing exists?
