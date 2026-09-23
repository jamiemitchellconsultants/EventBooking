using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScopeOpenProposalWindowToLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_event_proposal_open_window",
                table: "event_proposal");

            migrationBuilder.CreateIndex(
                name: "ux_event_proposal_open_window",
                table: "event_proposal",
                columns: new[] { "location_id", "date", "start_time" },
                unique: true,
                filter: "status = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_event_proposal_open_window",
                table: "event_proposal");

            migrationBuilder.CreateIndex(
                name: "ux_event_proposal_open_window",
                table: "event_proposal",
                columns: new[] { "date", "start_time" },
                unique: true,
                filter: "status = 1");
        }
    }
}
