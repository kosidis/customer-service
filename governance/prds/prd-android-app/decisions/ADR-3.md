---
id: ADR-3
prd: prd-android-app
title: Allow only HTTPS to the API and put TLS in front of it
status: proposed
kind: architecture
date: 2026-11-18
deciders: [K.Sidiropoulos]
supersedes:
---

# Allow only HTTPS to the API and put TLS in front of it

## Status
proposed

## Context
`AAU-0110` says the app sends credentials and the session token only over HTTPS. Compose serves the API over plain HTTP on port 8080, and only the web app is public today. The PRD leaves open how a phone reaches the API in each environment.

## Decision
We will set the base URL through a build-config field per build type and reject any value that does not start with `https://` at start-up. The release manifest sets `android:usesCleartextTraffic="false"` and a network security configuration that trusts only system certificate authorities. The API's Helm chart exposes the API through its existing ingress with a TLS secret in staging and prod. A local setup uses a TLS front on the Compose stack, kept out of the API itself, with a development certificate that only the debug build's network security configuration trusts.

## Consequences
The app cannot talk to the API over HTTP by mistake, and a test can prove it (`AAU-0110`). The API ingress, which the web chart's docs keep off today, is turned on with TLS for the phone path, so the API becomes reachable beyond the web app and must rely on its own authentication, as the login PRD already requires. Local development needs a TLS front.

## Alternatives considered
Allowing cleartext in debug builds means the HTTPS rule is untested where developers work. Putting the API behind the web app as a proxy keeps one public entry point but adds a hop and a second contract to maintain, and the PRD says the app calls the API directly.
