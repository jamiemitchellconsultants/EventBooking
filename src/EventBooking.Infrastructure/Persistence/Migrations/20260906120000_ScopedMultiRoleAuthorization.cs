using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScopedMultiRoleAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_access_profile",
                columns: table => new
                {
                    staff_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_manager = table.Column<bool>(type: "boolean", nullable: false),
                    is_coordinator = table.Column<bool>(type: "boolean", nullable: false),
                    is_admin = table.Column<bool>(type: "boolean", nullable: false),
                    is_appointment_staff = table.Column<bool>(type: "boolean", nullable: false),
                    appointment_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_access_profile", x => x.staff_user_id);
                    table.CheckConstraint("ck_staff_access_profile_admin_exclusive", "NOT is_admin OR (NOT is_coordinator AND NOT is_manager AND NOT is_appointment_staff AND appointment_type_id IS NULL)");
                    table.CheckConstraint("ck_staff_access_profile_has_role", "is_admin OR is_coordinator OR is_manager OR is_appointment_staff");
                    table.CheckConstraint("ck_staff_access_profile_scope", "((is_manager OR is_appointment_staff) AND appointment_type_id IS NOT NULL) OR (NOT is_manager AND NOT is_appointment_staff AND appointment_type_id IS NULL)");
                    table.CheckConstraint("ck_staff_access_profile_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_staff_access_profile_appointment_type_appointment_type_id",
                        column: x => x.appointment_type_id,
                        principalTable: "appointment_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM appointment_type AS appointment
                        WHERE appointment.manager_user_id IS NOT NULL
                          AND NOT EXISTS (
                              SELECT 1
                              FROM user_role_assignment AS assignment
                              WHERE assignment.role = 1
                                AND assignment.appointment_type_id = appointment.id
                                AND assignment.entra_object_id = appointment.manager_user_id))
                    THEN
                        RAISE EXCEPTION 'legacy manager representations disagree';
                    END IF;

                    IF EXISTS (
                        SELECT appointment_type_id
                        FROM user_role_assignment
                        WHERE role = 1
                        GROUP BY appointment_type_id
                        HAVING COUNT(*) > 1)
                    THEN
                        RAISE EXCEPTION 'legacy manager assignments contain duplicate appointment types';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO staff_access_profile
                    (staff_user_id, is_admin, is_coordinator, is_manager,
                     is_appointment_staff, appointment_type_id, version)
                SELECT
                    entra_object_id,
                    role = 3,
                    role = 2,
                    role = 1,
                    FALSE,
                    appointment_type_id,
                    1
                FROM user_role_assignment;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_staff_access_profile_manager_appointment_type",
                table: "staff_access_profile",
                column: "appointment_type_id",
                unique: true,
                filter: "is_manager");

            migrationBuilder.DropTable(
                name: "user_role_assignment");

            migrationBuilder.DropColumn(
                name: "manager_user_id",
                table: "appointment_type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException(
                "Scoped multi-role profiles cannot be represented by the legacy single-role schema.");
    }
}
