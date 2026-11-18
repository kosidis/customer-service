---
id: ADR-1
prd: prd-android-app
title: Build the Android app in Kotlin with Jetpack Compose in the monorepo
status: proposed
kind: architecture
date: 2026-11-18
deciders: [K.Sidiropoulos]
supersedes:
---

# Build the Android app in Kotlin with Jetpack Compose in the monorepo

## Status
proposed

## Context
The PRD asks for an Android app for phones with no offline mode, no other platforms and no store listing. This repository holds the API and the web app and has no mobile code. The PRD scopes the work to this repository.

## Decision
We will write the app in Kotlin with Jetpack Compose, as a single-activity app under `apps/android`, built with Gradle and a version catalogue. Networking uses OkHttp and Retrofit with kotlinx.serialization. State lives in view models that expose `StateFlow`. The minimum Android version is set in `tech-design-1` (open question in the PRD).

## Consequences
The app uses the platform's own tooling, so the Keystore, the network security configuration and instrumented tests work without a bridge. The repository gains a Gradle build and a JDK requirement beside .NET and Node, and `.claude/pre-pr-checks` needs an Android step when code lands.

## Alternatives considered
Flutter and React Native add a second runtime and a bridge to the Keystore for one platform. XML views with fragments need more code for the same screens and are the older path.
