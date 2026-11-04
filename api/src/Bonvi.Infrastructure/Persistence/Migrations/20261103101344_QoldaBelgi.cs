using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// HR qo'lda qo'shadigan belgi (SPEC-qolda-belgi §A): dalil jadvali va «HR (qo'lda)»
    /// sintetik qurilma qatori.
    /// </summary>
    /// <remarks>
    /// Qurilma qatori «Mobil ilova» (<c>SeedMobileAppDevice</c>) bilan AYNAN bir xil naqsh va
    /// sabablar: <c>attendance_passes.device_id</c> majburiy, qator faolsiz, tokeni o'lik
    /// (oxirgi bayti 01 bo'lgan 32 bayt — nol xesh «Mobil ilova»niki, ustun unikal), <c>ON CONFLICT DO NOTHING</c>.
    /// </remarks>
    public partial class QoldaBelgi : Migration
    {
        /// <summary>
        /// «HR (qo'lda)» qatorining qat'iy identifikatori — <c>AttendanceDevice.HrManualId</c> bilan AYNAN bir xil
        /// (domen konstantasi ataylab chaqirilmaydi: migratsiya yozilgan paytdagi haqiqatni saqlaydi).
        /// </summary>
        private const string HrManualId = "01996f00-0000-7000-8000-00000000b072";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attendance_manual_passes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_manual_passes", x => x.id);
                    table.ForeignKey(
                        name: "fk_attendance_manual_passes_attendance_passes_id",
                        column: x => x.id,
                        principalTable: "attendance_passes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_attendance_manual_passes_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_manual_passes_employee_id",
                table: "attendance_manual_passes",
                column: "employee_id");

            migrationBuilder.Sql(
                $"""
                INSERT INTO attendance_devices (
                    id, name, provider, identity_provider, direction_role, auth_mode,
                    token_hash, token_prefix, token_issued_at, is_active, created_at)
                VALUES (
                    '{HrManualId}',
                    'HR (qo''lda)',

                    -- AttendanceProvider.Mobile: tahlilchisi YO'Q qurilma (sintetik qatorlar turi).
                    3,

                    -- AttendanceIdentityProvider.HrManual: `raw_person_id` da xodimning o'z identifikatori.
                    6,

                    -- DeviceDirectionRole.ReportsDirection: yo'nalishni HR aytadi.
                    1,

                    -- DeviceAuthMode.Token: ishlatilmaydi, qator hech qachon tasdiqlanmaydi.
                    1,

                    -- O'lik xesh: «Mobil ilova» nol baytlarni olgan (`ux_attendance_devices_token_hash`
                    -- unikal), bu yerda oxirgi bayt 01. Bu natijani beradigan token amalda topilmaydi.
                    '\x0000000000000000000000000000000000000000000000000000000000000001'::bytea,
                    '00000000',
                    now(),
                    false,
                    now())
                ON CONFLICT (id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_manual_passes");

            // HR belgilari bor bazada qator qoladi — `device_id` unga RESTRICT bilan ishora qiladi
            // (SeedMobileAppDevice dagi bilan bir xil sabab).
            migrationBuilder.Sql(
                $"""
                DELETE FROM attendance_devices
                WHERE id = '{HrManualId}'
                  AND NOT EXISTS (
                      SELECT 1 FROM attendance_passes WHERE device_id = '{HrManualId}');
                """);
        }
    }
}
