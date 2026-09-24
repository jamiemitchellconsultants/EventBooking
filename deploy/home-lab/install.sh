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
if grep -q 'EVENTBOOKING_' .rendered/eventbooking-realm.json .rendered/eventbooking.caddy; then
  echo "A runtime placeholder remained after rendering." >&2
  exit 1
fi
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
