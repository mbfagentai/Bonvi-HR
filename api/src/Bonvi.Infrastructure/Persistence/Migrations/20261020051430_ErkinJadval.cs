using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// «Erkin ish jadvali» (SPEC-verifikatsiya-erkin-jadval §3): haftalik kun, yillik to'r katagi va
    /// jadval so'rovi kunida <c>is_flexible</c> + <c>required_minutes</c> («Ish vaqti»), kun yozuvida
    /// <c>is_flexible</c> (reja surati) + <c>shortfall_minutes</c> («Kam ishlangan»).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Faqat ADD COLUMN va ADD CONSTRAINT. <b>UPDATE yo'q</b>: mavjud har bir qator erkin emas,
    /// kam ishlangan 0 — ya'ni avvalgidek hisoblanadi. Doimiy sukutlar PostgreSQL 11+ da faqat
    /// katalog yozuvi, jadval (<c>attendance_days</c> katta) qayta yozilmaydi.
    /// </para>
    /// <para>
    /// Sukut BAZADA qoladi: eski image rollback'dan keyin ustunlarni bilmay qator qo'sha oladi
    /// (<c>EmployeeMobileFlagSukuti</c> dagi saboq). CHECK da <c>is_working</c> ATAYLAB yo'q — eski
    /// image erkin kunni dam olish kuniga aylantirsa saqlash yiqilmasin. Erkin tarmoqdagi
    /// <c>required_minutes IS NOT NULL</c> — SPEC matniga qo'shimcha: usiz (true, NULL) qator NULL
    /// natija bilan CHECK dan o'tib ketardi. Modelda sukut yo'q — loyiha qoidasi.
    /// ⚠️ Down barcha erkin sozlamalarni o'chiradi.
    /// </para>
    /// </remarks>
    public partial class ErkinJadval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET lock_timeout = '3s';");

            migrationBuilder.AddColumn<bool>(
                name: "is_flexible",
                table: "work_schedule_days",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "required_minutes",
                table: "work_schedule_days",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_flexible",
                table: "work_schedule_dates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "required_minutes",
                table: "work_schedule_dates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_flexible",
                table: "schedule_change_request_days",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "required_minutes",
                table: "schedule_change_request_days",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_flexible",
                table: "attendance_days",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "shortfall_minutes",
                table: "attendance_days",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_schedule_days_flexible",
                table: "work_schedule_days",
                sql: "(NOT is_flexible AND required_minutes IS NULL) OR (is_flexible AND required_minutes IS NOT NULL AND required_minutes BETWEEN 1 AND 1440)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_schedule_dates_flexible",
                table: "work_schedule_dates",
                sql: "(NOT is_flexible AND required_minutes IS NULL) OR (is_flexible AND required_minutes IS NOT NULL AND required_minutes BETWEEN 1 AND 1440)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_schedule_change_request_days_flexible",
                table: "schedule_change_request_days",
                sql: "(NOT is_flexible AND required_minutes IS NULL) OR (is_flexible AND required_minutes IS NOT NULL AND required_minutes BETWEEN 1 AND 1440)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_work_schedule_days_flexible",
                table: "work_schedule_days");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_schedule_dates_flexible",
                table: "work_schedule_dates");

            migrationBuilder.DropCheckConstraint(
                name: "ck_schedule_change_request_days_flexible",
                table: "schedule_change_request_days");

            migrationBuilder.DropColumn(
                name: "is_flexible",
                table: "work_schedule_days");

            migrationBuilder.DropColumn(
                name: "required_minutes",
                table: "work_schedule_days");

            migrationBuilder.DropColumn(
                name: "is_flexible",
                table: "work_schedule_dates");

            migrationBuilder.DropColumn(
                name: "required_minutes",
                table: "work_schedule_dates");

            migrationBuilder.DropColumn(
                name: "is_flexible",
                table: "schedule_change_request_days");

            migrationBuilder.DropColumn(
                name: "required_minutes",
                table: "schedule_change_request_days");

            migrationBuilder.DropColumn(
                name: "is_flexible",
                table: "attendance_days");

            migrationBuilder.DropColumn(
                name: "shortfall_minutes",
                table: "attendance_days");
        }
    }
}
