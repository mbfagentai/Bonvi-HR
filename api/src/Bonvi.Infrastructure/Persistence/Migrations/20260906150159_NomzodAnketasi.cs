using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NomzodAnketasi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "photo_key",
                table: "candidates",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "candidate_questionnaires",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    education = table.Column<int>(type: "integer", nullable: false),
                    work_experience = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    marital_status = table.Column<int>(type: "integer", nullable: false),
                    russian_level = table.Column<int>(type: "integer", nullable: false),
                    other_languages = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    office_skills = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    last_salary = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    extra_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    has_driver_license = table.Column<bool>(type: "boolean", nullable: false),
                    driver_license_categories = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    expected_salary = table.Column<int>(type: "integer", nullable: false),
                    referral_source = table.Column<int>(type: "integer", nullable: false),
                    offer_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_questionnaires", x => x.id);
                    table.ForeignKey(
                        name: "fk_candidate_questionnaires_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_questionnaires_candidate_id",
                table: "candidate_questionnaires",
                column: "candidate_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_questionnaires");

            migrationBuilder.DropColumn(
                name: "photo_key",
                table: "candidates");
        }
    }
}
