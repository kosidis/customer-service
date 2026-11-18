---
id: ADR-2
prd: prd-android-app
title: Keep the session token encrypted with an Android Keystore key
status: proposed
kind: architecture
date: 2026-11-18
deciders: [K.Sidiropoulos]
supersedes:
---

# Keep the session token encrypted with an Android Keystore key

## Status
proposed

## Context
The session must survive app restarts and stay unreadable to other apps (`AAU-0050`, `AAU-0070`). The API issues an opaque Bearer token that never expires by time, so a leaked token stays valid until sign-out.

## Decision
We will generate a non-exportable AES-256 key in the Android Keystore and encrypt the token with AES-GCM before writing it to app-private Preferences DataStore. The app excludes that file from cloud backup and device transfer. The token is never written to logs, crash reports or intents, and the HTTP logging interceptor redacts the `Authorization` header.

## Consequences
Other apps cannot read the token, and a restored backup cannot carry it to another phone. Clearing app data or losing the Keystore key signs the user out, which is acceptable. The code owns a small amount of crypto glue that needs unit and instrumented tests.

## Alternatives considered
`EncryptedSharedPreferences` does the same job but is deprecated. Plain DataStore is readable on a rooted device and in a backup. Keeping the token only in memory fails `AAU-0050`.
