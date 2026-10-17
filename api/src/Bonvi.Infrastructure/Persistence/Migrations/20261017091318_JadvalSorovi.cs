using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// SPEC-verifikatsiya-jadval-sorovi: <c>absence_types.employee_can_request</c> (§A) va jadval so'rovlari
    /// jadvallari <c>schedule_change_requests</c> + <c>schedule_change_request_days</c> (§C).
    /// </summary>
    /// <remarks>
    /// Mavjud qatorlar o'zgarmaydi: yangi ustun doimiy sukut bilan (katalog yozuvi, jadval qayta
    /// yozilmaydi; sukut bazada qoladi — eski image rollback'da ham yoza oladi), qolgani yangi jadvallar.
    /// </remarks>
    public partial class JadvalSorovi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET lock_timeout = '3s';");

            // Sukut TRUE: mavjud turlarni xodimlar hozir ham ilovadan so'raydi — hech narsa yopilmasin.
            migrationBuilder.AddColumn<bool>(
                name: "employee_can_request",
                table: "absence_types",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "schedule_change_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    decision_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submitted_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schedule_change_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_schedule_change_requests_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "schedule_change_request_days",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_working = table.Column<bool>(type: "boolean", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    break_start = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    break_end = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    swap_with = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schedule_change_request_days", x => x.id);
                    table.CheckConstraint("ck_schedule_change_request_days_break", "(break_start IS NULL AND break_end IS NULL) OR (is_working AND break_start IS NOT NULL AND break_end IS NOT NULL AND break_start <> break_end)");
                    table.CheckConstraint("ck_schedule_change_request_days_times", "(is_working AND start_time IS NOT NULL AND end_time IS NOT NULL) OR (NOT is_working AND start_time IS NULL AND end_time IS NULL)");
                    table.ForeignKey(
                        name: "fk_schedule_change_request_days_schedule_change_requests_reque",
                        column: x => x.request_id,
                        principalTable: "schedule_change_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_schedule_change_request_days_date",
                table: "schedule_change_request_days",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "ix_schedule_change_request_days_request_id_date",
                table: "schedule_change_request_days",
                columns: new[] { "request_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_schedule_change_requests_employee_id_status",
                table: "schedule_change_requests",
                columns: new[] { "employee_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "schedule_change_request_days");

            migrationBuilder.DropTable(
                name: "schedule_change_requests");

            migrationBuilder.DropColumn(
                name: "employee_can_request",
                table: "absence_types");
        }
    }
}
