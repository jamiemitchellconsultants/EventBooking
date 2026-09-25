---
date: 2026-09-25
slug: seed-and-deploy-eventbooking
title: "Seed and deploy EventBooking"
summary: "Use a migrate-only seed default, add LAB as the fourth managed active appointment type, retire the single-zone clock, and support local Compose plus an isolated home-lab topology behind shared Caddy and Keycloak services."
kind: product
status: accepted
sequence: 2026-09-25T04:04:28.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/56; merge commit 80b55191c7cb41ba2af101b05525655135631d8b"
---

## Context

EventBooking needed reproducible demo data and two supported container deployment shapes after the
application and Web surfaces were defined.

## Decision

Use a migrate-only seed default, add LAB as the fourth managed active appointment type, retire the
single-zone clock, and support local Compose plus an isolated home-lab topology behind shared Caddy
and Keycloak services.

## Consequences

Demo state now requires an explicit flag, destructive reseeding has a second guard, operators have
documented forward-only upgrade and fresh-volume recovery paths, and releases publish five images
plus a self-contained migration executable.

AI-Fingerprint: sha256:aed4f2c2c176
