using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "award_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    has_amount = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_award_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "directions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    color = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_directions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_document_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legal_entities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_id = table.Column<string>(type: "text", nullable: true),
                    full_name = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_legal_entities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "penalty_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    has_amount = table.Column<bool>(type: "boolean", nullable: false),
                    severity = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_penalty_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "termination_reasons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_voluntary = table.Column<bool>(type: "boolean", nullable: false),
                    is_rehireable = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_termination_reasons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "departments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    level = table.Column<int>(type: "integer", nullable: false),
                    path = table.Column<string>(type: "text", nullable: true),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    head_employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_departments", x => x.id);
                    table.ForeignKey(
                        name: "fk_departments_departments_parent_id",
                        column: x => x.parent_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_departments_legal_entities_legal_entity_id",
                        column: x => x.legal_entity_id,
                        principalTable: "legal_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "positions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    grade = table.Column<int>(type: "integer", nullable: false),
                    probation_days = table.Column<int>(type: "integer", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_positions", x => x.id);
                    table.ForeignKey(
                        name: "fk_positions_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "position_duties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_position_duties", x => x.id);
                    table.ForeignKey(
                        name: "fk_position_duties_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_award_types_code",
                table: "award_types",
                column: "code",
                unique: true,
                filter: "code IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_award_types_is_active_sort_order",
                table: "award_types",
                columns: new[] { "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_departments_code",
                table: "departments",
                column: "code",
                unique: true,
                filter: "code IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_departments_head_employee_id",
                table: "departments",
                column: "head_employee_id",
                filter: "head_employee_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_departments_is_active_sort_order",
                table: "departments",
                columns: new[] { "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_departments_legal_entity_id",
                table: "departments",
                column: "legal_entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_departments_parent_id",
                table: "departments",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_departments_path",
                table: "departments",
                column: "path",
                filter: "path IS NOT NULL")
                .Annotation("Npgsql:IndexOperators", new[] { "text_pattern_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_directions_code",
                table: "directions",
                column: "code",
                unique: true,
                filter: "code IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_directions_is_active_sort_order",
                table: "directions",
                columns: new[] { "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_document_types_code",
                table: "document_types",
                column: "code",
                unique: true,
                filter: "code IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_document_types_is_active_sort_order",
                table: "document_types",
                columns: new[] { "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_document_types_is_required",
                table: "document_types",
                column: "is_required",
                filter: "is_required");

            migrationBuilder.CreateIndex(
                name: "ix_legal_entities_code",
                table: "legal_entities",
                column: "code",
                unique: true,
                filter: "code IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_legal_entities_is_active_sort_order",
                table: "legal_entities",
                columns: new[] { "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_legal_entities_tax_id",
                table: "legal_entities",
                column: "tax_id",
                filter: "tax_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_penalty_types_code",
                table: "penalty_types",
                column: "code",
                unique: true,
                filter: "code IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_penalty_types_is_active_sort_order",
                table: "penalty_types",
                columns: new[] { "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_position_duties_position_id_sort_order",
                table: "position_duties",
                columns: new[] { "position_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_positions_code",
                table: "positions",
                column: "code",
                unique: true,
                filter: "code IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_positions_department_id",
                table: "positions",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_positions_is_active_sort_order",
                table: "positions",
                columns: new[] { "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_termination_reasons_code",
                table: "termination_reasons",
                column: "code",
                unique: true,
                filter: "code IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_termination_reasons_is_active_sort_order",
                table: "termination_reasons",
                columns: new[] { "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_termination_reasons_is_voluntary",
                table: "termination_reasons",
                column: "is_voluntary");

            // ════════════════════════════════════════════════════════════
            //  NOM BO'YICHA UNIKALLIK — REGISTRGA SEZGIR EMAS
            //
            //  Muammo: Excel'da «Маркетинг бўлими» va «маркетинг бўлими»
            //  ikki xil yozuv bo'lib yurgan. Oddiy UNIQUE indeks ularni
            //  boshqa-boshqa deb hisoblaydi va takror qayta paydo bo'ladi.
            //
            //  Yechim: `lower(name)` IFODASI ustidagi unikal indeks.
            //  Nega EF `HasIndex(...)` emas: EF Core fluent API'da ifoda
            //  bo'yicha indeks yo'q — u faqat ustunlar ro'yxatini biladi.
            //  Hisoblangan ustun (`GENERATED ... AS (lower(name)) STORED`)
            //  ham ko'rib chiqildi va rad etildi: PostgreSQL so'rovdagi
            //  `lower(name) = $1` ifodasini hisoblangan ustun indeksiga
            //  O'ZI bog'lamaydi, ya'ni repozitoriy indeksdan foydalana
            //  olmasdi. Xom SQL esa ikkala talabni ham bajaradi.
            //
            //  `WHERE deleted_at IS NULL` — yumshoq o'chirilgan yozuvning
            //  nomini qayta ishlatish mumkin bo'lishi kerak.
            //
            //  DIQQAT: bu indekslar EF model suratida (snapshot) YO'Q.
            //  Ular qo'shimcha cheklov bo'lgani uchun keyingi migratsiyalar
            //  ularni ko'rmaydi va o'chirmaydi. Jadval nomi o'zgarsa,
            //  bu yerdagi SQL ham qo'lda yangilanishi kerak — shu sababli
            //  jadval nomlari konfiguratsiyalarda `ConfigureReference("…")`
            //  ga aniq yozilgan.
            // ════════════════════════════════════════════════════════════
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ix_legal_entities_name_lower
                    ON legal_entities (lower(name))
                    WHERE deleted_at IS NULL;
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ix_directions_name_lower
                    ON directions (lower(name))
                    WHERE deleted_at IS NULL;
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ix_departments_name_lower
                    ON departments (lower(name))
                    WHERE deleted_at IS NULL;
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ix_positions_name_lower
                    ON positions (lower(name))
                    WHERE deleted_at IS NULL;
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ix_termination_reasons_name_lower
                    ON termination_reasons (lower(name))
                    WHERE deleted_at IS NULL;
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ix_award_types_name_lower
                    ON award_types (lower(name))
                    WHERE deleted_at IS NULL;
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ix_penalty_types_name_lower
                    ON penalty_types (lower(name))
                    WHERE deleted_at IS NULL;
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ix_document_types_name_lower
                    ON document_types (lower(name))
                    WHERE deleted_at IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Jadval o'chirilganda indekslari ham ketadi, lekin tartib
            // aniq bo'lishi uchun ular ataylab oldin olib tashlanadi.
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_legal_entities_name_lower;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_directions_name_lower;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_departments_name_lower;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_positions_name_lower;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_termination_reasons_name_lower;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_award_types_name_lower;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_penalty_types_name_lower;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_document_types_name_lower;");

            migrationBuilder.DropTable(
                name: "award_types");

            migrationBuilder.DropTable(
                name: "directions");

            migrationBuilder.DropTable(
                name: "document_types");

            migrationBuilder.DropTable(
                name: "penalty_types");

            migrationBuilder.DropTable(
                name: "position_duties");

            migrationBuilder.DropTable(
                name: "termination_reasons");

            migrationBuilder.DropTable(
                name: "positions");

            migrationBuilder.DropTable(
                name: "departments");

            migrationBuilder.DropTable(
                name: "legal_entities");
        }
    }
}
