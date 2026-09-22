# 06b — Local Compose stack (Task 29)

[← Phase overview](phase-6-seed-and-deployment.md) · [Previous task](phase-6a-seed-cli.md) · [Ontology](../ontology.md)

This task is the local infrastructure layer of Phase 6. It packages the four executable projects,
adds a seed job, and proves the whole developer topology through one reproducible smoke workflow.

> Use superpowers:executing-plans. Apply this document after Task 28 on the Phase 6 branch.

**Goal:** Make `docker compose up --build` produce a usable local system with PostgreSQL,
Keycloak, Mailpit, API, Web and MCP, while keeping demo data opt-in through the `seed` profile.

**Architecture:** Keycloak deliberately advertises `http://keycloak.localhost:8081`: browsers
resolve the `.localhost` suffix to loopback, while its Compose network alias gives API and MCP the
same issuer inside the network. PostgreSQL and Mailpit gate dependants with health checks. The seed
container is a one-shot profile and uses Task 28's CLI instead of a second migration mechanism.

**Tech Stack:** Docker Compose, PostgreSQL 16, Keycloak 26, Mailpit, .NET 10, nginx-unprivileged,
GitHub Actions and Bash.

**Spec:** [Master Task 29](../superpowers/plans/2026-09-19-eventbooking-implementation.md),
[local deployment](../design/07-deployment.md#local-docker-composeyml-at-the-repository-root), and
[Keycloak](../design/06-security-and-authentication.md#staff-authentication-keycloak-by-default).

### Task 29: Local Compose stack and smoke workflow

**Files:**

- Modify: .dockerignore
- Modify: src/EventBooking.Api/Dockerfile
- Modify: src/EventBooking.Mcp/Dockerfile
- Modify: src/EventBooking.Web/Dockerfile
- Create: src/EventBooking.Web/Dockerfile.caddy
- Create: src/EventBooking.SeedData/Dockerfile
- Modify: deploy/keycloak/realm-export.json
- Create: deploy/postgres/init-roles.sh
- Create: deploy/web/Caddyfile
- Create: deploy/web/appsettings.json
- Create: docker-compose.yml
- Create: .github/workflows/compose-smoke.yml

**Interfaces:**

```csharp
namespace EventBooking.Deployment;

public static class LocalDeploymentContract
{
    // Browser and container issuer are intentionally identical.
    public const string Authority = "http://keycloak.localhost:8081/realms/eventbooking";
    public const string Audience = "eventbooking-web";

    // Host ports: PostgreSQL, Keycloak, SMTP, Mailpit UI, API, Web and MCP.
    public static readonly int[] PublishedPorts = [5432, 8081, 1025, 8025, 5001, 5002, 5003];

    // Seed is never started by ordinary `docker compose up`.
    public const string SeedProfile = "seed";
}
```

- [ ] **Step 1: Write the failing smoke workflow**

Create the workflow before the deployment files. It validates the Compose model, waits on the real
readiness endpoint, checks discovery, and proves that Task 28 sent at least one invitation into
Mailpit. All third-party actions are pinned to full commit SHAs.

```yaml
# .github/workflows/compose-smoke.yml (complete)
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

- [ ] **Step 2: Prove the workflow is red before implementation**

```bash
docker compose config --quiet
docker compose up --detach --build --wait postgres keycloak mailpit
docker compose --profile seed run --rm seed --demo --reanchor
```

Expected: FAIL because the root Compose model, seed image and supporting configuration do not
exist. Stop any partially created resources with `docker compose down --volumes --remove-orphans`.

- [ ] **Step 3: Implement images, realm and Compose topology**

Replace `.dockerignore` with the complete repository-context filter:

```dockerignore
# .dockerignore (complete)
**/bin
**/obj
**/TestResults
**/node_modules
.git
.github
.idea
.vscode
.codex
**/.env
**/.env.*
!**/.env.example
*.user
*.suo
```

Use these complete Dockerfiles. Do not add package managers or shells to the runtime images.

```dockerfile
# src/EventBooking.Api/Dockerfile (complete)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/EventBooking.Api/EventBooking.Api.csproj -c Release -o /out --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "EventBooking.Api.dll"]
```

```dockerfile
# src/EventBooking.Mcp/Dockerfile (complete)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/EventBooking.Mcp/EventBooking.Mcp.csproj -c Release -o /out --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "EventBooking.Mcp.dll"]
```

```dockerfile
# src/EventBooking.Web/Dockerfile (complete)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/EventBooking.Web/EventBooking.Web.csproj -c Release -o /out

FROM nginxinc/nginx-unprivileged:alpine AS runtime
COPY --from=build /out/wwwroot /usr/share/nginx/html
COPY src/EventBooking.Web/nginx.conf /etc/nginx/conf.d/default.conf
USER 101
EXPOSE 8080
```

```dockerfile
# src/EventBooking.Web/Dockerfile.caddy (complete; publication input, not used by local Compose)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/EventBooking.Web/EventBooking.Web.csproj -c Release -o /out

FROM caddy:2.10-alpine AS runtime
COPY --from=build /out/wwwroot /srv
COPY deploy/web/Caddyfile /etc/caddy/Caddyfile
USER 1000:1000
EXPOSE 8080
```

```caddyfile
# deploy/web/Caddyfile (complete publication-image default)
:8080 {
    root * /srv
    encode zstd gzip
    try_files {path} /index.html
    file_server
}
```

```dockerfile
# src/EventBooking.SeedData/Dockerfile (complete)
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

The local realm contains no demo users; Task 28 converges those. Replace the ported realm with this
complete import. `staffId` remains admin-editable and every application claim is in the access
token.

```json
{
  "realm": "eventbooking",
  "enabled": true,
  "sslRequired": "none",
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
      "directAccessGrantsEnabled": true,
      "redirectUris": [
        "http://localhost:5002/authentication/login-callback",
        "http://localhost:5002/authentication/logout-callback"
      ],
      "webOrigins": ["http://localhost:5002"],
      "attributes": {
        "pkce.code.challenge.method": "S256",
        "post.logout.redirect.uris": "http://localhost:5002/authentication/logout-callback"
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

Create the database initializer as a mounted, read-only startup script. The API and MCP credentials
are separate login roles even though both initially receive the same least-privilege grants.

```bash
#!/usr/bin/env bash
# deploy/postgres/init-roles.sh (complete)
set -euo pipefail

psql --set ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  --set api_password="$EVENTBOOKING_API_DB_PASSWORD" \
  --set mcp_password="$EVENTBOOKING_MCP_DB_PASSWORD" <<'SQL'
DO $$ BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'eventbooking_migrator') THEN
    CREATE ROLE eventbooking_migrator NOLOGIN;
  END IF;
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'eventbooking_app') THEN
    CREATE ROLE eventbooking_app NOLOGIN;
  END IF;
END $$;
SELECT format('CREATE ROLE eventbooking_api LOGIN PASSWORD %L', :'api_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'eventbooking_api') \gexec
SELECT format('CREATE ROLE eventbooking_mcp LOGIN PASSWORD %L', :'mcp_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'eventbooking_mcp') \gexec
GRANT CONNECT ON DATABASE eventbooking TO eventbooking_api, eventbooking_mcp;
GRANT eventbooking_app TO eventbooking_api, eventbooking_mcp;
SQL
```

Create the runtime Web configuration:

```json
{
  "ApiBaseUrl": "http://localhost:5001",
  "ProductName": "EventBooking",
  "LogoPath": null,
  "CoordinatorContact": "coordinator@example.test",
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://keycloak.localhost:8081/realms/eventbooking",
      "ClientId": "eventbooking-web"
    }
  }
}
```

Create the complete root model. Do not add MinIO. The named alias is part of the authentication
contract; retain it if a service is renamed.

```yaml
# docker-compose.yml (complete)
name: eventbooking

services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: eventbooking
      POSTGRES_USER: eventbooking_owner
      POSTGRES_PASSWORD: local-owner-password
      EVENTBOOKING_API_DB_PASSWORD: local-api-password
      EVENTBOOKING_MCP_DB_PASSWORD: local-mcp-password
    ports: ["5432:5432"]
    volumes:
      - eventbooking-postgres:/var/lib/postgresql/data
      - ./deploy/postgres/init-roles.sh:/docker-entrypoint-initdb.d/10-roles.sh:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U eventbooking_owner -d eventbooking"]
      interval: 5s
      timeout: 5s
      retries: 20

  keycloak:
    image: quay.io/keycloak/keycloak:26.3.3
    command: ["start-dev", "--http-port=8081", "--import-realm"]
    environment:
      KC_BOOTSTRAP_ADMIN_USERNAME: admin
      KC_BOOTSTRAP_ADMIN_PASSWORD: local-keycloak-password
      KC_HOSTNAME: http://keycloak.localhost:8081
      KC_HTTP_ENABLED: "true"
      KC_HEALTH_ENABLED: "true"
    ports: ["8081:8081"]
    volumes:
      - ./deploy/keycloak/realm-export.json:/opt/keycloak/data/import/eventbooking-realm.json:ro
    networks:
      default:
        aliases: [keycloak.localhost]
    healthcheck:
      test: ["CMD-SHELL", "exec 3<>/dev/tcp/127.0.0.1/9000 && printf 'GET /health/ready HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n' >&3 && grep -q '200 OK' <&3"]
      interval: 10s
      timeout: 5s
      retries: 30
      start_period: 30s

  mailpit:
    image: axllent/mailpit:v1.27.8
    ports: ["1025:1025", "8025:8025"]
    healthcheck:
      test: ["CMD", "/mailpit", "readyz"]
      interval: 5s
      timeout: 5s
      retries: 20

  api:
    build:
      context: .
      dockerfile: src/EventBooking.Api/Dockerfile
    environment: &application-environment
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__EventBooking: Host=postgres;Database=eventbooking;Username=eventbooking_api;Password=local-api-password
      Auth__Authority: http://keycloak.localhost:8081/realms/eventbooking
      Auth__Audience: eventbooking-web
      Tokens__SigningKey: local-only-token-signing-key-at-least-32-bytes
      Email__Smtp__Host: mailpit
      Email__Smtp__Port: "1025"
      Email__FromAddress: eventbooking@example.test
      Email__FromName: EventBooking
      Portal__BaseUrl: http://localhost:5002
      Portal__CoordinatorContact: coordinator@example.test
      Cors__AllowedOrigins__0: http://localhost:5002
      Identity__StaffIdPattern: ^DEMO[0-9]{3}$
    ports: ["5001:8080"]
    depends_on:
      postgres: { condition: service_healthy }
      keycloak: { condition: service_healthy }
      mailpit: { condition: service_healthy }
    healthcheck:
      test: ["CMD", "bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080; printf 'GET /health/ready HTTP/1.1\\r\\nHost: localhost\\r\\nConnection: close\\r\\n\\r\\n' >&3; grep -q '200 OK' <&3"]
      interval: 10s
      timeout: 5s
      retries: 20
      start_period: 20s

  mcp:
    build:
      context: .
      dockerfile: src/EventBooking.Mcp/Dockerfile
    environment:
      <<: *application-environment
      ConnectionStrings__EventBooking: Host=postgres;Database=eventbooking;Username=eventbooking_mcp;Password=local-mcp-password
    ports: ["5003:8080"]
    depends_on:
      postgres: { condition: service_healthy }
      keycloak: { condition: service_healthy }
      mailpit: { condition: service_healthy }

  web:
    build:
      context: .
      dockerfile: src/EventBooking.Web/Dockerfile
    ports: ["5002:8080"]
    volumes:
      - ./deploy/web/appsettings.json:/usr/share/nginx/html/appsettings.json:ro
    depends_on:
      api: { condition: service_healthy }

  seed:
    profiles: [seed]
    build:
      context: .
      dockerfile: src/EventBooking.SeedData/Dockerfile
    environment:
      ConnectionStrings__EventBooking: Host=postgres;Database=eventbooking;Username=eventbooking_owner;Password=local-owner-password
      Keycloak__BaseUrl: http://keycloak.localhost:8081
      Keycloak__Realm: eventbooking
      Keycloak__AdminRealm: master
      Keycloak__AdminUsername: admin
      Keycloak__AdminPassword: local-keycloak-password
      Keycloak__DemoPassword: EventBooking1!
      Keycloak__RealmExportPath: /config/eventbooking-realm.json
      Tokens__SigningKey: local-only-token-signing-key-at-least-32-bytes
      Email__Smtp__Host: mailpit
      Email__Smtp__Port: "1025"
      Email__FromAddress: eventbooking@example.test
      Email__FromName: EventBooking
      Portal__BaseUrl: http://localhost:5002
      Portal__CoordinatorContact: coordinator@example.test
      Identity__StaffIdPattern: ^DEMO[0-9]{3}$
    volumes:
      - ./deploy/keycloak/realm-export.json:/config/eventbooking-realm.json:ro
    depends_on:
      postgres: { condition: service_healthy }
      keycloak: { condition: service_healthy }
      mailpit: { condition: service_healthy }

volumes:
  eventbooking-postgres:
```

The API probe uses Bash already present in the Debian .NET runtime and opens no extra package or
privilege surface. Verify that assumption in the built final image; if Microsoft changes the base
image, replace the probe with a tiny repository-owned .NET health probe rather than installing curl.

- [ ] **Step 4: Exercise the full local journey**

```bash
docker compose config --quiet
docker compose up --detach --build --wait postgres keycloak mailpit
docker compose --profile seed run --rm seed --demo --reanchor
docker compose up --detach --build --wait api mcp web
curl --fail http://localhost:5001/health/ready
curl --fail http://localhost:5001/api
curl --fail http://localhost:5003/health/ready
curl --fail http://localhost:5002/
test "$(curl --fail --silent http://localhost:8025/api/v1/messages | jq '.messages | length')" -ge 1
```

Open Web and sign in as all eight Task 28 users using `EventBooking1!`. Exercise one proposal,
acceptance, invitation, booking and check-in. Confirm Mailpit shows the invitation and that its
options include location and IANA zone-derived local time. Record any failed persona or journey;
do not reduce the exercise list to make the task pass.

- [ ] **Step 5: Validate formats, image users and the workflow**

```bash
jq --exit-status . deploy/keycloak/realm-export.json >/dev/null
shellcheck deploy/postgres/init-roles.sh
docker compose config --quiet
for spec in \
  eventbooking-api:src/EventBooking.Api/Dockerfile \
  eventbooking-mcp:src/EventBooking.Mcp/Dockerfile \
  eventbooking-web:src/EventBooking.Web/Dockerfile \
  eventbooking-web-caddy:src/EventBooking.Web/Dockerfile.caddy \
  eventbooking-seed:src/EventBooking.SeedData/Dockerfile
do
  image="${spec%%:*}"; dockerfile="${spec#*:}"
  docker build --file "$dockerfile" --tag "$image:task29" .
  user="$(docker image inspect "$image:task29" --format '{{.Config.User}}')"
  test -n "$user" && test "$user" != 0 && test "$user" != root
done
actionlint .github/workflows/compose-smoke.yml
docker compose down --volumes --remove-orphans
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
```

Expected: PASS with zero skipped tests. No Phase 6 count is known in advance; record the executor's
actual counts in HANDOVER.md only after this run.

- [ ] **Step 6: Commit and push**

```bash
git add -A
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
git commit -m "feat(deploy): local Compose stack without MinIO"
git push
```
