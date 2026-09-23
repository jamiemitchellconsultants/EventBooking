using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingAppointments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "booking_appointment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    checked_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    outcome_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_changed_by_staff_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_appointment", x => x.id);
                    table.CheckConstraint("CK_booking_appointment_last_change_pair", "(last_changed_by_staff_user_id IS NULL) = (last_changed_at IS NULL)");
                    table.CheckConstraint("CK_booking_appointment_status_timestamps", "(status = 1 AND checked_in_at IS NULL AND outcome_at IS NULL)\nOR (status = 2 AND checked_in_at IS NOT NULL AND outcome_at IS NULL)\nOR (status = 3 AND checked_in_at IS NOT NULL AND outcome_at IS NOT NULL\n    AND outcome_at >= checked_in_at)\nOR (status = 4 AND checked_in_at IS NULL AND outcome_at IS NOT NULL)");
                    table.CheckConstraint("CK_booking_appointment_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_booking_appointment_appointment_type_appointment_type_id",
                        column: x => x.appointment_type_id,
                        principalTable: "appointment_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_booking_appointment_booking_booking_id",
                        column: x => x.booking_id,
                        principalTable: "booking",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_booking_appointment_appointment_type_id_status",
                table: "booking_appointment",
                columns: new[] { "appointment_type_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_appointment_booking_id_appointment_type_id",
                table: "booking_appointment",
                columns: new[] { "booking_id", "appointment_type_id" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO booking_appointment
                    (id, booking_id, appointment_type_id, status, checked_in_at, outcome_at,
                     last_changed_by_staff_user_id, last_changed_at, version)
                SELECT
                    gen_random_uuid(), b.id, requirement.appointment_type_id, 1,
                    NULL, NULL, NULL, NULL, 1
                FROM booking AS b
                INNER JOIN attendee_requirement AS requirement
                    ON requirement.attendee_id = b.attendee_id
                WHERE b.status = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_appointment");
        }
    }
}
