using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequireEventStartInstant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The generated backfill was 0001-01-01, which the eligibility query would read as an
            // event that started two thousand years ago. Every existing row has a window and a
            // location, and PostgreSQL resolves IANA rules itself, so the true historical instant
            // is computable rather than invented.
            migrationBuilder.Sql(
                """
                UPDATE event e
                   SET start_utc = (e.date + e.start_time) AT TIME ZONE l.time_zone_id
                  FROM location l
                 WHERE l.id = e.location_id
                   AND e.start_utc IS NULL;
                """);

            // An event whose location has no row cannot be given a true instant. Refuse, rather
            // than store a wrong one that hides the event instead of failing.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE unresolved bigint;
                BEGIN
                    SELECT COUNT(*) INTO unresolved FROM event WHERE start_utc IS NULL;
                    IF unresolved > 0 THEN
                        RAISE EXCEPTION
                            'Cannot require event.start_utc: % event rows have no location row to compute it from.',
                            unresolved;
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "start_utc",
                table: "event",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            // No column default: an insert that does not state the instant must fail rather than
            // take a value nothing computed.
            migrationBuilder.Sql("ALTER TABLE event ALTER COLUMN start_utc DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "start_utc",
                table: "event",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");
        }
    }
}
