using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Ism va ma'lumotnoma nomi ustunlariga o'zbek (kirill) saralash
    /// qoidasini qo'yadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Baza <c>en_US.utf8</c> kolatsiyasida yaratilgan va kirillni bayt
    /// tartibida solishtiradi: «Ўринов» «Абдуллаев» dan oldin, «Ғ»/«Қ»/«Ҳ»
    /// esa alifboning oxirida chiqardi. HR ning asosiy ekrani — familiya
    /// bo'yicha saralangan xodimlar ro'yxati — shu tartibda ishlatib
    /// bo'lmasdi. Sabab va tanlov `DatabaseCollations` da batafsil.
    /// </para>
    /// <para>
    /// <c>ALTER COLUMN … TYPE</c> bog'liq indekslarni PostgreSQL'ning o'zi
    /// qayta quradi — shu jumladan migratsiyada xom SQL bilan yaratilgan
    /// <c>lower(name)</c> unikal indekslarini va FIO ustidagi trigramma
    /// indeksini. Alohida qadam kerak emas.
    /// </para>
    /// </remarks>
    public partial class UzbekNameCollation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "termination_reasons",
                type: "text",
                nullable: false,
                collation: "uz-Cyrl-x-icu",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "positions",
                type: "text",
                nullable: false,
                collation: "uz-Cyrl-x-icu",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "penalty_types",
                type: "text",
                nullable: false,
                collation: "uz-Cyrl-x-icu",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "legal_entities",
                type: "text",
                nullable: false,
                collation: "uz-Cyrl-x-icu",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "middle_name",
                table: "employees",
                type: "text",
                nullable: true,
                collation: "uz-Cyrl-x-icu",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "last_name",
                table: "employees",
                type: "text",
                nullable: false,
                collation: "uz-Cyrl-x-icu",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "first_name",
                table: "employees",
                type: "text",
                nullable: false,
                collation: "uz-Cyrl-x-icu",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "document_types",
                type: "text",
                nullable: false,
                collation: "uz-Cyrl-x-icu",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "directions",
                type: "text",
                nullable: false,
                collation: "uz-Cyrl-x-icu",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "departments",
                type: "text",
                nullable: false,
                collation: "uz-Cyrl-x-icu",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "award_types",
                type: "text",
                nullable: false,
                collation: "uz-Cyrl-x-icu",
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "termination_reasons",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldCollation: "uz-Cyrl-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "positions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldCollation: "uz-Cyrl-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "penalty_types",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldCollation: "uz-Cyrl-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "legal_entities",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldCollation: "uz-Cyrl-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "middle_name",
                table: "employees",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldCollation: "uz-Cyrl-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "last_name",
                table: "employees",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldCollation: "uz-Cyrl-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "first_name",
                table: "employees",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldCollation: "uz-Cyrl-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "document_types",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldCollation: "uz-Cyrl-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "directions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldCollation: "uz-Cyrl-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "departments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldCollation: "uz-Cyrl-x-icu");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "award_types",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldCollation: "uz-Cyrl-x-icu");
        }
    }
}
