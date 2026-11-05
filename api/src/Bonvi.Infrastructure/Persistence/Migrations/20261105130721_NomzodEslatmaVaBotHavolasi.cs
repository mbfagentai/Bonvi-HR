using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NomzodEslatmaVaBotHavolasi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "interview_reminder_delivered",
                table: "candidates",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "interview_reminder_sent_at",
                table: "candidates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telegram_link_token",
                table: "candidates",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_candidates_telegram_link_token",
                table: "candidates",
                column: "telegram_link_token",
                unique: true,
                filter: "telegram_link_token IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_candidates_telegram_link_token",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "interview_reminder_delivered",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "interview_reminder_sent_at",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "telegram_link_token",
                table: "candidates");
        }
    }
}
