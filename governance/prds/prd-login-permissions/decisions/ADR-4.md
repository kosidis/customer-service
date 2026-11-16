---
id: ADR-4
prd: prd-login-permissions
title: Seed the default Admin at start-up when the user table is empty
status: accepted
kind: architecture
date: 2026-11-16
deciders: [K.Sidiropoulos]
supersedes:
---

# Seed the default Admin at start-up when the user table is empty

## Status
accepted

## Context
`AUTHZ-0070` needs a default Admin with a standard initial password on first start, with no manual database edit. `AUTHZ-0080` says a restart must not recreate the account or reset its password. `Program.cs` already runs migrations on start-up inside a `try` block when `Database:ApplyMigrationsOnStartup` is true, and both Compose and the API Helm chart set it.

## Decision
We will seed in `Program.cs`, immediately after the migration step and inside the same flag and `try` block. The seeder creates the Admin only when the `Users` table has no rows. The account has the username `admin`, a password hash of the configured initial password, the Admin role and the password-change flag set. The initial password comes from the `Seed:DefaultAdminPassword` setting, which has a documented default in `appsettings.json` so every deployment starts with the same known value.

## Consequences
A fresh database gets an Admin with no operator step, and a restart finds users and does nothing. A deployment that migrates the database outside the API must run the API once with the flag on. If every Admin row is removed by hand, the next start recreates the default Admin, which is acceptable because the PRD has no user deletion.

## Alternatives considered
Inserting the Admin in an EF migration with a precomputed hash was rejected because the hash then lives in source control and can't follow the `Seed:DefaultAdminPassword` setting. A separate seeding command was rejected because it adds an operator step that `AUTHZ-0070` forbids.
