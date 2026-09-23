using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RepairCConcurrencyBackstops : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invite_attendee_id",
                table: "invite");

            migrationBuilder.CreateIndex(
                name: "ux_event_proposal_open_window",
                table: "event_proposal",
                columns: new[] { "date", "start_time" },
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ux_invite_pending_attendee",
                table: "invite",
                column: "attendee_id",
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ux_booking_active_attendee",
                table: "booking",
                column: "attendee_id",
                unique: true,
                filter: "status = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_event_proposal_open_window",
                table: "event_proposal");

            migrationBuilder.DropIndex(
                name: "ux_invite_pending_attendee",
                table: "invite");

            migrationBuilder.DropIndex(
                name: "ux_booking_active_attendee",
                table: "booking");

            migrationBuilder.CreateIndex(
                name: "IX_invite_attendee_id",
                table: "invite",
                column: "attendee_id");
        }
    }
}
