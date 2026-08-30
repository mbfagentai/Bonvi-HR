using System.Globalization;

namespace Bonvi.Bot.Conversation;

/// <summary>
/// Botning barcha matnlari — ikki alifboda.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega loyihaning `i18n` tizimi ishlatilmadi.</b> U brauzer uchun
/// (JSON fayllar + React hooki) va bu jarayonga umuman yetib bormaydi.
/// Bot esa boshqa loyihalarga havola qilmaydi (izohi
/// <c>Bonvi.Bot.csproj</c> da). Matnlar soni yigirmadan kam, shu sababli
/// ular shu yerda — kalitlarni ikkinchi joyda saqlash ularning
/// eskirishiga olib kelardi.
/// </para>
/// <para>
/// <b>Har bir matn ikkala alifboda ham qo'lda yozilgan.</b> Avtomatik
/// o'girish jozibador ko'rinadi, lekin u «ў» va «ғ» kabi harflarda va
/// qo'shma so'zlarda xato qiladi — nomzod ko'radigan birinchi ekranda
/// bunday xato ishonchni yo'qotadi.
/// </para>
/// </remarks>
public static class BotTexts
{
    /// <summary>
    /// Matnni HTML uchun xavfsiz qiladi.
    /// </summary>
    /// <remarks>
    /// Xabarlar <c>parse_mode=HTML</c> bilan yuboriladi (izohi
    /// <c>TelegramClient.SendMessageAsync</c> da). Tashqaridan tushadigan
    /// yagona qiymat — vakansiya nomi, lekin unda <c>&amp;</c> yoki
    /// <c>&lt;</c> bo'lsa Telegram BUTUN xabarni rad etardi va nomzod
    /// hech qanday javob olmasdi.
    /// </remarks>
    public static string Escape(string? value)
        => (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    /// <summary>Til tanlash — yagona matn, ikkala alifboda birga.</summary>
    public const string ChooseLanguage = "Тилни танланг / Tilni tanlang:";

    public const string LanguageCyrillic = "Ўзбекча (кирилл)";
    public const string LanguageLatin = "O‘zbekcha (lotin)";

    public static string Greeting(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Ассалому алайкум! 👋\nБонви HR га хуш келибсиз.\n\n"
            + "Бу бот орқали очиқ иш ўринларига ариза топширишингиз мумкин.",
        _ =>
            "Assalomu alaykum! 👋\nBonvi HR ga xush kelibsiz.\n\n"
            + "Bu bot orqali ochiq ish o‘rinlariga ariza topshirishingiz mumkin.",
    };

    public static string ChooseVacancy(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "Қайси вакансияга ариза топширмоқчисиз?",
        _ => "Qaysi vakansiyaga ariza topshirmoqchisiz?",
    };

    /// <summary>Ochiq vakansiya yo'q — bu xato emas, odatiy hol.</summary>
    public static string NoVacancies(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Ҳозирча очиқ иш ўрни йўқ. 🙏\n\n"
            + "Кейинроқ /start буйруғи билан яна уриниб кўринг.",
        _ =>
            "Hozircha ochiq ish o‘rni yo‘q. 🙏\n\n"
            + "Keyinroq /start buyrug‘i bilan yana urinib ko‘ring.",
    };

    public static string AskName(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Фамилия, исм ва отангизнинг исмини ёзинг.\n\n"
            + "Масалан: <i>Каримов Азиз Акмал ўғли</i>",
        _ =>
            "Familiya, ism va otangizning ismini yozing.\n\n"
            + "Masalan: <i>Karimov Aziz Akmal o‘g‘li</i>",
    };

    public static string NameNotUnderstood(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Камида фамилия ва исм керак. Илтимос, тўлиқ ёзинг.\n\n"
            + "Масалан: <i>Каримов Азиз Акмал ўғли</i>",
        _ =>
            "Kamida familiya va ism kerak. Iltimos, to‘liq yozing.\n\n"
            + "Masalan: <i>Karimov Aziz Akmal o‘g‘li</i>",
    };

    public static string AskPhone(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Раҳмат! Энди телефон рақамингизни юборинг.\n\n"
            + "Пастдаги тугмани босинг — рақам ўзи юборилади.",
        _ =>
            "Rahmat! Endi telefon raqamingizni yuboring.\n\n"
            + "Pastdagi tugmani bosing — raqam o‘zi yuboriladi.",
    };

    public static string PhoneButton(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "📱 Рақамни юбориш",
        _ => "📱 Raqamni yuborish",
    };

    /// <summary>Raqam qo'lda yozilganda — tugmani bosish so'raladi.</summary>
    public static string PhoneButtonOnly(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Илтимос, рақамни қўлда ёзманг — пастдаги тугмани босинг.\n"
            + "Шунда рақам хатосиз тушади.",
        _ =>
            "Iltimos, raqamni qo‘lda yozmang — pastdagi tugmani bosing.\n"
            + "Shunda raqam xatosiz tushadi.",
    };

    /// <summary>Boshqa odamning kontakti yuborilganda.</summary>
    public static string OwnPhoneOnly(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Бу бошқа одамнинг рақами. Илтимос, ўз рақамингизни юборинг.",
        _ =>
            "Bu boshqa odamning raqami. Iltimos, o‘z raqamingizni yuboring.",
    };

    public static string Accepted(BotLanguage language, string vacancy) => language switch
    {
        BotLanguage.Cyrillic =>
            $"✅ Аризангиз қабул қилинди!\n\nВакансия: <b>{Escape(vacancy)}</b>\n\n"
            + "ХР бўлими кўриб чиқади ва сиз билан телефон орқали боғланади.\n"
            + "Янги ариза учун: /start",
        _ =>
            $"✅ Arizangiz qabul qilindi!\n\nVakansiya: <b>{Escape(vacancy)}</b>\n\n"
            + "HR bo‘limi ko‘rib chiqadi va siz bilan telefon orqali bog‘lanadi.\n"
            + "Yangi ariza uchun: /start",
    };

    /// <summary>Takroriy ariza — holati bilan birga.</summary>
    public static string AlreadyApplied(
        BotLanguage language,
        string vacancy,
        DateTimeOffset appliedAt,
        string? stage)
    {
        var date = appliedAt.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        return language switch
        {
            BotLanguage.Cyrillic =>
                $"⚠️ Сиз «{Escape(vacancy)}» вакансиясига {date} да ариза топширгансиз.\n\n"
                + (stage is null ? string.Empty : $"Ҳозирги ҳолат: <b>{Escape(stage)}</b>\n\n")
                + "Бошқа вакансияни танлашингиз мумкин: /start",
            _ =>
                $"⚠️ Siz «{Escape(vacancy)}» vakansiyasiga {date} da ariza topshirgansiz.\n\n"
                + (stage is null ? string.Empty : $"Hozirgi holat: <b>{Escape(stage)}</b>\n\n")
                + "Boshqa vakansiyani tanlashingiz mumkin: /start",
        };
    }

    /// <summary>Server javob bermadi — ariza YO'QOLDI deb aytilmaydi.</summary>
    public static string Failed(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Кечирасиз, ариза юборилмади. 😔\n\n"
            + "Бироздан кейин /start билан яна уриниб кўринг.",
        _ =>
            "Kechirasiz, ariza yuborilmadi. 😔\n\n"
            + "Birozdan keyin /start bilan yana urinib ko‘ring.",
    };

    /// <summary>Bosh menyu savoli.</summary>
    public static string MenuPrompt(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "Нима қилмоқчисиз?",
        _ => "Nima qilmoqchisiz?",
    };

    /// <summary>Bosh menyudagi «Kompaniya haqida» tugmasi.</summary>
    public static string AboutButton(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "ℹ️ Компания ҳақида",
        _ => "ℹ️ Kompaniya haqida",
    };

    /// <summary>Bosh menyudagi «Ariza topshirish» tugmasi.</summary>
    public static string ApplyButton(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "📝 Ариза топшириш",
        _ => "📝 Ariza topshirish",
    };

    /// <summary>Kompaniya matnidan keyingi qaytish taklifi.</summary>
    public static string AfterAbout(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "\n\nАриза топшириш учун: /start",
        _ => "\n\nAriza topshirish uchun: /start",
    };

    /// <summary>Suhbat unutilgan yoki tushunilmagan xabar.</summary>
    public static string Restart(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "Аризани бошидан бошлаш учун: /start",
        _ => "Arizani boshidan boshlash uchun: /start",
    };
}
