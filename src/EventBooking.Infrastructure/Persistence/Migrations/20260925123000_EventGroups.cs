using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EventGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "event_group",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    is_open = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_group", x => x.id);
                    table.CheckConstraint("ck_event_group_title_nonblank", "title <> ''");
                });

            migrationBuilder.CreateTable(
                name: "event_group_attendee_group",
                columns: table => new
                {
                    event_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attendee_group_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_group_attendee_group", x => new { x.event_group_id, x.attendee_group_id });
                    table.ForeignKey(
                        name: "FK_event_group_attendee_group_attendee_group_attendee_group_id",
                        column: x => x.attendee_group_id,
                        principalTable: "attendee_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_event_group_attendee_group_event_group_event_group_id",
                        column: x => x.event_group_id,
                        principalTable: "event_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "event_group_event",
                columns: table => new
                {
                    event_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_open = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_group_event", x => new { x.event_group_id, x.event_id });
                    table.ForeignKey(
                        name: "FK_event_group_event_event_event_id",
                        column: x => x.event_id,
                        principalTable: "event",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_event_group_event_event_group_event_group_id",
                        column: x => x.event_group_id,
                        principalTable: "event_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_group_attendee_group_attendee_group_id",
                table: "event_group_attendee_group",
                column: "attendee_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_group_event_event_group_id_is_open",
                table: "event_group_event",
                columns: new[] { "event_group_id", "is_open" },
                filter: "is_open");

            migrationBuilder.CreateIndex(
                name: "IX_event_group_event_event_id",
                table: "event_group_event",
                column: "event_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_group_attendee_group");

            migrationBuilder.DropTable(
                name: "event_group_event");

            migrationBuilder.DropTable(
                name: "event_group");
        }
    }
}
