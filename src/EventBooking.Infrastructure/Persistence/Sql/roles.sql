-- Database roles for EventBooking.
--
-- Applied by the SeedData CLI before migrations, because the initial migration grants to the
-- application role and the role has to exist first. Both roles are NOLOGIN: a deployment attaches
-- its own login role to them, and a test reaches them with SET ROLE.
--
-- Re-running this script is safe.

DO $$
BEGIN
    -- Owns every object. Nothing the application connects as can drop a table.
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'eventbooking_migrator') THEN
        CREATE ROLE eventbooking_migrator NOLOGIN;
    END IF;

    -- Reads and writes rows, and nothing else. DDL is the migrator's alone.
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'eventbooking_app') THEN
        CREATE ROLE eventbooking_app NOLOGIN;
    END IF;
END
$$;

GRANT USAGE ON SCHEMA public TO eventbooking_app;

-- Tables that already exist, for a re-run against a migrated database. The initial migration
-- repeats these grants for the tables it creates.
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO eventbooking_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO eventbooking_app;

-- The audit trail is append-only for the application. Only the migrator may correct it, and that
-- correction is itself a deployment event rather than something a request can do.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'public' AND tablename = 'audit_log') THEN
        REVOKE UPDATE, DELETE, TRUNCATE ON audit_log FROM eventbooking_app;
    END IF;
END
$$;
