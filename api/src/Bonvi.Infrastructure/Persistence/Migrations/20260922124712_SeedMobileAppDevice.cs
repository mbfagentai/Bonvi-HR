using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bonvi.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// «Mobil ilova» — telefon belgilarining sintetik qurilma qatori
    /// (SPEC-xodim-ilovasi §D5, §K4).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega bu qator umuman kerak.</b> <c>attendance_passes.device_id</c>
    /// — <c>NOT NULL</c>, telefon belgisi esa o'tish bo'lib yoziladi. Ustunni
    /// nullable qilish o'nlab o'qish yo'liga tarqalardi va eng yomoni
    /// <c>ux_attendance_passes_device_dedupe</c> ni buzardi: PostgreSQL
    /// unikal indeksda NULL larni bir-biridan FARQLI deb hisoblaydi, ya'ni
    /// modulning eng muhim indeksi telefon belgilarini jimgina takrorlashga
    /// ruxsat berib qo'yardi.
    /// </para>
    /// <para>
    /// ⭐ <b>Qator FAOLSIZ va tokeni o'lik.</b> Ikkalasi ham haqiqat, hiyla
    /// emas: <c>is_active</c> «bu qurilmaning tokeni qabul qilinadi» degani
    /// va bu qatorning tokeni hech qachon qabul qilinmaydi. Buning ikkita
    /// yon foydasi bor — qabul endpointi ham, agent ro'yxati ham hech qanday
    /// yangi kodsiz yopiq qoladi (tasdiqlovchi faolsiz qurilmani birinchi
    /// bo'lib rad etadi), va xodim kartasi FAQAT faol qurilmalarni chizgani
    /// uchun 950 ta kartada hech qachon bajarilmaydigan «Mobil ilova —
    /// Rasm yo'q» qatori paydo bo'lmaydi.
    /// </para>
    /// <para>
    /// ⚠️ <c>token_hash</c> — 32 ta NOL bayt. Tasdiqlovchi qurilmani
    /// <c>SHA256(kelgan token)</c> bo'yicha qidiradi; butunlay nol natija
    /// beradigan satr amalda mavjud emas, ya'ni qator tasdiqdan o'ta
    /// olmaydi. Bu <c>pgcrypto</c> ham, hech qayerda saqlanadigan sir ham
    /// talab qilmaydi. Uni «tuzatib» aylantirish ilovaga ISHLAYDIGAN qabul
    /// tokeni berardi — SPEC aynan shuni taqiqlaydi.
    /// </para>
    /// <para>
    /// <c>ON CONFLICT DO NOTHING</c> — qayta yurgizish, tiklangan zaxira
    /// yoki test fixture'i takror kalit xatosi bermasin.
    /// </para>
    /// </remarks>
    public partial class SeedMobileAppDevice : Migration
    {
        /// <summary>«Mobil ilova» qatorining qat'iy identifikatori.</summary>
        /// <remarks>
        /// ⚠️ <c>AttendanceDevice.MobileAppId</c> bilan AYNAN bir xil bo'lishi
        /// SHART. Domen konstantasi bu yerdan chaqirilmadi va bu ataylab:
        /// migratsiya BAJARILGAN paytdagi kodni emas, YOZILGAN paytdagi
        /// haqiqatni saqlashi kerak. Konstanta bir kun o'zgarsa, allaqachon
        /// qo'llangan migratsiya boshqa qatorni yaratgan bo'lib qolardi.
        /// </remarks>
        private const string MobileAppId = "01996f00-0000-7000-8000-00000000b071";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.Sql(
                $"""
                INSERT INTO attendance_devices (
                    id, name, provider, identity_provider, direction_role, auth_mode,
                    token_hash, token_prefix, token_issued_at, is_active, created_at)
                VALUES (
                    '{MobileAppId}',
                    'Mobil ilova',

                    -- AttendanceProvider.Mobile: tahlilchisi YO'Q qurilma.
                    -- `Generic` deb belgilash jurnalda har bir telefon
                    -- belgisiga «Generic» chipini chiqarardi.
                    3,

                    -- AttendanceIdentityProvider.Mobile: `raw_person_id`
                    -- da xodimning o'z identifikatori turadi.
                    5,

                    -- DeviceDirectionRole.ReportsDirection: yo'nalish
                    -- hodisaning o'zida keladi — uni odam aytadi.
                    1,

                    -- DeviceAuthMode.Token: ishlatilmaydi, bu qator hech
                    -- qachon tasdiqlanmaydi.
                    1,

                    -- 32 bayt nol (sinf izohi).
                    '\x0000000000000000000000000000000000000000000000000000000000000000'::bytea,
                    '00000000',
                    now(),

                    -- FAOLSIZ — bu haqiqat, hiyla emas (sinf izohi).
                    false,
                    now())
                ON CONFLICT (id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            /*
              ⚠️ ORQAGA QAYTARISH — telefon belgisi yozilgan bazada bu
              qator O'CHMAYDI va o'chmasligi ham kerak:
              `attendance_passes.device_id` unga `RESTRICT` bilan ishora
              qiladi, ya'ni oddiy `DELETE` tashqi kalit xatosi bilan
              yiqilib, butun qaytarishni to'xtatib qo'yardi.

              `NOT EXISTS` sharti buni TOZA hech-narsa-qilmaslikka
              aylantiradi: o'tishlari bor bo'lsa qator joyida qoladi
              (u yerda kerak), bo'lmasa o'chadi.
            */
            migrationBuilder.Sql(
                $"""
                DELETE FROM attendance_devices
                WHERE id = '{MobileAppId}'
                  AND NOT EXISTS (
                      SELECT 1 FROM attendance_passes WHERE device_id = '{MobileAppId}');
                """);
        }
    }
}
