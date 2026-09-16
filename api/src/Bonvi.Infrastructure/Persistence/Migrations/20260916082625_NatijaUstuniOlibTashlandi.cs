using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bosqich yozuvidagi «natija» ustunini olib tashlaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nima uchun.</b> Har yozuvda «кутилмоқда / ўтди / ўтмади»
    /// sanog'i bor edi. Foydalanuvchi qarori (2026-09-16): u ortiqcha —
    /// BOSQICHNING O'ZI holatni aytadi («Анкета», «Гаплашилди»,
    /// «Кенгаш»), natija esa o'sha bosqichning XULOSASIDA yozilgan.
    /// Ikkita joy bir xil savolga javob berardi.
    /// </para>
    /// <para>
    /// ⚠️ <b>Ustun tashlanishidan OLDIN ma'lumot ko'chiriladi.</b>
    /// Yozuvlarning aksariyatida natija xulosa matnida ham bor (Excel
    /// ko'chirishida ETL aynan shu matnni yozgan), lekin bir necha
    /// o'nlab qatorda xulosa BO'SH va natija yagona ma'lumot bo'lib
    /// qolgan. O'shalarga natija matn bo'lib yoziladi — aks holda
    /// «ўтди/ўтмади» butunlay yo'qolardi. Bo'sh bo'lmagan xulosaga
    /// TEGILMAYDI: u HR yozgan matn va unga qo'shimcha yozish uni
    /// buzardi.
    /// </para>
    /// <para>
    /// <b>«Кутилмоқда» ko'chirilmaydi</b> — u ma'lumot emas, «hech
    /// narsa yozilmagan» degani.
    /// </para>
    /// <para>
    /// <b>Qaytarish.</b> <c>Down</c> ustunni qaytaradi, lekin QIYMATLARNI
    /// emas — ular tiklanmaydi. Ma'lumot kerak bo'lsa, deploy oldidan
    /// olingan zaxiradan olinadi.
    /// </para>
    /// </remarks>
    public partial class NatijaUstuniOlibTashlandi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            // Natija — xulosasi BO'SH yozuvlarda matnga ko'chiriladi.
            // Matn kirillcha: bu yozuvlarning deyarli hammasi Excel
            // kengash arxividan va u yerdagi butun matn kirillcha.
            migrationBuilder.Sql("""
                UPDATE candidate_stage_entries
                   SET conclusion = CASE outcome
                                        WHEN 2 THEN 'Натижа: ўтди'
                                        WHEN 3 THEN 'Натижа: ўтмади'
                                    END
                 WHERE outcome IN (2, 3)
                   AND (conclusion IS NULL OR btrim(conclusion) = '');
                """);

            migrationBuilder.DropColumn(
                name: "outcome",
                table: "candidate_stage_entries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            // Qiymatlar TIKLANMAYDI — hammasi «кутилмоқда» (1) bo'lib
            // qaytadi. Sukut qiymat 1: sanoqda 0 degan qiymat yo'q va u
            // o'qilganda istisno tashlardi.
            migrationBuilder.AddColumn<int>(
                name: "outcome",
                table: "candidate_stage_entries",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }
    }
}
