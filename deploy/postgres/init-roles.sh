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
