#!/usr/bin/env bash
# tests/load/run.sh (complete)
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$root"
command -v docker >/dev/null
command -v k6 >/dev/null
if [[ -e tests/load/fixture.json ]]; then
  echo "Remove the previous disposable fixture after inspecting it; this run requires a fresh one." >&2
  exit 2
fi
compose=(docker compose -p eventbooking-load -f docker-compose.yml -f tests/load/compose.load.yml)
"${compose[@]}" up --detach --build --wait postgres keycloak mailpit
"${compose[@]}" --profile seed run --rm --user "$(id -u):$(id -g)" seed --demo --reanchor --load-fixture
test -s tests/load/fixture.json
"${compose[@]}" up --detach --build --wait api
curl --fail --silent http://localhost:5001/health/ready >/dev/null
started="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
set +e
k6 run tests/load/confirm-burst.js
load_status=$?
"${compose[@]}" logs --no-color --since "$started" postgres > tests/load/postgres.log
logs_status=$?
set -e
if [[ "$logs_status" -ne 0 ]]; then
  echo "Could not inspect PostgreSQL logs for deadlocks." >&2
  exit 1
fi
if grep -Eiq 'deadlock detected|SQLSTATE[[:space:]]*40P01' tests/load/postgres.log; then
  echo "PostgreSQL reported a deadlock during the burst." >&2
  exit 1
fi
exit "$load_status"
