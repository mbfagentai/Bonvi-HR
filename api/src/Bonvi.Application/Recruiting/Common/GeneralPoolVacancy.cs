using Bonvi.Application.Settings;
using Bonvi.Domain.Settings;

namespace Bonvi.Application.Recruiting.Common;

/// <summary>
/// «Umumiy anketa» — vakansiyasiz kelgan anketalar tushadigan TIZIM
/// vakansiyasi (izohi <c>SystemSettingKeys.GeneralPoolVacancyId</c> da).
/// </summary>
/// <remarks>
/// <para>
/// Sozlamani o'qiydigan joylar ko'paydi (ro'yxat, yopish, o'chirish,
/// DTO), shu sababli identifikator BITTA joyda olinadi.
/// </para>
/// <para>
/// <b>Yopish va o'chirish (egasining qarori, 2026-10-05).</b> «Umumiy anketa»
/// boshqa vakansiyalar kabi YOPILADI va qayta ochiladi: yopiq bo'lsa botdagi
/// «📋 Anketa to'ldirish» orqali ariza ham YOPIQ — qaysi vakansiya yopiq
/// bo'lsa, unga anketa topshirib bo'lmaydi. O'chirish esa hali ham taqiqlangan
/// (<see cref="RecruitingErrors.SystemVacancyLocked"/>): sozlama o'chirilgan
/// yozuvni ko'rsatib qolardi.
/// </para>
/// <para>
/// ⚠️ Tarix (2026-09-29 qarori, endi bekor): prod'da HR uni 2026-09-11 da
/// yopgan va 18 kun davomida bot nomzodga buni 22 ta savoldan KEYIN «ariza
/// yuborilmadi» deb aytgan — 67 ta ariza yo'qolgan. Shu sababli yopish bir
/// muddat taqiqlangan edi. Endi qoida — jim yo'qotish yo'q: bot anketa yopiq
/// ekanini «Anketa to'ldirish» bosilishi bilanoq aytadi
/// (<c>GET /api/recruiting/vacancies/general-pool</c>), yuborish paytida
/// yopilib qolgan bo'lsa ham aniq sabab bilan javob beradi.
/// </para>
/// </remarks>
public static class GeneralPoolVacancy
{
    /// <summary>Tizim vakansiyasining identifikatori. Sozlanmagan bo'lsa — <c>null</c>.</summary>
    /// <remarks>
    /// Bu yerdan XATO chiqmaydi: sozlama bo'sh yoki buzilgan bo'lsa ham
    /// chaqiruvchilar avvalgidek ishlaydi — faqat himoya ishlamaydi.
    /// </remarks>
    public static async Task<Guid?> IdAsync(SystemSettingsReader settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var values = await settings.ReadAsync(cancellationToken).ConfigureAwait(false);

        return Guid.TryParse(values.Hidden(SystemSettingKeys.GeneralPoolVacancyId), out var id) ? id : null;
    }
}
