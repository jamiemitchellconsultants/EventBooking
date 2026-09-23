using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VariableEventWindowDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every inherited row is a fixed four-hour window, so 240 is the only correct backfill:
            // EF's generated 0 would violate the domain's 15-to-720-minute invariant on read.
            migrationBuilder.AddColumn<int>(
                name: "duration_minutes",
                table: "event_proposal",
                type: "integer",
                nullable: false,
                defaultValue: 240);

            migrationBuilder.AddColumn<int>(
                name: "duration_minutes",
                table: "event",
                type: "integer",
                nullable: false,
                defaultValue: 240);

            // The backfill is historical, not a policy: new rows must state their own duration.
            migrationBuilder.Sql("ALTER TABLE event_proposal ALTER COLUMN duration_minutes DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE event ALTER COLUMN duration_minutes DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "duration_minutes",
                table: "event_proposal");

            migrationBuilder.DropColumn(
                name: "duration_minutes",
                table: "event");
        }
    }
}
