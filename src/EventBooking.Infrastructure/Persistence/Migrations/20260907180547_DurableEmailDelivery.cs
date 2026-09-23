using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DurableEmailDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "booking_id",
                table: "email_log",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "claimed_at",
                table: "email_log",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "event_id",
                table: "email_log",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "invite_id",
                table: "email_log",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_log_attendee_id_sent_at",
                table: "email_log",
                columns: new[] { "attendee_id", "sent_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_email_log_attendee_id_sent_at",
                table: "email_log");

            migrationBuilder.DropColumn(
                name: "booking_id",
                table: "email_log");

            migrationBuilder.DropColumn(
                name: "claimed_at",
                table: "email_log");

            migrationBuilder.DropColumn(
                name: "event_id",
                table: "email_log");

            migrationBuilder.DropColumn(
                name: "invite_id",
                table: "email_log");
        }
    }
}
