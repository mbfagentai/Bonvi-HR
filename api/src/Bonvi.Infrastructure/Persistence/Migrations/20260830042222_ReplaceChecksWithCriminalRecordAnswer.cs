using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Ikkita tekshiruv belgisini bitta ANIQ javob bilan almashtiradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nima olib tashlanadi.</b> Xodim kartasidagi «Tekshiruvlar»
    /// kartasida uchta qator bor edi va ikkitasi ortiqcha bo'lib chiqdi
    /// (foydalanuvchi qarori):
    /// </para>
    /// <list type="bullet">
    ///   <item><c>registered_on_mehnat_uz</c> — «mehnat.uz: ha/yo'q»
    ///   qatori. Uning ostida mehnat.uz havolasi va skrinshoti bloki
    ///   turardi, ya'ni bir xil savol ekranda ikki marta so'ralardi.</item>
    ///   <item><c>security_checked</c> — «xavfsizlik tekshiruvi» qatori.
    ///   Bazada bironta ham to'ldirilgan yozuv yo'q edi.</item>
    /// </list>
    /// <para>
    /// <b>Nima qo'shiladi.</b> <c>has_criminal_record</c> — uch holatli
    /// javob (<c>true</c> / <c>false</c> / <c>NULL</c> = noma'lum).
    /// Ilgari sudlanganlik faqat ERKIN MATN edi va undan javobni o'qib
    /// bo'lmasdi: bo'sh matn «sudlanmagan» degani emas, «hech kim
    /// yozmagan» degani. HR uchun bu ikkisi butunlay boshqa javob.
    /// Matn saqlanib qoladi — endi u javobning TAFSILOTI.
    /// </para>
    /// <para>
    /// <b>Nega <c>RENAME</c> emas.</b> EF <c>security_checked</c> ni
    /// <c>has_criminal_record</c> ga «nom o'zgartirish» deb taklif qildi,
    /// lekin ularning ma'nosi TESKARI: «tekshiruvdan o'tgan» odam
    /// «sudlangan» bo'lib qolardi. Shu sababli eski ustun o'chiriladi,
    /// yangisi esa IZOH MATNIDAN to'ldiriladi.
    /// </para>
    /// <para>
    /// <b>Yo'qoladigan yagona narsa</b> — <c>registered_on_mehnat_uz</c>
    /// dagi «ha» javobi (bu bazada 9 ta yozuv). Uni saqlab qolishning
    /// yo'li yo'q: havolasi bo'lmagan «ha» ga o'rnini bosadigan qiymat
    /// yo'q. Ko'chirish vositasi endi o'sha Excel ustunidan HAVOLANING
    /// O'ZINI oladi (<c>EmployeesStep</c>), ya'ni bundan keyin ma'lumot
    /// ko'proq saqlanadi, kamroq emas.
    /// </para>
    /// </remarks>
    public partial class ReplaceChecksWithCriminalRecordAnswer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<bool>(
                name: "has_criminal_record",
                table: "employees",
                type: "boolean",
                nullable: true);

            // Javob MATNDAN o'qiladi:
            //   matn yo'q          → NULL  (hech kim tekshirmagan)
            //   «судланмаган»      → false (tekshirilgan, topilmagan)
            //   boshqa har qanday  → true  (modda, sana, izoh yozilgan)
            migrationBuilder.Sql("""
                UPDATE employees
                SET has_criminal_record = CASE
                    WHEN btrim(coalesce(criminal_record_note, '')) = '' THEN NULL
                    WHEN lower(criminal_record_note) LIKE '%судланмаган%'
                      OR lower(criminal_record_note) LIKE '%sudlanmagan%'
                      OR lower(criminal_record_note) LIKE '%текширилди%' THEN false
                    ELSE true
                END
                WHERE deleted_at IS NULL;
                """);

            migrationBuilder.DropColumn(
                name: "registered_on_mehnat_uz",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "security_checked",
                table: "employees");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Ustunlar qaytariladi, lekin QIYMATLAR qaytmaydi: yangi javob
        /// eskisining ikkalasidan ham boshqa narsani anglatadi va uni
        /// orqaga o'girish taxmin bo'lardi.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<bool>(
                name: "security_checked",
                table: "employees",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "registered_on_mehnat_uz",
                table: "employees",
                type: "boolean",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "has_criminal_record",
                table: "employees");
        }
    }
}
