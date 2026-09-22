using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// «Номзодлик ҳужжатлари» turini MAVJUD bazalarga qo'shadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Nega urug' fayli yetmaydi.</b> <c>CatalogSeeder</c> ataylab
    /// faqat BO'SH ma'lumotnomani to'ldiradi (sababi uning izohida: HR
    /// tuzatgan nomni seeder qayta yaratib, dublikat yasardi). Ya'ni
    /// <c>document-types.json</c> ga qo'shilgan yangi qator ishlayotgan
    /// bazaga hech qachon yetib bormaydi — turi yo'qligi uchun ishga
    /// qabulda fayl ko'chirish jimgina bajarilmasdi.
    /// </para>
    /// <para>
    /// <b>Nega shart IKKITA.</b> <c>EXISTS</c> — jadvalda allaqachon
    /// qator bo'lsagina qo'shiladi: TOZA bazada migratsiya urug'dan
    /// OLDIN ishlaydi va bitta qator qo'yib ketsa, seeder jadvalni
    /// «to'ldirilgan» deb hisoblab, qolgan o'n beshtasini umuman
    /// quymasdi. <c>NOT EXISTS</c> — takrorga qarshi: nom bo'yicha
    /// unikal indeks bor va ikkinchi urinish butun migratsiyani
    /// yiqitardi.
    /// </para>
    /// <para>
    /// <c>is_required = false</c>: bu tur — QABULXONA, har xodimda
    /// bo'lishi shart emas (sababi
    /// <c>CandidateHandover.DocumentTypeName</c> izohida).
    /// </para>
    /// </remarks>
    public partial class NomzodlikHujjatTuri : Migration
    {
        private const string TypeName = "Номзодлик ҳужжатлари";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql(
                $"""
                INSERT INTO document_types (id, name, is_required, is_active, created_at)
                SELECT gen_random_uuid(), '{TypeName}', false, true, now()
                WHERE EXISTS (SELECT 1 FROM document_types)
                  AND NOT EXISTS (
                      SELECT 1 FROM document_types
                      WHERE lower(name) = lower('{TypeName}') AND deleted_at IS NULL);
                """);
        }

        /// <inheritdoc />
        /// <remarks>
        /// ⚠️ Qator FAQAT ishlatilmagan bo'lsa o'chadi. Unga bog'langan
        /// hujjat bo'lsa, o'chirish tashqi kalitga urilib migratsiyani
        /// yiqitardi — orqaga qaytish esa nosozlikdan chiqish yo'li,
        /// uning o'zi nosozlik bo'lmasligi kerak.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql(
                $"""
                DELETE FROM document_types
                WHERE lower(name) = lower('{TypeName}')
                  AND NOT EXISTS (
                      SELECT 1 FROM employee_documents
                      WHERE employee_documents.document_type_id = document_types.id);
                """);
        }
    }
}
