using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InviteSettingsSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing rows inherit the domain defaults (SystemSettings.CreateDefault);
            // the defaults are dropped below so new rows must carry explicit snapshots.
            migrationBuilder.AddColumn<int>(
                name: "invite_expiry_days",
                table: "invite",
                type: "integer",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<int>(
                name: "invite_option_count",
                table: "invite",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "max_auto_retry_count",
                table: "invite",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AlterColumn<int>(
                name: "invite_expiry_days",
                table: "invite",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 7);

            migrationBuilder.AlterColumn<int>(
                name: "invite_option_count",
                table: "invite",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 3);

            migrationBuilder.AlterColumn<int>(
                name: "max_auto_retry_count",
                table: "invite",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "invite_expiry_days",
                table: "invite");

            migrationBuilder.DropColumn(
                name: "invite_option_count",
                table: "invite");

            migrationBuilder.DropColumn(
                name: "max_auto_retry_count",
                table: "invite");
        }
    }
}
