using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMehnatUzProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "mehnat_uz_screenshot_content_type",
                table: "employees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mehnat_uz_screenshot_key",
                table: "employees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mehnat_uz_url",
                table: "employees",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mehnat_uz_screenshot_content_type",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "mehnat_uz_screenshot_key",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "mehnat_uz_url",
                table: "employees");
        }
    }
}
