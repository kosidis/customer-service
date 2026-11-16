---
id: ADR-3
prd: prd-login-permissions
title: Enforce authentication and roles in the API, with optimistic checks in the web app
status: proposed
kind: architecture
date: 2026-11-16
deciders: [K.Sidiropoulos]
supersedes:
---

# Enforce authentication and roles in the API, with optimistic checks in the web app

## Status
proposed

## Context
Requirements ask for 401 and 403 responses from the API (`AUTH-0040`, `AUTHZ-0050`, `USR-0050`) and for the web app to redirect and hide controls (`AUTH-0030`, `AUTHZ-0060`, `USR-0060`). The Next.js 16 authentication guide recommends optimistic checks in the request proxy and secure checks next to the data. A layout does not re-render on every navigation, so it can't be the only guard.

## Decision
We will make the API the only place that decides access. It uses a custom authentication handler for the bearer session token, a fallback policy that requires a signed-in user whose password change is not pending, and an `AdminOnly` policy. A small set of endpoints (sign-in, health) allow anonymous callers, and sign-out, current user and change-password allow a user whose password change is pending. The web app mirrors these rules for user experience only: `proxy.ts` redirects requests with no session cookie to the sign-in page, and a server-only helper called by every page redirects by role and pending password change. Server actions rely on the API's 403 as the final check.

## Consequences
A bug in the web app can show the wrong control but can't change data. Every new API endpoint is protected by default, because the fallback policy applies unless the endpoint opts out. The web app makes one extra API call per page to read the current user, which React `cache` limits to one per request.

## Alternatives considered
Checking roles only in the web app was rejected because the API would stay open to any caller who reaches it. Per-endpoint `[Authorize]` attributes without a fallback policy were rejected because a forgotten attribute would leave an endpoint open.
