using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrantApplicationRoleOnLaterTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only the initial migration granted, and the role script runs before migrating, so
            // a table a later migration creates in the same run (idempotency_record) reached the
            // application role with no privileges at all. This re-grants everything that exists
            // and makes every later table this migrator creates carry the grant from birth.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'eventbooking_app') THEN
                        GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public
                            TO eventbooking_app;
                        GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO eventbooking_app;

                        -- Still append-only. An audit entry the application could edit or delete
                        -- is not an audit trail.
                        REVOKE UPDATE, DELETE, TRUNCATE ON audit_log FROM eventbooking_app;

                        ALTER DEFAULT PRIVILEGES IN SCHEMA public
                            GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO eventbooking_app;
                        ALTER DEFAULT PRIVILEGES IN SCHEMA public
                            GRANT USAGE, SELECT ON SEQUENCES TO eventbooking_app;
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Existing table grants stay: the schema this rolls back to needs them too.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'eventbooking_app') THEN
                        ALTER DEFAULT PRIVILEGES IN SCHEMA public
                            REVOKE SELECT, INSERT, UPDATE, DELETE ON TABLES FROM eventbooking_app;
                        ALTER DEFAULT PRIVILEGES IN SCHEMA public
                            REVOKE USAGE, SELECT ON SEQUENCES FROM eventbooking_app;
                    END IF;
                END
                $$;
                """);
        }
    }
}
