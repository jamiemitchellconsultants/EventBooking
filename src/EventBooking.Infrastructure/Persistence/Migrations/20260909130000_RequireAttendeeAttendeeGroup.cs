using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequireAttendeeAttendeeGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE blocking integer;
                BEGIN
                    SELECT COUNT(*) INTO blocking
                    FROM attendee AS c
                    WHERE c.attendee_group_id IS NULL
                       OR EXISTS (
                           SELECT appointment_type_id FROM attendee_requirement WHERE attendee_id = c.id
                           EXCEPT
                           SELECT appointment_type_id FROM attendee_group_requirement
                           WHERE attendee_group_id = c.attendee_group_id)
                       OR EXISTS (
                           SELECT appointment_type_id FROM attendee_group_requirement
                           WHERE attendee_group_id = c.attendee_group_id
                           EXCEPT
                           SELECT appointment_type_id FROM attendee_requirement WHERE attendee_id = c.id);
                    IF blocking > 0 THEN
                        RAISE EXCEPTION 'Attendee Group reconciliation is incomplete: % attendee(s) block Release 2.', blocking;
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "attendee_group_id",
                table: "attendee",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "attendee_group_id",
                table: "attendee",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }
    }
}
