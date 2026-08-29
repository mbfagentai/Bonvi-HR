using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// «Bir vaqtda atigi bitta kuchdagi belgi» qoidasini MAVJUD ma'lumotga
    /// qo'llaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nima uchun kerak.</b> Qoida bugundan boshlab use-case qatlamida
    /// qat'iy tekshiriladi (<c>RaiseFlagCommandHandler</c>), lekin u faqat
    /// YANGI yozuvlarga ta'sir qiladi. Bazada esa allaqachon bir vaqtda
    /// ikkita kuchdagi belgi olgan xodimlar bor edi — ular tekshiruvsiz
    /// yozilgan. Ular tegilmasa, karta va ro'yxatlar tizim ta'qiqlaydigan
    /// holatni ko'rsatib turaverardi.
    /// </para>
    /// <para>
    /// <b>Nima qilinadi.</b> Har bir xodimda ENG SO'NGGI belgi qoldiriladi
    /// (boshlanish sanasi bo'yicha, teng bo'lsa kiritilgan vaqti bo'yicha),
    /// qolganlari esa YOPILADI — o'chirilmaydi. Sabab matniga izoh
    /// qo'shiladi, shunda HR kartani ochganda belgi nima uchun yopilganini
    /// ko'radi va uni «kimdir qo'lda o'chirib yuborgan» deb o'ylamaydi.
    /// Bu <c>ISoftDeletable</c> falsafasi bilan bir xil: HR ma'lumoti
    /// tarixiy hujjat, undan hech narsa jimgina yo'qolmaydi.
    /// </para>
    /// <para>
    /// <b>Yopilish sanasi</b> — qoladigan belgining boshlanish sanasi
    /// (ya'ni «yangisi kuchga kirgan kuni eskisi tugadi»), lekin belgining
    /// O'Z boshlanishidan oldin emas: <c>GREATEST</c> aynan shuning uchun.
    /// Aks holda kelajakdagi sana bilan kiritilgan belgi «boshlanmasdan
    /// tugagan» bo'lib qolardi.
    /// </para>
    /// <para>
    /// <b>Shart <c>end_date IS NULL OR end_date &gt; CURRENT_DATE</c>.</b>
    /// Faqat muddatsizlar emas: uch oylik nazorat kabi MUDDATLI belgi ham
    /// hozir kuchda va u ham qoidaga tushadi. Allaqachon tugagan belgilar
    /// esa hech kimga xalaqit bermaydi va tegilmaydi.
    /// </para>
    /// </remarks>
    public partial class CloseExtraActiveFlags : Migration
    {
        /// <summary>Migratsiya qo'shadigan izoh matni — sababga qo'shiladi.</summary>
        private const string Izoh =
            "boshqa belgi bilan almashtirildi (bir vaqtda atigi bitta belgi qoidasi)";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql($"""
                WITH kuchdagilar AS (
                    SELECT
                        id,
                        start_date,
                        row_number() OVER (
                            PARTITION BY employee_id
                            ORDER BY start_date DESC, created_at DESC, id DESC
                        ) AS orin,
                        first_value(start_date) OVER (
                            PARTITION BY employee_id
                            ORDER BY start_date DESC, created_at DESC, id DESC
                        ) AS qoladigan_boshlanish
                    FROM employee_flags
                    WHERE end_date IS NULL OR end_date > CURRENT_DATE
                )
                UPDATE employee_flags AS f
                SET
                    end_date = GREATEST(k.qoladigan_boshlanish, f.start_date),
                    reason = CASE
                        WHEN f.reason IS NULL OR btrim(f.reason) = '' THEN '{Izoh}'
                        ELSE f.reason || '; ' || '{Izoh}'
                    END,
                    updated_at = now()
                FROM kuchdagilar AS k
                WHERE f.id = k.id AND k.orin > 1;
                """);
        }

        /// <summary>
        /// Orqaga qaytarish YO'Q — va bu ataylab.
        /// </summary>
        /// <remarks>
        /// Migratsiya <c>end_date</c> va <c>reason</c> ni o'zgartiradi, eski
        /// qiymatlarni esa hech qayerda saqlamaydi (ular <c>NULL</c> edi).
        /// Ularni «tiklash» degani — bazadagi HAR QANDAY yopilgan belgini
        /// qaytadan ochish bo'lardi, shu jumladan HR o'z qo'li bilan
        /// yopganlarini ham. Bo'sh <c>Down</c> ma'lumotni buzadigan
        /// <c>Down</c> dan xavfsizroq.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ataylab bo'sh — sababi yuqoridagi izohda.
        }
    }
}
