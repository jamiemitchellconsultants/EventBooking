---
date: 2026-09-23
slug: phase-0-port-and-strip-tasks-1-2-3a-3d
title: "Phase 0: port and strip (Tasks 1, 2, 3a-3d)"
summary: "This pull request takes decisions D5, D6, D8, D9 and D11 into effect in code. D5: port the predecessor solution and generalise it here. D6: drop bulk event import, which contradicts negotiation-only event creation."
kind: product
status: accepted
sequence: 2026-09-23T16:32:45.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/42; merge commit 8b48f7134b821efa992e29fdc0d9add85caad96c"
---

## Context

Phase 0 ports the JointBooking predecessor solution into this repository and generalises it toward the EventBooking design, rather than rebuilding from the redesign documents or generalising the predecessor in place. The port lands under EventBooking names with the documented vocabulary mapping, while the strip removes everything the design does not carry over: cloud dependencies, bulk event import, head-office configuration, and the organisation-specific staff identifier format. The no-overbooking guarantee survives unchanged, enforced in the application and the database as before.

## Decision

This pull request takes decisions D5, D6, D8, D9 and D11 into effect in code. D5: port the predecessor solution and generalise it here. D6: drop bulk event import, which contradicts negotiation-only event creation. D8: Keycloak is the only identity provider for local and home-lab deployments, keeping the authentication-provider seam so another OIDC provider can be added later. D9: drop MinIO object storage, which nothing reads or writes. D11: the staff identifier format is a deployment-configured regular expression defaulting to ^[A-Z0-9]{1,32}$; the identity provider validation and the seed data validation follow the same deployment policy.

## Consequences

The repository now builds and runs with no cloud dependencies and no provider-specific identity adapter beyond Keycloak. Events become bookable through manager negotiation only; the direct creation and import paths are gone, and seeded events reconstruct the same proposal, acceptance, and confirmation audit history the handlers record. Staff identifier policy is a deployment concern end to end: application boundaries, the Keycloak realm profiles, and the demo seed all validate against it. The migration chain is still inherited from the predecessor and is replaced by a fresh initial migration in Phase 2 under D12. The user guides ship without screenshots until valid captures are produced; fresh EventBooking captures are tracked in #43.

---

AI-Fingerprint: sha256:d458d77954b4
