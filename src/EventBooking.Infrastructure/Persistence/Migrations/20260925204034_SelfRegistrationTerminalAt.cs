using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SelfRegistrationTerminalAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "terminal_at",
                table: "pending_registration",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_pending_registration_status_terminal_at",
                table: "pending_registration",
                columns: new[] { "status", "terminal_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pending_registration_status_terminal_at",
                table: "pending_registration");

            migrationBuilder.DropColumn(
                name: "terminal_at",
                table: "pending_registration");
        }
    }
}
