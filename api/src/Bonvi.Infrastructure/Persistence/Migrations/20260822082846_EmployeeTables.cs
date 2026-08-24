using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "employees",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    gender = table.Column<int>(type: "integer", nullable: true),
                    pinfl = table.Column<string>(type: "text", nullable: true),
                    passport = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    second_phone = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    speciality = table.Column<string>(type: "text", nullable: true),
                    telegram_url = table.Column<string>(type: "text", nullable: true),
                    photo_key = table.Column<string>(type: "text", nullable: true),
                    criminal_record_note = table.Column<string>(type: "text", nullable: true),
                    security_checked = table.Column<bool>(type: "boolean", nullable: true),
                    registered_on_mehnat_uz = table.Column<bool>(type: "boolean", nullable: true),
                    direction_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    needs_review = table.Column<bool>(type: "boolean", nullable: false),
                    review_note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    middle_name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employees", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "employee_flags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    responsible_employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_flags", x => x.id);
                    table.ForeignKey(
                        name: "fk_employee_flags_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_employee_flags_employees_responsible_employee_id",
                        column: x => x.responsible_employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    position_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hire_date = table.Column<DateOnly>(type: "date", nullable: false),
                    hire_order_no = table.Column<string>(type: "text", nullable: true),
                    hire_order_date = table.Column<DateOnly>(type: "date", nullable: true),
                    rate = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    termination_date = table.Column<DateOnly>(type: "date", nullable: true),
                    termination_reason_id = table.Column<Guid>(type: "uuid", nullable: true),
                    termination_order_no = table.Column<string>(type: "text", nullable: true),
                    termination_note = table.Column<string>(type: "text", nullable: true),
                    responsible_employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    needs_review = table.Column<bool>(type: "boolean", nullable: false),
                    review_note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    salary_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    salary_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employments", x => x.id);
                    table.ForeignKey(
                        name: "fk_employments_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employments_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employments_employees_responsible_employee_id",
                        column: x => x.responsible_employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employments_legal_entities_legal_entity_id",
                        column: x => x.legal_entity_id,
                        principalTable: "legal_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employments_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_employments_termination_reasons_termination_reason_id",
                        column: x => x.termination_reason_id,
                        principalTable: "termination_reasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employment_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    order_no = table.Column<string>(type: "text", nullable: true),
                    order_date = table.Column<DateOnly>(type: "date", nullable: true),
                    from_department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    from_position_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_position_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_imported = table.Column<bool>(type: "boolean", nullable: false),
                    from_salary_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    from_salary_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    to_salary_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    to_salary_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employment_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_employment_events_employments_employment_id",
                        column: x => x.employment_id,
                        principalTable: "employments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "probations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    resolved_at = table.Column<DateOnly>(type: "date", nullable: true),
                    conclusion = table.Column<string>(type: "text", nullable: true),
                    reviewer_employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_probations", x => x.id);
                    table.ForeignKey(
                        name: "fk_probations_employees_reviewer_employee_id",
                        column: x => x.reviewer_employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_probations_employments_employment_id",
                        column: x => x.employment_id,
                        principalTable: "employments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_employee_flags_employee_id_type",
                table: "employee_flags",
                columns: new[] { "employee_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_employee_flags_responsible_employee_id",
                table: "employee_flags",
                column: "responsible_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_flags_type_start_date_end_date",
                table: "employee_flags",
                columns: new[] { "type", "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "ix_employees_direction_ids",
                table: "employees",
                column: "direction_ids")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_employees_needs_review",
                table: "employees",
                column: "needs_review",
                filter: "needs_review");

            migrationBuilder.CreateIndex(
                name: "ix_employees_passport",
                table: "employees",
                column: "passport",
                filter: "passport IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_employees_phone",
                table: "employees",
                column: "phone",
                filter: "phone IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_employees_pinfl",
                table: "employees",
                column: "pinfl",
                unique: true,
                filter: "pinfl IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_employment_events_employment_id_effective_date",
                table: "employment_events",
                columns: new[] { "employment_id", "effective_date" });

            migrationBuilder.CreateIndex(
                name: "ix_employment_events_order_no",
                table: "employment_events",
                column: "order_no",
                filter: "order_no IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_employment_events_type_effective_date",
                table: "employment_events",
                columns: new[] { "type", "effective_date" });

            migrationBuilder.CreateIndex(
                name: "ix_employments_department_id",
                table: "employments",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_employments_employee_id",
                table: "employments",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_employments_employee_id_is_primary",
                table: "employments",
                columns: new[] { "employee_id", "is_primary" },
                filter: "is_primary AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_employments_legal_entity_id",
                table: "employments",
                column: "legal_entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_employments_position_id",
                table: "employments",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "ix_employments_responsible_employee_id",
                table: "employments",
                column: "responsible_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_employments_status_hire_date",
                table: "employments",
                columns: new[] { "status", "hire_date" });

            migrationBuilder.CreateIndex(
                name: "ix_employments_termination_date",
                table: "employments",
                column: "termination_date",
                filter: "termination_date IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_employments_termination_reason_id",
                table: "employments",
                column: "termination_reason_id");

            migrationBuilder.CreateIndex(
                name: "ix_probations_employment_id",
                table: "probations",
                column: "employment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_probations_reviewer_employee_id",
                table: "probations",
                column: "reviewer_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_probations_status_end_date",
                table: "probations",
                columns: new[] { "status", "end_date" });

            // ────────────────────────────────────────────────────────────
            //  Ism bo'yicha qidiruv indekslari — XOM SQL bilan
            //
            //  Nega bu yerda, konfiguratsiyada emas: ikkalasi ham complex
            //  type (`FullName`) ustunlari ustida, ikkinchisi esa umuman
            //  IFODA ustidagi indeks. EF Core'ning fluent API'si na birini,
            //  na ikkinchisini ifodalay oladi.
            // ────────────────────────────────────────────────────────────

            // Familiya + ism bo'yicha aniq qidiruv va saralash. Xodimlar
            // ro'yxati DOIM shu tartibda ko'rsatiladi, shu sababli kompozit
            // indeks saralashni ham qoplaydi.
            migrationBuilder.Sql(
                """
                CREATE INDEX ix_employees_last_name_first_name
                    ON employees (last_name, first_name);
                """);

            // `pg_trgm` — matnni uch harfli bo'laklarga bo'lib indekslaydi.
            //
            // NEGA KERAK: HR ismning BIR QISMINI yozib qidiradi («мамаж»,
            // «бобур»). Oddiy B-tree indeks faqat boshidan mos kelishni
            // tezlashtiradi (`LIKE 'мамаж%'`), o'rtadan qidiruvda esa 540
            // qatorning hammasi skanerlanadi. Trigramma indeksi
            // `LIKE '%бобур%'` va `ILIKE` so'rovlarini ham qoplaydi.
            //
            // Kengaytma o'rnatilishi migratsiyaning bir qismi: aks holda
            // yangi muhitda (va har bir test bazasida) indeks yaratilmay
            // qolib, qidiruv jimgina sekinlashardi.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            // Indeks ifodasi qidiruv ifodasi bilan AYNAN bir xil bo'lishi shart —
            // aks holda PostgreSQL uni ishlatmaydi. `lower(...)` registrni,
            // `coalesce(middle_name, '')` esa otasining ismi yo'q yozuvlarni
            // hisobga oladi (ular bo'lmasa butun ifoda NULL bo'lib qolardi).
            migrationBuilder.Sql(
                """
                CREATE INDEX ix_employees_full_name_trgm
                    ON employees
                    USING gin (
                        lower(last_name || ' ' || first_name || ' ' || coalesce(middle_name, ''))
                        gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Jadval o'chirilganda indekslari ham ketadi, lekin tartib aniq
            // bo'lishi uchun ular ataylab oldin olib tashlanadi.
            //
            // `pg_trgm` kengaytmasi ATAYLAB o'chirilmaydi: uni boshqa jadvallar
            // ham ishlatishi mumkin va `DROP EXTENSION` ularning indekslarini
            // sudrab ketardi.
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_employees_full_name_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_employees_last_name_first_name;");

            migrationBuilder.DropTable(
                name: "employee_flags");

            migrationBuilder.DropTable(
                name: "employment_events");

            migrationBuilder.DropTable(
                name: "probations");

            migrationBuilder.DropTable(
                name: "employments");

            migrationBuilder.DropTable(
                name: "employees");
        }
    }
}
