using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFormDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "definition_version",
                table: "candidate_forms",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "form_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    definition = table.Column<string>(type: "jsonb", nullable: false),
                    sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_form_definitions", x => x.id);
                    table.UniqueConstraint("ak_form_definitions_form_code_version", x => new { x.form_code, x.version });
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_forms_form_code_definition_version",
                table: "candidate_forms",
                columns: new[] { "form_code", "definition_version" });

            migrationBuilder.AddForeignKey(
                name: "fk_candidate_forms_form_definitions_form_code_definition_versi",
                table: "candidate_forms",
                columns: new[] { "form_code", "definition_version" },
                principalTable: "form_definitions",
                principalColumns: new[] { "form_code", "version" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_candidate_forms_form_definitions_form_code_definition_versi",
                table: "candidate_forms");

            migrationBuilder.DropTable(
                name: "form_definitions");

            migrationBuilder.DropIndex(
                name: "ix_candidate_forms_form_code_definition_version",
                table: "candidate_forms");

            migrationBuilder.DropColumn(
                name: "definition_version",
                table: "candidate_forms");
        }
    }
}
