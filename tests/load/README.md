<!-- tests/load/README.md (complete) -->
# 500-invitation confirmation burst

Run this before each release against the local Task 29 stack. Install Docker Compose and k6,
then stop any normal local stack that occupies the same ports. The script uses a separate
`eventbooking-load` Compose project and requires a fresh disposable database volume.

From the repository root:

```bash
bash tests/load/run.sh
```

The runner starts PostgreSQL, Keycloak and Mailpit; runs the ordinary demo seed plus a guarded
load fixture; starts the API; then sends 500 one-shot confirmations to
`http://localhost:5001`. It writes 500 book tokens to ignored
`tests/load/fixture.json` with owner-only permissions. Treat that file as a secret and do not
commit or share it. The runner leaves the stack and logs for inspection. Recreate the
disposable project before another burst; never run against a normal or home-lab database:

```bash
docker compose -p eventbooking-load -f docker-compose.yml -f tests/load/compose.load.yml down --volumes
rm -f tests/load/fixture.json tests/load/postgres.log
```

The load-only override raises the attendee per-IP allowance to 600/min; normal local and
home-lab defaults remain 30/min. PostgreSQL keeps its default `max_connections` of 100: the
API's Npgsql pool defaults to 20 connections (design 08), so the 500 confirmations queue for a
pooled connection instead of exhausting the server. Each of the 500 book tokens occupies a
different token prefix partition, so the 10/min per-token policy remains enabled. The test passes only with
100 HTTP 201 bookings, 400 HTTP 409 `capacity-exhausted` problems, no unexpected responses,
no 5xx, no PostgreSQL deadlock, 500 new lock-hold samples and at least 475 samples strictly
under 50 ms. A missing or malformed metric fails the run.

If it fails, inspect k6 output and `tests/load/postgres.log`, then inspect API logs with
`docker compose -p eventbooking-load -f docker-compose.yml -f tests/load/compose.load.yml logs api`.
Do not reuse the partially booked fixture for a second measurement.
