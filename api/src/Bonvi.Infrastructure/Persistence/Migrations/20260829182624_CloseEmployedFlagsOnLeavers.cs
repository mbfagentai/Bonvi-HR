using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Ishdan ketgan xodimlarda ochiq qolgan «nazoratda» va «qobiliyatli»
    /// belgilarini yopadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nima uchun kerak.</b> Belgi bosqichi qoidasi
    /// (<c>EmployeeFlagStage</c>) bugundan boshlab qat'iy tekshiriladi va
    /// bo'shatish bunday belgilarni o'zi yopadi
    /// (<c>TerminateEmploymentCommandHandler</c>), lekin ikkalasi ham
    /// faqat YANGI amallarga ta'sir qiladi. Bazadagi eski yozuvlar
    /// tegilmasa, «Nazoratdagilar» ro'yxatida ishdan ketgan odamlar
    /// turaverardi — hech kim bajarmaydigan vazifa.
    /// </para>
    /// <para>
    /// <b>Faqat BIR yo'nalish.</b> Teskarisi — ishlab turgan odamdagi
    /// «rezerv»/«qora ro'yxat» — ATAYLAB tegilmaydi. Bunday nomuvofiqlik
    /// deyarli har doim mehnat munosabati ma'lumotining to'liq emasligini
    /// bildiradi (odam ketgan, lekin buyruq yozilmagan). Qora ro'yxatni
    /// jimgina yopish esa qayta ishga olish to'sig'ini o'chirib qo'yardi —
    /// bu tozalashda qilinishi mumkin bo'lgan eng qimmat xato. Bunday
    /// yozuvlar ko'chirish hisobotida ko'rsatiladi va HR ularni o'zi hal
    /// qiladi (<c>FlagsStep.Reconcile</c> dagi bir xil mantiq).
    /// </para>
    /// <para>
    /// <b>Yopilish sanasi</b> — xodimning eng oxirgi ketish sanasi, lekin
    /// belgining O'Z boshlanishidan oldin emas (<c>GREATEST</c>): aks
    /// holda keyinroq qo'yilgan belgi «boshlanmasdan tugagan» bo'lib
    /// qolardi. Ketish sanasi umuman noma'lum bo'lsa (Excel'dan
    /// ko'chirilgan yozuvlarda uchraydi) — bugungi kun.
    /// </para>
    /// <para>
    /// Yozuv O'CHIRILMAYDI, sababga izoh qo'shiladi — HR kartani ochganda
    /// belgi nima uchun yopilganini ko'radi (<c>CloseExtraActiveFlags</c>
    /// bilan bir xil falsafa: HR ma'lumotidan hech narsa jimgina
    /// yo'qolmaydi).
    /// </para>
    /// </remarks>
    public partial class CloseEmployedFlagsOnLeavers : Migration
    {
        /// <summary>Sababga qo'shiladigan izoh.</summary>
        private const string Izoh = "xodim ishdan ketgan (belgi bosqichi qoidasi)";

        /// <summary>Ishlab turganlar uchun belgilar: 3 — nazorat, 4 — qobiliyatli.</summary>
        private const string EmployedTypes = "(3, 4)";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql($"""
                WITH ketganlar AS (
                    SELECT
                        f.id,
                        GREATEST(
                            f.start_date,
                            COALESCE(
                                (SELECT max(e.termination_date)
                                 FROM employments e
                                 WHERE e.employee_id = f.employee_id
                                   AND e.deleted_at IS NULL),
                                CURRENT_DATE)) AS yopiladi
                    FROM employee_flags f
                    WHERE f.type IN {EmployedTypes}
                      AND (f.end_date IS NULL OR f.end_date > CURRENT_DATE)
                      -- Bironta ham ochiq munosabati yo'q: 3 — «ishdan ketgan».
                      AND NOT EXISTS (
                          SELECT 1 FROM employments e
                          WHERE e.employee_id = f.employee_id
                            AND e.status <> 3
                            AND e.deleted_at IS NULL)
                )
                UPDATE employee_flags AS f
                SET end_date = k.yopiladi,
                    reason = CASE
                        WHEN f.reason IS NULL OR btrim(f.reason) = '' THEN '{Izoh}'
                        ELSE f.reason || '; {Izoh}'
                    END,
                    updated_at = now()
                FROM ketganlar AS k
                WHERE f.id = k.id;
                """);
        }

        /// <summary>
        /// Orqaga qaytarish YO'Q — ataylab.
        /// </summary>
        /// <remarks>
        /// Qaysi belgi aynan shu migratsiya tomonidan yopilganini ajratib
        /// bo'lmaydi: <c>end_date</c> qo'lda ham qo'yilgan bo'lishi mumkin.
        /// «Hammasini qaytadan ochish» esa qoidani buzgan holatni tiklardi.
        /// Sabab matnidagi izoh — nima bo'lganini o'qish uchun yetarli
        /// (<c>CloseExtraActiveFlags</c> bilan bir xil qaror).
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ataylab bo'sh — yuqoridagi izohga qarang.
        }
    }
}
