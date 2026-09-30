using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Xodim ilovasining hisoblari va ularning yangilash tokenlari
    /// (SPEC-xodim-ilovasi §D2, §K3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>ORQAGA QAYTARISH — MA'LUMOT YO'QOLADI, ZAXIRA SHART.</b>
    /// <c>Down</c> ~950 ta parol xeshini va barcha ochiq sessiyalarni
    /// o'chiradi. Qayta ko'tarilganda HR har bir bir martalik parolni
    /// QO'LDA qaytadan berishga majbur bo'ladi.
    /// </para>
    /// <para>
    /// Ikkala jadval ham yangi va bo'sh, ya'ni qulf xavfi yo'q:
    /// <c>employees</c> ga tashqi kalit qo'shilayotgan BOLA jadval bo'sh
    /// bo'lgani uchun tekshirish scan talab qilmaydi.
    /// </para>
    /// </remarks>
    public partial class AddEmployeeAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "employee_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    must_change_password = table.Column<bool>(type: "boolean", nullable: false),
                    password_set_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_login_attempts = table.Column<int>(type: "integer", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_accounts", x => x.id);
                    table.ForeignKey(
                        name: "fk_employee_accounts_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employee_refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_by_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    replaced_by_token_hash = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_employee_refresh_tokens_employee_accounts_employee_account_",
                        column: x => x.employee_account_id,
                        principalTable: "employee_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_employee_accounts_employee",
                table: "employee_accounts",
                column: "employee_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_employee_accounts_login",
                table: "employee_accounts",
                column: "login",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_employee_refresh_tokens_account_expires",
                table: "employee_refresh_tokens",
                columns: new[] { "employee_account_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ux_employee_refresh_tokens_hash",
                table: "employee_refresh_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_refresh_tokens");

            migrationBuilder.DropTable(
                name: "employee_accounts");
        }
    }
}
