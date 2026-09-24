---
date: 2026-09-24
slug: phase-4-api-endpoint-catalogue-and-mcp-parity
title: "Phase 4: API endpoint catalogue and MCP parity"
summary: "The branch implements the design-05 endpoint catalogue and a 45-tool MCP surface with three-way parity (operation catalogue, OpenAPI document, tools/list)."
kind: product
status: accepted
sequence: 2026-09-24T13:40:22.000Z
evidence: "https://github.com/jamiemitchellconsultants/EventBooking/pull/51; merge commit 9776785a38a162c890689dd5f2ebe26a6374969f"
---

## Context

Phase 3 landed the domain, application, and persistence layers behind a ported API/MCP surface whose routes, shapes, and tool names predated the design-05 contract. The Phase 4 plans were hand-authored before Phase 3 was executed, so every plan fragment had to be diffed against the merged code and the code followed where they disagreed. The work therefore had two intertwined goals: stand up the design-05 surface faithfully, and reconcile each point where the plan, the design, and the Phase-3 code said different things.

## Decision

The branch implements the design-05 endpoint catalogue and a 45-tool MCP surface with three-way parity (operation catalogue, OpenAPI document, tools/list). Where sources disagreed, the order of authority applied was: functional requirements, then design 05, then the merged Phase-3 code, then the plan documents. Material calls made under that rule:

- The roster handlers' 403-for-unlisted-type / 200-empty-roster semantics were accepted over the ported tests' 404 expectations, because the event list shows every event listing the caller's type and the detail must agree with the list.
- The import 1000-row bound moved from the REST endpoint into the handler so both surfaces refuse one oversized file with one application error.
- The roster CSV drops the `version` column (FR-8.7) and gains the RFC-4180 quoting the new renderer lacked.
- MCP tools call the same handlers as REST with the same translations (timestamp parsing, mutual-exclusion checks, role reconciliation on the identity tool), and refusals carry the application error code so the refusal-parity suite can put each one through the REST problem catalogue.
- The plan's ToolDescriptions helper was skipped: the plan describes it as the parity oracle but the plan's own parity test compares against the catalogue directly, leaving the helper with no caller.

## Consequences

- REST and MCP are now two transports over one handler layer with mechanically enforced parity: any new staff operation without a tool (or any tool without an operation) fails the gate.
- The ported API and MCP suites that drove deleted routes and tools are gone; behavior they alone covered (audit search bounds, workspace minimum-data shapes, recovery flows, transport auth) was migrated, not dropped.
- Deliberately open: MCP request/response schemas are shaped by the SDK from handler views rather than reviewed line by line; the CSV download filename is a stable `roster-{id}` rather than the old descriptive name; and MCP list tools pass `limit` straight to handlers instead of enforcing the 1–200 transport bound.

---

AI-Fingerprint: sha256:837518e5c534
