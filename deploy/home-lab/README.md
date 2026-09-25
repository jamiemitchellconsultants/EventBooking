# EventBooking home-lab deployment

This deployment expects Docker Compose, curl, jq, envsubst and an existing Caddy container,
Keycloak service and external Docker network. It publishes no database or API host port. Copy
`.env.example` to `.env`, replace every example secret, pin `EVENTBOOKING_IMAGE_TAG` to a reviewed
`vX.Y.Z` or `sha-xxxxxxx` tag, then run `./install.sh`.

## Install

1. Confirm the shared Caddy container imports `EVENTBOOKING_CADDY_IMPORT_DIR/*.caddy` and joins
   `EVENTBOOKING_EDGE_NETWORK`.
2. Create `.env`; generate the two database passwords and token signing key independently.
3. Set the Keycloak Admin API credentials and protect `.env` as a secret-bearing file. The same
   credentials let later idempotent runs distinguish an existing realm from a failed request.
4. Run `./install.sh`; inspect `docker compose ps` and `docker compose logs`.
5. Verify `https://EVENTBOOKING_HOSTNAME/`, `/api`, `/mcp`, and authenticated `/mailpit`.

The API serves its discovery schema at /openapi/v1.json and its interactive Swagger UI at
/swagger. Both describe the deployed REST host.

Demo state is opt-in. Set `EVENTBOOKING_SEED_DEMO=true` only on a demonstration installation.
Destructive reseeding additionally requires `EVENTBOOKING_ALLOW_RESEED=true` and an explicit
manual `docker compose --profile seed run --rm eventbooking-seed --demo --reseed` command.

EventBooking.SeedData supports idempotent Keycloak demo convergence. The seed container maps the
deployment environment to Keycloak__BaseUrl, Keycloak__AdminUsername, Keycloak__AdminPassword and
Keycloak__DemoPassword. The realm owns roles; EventBooking owns appointment-type scope. Secrets
must not be committed.

## Upgrade and rollback

For an upgrade, change only `EVENTBOOKING_IMAGE_TAG`, run `docker compose pull`, run the seed job
without `--demo` to apply forward migrations, then run `docker compose up -d`. Verify readiness and
the principal user journeys before removing old images.

For rollback, restore the previous image tag and run `docker compose up -d`. Do not run a down migration. Every schema change must remain compatible with the previous image for one release.

## Backup

Start nightly retained backups with `docker compose --profile backup up -d eventbooking-backup`.
Custom-format dumps are written to `backups/` and files older than 14 days are removed. Monitor the
container and copy dumps to storage outside this host. This supplies an RPO of at most 24 hours;
operators needing a lower RPO must configure PostgreSQL WAL archiving separately, which this
repository documents as an extension but does not ship.

## Restore into a fresh volume

Never overwrite the current volume. Stop application writers, identify the dump, create a new
volume, restore into it, then start a second Compose project against that volume for validation:

```bash
docker compose stop eventbooking-api eventbooking-mcp eventbooking-seed
dump_file="backups/eventbooking-YYYYMMDDTHHMMSSZ.dump"
test -s "$dump_file"
docker volume create eventbooking-restored-db
docker run --detach --name eventbooking-restore-db \
  --network "${COMPOSE_PROJECT_NAME:-eventbooking}_eventbooking-private" \
  -e POSTGRES_DB=eventbooking -e POSTGRES_USER=eventbooking_owner \
  -e POSTGRES_PASSWORD="$EVENTBOOKING_DB_PASSWORD" \
  -e EVENTBOOKING_API_DB_PASSWORD="$EVENTBOOKING_DB_APP_PASSWORD" \
  -e EVENTBOOKING_MCP_DB_PASSWORD="$EVENTBOOKING_DB_APP_PASSWORD" \
  -v eventbooking-restored-db:/var/lib/postgresql/data \
  -v "$PWD/../postgres/init-roles.sh:/docker-entrypoint-initdb.d/10-roles.sh:ro" \
  postgres:16-alpine
until docker exec eventbooking-restore-db pg_isready -U eventbooking_owner -d eventbooking; do sleep 2; done
docker run --rm --network "${COMPOSE_PROJECT_NAME:-eventbooking}_eventbooking-private" \
  -e PGPASSWORD="$EVENTBOOKING_DB_PASSWORD" -v "$PWD/$dump_file:/restore.dump:ro" \
  postgres:16-alpine pg_restore --host=eventbooking-restore-db \
  --username=eventbooking_owner --dbname=eventbooking --exit-on-error /restore.dump
```

Start a second API container on the private network with its connection host changed to
`eventbooking-restore-db`. Validate `/health/ready`, row counts and one read-only user journey
before changing the production volume reference. Do not point `pg_restore` at the live container.
The recovery-time objective is two hours from declaring restore to validated readiness.

## Rehearsal evidence

Every install-changing pull request appends a dated record with host class, source and target image
tags, install duration, upgrade duration, fresh-volume restore duration, readiness result and the
operator. Absence of that record blocks the Task 30 commit.

### 2026-09-24 — initial home-lab rehearsal (Task 30)

- Host class: macOS arm64, Docker Desktop Linux VM; scratch Caddy, Keycloak 26 and edge network;
  local registry `localhost:5005`; base seed (`EVENTBOOKING_SEED_DEMO` unset).
- Source tag `rehearsal2`, target tag `rehearsal3` (retag of the same images; pull, migrate, up).
- Install: 19.9 s (`./install.sh` exit 0, 21:01:45Z–21:02:05Z), all services up, API `/health/ready`
  200.
- Upgrade: 10.2 s (pull, seed without `--demo`, `up -d`; 21:02:40Z–21:02:51Z), API on `rehearsal3`,
  `/health/ready` 200, marker location `TIMED` survived.
- Fresh-volume restore: 35 s from declaring restore to validated readiness
  (21:03:21Z–21:03:56Z), well under the two-hour RTO. `pg_restore --exit-on-error` exit 0 into
  `eventbooking-restored-db`; second API on the restored volume returned `/health/ready` 200,
  `/health/live` 200 and the anonymous `/api` index 200; restored row counts matched the live
  database exactly (1 location, 1 settings row); the original `eventbooking_eventbooking-db`
  volume still exists and is unchanged.
- Operator: Muse Code.

## Verification before and after a release

Record the previous and target `EVENTBOOKING_IMAGE_TAG` values, the backup filename and the
operator before changing the tag. Verify that the existing stack passes `/health/ready` and that
a recent custom-format dump is present outside the host. Run the [local load scenario](../../tests/load/README.md)
against the local Compose topology for the release candidate; it is not a home-lab traffic
generator. Complete the manual attendee keyboard and screen-reader pass required by design 08.

After a version upgrade, check `docker compose ps`, the API readiness route through the private
network, the public Web route, `/api` and `/mcp` through ingress, one authenticated staff read and
one anonymous invitation view. Check the outbox backlog and recent sweep failures in metrics and
structured logs. Do not write tokens or attendee addresses into the release record.

If the new image fails, restore the previous `EVENTBOOKING_IMAGE_TAG` and run
`docker compose up -d`. Do not run a down migration. If the database is damaged, follow the
fresh-volume restore above and prove readiness and a read-only journey before repointing the
application. Keep the original volume until the recovery has been accepted.

## Routine operations

Monitor `/health/ready`, sweep failures, pending outbox age and capacity-exhausted counts.
Investigate an outbox item pending longer than 15 minutes or two consecutive failed sweeps. Check
the nightly backup container's exit status and verify a restorable dump exists every day; a backup
file alone is not a successful restore rehearsal. The deployment target is at most 24 hours of
data loss and two hours to validated readiness after a restore declaration.
