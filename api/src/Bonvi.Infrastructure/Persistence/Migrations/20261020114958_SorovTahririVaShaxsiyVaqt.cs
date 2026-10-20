using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Kutayotgan so'rovni tahrirlash va shaxsiy vaqt (SPEC-verifikatsiya-sorov-tahriri §2.1): yo'qlik va
    /// jadval so'rovida <c>version</c> (parallellik belgisi) + <c>edited_at</c> / <c>edited_by_account_id</c>
    /// (oxirgi tahrir), kun yozuvida <c>personal_minutes</c> («Shaxsiy vaqt»).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Faqat yettita ADD COLUMN. <b>UPDATE yo'q</b>: mavjud qatorlar versiya 0 da, hech qachon
    /// tahrirlanmagan va shaxsiy vaqti 0 — ya'ni avvalgidek ishlaydi. Doimiy sukutlar PostgreSQL 11+ da
    /// faqat katalog yozuvi, <c>attendance_days</c> qayta yozilmaydi. Indeks va CHECK yo'q:
    /// <c>attendance_days</c> — yagona yozuvchili hisoblangan jadval, <c>version</c> ni qo'lda hech kim
    /// o'zgartirmaydi. <c>edited_by_account_id</c> ga FK yo'q — <c>submitted_by_account_id</c> kabi.
    /// </para>
    /// <para>
    /// Sukut BAZADA qoladi: eski image rollback'dan keyin ustunlarni bilmay qator qo'sha oladi
    /// (<c>EmployeeMobileFlagSukuti</c> dagi saboq); u paytda <c>version</c> oshmaydi va
    /// <c>personal_minutes</c> eskiradi (eski kod ikkalasini ham o'qimaydi). Modelda sukut yo'q —
    /// loyiha qoidasi. ⚠️ Down tahrir belgilari va shaxsiy daqiqalarni o'chiradi (qayta hisoblanadi).
    /// </para>
    /// </remarks>
    public partial class SorovTahririVaShaxsiyVaqt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET lock_timeout = '3s';");

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "absence_requests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "edited_at",
                table: "absence_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "edited_by_account_id",
                table: "absence_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "schedule_change_requests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "edited_at",
                table: "schedule_change_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "edited_by_account_id",
                table: "schedule_change_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "personal_minutes",
                table: "attendance_days",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "personal_minutes",
                table: "attendance_days");

            migrationBuilder.DropColumn(
                name: "edited_by_account_id",
                table: "schedule_change_requests");

            migrationBuilder.DropColumn(
                name: "edited_at",
                table: "schedule_change_requests");

            migrationBuilder.DropColumn(
                name: "version",
                table: "schedule_change_requests");

            migrationBuilder.DropColumn(
                name: "edited_by_account_id",
                table: "absence_requests");

            migrationBuilder.DropColumn(
                name: "edited_at",
                table: "absence_requests");

            migrationBuilder.DropColumn(
                name: "version",
                table: "absence_requests");
        }
    }
}
