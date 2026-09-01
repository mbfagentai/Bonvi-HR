using System.Globalization;
using System.Text.RegularExpressions;
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

    /// <summary>
    /// SIR — bazaga shifrlangan holda yoziladi va o'qib bo'lmaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Text"/> dan farqi TURIDA emas, MUOMALADA. Qiymat
    /// <see cref="Bonvi.Domain.Abstractions.ISecretProtector"/> orqali
    /// shifrlanadi, API javobida hech qachon qaytmaydi (o'rniga «qo'yilganmi»
    /// bayrog'i va niqoblangan ko'rinishi beriladi) va ekranda parol
    /// maydoni bo'lib chiziladi.
    /// </para>
    /// <para>
    /// <b>Bo'sh qiymat — «o'chirish».</b> Matnda bo'sh satr haqiqiy
    /// qiymat (bot matnini o'chirish), sirda esa yozuvning O'ZI
    /// olib tashlanadi: «bo'sh sir» degan tushuncha yo'q, sir yo bor,
    /// yo yo'q.
    /// </para>
    /// </remarks>
    Secret = 3,

    /// <summary>
    /// RASM — qiymat fayl kaliti, uni foydalanuvchi YOZMAYDI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Boshqa turlardan farqi shunda: qiymatni umumiy «sozlamalarni
    /// saqlash» so'rovi O'ZGARTIRA OLMAYDI. Rasm alohida yo'l bilan
    /// yuklanadi (<c>POST /api/settings/bot/logo</c>) — u yerda hajm,
    /// tur va mazmun tekshiriladi, eski fayl o'chiriladi va faqat
    /// shundan keyin kalit yoziladi.
    /// </para>
    /// <para>
    /// Ro'yxatda esa u BOR: ekran rasmni ko'rsatishi va uni almashtirish
    /// tugmasini chizishi kerak, ya'ni «bunday sozlama bor» degan bilim
    /// baribir kerak.
    /// </para>
    /// </remarks>
    Photo = 4,
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
/// <param name="Pattern">
/// Qiymat mos kelishi shart bo'lgan naqsh (regex). <c>null</c> — tekshiruv yo'q.
/// <para>
/// NEGA kerak: <see cref="SystemSettingKind.Secret"/> turidagi qiymat
/// noto'g'ri bo'lsa, buni HECH KIM darhol ko'rmaydi — server uni
/// shifrlab qo'yadi, ekran esa «qo'yilgan» deb ko'rsatadi va nosozlik
/// faqat botning jurnalida qoladi. Naqsh esa terish xatosini
/// SAQLANGUNCHA ushlaydi.
/// </para>
/// <para>
/// Matnga ham tegishli va shu sabab bilan: xarita nuqtasi o'rniga
/// manzil yozilsa, nomzod xaritani olmaydi va buni hech kim
/// ko'rmaydi. BO'SH matn esa naqshdan qat'i nazar o'tadi — bo'shlik
/// «o'chirish» degani.
/// </para>
/// <para>
/// Naqsh KODDA yoziladi va tashqaridan kelmaydi — ya'ni «yovuz regex»
/// xavfi yo'q. Shunga qaramay tekshiruv muddat bilan chegaralangan
/// (<see cref="RegexTimeout"/>): naqsh kelajakda murakkablashsa,
/// so'rov osilib qolmasin.
/// </para>
/// </param>
public sealed record SystemSettingDefinition(
    string Key,
    SystemSettingKind Kind,
    string DefaultValue,
    int? Min = null,
    int? Max = null,
    int? MaxLength = null,
    string? Pattern = null)
{
    /// <summary>Naqsh tekshiruvining eng uzun muddati.</summary>
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

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
        if (Kind == SystemSettingKind.Photo)
        {
            // Rasm kaliti — serverning ichki qiymati. Uni qo'lda yozish
            // bazadagi kalitni mavjud bo'lmagan faylga qaratib qo'yardi
            // va bot rasm o'rniga hech narsa yubormasdi.
            return SystemSettingErrors.NotEditable;
        }

        if (Kind == SystemSettingKind.Secret)
        {
            /*
              Sirda ichki bo'shliq ham xato: token va kalit bir bo'lakli
              satr bo'ladi va ular odatda nusxa olishda qator ko'chishi
              yoki tab bilan birga kelib qoladi. `Trim()` yetarli emas —
              o'rtadagi bo'shliqni ham olib tashlash kerak, aks holda u
              naqshdan o'tmay, foydalanuvchi esa sababini ko'rmasdi.
            */
            var secret = new string((value ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray());

            // Bo'sh — «o'chirish», xato emas (izohi `SystemSettingKind.Secret` da).
            if (secret.Length == 0)
            {
                return Result.Success(string.Empty);
            }

            if (MaxLength is { } secretLimit && secret.Length > secretLimit)
            {
                return SystemSettingErrors.TextTooLong(secretLimit);
            }

            return Pattern is not null && !Regex.IsMatch(secret, Pattern, RegexOptions.None, RegexTimeout)
                ? SystemSettingErrors.BadFormat
                : Result.Success(secret);
        }

        if (Kind == SystemSettingKind.Text)
        {
            // Chetdagi bo'shliqlar olib tashlanadi: HR matnni Word'dan
            // nusxa qilganda oxirida ko'rinmas bo'shliq va yangi qator
            // qolib ketadi, bot esa uni xabar oxirida bo'sh qator qilib
            // chizardi.
            var text = value?.Trim() ?? string.Empty;

            if (MaxLength is { } limit && text.Length > limit)
            {
                return SystemSettingErrors.TextTooLong(limit);
            }

            /*
              Naqsh MATNGA ham tegishli. Ilgari u faqat sirda
              tekshirilardi va bu jimgina teshik edi: `bot.location`
              uchun naqsh yozib qo'yilgan bo'lsa ham, uning o'rniga
              manzil matni yozilsa server uni QABUL QILARDI. Nomzod
              esa xaritani olmasdi va buni hech kim ko'rmasdi —
              aynan naqsh oldini olishi kerak bo'lgan holat.

              BO'SH matn baribir o'tadi: bo'shlik — «o'chirish» degani
              va bu qoida yuqoridagi izohda. Naqshni bo'sh satrga ham
              qo'llash sozlamani o'chirib bo'lmaydigan qilib qo'yardi.
            */
            return text.Length > 0
                && Pattern is not null
                && !Regex.IsMatch(text, Pattern, RegexOptions.None, RegexTimeout)
                    ? SystemSettingErrors.BadFormat
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

    /// <summary>
    /// @BotFather bergan Telegram tokeni — SIR.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega u endi bu yerda, <c>.env</c> da emas</b> (foydalanuvchi
    /// qarori). Token almashadi: bot qayta yaratiladi, tokeni o'g'irlangan
    /// deb bekor qilinadi, sinov botidan ishchi botga o'tiladi.
    /// Har safar serverga kirib, faylni tahrirlab, konteynerni qayta
    /// ishga tushirish kerak edi — ya'ni oddiy ma'muriy ish dasturchining
    /// ishiga aylanardi.
    /// </para>
    /// <para>
    /// <b><c>.env</c> dagi <c>TELEGRAM__BOTTOKEN</c> YO'QOLMADI</b> — u
    /// endi ZAXIRA: bu yerda qiymat bo'lmasa bot o'shanga tushadi. Shu
    /// tufayli mavjud o'rnatmalar hech narsa o'zgartirmasdan ishlab
    /// ketaveradi va yangi o'rnatishda botni ekranga kirmasdan ham
    /// yoqib qo'yish mumkin.
    /// </para>
    /// <para>
    /// <b>Naqsh Telegram formatiga bog'langan:</b> <c>&lt;bot id&gt;:&lt;sir&gt;</c>.
    /// Bu qat'iy tekshiruv emas (Telegram formatni o'zgartirishi mumkin),
    /// lekin eng keng tarqalgan xatoni — tokenni chala nusxa olishni —
    /// darhol ushlaydi. Aks holda xato faqat botning jurnaliga tushardi
    /// va sozlagan odam «nega ishlamayapti?» deb qolardi.
    /// </para>
    /// </remarks>
    public const string BotToken = "bot.token";

    /// <summary>«Kompaniya haqida» matni — kirill alifbosida.</summary>
    public const string BotAboutCyrillic = "bot.about.cyrillic";

    /// <summary>«Kompaniya haqida» matni — lotin alifbosida.</summary>
    public const string BotAboutLatin = "bot.about.latin";

    /// <summary>«Kompaniya haqida» matni — rus tilida.</summary>
    public const string BotAboutRussian = "bot.about.russian";

    /// <summary>Kontaktlar va manzil — kirill alifbosida.</summary>
    /// <remarks>
    /// <para>
    /// Telefon va manzil BITTA matnda, alohida maydonlarga bo'linmasdan.
    /// Sabab: kadrlar bo'limida bir nechta raqam bor, ba'zi filialda
    /// qo'shimcha yo'l ko'rsatma kerak bo'ladi va bularning shakli
    /// oldindan ma'lum emas. Har birini alohida maydon qilish HR ni
    /// bizning tasavvurimizga majburlardi.
    /// </para>
    /// <para>
    /// Bo'sh bo'lsa menyuda «Kontaktlar» tugmasi UMUMAN chizilmaydi —
    /// «Kompaniya haqida» bilan bir xil qoida: sozlanmagan bo'lim
    /// nomzodga bo'sh ekran ko'rsatgandan ko'ra ko'rinmagani yaxshi.
    /// </para>
    /// </remarks>
    public const string BotContactsCyrillic = "bot.contacts.cyrillic";

    /// <summary>Kontaktlar va manzil — lotin alifbosida.</summary>
    public const string BotContactsLatin = "bot.contacts.latin";

    /// <summary>Kontaktlar va manzil — rus tilida.</summary>
    public const string BotContactsRussian = "bot.contacts.russian";

    /// <summary>Kompaniya logotipi — fayl kaliti.</summary>
    /// <remarks>
    /// Qiymatni ekrandagi yuklash tugmasi yozadi, qo'lda emas
    /// (<see cref="SystemSettingKind.Photo"/>). Bo'sh bo'lsa bot
    /// «Kompaniya haqida» ni faqat matn bilan yuboradi.
    /// </remarks>
    public const string BotLogo = "bot.logo";

    /// <summary>Instagram sahifasi — «Kompaniya haqida» ostidagi tugma.</summary>
    /// <remarks>
    /// <para>
    /// To'rtta havola ALOHIDA sozlama, bitta ro'yxat emas. Sabab: har
    /// birining tugmasida o'z nomi va o'z ikonasi turadi, ya'ni bot
    /// qaysi havola qaysi tarmoqniki ekanini BILISHI kerak. Bitta
    /// matnga yig'ilgan ro'yxatda bu bilim yo'qolardi va tugmalar
    /// «Havola 1», «Havola 2» bo'lib qolardi.
    /// </para>
    /// <para>
    /// Bo'sh bo'lsa tugma umuman chizilmaydi — menyudagi bilan bir xil
    /// qoida.
    /// </para>
    /// </remarks>
    public const string BotSocialInstagram = "bot.social.instagram";

    /// <summary>Telegram kanali — «Kompaniya haqida» ostidagi tugma.</summary>
    public const string BotSocialTelegram = "bot.social.telegram";

    /// <summary>Facebook sahifasi — «Kompaniya haqida» ostidagi tugma.</summary>
    public const string BotSocialFacebook = "bot.social.facebook";

    /// <summary>Kompaniya sayti — «Kompaniya haqida» ostidagi tugma.</summary>
    public const string BotSocialWebsite = "bot.social.website";

    /// <summary>Bosh ofis xaritadagi nuqtasi — «kenglik,uzunlik».</summary>
    /// <remarks>
    /// <para>
    /// Nomzod manzilni MATN bilan emas, xaritadagi nuqta bilan tushunadi:
    /// Telegram uni ochib navigatorga uzatadi. Shu sababli kontakt
    /// matnidan keyin alohida «Location» yuboriladi.
    /// </para>
    /// <para>
    /// <b>Nega bitta matn maydoni, ikkita son emas.</b> HR nuqtani
    /// Google Maps'dan NUSXA OLADI va u yerda u aynan shu ko'rinishda —
    /// «40.505852, 70.930622». Ikkita alohida maydon HR ni sonni qo'lda
    /// bo'lishga majbur qilardi va bu xatoning eng oson turi.
    /// </para>
    /// <para>
    /// Bo'sh bo'lsa xarita yuborilmaydi, kontakt matni esa baribir
    /// chiqadi: manzil matn bilan ham tushunarli.
    /// </para>
    /// </remarks>
    public const string BotLocation = "bot.location";

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

    /// <summary>O'qilgan bildirishnoma ro'yxatda necha kun qoladi.</summary>
    public const string ReadRetentionDays = "notifications.readRetentionDays";

    /// <summary>Sirning eng katta uzunligi.</summary>
    /// <remarks>
    /// Telegram tokeni ~46 belgi. 200 — zaxira bilan: chegara bu yerda
    /// to'g'rilik uchun emas, so'rov tanasini cheksiz o'stirishning
    /// oldini olish uchun.
    /// </remarks>
    public const int MaxSecretLength = 200;

    /// <summary>
    /// Telegram tokenining ko'rinishi: <c>&lt;raqam&gt;:&lt;sir&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Ikki nuqtagacha — botning identifikatori (u sir emas, hatto
    /// niqoblangan ko'rinishda ham ko'rsatiladi), keyin sirning o'zi.
    /// Uzunliklar ATAYLAB keng olingan: Telegram ularni e'lon qilmagan
    /// va tor chegara bir kun haqiqiy tokenni rad etib qo'yardi.
    /// </remarks>
    public const string TelegramTokenPattern = @"^[0-9]{5,20}:[A-Za-z0-9_-]{20,}$";

    /// <summary>
    /// Xarita nuqtasining shakli — «kenglik,uzunlik».
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sonlar orasida vergul, ixtiyoriy bo'sh joy bilan: Google Maps
    /// aynan shunday nusxalaydi. Manfiy qiymat ham ruxsat etilgan —
    /// janubiy yarim shar va g'arbiy uzunlik.
    /// </para>
    /// <para>
    /// Shablon FAQAT shaklni tekshiradi, chegaralarni emas (kenglik
    /// ±90, uzunlik ±180). Sabab: shablon domen qoidasi emas, ekrandagi
    /// birinchi to'siq — «nuqta o'rniga manzil yozib qo'yildi» degan
    /// eng ko'p uchraydigan xatoni darhol ushlaydi. Chegaralarni aniq
    /// tekshiradigan naqsh esa o'qib bo'lmas darajada uzun bo'lardi.
    /// </para>
    /// <para>
    /// Chegara yuborishdan oldin tekshiriladi (<c>MapPoint.TryParse</c>):
    /// «91, 70» saqlanadi, lekin xarita YUBORILMAYDI — Telegram bunday
    /// nuqtali xabarni butunlay rad etardi va nomzod kontakt matnini
    /// ham olmay qolardi.
    /// </para>
    /// </remarks>
    public const string LocationPattern = @"^-?[0-9]{1,3}(\.[0-9]+)?,\s*-?[0-9]{1,3}(\.[0-9]+)?$";

    /// <summary>Xarita nuqtasi matnining eng katta uzunligi.</summary>
    public const int MaxLocationLength = 64;

    /// <summary>
    /// Ijtimoiy tarmoq havolasining shakli.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Faqat <c>https</c>.</b> Telegram tugmaga qo'yilgan havolani
    /// TEKSHIRADI va noto'g'risida butun xabarni rad etadi — ya'ni
    /// bitta noto'g'ri havola «Kompaniya haqida» ni butunlay
    /// ishlamaydigan qilib qo'yardi. Bundan tashqari <c>http</c>
    /// havolasi nomzodning brauzerida ogohlantirish chiqaradi.
    /// </para>
    /// <para>
    /// Ichki tuzilishi tekshirilmaydi: havolalar uzun va so'rov
    /// parametrlari bilan keladi (Facebook profil havolasi bunga
    /// yaqqol misol) va ularni naqsh bilan qamrab olishga urinish
    /// haqiqiy havolani rad etib qo'yardi.
    /// </para>
    /// </remarks>
    public const string UrlPattern = @"^https://\S{3,}$";

    /// <summary>Havola matnining eng katta uzunligi.</summary>
    /// <remarks>
    /// Telegram tugmasidagi havola uchun chegara e'lon qilmagan, lekin
    /// 2048 — brauzerlarning amaldagi chegarasi va undan uzun havola
    /// baribir hech qayerda ishlamasdi.
    /// </remarks>
    public const int MaxUrlLength = 2048;

    /// <summary>Fayl kalitining eng katta uzunligi.</summary>
    public const int MaxKeyLength = 200;

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
        // Token — bot bo'limining BIRINCHI qatori: usiz qolgan uchtasi
        // umuman ishlamaydi.
        new(
            BotToken,
            SystemSettingKind.Secret,

            // Sukut qiymat BO'SH va boshqacha bo'lishi ham mumkin emas:
            // sirning «tizim taklif qilgan qiymati» degan tushunchasi
            // yo'q. Bo'sh — «qo'yilmagan», bot esa `.env` dagi zaxiraga
            // tushadi.
            string.Empty,
            MaxLength: MaxSecretLength,
            Pattern: TelegramTokenPattern),

        new(BotAboutCyrillic, SystemSettingKind.Text, string.Empty, MaxLength: MaxTextLength),
        new(BotAboutLatin, SystemSettingKind.Text, string.Empty, MaxLength: MaxTextLength),
        new(BotAboutRussian, SystemSettingKind.Text, string.Empty, MaxLength: MaxTextLength),
        new(BotContactsCyrillic, SystemSettingKind.Text, string.Empty, MaxLength: MaxTextLength),
        new(BotContactsLatin, SystemSettingKind.Text, string.Empty, MaxLength: MaxTextLength),
        new(BotContactsRussian, SystemSettingKind.Text, string.Empty, MaxLength: MaxTextLength),
        new(
            BotLocation,
            SystemSettingKind.Text,
            string.Empty,
            MaxLength: MaxLocationLength,
            Pattern: LocationPattern),
        new(BotLogo, SystemSettingKind.Photo, string.Empty, MaxLength: MaxKeyLength),
        new(BotSocialInstagram, SystemSettingKind.Text, string.Empty, MaxLength: MaxUrlLength, Pattern: UrlPattern),
        new(BotSocialTelegram, SystemSettingKind.Text, string.Empty, MaxLength: MaxUrlLength, Pattern: UrlPattern),
        new(BotSocialFacebook, SystemSettingKind.Text, string.Empty, MaxLength: MaxUrlLength, Pattern: UrlPattern),
        new(BotSocialWebsite, SystemSettingKind.Text, string.Empty, MaxLength: MaxUrlLength, Pattern: UrlPattern),
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
        new(
            ReadRetentionDays,
            SystemSettingKind.Number,
            Number(NotificationRules.ReadRetentionDays),
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
