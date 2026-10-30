using System.Globalization;
using Bonvi.Domain.Abstractions;

namespace Bonvi.Domain.Notifications;

/// <summary>
/// Bildirishnomaning <b>barqaror</b> identifikatori.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega kalit DETERMINISTIK bo'lishi SHART.</b> Bu loyihada
/// bildirishnomalar bazada saqlanmaydi — ular so'rov kelganda qaytadan
/// hisoblanadi (sababi <c>INotificationReadModel</c> izohida). Ya'ni
/// «bir xil bildirishnoma» degan tushuncha faqat KALIT orqali yashaydi.
/// </para>
/// <para>
/// Agar kalit hisoblash paytida hosil bo'lganda (masalan tasodifiy
/// identifikator yoki hisoblangan vaqt qo'shilsa), har bir so'rov YANGI
/// yozuv yaratardi: kecha o'qilgan «Karimovning sinovi tugayapti»
/// bugun yana o'qilmagan bo'lib chiqardi, o'chirilgani esa qaytib kelardi.
/// Foydalanuvchi bir xil narsani kuniga o'nlab marta ko'rardi va
/// qo'ng'iroqdagi nuqtaga ishonishni butunlay to'xtatardi — bu esa hozirgi
/// «doim yonib turadigan qattiq yozilgan nuqta» dan farq qilmasdi.
/// </para>
/// <para>
/// Shu sababli kalit FAQAT ma'lumotning o'zidan hosil bo'ladi: tur
/// prefiksi + manba yozuvining identifikatori (+ shartni belgilaydigan
/// sana). Bir xil ma'lumotdan bir xil kalit chiqadi — bugun ham,
/// ertaga ham, boshqa serverda ham.
/// </para>
/// <para>
/// <b>Nega kalitga «necha kun qoldi» QO'SHILMAYDI.</b> U har kuni
/// o'zgaradi va kalitni har kuni yangilab yuborardi — ya'ni aynan
/// yuqoridagi muammoni qaytarardi. Qolgan kunlar PARAMETR bo'lib
/// javobda ketadi (matn shundan yig'iladi), kalitda esa shartning
/// O'ZGARMAS asosi turadi: qaysi sinov, qaysi tugash sanasi.
/// </para>
/// <para>
/// <b>Nega TUGASH SANASI kalitda bor.</b> HR sinov muddatini uzaytirsa
/// (<c>Probation.Extend</c>), bu YANGI holat: eski ogohlantirish endi
/// noto'g'ri va uning «o'qildi» belgisi yangi muddatga o'tmasligi kerak.
/// Sana kalitda turgani uchun uzaytirishdan keyin yangi kalit hosil
/// bo'ladi va bildirishnoma qaytadan ko'rinadi. Sana kalitda bo'lmasa,
/// bir marta o'qilgan foydalanuvchi uzaytirilgan muddat haqida boshqa
/// hech qachon xabar olmasdi.
/// </para>
/// </remarks>
public static class NotificationKey
{
    /// <summary>
    /// Kalitning eng katta uzunligi — baza ustuni va so'rov tekshiruvi shu bo'yicha.
    /// </summary>
    /// <remarks>
    /// Eng uzun kalit — prefiks (~20) + GUID (36) + sana (10) + ikkita
    /// ajratgich, ya'ni 70 belgi atrofida. 200 — ikki barobardan ortiq
    /// zaxira bilan qo'yilgan chegara: uning vazifasi «yetadimi» emas,
    /// mijozdan kelgan uzun-uzun satrlar bazaga tushmasligi.
    /// </remarks>
    public const int MaxLength = 200;

    /// <summary>Bo'laklarni ajratuvchi belgi.</summary>
    /// <remarks>
    /// Ikki nuqta tanlangan, chunki u prefikslarda ham
    /// (<c>sinov.tugayapti</c>), GUID'da ham, ISO sanada ham
    /// (<c>2026-08-30</c>) uchramaydi — ya'ni bo'lish har doim aniq.
    /// </remarks>
    public const char Separator = ':';

    /// <summary>
    /// Turlarning kalit prefikslari.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Prefikslar xato kodlari bilan bir uslubda — o'zbekcha, nuqta bilan
    /// («<c>sinov.tugayapti</c>»). Sabab: bu satrlar loglarda va bazada
    /// ochiq yotadi va ularni o'qiydigan odam aynan shu tilda o'ylaydi.
    /// </para>
    /// <para>
    /// ⚠️ Prefikslar bir-birining boshlanishi bo'lishi MUMKIN (masalan
    /// <c>sinov.tugayapti</c> yoniga <c>sinov.tugayapti_yana</c>
    /// qo'shilsa) — bu xavfsiz, chunki tur aniqlanayotganda kalit AVVAL
    /// <see cref="Separator"/> bo'yicha bo'linadi va birinchi bo'lak
    /// TO'LIQ solishtiriladi, <c>StartsWith</c> bilan emas.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<NotificationType, string> Prefixes =
        new()
        {
            [NotificationType.ProbationEndingSoon] = "sinov.tugayapti",
            [NotificationType.ProbationEndsToday] = "sinov.bugun_tugaydi",
            [NotificationType.ProbationOverdue] = "sinov.otib_ketgan",
            [NotificationType.ProbationMissing] = "sinov.ochilmagan",
            [NotificationType.BirthdayToday] = "tugilgan_kun.bugun",
            [NotificationType.BirthdayTomorrow] = "tugilgan_kun.ertaga",
            [NotificationType.CandidateNew] = "nomzod.yangi",
            [NotificationType.CandidateFromBot] = "nomzod.botdan",
            [NotificationType.CandidateContactDue] = "nomzod.aloqa_bugun",
            [NotificationType.CandidateContactOverdue] = "nomzod.aloqa_kechikkan",
            [NotificationType.CandidateInterviewSoon] = "nomzod.suhbat_yaqin",
            [NotificationType.CandidateReapplied] = "nomzod.anketa_toldirdi",
        };

    /// <summary>Prefiksdan turga — <see cref="TypeOf"/> uchun teskari jadval.</summary>
    private static readonly Dictionary<string, NotificationType> Types =
        Prefixes.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    /// <summary>Turning kalit prefiksi.</summary>
    public static string Prefix(NotificationType type) => Prefixes.TryGetValue(type, out var prefix)
        ? prefix
        : throw new ArgumentOutOfRangeException(
            nameof(type),
            type,
            "Bildirishnoma turi uchun kalit prefiksi belgilanmagan.");

    /// <summary>Sanaga bog'liq bo'lmagan kalit: <c>nomzod.yangi:{id}</c>.</summary>
    public static string For(NotificationType type, Guid id)
        => string.Concat(Prefix(type), Separator, id.ToString("D", CultureInfo.InvariantCulture));

    /// <summary>
    /// Sana bilan bog'langan kalit: <c>tugilgan_kun.bugun:{id}:2026-08-30</c>.
    /// </summary>
    /// <remarks>
    /// Sana ISO ko'rinishida (<c>yyyy-MM-dd</c>) va
    /// <see cref="CultureInfo.InvariantCulture"/> bilan yoziladi: kalit
    /// serverning til sozlamasiga bog'liq bo'lib qolsa, muhit
    /// o'zgarganda hamma «o'qildi» belgisi yo'qolardi.
    /// </remarks>
    public static string For(NotificationType type, Guid id, DateOnly date)
        => string.Concat(
            Prefix(type),
            Separator,
            id.ToString("D", CultureInfo.InvariantCulture),
            Separator,
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    /// <summary>
    /// LAHZA bilan bog'langan kalit: <c>nomzod.suhbat_yaqin:{id}:2026-09-10T14:30</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega sana yetmaydi.</b> Yuqoridagi qoida bo'yicha kalitda
    /// shartning O'ZGARMAS asosi turadi va u o'zgarsa — bildirishnoma
    /// qaytadan ko'rinadi (sinov muddati uzaytirilgandagi bilan bir xil
    /// mulohaza). Suhbatda esa o'zgaradigan narsa KUN emas, VAQT: HR
    /// suhbatni soat 10:00 dan 15:00 ga surganda nomzod yangi xabar
    /// oladi (<c>Candidate.InviteToInterview</c>), HR ning o'zi esa
    /// ertalab «o'qildi» qilgan yozuvi tufayli yangi vaqt haqida
    /// ogohlantirishni UMUMAN olmasdi — kun o'zgarmagani uchun kalit
    /// ham o'zgarmasdi.
    /// </para>
    /// <para>
    /// <b>Aniqlik — DAQIQA.</b> Sekund kesiladi: suhbat vaqti ekranda
    /// daqiqagacha qo'yiladi va kalitga sekund qo'shish uni
    /// ma'lumotning ko'rinmaydigan qismiga bog'lab qo'yardi.
    /// </para>
    /// <para>
    /// Vaqt TOSHKENT mintaqasida yoziladi (<see cref="BonviTime"/>):
    /// kalit loglarda va bazada ochiq yotadi, uni o'qiydigan odam esa
    /// ekrandagi vaqt bilan solishtiradi. Siljish qat'iy son bo'lgani
    /// uchun natija baribir deterministik — bir xil lahzadan har doim
    /// bir xil kalit chiqadi.
    /// </para>
    /// </remarks>
    public static string For(NotificationType type, Guid id, DateTimeOffset moment)
        => string.Concat(
            Prefix(type),
            Separator,
            id.ToString("D", CultureInfo.InvariantCulture),
            Separator,
            moment.ToTashkent().ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture));

    /// <summary>
    /// Kalitning turi. Noma'lum prefiks — <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Mijoz «o'qildi»/«o'chirildi» so'rovida ISTALGAN satrni yubora
    /// oladi. Tekshiruvsiz ular to'g'ridan-to'g'ri bazaga tushardi va
    /// jadval hech qachon ishlatilmaydigan qatorlar bilan to'lardi.
    /// Shu sababli har bir kalit AVVAL shu yerdan o'tadi.
    /// </remarks>
    public static NotificationType? TypeOf(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxLength)
        {
            return null;
        }

        var separator = key.IndexOf(Separator, StringComparison.Ordinal);
        if (separator <= 0)
        {
            return null;
        }

        return Types.TryGetValue(key[..separator], out var type) ? type : null;
    }

    /// <summary>Kalit tanish turga tegishlimi.</summary>
    public static bool IsValid(string? key) => TypeOf(key) is not null;
}
