using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSelfRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "pending_registration_expiry_hours",
                table: "system_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "pending_registration",
                columns: table => new
                {
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attendee_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    token_version = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_registration", x => x.request_id);
                });

            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: 1,
                column: "pending_registration_expiry_hours",
                value: 48);

            migrationBuilder.CreateIndex(
                name: "IX_pending_registration_event_id_email",
                table: "pending_registration",
                columns: new[] { "event_id", "email" },
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "IX_pending_registration_status_expires_at",
                table: "pending_registration",
                columns: new[] { "status", "expires_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pending_registration");

            migrationBuilder.DropColumn(
                name: "pending_registration_expiry_hours",
                table: "system_settings");
        }
    }
}
