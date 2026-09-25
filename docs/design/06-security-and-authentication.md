# 06 — Security and Authentication

[← API design](05-api-design.md) · [Deployment →](07-deployment.md)

EventBooking has two kinds of identity:

- **staff**, who authenticate with an OIDC identity provider;
- **attendees**, who have no account and authenticate by possessing a token.

The two schemes use disjoint endpoints and are never accepted on each other's routes.

## Staff authentication (Keycloak by default)

- The Web front end uses the authorization-code flow with PKCE against the configured authority,
  and sends the access token as a bearer token. The API never handles passwords.
- The API validates, on every request:
  - the signature against the authority's JWKS, cached and refreshed when a key id is not
    recognised;
  - `iss` and `aud`;
  - `exp` and `nbf`, with 60 seconds of clock skew.

  Failure returns 401.
- Keycloak is the shipped provider (decision D8). The realm, named `eventbooking`, defines:
  - a public client, `eventbooking-web`, with PKCE, restricted redirect URIs and web origins;
  - the realm roles `Admin`, `Coordinator`, `Manager` and `AppointmentStaff`;
  - protocol mappers emitting a flat multi-valued `roles` claim, `staff_id` (from a user attribute)
    and `name` (full name) into the access token.
- Any other OIDC provider can be used by setting `Auth__Authority`, `Auth__Audience` and the claim
  names. The application never calls a provider's admin API.
- **Roles live in the identity provider; scope lives in EventBooking.** The provider is the only
  source of truth for who holds which role. EventBooking owns only which `AppointmentType` a
  scoped role points at.

### `StaffId` and `StaffIdentity`

- The `staff_id` claim is required. It is trimmed, upper-cased and matched against
  `Identity__StaffIdPattern`. If it is missing or does not match, every staff endpoint except
  `GET /api/me` returns 403. `GET /api/me` explains the problem, and no `StaffIdentity` row is
  written. The system never audits an actor it cannot name.
- `StaffIdentity` mirrors (`staffUserId`, `staffId`, `displayName`, `lastSeenAt`). It is upserted at
  most every 15 minutes per identity, and `staffId` is unique.

### Role synchronisation on every request (carried hardening)

Authorization middleware loads the caller's `StaffAccessProfile` once per request and compares its
`roles` with the token's:

| Situation | Action |
|---|---|
| Roles differ | Update `roles` and audit `StaffRolesSynced` with actor type `System`. Prune the scope if the new roles no longer include a scoped role |
| Token asserts roles and no profile exists | Create the profile with a null scope |
| Token asserts no roles | Remove the profile |
| The change would remove the last Admin | Refuse the sync, keep the stored profile, and alert |

The comparison runs on every REST and MCP request, so a reduced role takes effect on the caller's
next request, bounded only by token lifetime. Access tokens should live for 5 minutes, configured
in the realm.

## Authorization

Every handler demands exactly one `StaffCapability`. Handlers never check a `Role` directly.

| Capability | Admin | Coordinator | Manager (scoped) | AppointmentStaff (scoped) |
|---|:---:|:---:|:---:|:---:|
| `ManageSettings` | ✔ | | | |
| `ManageReferenceData` | ✔ | | | |
| `ManageEventGroups` | ✔ | ✔ | | |
| `ManageStaffAccess` | ✔ | | | |
| `ManageAttendees` | | ✔ | | |
| `ViewAttendeeDashboards` | | ✔ | | |
| `ViewAttendeeAudit` | | ✔ | | |
| `ViewEventAudit` | ✔ | ✔ | | |
| `ManageEventNegotiation` | | | ✔ | |
| `ViewEventOperations` | ✔ | ✔ | | |
| `CancelEvent` | ✔ | ✔ | ✔ (events listing their type) | |
| `ConductAppointments` | | | ✔ (own type) | ✔ (own type) |

Evaluation order, identical for REST and MCP:

1. The token is valid and `StaffId` is present.
2. Roles are synchronised.
3. **Null-scope gate**: if the profile holds a scoped role with a null `appointmentTypeId`, **no**
   capability is granted (carried hardening).
4. Look up the capability from the role union.
5. Apply the scope filter inside the handler, for example "events listing the caller's type".
6. **Admin data gate**: attendee read models refuse to execute for an Admin-shaped caller, so the
   exclusion holds even against a hand-crafted request.

Role-union rules from the ontology: Admin is exclusive; Coordinator may combine with Manager and/or
AppointmentStaff; Manager and AppointmentStaff share one scope; there is at most one Manager per
type.

## Attendee authentication

This settles the token lifecycle the predecessor left open (decision D14).

| Token | Issued | Valid while | Invalidated by |
|---|---|---|---|
| Book token | When the `Invite` is created (`tokenVersion` = 1) | The `Invite` is `Pending` and `now < expiresAt` | The invite becoming `Used`, `Expired`, `Superseded` or `Cancelled` |
| Manage token | When the `Booking` is created (`manageTokenVersion` = 1). It is shown on the confirmation page and in the `BookingConfirmation` email | The `Booking` exists. Actions are allowed only while it is `Active` and the window has not started; otherwise the token is read-only | Nothing in the first release. After the event has ended, the page shows history only |
| Registration token | When the `SelfRegistration` is submitted (`tokenVersion` = 1). It is returned in the submit response and emailed in the confirmation path | The request is `Pending` and `now < expiresAt` | Confirmation, which is single-use; a repeated confirmation reports the existing booking |

Token format and handling:

- **Format:** `base64url(purpose ‖ id ‖ version ‖ HMAC-SHA256(key, purpose ‖ id ‖ version))`,
  where `purpose` is book, manage or registration and `id` is the `Invite`, `Booking` or
  `SelfRegistration` request id. The token is
  deterministic: the server can reproduce the same link at email-dispatch time without ever
  storing it. That is what lets the confirmation page and the confirmation email share one manage
  link.
- **Validation:** recompute the HMAC and compare it in constant time before any database access,
  which cheaply rejects forgery and enumeration. Then load the row, and require that its stored
  version equals the token's version and that its state permits the operation.
- **Storage:** only the version counter is stored (`Invite.tokenVersion`,
  `Booking.manageTokenVersion`). The raw token appears only in the URL, and is never logged,
  audited or stored. The version exists so that a single link can be revoked by incrementing it;
  no first-release screen does this, and resending an email reuses the current link.
- **Reuse:** a token is reusable for viewing and acting until it is invalidated. It is not consumed
  per request. Actions are idempotent against the final state.
- **Unknown or invalid token:** return 404 `token-invalid`. An expired token returns 410, and the
  page explains who to contact.

`Tokens__SigningKey` must be at least 32 bytes. Startup fails if it is shorter or matches a
denylist of placeholder values. It is rotated by deploying a new key, which invalidates every
outstanding link. That is acceptable, because a Coordinator can resend.

## Rate limiting

- **Attendee token endpoints (carried hardening):** a sliding window **per client IP**, 30 requests per minute by
  default, and per token prefix, 10 requests per minute. This way one busy network cannot starve
  other attendees, and one leaked link cannot be hammered. Forwarded headers are trusted only from
  the configured reverse-proxy network.
- **Staff endpoints:** 300 requests per minute per `staffUserId`, as defence in depth.
- **Public event-group endpoints:** the shared remote-IP limiter, like the other anonymous
  routes: no staff token is ever accepted or required there, and the plain public HTTP client
  sends none.
- A rejected request returns 429 with `Retry-After`.

## Audit

- `AuditLog` is append-only. The database role used by the application has no `UPDATE` or `DELETE`
  on it.
- Every entry records actor type (`Staff`, `AttendeeToken` or `System`) and actor id: `StaffId` for
  staff, the `Invite` or `Booking` id for token actors.
- `details` is structured JSON of at most 1000 characters, holding identifiers, codes and old and
  new values only. It never holds names, emails, tokens, URLs or free text (carried hardening).
- Searching the audit log is itself not audited, but it is logged operationally with the caller
  and filters.

## Data protection

- **Least data by screen.** The workspace's never-sent list (FR-8.1) is enforced by the read model,
  not by the UI. The public group payload carries no capacities, emails or tokens.
- **Terminal retention.** A terminal `SelfRegistration` (name, email) is purged with its
  confirmation email rows 30 days after `terminalAt` (FR-16.6). Expiry is audited with the
  request id and status only; names, emails and tokens never enter the audit log.
- **CSV injection.** Every generated CSV neutralises cells that start with a formula character.
- **Transport.** TLS terminates at the reverse proxy (Caddy) with automatic certificates. The
  containers talk over an internal Docker network with no published database port. Nothing is
  exposed over plain HTTP except the local development stack.
- **CORS.** Only the configured web origins are allowed; never a wildcard.
- **Security headers,** served by the web container:
  - a Content Security Policy that allows only self, the API origin and the identity-provider
    origin, and permits WebAssembly;
  - `X-Content-Type-Options: nosniff`;
  - `Referrer-Policy: no-referrer`, so attendee tokens in URLs never leak through the Referer
    header;
  - `X-Frame-Options: DENY`.
- **Secrets.** The signing key, database password and SMTP credentials come from environment
  variables supplied by the deployment, in `.env` files kept outside the repository. Startup
  validates them, and they are never committed.
- **Database roles.** A migration role owns the schema. The application role has only the DML it
  needs, and none on `AuditLog` beyond `INSERT` and `SELECT`.
- **Containers.** Containers run as non-root with read-only root file systems where possible, and
  images are built from a `.dockerignore`-filtered context.
