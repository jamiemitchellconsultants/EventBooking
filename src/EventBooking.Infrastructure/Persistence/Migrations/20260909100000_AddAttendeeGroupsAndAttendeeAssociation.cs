using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendeeGroupsAndAttendeeAssociation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "attendee_group_id",
                table: "attendee",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "attendee_group",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendee_group", x => x.id);
                    table.CheckConstraint("ck_attendee_group_code_nonblank", "code <> ''");
                    table.CheckConstraint("ck_attendee_group_name_nonblank", "name <> ''");
                });

            migrationBuilder.CreateTable(
                name: "attendee_group_requirement",
                columns: table => new
                {
                    attendee_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_type_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendee_group_requirement", x => new { x.attendee_group_id, x.appointment_type_id });
                    table.ForeignKey(
                        name: "FK_attendee_group_requirement_appointment_type_appointment_typ~",
                        column: x => x.appointment_type_id,
                        principalTable: "appointment_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attendee_group_requirement_attendee_group_attendee_group_id",
                        column: x => x.attendee_group_id,
                        principalTable: "attendee_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attendee_attendee_group_id",
                table: "attendee",
                column: "attendee_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_attendee_group_code",
                table: "attendee_group",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attendee_group_requirement_appointment_type_id",
                table: "attendee_group_requirement",
                column: "appointment_type_id");

            migrationBuilder.AddForeignKey(
                name: "FK_attendee_attendee_group_attendee_group_id",
                table: "attendee",
                column: "attendee_group_id",
                principalTable: "attendee_group",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Nullable reconciliation release: the Attendee association above stays nullable so
            // legacy rows keep their existing requirements until explicit reconciliation.
            migrationBuilder.Sql("COMMENT ON TABLE attendee_group IS 'Change-controlled reference data (nullable reconciliation release).'");
            migrationBuilder.Sql("COMMENT ON TABLE attendee_group_requirement IS 'Fixed appointment-type mappings (nullable reconciliation release).'");

            migrationBuilder.InsertData(
                table: "attendee_group",
                columns: new[] { "id", "code", "name", "is_active" },
                values: new object[,]
                {
                    { new Guid("e0000001-0000-0000-0000-000000000001"), "CABIN_CREW", "Cabin Crew", true },
                    { new Guid("e0000002-0000-0000-0000-000000000002"), "PILOTS", "Pilots", true },
                    { new Guid("e0000003-0000-0000-0000-000000000003"), "GROUND_OPERATIONS_AGENT", "Ground Operations Agent", true },
                    { new Guid("e0000004-0000-0000-0000-000000000004"), "ENGINEERING", "Engineering", true },
                    { new Guid("e0000005-0000-0000-0000-000000000005"), "GROUND_TRANSPORT_SERVICES", "Ground Transport Services", true }
                });

            migrationBuilder.InsertData(
                table: "attendee_group_requirement",
                columns: new[] { "attendee_group_id", "appointment_type_id" },
                values: new object[,]
                {
                    { new Guid("e0000001-0000-0000-0000-000000000001"), new Guid("a0000001-0000-0000-0000-000000000001") },
                    { new Guid("e0000001-0000-0000-0000-000000000001"), new Guid("a0000002-0000-0000-0000-000000000002") },
                    { new Guid("e0000001-0000-0000-0000-000000000001"), new Guid("a0000003-0000-0000-0000-000000000003") },
                    { new Guid("e0000002-0000-0000-0000-000000000002"), new Guid("a0000001-0000-0000-0000-000000000001") },
                    { new Guid("e0000002-0000-0000-0000-000000000002"), new Guid("a0000003-0000-0000-0000-000000000003") },
                    { new Guid("e0000003-0000-0000-0000-000000000003"), new Guid("a0000002-0000-0000-0000-000000000002") },
                    { new Guid("e0000004-0000-0000-0000-000000000004"), new Guid("a0000002-0000-0000-0000-000000000002") },
                    { new Guid("e0000005-0000-0000-0000-000000000005"), new Guid("a0000001-0000-0000-0000-000000000001") },
                    { new Guid("e0000005-0000-0000-0000-000000000005"), new Guid("a0000002-0000-0000-0000-000000000002") },
                    { new Guid("e0000005-0000-0000-0000-000000000005"), new Guid("a0000003-0000-0000-0000-000000000003") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_attendee_attendee_group_attendee_group_id",
                table: "attendee");

            migrationBuilder.DropTable(
                name: "attendee_group_requirement");

            migrationBuilder.DropTable(
                name: "attendee_group");

            migrationBuilder.DropIndex(
                name: "IX_attendee_attendee_group_id",
                table: "attendee");

            migrationBuilder.DropColumn(
                name: "attendee_group_id",
                table: "attendee");
        }
    }
}
