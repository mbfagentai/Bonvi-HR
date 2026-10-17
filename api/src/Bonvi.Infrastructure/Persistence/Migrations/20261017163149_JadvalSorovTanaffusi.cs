using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Jadval so'rovi kunida tanaffus uzunligi oynadan alohida saqlanadi — jadval kunidagidek
    /// (<c>work_schedule_days.break_minutes</c>): almashtirishda 60 daqiqalik tushlik 12:00–15:00
    /// oynasi bilan 180 daqiqaga aylanib ketmasin.
    /// </summary>
    /// <remarks>
    /// Mavjud qatorlarga oynaning uzunligi yoziladi — ular shu qoida bilan saqlangan edi
    /// (yarim tundan o'tgan oyna ham: 23:30–00:30 = 60).
    /// </remarks>
    public partial class JadvalSorovTanaffusi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET lock_timeout = '3s';");

            migrationBuilder.AddColumn<int>(
                name: "break_minutes",
                table: "schedule_change_request_days",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE schedule_change_request_days
                   SET break_minutes = (EXTRACT(EPOCH FROM (break_end - break_start)) / 60)::int
                                       + CASE WHEN break_end < break_start THEN 1440 ELSE 0 END
                 WHERE is_working AND break_start IS NOT NULL AND break_end IS NOT NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_schedule_change_request_days_break_minutes",
                table: "schedule_change_request_days",
                sql: "break_minutes BETWEEN 0 AND 480 AND (is_working OR break_minutes = 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_schedule_change_request_days_break_minutes",
                table: "schedule_change_request_days");

            migrationBuilder.DropColumn(
                name: "break_minutes",
                table: "schedule_change_request_days");
        }
    }
}
