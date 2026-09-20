# 02b — One fresh schema, and the roles that keep the audit trail append-only, edits 22 (Task 9b)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — src/EventBooking.Infrastructure/Persistence/Migrations/20260920120000_InitialSchema.cs — 1/1

<!-- retirement-file: {"id":56,"file":"src/EventBooking.Infrastructure/Persistence/Migrations/20260920120000_InitialSchema.cs","beforeSha":null,"afterSha":"22324e8e6dfbbb2d462c2ebca7ddca6a6074f09952a6bcbf32fced5298081eee","side":"after","part":1,"parts":1} -->

`````csharp
﻿using System;
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
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointment_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "attendee_group",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendee_group", x => x.id);
                    table.CheckConstraint("ck_attendee_group_code_nonblank", "code <> ''");
                    table.CheckConstraint("ck_attendee_group_name_nonblank", "name <> ''");
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
                    attendee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    recovery_of_booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    manage_token_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking", x => x.id);
                    table.CheckConstraint("ck_booking_no_self_recovery", "recovery_of_booking_id IS NULL OR recovery_of_booking_id <> id");
                    table.ForeignKey(
                        name: "FK_booking_booking_recovery_of_booking_id",
                        column: x => x.recovery_of_booking_id,
                        principalTable: "booking",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "email_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    attendee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_name = table.Column<int>(type: "integer", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    invite_id = table.Column<Guid>(type: "uuid", nullable: true),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    claim_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "event",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "event_proposal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_by_manager_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposer_appointment_type_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_proposal", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "location",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_location", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "staff_identity",
                columns: table => new
                {
                    staff_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_id = table.Column<string>(type: "character varying(32)", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_identity", x => x.staff_user_id);
                    table.CheckConstraint("ck_staff_identity_format", "char_length(staff_id) BETWEEN 1 AND 32 AND staff_id = btrim(staff_id)");
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    invite_expiry_days = table.Column<int>(type: "integer", nullable: false),
                    max_auto_retry_count = table.Column<int>(type: "integer", nullable: false),
                    invite_option_count = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.id);
                });

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
                    table.CheckConstraint("ck_staff_access_profile_scope", "appointment_type_id IS NULL OR (is_manager OR is_appointment_staff)");
                    table.CheckConstraint("ck_staff_access_profile_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_staff_access_profile_appointment_type_appointment_type_id",
                        column: x => x.appointment_type_id,
                        principalTable: "appointment_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendee",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    attendee_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendee", x => x.id);
                    table.ForeignKey(
                        name: "FK_attendee_attendee_group_attendee_group_id",
                        column: x => x.attendee_group_id,
                        principalTable: "attendee_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateTable(
                name: "invite",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    attendee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recovery_of_booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    token_version = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invite", x => x.id);
                    table.ForeignKey(
                        name: "FK_invite_booking_recovery_of_booking_id",
                        column: x => x.recovery_of_booking_id,
                        principalTable: "booking",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "event_capacity",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_headcount = table.Column<int>(type: "integer", nullable: false),
                    remaining_capacity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_capacity", x => new { x.event_id, x.appointment_type_id });
                    table.CheckConstraint("ck_event_capacity_bounds", "remaining_capacity >= 0 AND remaining_capacity <= total_headcount AND total_headcount > 0");
                    table.ForeignKey(
                        name: "FK_event_capacity_event_event_id",
                        column: x => x.event_id,
                        principalTable: "event",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_proposal_appointment_type",
                columns: table => new
                {
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_type_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_proposal_appointment_type", x => new { x.proposal_id, x.appointment_type_id });
                    table.ForeignKey(
                        name: "FK_event_proposal_appointment_type_event_proposal_proposal_id",
                        column: x => x.proposal_id,
                        principalTable: "event_proposal",
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
                        name: "FK_proposal_acceptance_event_proposal_proposal_id",
                        column: x => x.proposal_id,
                        principalTable: "event_proposal",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attendee_requirement",
                columns: table => new
                {
                    attendee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_type_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendee_requirement", x => new { x.attendee_id, x.appointment_type_id });
                    table.ForeignKey(
                        name: "FK_attendee_requirement_attendee_attendee_id",
                        column: x => x.attendee_id,
                        principalTable: "attendee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invite_location",
                columns: table => new
                {
                    invite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invite_location", x => new { x.invite_id, x.location_id });
                    table.ForeignKey(
                        name: "FK_invite_location_invite_invite_id",
                        column: x => x.invite_id,
                        principalTable: "invite",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invite_option",
                columns: table => new
                {
                    invite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invite_option", x => new { x.invite_id, x.event_id });
                    table.ForeignKey(
                        name: "FK_invite_option_invite_invite_id",
                        column: x => x.invite_id,
                        principalTable: "invite",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invite_requirement",
                columns: table => new
                {
                    invite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_type_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invite_requirement", x => new { x.invite_id, x.appointment_type_id });
                    table.ForeignKey(
                        name: "FK_invite_requirement_appointment_type_appointment_type_id",
                        column: x => x.appointment_type_id,
                        principalTable: "appointment_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invite_requirement_invite_invite_id",
                        column: x => x.invite_id,
                        principalTable: "invite",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "appointment_type",
                columns: new[] { "id", "code", "is_active", "name", "version" },
                values: new object[,]
                {
                    { new Guid("a0000001-0000-0000-0000-000000000001"), "DAT", true, "Drug & Alcohol Testing", 1L },
                    { new Guid("a0000002-0000-0000-0000-000000000002"), "MED", true, "Medical Check-up", 1L },
                    { new Guid("a0000003-0000-0000-0000-000000000003"), "UNI", true, "Uniform Fitting", 1L }
                });

            migrationBuilder.InsertData(
                table: "attendee_group",
                columns: new[] { "id", "code", "is_active", "name", "version" },
                values: new object[,]
                {
                    { new Guid("e0000001-0000-0000-0000-000000000001"), "CABIN_CREW", true, "Cabin Crew", 1L },
                    { new Guid("e0000002-0000-0000-0000-000000000002"), "PILOTS", true, "Pilots", 1L },
                    { new Guid("e0000003-0000-0000-0000-000000000003"), "GROUND_OPERATIONS_AGENT", true, "Ground Operations Agent", 1L },
                    { new Guid("e0000004-0000-0000-0000-000000000004"), "ENGINEERING", true, "Engineering", 1L },
                    { new Guid("e0000005-0000-0000-0000-000000000005"), "GROUND_TRANSPORT_SERVICES", true, "Ground Transport Services", 1L }
                });

            migrationBuilder.InsertData(
                table: "location",
                columns: new[] { "id", "address", "code", "is_active", "name", "time_zone_id", "version" },
                values: new object[] { new Guid("10000000-0000-0000-0000-000000000001"), "Recorded against the transitional site until Phase 3.", "TRANSITIONAL", true, "Transitional location", "Europe/London", 1L });

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "id", "invite_expiry_days", "invite_option_count", "max_auto_retry_count", "version" },
                values: new object[] { 1, 7, 3, 2, 1L });

            migrationBuilder.InsertData(
                table: "attendee_group_requirement",
                columns: new[] { "appointment_type_id", "attendee_group_id" },
                values: new object[,]
                {
                    { new Guid("a0000001-0000-0000-0000-000000000001"), new Guid("e0000001-0000-0000-0000-000000000001") },
                    { new Guid("a0000002-0000-0000-0000-000000000002"), new Guid("e0000001-0000-0000-0000-000000000001") },
                    { new Guid("a0000003-0000-0000-0000-000000000003"), new Guid("e0000001-0000-0000-0000-000000000001") },
                    { new Guid("a0000001-0000-0000-0000-000000000001"), new Guid("e0000002-0000-0000-0000-000000000002") },
                    { new Guid("a0000003-0000-0000-0000-000000000003"), new Guid("e0000002-0000-0000-0000-000000000002") },
                    { new Guid("a0000002-0000-0000-0000-000000000002"), new Guid("e0000003-0000-0000-0000-000000000003") },
                    { new Guid("a0000002-0000-0000-0000-000000000002"), new Guid("e0000004-0000-0000-0000-000000000004") },
                    { new Guid("a0000001-0000-0000-0000-000000000001"), new Guid("e0000005-0000-0000-0000-000000000005") },
                    { new Guid("a0000002-0000-0000-0000-000000000002"), new Guid("e0000005-0000-0000-0000-000000000005") },
                    { new Guid("a0000003-0000-0000-0000-000000000003"), new Guid("e0000005-0000-0000-0000-000000000005") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_appointment_type_code",
                table: "appointment_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attendee_attendee_group_id",
                table: "attendee",
                column: "attendee_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_attendee_status",
                table: "attendee",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_attendee_group_code",
                table: "attendee_group",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attendee_group_requirement_appointment_type_id",
                table: "attendee_group_requirement",
                column: "appointment_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_entity_type_entity_id",
                table: "audit_log",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_timestamp",
                table: "audit_log",
                columns: new[] { "timestamp", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_booking_attendee_id_status",
                table: "booking",
                columns: new[] { "attendee_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_event_id_status",
                table: "booking",
                columns: new[] { "event_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_booking_active_original_attendee",
                table: "booking",
                column: "attendee_id",
                unique: true,
                filter: "status = 1 AND recovery_of_booking_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_booking_active_recovery",
                table: "booking",
                column: "recovery_of_booking_id",
                unique: true,
                filter: "status = 1 AND recovery_of_booking_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_booking_appointment_appointment_type_id_status",
                table: "booking_appointment",
                columns: new[] { "appointment_type_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_appointment_booking_id_appointment_type_id",
                table: "booking_appointment",
                columns: new[] { "booking_id", "appointment_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_log_attendee_id",
                table: "email_log",
                column: "attendee_id");

            migrationBuilder.CreateIndex(
                name: "IX_email_log_attendee_id_sent_at",
                table: "email_log",
                columns: new[] { "attendee_id", "sent_at" });

            migrationBuilder.CreateIndex(
                name: "ix_event_eligibility",
                table: "event",
                columns: new[] { "status", "location_id", "start_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_event_proposal_id",
                table: "event",
                column: "proposal_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_status",
                table: "event",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_event_proposal_status",
                table: "event_proposal",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_event_proposal_open_window",
                table: "event_proposal",
                columns: new[] { "date", "start_time" },
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "IX_invite_recovery_of_booking_id",
                table: "invite",
                column: "recovery_of_booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_invite_status_expires_at",
                table: "invite",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ux_invite_pending_attendee",
                table: "invite",
                column: "attendee_id",
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "IX_invite_location_location_id",
                table: "invite_location",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "IX_invite_requirement_appointment_type_id",
                table: "invite_requirement",
                column: "appointment_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_location_code",
                table: "location",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_staff_access_profile_manager_appointment_type",
                table: "staff_access_profile",
                column: "appointment_type_id",
                unique: true,
                filter: "is_manager");

            migrationBuilder.CreateIndex(
                name: "ux_staff_identity_staff_id",
                table: "staff_identity",
                column: "staff_id",
                unique: true);

            // A functional index, which EF cannot express: two attendees must not differ only by
            // the case of their address, because that is one mailbox.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_attendee_email_lower ON attendee (lower(email));");

            // Persistence/Sql/roles.sql creates these roles and the SeedData CLI applies it before
            // migrating. A database migrated without it is still valid, so the grants are skipped
            // rather than fatal.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'eventbooking_app') THEN
                        GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public
                            TO eventbooking_app;
                        GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO eventbooking_app;

                        -- Append-only. An audit entry the application could edit or delete is not
                        -- an audit trail.
                        REVOKE UPDATE, DELETE, TRUNCATE ON audit_log FROM eventbooking_app;
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendee_group_requirement");

            migrationBuilder.DropTable(
                name: "attendee_requirement");

            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "booking_appointment");

            migrationBuilder.DropTable(
                name: "email_log");

            migrationBuilder.DropTable(
                name: "event_capacity");

            migrationBuilder.DropTable(
                name: "event_proposal_appointment_type");

            migrationBuilder.DropTable(
                name: "invite_location");

            migrationBuilder.DropTable(
                name: "invite_option");

            migrationBuilder.DropTable(
                name: "invite_requirement");

            migrationBuilder.DropTable(
                name: "location");

            migrationBuilder.DropTable(
                name: "proposal_acceptance");

            migrationBuilder.DropTable(
                name: "staff_access_profile");

            migrationBuilder.DropTable(
                name: "staff_identity");

            migrationBuilder.DropTable(
                name: "system_settings");

            migrationBuilder.DropTable(
                name: "attendee");

            migrationBuilder.DropTable(
                name: "event");

            migrationBuilder.DropTable(
                name: "invite");

            migrationBuilder.DropTable(
                name: "event_proposal");

            migrationBuilder.DropTable(
                name: "appointment_type");

            migrationBuilder.DropTable(
                name: "attendee_group");

            migrationBuilder.DropTable(
                name: "booking");
        }
    }
}
`````
