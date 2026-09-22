# 06c — Home-lab deployment and recovery (Task 30)

[← Phase overview](phase-6-seed-and-deployment.md) · [Previous task](phase-6b-local-compose.md) · [Ontology](../ontology.md)

This task is the production-like infrastructure layer of Phase 6. It places EventBooking behind an
existing Caddy ingress and Keycloak, isolates data services, and turns install, upgrade, backup and
restore into rehearsable operator procedures.

> Use superpowers:executing-plans. Apply this document after Task 29 on the Phase 6 branch.

**Goal:** Ship a pin-able, secret-free home-lab deployment whose installer is idempotent and whose
backup can be restored into a new volume without touching the last known-good database.

**Architecture:** The shared ingress reaches only Web, MCP and the protected Mailpit route through
the external edge network. Database traffic remains on an internal network. API also joins edge for
outbound Keycloak discovery but publishes no host port. Templates are rendered into ignored files;
the checked-in realm, Web settings and ingress fragment retain explicit substitution tokens.

**Tech Stack:** Docker Compose, Caddy 2, PostgreSQL 16, Mailpit, Keycloak Admin REST, Bash,
`envsubst`, curl, jq and shellcheck.

**Spec:** [Master Task 30](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[home lab](../design/07-deployment.md#home-lab-deployhome-lab), and
[reliability](../design/08-nonfunctional-requirements.md#reliability).

### Task 30: Home-lab deployment, installer and recovery runbook

**Files:**

- Create: deploy/home-lab/docker-compose.yml
- Create: deploy/home-lab/web/Caddyfile
- Create: deploy/home-lab/web/Dockerfile
- Create: deploy/home-lab/web/appsettings.json
- Create: deploy/home-lab/caddy/eventbooking.caddy
- Modify: deploy/home-lab/keycloak/eventbooking-realm.json
- Create: deploy/home-lab/seed/Dockerfile
- Create: deploy/home-lab/theme.css
- Create: deploy/home-lab/.env.example
- Create: deploy/home-lab/.gitignore
- Create: deploy/home-lab/install.sh
- Modify: deploy/home-lab/README.md
- Modify: .github/workflows/compose-smoke.yml

**Interfaces:**

```csharp
namespace EventBooking.Deployment;

public sealed record HomeLabContract
{
    // Required public and shared-service inputs. Secrets exist only in ignored .env.
    public required string Hostname { get; init; }
    public required string ImageTag { get; init; }
    public required string KeycloakUrl { get; init; }
    public required string EdgeNetwork { get; init; }

    // Database and API expose no host ports. Only these route prefixes cross shared ingress.
    public static readonly string[] PublicRoutes = ["/", "/mcp", "/mailpit"];

    // Restore always targets a new named volume; rollback never runs a down migration.
    public const int BackupRetentionDays = 14;
    public static readonly TimeSpan RecoveryTimeObjective = TimeSpan.FromHours(2);

    // The operator-overridable theme.css is mounted read-only into the Web image.
    public const string ThemeCssPath = "/srv/theme.css";
}
```

- [ ] **Step 1: Extend the failing deployment smoke checks**

Replace the Task 29 workflow with this complete version. The local job is unchanged in behaviour;
the new job parses every home-lab artifact without requiring a shared Caddy or Keycloak instance.

```yaml
# .github/workflows/compose-smoke.yml (complete after Task 30)
name: Compose smoke

on:
  pull_request:
    paths:
      - "docker-compose.yml"
      - ".dockerignore"
      - "deploy/**"
      - "src/**/Dockerfile*"
      - ".github/workflows/compose-smoke.yml"
  workflow_dispatch:

permissions:
  contents: read

concurrency:
  group: compose-smoke-${{ github.ref }}
  cancel-in-progress: true

jobs:
  validate-home-lab:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4.4.0
      - name: Install format checkers
        run: |
          sudo apt-get update
          sudo apt-get install --yes gettext-base shellcheck
      - name: Validate deployment files
        working-directory: deploy/home-lab
        run: |
          shellcheck install.sh ../postgres/init-roles.sh
          jq --exit-status . keycloak/eventbooking-realm.json >/dev/null
          docker compose --env-file .env.example config --quiet
          docker run --rm -v "$PWD/web/Caddyfile:/etc/caddy/Caddyfile:ro" caddy:2.10-alpine caddy validate --config /etc/caddy/Caddyfile

  local-compose:
    runs-on: ubuntu-latest
    timeout-minutes: 25
    steps:
      - uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4.4.0
      - name: Validate shell and Compose files
        run: |
          sudo apt-get update
          sudo apt-get install --yes shellcheck
          shellcheck deploy/postgres/init-roles.sh
          docker compose config --quiet
      - name: Start dependencies
        run: docker compose up --detach --build --wait postgres keycloak mailpit
      - name: Apply migrations and demo seed
        run: docker compose --profile seed run --rm seed --demo --reanchor
      - name: Start application services
        run: docker compose up --detach --build --wait api mcp web
      - name: Probe readiness, discovery and invitations
        run: |
          curl --fail --retry 20 --retry-all-errors --retry-delay 3 http://localhost:5001/health/ready
          curl --fail --retry 10 --retry-all-errors --retry-delay 2 http://localhost:5001/api
          curl --fail --retry 10 --retry-all-errors --retry-delay 2 http://localhost:5003/health/ready
          curl --fail --retry 10 --retry-all-errors --retry-delay 2 http://localhost:5002/
          message_count="$(curl --fail --silent http://localhost:8025/api/v1/messages | jq '.messages | length')"
          test "$message_count" -ge 1
      - name: Capture logs
        if: always()
        run: docker compose logs --no-color | tee compose-smoke.log
      - name: Stop stack
        if: always()
        run: docker compose down --volumes --remove-orphans
```

Run it locally before creating the files:

```bash
cd deploy/home-lab
docker compose --env-file .env.example config --quiet
shellcheck install.sh
```

Expected: FAIL because the home-lab Compose model and installer do not exist.

- [ ] **Step 2: Implement the isolated home-lab topology**

Create this complete Compose model. Images are pulled by immutable operator-selected tag; no source
checkout is needed after templates have been copied. The backup profile is opt-in.

```yaml
# deploy/home-lab/docker-compose.yml (complete)
name: eventbooking

x-app-environment: &app-environment
  ASPNETCORE_URLS: http://+:8080
  Auth__Authority: ${EVENTBOOKING_KEYCLOAK_URL:?required}/realms/eventbooking
  Auth__Audience: eventbooking-web
  Tokens__SigningKey: ${EVENTBOOKING_TOKENS_SIGNING_KEY:?required}
  Email__Smtp__Host: ${EVENTBOOKING_SMTP_HOST:-eventbooking-mailpit}
  Email__Smtp__Port: ${EVENTBOOKING_SMTP_PORT:-1025}
  Email__Smtp__Username: ${EVENTBOOKING_SMTP_USERNAME:-}
  Email__Smtp__Password: ${EVENTBOOKING_SMTP_PASSWORD:-}
  Email__FromAddress: ${EVENTBOOKING_SMTP_FROM_ADDRESS:-eventbooking@localhost}
  Email__FromName: ${EVENTBOOKING_SMTP_FROM_NAME:-EventBooking}
  Portal__BaseUrl: https://${EVENTBOOKING_HOSTNAME:?required}
  Portal__CoordinatorContact: ${EVENTBOOKING_COORDINATOR_CONTACT:?required}
  Cors__AllowedOrigins__0: https://${EVENTBOOKING_HOSTNAME:?required}
  Proxy__Networks__0: ${EVENTBOOKING_PROXY_NETWORK:-172.16.0.0/12}
  Identity__StaffIdPattern: ${EVENTBOOKING_STAFF_ID_PATTERN:-^DEMO[0-9]{3}$}

services:
  eventbooking-db:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: eventbooking
      POSTGRES_USER: eventbooking_owner
      POSTGRES_PASSWORD: ${EVENTBOOKING_DB_PASSWORD:?required}
      EVENTBOOKING_API_DB_PASSWORD: ${EVENTBOOKING_DB_APP_PASSWORD:?required}
      EVENTBOOKING_MCP_DB_PASSWORD: ${EVENTBOOKING_DB_APP_PASSWORD:?required}
    volumes:
      - eventbooking-db:/var/lib/postgresql/data
      - ../postgres/init-roles.sh:/docker-entrypoint-initdb.d/10-roles.sh:ro
    networks: [eventbooking-private]
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U eventbooking_owner -d eventbooking"]
      interval: 10s
      timeout: 5s
      retries: 20

  eventbooking-mailpit:
    image: axllent/mailpit:v1.27.8
    environment:
      MP_WEBROOT: /mailpit/
    networks: [eventbooking-private, edge]
    healthcheck:
      test: ["CMD", "/mailpit", "readyz"]
      interval: 10s
      timeout: 5s
      retries: 20

  eventbooking-api:
    image: ghcr.io/jamiemitchellconsultants/eventbooking-api:${EVENTBOOKING_IMAGE_TAG:-latest}
    environment:
      <<: *app-environment
      ConnectionStrings__EventBooking: Host=eventbooking-db;Database=eventbooking;Username=eventbooking_api;Password=${EVENTBOOKING_DB_APP_PASSWORD:?required}
    networks: [eventbooking-private, edge]
    depends_on:
      eventbooking-db: { condition: service_healthy }
      eventbooking-mailpit: { condition: service_healthy }
    healthcheck:
      test: ["CMD", "bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080; printf 'GET /health/ready HTTP/1.1\\r\\nHost: localhost\\r\\nConnection: close\\r\\n\\r\\n' >&3; grep -q '200 OK' <&3"]
      interval: 10s
      timeout: 5s
      retries: 20
      start_period: 20s

  eventbooking-mcp:
    image: ghcr.io/jamiemitchellconsultants/eventbooking-mcp:${EVENTBOOKING_IMAGE_TAG:-latest}
    environment:
      <<: *app-environment
      ConnectionStrings__EventBooking: Host=eventbooking-db;Database=eventbooking;Username=eventbooking_mcp;Password=${EVENTBOOKING_DB_APP_PASSWORD:?required}
    networks: [eventbooking-private, edge]
    depends_on:
      eventbooking-db: { condition: service_healthy }

  eventbooking-web:
    image: ghcr.io/jamiemitchellconsultants/eventbooking-web-caddy:${EVENTBOOKING_IMAGE_TAG:-latest}
    environment:
      EVENTBOOKING_KEYCLOAK_URL: ${EVENTBOOKING_KEYCLOAK_URL:?required}
    volumes:
      - ./.rendered/web-appsettings.json:/srv/appsettings.json:ro
      - ./theme.css:/srv/theme.css:ro
    networks: [eventbooking-private, edge]
    depends_on:
      eventbooking-api: { condition: service_healthy }

  eventbooking-seed:
    profiles: [seed]
    image: ghcr.io/jamiemitchellconsultants/eventbooking-seed:${EVENTBOOKING_IMAGE_TAG:-latest}
    environment:
      ConnectionStrings__EventBooking: Host=eventbooking-db;Database=eventbooking;Username=eventbooking_owner;Password=${EVENTBOOKING_DB_PASSWORD:?required}
      Keycloak__BaseUrl: ${EVENTBOOKING_KEYCLOAK_URL:?required}
      Keycloak__Realm: eventbooking
      Keycloak__AdminRealm: master
      Keycloak__AdminUsername: ${EVENTBOOKING_KEYCLOAK_SEED_ADMIN_USERNAME:-}
      Keycloak__AdminPassword: ${EVENTBOOKING_KEYCLOAK_SEED_ADMIN_PASSWORD:-}
      Keycloak__DemoPassword: ${EVENTBOOKING_KEYCLOAK_DEMO_PASSWORD:-}
      Keycloak__RealmExportPath: /config/eventbooking-realm.json
      Tokens__SigningKey: ${EVENTBOOKING_TOKENS_SIGNING_KEY:?required}
      Email__Smtp__Host: ${EVENTBOOKING_SMTP_HOST:-eventbooking-mailpit}
      Email__Smtp__Port: ${EVENTBOOKING_SMTP_PORT:-1025}
      Email__FromAddress: ${EVENTBOOKING_SMTP_FROM_ADDRESS:-eventbooking@localhost}
      Email__FromName: ${EVENTBOOKING_SMTP_FROM_NAME:-EventBooking}
      Portal__BaseUrl: https://${EVENTBOOKING_HOSTNAME:?required}
      Portal__CoordinatorContact: ${EVENTBOOKING_COORDINATOR_CONTACT:?required}
      Identity__StaffIdPattern: ${EVENTBOOKING_STAFF_ID_PATTERN:-^DEMO[0-9]{3}$}
      EVENTBOOKING_ALLOW_RESEED: ${EVENTBOOKING_ALLOW_RESEED:-false}
    volumes:
      - ./.rendered/eventbooking-realm.json:/config/eventbooking-realm.json:ro
    networks: [eventbooking-private, edge]
    depends_on:
      eventbooking-db: { condition: service_healthy }
      eventbooking-mailpit: { condition: service_healthy }

  eventbooking-backup:
    profiles: [backup]
    image: postgres:16-alpine
    environment:
      PGPASSWORD: ${EVENTBOOKING_DB_PASSWORD:?required}
    command:
      - /bin/sh
      - -ec
      - |
        while true; do
          stamp="$$(date -u +%Y%m%dT%H%M%SZ)"
          pg_dump --host=eventbooking-db --username=eventbooking_owner --dbname=eventbooking --format=custom --file="/backups/eventbooking-$${stamp}.dump"
          find /backups -type f -name 'eventbooking-*.dump' -mtime +14 -delete
          sleep 86400
        done
    volumes:
      - ./backups:/backups
    networks: [eventbooking-private]
    depends_on:
      eventbooking-db: { condition: service_healthy }
    restart: unless-stopped

volumes:
  eventbooking-db:

networks:
  eventbooking-private:
    internal: true
  edge:
    external: true
    name: ${EVENTBOOKING_EDGE_NETWORK:-edge}
```

Create the Caddy Web image and runtime configuration:

```dockerfile
# deploy/home-lab/web/Dockerfile (complete)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/EventBooking.Web/EventBooking.Web.csproj -c Release -o /out

FROM caddy:2.10-alpine AS runtime
COPY --from=build /out/wwwroot /srv
COPY deploy/home-lab/web/Caddyfile /etc/caddy/Caddyfile
USER 1000:1000
EXPOSE 8080
```

```caddyfile
# deploy/home-lab/web/Caddyfile (complete)
:8080 {
    header {
        Content-Security-Policy "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; script-src 'self' 'wasm-unsafe-eval'; connect-src 'self' {$EVENTBOOKING_KEYCLOAK_URL}; img-src 'self' data:; style-src 'self' 'unsafe-inline'; font-src 'self'"
        X-Content-Type-Options "nosniff"
        Referrer-Policy "no-referrer"
        X-Frame-Options "DENY"
    }
    handle /api* {
        reverse_proxy eventbooking-api:8080
    }
    handle /mcp* {
        reverse_proxy eventbooking-mcp:8080
    }
    handle {
        root * /srv
        encode zstd gzip
        try_files {path} /index.html
        file_server
    }
}
```

```json
{
  "ApiBaseUrl": "https://EVENTBOOKING_HOSTNAME",
  "ProductName": "EventBooking",
  "LogoPath": null,
  "CoordinatorContact": "EVENTBOOKING_COORDINATOR_CONTACT",
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "EVENTBOOKING_KEYCLOAK_URL/realms/eventbooking",
      "ClientId": "eventbooking-web"
    }
  }
}
```

```caddyfile
# deploy/home-lab/caddy/eventbooking.caddy (complete template)
EVENTBOOKING_HOSTNAME {
    handle /mailpit* {
        basic_auth {
            operator EVENTBOOKING_MAILPIT_BASIC_AUTH
        }
        reverse_proxy eventbooking-mailpit:8025
    }
    handle /mcp* {
        reverse_proxy eventbooking-mcp:8080
    }
    handle {
        reverse_proxy eventbooking-web:8080
    }
}
```

The Task 30 seed Dockerfile is intentionally the same source build as the publication Dockerfile;
this lets an operator build a reviewed checkout even when GHCR is unavailable.

```dockerfile
# deploy/home-lab/seed/Dockerfile (complete)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/EventBooking.SeedData/EventBooking.SeedData.csproj -c Release -o /out --no-self-contained

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "EventBooking.SeedData.dll"]
```

```css
/* deploy/home-lab/theme.css (complete neutral default; operators may replace this file) */
:root {
  --eventbooking-accent: #315f78;
  --eventbooking-surface: #ffffff;
  --eventbooking-text: #17212b;
}
```

- [ ] **Step 3: Implement the realm, environment and exact seven-step installer**

Replace the home-lab realm with the local realm's roles and mappers, changing only transport and
redirect values. This is the complete checked-in template:

```json
{
  "realm": "eventbooking",
  "enabled": true,
  "sslRequired": "external",
  "accessTokenLifespan": 300,
  "registrationAllowed": false,
  "resetPasswordAllowed": true,
  "attributes": {
    "userProfileEnabled": "true"
  },
  "components": {
    "org.keycloak.userprofile.UserProfileProvider": [
      {
        "name": "declarative-user-profile",
        "providerId": "declarative-user-profile",
        "subComponents": {},
        "config": {
          "config-pieces-count": ["1"],
          "config-piece-0": [
            "{\"attributes\":[{\"name\":\"username\",\"displayName\":\"Username\",\"validations\":{\"length\":{\"min\":3,\"max\":255},\"username-prohibited-characters\":{}}},{\"name\":\"email\",\"displayName\":\"Email\",\"validations\":{\"email\":{},\"length\":{\"max\":255}}},{\"name\":\"firstName\",\"displayName\":\"First name\",\"permissions\":{\"view\":[\"user\",\"admin\"],\"edit\":[\"user\",\"admin\"]}},{\"name\":\"lastName\",\"displayName\":\"Last name\",\"permissions\":{\"view\":[\"user\",\"admin\"],\"edit\":[\"user\",\"admin\"]}},{\"name\":\"staffId\",\"displayName\":\"Staff number\",\"required\":{\"roles\":[\"user\",\"admin\"]},\"permissions\":{\"view\":[\"user\",\"admin\"],\"edit\":[\"admin\"]},\"validations\":{\"pattern\":{\"pattern\":\"^[A-Z0-9]{1,32}$\"}}}],\"groups\":[]}"
          ]
        }
      }
    ]
  },
  "roles": {
    "realm": [
      { "name": "Admin" },
      { "name": "Coordinator" },
      { "name": "Manager" },
      { "name": "AppointmentStaff" }
    ]
  },
  "clients": [
    {
      "clientId": "eventbooking-web",
      "name": "EventBooking Web",
      "enabled": true,
      "publicClient": true,
      "standardFlowEnabled": true,
      "directAccessGrantsEnabled": false,
      "redirectUris": [
        "https://EVENTBOOKING_HOSTNAME/authentication/login-callback",
        "https://EVENTBOOKING_HOSTNAME/authentication/logout-callback"
      ],
      "webOrigins": ["https://EVENTBOOKING_HOSTNAME"],
      "attributes": {
        "pkce.code.challenge.method": "S256",
        "post.logout.redirect.uris": "https://EVENTBOOKING_HOSTNAME/authentication/logout-callback"
      },
      "protocolMappers": [
        {
          "name": "audience",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-audience-mapper",
          "config": {
            "included.client.audience": "eventbooking-web",
            "id.token.claim": "false",
            "access.token.claim": "true"
          }
        },
        {
          "name": "roles",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-realm-role-mapper",
          "config": {
            "claim.name": "roles",
            "multivalued": "true",
            "jsonType.label": "String",
            "id.token.claim": "true",
            "access.token.claim": "true",
            "userinfo.token.claim": "true"
          }
        },
        {
          "name": "staff_id",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-attribute-mapper",
          "config": {
            "user.attribute": "staffId",
            "claim.name": "staff_id",
            "jsonType.label": "String",
            "id.token.claim": "true",
            "access.token.claim": "true",
            "userinfo.token.claim": "true"
          }
        },
        {
          "name": "name",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-full-name-mapper",
          "config": {
            "id.token.claim": "true",
            "access.token.claim": "true",
            "userinfo.token.claim": "true"
          }
        }
      ]
    }
  ]
}
```

```dotenv
# deploy/home-lab/.env.example (complete; these are examples, never deployment secrets)
EVENTBOOKING_HOSTNAME=events.example.test
EVENTBOOKING_IMAGE_TAG=latest
EVENTBOOKING_DB_PASSWORD=replace-with-generated-owner-password
EVENTBOOKING_DB_APP_PASSWORD=replace-with-generated-app-password
EVENTBOOKING_TOKENS_SIGNING_KEY=replace-with-at-least-32-random-bytes
EVENTBOOKING_KEYCLOAK_URL=https://identity.example.test
EVENTBOOKING_KEYCLOAK_SEED_ADMIN_USERNAME=
EVENTBOOKING_KEYCLOAK_SEED_ADMIN_PASSWORD=
EVENTBOOKING_KEYCLOAK_DEMO_PASSWORD=
EVENTBOOKING_SMTP_HOST=
EVENTBOOKING_SMTP_PORT=1025
EVENTBOOKING_SMTP_USERNAME=
EVENTBOOKING_SMTP_PASSWORD=
EVENTBOOKING_SMTP_FROM_ADDRESS=eventbooking@example.test
EVENTBOOKING_SMTP_FROM_NAME=EventBooking
EVENTBOOKING_COORDINATOR_CONTACT=coordinator@example.test
EVENTBOOKING_EDGE_NETWORK=edge
EVENTBOOKING_PROXY_NETWORK=172.16.0.0/12
EVENTBOOKING_MAILPIT_BASIC_AUTH='$2a$14$examplehashmustbereplaced'
EVENTBOOKING_CADDY_IMPORT_DIR=/srv/caddy/imports
EVENTBOOKING_SHARED_CADDY_CONTAINER=caddy
EVENTBOOKING_SEED_DEMO=false
EVENTBOOKING_ALLOW_RESEED=false
```

```gitignore
# deploy/home-lab/.gitignore (complete)
.env
.rendered/
backups/
```

The script below has seven numbered operations matching design 07 exactly. It never rewrites a
checked-in template. Realm creation treats an existing realm as success; it does not replace live
realm settings on an upgrade.

```bash
#!/usr/bin/env bash
# deploy/home-lab/install.sh (complete)
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$script_dir"

# 1. Update a branch checkout when it has an upstream; detached release tags stay pinned.
if branch="$(git symbolic-ref --quiet --short HEAD 2>/dev/null)" \
  && git rev-parse --abbrev-ref "${branch}@{upstream}" >/dev/null 2>&1; then
  git pull --ff-only
fi

# 2. Create the ignored environment file once and generate its three application secrets.
if ! test -f .env; then
  command -v openssl >/dev/null
  cp .env.example .env
  chmod 0600 .env
  owner_password="$(openssl rand -hex 32)"
  app_password="$(openssl rand -hex 32)"
  signing_key="$(openssl rand -base64 48 | tr -d '\n')"
  sed -i \
    -e "s|replace-with-generated-owner-password|$owner_password|" \
    -e "s|replace-with-generated-app-password|$app_password|" \
    -e "s|replace-with-at-least-32-random-bytes|$signing_key|" .env
  unset owner_password app_password signing_key
  echo "Created .env with generated database and token secrets." >&2
  echo "Set hostname, Keycloak, Caddy and Mailpit-auth values, then run install.sh again." >&2
  exit 2
fi
set -a
# shellcheck disable=SC1091
source .env
set +a

required=(EVENTBOOKING_HOSTNAME EVENTBOOKING_IMAGE_TAG EVENTBOOKING_DB_PASSWORD
  EVENTBOOKING_DB_APP_PASSWORD EVENTBOOKING_TOKENS_SIGNING_KEY EVENTBOOKING_KEYCLOAK_URL
  EVENTBOOKING_KEYCLOAK_SEED_ADMIN_USERNAME EVENTBOOKING_KEYCLOAK_SEED_ADMIN_PASSWORD
  EVENTBOOKING_COORDINATOR_CONTACT EVENTBOOKING_EDGE_NETWORK EVENTBOOKING_MAILPIT_BASIC_AUTH
  EVENTBOOKING_CADDY_IMPORT_DIR EVENTBOOKING_SHARED_CADDY_CONTAINER)
for name in "${required[@]}"; do
  test -n "${!name:-}" || { echo "$name is required" >&2; exit 2; }
done
command -v docker >/dev/null
command -v curl >/dev/null
command -v envsubst >/dev/null
command -v jq >/dev/null
docker network inspect "$EVENTBOOKING_EDGE_NETWORK" >/dev/null
case "$EVENTBOOKING_DB_PASSWORD $EVENTBOOKING_DB_APP_PASSWORD $EVENTBOOKING_TOKENS_SIGNING_KEY" in
  *replace-with*) echo "Replace every generated-secret example value in .env" >&2; exit 2 ;;
esac
case "$EVENTBOOKING_MAILPIT_BASIC_AUTH" in
  *examplehash*) echo "Replace the Mailpit basic-auth example hash in .env" >&2; exit 2 ;;
esac

# 1. For an image deployment, pull the operator-pinned tag represented by this checkout.
docker compose --env-file .env pull

# 2. Complete the ignored runtime state; never print a secret.
install -d -m 0700 .rendered backups

# 3. Render the hostname, identity URL and Mailpit hash into ignored copies.
export EVENTBOOKING_HOSTNAME EVENTBOOKING_KEYCLOAK_URL EVENTBOOKING_MAILPIT_BASIC_AUTH
export EVENTBOOKING_COORDINATOR_CONTACT
# envsubst must receive literal variable names.
# shellcheck disable=SC2016
envsubst '$EVENTBOOKING_HOSTNAME' \
  < keycloak/eventbooking-realm.json > .rendered/eventbooking-realm.json
# envsubst must receive literal variable names.
# shellcheck disable=SC2016
envsubst '$EVENTBOOKING_HOSTNAME $EVENTBOOKING_MAILPIT_BASIC_AUTH' \
  < caddy/eventbooking.caddy > .rendered/eventbooking.caddy
sed -e "s|EVENTBOOKING_HOSTNAME|$EVENTBOOKING_HOSTNAME|g" \
    -e "s|EVENTBOOKING_KEYCLOAK_URL|$EVENTBOOKING_KEYCLOAK_URL|g" \
    -e "s|EVENTBOOKING_COORDINATOR_CONTACT|$EVENTBOOKING_COORDINATOR_CONTACT|g" \
  web/appsettings.json > .rendered/web-appsettings.json

# 4. Create the Keycloak realm once.
admin_token="$(curl --fail --silent --show-error \
  --data-urlencode "username=${EVENTBOOKING_KEYCLOAK_SEED_ADMIN_USERNAME}" \
  --data-urlencode "password=${EVENTBOOKING_KEYCLOAK_SEED_ADMIN_PASSWORD}" \
  --data-urlencode 'grant_type=password' --data-urlencode 'client_id=admin-cli' \
  "${EVENTBOOKING_KEYCLOAK_URL%/}/realms/master/protocol/openid-connect/token" | jq -r .access_token)"
realm_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  --header "Authorization: Bearer $admin_token" \
  "${EVENTBOOKING_KEYCLOAK_URL%/}/admin/realms/eventbooking")"
if test "$realm_status" = 404; then
  curl --fail --silent --show-error --request POST \
    --header "Authorization: Bearer $admin_token" --header 'Content-Type: application/json' \
    --data-binary @.rendered/eventbooking-realm.json \
    "${EVENTBOOKING_KEYCLOAK_URL%/}/admin/realms"
elif test "$realm_status" != 200; then
  echo "Unexpected Keycloak realm status: $realm_status" >&2
  exit 1
fi
unset admin_token

# 5. Install the shared-Caddy fragment and reload a validated configuration.
install -m 0644 .rendered/eventbooking.caddy \
  "$EVENTBOOKING_CADDY_IMPORT_DIR/eventbooking.caddy"
docker exec "$EVENTBOOKING_SHARED_CADDY_CONTAINER" caddy validate --config /etc/caddy/Caddyfile
docker exec "$EVENTBOOKING_SHARED_CADDY_CONTAINER" caddy reload --config /etc/caddy/Caddyfile

# 6. Always migrate; add demo state only after an explicit environment choice.
seed_args=()
if test "${EVENTBOOKING_SEED_DEMO:-false}" = true; then
  test -n "${EVENTBOOKING_KEYCLOAK_DEMO_PASSWORD:-}" \
    || { echo "EVENTBOOKING_KEYCLOAK_DEMO_PASSWORD is required for demo seeding" >&2; exit 2; }
  seed_args=(--demo --reanchor)
fi
docker compose --env-file .env --profile seed run --rm eventbooking-seed "${seed_args[@]}"

# 7. Reconcile long-running services to the selected image tag.
docker compose --env-file .env up --detach --remove-orphans
```

- [ ] **Step 4: Write the complete operator runbook**

Replace README with the following. During Step 5 append a dated `Rehearsal evidence` subsection
containing the actual three durations and image tags; never invent numbers in the plan.

```markdown
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

Demo state is opt-in. Set `EVENTBOOKING_SEED_DEMO=true` only on a demonstration installation.
Destructive reseeding additionally requires `EVENTBOOKING_ALLOW_RESEED=true` and an explicit
manual `docker compose --profile seed run --rm eventbooking-seed --demo --reseed` command.

## Upgrade and rollback

For an upgrade, change only `EVENTBOOKING_IMAGE_TAG`, run `docker compose pull`, run the seed job
without `--demo` to apply forward migrations, then run `docker compose up -d`. Verify readiness and
the principal user journeys before removing old images.

For rollback, restore the previous image tag and run `docker compose up -d`. Do not run a down
migration. Every schema change must remain compatible with the previous image for one release.

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
```

- [ ] **Step 5: Rehearse install, upgrade and fresh-volume restore**

Use a disposable Linux host or VM joined to scratch Caddy, Keycloak and edge network. Time the
three operations with UTC timestamps. For restore, create and initialise a second PostgreSQL
container on `eventbooking-restored-db`, restore the chosen dump there, and point a second Compose
project at it; the illustrative command in the README is not permission to restore into the live
database service. Confirm the old volume still exists and has not changed.

Append the observed record to README before committing. The restore duration must be below two
hours. If any operation fails, keep the logs and use superpowers:systematic-debugging; do not write
a successful-looking record.

- [ ] **Step 6: Run deployment and solution verification**

```bash
cd deploy/home-lab
shellcheck install.sh ../postgres/init-roles.sh
jq --exit-status . keycloak/eventbooking-realm.json >/dev/null
docker compose --env-file .env.example config --quiet
docker run --rm -v "$PWD/web/Caddyfile:/etc/caddy/Caddyfile:ro" \
  caddy:2.10-alpine caddy validate --config /etc/caddy/Caddyfile
cd ../..
actionlint .github/workflows/compose-smoke.yml
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
```

Expected: PASS with zero skipped tests. No Phase 6 count is known in advance; record only the
executor's observed result.

- [ ] **Step 7: Commit and push**

```bash
git add -A
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
git commit -m "feat(deploy): home-lab deployment with reference install script"
git push
```
