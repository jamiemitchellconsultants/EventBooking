using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeGroupsAndCandidateAssociation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "employee_group_id",
                table: "candidate",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "employee_group",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_group", x => x.id);
                    table.CheckConstraint("ck_employee_group_code_nonblank", "code <> ''");
                    table.CheckConstraint("ck_employee_group_name_nonblank", "name <> ''");
                });

            migrationBuilder.CreateTable(
                name: "employee_group_requirement",
                columns: table => new
                {
                    employee_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_type_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_group_requirement", x => new { x.employee_group_id, x.appointment_type_id });
                    table.ForeignKey(
                        name: "FK_employee_group_requirement_appointment_type_appointment_typ~",
                        column: x => x.appointment_type_id,
                        principalTable: "appointment_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_group_requirement_employee_group_employee_group_id",
                        column: x => x.employee_group_id,
                        principalTable: "employee_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_employee_group_id",
                table: "candidate",
                column: "employee_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_group_code",
                table: "employee_group",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employee_group_requirement_appointment_type_id",
                table: "employee_group_requirement",
                column: "appointment_type_id");

            migrationBuilder.AddForeignKey(
                name: "FK_candidate_employee_group_employee_group_id",
                table: "candidate",
                column: "employee_group_id",
                principalTable: "employee_group",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Nullable reconciliation release: the Candidate association above stays nullable so
            // legacy rows keep their existing requirements until explicit reconciliation.
            migrationBuilder.Sql("COMMENT ON TABLE employee_group IS 'Change-controlled reference data (nullable reconciliation release).'");
            migrationBuilder.Sql("COMMENT ON TABLE employee_group_requirement IS 'Fixed appointment-type mappings (nullable reconciliation release).'");

            migrationBuilder.InsertData(
                table: "employee_group",
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
                table: "employee_group_requirement",
                columns: new[] { "employee_group_id", "appointment_type_id" },
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
                name: "FK_candidate_employee_group_employee_group_id",
                table: "candidate");

            migrationBuilder.DropTable(
                name: "employee_group_requirement");

            migrationBuilder.DropTable(
                name: "employee_group");

            migrationBuilder.DropIndex(
                name: "IX_candidate_employee_group_id",
                table: "candidate");

            migrationBuilder.DropColumn(
                name: "employee_group_id",
                table: "candidate");
        }
    }
}
