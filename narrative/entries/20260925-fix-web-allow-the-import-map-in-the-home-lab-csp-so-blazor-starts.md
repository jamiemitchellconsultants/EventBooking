---
date: 2026-09-25
slug: fix-web-allow-the-import-map-in-the-home-lab-csp-so-blazor-starts
title: "fix(web): allow the import map in the home-lab CSP so Blazor starts"
summary: "Keep a strict `script-src` and allow the import map by build-time hash, rather than adding `'unsafe-inline'`. Remove the app's other inline script instead of hashing it, and fail the image build if a new inline script appears."
kind: product
status: accepted
sequence: 2026-09-25T09:15:20.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/64; merge commit b7f7e7787f2a456138677dc56c2366cbc6592529"
---

## Context

The home-lab rehearsal exercised install, upgrade and restore against the API but never loaded the web front end through the Caddy image, so a policy that blocks the app's own inline scripts shipped.

## Decision

Keep a strict `script-src` and allow the import map by build-time hash, rather than adding `'unsafe-inline'`. Remove the app's other inline script instead of hashing it, and fail the image build if a new inline script appears.

## Consequences

The policy stays tight and self-updating across publishes. Adding any inline script to `index.html` now fails the image build, which is deliberate. Not covered: there is still no automated browser test of the home-lab web image.

---

AI-Fingerprint: sha256:5dd2ddec47e9

🤖 Generated with [Claude Code](https://claude.com/claude-code)
