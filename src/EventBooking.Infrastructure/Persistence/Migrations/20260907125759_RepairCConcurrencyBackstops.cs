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
                name: "IX_invite_candidate_id",
                table: "invite");

            migrationBuilder.CreateIndex(
                name: "ux_slot_proposal_open_window",
                table: "slot_proposal",
                columns: new[] { "date", "start_time" },
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ux_invite_pending_candidate",
                table: "invite",
                column: "candidate_id",
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ux_booking_active_candidate",
                table: "booking",
                column: "candidate_id",
                unique: true,
                filter: "status = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_slot_proposal_open_window",
                table: "slot_proposal");

            migrationBuilder.DropIndex(
                name: "ux_invite_pending_candidate",
                table: "invite");

            migrationBuilder.DropIndex(
                name: "ux_booking_active_candidate",
                table: "booking");

            migrationBuilder.CreateIndex(
                name: "IX_invite_candidate_id",
                table: "invite",
                column: "candidate_id");
        }
    }
}
