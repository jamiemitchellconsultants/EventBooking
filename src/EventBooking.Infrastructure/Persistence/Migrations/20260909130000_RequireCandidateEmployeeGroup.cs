using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequireCandidateEmployeeGroup : Migration
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
                    FROM candidate AS c
                    WHERE c.employee_group_id IS NULL
                       OR EXISTS (
                           SELECT appointment_type_id FROM candidate_requirement WHERE candidate_id = c.id
                           EXCEPT
                           SELECT appointment_type_id FROM employee_group_requirement
                           WHERE employee_group_id = c.employee_group_id)
                       OR EXISTS (
                           SELECT appointment_type_id FROM employee_group_requirement
                           WHERE employee_group_id = c.employee_group_id
                           EXCEPT
                           SELECT appointment_type_id FROM candidate_requirement WHERE candidate_id = c.id);
                    IF blocking > 0 THEN
                        RAISE EXCEPTION 'Employee Group reconciliation is incomplete: % candidate(s) block Release 2.', blocking;
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "employee_group_id",
                table: "candidate",
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
                name: "employee_group_id",
                table: "candidate",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }
    }
}
