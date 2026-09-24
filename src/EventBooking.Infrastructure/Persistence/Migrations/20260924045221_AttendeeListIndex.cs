using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttendeeListIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_attendee_status",
                table: "attendee");

            migrationBuilder.CreateIndex(
                name: "IX_attendee_status_name_id",
                table: "attendee",
                columns: new[] { "status", "name", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_attendee_status_name_id",
                table: "attendee");

            migrationBuilder.CreateIndex(
                name: "IX_attendee_status",
                table: "attendee",
                column: "status");
        }
    }
}
