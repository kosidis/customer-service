---
id: ADR-1
prd: prd-login-permissions
title: Identify signed-in users with database-backed opaque sessions
status: proposed
kind: architecture
date: 2026-11-16
deciders: [K.Sidiropoulos]
supersedes:
---

# Identify signed-in users with database-backed opaque sessions

## Status
proposed

## Context
The web app calls the API from the server, and the API must know who the caller is and what role they hold on every customer request. The PRD says a signed-out session must stop granting access at once (`AUTH-0020`), that sessions never expire from inactivity (`AUTH-0060`), and that a user on an initial password is blocked until they change it (`AUTH-0090`, `AUTH-0100`). Both flags change while a session is live, so the API needs current user state on each request.

## Decision
We will issue a random opaque token at sign-in and store only its SHA-256 hash in a `Sessions` table, keyed to the user. The web app keeps the token in an httpOnly cookie and sends it to the API as a bearer token. The API looks up the session and the user on every request. Sign-out deletes the session row. The API never expires a session by time.

## Consequences
Sign-out and a password change take effect on the next request, and the role and password-change flag are always current. The cost is one indexed database read per request and a table that only grows until sessions are removed at sign-out. A database leak does not expose usable tokens because only hashes are stored.

## Alternatives considered
A signed JWT with the role inside needs no table, but the API can't revoke it at sign-out, and a role or password-change flag stays stale until the token expires. That conflicts with `AUTH-0020` and `AUTH-0100`, and the PRD rules out expiry. Framework cookie authentication on the API was rejected because the API is called server to server by the web app, not by a browser.
