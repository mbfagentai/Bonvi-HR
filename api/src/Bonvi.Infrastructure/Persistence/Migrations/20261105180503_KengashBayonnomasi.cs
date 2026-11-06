using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KengashBayonnomasi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "council_meetings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_date = table.Column<DateOnly>(type: "date", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    members = table.Column<string>(type: "jsonb", nullable: false),
                    follow_up = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_council_meetings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "council_decisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<int>(type: "integer", nullable: false),
                    practical = table.Column<int>(type: "integer", nullable: true),
                    offered_salary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    condition = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    conclusion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    stage_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_council_decisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_council_decisions_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_council_decisions_council_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "council_meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_council_decisions_candidate_id",
                table: "council_decisions",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_council_decisions_meeting_id_candidate_id",
                table: "council_decisions",
                columns: new[] { "meeting_id", "candidate_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_council_meetings_meeting_date",
                table: "council_meetings",
                column: "meeting_date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_council_meetings_number",
                table: "council_meetings",
                column: "number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "council_decisions");

            migrationBuilder.DropTable(
                name: "council_meetings");
        }
    }
}
