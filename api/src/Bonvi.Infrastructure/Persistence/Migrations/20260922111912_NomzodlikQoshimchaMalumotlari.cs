using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NomzodlikQoshimchaMalumotlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "candidate_note",
                table: "employee_competencies",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "candidate_note",
                table: "employee_competencies");
        }
    }
}
