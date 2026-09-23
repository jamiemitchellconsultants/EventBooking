using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RelaxStaffAccessProfileScopeConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_staff_access_profile_scope",
                table: "staff_access_profile");

            migrationBuilder.AddCheckConstraint(
                name: "ck_staff_access_profile_scope",
                table: "staff_access_profile",
                sql: "appointment_type_id IS NULL OR (is_manager OR is_appointment_staff)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_staff_access_profile_scope",
                table: "staff_access_profile");

            migrationBuilder.AddCheckConstraint(
                name: "ck_staff_access_profile_scope",
                table: "staff_access_profile",
                sql: "((is_manager OR is_appointment_staff) AND appointment_type_id IS NOT NULL) OR (NOT is_manager AND NOT is_appointment_staff AND appointment_type_id IS NULL)");
        }
    }
}
