using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Ism bo'yicha qidiruv <c>қ</c>, <c>ғ</c>, <c>ҳ</c>, <c>ў</c> va <c>ё</c>
    /// harflarini asosiy harf bilan bir xil deb hisoblaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sabab <c>SearchFolding</c> izohida: bu harflar rus klaviaturasida yo'q
    /// va HR «Абдуқодиров» o'rniga «Абдукодиров» yozadi — yozuv ko'z oldida
    /// tursa ham topilmaydi, HR uni «bazada yo'q» deb hisoblab dublikat
    /// ochadi.
    /// </para>
    /// <para>
    /// <b>Nega eski indeks o'chiriladi.</b> <c>ix_employees_full_name_trgm</c>
    /// yig'ilmagan ifoda (<c>lower(...)</c>) ustida qurilgan. So'rov endi
    /// <c>translate(lower(...), …)</c> ni ishlatadi va PostgreSQL ifoda
    /// ustidagi indeksni faqat AYNAN mos tushganda oladi — eski indeks
    /// ishlatilmay, faqat yozuv paytida qo'shimcha yuk bo'lib qolardi.
    /// </para>
    /// <para>
    /// <b>Almashtirish jadvali uch joyda va u BIR XIL bo'lishi shart:</b>
    /// shu yerdagi indeksda, <c>EmployeeReadModel.MatchingEmployees</c> dagi
    /// so'rovda va <c>SearchFolding.NameFoldFrom/NameFoldTo</c> konstantalarida.
    /// Biri o'zgarsa, indeks jimgina ishlamay qo'yadi.
    /// </para>
    /// </remarks>
    public partial class SearchFolding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_employees_full_name_trgm;");

            migrationBuilder.Sql(
                """
                CREATE INDEX ix_employees_full_name_fold_trgm
                    ON employees
                    USING gin (
                        translate(
                            lower(last_name || ' ' || first_name || ' ' || coalesce(middle_name, '')),
                            'қғҳўё', 'кгхуе')
                        gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_employees_full_name_fold_trgm;");

            // Eski indeks aynan avvalgi ko'rinishida qaytariladi — orqaga
            // qaytarish so'rov kodi bilan birga bajariladi va ikkalasi
            // bir-biriga mos qolishi kerak.
            migrationBuilder.Sql(
                """
                CREATE INDEX ix_employees_full_name_trgm
                    ON employees
                    USING gin (
                        lower(last_name || ' ' || first_name || ' ' || coalesce(middle_name, ''))
                        gin_trgm_ops);
                """);
        }
    }
}
