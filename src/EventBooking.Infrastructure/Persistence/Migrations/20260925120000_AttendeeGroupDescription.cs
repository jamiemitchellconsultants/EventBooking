using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttendeeGroupDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "attendee_group",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "description",
                table: "attendee_group");
        }
    }
}
