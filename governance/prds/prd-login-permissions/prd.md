---
id: prd-login-permissions
title: Login and role-based permissions
owner: Konstantinos Sidiropoulos
approvers: [kosidis]
min-approvals: 1
initiative:
epics: {}
---

# Login and role-based permissions

## Stakeholders

| Name | Area | Role |
|---|---|---|
| Konstantinos Sidiropoulos | Product / Architecture | Owner, Approver |

## Related Documents

| Description | Link |
|---|---|
| Project overview and conventions | `CLAUDE.md` in the repository root |

## Definitions

| Term | Description |
|---|---|
| User | A person with an account who signs in to the web app. |
| Role | The permission level of a user. This PRD defines two: Viewer and Admin. |
| Viewer | A signed-in user who can see customers but cannot change them. |
| Admin | A signed-in user who can see, edit and delete customers. |
| Signed in | The user has proved who they are and has an active session. |

## Introduction

### Overview
The customer service has no login. Anyone who can reach the web app can list, add, edit and delete customers. That is acceptable for a prototype and not for real customer data. Owners need to know who is using the app and to limit destructive actions to trusted people.

This PRD adds sign-in and two roles. Every signed-in user can see customers. Only Admins can edit and delete them.

### In Scope

- Sign in and sign out for the web app.
- Two roles, Viewer and Admin, with one role per user.
- Blocking unauthenticated access to customer data.
- Blocking edit and delete for users who are not Admins, in both the web app and the API.

### Out of Scope

- Self-service registration, password reset and email verification.
- More than two roles, and per-customer or per-field permissions.
- Audit logging of who changed what.
- Single sign-on with an external identity provider.

## Codebase Context

*Advisory — gathered 2026-11-16. Informs Impacted Systems, Definitions, and Assumptions; no gate depends on this section.*

| System | What exists today |
|---|---|
| customer-service (this repository) | The API and the web app have no authentication or authorisation. All customer endpoints are open. The browser only talks to the web app, which calls the API from the server. |

## Impacted Systems

| System | Expected Impact |
|---|---|
| Web app | Sign-in and sign-out pages, session handling, and role-aware screens that hide edit and delete for Viewers. |
| API | Verifies the caller's identity and role on every customer endpoint and rejects forbidden actions. |
| Database | Stores users and their roles. |
| Deployment (Compose, Helm) | New configuration for any secrets the sign-in needs. |

## Assumptions

- The API stays reachable only from the web app, and the browser never calls it directly.
- Users are created by an Admin or by an operator, not by themselves (see Open questions).
- There is at least one Admin from the first deployment.

## Requirements

### Authentication

| Req. ID | Product | Description |
|---|---|---|
| AUTH - 0010 | Customer Service | Users must be able to sign in with credentials they hold. |
| AUTH - 0020 | Customer Service | Users must be able to sign out, and a signed-out session must no longer grant access. |
| AUTH - 0030 | Customer Service | A request with no valid sign-in to any customer page must be redirected to the sign-in page. |
| AUTH - 0040 | Customer Service | A request with no valid sign-in to any customer API endpoint must be rejected with a 401 response. |
| AUTH - 0050 | Customer Service | A failed sign-in must show a generic error that does not reveal whether the account exists. |
| AUTH - 0060 | Customer Service | A signed-in session must expire after a period of inactivity that operators can configure. |
| AUTH - 0070 | Customer Service | Passwords must not be stored or logged in plain text. |

### Authorisation

| Req. ID | Product | Description |
|---|---|---|
| AUTHZ - 0010 | Customer Service | Each user must have exactly one role, Viewer or Admin. |
| AUTHZ - 0020 | Customer Service | Viewers and Admins must be able to list, search and view customers. |
| AUTHZ - 0030 | Customer Service | Admins must be able to edit customers. |
| AUTHZ - 0040 | Customer Service | Admins must be able to delete customers. |
| AUTHZ - 0050 | Customer Service | An edit or delete request from a Viewer must be rejected with a 403 response by the API, and the customer must stay unchanged. |
| AUTHZ - 0060 | Customer Service | The web app must not show edit or delete controls to Viewers, and must refuse direct visits to the edit page by a Viewer. |
| AUTHZ - 0070 | Customer Service | The system must have at least one Admin from the first deployment, without a manual database edit. |

## Success metrics

- Unauthenticated access to customer data: 0 requests succeed in the integration tests across all customer endpoints.
- Viewer write attempts: 100% of edit and delete requests from a Viewer are rejected, covered by automated tests.
- Admin workflow regression: all existing list, search, add, edit and delete tests still pass for an Admin.

## Open questions

- How are users created and assigned a role: an Admin screen, a command-line seed, or both?
- Is adding a customer open to Viewers, or Admin only? The request names edit and delete only.
- Where do credentials live: this service's own database, or an external identity provider later?
- What default inactivity timeout do operators expect?
