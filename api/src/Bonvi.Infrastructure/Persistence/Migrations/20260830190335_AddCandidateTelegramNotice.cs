using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateTelegramNotice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "rejection_notice_sent_at",
                table: "candidates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "telegram_chat_id",
                table: "candidates",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telegram_language",
                table: "candidates",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_candidates_status_rejection_notice_sent_at",
                table: "candidates",
                columns: new[] { "status", "rejection_notice_sent_at" },
                filter: "telegram_chat_id IS NOT NULL AND rejection_notice_sent_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_candidates_status_rejection_notice_sent_at",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "rejection_notice_sent_at",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "telegram_chat_id",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "telegram_language",
                table: "candidates");
        }
    }
}
