using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Ma'lumotnomalardan <c>code</c> va <c>sort_order</c> ustunlari
    /// olib tashlanadi; nom esa unikal bo'ladi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega kod ketdi.</b> U qo'lda yoziladigan maydon edi va yangi
    /// yozuv yaratayotgan HR dan so'ralardi. Bitta nomni ikki xil kod
    /// bilan yaratish mumkin bo'lib, keyin bir joyda birinchisi, boshqa
    /// joyda ikkinchisi ishlatilardi. Yozuvning haqiqiy o'ziga xosligi
    /// <c>id</c> da — u baza tomonidan beriladi va hech qachon
    /// takrorlanmaydi. Urug' fayllari va <c>aliases.json</c> ham endi
    /// NOM orqali bog'lanadi.
    /// </para>
    /// <para>
    /// <b>Nega tartib raqami ketdi.</b> Uni to'g'ri qo'yish qiyin edi
    /// (ikki yozuvga bir xil raqam berilsa tartib tasodifiy bo'lardi) va
    /// u hech qanday savolga javob bermasdi. Ro'yxatlar endi ALIFBO
    /// tartibida — <c>name</c> ustunida o'zbek ICU kolatsiyasi bor, ya'ni
    /// «Ғаниев», «Қосимов», «Ҳакимов» o'z joyiga tushadi.
    /// </para>
    /// <para>
    /// <b>Nom unikal — endi BAZADA ham.</b> Ilgari bu faqat use-case
    /// qatlamida tekshirilardi va import yo'li uni chetlab o'tardi.
    /// Indeks <c>lower(name)</c> ustida: «Бухгалтерия» va «бухгалтерия»
    /// bir xil yozuv. Yumshoq o'chirilganlar hisobga olinmaydi — o'chirilgan
    /// yozuvning nomini qayta ishlatish mumkin bo'lishi kerak.
    /// </para>
    /// <para>
    /// ⚠️ <b>Orqaga qaytarish ustunlarni qaytaradi, LEKIN qiymatlarni
    /// emas.</b> Kodlar urug' fayllarida ham yo'q (ular nomga o'girildi) —
    /// eski qiymatlar faqat git tarixida qoldi.
    /// </para>
    /// </remarks>
    public partial class DropCatalogCodeAndSortOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_termination_reasons_code",
                table: "termination_reasons");

            migrationBuilder.DropIndex(
                name: "ix_termination_reasons_is_active_sort_order",
                table: "termination_reasons");

            migrationBuilder.DropIndex(
                name: "ix_positions_code",
                table: "positions");

            migrationBuilder.DropIndex(
                name: "ix_positions_is_active_sort_order",
                table: "positions");

            migrationBuilder.DropIndex(
                name: "ix_penalty_types_code",
                table: "penalty_types");

            migrationBuilder.DropIndex(
                name: "ix_penalty_types_is_active_sort_order",
                table: "penalty_types");

            migrationBuilder.DropIndex(
                name: "ix_legal_entities_code",
                table: "legal_entities");

            migrationBuilder.DropIndex(
                name: "ix_legal_entities_is_active_sort_order",
                table: "legal_entities");

            migrationBuilder.DropIndex(
                name: "ix_document_types_code",
                table: "document_types");

            migrationBuilder.DropIndex(
                name: "ix_document_types_is_active_sort_order",
                table: "document_types");

            migrationBuilder.DropIndex(
                name: "ix_directions_code",
                table: "directions");

            migrationBuilder.DropIndex(
                name: "ix_directions_is_active_sort_order",
                table: "directions");

            migrationBuilder.DropIndex(
                name: "ix_departments_code",
                table: "departments");

            migrationBuilder.DropIndex(
                name: "ix_departments_is_active_sort_order",
                table: "departments");

            migrationBuilder.DropIndex(
                name: "ix_award_types_code",
                table: "award_types");

            migrationBuilder.DropIndex(
                name: "ix_award_types_is_active_sort_order",
                table: "award_types");

            migrationBuilder.DropColumn(
                name: "code",
                table: "termination_reasons");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "termination_reasons");

            migrationBuilder.DropColumn(
                name: "code",
                table: "positions");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "positions");

            migrationBuilder.DropColumn(
                name: "code",
                table: "penalty_types");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "penalty_types");

            migrationBuilder.DropColumn(
                name: "code",
                table: "legal_entities");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "legal_entities");

            migrationBuilder.DropColumn(
                name: "code",
                table: "document_types");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "document_types");

            migrationBuilder.DropColumn(
                name: "code",
                table: "directions");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "directions");

            migrationBuilder.DropColumn(
                name: "code",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "departments");

            migrationBuilder.DropColumn(
                name: "code",
                table: "award_types");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "award_types");

            migrationBuilder.CreateIndex(
                name: "ix_termination_reasons_is_active_name",
                table: "termination_reasons",
                columns: new[] { "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_positions_is_active_name",
                table: "positions",
                columns: new[] { "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_penalty_types_is_active_name",
                table: "penalty_types",
                columns: new[] { "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_legal_entities_is_active_name",
                table: "legal_entities",
                columns: new[] { "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_document_types_is_active_name",
                table: "document_types",
                columns: new[] { "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_directions_is_active_name",
                table: "directions",
                columns: new[] { "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_departments_is_active_name",
                table: "departments",
                columns: new[] { "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_award_types_is_active_name",
                table: "award_types",
                columns: new[] { "is_active", "name" });

            // Nom bo'yicha unikallik — XOM SQL bilan, chunki EF fluent API
            // ifoda ustidagi indeksni (`lower(name)`) ifodalay olmaydi.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ux_legal_entities_name
                    ON legal_entities (lower(name))
                    WHERE deleted_at IS NULL;
                """);
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ux_directions_name
                    ON directions (lower(name))
                    WHERE deleted_at IS NULL;
                """);
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ux_departments_name
                    ON departments (lower(name))
                    WHERE deleted_at IS NULL;
                """);
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ux_positions_name
                    ON positions (lower(name))
                    WHERE deleted_at IS NULL;
                """);
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ux_termination_reasons_name
                    ON termination_reasons (lower(name))
                    WHERE deleted_at IS NULL;
                """);
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ux_award_types_name
                    ON award_types (lower(name))
                    WHERE deleted_at IS NULL;
                """);
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ux_penalty_types_name
                    ON penalty_types (lower(name))
                    WHERE deleted_at IS NULL;
                """);
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ux_document_types_name
                    ON document_types (lower(name))
                    WHERE deleted_at IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_legal_entities_name;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_directions_name;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_departments_name;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_positions_name;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_termination_reasons_name;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_award_types_name;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_penalty_types_name;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_document_types_name;");

            migrationBuilder.DropIndex(
                name: "ix_termination_reasons_is_active_name",
                table: "termination_reasons");

            migrationBuilder.DropIndex(
                name: "ix_positions_is_active_name",
                table: "positions");

            migrationBuilder.DropIndex(
                name: "ix_penalty_types_is_active_name",
                table: "penalty_types");

            migrationBuilder.DropIndex(
                name: "ix_legal_entities_is_active_name",
                table: "legal_entities");

            migrationBuilder.DropIndex(
                name: "ix_document_types_is_active_name",
                table: "document_types");

            migrationBuilder.DropIndex(
                name: "ix_directions_is_active_name",
                table: "directions");

            migrationBuilder.DropIndex(
                name: "ix_departments_is_active_name",
                table: "departments");

            migrationBuilder.DropIndex(
                name: "ix_award_types_is_active_name",
                table: "award_types");

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "termination_reasons",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "termination_reasons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "positions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "positions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "penalty_types",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "penalty_types",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "legal_entities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "legal_entities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "document_types",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "document_types",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "directions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "directions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "departments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "departments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "award_types",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "award_types",
                type: "integer",
                nullable: false,
                defaultValue: 0);

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
                name: "ix_positions_code",
                table: "positions",
                column: "code",
                unique: true,
                filter: "code IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_positions_is_active_sort_order",
                table: "positions",
                columns: new[] { "is_active", "sort_order" });

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
                name: "ix_departments_code",
                table: "departments",
                column: "code",
                unique: true,
                filter: "code IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_departments_is_active_sort_order",
                table: "departments",
                columns: new[] { "is_active", "sort_order" });

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
        }
    }
}
