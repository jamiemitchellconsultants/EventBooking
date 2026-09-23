using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ManagedReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing reference data is live: every inherited appointment type is active, every
            // concurrency token starts at 1, and the option count takes the design's default of 3.
            // EF's zero-and-false defaults would misdescribe rows that already exist.
            migrationBuilder.AddColumn<int>(
                name: "invite_option_count",
                table: "system_settings",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<long>(
                name: "version",
                table: "system_settings",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "version",
                table: "attendee_group",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "appointment_type",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<long>(
                name: "version",
                table: "appointment_type",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.UpdateData(
                table: "appointment_type",
                keyColumn: "id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000001"),
                columns: new[] { "is_active", "version" },
                values: new object[] { true, 1L });

            migrationBuilder.UpdateData(
                table: "appointment_type",
                keyColumn: "id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000002"),
                columns: new[] { "is_active", "version" },
                values: new object[] { true, 1L });

            migrationBuilder.UpdateData(
                table: "appointment_type",
                keyColumn: "id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000003"),
                columns: new[] { "is_active", "version" },
                values: new object[] { true, 1L });

            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "invite_expiry_days", "invite_option_count", "version" },
                values: new object[] { 7, 3, 1L });
            migrationBuilder.Sql("ALTER TABLE system_settings ALTER COLUMN invite_option_count DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE system_settings ALTER COLUMN version DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE attendee_group ALTER COLUMN version DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE appointment_type ALTER COLUMN is_active DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE appointment_type ALTER COLUMN version DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "invite_option_count",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "version",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "version",
                table: "attendee_group");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "appointment_type");

            migrationBuilder.DropColumn(
                name: "version",
                table: "appointment_type");

            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: 1,
                column: "invite_expiry_days",
                value: 4);
        }
    }
}
