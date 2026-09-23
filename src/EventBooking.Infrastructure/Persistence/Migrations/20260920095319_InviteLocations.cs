using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InviteLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "IX_invite_location_location_id",
                table: "invite_location",
                column: "location_id");

            // Every inherited invite was issued at the single predecessor site, so the true
            // historical value exists here and the row is written rather than defaulted. An
            // invite with no location would break the one-or-more rule the ontology states.
            migrationBuilder.Sql(
                "INSERT INTO invite_location (invite_id, location_id) " +
                "SELECT id, '10000000-0000-0000-0000-000000000001'::uuid FROM invite;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invite_location");
        }
    }
}
