using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Hech qachon to'ldirilmagan ikkita ustunni olib tashlaydi:
    /// <c>employee_flags.responsible_employee_id</c> va
    /// <c>incidents.position_id</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Belgidagi «mas'ul shaxs».</b> Maydon boshidan beri mavjud edi,
    /// lekin belgi qo'yish oynasi uni HECH QACHON yubormagan va ETL ham
    /// yozmagan — bazadagi barcha yozuvlarda u <c>NULL</c>. Ayni paytda
    /// belgi ro'yxatlarida («Rezerv», «Qora ro'yxat», «Nazoratdagilar»,
    /// «Qobiliyatli») shu maydondan chiziladigan «Mas'ul» ustuni bor edi
    /// va u har doim bo'sh turardi. Foydalanuvchi qarori: maydon ham,
    /// ustun ham kerak emas.
    /// </para>
    /// <para>
    /// <b>Hodisadagi lavozim.</b> U hodisa vaqtidagi lavozim SNAPSHOT'i
    /// sifatida o'ylangan edi, lekin: xodimga lavozim allaqachon
    /// biriktirilgan, formada uni qayta so'rash HR dan ortiqcha ish talab
    /// qilardi va qiymat HECH QAYERDA ko'rsatilmasdi — na jurnalda, na
    /// kartada. Foydalanuvchi qarori: butunlay olib tashlanadi.
    /// </para>
    /// <para>
    /// <b>Ma'lumot yo'qolmaydi.</b> Ikkala ustun ham amalda bo'sh:
    /// <c>responsible_employee_id</c> — 0 ta to'ldirilgan yozuv,
    /// <c>position_id</c> — faqat kecha avtomatik to'ldirilgan sinov
    /// yozuvida bor edi va u o'chirilgan. <c>Down</c> ustunlarni
    /// qaytaradi, lekin qiymatlarni emas — qaytaradigan qiymat yo'q.
    /// </para>
    /// </remarks>
    public partial class DropUnusedFlagAndIncidentColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_employee_flags_employees_responsible_employee_id",
                table: "employee_flags");

            migrationBuilder.DropForeignKey(
                name: "fk_incidents_positions_position_id",
                table: "incidents");

            migrationBuilder.DropIndex(
                name: "ix_incidents_position_id",
                table: "incidents");

            migrationBuilder.DropIndex(
                name: "ix_employee_flags_responsible_employee_id",
                table: "employee_flags");

            migrationBuilder.DropColumn(
                name: "position_id",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "responsible_employee_id",
                table: "employee_flags");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "position_id",
                table: "incidents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "responsible_employee_id",
                table: "employee_flags",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_incidents_position_id",
                table: "incidents",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_flags_responsible_employee_id",
                table: "employee_flags",
                column: "responsible_employee_id");

            migrationBuilder.AddForeignKey(
                name: "fk_employee_flags_employees_responsible_employee_id",
                table: "employee_flags",
                column: "responsible_employee_id",
                principalTable: "employees",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_incidents_positions_position_id",
                table: "incidents",
                column: "position_id",
                principalTable: "positions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
