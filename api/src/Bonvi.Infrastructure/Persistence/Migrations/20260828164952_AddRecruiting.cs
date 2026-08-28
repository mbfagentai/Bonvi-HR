using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecruiting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "candidate_id",
                table: "employment_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pipeline_stages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false, collation: "uz-Cyrl-x-icu"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pipeline_stages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vacancies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    openings = table.Column<int>(type: "integer", nullable: false),
                    opened_at = table.Column<DateOnly>(type: "date", nullable: false),
                    closed_at = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    responsible_employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vacancies", x => x.id);
                    table.ForeignKey(
                        name: "fk_vacancies_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vacancies_employees_responsible_employee_id",
                        column: x => x.responsible_employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vacancies_legal_entities_legal_entity_id",
                        column: x => x.legal_entity_id,
                        principalTable: "legal_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vacancies_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "candidates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vacancy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_stage_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    source = table.Column<int>(type: "integer", nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    speciality = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    resume_file_key = table.Column<string>(type: "text", nullable: true),
                    resume_file_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    resume_content_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    resume_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    closed_at = table.Column<DateOnly>(type: "date", nullable: true),
                    closing_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    first_name = table.Column<string>(type: "text", nullable: false, collation: "uz-Cyrl-x-icu"),
                    last_name = table.Column<string>(type: "text", nullable: false, collation: "uz-Cyrl-x-icu"),
                    middle_name = table.Column<string>(type: "text", nullable: true, collation: "uz-Cyrl-x-icu")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidates", x => x.id);
                    table.ForeignKey(
                        name: "fk_candidates_pipeline_stages_current_stage_id",
                        column: x => x.current_stage_id,
                        principalTable: "pipeline_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_candidates_vacancies_vacancy_id",
                        column: x => x.vacancy_id,
                        principalTable: "vacancies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "candidate_stage_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    outcome = table.Column<int>(type: "integer", nullable: false),
                    conclusion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_stage_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_candidate_stage_entries_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_candidate_stage_entries_pipeline_stages_stage_id",
                        column: x => x.stage_id,
                        principalTable: "pipeline_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_offers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vacancy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    sent_at = table.Column<DateOnly>(type: "date", nullable: false),
                    expires_at = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    responded_at = table.Column<DateOnly>(type: "date", nullable: true),
                    response_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    salary_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    salary_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_offers", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_offers_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_job_offers_vacancies_vacancy_id",
                        column: x => x.vacancy_id,
                        principalTable: "vacancies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_employment_events_candidate_id",
                table: "employment_events",
                column: "candidate_id",
                filter: "candidate_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_stage_entries_candidate_id_recorded_at",
                table: "candidate_stage_entries",
                columns: new[] { "candidate_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_stage_entries_stage_id",
                table: "candidate_stage_entries",
                column: "stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidates_current_stage_id_status",
                table: "candidates",
                columns: new[] { "current_stage_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_candidates_phone",
                table: "candidates",
                column: "phone",
                filter: "phone IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_candidates_source",
                table: "candidates",
                column: "source");

            migrationBuilder.CreateIndex(
                name: "ix_candidates_vacancy_id_status",
                table: "candidates",
                columns: new[] { "vacancy_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_job_offers_candidate_id_status",
                table: "job_offers",
                columns: new[] { "candidate_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_job_offers_sent_at",
                table: "job_offers",
                column: "sent_at");

            migrationBuilder.CreateIndex(
                name: "ix_job_offers_vacancy_id",
                table: "job_offers",
                column: "vacancy_id");

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_stages_is_active_name",
                table: "pipeline_stages",
                columns: new[] { "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_stages_is_active_sort_order",
                table: "pipeline_stages",
                columns: new[] { "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_vacancies_department_id",
                table: "vacancies",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_vacancies_legal_entity_id",
                table: "vacancies",
                column: "legal_entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_vacancies_position_id",
                table: "vacancies",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "ix_vacancies_responsible_employee_id",
                table: "vacancies",
                column: "responsible_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_vacancies_status_opened_at",
                table: "vacancies",
                columns: new[] { "status", "opened_at" });

            // ════════════════════════════════════════════════════════════
            //  BOSQICH NOMI BO'YICHA UNIKALLIK — REGISTRGA SEZGIR EMAS
            //
            //  Voronka bosqichi ham ma'lumotnoma, ya'ni qolgan sakkiztasi
            //  bilan bir xil qoida amal qiladi: «Кенгаш» va «кенгаш» ikki
            //  xil bosqich bo'lib qolishi mumkin emas — voronkada ikkita
            //  bir xil ustun paydo bo'lardi.
            //
            //  Nega EF `HasIndex(...)` emas: fluent API ifoda ustidagi
            //  indeksni bilmaydi (batafsil sabab `CatalogTables`
            //  migratsiyasidagi izohda). `WHERE deleted_at IS NULL` —
            //  yumshoq o'chirilgan yozuvning nomini qayta ishlatish mumkin
            //  bo'lishi kerak.
            //
            //  DIQQAT: bu indeks EF model suratida (snapshot) YO'Q va
            //  keyingi migratsiyalar uni ko'rmaydi. Jadval nomi
            //  o'zgarsa, bu yerdagi SQL ham qo'lda yangilanishi kerak.
            // ════════════════════════════════════════════════════════════
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ix_pipeline_stages_name_lower
                    ON pipeline_stages (lower(name))
                    WHERE deleted_at IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Xom SQL bilan yaratilgan indeks ham qo'lda olib tashlanadi:
            // u EF model suratida yo'q va `DropTable` dan oldin tushishi
            // kerak emas (jadval bilan birga ketadi), lekin niyat aniq
            // ko'rinib tursin.
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_pipeline_stages_name_lower;");

            migrationBuilder.DropTable(
                name: "candidate_stage_entries");

            migrationBuilder.DropTable(
                name: "job_offers");

            migrationBuilder.DropTable(
                name: "candidates");

            migrationBuilder.DropTable(
                name: "pipeline_stages");

            migrationBuilder.DropTable(
                name: "vacancies");

            migrationBuilder.DropIndex(
                name: "ix_employment_events_candidate_id",
                table: "employment_events");

            migrationBuilder.DropColumn(
                name: "candidate_id",
                table: "employment_events");
        }
    }
}
