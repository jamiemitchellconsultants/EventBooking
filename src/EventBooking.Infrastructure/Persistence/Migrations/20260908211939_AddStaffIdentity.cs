using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_identity",
                columns: table => new
                {
                    staff_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_id = table.Column<string>(type: "character(7)", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_identity", x => x.staff_user_id);
                    table.CheckConstraint("ck_staff_identity_format", "staff_id ~* '^[UN][0-9]{6}$'");
                });

            migrationBuilder.CreateIndex(
                name: "ux_staff_identity_staff_id",
                table: "staff_identity",
                column: "staff_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_identity");
        }
    }
}
