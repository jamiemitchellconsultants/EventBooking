using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GeneralizeStaffIdentifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_staff_identity_format",
                table: "staff_identity");

            migrationBuilder.AlterColumn<string>(
                name: "staff_id",
                table: "staff_identity",
                type: "character varying(32)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(7)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_staff_identity_format",
                table: "staff_identity",
                sql: "char_length(staff_id) BETWEEN 1 AND 32 AND staff_id = btrim(staff_id)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_staff_identity_format",
                table: "staff_identity");

            migrationBuilder.AlterColumn<string>(
                name: "staff_id",
                table: "staff_identity",
                type: "character(7)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_staff_identity_format",
                table: "staff_identity",
                sql: "staff_id ~* '^[UN][0-9]{6}$'");
        }
    }
}
