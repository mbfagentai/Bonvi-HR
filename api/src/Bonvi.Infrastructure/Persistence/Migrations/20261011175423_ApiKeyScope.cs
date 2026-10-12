using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApiKeyScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mavjud kalitlar 1 (Basic) bo'lib qoladi: ularning doirasi kengaymasligi
            // kerak. EF yaratgan 0 — noma'lum qiymat, enum'da yo'q.
            migrationBuilder.AddColumn<int>(
                name: "scope",
                table: "api_keys",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "scope",
                table: "api_keys");
        }
    }
}
