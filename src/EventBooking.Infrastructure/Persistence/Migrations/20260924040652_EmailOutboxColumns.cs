using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmailOutboxColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "correlation_id",
                table: "email_log",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "not_before",
                table: "email_log",
                type: "timestamp with time zone",
                nullable: true);

            // In-flight claims from before the dispatcher do not survive the deploy:
            // every row starts unclaimed and the dispatcher reclaims what is still due.
            migrationBuilder.Sql(
                "UPDATE email_log SET claimed_at = NULL, claim_count = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "correlation_id",
                table: "email_log");

            migrationBuilder.DropColumn(
                name: "not_before",
                table: "email_log");
        }
    }
}
