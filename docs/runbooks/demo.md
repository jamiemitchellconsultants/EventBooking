# Local demo walkthrough

Run this on a disposable local Compose project. The seed uses example identities and Mailpit; do
not use the local password or signing key in a real deployment. Start from a fresh clone with Docker
running and ports 5432, 8081, 1025, 8025, 5001, 5002 and 5003 available.

## Start the stack

From the repository root, run these commands in this order:

```bash
docker compose up --detach --build --wait postgres keycloak mailpit
docker compose --profile seed run --rm seed --demo --reanchor
docker compose up --detach --build --wait api mcp web
curl --fail http://localhost:5001/health/ready
curl --fail http://localhost:5001/api
curl --fail http://localhost:5003/health/ready
```

The seed job must report reference-data and attendee counts, invitation delivery and Keycloak
convergence. If it fails, stop and inspect
`docker compose logs seed postgres keycloak mailpit`; do not continue with a partial
dataset. Re-running `--demo --reanchor` is idempotent. Running the seed without `--demo` migrates
only. Use `--reseed` solely in a disposable local environment with
`EVENTBOOKING_ALLOW_RESEED=true` and `--demo`.

## Open the system

Open <http://localhost:5002> for Web, <http://localhost:5001/api> for API discovery and
<http://localhost:8025> for Mailpit. The local Keycloak realm is `eventbooking`. All eight demo
staff accounts use the local-only password `EventBooking1!`:

| Username | Role and scope | What to inspect |
| --- | --- | --- |
| `admin` | Admin | Locations, six appointment types, groups, settings and staff access |
| `coordinator` | Coordinator | Attendees, invitations, dashboards and audit |
| `coordinator.med` | Coordinator and Manager of MED | Both Coordinator and MED Manager routes |
| `manager.fit` | Manager of FIT | FIT proposal acceptance and capacity |
| `manager.ind` | Manager of IND | IND proposal acceptance and capacity |
| `manager.lab` | Manager of LAB | LAB proposal acceptance and capacity |
| `appointment.med` | AppointmentStaff for MED | MED appointment workspace |
| `appointment.unscoped` | AppointmentStaff without a type | Awaiting-assignment state |

Sign in as `admin` and open `/admin/appointment-types`. MED, FIT, IND and LAB are active and have
Managers. ESC is active without a Manager and cannot be selected for a proposal; DOC is inactive.
Open `/admin/locations` to see active London and Dublin locations and inactive Manchester. Open
`/admin/attendee-groups` to see the IND-only and CORE_THREE examples.

## Follow an invitation through booking

Sign out and sign in as `coordinator`. At `/attendees`, find Nia New,
`demo.attendee.01@example.test`, and inspect her IND requirement and invitation status. Open
Mailpit and find the invitation to that address. Use the link in that email in an anonymous browser
window. It opens `/book/{token}` with event choices and local time and location; choose an offered
event and confirm once. The confirmation displays a manage link. Copy it into the same anonymous
window and inspect the booking at `/manage/{token}`. Keep both links private and do not paste them
into logs or issue reports.

Return to the Coordinator session and refresh `/attendees`. Nia is booked. Open `/dashboards` and
`/audit` to inspect the changed counts and event history. A second use of the same book link is a
refusal naming the existing booking; it creates no second booking. Do not run the 500-way load
scenario on this general demo dataset; it has its own 100-place fixture.

## Inspect negotiation and appointments

Sign in as `coordinator.med` and open `/events/negotiate`. The seeded proposals show one of two
and two of four accepted types. Propose a new future London event listing MED and FIT, then sign
in as `manager.fit` and accept FIT with a positive headcount. The proposal becomes an active event
after both types accept. The proposing type is fixed; each Manager edits only their own headcount.

Sign in as `appointment.med` and open `/appointments`. Inspect the MED roster and the seeded
checked-in example. `appointment.unscoped` instead sees awaiting assignment. A new check-in is
allowed only on that event's local date, and a no-show only after its window ends. The reanchored
seed events are future dated, so this fresh-clone walkthrough does not claim a new check-in can be
performed immediately. Return on the relevant local date to exercise that transition.

## Close and troubleshoot

For a failed health check, inspect `docker compose ps` and `docker compose logs api postgres
keycloak mailpit`. For a missing invitation, inspect the seed command's result and Mailpit; do not
substitute a fabricated book token. Use `docker compose down` to stop the demo while retaining the
database volume. `docker compose down --volumes` deletes this local project's data.
