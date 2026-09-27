using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Birlashgan hujjat turlarini MAVJUD bazalarga qo'shadi:
    /// «Анкета + кенгаш хулосаси» va «Анкета + кенгаш хулосаси + тест жавоблари».
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega (HR talabi, 2026-09-27).</b> Nomzodlik papkasi odatda BITTA
    /// PDF bo'lib skanerlanadi va uning ichida bir nechta hujjat bo'ladi.
    /// Ro'yxatda faqat «Анкета» bor edi — HR uch hujjatli faylni ham shu
    /// tur bilan yuklashga majbur edi va keyin PDF'ni ochmasdan ichida nima
    /// borligini bilib bo'lmasdi. Tur NOMI endi mazmunni aytadi:
    /// </para>
    /// <list type="bullet">
    ///   <item>ishga kirganlar — anketa + kengash xulosasi + test javoblari;</item>
    ///   <item>rad etilganlar va rezerv — anketa + kengash xulosasi;</item>
    ///   <item>kengashga yetmaganlar — faqat anketa (tur allaqachon bor).</item>
    /// </list>
    /// <para>
    /// <b>Qolgan hamma narsa <c>NomzodlikHujjatTuri</c> bilan bir xil</b>
    /// (sabablar o'sha migratsiya izohida): urug' fayli ishlayotgan bazaga
    /// yetib bormaydi, <c>EXISTS</c> — toza bazada urug'ni buzmaslik uchun,
    /// <c>NOT EXISTS</c> — takrorga qarshi (HR shu nomni ekrandan o'zi
    /// qo'shgan bo'lishi ham mumkin).
    /// </para>
    /// <para>
    /// <c>is_required = false</c>: har xodimda bo'lishi shart emas — qaysi
    /// biri kerakligi odamning yo'liga bog'liq.
    /// </para>
    /// </remarks>
    public partial class BirlashganHujjatTurlari : Migration
    {
        private static readonly string[] TypeNames =
        [
            "Анкета + кенгаш хулосаси",
            "Анкета + кенгаш хулосаси + тест жавоблари",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            foreach (var name in TypeNames)
            {
                migrationBuilder.Sql(
                    $"""
                    INSERT INTO document_types (id, name, is_required, is_active, created_at)
                    SELECT gen_random_uuid(), '{name}', false, true, now()
                    WHERE EXISTS (SELECT 1 FROM document_types)
                      AND NOT EXISTS (
                          SELECT 1 FROM document_types
                          WHERE lower(name) = lower('{name}') AND deleted_at IS NULL);
                    """);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// ⚠️ Qator FAQAT ishlatilmagan bo'lsa o'chadi — sababi
        /// <c>NomzodlikHujjatTuri.Down</c> izohida: orqaga qaytish tashqi
        /// kalitga urilib yiqilmasligi kerak.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            foreach (var name in TypeNames)
            {
                migrationBuilder.Sql(
                    $"""
                    DELETE FROM document_types
                    WHERE lower(name) = lower('{name}')
                      AND NOT EXISTS (
                          SELECT 1 FROM employee_documents
                          WHERE employee_documents.document_type_id = document_types.id);
                    """);
            }
        }
    }
}
