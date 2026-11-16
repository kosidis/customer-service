---
id: ADR-2
prd: prd-login-permissions
title: Hash passwords with the ASP.NET Core PasswordHasher
status: accepted
kind: architecture
date: 2026-11-16
deciders: [K.Sidiropoulos]
supersedes:
---

# Hash passwords with the ASP.NET Core PasswordHasher

## Status
accepted

## Context
`AUTH-0070` requires salted hashes in the database and no plain text in logs. The API runs on .NET 10 and has no password code today. Writing hashing code by hand carries a high risk of a subtle flaw.

## Decision
We will use `PasswordHasher<TUser>` from `Microsoft.Extensions.Identity.Core` to hash and verify passwords. It is a library class only, with no ASP.NET Core Identity tables, stores or UI. Verification uses the result that reports whether a stored hash needs upgrading, and the API rewrites the hash on sign-in when it does.

## Consequences
The API gets a maintained, versioned hash format with a per-password salt and no custom cryptography. It adds one package dependency. Hash cost is fixed by the framework default, so the sign-in latency budget depends on that default.

## Alternatives considered
A dedicated Argon2 or bcrypt package has stronger memory-hardness arguments, but it adds a third-party dependency and a custom format for a two-role internal tool. Hand-rolled PBKDF2 was rejected because the framework class already does it correctly.
