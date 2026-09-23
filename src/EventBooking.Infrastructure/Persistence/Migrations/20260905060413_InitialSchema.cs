using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appointment_type",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    manager_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointment_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<int>(type: "integer", nullable: false),
                    actor_type = table.Column<int>(type: "integer", nullable: false),
                    actor_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "booking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmed_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    manage_token_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "candidate",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "confirmed_slot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_confirmed_slot", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_name = table.Column<int>(type: "integer", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "invite",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invite", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "slot_proposal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_by_manager_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slot_proposal", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    invite_expiry_days = table.Column<int>(type: "integer", nullable: false),
                    max_auto_retry_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_role_assignment",
                columns: table => new
                {
                    entra_object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    appointment_type_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_role_assignment", x => x.entra_object_id);
                });

            migrationBuilder.CreateTable(
                name: "candidate_requirement",
                columns: table => new
                {
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_type_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_requirement", x => new { x.candidate_id, x.appointment_type_id });
                    table.ForeignKey(
                        name: "FK_candidate_requirement_candidate_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidate",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "slot_capacity",
                columns: table => new
                {
                    confirmed_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_headcount = table.Column<int>(type: "integer", nullable: false),
                    remaining_capacity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slot_capacity", x => new { x.confirmed_slot_id, x.appointment_type_id });
                    table.CheckConstraint("ck_slot_capacity_within_bounds", "remaining_capacity >= 0 AND remaining_capacity <= total_headcount");
                    table.ForeignKey(
                        name: "FK_slot_capacity_confirmed_slot_confirmed_slot_id",
                        column: x => x.confirmed_slot_id,
                        principalTable: "confirmed_slot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invite_option",
                columns: table => new
                {
                    invite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmed_slot_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invite_option", x => new { x.invite_id, x.confirmed_slot_id });
                    table.ForeignKey(
                        name: "FK_invite_option_invite_invite_id",
                        column: x => x.invite_id,
                        principalTable: "invite",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "proposal_acceptance",
                columns: table => new
                {
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manager_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    headcount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proposal_acceptance", x => new { x.proposal_id, x.appointment_type_id });
                    table.ForeignKey(
                        name: "FK_proposal_acceptance_slot_proposal_proposal_id",
                        column: x => x.proposal_id,
                        principalTable: "slot_proposal",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "appointment_type",
                columns: new[] { "id", "code", "manager_user_id", "name" },
                values: new object[,]
                {
                    { new Guid("a0000001-0000-0000-0000-000000000001"), "DAT", null, "Drug & Alcohol Testing" },
                    { new Guid("a0000002-0000-0000-0000-000000000002"), "MED", null, "Medical Check-up" },
                    { new Guid("a0000003-0000-0000-0000-000000000003"), "UNI", null, "Uniform Fitting" }
                });

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "id", "invite_expiry_days", "max_auto_retry_count" },
                values: new object[] { 1, 4, 2 });

            migrationBuilder.CreateIndex(
                name: "IX_appointment_type_code",
                table: "appointment_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_entity_type_entity_id",
                table: "audit_log",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_candidate_id_status",
                table: "booking",
                columns: new[] { "candidate_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_confirmed_slot_id_status",
                table: "booking",
                columns: new[] { "confirmed_slot_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_manage_token_hash",
                table: "booking",
                column: "manage_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_email",
                table: "candidate",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_status",
                table: "candidate",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_confirmed_slot_proposal_id",
                table: "confirmed_slot",
                column: "proposal_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_confirmed_slot_status",
                table: "confirmed_slot",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_email_log_candidate_id",
                table: "email_log",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "IX_invite_candidate_id",
                table: "invite",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "IX_invite_status_expires_at",
                table: "invite",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_invite_token_hash",
                table: "invite",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_slot_proposal_status",
                table: "slot_proposal",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointment_type");

            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "booking");

            migrationBuilder.DropTable(
                name: "candidate_requirement");

            migrationBuilder.DropTable(
                name: "email_log");

            migrationBuilder.DropTable(
                name: "invite_option");

            migrationBuilder.DropTable(
                name: "proposal_acceptance");

            migrationBuilder.DropTable(
                name: "slot_capacity");

            migrationBuilder.DropTable(
                name: "system_settings");

            migrationBuilder.DropTable(
                name: "user_role_assignment");

            migrationBuilder.DropTable(
                name: "candidate");

            migrationBuilder.DropTable(
                name: "invite");

            migrationBuilder.DropTable(
                name: "slot_proposal");

            migrationBuilder.DropTable(
                name: "confirmed_slot");
        }
    }
}
