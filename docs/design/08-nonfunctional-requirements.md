# 08 — Non-Functional Requirements

[← Deployment](07-deployment.md) · [Predecessor traceability →](09-predecessor-traceability.md)

The design scale is one organisation per instance on one home-lab host, with these peak figures:

| Measure | Design ceiling |
|---|---|
| `Location`s | 50 |
| `AppointmentType`s | 30 |
| Active `Event`s | 2 000 |
| Attendees | 50 000 |
| Attendees confirming in one burst after a batch of invites | 500 |

Targets below apply at that scale.

## Performance

| ID | Target |
|---|---|
| NFR-P1 | API p95 below 300 ms for reads and below 600 ms for writes, measured at the reverse proxy |
| NFR-P2 | Invite selection query below 50 ms p95 with 2 000 active events and 10 required types |
| NFR-P3 | Booking confirmation holds its capacity locks for under 50 ms p95. There are no retry loops visible to the client |
| NFR-P4 | Attendee book and manage pages are interactive within 3 s on a mid-range phone over 4G, including the WebAssembly download on a first visit. The bundle is trimmed and compressed, and uses lazy loading where available |
| NFR-P5 | A 1000-row attendee CSV import completes within 10 s |

## Scalability

- The API and MCP containers are stateless. Several API replicas are safe, because background jobs
  use advisory locks and `SKIP LOCKED` (FR-15.3).
- The connection pool (Npgsql) is sized to 20 per replica by default. PostgreSQL's
  `max_connections` must exceed the replica count × 20 + 10.
- Throughput under contention on one popular event is **measured, not assumed**: the load test
  under Verification drives 500 concurrent confirmations against one event.

## Reliability

| ID | Target | Mechanism |
|---|---|---|
| NFR-R1 | RPO ≤ 24 h (home lab) | Nightly `pg_dump`, retained 14 days ([07](07-deployment.md#upgrades-and-rollback)). Operators wanting lower RPO enable WAL archiving; this is documented but not shipped |
| NFR-R2 | RTO ≤ 2 h | A restore runbook in `deploy/home-lab/README.md`, rehearsed before the first real use |
| NFR-R3 | No email is lost on crash | The outbox: `EmailLog` is committed with the business change, and the dispatcher reclaims stale claims after 5 minutes |
| NFR-R4 | Bounded external calls | SMTP send has a 30 s timeout, and failure is recorded (FR-11.2). A JWKS fetch failure uses cached keys; with no usable key the request gets 401, never skipped validation |
| NFR-R5 | Background jobs survive failure | Per-item transactions, errors logged and counted, and the next run proceeds (FR-15.4) |

## Observability

- **Logs.** Structured JSON logs to stdout, with a correlation id taken from `traceparent` or
  generated, propagated to background work and written into outbox rows.
- **Health.** `/health/live` and `/health/ready`, the latter checking the database. Compose health
  checks use them.
- **Metrics.** OpenTelemetry metrics are exposed at `/metrics` (Prometheus format) for:
  - request rate, errors and latency per endpoint group;
  - `capacity-exhausted` count, as a business signal of undersupply;
  - booking capacity-lock hold duration, including cumulative counts under 50 ms, used by the
    500-way release load test to assert NFR-P3 from the booking handler's measurement;
  - sweep runs, failures and items processed;
  - outbox pending count and oldest pending age;
  - connection pool usage.
- **Alerts** (for operators with a metrics stack):
  - sweep failures on 2 consecutive runs;
  - an outbox item pending for more than 15 minutes;
  - a failed last-admin sync.
- **Audit is not observability.** `AuditLog` is a business record with its own access rules, and
  operational logs are kept separately. Logs follow the same personal-data rule as the audit log:
  no names, emails or tokens.

## Accessibility

- WCAG 2.1 AA ([03a](03a-design-system-and-ia.md#accessibility-baseline)).
- An automated axe-core scan in CI covers every route, including the attendee pages with seeded
  tokens. A new violation fails the build.
- A manual keyboard and screen-reader pass of the attendee book and manage flow is done before
  each release.

## Privacy and retention

- Attendee personal data (`name`, `email`) lives only on `Attendee`. It appears only on the
  Attendees screen, in the workspace (name and email only), and in the emails sent to that
  attendee. It never appears in `AuditLog`, `EmailLog` or logs.
- **Retention (decided).** The Coordinator's delete is the erasure mechanism (FR-4.5). No automatic
  anonymisation is shipped. A deploying organisation sets its own retention policy and applies it
  through the delete endpoint (for example, from an MCP agent). `AuditLog` and `EmailLog` rows
  survive deletion, keyed only by id, and contain no personal data.
- **Residency** is wherever the operator hosts the instance.

## Boundary values

These values are fixed. The predecessor left several of them open.

| Item | Value |
|---|---|
| `Attendee.name` / `email` | 1–200 / at most 320 characters. Email is trimmed and lower-cased, and unique |
| `Location.code` / `name` / `address` | at most 50 / 100 / 500 characters |
| `AppointmentType.code` / `name` | at most 50 / 100 characters |
| `AttendeeGroup.code` / `name` | at most 50 / 200 characters |
| `EventWindow.durationMinutes` | 15–720, in multiples of 15. It must not cross local midnight |
| Proposal lead time | The start instant must be in the future. There is no minimum notice and no business-hours rule |
| `ProposalAcceptance.headcount`, `EventCapacity.totalHeadcount` | 1–1000 |
| Types per proposal | 1–20 |
| `inviteExpiryDays` / `maxAutoRetryCount` / `inviteOptionCount` | 1–60 (default 7) / 0–10 (default 2) / 1–5 (default 3) |
| Attendee CSV | At most 1000 rows and 1 MB. UTF-8, with a header row |
| Locations per invite | 1–50 |
| Page size | 1–200, default 50 |
| `AuditLog.details` | At most 1000 characters |
| Workspace event window | End instant between 7 days ago and 14 days ahead |
| Dashboard events window | End instant between 7 days ago and 60 days ahead |
| Sweep interval / outbox poll | 15 min / 5 s |
| Rate limits | 30 per minute per IP and 10 per minute per token on attendee endpoints; 300 per minute per staff member |

## Verification

- **A load test** (k6, in `tests/load/`) issues 500 invites whose options all include one event with
  100 places for a shared type, then confirms all 500 concurrently. The outcomes must be:
  - exactly 100 bookings;
  - 400 `capacity-exhausted` responses;
  - zero deadlocks and zero 5xx responses;
  - NFR-P3 met.

  It runs before each release, not on every pull request.
- The concurrency tests in Infrastructure.Tests run on every pull request.

## Definition of done

A change is done when all of the following hold:

1. The ontology is updated and regenerated in the same commit, if the domain changed.
2. The requirement is covered by tests at the right level ([04](04-solution-architecture.md#testing-strategy)),
   and CI is green.
3. The REST endpoint, the MCP tool and the OpenAPI document all include the change, and the parity
   test passes.
4. The role guides under Help are updated for any user-visible change.
5. This design package is updated if the change alters a documented rule. The package is
   maintained alongside the code, not frozen.
6. The pull request carries its Narrative sections when it makes a decision ([AGENTS.md](../../AGENTS.md)).

A **release** is done when, in addition:

- the load test passes;
- the manual accessibility pass is done;
- images are published with a version tag;
- the home-lab upgrade steps have been exercised against a copy of the previous release's data.
