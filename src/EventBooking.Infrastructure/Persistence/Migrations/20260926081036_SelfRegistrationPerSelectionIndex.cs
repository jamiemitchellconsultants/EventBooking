using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SelfRegistrationPerSelectionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pending_registration_event_id_email",
                table: "pending_registration");

            migrationBuilder.CreateIndex(
                name: "IX_pending_registration_event_id_email_event_group_id_attendee~",
                table: "pending_registration",
                columns: new[] { "event_id", "email", "event_group_id", "attendee_group_id" },
                unique: true,
                filter: "status = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE pending_registration p SET status = 3, terminal_at = now() " +
                "WHERE p.status = 1 AND EXISTS (SELECT 1 FROM pending_registration q " +
                "WHERE q.status = 1 AND q.event_id = p.event_id AND q.email = p.email " +
                "AND q.request_id < p.request_id);");

            migrationBuilder.DropIndex(
                name: "IX_pending_registration_event_id_email_event_group_id_attendee~",
                table: "pending_registration");

            migrationBuilder.CreateIndex(
                name: "IX_pending_registration_event_id_email",
                table: "pending_registration",
                columns: new[] { "event_id", "email" },
                unique: true,
                filter: "status = 1");
        }
    }
}
