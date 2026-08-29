using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Vakansiyaning «to'xtatilgan» holatini yopilganga ko'chiradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>SXEMA O'ZGARMAYDI</b> — bu faqat MA'LUMOT migratsiyasi. Holat
    /// bazada butun son bo'lib yotadi (<c>status integer</c>), ya'ni
    /// sanoqdan qiymat olib tashlash jadvalga hech qanday ta'sir
    /// qilmaydi. Aynan shu sababli bunday migratsiya kerak: usiz
    /// <c>status = 2</c> qatorlar bazada qolib ketardi va ular
    /// <c>VacancyStatus</c> ning hech qaysi qiymatiga mos kelmasdi —
    /// ro'yxatda tanilmagan holat, yorliqsiz qator va, eng yomoni,
    /// «ochiq emas, yopiq ham emas» degan javob.
    /// </para>
    /// <para>
    /// <b>NEGA aynan <c>Closed</c> ga.</b> To'xtatilgan vakansiya ham
    /// yangi nomzod qabul qilmasdi — ya'ni tizim uchun u allaqachon
    /// yopiq edi (batafsil sabab <c>VacancyStatus</c> izohida).
    /// <c>Open</c> ga ko'chirish esa HR ning aniq qarorini
    /// («hozircha kerak emas») bekor qilardi va o'sha vakansiyalar
    /// jimgina yana nomzod qabul qila boshlardi.
    /// </para>
    /// <para>
    /// <b>NEGA <c>closed_at</c> ham to'ldiriladi.</b> Yopilgan
    /// vakansiyada yopilish sanasi bo'lishi kerak
    /// (<c>Vacancy.ClosedAt</c>) — aks holda kartada «yopilgan, sanasi
    /// noma'lum» degan yarim javob turardi. To'xtatilgan sana bazada
    /// hech qayerda saqlanmagan, shu sababli ko'chirish KUNI qo'yiladi:
    /// bu yagona rost fakt — vakansiya aynan shu kuni yopiq deb
    /// hisoblana boshladi. <c>GREATEST</c> ochilish sanasidan oldinga
    /// tushib qolishdan himoya qiladi (domendagi
    /// «vakansiya.yopish_sanasi» qoidasi bilan bir xil).
    /// </para>
    /// <para>
    /// <b>Nomzodlar, bosqichlar va takliflar TEGILMAYDI.</b> Yopish
    /// ular haqida emas — u faqat yangi nomzod kiritishni to'sadi. Dev
    /// bazadagi vakansiya ham, uning nomzodlari ham joyida qoladi.
    /// </para>
    /// </remarks>
    public partial class RemoveVacancyPausedStatus : Migration
    {
        /// <summary>«To'xtatilgan» (2) → «Yopilgan» (3).</summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Yumshoq o'chirilgan qatorlar ham ko'chiriladi (`deleted_at`
            // bo'yicha filtr YO'Q): ular tiklanishi mumkin va tiklangan
            // yozuv tanilmagan holat bilan qaytishi kerak emas.
            migrationBuilder.Sql(
                """
                UPDATE vacancies
                SET status = 3,
                    closed_at = COALESCE(closed_at, GREATEST(opened_at, CURRENT_DATE))
                WHERE status = 2;
                """);
        }

        /// <summary>
        /// Ortga qaytarish — ATAYLAB bo'sh.
        /// </summary>
        /// <remarks>
        /// Qaysi vakansiya «to'xtatilgan», qaysisi «yopilgan» bo'lgani
        /// bazada boshqa hech qayerda saqlanmagan: ikkala holat ham
        /// endi bitta raqam. Hammasini 2 ga qaytarish HAQIQATAN yopilgan
        /// vakansiyalarni ham to'xtatilgan qilib qo'yardi — ya'ni
        /// yolg'on ma'lumot yaratardi. Bo'sh <c>Down</c> — sxema
        /// o'zgarmagani uchun xavfsiz: eski kod <c>status = 3</c> ni
        /// «yopilgan» deb o'qiyveradi.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
