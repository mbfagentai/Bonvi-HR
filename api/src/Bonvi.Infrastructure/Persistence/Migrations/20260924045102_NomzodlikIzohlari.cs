using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NomzodlikIzohlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "candidate_note",
                table: "employee_competencies",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);
        }

        /// <inheritdoc />
        /// <remarks>
        /// ⚠️ Avval 2000 dan uzun matn kesiladi — aks holda ustunni
        /// toraytirish yiqilardi, orqaga qaytish esa nosozlikdan chiqish
        /// yo'li bo'lishi kerak.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE employee_competencies SET candidate_note = left(candidate_note, 2000) WHERE length(candidate_note) > 2000;");

            migrationBuilder.AlterColumn<string>(
                name: "candidate_note",
                table: "employee_competencies",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10000)",
                oldMaxLength: 10000,
                oldNullable: true);
        }
    }
}
