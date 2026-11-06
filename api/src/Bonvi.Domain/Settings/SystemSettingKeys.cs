using System.Globalization;
using System.Text.RegularExpressions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;
using Bonvi.Domain.Notifications;
using Bonvi.Domain.Recruiting;

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
/// Yangi tur qo'shish ARZON emas: har biri o'z tekshiruvini, o'z
/// saqlash yo'lini va frontendda o'z maydonini talab qiladi. Shu
/// sababli bu yerda faqat MUOMALASI boshqacha bo'lgan turlar bor
/// (sir shifrlanadi, rasm alohida yuklanadi, yashirin qiymatni
/// server yozadi). Sana yoki bayroq (ha/yo'q) qo'shilmagan —
/// bunday sozlama hali so'ralmagan.
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

    /// <summary>
    /// YASHIRIN — qiymatni server o'zi yozadi va u ekranga UMUMAN chiqmaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Photo"/> bilan bir xil tomoni: umumiy «sozlamalarni
    /// saqlash» so'rovi bu qiymatga tega olmaydi. Farqi — ro'yxatda
    /// ham YO'Q. Rasmni foydalanuvchi ko'rishi va almashtirishi kerak,
    /// bu yerdagi qiymat esa tizimning ICHKI bog'lanishi («Umumiy
    /// anketa» vakansiyasining identifikatori). Uni ekranga chiqarish
    /// foydalanuvchiga hech narsa bermaydi, lekin «bu nima va uni
    /// o'zgartirsam nima bo'ladi?» degan savol tug'dirardi.
    /// </para>
    /// <para>
    /// <b>Nega u baribir SHU jadvalda.</b> Qiymat aynan SHU bazadagi
    /// qatorga ishora qiladi, ya'ni baza bilan birga yashashi kerak.
    /// <c>.env</c> ga yozilganda nusxa olingan bazada (sinov muhiti,
    /// zaxiradan tiklash) u boshqa bazadagi identifikatorni
    /// ko'rsatib turardi.
    /// </para>
    /// </remarks>
    Hidden = 5,
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
/// <param name="AllowZero">
/// <c>0</c> chegaradan TASHQARIDA turgani holda ham qabul qilinsinmi.
/// <para>
/// Faqat <see cref="SystemSettingKind.Number"/> uchun va faqat nol
/// ALOHIDA MA'NO anglatganda yoqiladi («hech qachon», «o'chirilgan») —
/// ya'ni u chegaraning bir uchi emas, boshqa javob. Birinchi egasi
/// <see cref="SystemSettingKeys.PassRetentionDays"/>: u yerda 0 —
/// «o'tishlar umuman o'chirilmasin».
/// </para>
/// <para>
/// Nega shunchaki <c>Min = 0</c> emas: o'shanda 0 bilan haqiqiy quyi
/// chegara orasidagi sonlar ham o'tib ketardi va ular ma'noli qiymat
/// emas, TERISH XATOSI. «365» o'rniga «5» yozilgani bir yillik dalilni
/// bir kechada o'chirib yuborardi.
/// </para>
/// </param>
public sealed record SystemSettingDefinition(
    string Key,
    SystemSettingKind Kind,
    string DefaultValue,
    int? Min = null,
    int? Max = null,
    int? MaxLength = null,
    string? Pattern = null,
    bool AllowZero = false)
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
        if (Kind is SystemSettingKind.Photo or SystemSettingKind.Hidden)
        {
            /*
              Ikkalasini ham SERVER yozadi: rasm kaliti — saqlash
              natijasidan, yashirin qiymat — urug'lantirishdan. Qo'lda
              yozishga ruxsat berilsa, kalit mavjud bo'lmagan FAYLGA,
              identifikator esa mavjud bo'lmagan QATORGA qarab qolardi.
              Ikkala xato ham jimgina o'tardi: bot rasm o'rniga hech
              narsa yubormasdi, nomzod esa yo'q vakansiyaga tushardi.
            */
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

        /*
          NOL chegaradan tashqarida, lekin `AllowZero` yoqilgan sozlamada
          u HAQIQIY qiymat: «hech qachon» degan javob (izohi
          `AllowZero` da). Oraliqdagi qolgan sonlar esa o'zgarishsiz
          rad etiladi — aynan shu farq bu yerda bir shart bo'lib turadi.
        */
        if (AllowZero && number == 0)
        {
            return Result.Success("0");
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

    /// <summary>
    /// Suhbat taklifidagi mas'ul telefon raqami.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega <see cref="BotContactsCyrillic"/> dan alohida.</b> «Kontaktlar»
    /// — menyudagi bo'lim uchun yozilgan ERKIN matn: unda bir nechta raqam,
    /// ish vaqti va yo'l ko'rsatma bo'lishi mumkin. Suhbat taklifi esa qisqa
    /// xabar va unga BITTA raqam kerak — nomzod chalkashmasligi kerak,
    /// «kelolmayman» deb kimga qo'ng'iroq qilishini bir qarashda ko'rishi
    /// shart.
    /// </para>
    /// <para>
    /// Bo'sh bo'lsa xabarda telefon qatori umuman chizilmaydi — botning
    /// qolgan hamma joyidagi bilan bir xil qoida.
    /// </para>
    /// </remarks>
    public const string BotInterviewPhone = "bot.interview.phone";

    /// <summary>
    /// The bot's Telegram username, written by the bot itself at start (it learns it from
    /// <c>getMe</c>). Links to the bot are built from it; empty until the bot has reported.
    /// </summary>
    public const string BotUsername = "bot.username";

    /// <summary>The council's Telegram group: its chat id, written when the group is bound (SPEC-kengash-guruhi §2).</summary>
    public const string BotCouncilChatId = "bot.council.chatId";

    /// <summary>The bound group's name, only to show which group it is.</summary>
    public const string BotCouncilChatTitle = "bot.council.chatTitle";

    /// <summary>The binding code in force, as <c>code|expiry</c>; empty when none.</summary>
    public const string BotCouncilBindCode = "bot.council.bindCode";

    /// <summary>The Mini App's short name in BotFather. Empty: buttons go through the private chat.</summary>
    public const string BotCouncilMiniAppName = "bot.council.miniAppName";

    /// <summary>
    /// Suhbat taklifidagi manzil — qisqa satr («Qo'qon», «Toshkent, Chilonzor»).
    /// </summary>
    /// <remarks>
    /// Xaritadagi nuqta <see cref="BotLocation"/> dan olinadi va u ALOHIDA
    /// xabar bo'lib ketadi (Telegram uni navigatorga uzatadi). Bu maydon esa
    /// nuqtaning YORLIG'I: nomzod xaritani ochmasdan ham qayerga borishini
    /// bilishi kerak. Ikkalasi bir-birini almashtirmaydi.
    /// </remarks>
    public const string BotInterviewAddress = "bot.interview.address";

    /// <summary>Ommaviy oferta havolasi — anketaning oxirgi qadamida.</summary>
    /// <remarks>
    /// <para>
    /// Nomzod anketani yuborishdan oldin oferta bilan tanishib,
    /// «Roziman» tugmasini bosadi va rozilik VAQTI yozib qo'yiladi
    /// (<c>CandidateQuestionnaire.OfferAcceptedAt</c>) — bu huquqiy
    /// fakt, oddiy bayroq emas.
    /// </para>
    /// <para>
    /// <b>Bo'sh bo'lsa bot oferta qadamini UMUMAN chizmaydi</b> va
    /// anketa bir qadam qisqaradi. Boshqa <c>bot.</c> sozlamalari
    /// bilan bir xil qoida: sozlanmagan bo'lim nomzodga bo'sh ekran
    /// ko'rsatgandan ko'ra ko'rinmagani yaxshi. Rozilik ham
    /// so'ralmaydi — o'qib bo'lmaydigan hujjatga rozilik olish
    /// huquqiy jihatdan hech narsa bermaydi.
    /// </para>
    /// </remarks>
    public const string BotOfferUrl = "bot.offerUrl";

    /// <summary>Yarim qolgan suhbat qancha vaqtdan keyin unutiladi (daqiqa).</summary>
    public const string BotConversationTimeoutMinutes = "bot.conversationTimeoutMinutes";

    // ────────────────────────────────────────────────────────────
    //  BILDIRISHNOMALAR — qachon ogohlantirish paydo bo'ladi
    // ────────────────────────────────────────────────────────────

    /// <summary>Sinov muddati tugashiga necha kun qolganda ogohlantiriladi.</summary>
    public const string ProbationWarningDays = "notifications.probationWarningDays";

    /// <summary>Nomzod necha kun davomida «yangi» hisoblanadi.</summary>
    public const string NewCandidateDays = "notifications.newCandidateDays";

    /// <summary>
    /// Suhbatgacha necha SOAT qolganda ogohlantirish paydo bo'ladi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bildirishnoma sozlamalari orasida yagona SOATLI qiymat: qolgan
    /// uchtasi kun bilan o'lchanadi, chunki ular kalendar faktlariga
    /// tegishli. Suhbat esa lahza — sabab va sukut qiymatning kelib chiqishi
    /// <see cref="NotificationRules.InterviewWarningHours"/> izohida.
    /// </para>
    /// <para>
    /// ⚠️ Chegara <see cref="MaxDays"/> ga TAYANMAYDI (90 kun bu yerda
    /// 90 soat degani bo'lardi va u ma'nosiz katta) — o'zining
    /// <see cref="MaxWarningHours"/> si bor.
    /// </para>
    /// </remarks>
    public const string InterviewWarningHours = "notifications.interviewWarningHours";

    /// <summary>O'qilgan bildirishnoma ro'yxatda necha kun qoladi.</summary>
    public const string ReadRetentionDays = "notifications.readRetentionDays";

    // ────────────────────────────────────────────────────────────
    //  DAVOMAT — verifikatsiya moduli
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Turniket surati va xom hodisa yozuvi necha kun saqlanadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ Bu sozlama KO'RSATISHNI emas, FAYL O'CHIRISHNI boshqaradi.
    /// Muddati o'tgan suratni tungi tozalash ishi diskdan o'chiradi va uni
    /// QAYTARIB BO'LMAYDI: o'tish qatorining o'zi joyida qoladi, faqat
    /// surat bilan xom hodisa yo'qoladi. Ya'ni qiymatni pasaytirish
    /// «ro'yxatni qisqartirish» emas — allaqachon yig'ilgan rasmlarni
    /// keyingi tungi yurishda o'chirib yuborish degani.
    /// </para>
    /// <para>
    /// <b>Nega aynan SHU chegara sozlanadi</b>, davomatning qolgan
    /// chegaralari esa domen konstantasi bo'lib qoladi
    /// (<see cref="AttendanceRules"/>): saqlash muddati texnik qoida emas, DISK
    /// bilan kelishuv. 540 xodim kuniga 10 martagacha o'tadi va bu 60
    /// kunda ~13 GB surat degani; joyni nazorat qiladigan odam buni
    /// dasturchisiz o'zgartira olishi kerak.
    /// </para>
    /// <para>
    /// Eng past chegara 7 kun ATAYLAB noldan katta. <c>0</c> deb yozib
    /// yuborish bugungi rasmlarni ham o'chirardi, holbuki surat aynan
    /// yangi kunlarda kerak bo'ladi: «bu men emasman» degan bahs o'sha
    /// hafta ichida chiqadi, bir yildan keyin emas.
    /// </para>
    /// </remarks>
    public const string PhotoRetentionDays = "attendance.photoRetentionDays";

    /// <summary>
    /// Xom o'tish yozuvi (<c>attendance_passes</c>) necha kun saqlanadi.
    /// <c>0</c> — hech qachon o'chirilmaydi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ Bu sozlama ham KO'RSATISHNI emas, QATOR O'CHIRISHNI boshqaradi:
    /// muddati o'tgan o'tishlarni tungi ish bazadan olib tashlaydi va uni
    /// QAYTARIB BO'LMAYDI. Qiymatni pasaytirish keyingi tungi yurishda
    /// allaqachon yig'ilgan yozuvlarni o'chiradi.
    /// </para>
    /// <para>
    /// <b>Hisobot tarixi bundan ZARAR KO'RMAYDI.</b> O'chadigan narsa —
    /// xom dalil: turniket yozuvi va uning surati. Hisoblangan kunlar
    /// (<c>attendance_days</c> — tabel, ishlangan soat, kechikish) SHU
    /// sozlama bilan hech qachon o'chirilmaydi, ya'ni o'tgan yillarning
    /// tabeli ham, hisoboti ham joyida qoladi. Yo'qoladigani faqat
    /// «o'sha kuni soat 08:03 da aynan shu odam o'tgan» degan dalil.
    /// </para>
    /// <para>
    /// Sukut qiymat — 365 kun (egasining qarori, bir yil). Surat esa
    /// odatda ANCHA erta ketadi (<see cref="PhotoRetentionDays"/>):
    /// avval rasm, keyin yozuvning o'zi.
    /// </para>
    /// <para>
    /// <b><c>0</c> — «hech qachon o'chirilmasin»</b> va u ATAYLAB
    /// chegaradan tashqarida turadi (<see cref="SystemSettingDefinition.AllowZero"/>).
    /// Shu sababli quyi chegara oddiygina «30 dan kam bo'lmasin» emas:
    /// 0 — aniq aytilgan qaror, 1…29 esa terish xatosi va rad etiladi.
    /// Yuqori chegara 3650 kun — o'n yil, ya'ni «amalda cheksiz»ning
    /// sanab ko'rsatilgan ko'rinishi.
    /// </para>
    /// </remarks>
    public const string PassRetentionDays = "attendance.passRetentionDays";

    /// <summary>
    /// Jadval so'rovidagi tanaffusning ruxsat etilgan oralig'i — BOSHI, kun daqiqasi
    /// (720 = 12:00).
    /// </summary>
    /// <remarks>
    /// <para>
    /// To'rtta kalit bitta qoida: <c>RequestBreakRule</c> (SPEC-sorov-tahriri §2.2, §4). «Kunni
    /// o'zgartirish» so'rovining ish kunida tanaffus shu oraliqda, <see cref="RequestBreakMinutes"/>
    /// uzunlikda (jumada <see cref="RequestBreakFridayMaxMinutes"/> gacha) bo'lishi kerak — ilova
    /// va HR formasida, yaratish va tahrirda. Avval yuborilgan so'rovlar qayta tekshirilmaydi.
    /// </para>
    /// <para>
    /// ⚠️ Sozlamalar sahifasida (<c>GROUPS</c>) EMAS — «Ish jadvallari» sahifasidagi kartada
    /// tahrirlanadi, saqlash muddatlari kabi. Kalitlararo qoida (boshi &lt; oxiri, uzunlik ≤ jumadagi,
    /// jumadagi ≤ oraliq) — <c>UpdateSettingsCommandHandler</c> da.
    /// </para>
    /// </remarks>
    public const string RequestBreakFrom = "attendance.requestBreakFrom";

    /// <summary>Tanaffus oralig'ining OXIRI, kun daqiqasi (900 = 15:00).</summary>
    public const string RequestBreakTo = "attendance.requestBreakTo";

    /// <summary>Talab qilingan tanaffus uzunligi (daqiqa) — dushanba–payshanba, shanba, yakshanba; jumada eng kami.</summary>
    public const string RequestBreakMinutes = "attendance.requestBreakMinutes";

    /// <summary>Jumadagi eng uzun tanaffus (daqiqa) — juma namozi.</summary>
    public const string RequestBreakFridayMaxMinutes = "attendance.requestBreakFridayMaxMinutes";

    /// <summary>
    /// Shu sana va undan OLDINGI kunlar qayta hisoblanmaydi — ularning xom
    /// o'tishlari saqlash muddati bilan o'chirilgan bo'lishi mumkin. Serverning
    /// O'ZI yozadi (<see cref="SystemSettingKind.Hidden"/>), <c>yyyy-MM-dd</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Nega bor.</b> O'tish o'chirilgach uning kuni qayta hisoblansa, kun
    /// OMON QOLGAN o'tishlardan quriladi va bir yillik tabel jimgina nolga
    /// aylanadi (SPEC §9.2.1). Bunga olib boradigan yo'l bitta emas: qo'lda
    /// «qayta hisoblash», muddat chegarasidagi o'tishning qo'shni kuni (±1),
    /// eski o'tishlari kechikib bog'langan moslik. Shu sababli to'siq har bir
    /// yo'lda emas, YAGONA yozuvchida — <c>AttendanceDayRecomputer</c> shu
    /// sanagacha bo'lgan kunlarga tegmaydi.
    /// </para>
    /// <para>
    /// <b>Nega sozlamadan hisoblanmaydi, alohida saqlanadi.</b> Tozalash
    /// ishlagandan keyin muddat UZAYTIRILSA (365 → 730), sozlamadan hisoblangan
    /// chegara orqaga siljib, allaqachon o'chirilgan kunlarni yana «qayta
    /// hisoblasa bo'ladi» deb ko'rsatardi. Bu qiymat faqat oshadi va uni
    /// tozalash vazifasi birinchi qatorni o'chirishdan OLDIN yozadi.
    /// </para>
    /// <para>
    /// Bazada — ma'lumot bilan birga: zaxiradan tiklangan baza o'z o'tishlari
    /// bilan mos chegarani olib keladi. Bo'sh — hali hech narsa o'chirilmagan.
    /// </para>
    /// </remarks>
    public const string AttendanceFrozenThrough = "attendance.frozenThrough";

    /// <summary><see cref="AttendanceFrozenThrough"/> qiymatining shakli.</summary>
    public const string FrozenThroughFormat = "yyyy-MM-dd";

    /// <summary>
    /// Mehnat munosabatlaridagi o'zgarishlar davomatga shu lahzagacha yetkazilgan.
    /// Serverning O'ZI yozadi (<see cref="SystemSettingKind.Hidden"/>), ISO 8601 UTC
    /// (<see cref="EmploymentChangesCheckedAtFormat"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega bor.</b> Orqaga sana bilan kiritilgan ishdan bo'shatish, ishga qabul
    /// yoki ko'chirish o'tishlarga tegmaydi va iflos navbat ularni ko'rmaydi; tungi
    /// hisoblash esa faqat oxirgi uch kunni qamraydi. 15-sentabrda 10-sentabr sanasi
    /// bilan bo'shatilgan odamning 11–13-sentabri shu sababli «Kelmadi» bo'lib
    /// qolardi. <c>AttendanceEmploymentChangesJob</c> shu qiymatdan beri o'zgargan
    /// munosabatlarni topib, o'sha xodimning kunlarini qayta hisoblaydi va oxirida
    /// qiymatni yurish BOSHLANGAN lahzaga suradi.
    /// </para>
    /// <para>
    /// Bo'sh — vazifa hali bir marta ham yurmagan (birinchi yurish oxirgi chorakni
    /// ko'rib chiqadi).
    /// </para>
    /// </remarks>
    public const string AttendanceEmploymentChangesCheckedAt = "attendance.employmentChangesCheckedAt";

    /// <summary><see cref="AttendanceEmploymentChangesCheckedAt"/> qiymatining shakli — aylanma ISO 8601.</summary>
    public const string EmploymentChangesCheckedAtFormat = "O";

    // ────────────────────────────────────────────────────────────
    //  REKRUTING — nomzod bilan bog'liq qoidalar
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Yopilgan arizadan keyin necha OY o'tgach bot yangi anketani
    /// qabul qiladi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nima uchun bor</b> (2026-09-08, foydalanuvchi talabi):
    /// «битта номердан битта анкета». Bitta telefon raqami bilan ikki
    /// kun ichida ikkita anketa tushgani shu sozlamaning sababi.
    /// Ilgari to'siq FAQAT jarayondagi ariza uchun ishlardi va rad
    /// etilgan odam ertasiga qayta topshira olardi.
    /// </para>
    /// <para>
    /// <b>Nega mutlaq taqiq emas.</b> Foydalanuvchi ataylab shu yo'lni
    /// tanladi: odamning holati o'zgaradi va bir yildan keyingi urinish
    /// spam emas. Mutlaq taqiq kerak bo'lsa, qiymatni yuqori chegaraga
    /// (120 oy = 10 yil) qo'yish yetarli — bu dasturchisiz bajariladi.
    /// </para>
    /// <para>
    /// <b>Nega OY, kun emas.</b> Qolgan uchta sozlama kunda
    /// (<see cref="NewCandidateDays"/> va boshqalar), lekin ular
    /// kunlik ish ritmiga tegishli. Bu esa oylik ritm: «yarim yildan
    /// keyin» degan qarorni 180 kun deb yozish HR uchun o'qib
    /// bo'lmaydigan qiymat.
    /// </para>
    /// <para>
    /// ⚠️ Qoida faqat BOTGA tegishli. HR qo'lda nomzod kiritganda hech
    /// qanday to'siq yo'q — u odamni ko'rib turibdi.
    /// </para>
    /// </remarks>
    public const string ReapplyMonths = "recruiting.reapplyMonths";

    // ────────────────────────────────────────────────────────────
    //  REKRUTING — serverning O'ZI yozadigan ichki qiymatlar
    // ────────────────────────────────────────────────────────────

    /// <summary>«Umumiy anketa» vakansiyasining identifikatori.</summary>
    /// <remarks>
    /// <para>
    /// Ochiq vakansiya bo'lmaganda ham nomzod anketa to'ldira olishi
    /// kerak, <c>Candidate.VacancyId</c> esa majburiy — shu sababli
    /// tizimda doimiy «tushadigan joy» vakansiyasi bor. Uni
    /// <c>DatabaseInitializer</c> ishga tushishda yaratadi va
    /// identifikatorini SHU yerga yozadi.
    /// </para>
    /// <para>
    /// <b>Nega <see cref="SystemSettingKind.Hidden"/>.</b> Bu HR ning
    /// tanlovi emas, tizimning ichki bog'lanishi: qo'lda o'zgartirilsa
    /// bot nomzodni mavjud bo'lmagan vakansiyaga yozishga urinardi va
    /// xato faqat botning jurnalida ko'rinardi. Shu sababli u
    /// <c>GET /api/settings</c> javobida ham, ekranda ham YO'Q.
    /// </para>
    /// <para>
    /// Qiymat bo'sh yoki noto'g'ri bo'lishi — YIQILISH sababi emas:
    /// uni o'qiydigan joylar (ochiq vakansiyalar ro'yxati) qiymat
    /// tushunarsiz bo'lganda avvalgidek ishlashda davom etadi.
    /// </para>
    /// </remarks>
    public const string GeneralPoolVacancyId = "recruiting.generalPoolVacancyId";

    /// <summary>
    /// AI ulanish (MCP) yoqilganmi: <c>"true"</c> yoki <c>"false"</c>
    /// (<c>docs/SPEC-mcp.md</c> §2.2, §8).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sukut — <c>"false"</c>.</b> Deploy'dan keyin AI ulanishi o'z-o'zidan
    /// ochilib qolmaydi: uni egasi «Tizim sozlamalari» dagi o'chirgich bilan
    /// ongli ravishda yoqadi.
    /// </para>
    /// <para>
    /// <b>Nega <see cref="SystemSettingKind.Hidden"/>.</b> Umumiy sozlamalar
    /// formasida turmaydi — uni «AI ulanish» bo'limidagi alohida o'chirgich
    /// yozadi (<c>PUT /api/mcp/enabled</c>), ruxsati bir xil
    /// (<c>settings.manage</c>). Formaga qo'shilsa, «Saqlash» bosilganda boshqa
    /// sozlamalar bilan birga beixtiyor o'zgarib ketishi mumkin edi.
    /// </para>
    /// </remarks>
    public const string McpEnabled = "mcp.enabled";

    /// <summary>
    /// HRMentor manzili (<c>https://docs.bonvi.uz</c>) — blankalar va anketa matnlari shu
    /// yerdan olinadi (SPEC-blankalar-hrmentor §2).
    /// </summary>
    /// <remarks>
    /// Manzil va <see cref="HrMentorApiKey"/> ikkalasi qo'yilsa ulanish yoqiladi; biri bo'sh
    /// bo'lsa — o'chiq: anketa dasturdagi asl matnlar bilan ishlaydi, «Blankalar» sahifasi
    /// «HRMentor ulanmagan» deydi. HRMentor bizga hech qachon o'zi murojaat qilmaydi.
    /// </remarks>
    public const string HrMentorBaseUrl = "hrmentor.baseUrl";

    /// <summary>
    /// HRMentor API kaliti — faqat <c>forms:sync</c> ruxsati bilan (SPEC-blankalar-hrmentor §2).
    /// </summary>
    /// <remarks>
    /// Bot tokeni kabi shifrlanadi va hech qachon qaytarilmaydi. Naqsh HRMentor kaliti
    /// shaklida (<see cref="HrMentorApiKeyPattern"/>): chala nusxa olingan kalit saqlashda
    /// ushlanadi, aks holda xato faqat sinxronlash jurnalida qolardi.
    /// </remarks>
    public const string HrMentorApiKey = "hrmentor.apiKey";

    /// <summary>
    /// Oxirgi yuborilgan katalogning SHA-256 xeshi. Serverning O'ZI yozadi
    /// (<see cref="SystemSettingKind.Hidden"/>): katalog o'zgarmasa qayta yuborilmaydi.
    /// </summary>
    public const string HrMentorCatalogHash = "hrmentor.catalogHash";

    /// <summary>Oxirgi sinxronlash urinishi (ISO 8601 UTC). Serverning O'ZI yozadi.</summary>
    public const string HrMentorLastAttemptAt = "hrmentor.lastAttemptAt";

    /// <summary>Oxirgi XATOSIZ sinxronlash (ISO 8601 UTC). Serverning O'ZI yozadi.</summary>
    public const string HrMentorLastSuccessAt = "hrmentor.lastSuccessAt";

    /// <summary>
    /// Oxirgi urinishning xato KODI («hrmentor.unauthorized», …) yoki bo'sh. Serverning O'ZI
    /// yozadi. Faqat kod — hech qachon javob tanasi yoki sir.
    /// </summary>
    public const string HrMentorLastError = "hrmentor.lastError";

    /// <summary>
    /// Amaldagi konstruktor anketasining versiyasi (SPEC-anketa-konstruktori §5.1) yoki bo'sh —
    /// dasturdagi o'rnatilgan anketa. Serverning O'ZI yozadi: HRMentor e'lonni qaytarib olsa
    /// bo'shatiladi, saqlangan versiyalar esa qoladi.
    /// </summary>
    public const string HrMentorDefinitionVersion = "hrmentor.definitionVersion";

    /// <summary>
    /// Oxirgi yuborilgan profilning (bog'lanishlar + urug') SHA-256 i. Serverning O'ZI yozadi:
    /// profil o'zgarmasa qayta yuborilmaydi.
    /// </summary>
    public const string HrMentorProfileHash = "hrmentor.profileHash";

    /// <summary>
    /// Blankalar qayerdan olingani (SPEC-hujjat-manbai §2): <see cref="HrMentorBlanksFromPlatform"/>
    /// — HRMentor da shu platformaga biriktirilganlar, <see cref="HrMentorBlanksFromLegacy"/> —
    /// kalit hali platformaga bog'lanmagan, eski umumiy ro'yxat; bo'sh — hali olinmagan.
    /// Serverning O'ZI yozadi.
    /// </summary>
    public const string HrMentorBlanksSource = "hrmentor.blanksSource";

    /// <summary><see cref="HrMentorBlanksSource"/> qiymati: platforma ro'yxati.</summary>
    public const string HrMentorBlanksFromPlatform = "platform";

    /// <summary><see cref="HrMentorBlanksSource"/> qiymati: eski <c>/blanks</c> ro'yxati.</summary>
    public const string HrMentorBlanksFromLegacy = "legacy";

    /// <summary>
    /// Tashkent date (<c>yyyy-MM-dd</c>) the process engine went live (docs/SPEC-mening-ishlarim.md §6.6, §7.6).
    /// Written once by the seeder; absent → the engine is dormant (no cases, gates off). Subjects whose
    /// start fact is older become legacy cases.
    /// </summary>
    public const string ProcessesStartedOn = "processes.startedOn";

    /// <summary><see cref="ProcessesStartedOn"/> value format.</summary>
    public const string ProcessesStartedOnFormat = "yyyy-MM-dd";

    /// <summary>Gate kill switch (§6.6): <c>enforce</c>, <c>warn</c> (every block becomes a warning) or <c>off</c>.</summary>
    public const string ProcessesGatesMode = "processes.gates.mode";

    /// <summary><see cref="ProcessesGatesMode"/> value: gates block.</summary>
    public const string ProcessesGatesEnforce = "enforce";

    /// <summary>
    /// <see cref="ProcessesGatesMode"/> value and its default: a failing gate is only a warning. The owner runs the
    /// first one or two weeks in this mode and then switches to <see cref="ProcessesGatesEnforce"/> in the settings:
    /// the seeder writes <c>processes.startedOn</c> at first boot, so an <c>enforce</c> default would refuse
    /// terminations and direct hires from the first day.
    /// </summary>
    public const string ProcessesGatesWarn = "warn";

    /// <summary><see cref="ProcessesGatesMode"/> value: kill switch, gates do nothing at all.</summary>
    public const string ProcessesGatesOff = "off";

    /// <summary>Allowed values of <see cref="ProcessesGatesMode"/>.</summary>
    public const string ProcessesGatesModePattern = "^(enforce|warn|off)$";

    /// <summary>ISO weekday (1 = Monday … 5 = Friday) of the hiring council — the <c>nextCouncilDay</c> due rule (§3.1).</summary>
    public const string ProcessesCouncilWeekday = "processes.councilWeekday";

    /// <summary>Default <see cref="ProcessesCouncilWeekday"/> — Thursday.</summary>
    public const int DefaultProcessesCouncilWeekday = 4;

    /// <summary>Time of the last reconciler pass (ISO 8601, «o») — diagnostics only, every pass is full (§7.4).</summary>
    public const string ProcessesReconcileWatermark = "processes.reconcile.watermark";

    /// <summary>HRMentor API kalitining shakli: <c>hrm_</c> + 256 bit (43 belgi, URL-xavfsiz base64).</summary>
    public const string HrMentorApiKeyPattern = @"^hrm_[A-Za-z0-9_-]{43}$";

    /// <summary>
    /// HRMentor manzili: <c>https://</c>, yoki <c>http://</c> faqat shu mashinaning
    /// o'zidagi manzil (localhost, 127.0.0.1, [::1], host.docker.internal).
    /// </summary>
    /// <remarks>
    /// Kalit so'rov sarlavhasida ketadi — tashqi tarmoqda faqat shifrlangan ulanish.
    /// Mashina ichidagi http lokal sinov va bir serverdagi ikki dastur uchun:
    /// trafik mashinadan chiqmaydi. Xuddi shu qoida <c>HrMentorConnection.From</c> da.
    /// </remarks>
    public const string HrMentorUrlPattern =
        @"^(https://\S{3,}|http://(localhost|127\.0\.0\.1|\[::1\]|host\.docker\.internal)(:[0-9]{1,5})?(/\S*)?)$";

    /// <summary>Lahza sozlamalarining shakli (ISO 8601, «o»).</summary>
    public const string HrMentorMomentFormat = "o";

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

    /// <summary>Identifikator matnining eng katta uzunligi.</summary>
    /// <remarks>
    /// <c>Guid</c> ning «D» ko'rinishi — aynan 36 belgi
    /// («00000000-0000-0000-0000-000000000000»). Chegara tor ataylab:
    /// bu maydonga identifikatordan boshqa hech narsa tushmasligi
    /// kerak.
    /// </remarks>
    public const int MaxIdLength = 36;

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
    /// Soat bilan o'lchanadigan ogohlantirish chegarasining eng katta qiymati.
    /// </summary>
    /// <remarks>
    /// 24 soat — bir sutka. Undan uzoq oynada qo'ng'iroqda bugungi va
    /// ertangi suhbatlar aralashib ketardi va «bu odam hozir keladi»
    /// degan xabar o'z ma'nosini yo'qotardi. Pastki chegara esa 1 soat:
    /// undan kichik qiymat yo'q, chunki sozlama BUTUN sonda o'lchanadi
    /// (<see cref="SystemSettingKind.Number"/>).
    /// </remarks>
    public const int MaxWarningHours = 24;

    /// <summary>
    /// Barcha sozlamalar — E'LON TARTIBIDA.
    /// </summary>
    /// <remarks>
    /// Tartib ATAYLAB alifbo bo'yicha emas: ekranda sozlamalar shu
    /// ketma-ketlikda chiziladi va u ma'noga qarab guruhlangan (avval
    /// bot, keyin bildirishnomalar). Alifbo tartibida «bot.about.latin»
    /// bilan «notifications.newCandidateDays» aralashib ketardi.
    /// <para>
    /// ⚠️ Bu ro'yxatning HAMMASI ekranga chiqmaydi — chizish uchun
    /// <see cref="Visible"/> olinadi (sabab o'sha yerda).
    /// </para>
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
        new(BotInterviewPhone, SystemSettingKind.Text, string.Empty, MaxLength: MaxKeyLength),
        new(BotInterviewAddress, SystemSettingKind.Text, string.Empty, MaxLength: MaxKeyLength),
        new(BotUsername, SystemSettingKind.Hidden, string.Empty, MaxLength: MaxKeyLength),
        new(BotCouncilChatId, SystemSettingKind.Hidden, string.Empty, MaxLength: MaxKeyLength),
        new(BotCouncilChatTitle, SystemSettingKind.Hidden, string.Empty, MaxLength: MaxKeyLength),
        new(BotCouncilBindCode, SystemSettingKind.Hidden, string.Empty, MaxLength: MaxKeyLength),
        new(BotCouncilMiniAppName, SystemSettingKind.Hidden, string.Empty, MaxLength: MaxKeyLength),
        new(BotLogo, SystemSettingKind.Photo, string.Empty, MaxLength: MaxKeyLength),
        new(BotSocialInstagram, SystemSettingKind.Text, string.Empty, MaxLength: MaxUrlLength, Pattern: UrlPattern),
        new(BotSocialTelegram, SystemSettingKind.Text, string.Empty, MaxLength: MaxUrlLength, Pattern: UrlPattern),
        new(BotSocialFacebook, SystemSettingKind.Text, string.Empty, MaxLength: MaxUrlLength, Pattern: UrlPattern),
        new(BotSocialWebsite, SystemSettingKind.Text, string.Empty, MaxLength: MaxUrlLength, Pattern: UrlPattern),

        // Ijtimoiy havolalar bilan bir xil naqsh va bir xil chegara:
        // bu ham tashqariga chiqadigan havola va u ham bo'sh bo'lishi
        // mumkin.
        new(BotOfferUrl, SystemSettingKind.Text, string.Empty, MaxLength: MaxUrlLength, Pattern: UrlPattern),

        new(
            BotConversationTimeoutMinutes,
            SystemSettingKind.Number,
            // Sukut qiymat `BotOptions.ConversationTimeoutMinutes` bilan
            // AYNAN bir xil: bot sozlamani o'qiy olmaganda (API hali
            // ko'tarilmagan) o'z sukut qiymatiga tushadi va ikkalasi
            // farq qilsa, botning xatti-harakati ishga tushish paytiga
            // qarab o'zgarardi. Bir sutka — egasining talabi (2026-09-29):
            // anketani yarmida qoldirgan nomzod bir necha soatdan keyin
            // davom ettira olishi kerak.
            Number(1440),
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

        // Bildirishnoma guruhining ichida, lekin chegarasi BOSHQA
        // birlikda: soat (sabab kalit izohida). Joyi ataylab shu yerda —
        // ekranda u qo'shni ikkita bildirishnoma sozlamasi bilan yonma-yon
        // turishi kerak, «soat» degani uni boshqa guruhga ko'chirmaydi.
        new(
            InterviewWarningHours,
            SystemSettingKind.Number,
            Number(NotificationRules.InterviewWarningHours),
            Min: 1,
            Max: MaxWarningHours),

        new(
            ReadRetentionDays,
            SystemSettingKind.Number,
            Number(NotificationRules.ReadRetentionDays),
            Min: 1,
            Max: MaxDays),

        // Davomat surati — bildirishnomalardan KEYIN, rekrutingdan oldin.
        // Bu ro'yxatning tartibi ekranga chiqadi (sinf izohi) va sozlama
        // bildirishnoma chegaralarining qo'shnisi EMAS: u qo'ng'iroqchani
        // emas, diskdagi FAYLNI boshqaradi — ya'ni o'z guruhi.
        //
        // ⚠️ Ekranda ko'rinishi uchun uni `system-settings-page.tsx` dagi
        // `GROUPS` ga ham qo'shish kerak: u yerda kaliti yo'q sozlama
        // JIMGINA chizilmaydi.
        new(
            PhotoRetentionDays,
            SystemSettingKind.Number,

            // Sukut qiymat DOMENDAN olinadi — bildirishnoma
            // sozlamalari `NotificationRules` dan olgani kabi va aynan
            // shu sabab bilan (sinf izohi): 60 sonini bu yerda qayta
            // yozish ikkita «60» hosil qilardi va ular bir kun ajralib
            // ketardi. Sozlama qatori hali yozilmagan tizimda tozalash
            // ishi o'sha konstantaga tushadi.
            Number(AttendanceRules.DefaultPhotoRetentionDays),

            // 7 kun — eng past ma'noli qiymat (sabab kalit izohida).
            Min: 7,

            // 365 kun — bir yil. Chegara `MaxDays` (90) ga TAYANMAYDI:
            // u bildirishnoma ro'yxati uchun o'ylangan, bu esa arxiv
            // muddati va «o'tgan yilgi nizo» uchun bir yil real talab.
            Max: 365),

        // Xom o'tish yozuvining muddati — suratnikidan KEYIN: ekranda
        // ikkalasi yonma-yon turishi kerak, chunki ular bitta qarorning
        // ikki qadami (avval rasm ketadi, keyin qatorning o'zi).
        new(
            PassRetentionDays,
            SystemSettingKind.Number,

            // 365 kun — egasining qarori: bir yil.
            Number(AttendanceRules.DefaultPassRetentionDays),

            // 30 kun — eng qisqa ma'noli muddat: oylik tabel yopilgunicha
            // xom dalil turishi kerak, aks holda «bu raqam qayerdan
            // chiqdi?» degan savolga javob qolmaydi. NOL esa bu
            // chegaradan tashqarida va u ham to'g'ri qiymat — «hech
            // qachon o'chirilmasin» (sabab kalit izohida).
            Min: 30,

            // 3650 kun — o'n yil. Undan uzoq muddat «cheksiz» bilan
            // amalda bir xil, cheksizning o'z qiymati esa allaqachon bor.
            Max: 3650,
            AllowZero: true),

        // Jadval so'rovidagi tanaffus qoidasi — saqlash muddatlaridan KEYIN, davomat guruhida.
        // Sukutlar domendan (`AttendanceRules`), chegaralar SPEC-sorov-tahriri §2.2 dan:
        // kun daqiqasi 0…1439, uzunlik 1…480 (jadval kunidagi tanaffus chegarasi bilan bir xil).
        new(
            RequestBreakFrom,
            SystemSettingKind.Number,
            Number(AttendanceRules.DefaultRequestBreakFromMinute),
            Min: 0,
            Max: 1439),
        new(
            RequestBreakTo,
            SystemSettingKind.Number,
            Number(AttendanceRules.DefaultRequestBreakToMinute),
            Min: 1,
            Max: 1439),
        new(
            RequestBreakMinutes,
            SystemSettingKind.Number,
            Number(AttendanceRules.DefaultRequestBreakMinutes),
            Min: 1,
            Max: WorkScheduleDay.MaxBreakMinutes),
        new(
            RequestBreakFridayMaxMinutes,
            SystemSettingKind.Number,
            Number(AttendanceRules.DefaultRequestBreakFridayMaxMinutes),
            Min: 1,
            Max: WorkScheduleDay.MaxBreakMinutes),

        new(
            ReapplyMonths,
            SystemSettingKind.Number,
            Number(RecruitingRules.ReapplyMonths),
            // 1 oy — eng past ma'noli qiymat: undan pastda «qayta ariza
            // muddati» degan tushunchaning o'zi yo'qoladi. 120 oy esa
            // amalda «umuman bo'lmaydi» degani (kalit izohiga qarang).
            Min: 1,
            Max: 120),

        // Jarayonlar (SPEC-mening-ishlarim §6.6).
        new(
            ProcessesGatesMode,
            SystemSettingKind.Text,
            ProcessesGatesWarn,
            MaxLength: 7,
            Pattern: ProcessesGatesModePattern),
        new(
            ProcessesCouncilWeekday,
            SystemSettingKind.Number,
            Number(DefaultProcessesCouncilWeekday),
            Min: 1,
            Max: 5),

        // HRMentor ulanishi (SPEC-blankalar-hrmentor §2): manzil va faqat `forms:sync` kaliti.
        new(HrMentorBaseUrl, SystemSettingKind.Text, string.Empty, MaxLength: MaxUrlLength, Pattern: HrMentorUrlPattern),
        new(
            HrMentorApiKey,
            SystemSettingKind.Secret,
            string.Empty,
            MaxLength: MaxSecretLength,
            Pattern: HrMentorApiKeyPattern),

        // Yashirin qiymat ro'yxatning OXIRIDA: u ekranga chiqmaydi,
        // ya'ni tartibda joyi ham yo'q. Bot va bildirishnoma
        // guruhlarining orasiga tushib qolsa, keyingi sozlama
        // qo'shayotgan odam guruhlash buzilgan deb o'ylardi.
        new(GeneralPoolVacancyId, SystemSettingKind.Hidden, string.Empty, MaxLength: MaxIdLength),
        new(AttendanceFrozenThrough, SystemSettingKind.Hidden, string.Empty, MaxLength: FrozenThroughFormat.Length),

        // «2026-09-27T10:15:00.0000000+00:00» — 33 belgi.
        new(AttendanceEmploymentChangesCheckedAt, SystemSettingKind.Hidden, string.Empty, MaxLength: 40),

        // «true» / «false» — 5 belgi.
        new(McpEnabled, SystemSettingKind.Hidden, "false", MaxLength: 5),

        // HRMentor holati (SPEC-blankalar-hrmentor §2) — sinxronlash vazifasi yozadi.
        new(HrMentorCatalogHash, SystemSettingKind.Hidden, string.Empty, MaxLength: 64),
        new(HrMentorLastAttemptAt, SystemSettingKind.Hidden, string.Empty, MaxLength: 40),
        new(HrMentorLastSuccessAt, SystemSettingKind.Hidden, string.Empty, MaxLength: 40),
        new(HrMentorLastError, SystemSettingKind.Hidden, string.Empty, MaxLength: 80),

        // Konstruktor anketasi (SPEC-anketa-konstruktori §5.1) — sinxronlash vazifasi yozadi.
        new(HrMentorDefinitionVersion, SystemSettingKind.Hidden, string.Empty, MaxLength: 10),
        new(HrMentorProfileHash, SystemSettingKind.Hidden, string.Empty, MaxLength: 64),

        // Blankalar manbai (SPEC-hujjat-manbai §2) — «platform» yoki «legacy».
        new(HrMentorBlanksSource, SystemSettingKind.Hidden, string.Empty, MaxLength: 10),

        // Jarayonlar (SPEC-mening-ishlarim §6.6): «2026-11-04» — 10 belgi; oxirgi o'tish vaqti — 33 belgi.
        new(ProcessesStartedOn, SystemSettingKind.Hidden, string.Empty, MaxLength: 10),
        new(ProcessesReconcileWatermark, SystemSettingKind.Hidden, string.Empty, MaxLength: 40),
    ];

    /// <summary>
    /// Ekranda KO'RINADIGAN sozlamalar — <see cref="All"/> dan
    /// yashirinlari olib tashlangan holda.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega ikkita ro'yxat.</b> <see cref="All"/> — TEKSHIRUV uchun:
    /// bu yerda yo'q kalit na yoziladi, na o'qiladi. Ekran esa boshqa
    /// savolga javob kutadi — «foydalanuvchi nimani o'zgartira oladi».
    /// <see cref="SystemSettingKind.Hidden"/> turidagi qiymatni server
    /// o'zi yozadi, ya'ni uni ro'yxatga qo'shish foydalanuvchiga hech
    /// narsa bermaydi (sabab o'sha turning izohida).
    /// </para>
    /// <para>
    /// Filtr SHU YERDA, javobni yig'ayotgan joyda emas: aks holda har
    /// bir yangi javob yashirin kalitni chiqarib yuborishi mumkin
    /// bo'lardi va buni hech kim sezmasdi.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<SystemSettingDefinition> Visible { get; } =
    [
        .. All.Where(definition => definition.Kind != SystemSettingKind.Hidden),
    ];

    /*
      «Bot kalitlari» degan ALOHIDA ro'yxat ATAYLAB yo'q.

      Botga sozlamalar kalit-qiymat bo'lib emas, TIPLASHGAN maydonlar
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
