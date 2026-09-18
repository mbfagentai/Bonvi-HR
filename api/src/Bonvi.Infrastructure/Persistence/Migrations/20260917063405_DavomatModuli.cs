using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Davomat (verifikatsiya) moduli — yettita yangi jadval.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Migratsiya QO'SHIMCHA — mavjud birorta jadvalga tegmaydi.</b>
    /// <c>employees</c> va <c>employments</c> ga na ustun qo'shiladi, na
    /// indeks: modul ularni faqat O'QIYDI. <c>PLAN.md</c> §8 taklif qilgan
    /// <c>employments.external_ref</c> ustuni ATAYLAB yaratilmagan — sababi
    /// <c>AttendanceIdentity</c> izohida (bitta ustun bitta raqam saqlaydi,
    /// tarix saqlamaydi va raqam odamga tegishli, shartnomaga emas).
    /// </para>
    /// <para>
    /// <b>To'rtta indeks xom SQL bilan yaratiladi</b> (pastda). EF fluent
    /// API na <c>lower(...)</c> kabi IFODA ustidagi indeksni, na
    /// <c>NULLS NOT DISTINCT</c> ni ifodalay oladi. DIQQAT: ular EF model
    /// suratida (snapshot) YO'Q — jadval yoki ustun nomi o'zgarsa, bu
    /// yerdagi SQL ham qo'lda yangilanishi kerak.
    /// </para>
    /// <para>
    /// <c>attendance_days.break_taken_minutes</c> / <c>break_deduction_minutes</c>
    /// dastlab alohida migratsiya bilan qo'shilgan edi (parallel qurilish
    /// paytida birinchisini qayta yaratib bo'lmasdi). Merge'dan oldin
    /// SPEC §2.12 bo'yicha shu yerga BIRLASHTIRILDI — modul uchun bitta
    /// migratsiya.
    /// </para>
    /// <para>
    /// Hangfire jadvallari bu migratsiyaning qismi EMAS:
    /// <c>Hangfire.PostgreSql</c> ishga tushishda <c>hangfire</c> sxemasini
    /// o'zi yaratadi.
    /// </para>
    /// </remarks>
    public partial class DavomatModuli : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attendance_devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider = table.Column<int>(type: "integer", nullable: false),
                    identity_provider = table.Column<int>(type: "integer", nullable: false),
                    direction_role = table.Column<int>(type: "integer", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    location_note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    auth_mode = table.Column<int>(type: "integer", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    token_prefix = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    token_issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    token_issued_by = table.Column<Guid>(type: "uuid", nullable: true),
                    hmac_secret = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    allowed_ips = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_event_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_seen_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    last_skew_seconds = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_devices", x => x.id);
                    table.ForeignKey(
                        name: "fk_attendance_devices_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_devices_legal_entities_legal_entity_id",
                        column: x => x.legal_entity_id,
                        principalTable: "legal_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendance_identities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<int>(type: "integer", nullable: false),
                    external_person_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name_in_device = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_identities", x => x.id);
                    table.ForeignKey(
                        name: "fk_attendance_identities_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    cycle_length_days = table.Column<int>(type: "integer", nullable: true),
                    grace_in_minutes = table.Column<int>(type: "integer", nullable: false),
                    grace_out_minutes = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_schedules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "attendance_passes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_provider = table.Column<int>(type: "integer", nullable: false),
                    raw_person_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    resolved_employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    device_local_time = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    device_offset_minutes = table.Column<short>(type: "smallint", nullable: false),
                    offset_assumed = table.Column<bool>(type: "boolean", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    clock_skew_seconds = table.Column<int>(type: "integer", nullable: false),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    direction_source = table.Column<int>(type: "integer", nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    photo_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    photo_purged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    raw_event = table.Column<string>(type: "jsonb", nullable: true),
                    dedupe_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_passes", x => x.id);
                    table.ForeignKey(
                        name: "fk_attendance_passes_attendance_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "attendance_devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_passes_employees_resolved_employee_id",
                        column: x => x.resolved_employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendance_days",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    employment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    position_id = table.Column<Guid>(type: "uuid", nullable: true),
                    work_schedule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    plan_start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    plan_end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    first_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_out_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    planned_minutes = table.Column<int>(type: "integer", nullable: false),
                    worked_minutes = table.Column<int>(type: "integer", nullable: false),
                    late_minutes = table.Column<int>(type: "integer", nullable: false),
                    early_leave_minutes = table.Column<int>(type: "integer", nullable: false),
                    overtime_minutes = table.Column<int>(type: "integer", nullable: false),
                    break_taken_minutes = table.Column<int>(type: "integer", nullable: false),
                    break_deduction_minutes = table.Column<int>(type: "integer", nullable: false),
                    pass_count = table.Column<int>(type: "integer", nullable: false),
                    ignored_pass_count = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    computed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_days", x => x.id);
                    table.ForeignKey(
                        name: "fk_attendance_days_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_days_employments_employment_id",
                        column: x => x.employment_id,
                        principalTable: "employments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_days_work_schedules_work_schedule_id",
                        column: x => x.work_schedule_id,
                        principalTable: "work_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "schedule_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<int>(type: "integer", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    position_id = table.Column<Guid>(type: "uuid", nullable: true),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schedule_assignments", x => x.id);
                    table.CheckConstraint("ck_schedule_assignments_scope", "(scope = 1 AND employee_id IS NOT NULL AND position_id IS NULL AND department_id IS NULL) OR\n(scope = 2 AND position_id IS NOT NULL AND employee_id IS NULL AND department_id IS NULL) OR\n(scope = 3 AND department_id IS NOT NULL AND employee_id IS NULL AND position_id IS NULL) OR\n(scope = 4 AND employee_id IS NULL AND position_id IS NULL AND department_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_schedule_assignments_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_schedule_assignments_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_schedule_assignments_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_schedule_assignments_work_schedules_work_schedule_id",
                        column: x => x.work_schedule_id,
                        principalTable: "work_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_schedule_days",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_index = table.Column<int>(type: "integer", nullable: false),
                    is_working = table.Column<bool>(type: "boolean", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    break_minutes = table.Column<int>(type: "integer", nullable: false),
                    break_start = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    break_end = table.Column<TimeOnly>(type: "time without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_schedule_days", x => x.id);
                    table.CheckConstraint("ck_work_schedule_days_break_window", "(break_start IS NULL AND break_end IS NULL)\nOR (is_working\n    AND break_start IS NOT NULL\n    AND break_end IS NOT NULL\n    AND break_start <> break_end)");
                    table.ForeignKey(
                        name: "fk_work_schedule_days_work_schedules_work_schedule_id",
                        column: x => x.work_schedule_id,
                        principalTable: "work_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_days_date_status",
                table: "attendance_days",
                columns: new[] { "local_date", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_days_department_date",
                table: "attendance_days",
                columns: new[] { "department_id", "local_date" });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_days_employment_id",
                table: "attendance_days",
                column: "employment_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_days_legal_entity_date",
                table: "attendance_days",
                columns: new[] { "legal_entity_id", "local_date" });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_days_work_schedule_id",
                table: "attendance_days",
                column: "work_schedule_id");

            migrationBuilder.CreateIndex(
                name: "ux_attendance_days_employee_date",
                table: "attendance_days",
                columns: new[] { "employee_id", "local_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attendance_devices_department_id",
                table: "attendance_devices",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_devices_legal_entity_id",
                table: "attendance_devices",
                column: "legal_entity_id");

            migrationBuilder.CreateIndex(
                name: "ux_attendance_devices_token_hash",
                table: "attendance_devices",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attendance_identities_employee",
                table: "attendance_identities",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ux_attendance_identities_person",
                table: "attendance_identities",
                columns: new[] { "provider", "external_person_id" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_passes_device_time",
                table: "attendance_passes",
                columns: new[] { "device_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_passes_employee_time",
                table: "attendance_passes",
                columns: new[] { "resolved_employee_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_passes_local_date",
                table: "attendance_passes",
                column: "local_date");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_passes_pending",
                table: "attendance_passes",
                column: "processed_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_passes_photo",
                table: "attendance_passes",
                column: "received_at",
                filter: "photo_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_passes_unmatched",
                table: "attendance_passes",
                columns: new[] { "identity_provider", "raw_person_id" },
                filter: "resolved_employee_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_attendance_passes_device_dedupe",
                table: "attendance_passes",
                columns: new[] { "device_id", "dedupe_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_schedule_assignments_department_id",
                table: "schedule_assignments",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_schedule_assignments_employee_id",
                table: "schedule_assignments",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_schedule_assignments_lookup",
                table: "schedule_assignments",
                columns: new[] { "scope", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "ix_schedule_assignments_position_id",
                table: "schedule_assignments",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "ix_schedule_assignments_work_schedule_id",
                table: "schedule_assignments",
                column: "work_schedule_id");

            migrationBuilder.CreateIndex(
                name: "ux_work_schedule_days_day",
                table: "work_schedule_days",
                columns: new[] { "work_schedule_id", "day_index" },
                unique: true);

            // ════════════════════════════════════════════════════════════
            //  NOM BO'YICHA UNIKAL INDEKSLAR — XOM SQL bilan
            //
            //  Boshqa sakkizta ma'lumotnomadagi bilan AYNAN bir xil qoida:
            //  EF fluent API `lower(name)` kabi IFODA ustidagi indeksni
            //  ifodalay olmaydi, usiz esa «Kirish-1» va «kirish-1» ikki
            //  boshqa qurilma bo'lib o'tib ketardi.
            //
            //  `WHERE deleted_at IS NULL` — yumshoq o'chirilgan yozuvning
            //  nomini qayta ishlatish mumkin bo'lishi kerak.
            // ════════════════════════════════════════════════════════════
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ux_attendance_devices_name
                    ON attendance_devices (lower(name))
                    WHERE deleted_at IS NULL;
                """);

            // Seriya raqami ixtiyoriy, shu sababli filtrga `IS NOT NULL`
            // ham qo'shiladi: raqami ko'rsatilmagan o'nta qurilma
            // bir-birini to'sib qo'ymasligi kerak.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ux_attendance_devices_serial
                    ON attendance_devices (lower(serial_number))
                    WHERE serial_number IS NOT NULL AND deleted_at IS NULL;
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ux_work_schedules_name
                    ON work_schedules (lower(name))
                    WHERE deleted_at IS NULL;
                """);

            // ════════════════════════════════════════════════════════════
            //  BIRIKTIRMA TAKRORINI TO'SUVCHI INDEKS
            //
            //  ⭐ `NULLS NOT DISTINCT` — PostgreSQL 15+ sintaksisi (bizda
            //  18.6) va u bu yerda ZARURAT, bezak emas. Sukut bo'yicha
            //  PostgreSQL ikki NULL ni «har xil» deb hisoblaydi, ya'ni
            //  usiz ikkita bir xil «2026-01-01 dan sukut jadval» qatori
            //  BIR VAQTDA turaverardi (uchala obyekt ustuni ham NULL) va
            //  qaysi biri yutishi qator tartibiga qolardi.
            //
            //  EF fluent API bu sintaksisni ifodalay olmaydi — shu sababli
            //  xom SQL.
            // ════════════════════════════════════════════════════════════
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ux_schedule_assignments_scope
                    ON schedule_assignments
                       (scope, employee_id, position_id, department_id, effective_from)
                    NULLS NOT DISTINCT
                    WHERE deleted_at IS NULL;
                """);
        }

        /// <summary>Modulning yettita jadvalini o'chiradi.</summary>
        /// <remarks>
        /// Xom SQL bilan yaratilgan to'rtta indeks alohida o'chirilmaydi:
        /// ular o'z jadvallariga tegishli va <c>DROP TABLE</c> bilan birga
        /// ketadi. Moduldan TASHQARIDAGI birorta jadval bu yerda
        /// o'zgarmaydi — <c>Up</c> ham ularga tegmagan edi.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_days");

            migrationBuilder.DropTable(
                name: "attendance_identities");

            migrationBuilder.DropTable(
                name: "attendance_passes");

            migrationBuilder.DropTable(
                name: "schedule_assignments");

            migrationBuilder.DropTable(
                name: "work_schedule_days");

            migrationBuilder.DropTable(
                name: "attendance_devices");

            migrationBuilder.DropTable(
                name: "work_schedules");
        }
    }
}
