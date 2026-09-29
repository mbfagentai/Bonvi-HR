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
/// ⚠️ <b>Nega uni yopib ham, o'chirib ham bo'lmaydi (2026-09-29).</b>
/// Botdagi «📝 Anketa to'ldirish» tugmasi arizani aynan shu vakansiyaga
/// yuboradi. Prod'da u 2026-09-11 da HR tomonidan yopilgan va 18 kun
/// davomida shu tugma orqali kelgan HAR BIR nomzod 22 ta savoldan keyin
/// «ariza yuborilmadi» ni olgan (jurnalda 5 kunda 67 marta). Ekranda esa
/// u oddiy vakansiyadek ko'rinardi — uni yopish oqibatini hech kim
/// bilmagan. Endi server buni rad etadi (<see cref="RecruitingErrors.SystemVacancyLocked"/>),
/// ekran esa almashtirgich o'rniga qulf ko'rsatadi.
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
