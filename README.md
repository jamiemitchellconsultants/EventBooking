# EventBooking

EventBooking coordinates multi-type events, invitations, bookings and appointments. Managers
propose event times and commit capacity for their own appointment types. Coordinators invite
attendees and manage follow-up. Attendees book from a private link. Staff work within their
assigned roles and appointment-type scope.

## Local quick start

Requires Docker with Compose. From a fresh clone:

```bash
docker compose up --detach --build --wait postgres keycloak mailpit
docker compose --profile seed run --rm seed --demo --reanchor
docker compose up --detach --build --wait api mcp web
curl --fail http://localhost:5001/health/ready
```

Open the Web app at <http://localhost:5002>, the API index at
<http://localhost:5001/api>, and Mailpit at <http://localhost:8025>. The REST discovery document
is served at /api, the OpenAPI schema at /openapi/v1.json and the interactive Swagger UI at
/swagger; the MCP endpoint is at /mcp. Staff endpoints require an OIDC bearer token. The demo
seed is opt-in; running the seed container without `--demo` applies database roles and migrations
only. The local-only demo password is `EventBooking1!`.

Follow the [demo runbook](docs/runbooks/demo.md) for the seeded users and a complete walkthrough.
For the 500-confirmation capacity race, follow [the load-test guide](tests/load/README.md).

## Documentation

- [Design package](docs/design/README.md) — requirements, architecture, security, deployment and
  non-functional targets.
- [Application ontology](docs/ontology.md) — canonical domain terminology.
- [Detailed implementation plans](docs/detailed-implementations/README.md) — task-by-task build
  instructions and their verification status.
- [Home-lab operator runbook](deploy/home-lab/README.md) — install, upgrade, backup and recovery.

## Development checks

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
node scripts/build-ontology.mjs --check
node scripts/check-ontology-terms.mjs
```

Docker must be running for the PostgreSQL container tests. A change that touches domain concepts
starts in `docs/ontology.ttl`; regenerate `docs/ontology.md` with
`node scripts/build-ontology.mjs`. Pull requests follow [AGENTS.md](AGENTS.md), including
code-owner review, the narrative decision sections when applicable and the AI fingerprint.
