using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SelfRegistrationEmailLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "attendee_id",
                table: "email_log",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "self_registration_id",
                table: "email_log",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE system_settings SET pending_registration_expiry_hours = 24 WHERE id = 1 AND pending_registration_expiry_hours = 48;");

            migrationBuilder.CreateIndex(
                name: "IX_email_log_self_registration_id",
                table: "email_log",
                column: "self_registration_id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_email_log_one_recipient_context",
                table: "email_log",
                sql: "(attendee_id IS NULL) <> (self_registration_id IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_email_log_self_registration_id",
                table: "email_log");

            migrationBuilder.DropCheckConstraint(
                name: "CK_email_log_one_recipient_context",
                table: "email_log");

            migrationBuilder.DropColumn(
                name: "self_registration_id",
                table: "email_log");

            migrationBuilder.AlterColumn<Guid>(
                name: "attendee_id",
                table: "email_log",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.Sql(
                "UPDATE system_settings SET pending_registration_expiry_hours = 48 WHERE id = 1 AND pending_registration_expiry_hours = 24;");
        }
    }
}
