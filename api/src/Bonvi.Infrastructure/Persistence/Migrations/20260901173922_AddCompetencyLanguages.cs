using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetencyLanguages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "employee_competency_languages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_competency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_competency_languages", x => x.id);
                    table.ForeignKey(
                        name: "fk_employee_competency_languages_employee_competencies_employe",
                        column: x => x.employee_competency_id,
                        principalTable: "employee_competencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_employee_competency_languages_employee_competency_id_langua",
                table: "employee_competency_languages",
                columns: new[] { "employee_competency_id", "language" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_competency_languages");
        }
    }
}
