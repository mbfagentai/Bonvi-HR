using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Xodim ilovasi hisobi — login PROFIL telefonidan, parol HR da (egasining qarori, 2026-09-27).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Olib tashlanadi:</b> <c>login</c> (va <c>ux_employee_accounts_login</c>) —
    /// endi hisob <c>employees.phone</c> orqali topiladi, nusxa ikki raqamni
    /// jimgina ajratib yuborardi; <c>must_change_password</c> — xodim parolni
    /// almashtirmaydi; <c>is_active</c> — qo'lda yopish yo'q, yagona o'chirgich
    /// ishdan ketish (dev'dagi yopilgan hisoblar shu bilan o'zi ochiladi);
    /// <c>failed_login_attempts</c> / <c>locked_until</c> — faqat olib tashlangan
    /// parol almashtirish endpointiga xizmat qilardi (kirishdagi blok
    /// hisobdan tashqarida, <c>EmployeeLoginThrottle</c>).
    /// </para>
    /// <para>
    /// ⭐ <b>Qo'shiladi:</b> <c>password_protected</c> — ochiq parolning
    /// shifrlangan nusxasi, HR ko'rishi uchun. Mavjud qatorlarda <c>NULL</c>
    /// qoladi: eski parolni tiklab bo'lmaydi, u esa ishlashda davom etadi —
    /// HR kerak bo'lganda «Yangi parol yasash» ni bosadi.
    /// </para>
    /// <para>
    /// <c>Down</c> loginni profil telefonidan tiklaydi (raqam bo'sh yoki boshqa
    /// hisobning xodimida ham bo'lsa — hisob identifikatori, unikal indeks
    /// yiqilmasin); yopilgan/majburiy parol holatlari qaytmaydi.
    /// </para>
    /// </remarks>
    public partial class EmployeeAccountsFromProfilePhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_employee_accounts_login",
                table: "employee_accounts");

            migrationBuilder.DropColumn(
                name: "failed_login_attempts",
                table: "employee_accounts");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "employee_accounts");

            migrationBuilder.DropColumn(
                name: "locked_until",
                table: "employee_accounts");

            migrationBuilder.DropColumn(
                name: "login",
                table: "employee_accounts");

            migrationBuilder.DropColumn(
                name: "must_change_password",
                table: "employee_accounts");

            migrationBuilder.AddColumn<string>(
                name: "password_protected",
                table: "employee_accounts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "password_protected",
                table: "employee_accounts");

            migrationBuilder.AddColumn<int>(
                name: "failed_login_attempts",
                table: "employee_accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "employee_accounts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_until",
                table: "employee_accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "login",
                table: "employee_accounts",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE employee_accounts AS account
                SET login = COALESCE(
                    (SELECT employee.phone
                     FROM employees AS employee
                     WHERE employee.id = account.employee_id
                       AND employee.phone IS NOT NULL
                       AND NOT EXISTS (
                           SELECT 1
                           FROM employee_accounts AS other
                           JOIN employees AS holder ON holder.id = other.employee_id
                           WHERE holder.phone = employee.phone
                             AND other.id <> account.id)),
                    account.id::text);
                """);

            migrationBuilder.AlterColumn<string>(
                name: "login",
                table: "employee_accounts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "must_change_password",
                table: "employee_accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ux_employee_accounts_login",
                table: "employee_accounts",
                column: "login",
                unique: true);
        }
    }
}
