using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeterministicAttendeeTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invite_token_hash",
                table: "invite");

            migrationBuilder.DropIndex(
                name: "IX_booking_manage_token_hash",
                table: "booking");

            migrationBuilder.DropColumn(
                name: "token_hash",
                table: "invite");

            migrationBuilder.DropColumn(
                name: "manage_token_hash",
                table: "booking");

            // Version 0 is not a legal token version. Every surviving row is on version 1 of the
            // new scheme: the stored hashes are gone and the token format has changed, so every
            // link issued before this migration is dead whatever number is written here.
            migrationBuilder.AddColumn<int>(
                name: "token_version",
                table: "invite",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "manage_token_version",
                table: "booking",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // The backfill is done; a new row must state its own version.
            migrationBuilder.Sql("ALTER TABLE invite ALTER COLUMN token_version DROP DEFAULT;");
            migrationBuilder.Sql(
                "ALTER TABLE booking ALTER COLUMN manage_token_version DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "token_version",
                table: "invite");

            migrationBuilder.DropColumn(
                name: "manage_token_version",
                table: "booking");

            migrationBuilder.AddColumn<string>(
                name: "token_hash",
                table: "invite",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "manage_token_hash",
                table: "booking",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_invite_token_hash",
                table: "invite",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_booking_manage_token_hash",
                table: "booking",
                column: "manage_token_hash",
                unique: true);
        }
    }
}
