---
date: 2026-09-26
slug: feat-event-group-self-registration
title: "feat: event-group self-registration"
summary: "- Chosen: pending `SelfRegistration` rows with a signed registration-purpose token; confirmation revalidates both public gates, group membership, event compatibility and capacity, then books atomically through a one-option internal invite…"
kind: product
status: accepted
sequence: 2026-09-26T08:16:39.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/76; merge commit 59dd3a28e2850f3764a69267cc4924a82edda802"
---

## Context

Coordinators entered every attendee by hand, which bottlenecks open-intake events. The
2026-09-25 self-registration design asked for registrants to self-serve on deliberately
open groups without staff accounts, under hard constraints: stay anonymous, never expose
capacities or tokens, confirm only through an emailed link before any capacity moves,
serialize concurrent confirmations, and purge request personal data after retention.

## Decision

- Chosen: pending `SelfRegistration` rows with a signed registration-purpose token;
  confirmation revalidates both public gates, group membership, event compatibility and
  capacity, then books atomically through a one-option internal invite
  (`inviteOptionCount` 1) so the ordinary booking journey, confirmation email and
  manage link apply unchanged. The token travels only in the emailed link, so possession
  proves ownership of the address; an in-flight address gets the same neutral receipt and
  a fresh email rather than an error; a lapsed request is replaced, while a request for a different group or selection never touches a live one (uniqueness is per address, event, group and selection), and link emails to one address are spaced a minute apart. View/summary endpoints back the confirm page;
  `terminalAt` plus a 30-day sweep purge bounds personal-data lifetime.
- Rejected: holding capacity at submit — no-shows would strand places with no booking
  to release them. Rejected: sending staff credentials from the public client — it uses
  the plain named client with no Authorization header, and the server accepts none
  there. Rejected: per-group capacity on screen — the public payload carries no
  capacity data, so over-capacity confirms are refused server-side instead.

## Consequences

- New public routes (`/api/public/event-groups/…`, per-event submit, token
  view/confirm), three anonymous pages, catalog/design updates (FR-16), and a
  terminal-timestamp migration. Audit gains `SelfRegistrationExpired` usage;
  expiry records request id and status only.
- Trade-off: submission now depends on the outbox for the registrant to receive their link, and the default link lifetime is 24 hours (staff can configure 1–168 hours; the design docs are amended to say so and the page states no number) (migration moves untouched 48-hour settings). All test projects pass, including the Postgres-backed API suite; a retention-purge loop that never saved its batches, and hung that suite, is fixed. Anonymous routes stay MCP-excluded by the approved remote-MCP design.
- Open: whether coordinators need visibility into pending-request queues —
  deliberately out of this change.

---

AI-Fingerprint: sha256:37fc4406a028
