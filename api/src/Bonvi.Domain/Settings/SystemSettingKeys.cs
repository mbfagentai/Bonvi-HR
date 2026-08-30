using System.Globalization;
using Bonvi.Domain.Common;
using Bonvi.Domain.Notifications;

namespace Bonvi.Domain.Settings;

/// <summary>Sozlama qiymatining turi.</summary>
/// <remarks>
/// <para>
/// Bazada HAMMA qiymat matn bo'lib yotadi (<see cref="SystemSetting.Value"/>),
/// tur esa faqat SHU ro'yxatda. Nega ustun emas: tur sozlamaning
/// TA'RIFIGA tegishli, ma'lumotga emas. Ustun bo'lganda bazadagi
/// qator «bu son» deb turgani holda kod uni matn deb o'qiy olardi va
/// ikkalasi ham «to'g'ri» bo'lardi.
/// </para>
/// <para>
/// Turlar ATIGI IKKITA va bu ataylab. Sana, bayroq (ha/yo'q) yoki
/// ro'yxat qo'shilsa, har biri o'z tekshiruvi, o'z formati va
/// frontendda o'z maydonini talab qilardi — hozircha bunday sozlama
/// so'ralmagan. Kerak bo'lganda qo'shish oson: bitta qiymat va bitta
/// <c>switch</c> tarmog'i.
/// </para>
/// </remarks>
public enum SystemSettingKind
{
    /// <summary>Erkin matn (bo'sh bo'lishi mumkin).</summary>
    Text = 1,

    /// <summary>Butun son — har doim <see cref="SystemSettingDefinition.Min"/>…<see cref="SystemSettingDefinition.Max"/> oralig'ida.</summary>
    Number = 2,
}

/// <summary>
/// Bitta sozlamaning to'liq ta'rifi: kaliti, turi, sukut qiymati va chegarasi.
/// </summary>
/// <param name="Key">Barqaror kalit — API'da ham, bazada ham SHU satr.</param>
/// <param name="Kind">Qiymat turi.</param>
/// <param name="DefaultValue">
/// Sozlama YOZILMAGAN bo'lsa ishlatiladigan qiymat — matn ko'rinishida
/// (son ham matn bo'lib turadi, chunki bazada ham shunday).
/// </param>
/// <param name="Min">Sonning eng kichik qiymati. Matn uchun <c>null</c>.</param>
/// <param name="Max">Sonning eng katta qiymati. Matn uchun <c>null</c>.</param>
/// <param name="MaxLength">Matnning eng katta uzunligi. Son uchun <c>null</c>.</param>
public sealed record SystemSettingDefinition(
    string Key,
    SystemSettingKind Kind,
    string DefaultValue,
    int? Min = null,
    int? Max = null,
    int? MaxLength = null)
{
    /// <summary>
    /// Kelgan qiymatni tekshiradi va tozalangan holida qaytaradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Tekshiruvning YAGONA joyi.</b> Uni domen agregati
    /// (<see cref="SystemSetting.SetValue"/>) chaqiradi, ya'ni use-case
    /// qatlami o'z tekshiruvini yozmaydi va yoza olmaydi ham — bazaga
    /// yozish yo'li faqat shu yerdan o'tadi.
    /// </para>
    /// <para>
    /// Matn uchun <c>null</c> — BO'SH SATR, xato emas. Bot matnini
    /// o'chirish mumkin bo'lishi kerak: bo'sh matnda bot «Kompaniya
    /// haqida» tugmasini umuman chizmaydi (<c>BotOptions.AboutCyrillic</c>
    /// izohi). Son uchun esa <c>null</c> — xato: «sonni o'chirish» degan
    /// ma'no yo'q, uning o'rniga sukut qiymat qaytariladi va buning
    /// uchun yozuvni O'CHIRISH kerak.
    /// </para>
    /// </remarks>
    public Result<string> Normalize(string? value)
    {
        if (Kind == SystemSettingKind.Text)
        {
            // Chetdagi bo'shliqlar olib tashlanadi: HR matnni Word'dan
            // nusxa qilganda oxirida ko'rinmas bo'shliq va yangi qator
            // qolib ketadi, bot esa uni xabar oxirida bo'sh qator qilib
            // chizardi.
            var text = value?.Trim() ?? string.Empty;

            return MaxLength is { } limit && text.Length > limit
                ? SystemSettingErrors.TextTooLong(limit)
                : Result.Success(text);
        }

        // Son: `InvariantCulture` — chunki qiymat BAZADA ham shu ko'rinishda
        // yotadi. Foydalanuvchi tiliga bog'liq ajratgich («1 440») bu yerda
        // qabul qilinmaydi: bir muhitda yozilgan qiymat boshqasida
        // o'qilmay qolardi.
        if (!int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return SystemSettingErrors.NotANumber;
        }

        var min = Min ?? int.MinValue;
        var max = Max ?? int.MaxValue;

        return number < min || number > max
            ? SystemSettingErrors.OutOfRange(min, max)
            : Result.Success(number.ToString(CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Tizim sozlamalarining YAGONA ro'yxati: kalit, tur, sukut qiymat, chegara.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Bu ro'yxat — yagona manba.</b> Bu yerda yo'q kalit hech qayerdan
/// o'tmaydi: uni na yozib bo'ladi (<see cref="SystemSetting.Create"/> rad
/// etadi), na o'qib bo'ladi (o'qish faqat shu ro'yxat bo'yicha ketadi).
/// Sabab oddiy: sozlama — bu KOD bilan kelishuv. «bot.about.latin» ni
/// kimdir «bot.aboutLatin» qilib yozib qo'ysa, bot matnini topa olmasdi
/// va hech qanday xato ham chiqmasdi — u jimgina bo'sh matn ko'rsatardi.
/// </para>
/// <para>
/// <b>Nega sukut qiymatlar SHU YERDA, bazada emas.</b> Yangi o'rnatilgan
/// tizimda sozlamalar jadvali BO'SH bo'ladi va «Sozlamalar» ekrani hech
/// qachon ochilmagan bo'lishi mumkin. Tizim shunda ham ishlashi kerak:
/// bildirishnomalar kelaveradi, bot suhbatni unutish muddatini biladi.
/// Sukut qiymat urug'lantirish (seed) bilan bazaga yozilganda esa
/// migratsiyadan keyin ham yangilanmay qolgan qatorlar paydo bo'lardi.
/// </para>
/// <para>
/// <b>Nega bildirishnoma chegaralari <see cref="NotificationRules"/> dan
/// olinadi.</b> U yerdagi konstanta YO'QOLMADI — endi u SUKUT qiymat.
/// Bu yerda yangi son yozilsa, ikkita «7» paydo bo'lardi va ular bir kun
/// ajralib ketardi (aynan shu xavf <see cref="NotificationRules"/> izohida
/// tasvirlangan).
/// </para>
/// </remarks>
public static class SystemSettingKeys
{
    // ────────────────────────────────────────────────────────────
    //  BOT — Telegram anketa boti o'qiydigan sozlamalar
    // ────────────────────────────────────────────────────────────

    /// <summary>«Kompaniya haqida» matni — kirill alifbosida.</summary>
    public const string BotAboutCyrillic = "bot.about.cyrillic";

    /// <summary>«Kompaniya haqida» matni — lotin alifbosida.</summary>
    public const string BotAboutLatin = "bot.about.latin";

    /// <summary>Yarim qolgan suhbat qancha vaqtdan keyin unutiladi (daqiqa).</summary>
    public const string BotConversationTimeoutMinutes = "bot.conversationTimeoutMinutes";

    // ────────────────────────────────────────────────────────────
    //  BILDIRISHNOMALAR — qachon ogohlantirish paydo bo'ladi
    // ────────────────────────────────────────────────────────────

    /// <summary>Sinov muddati tugashiga necha kun qolganda ogohlantiriladi.</summary>
    public const string ProbationWarningDays = "notifications.probationWarningDays";

    /// <summary>Nomzod necha kun davomida «yangi» hisoblanadi.</summary>
    public const string NewCandidateDays = "notifications.newCandidateDays";

    /// <summary>Ish taklifi muddati tugashiga necha kun qolganda ogohlantiriladi.</summary>
    public const string OfferWarningDays = "notifications.offerWarningDays";

    /// <summary>Matnli sozlamaning eng katta uzunligi.</summary>
    /// <remarks>
    /// Telegram bitta xabarda 4096 belgigacha ruxsat beradi va bot matni
    /// aynan bitta xabar bo'lib ketadi. 4000 — shu chegaraga tayanadi va
    /// oxiriga qo'shiladigan «AfterAbout» matni uchun joy qoldiradi.
    /// </remarks>
    public const int MaxTextLength = 4000;

    /// <summary>
    /// Kun bilan o'lchanadigan bildirishnoma chegaralarining eng katta qiymati.
    /// </summary>
    /// <remarks>
    /// 90 kun — uch oy. Undan uzoq ogohlantirish ma'nosini yo'qotadi:
    /// ro'yxat hech qachon bo'shamaydi va HR unga qarashni to'xtatadi.
    /// </remarks>
    public const int MaxDays = 90;

    /// <summary>
    /// Barcha sozlamalar — E'LON TARTIBIDA.
    /// </summary>
    /// <remarks>
    /// Tartib ATAYLAB alifbo bo'yicha emas: ekranda sozlamalar shu
    /// ketma-ketlikda chiziladi va u ma'noga qarab guruhlangan (avval
    /// bot, keyin bildirishnomalar). Alifbo tartibida «bot.about.latin»
    /// bilan «notifications.newCandidateDays» aralashib ketardi.
    /// </remarks>
    public static IReadOnlyList<SystemSettingDefinition> All { get; } =
    [
        new(BotAboutCyrillic, SystemSettingKind.Text, string.Empty, MaxLength: MaxTextLength),
        new(BotAboutLatin, SystemSettingKind.Text, string.Empty, MaxLength: MaxTextLength),
        new(
            BotConversationTimeoutMinutes,
            SystemSettingKind.Number,
            // Sukut qiymat `BotOptions.ConversationTimeoutMinutes` bilan
            // AYNAN bir xil: bot sozlamani o'qiy olmaganda (API hali
            // ko'tarilmagan) o'z sukut qiymatiga tushadi va ikkalasi
            // farq qilsa, botning xatti-harakati ishga tushish paytiga
            // qarab o'zgarardi.
            Number(60),
            Min: 1,
            // 1440 daqiqa — bir sutka. Undan uzoq «yarim qolgan suhbat»
            // ma'nosiz: nomzod ertasiga kelib, kechagi savolga javob
            // yozganini o'zi ham eslamaydi.
            Max: 1440),

        new(
            ProbationWarningDays,
            SystemSettingKind.Number,
            Number(NotificationRules.ProbationWarningDays),
            Min: 1,
            Max: MaxDays),
        new(
            NewCandidateDays,
            SystemSettingKind.Number,
            Number(NotificationRules.NewCandidateDays),
            Min: 1,
            Max: MaxDays),
        new(
            OfferWarningDays,
            SystemSettingKind.Number,
            Number(NotificationRules.OfferWarningDays),
            Min: 1,
            Max: MaxDays),
    ];

    /*
      «Bot kalitlari» degan ALOHIDA ro'yxat ATAYLAB yo'q.

      Botga sozlamalar kalit-qiymat bo'lib emas, TIPLASHGAN uchta maydon
      bo'lib beriladi (`BotSettingsDto`) — shu sababli prefiks bo'yicha
      ajratilgan ro'yxat hech qayerda ishlatilmasdi. Bo'lsa ham, u
      «yangi bot. sozlamasi botga o'zi yetib boradi» degan noto'g'ri
      taassurot qoldirardi: yangi maydon baribir DTO ga qo'lda
      qo'shiladi.
    */

    /// <summary>Kalit bo'yicha tez qidirish uchun jadval.</summary>
    private static readonly Dictionary<string, SystemSettingDefinition> ByKey =
        All.ToDictionary(definition => definition.Key, StringComparer.Ordinal);

    /// <summary>Kalit ro'yxatda bormi.</summary>
    public static bool IsKnown(string? key) => Find(key) is not null;

    /// <summary>Kalitning ta'rifi. Noma'lum kalitda <c>null</c>.</summary>
    /// <remarks>
    /// Solishtirish <c>Ordinal</c> — harf registri AHAMIYATLI.
    /// «Bot.About.Latin» boshqa kalit hisoblanadi va rad etiladi: kalitlar
    /// kodda konstanta bo'lib turadi, ya'ni ularni qo'lda yozish holati
    /// faqat mijoz xato yuborganda uchraydi. Registrga befarq bo'lish esa
    /// bazada bir xil sozlamaning ikki xil yozuvini paydo qilardi.
    /// </remarks>
    public static SystemSettingDefinition? Find(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return ByKey.GetValueOrDefault(key.Trim());
    }

    /// <summary>Sonni sukut qiymat uchun matnga o'giradi.</summary>
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
