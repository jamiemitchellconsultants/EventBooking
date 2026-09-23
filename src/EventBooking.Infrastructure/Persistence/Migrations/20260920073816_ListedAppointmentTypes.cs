using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ListedAppointmentTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nothing in the predecessor's schema identifies which appointment type proposed a
            // window, and inventing one would fabricate negotiation history. A fresh schema (Task 9)
            // is the supported path; an upgrade with existing proposals stops here for a decision.
            migrationBuilder.Sql(
                "DO $$ BEGIN IF EXISTS (SELECT 1 FROM event_proposal) THEN " +
                "RAISE EXCEPTION 'event_proposal rows exist: proposer_appointment_type_id cannot be " +
                "derived from predecessor data. See docs/detailed-implementations/phase-1c-negotiation.md.'; " +
                "END IF; END $$;");

            // Every inherited proposal and event was held at the single predecessor site.
            migrationBuilder.AddColumn<Guid>(
                name: "location_id",
                table: "event_proposal",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("10000000-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "proposer_appointment_type_id",
                table: "event_proposal",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("10000000-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "location_id",
                table: "event",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("10000000-0000-0000-0000-000000000001"));

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
            migrationBuilder.Sql("ALTER TABLE event_proposal ALTER COLUMN location_id DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE event_proposal ALTER COLUMN proposer_appointment_type_id DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE event ALTER COLUMN location_id DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_proposal_appointment_type");

            migrationBuilder.DropColumn(
                name: "location_id",
                table: "event_proposal");

            migrationBuilder.DropColumn(
                name: "proposer_appointment_type_id",
                table: "event_proposal");

            migrationBuilder.DropColumn(
                name: "location_id",
                table: "event");
        }
    }
}
