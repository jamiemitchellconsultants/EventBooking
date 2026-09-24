using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetireTransitionalSeedRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "attendee_group_requirement",
                keyColumns: new[] { "appointment_type_id", "attendee_group_id" },
                keyValues: new object[] { new Guid("a0000001-0000-0000-0000-000000000001"), new Guid("e0000001-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "attendee_group_requirement",
                keyColumns: new[] { "appointment_type_id", "attendee_group_id" },
                keyValues: new object[] { new Guid("a0000002-0000-0000-0000-000000000002"), new Guid("e0000001-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "attendee_group_requirement",
                keyColumns: new[] { "appointment_type_id", "attendee_group_id" },
                keyValues: new object[] { new Guid("a0000003-0000-0000-0000-000000000003"), new Guid("e0000001-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "attendee_group_requirement",
                keyColumns: new[] { "appointment_type_id", "attendee_group_id" },
                keyValues: new object[] { new Guid("a0000001-0000-0000-0000-000000000001"), new Guid("e0000002-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                table: "attendee_group_requirement",
                keyColumns: new[] { "appointment_type_id", "attendee_group_id" },
                keyValues: new object[] { new Guid("a0000003-0000-0000-0000-000000000003"), new Guid("e0000002-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                table: "attendee_group_requirement",
                keyColumns: new[] { "appointment_type_id", "attendee_group_id" },
                keyValues: new object[] { new Guid("a0000002-0000-0000-0000-000000000002"), new Guid("e0000003-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "attendee_group_requirement",
                keyColumns: new[] { "appointment_type_id", "attendee_group_id" },
                keyValues: new object[] { new Guid("a0000002-0000-0000-0000-000000000002"), new Guid("e0000004-0000-0000-0000-000000000004") });

            migrationBuilder.DeleteData(
                table: "attendee_group_requirement",
                keyColumns: new[] { "appointment_type_id", "attendee_group_id" },
                keyValues: new object[] { new Guid("a0000001-0000-0000-0000-000000000001"), new Guid("e0000005-0000-0000-0000-000000000005") });

            migrationBuilder.DeleteData(
                table: "attendee_group_requirement",
                keyColumns: new[] { "appointment_type_id", "attendee_group_id" },
                keyValues: new object[] { new Guid("a0000002-0000-0000-0000-000000000002"), new Guid("e0000005-0000-0000-0000-000000000005") });

            migrationBuilder.DeleteData(
                table: "attendee_group_requirement",
                keyColumns: new[] { "appointment_type_id", "attendee_group_id" },
                keyValues: new object[] { new Guid("a0000003-0000-0000-0000-000000000003"), new Guid("e0000005-0000-0000-0000-000000000005") });

            migrationBuilder.DeleteData(
                table: "location",
                keyColumn: "id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "appointment_type",
                keyColumn: "id",
                keyValue: new Guid("a0000001-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "appointment_type",
                keyColumn: "id",
                keyValue: new Guid("a0000002-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "appointment_type",
                keyColumn: "id",
                keyValue: new Guid("a0000003-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "attendee_group",
                keyColumn: "id",
                keyValue: new Guid("e0000001-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "attendee_group",
                keyColumn: "id",
                keyValue: new Guid("e0000002-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "attendee_group",
                keyColumn: "id",
                keyValue: new Guid("e0000003-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "attendee_group",
                keyColumn: "id",
                keyValue: new Guid("e0000004-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "attendee_group",
                keyColumn: "id",
                keyValue: new Guid("e0000005-0000-0000-0000-000000000005"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
