using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BolimDavomatiKaliti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "department_id",
                table: "api_keys",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_api_keys_department_id",
                table: "api_keys",
                column: "department_id");

            migrationBuilder.AddForeignKey(
                name: "fk_api_keys_departments_department_id",
                table: "api_keys",
                column: "department_id",
                principalTable: "departments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_api_keys_departments_department_id",
                table: "api_keys");

            migrationBuilder.DropIndex(
                name: "ix_api_keys_department_id",
                table: "api_keys");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "api_keys");
        }
    }
}
