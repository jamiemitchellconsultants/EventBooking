using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInviteRequirementSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "recovery_of_booking_id",
                table: "invite",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "invite_requirement",
                columns: table => new
                {
                    invite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_type_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invite_requirement", x => new { x.invite_id, x.appointment_type_id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_invite_recovery_of_booking_id",
                table: "invite",
                column: "recovery_of_booking_id");

            migrationBuilder.AddForeignKey(
                name: "FK_invite_booking_recovery_of_booking_id",
                table: "invite",
                column: "recovery_of_booking_id",
                principalTable: "booking",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Used invites copy the distinct Appointment Types of their Booking Appointments.
            migrationBuilder.Sql(
                """
                INSERT INTO invite_requirement (invite_id, appointment_type_id)
                SELECT DISTINCT booking.invite_id, booking_appointment.appointment_type_id
                FROM booking
                JOIN booking_appointment ON booking_appointment.booking_id = booking.id
                JOIN invite ON invite.id = booking.invite_id
                WHERE invite.status = 2
                ON CONFLICT DO NOTHING;
                """);

            // Pending invites copy the Candidate's current derived requirements.
            migrationBuilder.Sql(
                """
                INSERT INTO invite_requirement (invite_id, appointment_type_id)
                SELECT invite.id, candidate_requirement.appointment_type_id
                FROM invite
                JOIN candidate_requirement ON candidate_requirement.candidate_id = invite.candidate_id
                WHERE invite.status = 1
                ON CONFLICT DO NOTHING;
                """);

            // Terminal unbooked invites copy current Candidate requirements for schema shape only.
            migrationBuilder.Sql(
                """
                INSERT INTO invite_requirement (invite_id, appointment_type_id)
                SELECT invite.id, candidate_requirement.appointment_type_id
                FROM invite
                JOIN candidate_requirement ON candidate_requirement.candidate_id = invite.candidate_id
                WHERE invite.status IN (3, 4)
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM invite
                        WHERE NOT EXISTS (
                            SELECT 1 FROM invite_requirement
                            WHERE invite_requirement.invite_id = invite.id)) THEN
                        RAISE EXCEPTION 'Invite requirement backfill left invite(s) without snapshot rows.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_invite_requirement_appointment_type_appointment_type_id",
                table: "invite_requirement",
                column: "appointment_type_id",
                principalTable: "appointment_type",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invite_requirement_invite_invite_id",
                table: "invite_requirement",
                column: "invite_id",
                principalTable: "invite",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "IX_invite_requirement_appointment_type_id",
                table: "invite_requirement",
                column: "appointment_type_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_invite_booking_recovery_of_booking_id",
                table: "invite");

            migrationBuilder.DropTable(
                name: "invite_requirement");

            migrationBuilder.DropIndex(
                name: "IX_invite_recovery_of_booking_id",
                table: "invite");

            migrationBuilder.DropColumn(
                name: "recovery_of_booking_id",
                table: "invite");
        }
    }
}
