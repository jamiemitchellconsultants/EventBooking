---
date: 2026-09-25
slug: fix-web-allow-keycloak-silent-sign-in-frame-so-the-page-loads-in-under-a
title: "fix(web): allow Keycloak silent sign-in frame so the page loads in under a second"
summary: "Allow framing of the Keycloak origin and same-origin framing, keeping every other restriction, rather than removing the policy or disabling silent sign-in in the app."
kind: product
status: accepted
sequence: 2026-09-25T10:02:49.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/66; merge commit 003f16cd4626d1fbb06fac951925b2ea85e268f0"
---

## Context

After the CSP fix that let Blazor start (PR 64), the site rendered but only after a ten second delay, which read as a very slow page load. The cause was the same policy blocking a different, hidden request.

## Decision

Allow framing of the Keycloak origin and same-origin framing, keeping every other restriction, rather than removing the policy or disabling silent sign-in in the app.

## Consequences

Page load no longer waits for the silent sign-in timeout. Same-origin framing is now permitted, which is what the silent sign-in callback needs. Still uncovered: no automated browser test of the home-lab web image against a real Keycloak.

---

AI-Fingerprint: sha256:22862a1f72a7

🤖 Generated with [Claude Code](https://claude.com/claude-code)
