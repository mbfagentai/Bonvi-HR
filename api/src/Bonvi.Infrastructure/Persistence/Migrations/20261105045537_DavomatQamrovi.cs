using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// «Davomat mas'uli»: adds the empty table of departments assigned to a scoped user.
    /// Additive, no existing table changes. The role itself is data and comes from
    /// <c>IdentitySeeder</c>.
    /// </summary>
    /// <remarks>
    /// <b>Rollback is not harmless.</b> <c>Down</c> drops only the assignments, but the role and
    /// its holders stay, and a pre-feature image knows neither the marker nor the guard. Its
    /// holders get org-wide attendance access and every login-only route. Deactivate them or
    /// move them to another role BEFORE returning to such an image (docs/DEPLOY-CHECKLIST.md).
    /// </remarks>
    public partial class DavomatQamrovi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_attendance_scopes",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_attendance_scopes", x => new { x.user_id, x.department_id });
                    table.ForeignKey(
                        name: "fk_user_attendance_scopes_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_attendance_scopes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_attendance_scopes_department_id",
                table: "user_attendance_scopes",
                column: "department_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_attendance_scopes");
        }
    }
}
