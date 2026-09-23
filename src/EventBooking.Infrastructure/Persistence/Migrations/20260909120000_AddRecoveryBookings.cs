using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecoveryBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_booking_active_attendee",
                table: "booking");

            migrationBuilder.AddColumn<Guid>(
                name: "recovery_of_booking_id",
                table: "booking",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_booking_active_original_attendee",
                table: "booking",
                column: "attendee_id",
                unique: true,
                filter: "status = 1 AND recovery_of_booking_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_booking_active_recovery",
                table: "booking",
                column: "recovery_of_booking_id",
                unique: true,
                filter: "status = 1 AND recovery_of_booking_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_booking_no_self_recovery",
                table: "booking",
                sql: "recovery_of_booking_id IS NULL OR recovery_of_booking_id <> id");

            migrationBuilder.AddForeignKey(
                name: "FK_booking_booking_recovery_of_booking_id",
                table: "booking",
                column: "recovery_of_booking_id",
                principalTable: "booking",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_booking_booking_recovery_of_booking_id",
                table: "booking");

            migrationBuilder.DropIndex(
                name: "ux_booking_active_original_attendee",
                table: "booking");

            migrationBuilder.DropIndex(
                name: "ux_booking_active_recovery",
                table: "booking");

            migrationBuilder.DropCheckConstraint(
                name: "ck_booking_no_self_recovery",
                table: "booking");

            migrationBuilder.DropColumn(
                name: "recovery_of_booking_id",
                table: "booking");

            migrationBuilder.CreateIndex(
                name: "ux_booking_active_attendee",
                table: "booking",
                column: "attendee_id",
                unique: true,
                filter: "status = 1");
        }
    }
}
