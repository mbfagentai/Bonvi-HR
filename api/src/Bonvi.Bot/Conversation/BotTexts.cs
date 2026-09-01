using System.Globalization;

namespace Bonvi.Bot.Conversation;

/// <summary>
/// Botning barcha matnlari — uch tilda.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega loyihaning `i18n` tizimi ishlatilmadi.</b> U brauzer uchun
/// (JSON fayllar + React hooki) va bu jarayonga umuman yetib bormaydi.
/// Bot esa boshqa loyihalarga havola qilmaydi (izohi
/// <c>Bonvi.Bot.csproj</c> da). Matnlar soni oz, shu sababli ular shu
/// yerda — kalitlarni ikkinchi joyda saqlash ularning eskirishiga olib
/// kelardi.
/// </para>
/// <para>
/// <b>Har bir matn qo'lda yozilgan.</b> Avtomatik o'girish jozibador
/// ko'rinadi, lekin u «ў» va «ғ» kabi harflarda va qo'shma so'zlarda
/// xato qiladi — nomzod ko'radigan birinchi ekranda bunday xato
/// ishonchni yo'qotadi. Solishtirish uchun: MBF ning hozirgi botida
/// aynan shunday xatolar bor («ушбу болими», «оз аризангизни»,
/// «мавжуд иш оринлари») va ular birinchi qarashdayoq ko'zga tashlanadi.
/// </para>
/// <para>
/// ⚠️ <b>Har bir <c>switch</c> da uchala til ham ALOHIDA yozilgan.</b>
/// <c>_ =></c> tarmog'i faqat lotinni bildiradi. Ruscha uchun alohida
/// tarmoq yozilmasa, u jimgina lotincha matn ko'rsatib qo'yardi va
/// kompilyator bunday xatoni ushlay olmaydi.
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
    /// qiymatlar — vakansiya nomi va sozlamalardagi matnlar, lekin ularda
    /// <c>&amp;</c> yoki <c>&lt;</c> bo'lsa Telegram BUTUN xabarni rad
    /// etardi va nomzod hech qanday javob olmasdi.
    /// </remarks>
    public static string Escape(string? value)
        => (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    /// <summary>Til tanlash — yagona matn, uchala tilda birga.</summary>
    public const string ChooseLanguage = "Тилни танланг / Tilni tanlang / Выберите язык:";

    public const string LanguageCyrillic = "Ўзбекча (кирилл)";
    public const string LanguageLatin = "O‘zbekcha (lotin)";
    public const string LanguageRussian = "Русский";

    public static string Greeting(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Ассалому алайкум! 👋\nБонви HR га хуш келибсиз.\n\n"
            + "Бу бот орқали очиқ иш ўринларига ариза топширишингиз мумкин.",
        BotLanguage.Russian =>
            "Здравствуйте! 👋\nДобро пожаловать в Bonvi HR.\n\n"
            + "Через этого бота вы можете подать заявку на открытые вакансии.",
        _ =>
            "Assalomu alaykum! 👋\nBonvi HR ga xush kelibsiz.\n\n"
            + "Bu bot orqali ochiq ish o‘rinlariga ariza topshirishingiz mumkin.",
    };

    public static string ChooseVacancy(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "Қайси вакансияга ариза топширмоқчисиз?",
        BotLanguage.Russian => "На какую вакансию вы хотите подать заявку?",
        _ => "Qaysi vakansiyaga ariza topshirmoqchisiz?",
    };

    /// <summary>Ochiq vakansiya yo'q — bu xato emas, odatiy hol.</summary>
    public static string NoVacancies(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Ҳозирча очиқ иш ўрни йўқ. 🙏\n\n"
            + "Кейинроқ яна кириб кўринг — пастдаги менюдан.",
        BotLanguage.Russian =>
            "Пока открытых вакансий нет. 🙏\n\n"
            + "Загляните позже — через меню ниже.",
        _ =>
            "Hozircha ochiq ish o‘rni yo‘q. 🙏\n\n"
            + "Keyinroq yana kirib ko‘ring — pastdagi menyudan.",
    };

    public static string AskName(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Фамилия, исм ва отангизнинг исмини ёзинг.\n\n"
            + "Масалан: <i>Каримов Азиз Акмал ўғли</i>",
        BotLanguage.Russian =>
            "Напишите фамилию, имя и отчество.\n\n"
            + "Например: <i>Каримов Азиз Акмалович</i>",
        _ =>
            "Familiya, ism va otangizning ismini yozing.\n\n"
            + "Masalan: <i>Karimov Aziz Akmal o‘g‘li</i>",
    };

    public static string NameNotUnderstood(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Камида фамилия ва исм керак. Илтимос, тўлиқ ёзинг.\n\n"
            + "Масалан: <i>Каримов Азиз Акмал ўғли</i>",
        BotLanguage.Russian =>
            "Нужны как минимум фамилия и имя. Пожалуйста, напишите полностью.\n\n"
            + "Например: <i>Каримов Азиз Акмалович</i>",
        _ =>
            "Kamida familiya va ism kerak. Iltimos, to‘liq yozing.\n\n"
            + "Masalan: <i>Karimov Aziz Akmal o‘g‘li</i>",
    };

    public static string AskPhone(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Раҳмат! Энди телефон рақамингизни юборинг.\n\n"
            + "Пастдаги тугмани босинг — рақам ўзи юборилади.",
        BotLanguage.Russian =>
            "Спасибо! Теперь отправьте номер телефона.\n\n"
            + "Нажмите кнопку ниже — номер отправится сам.",
        _ =>
            "Rahmat! Endi telefon raqamingizni yuboring.\n\n"
            + "Pastdagi tugmani bosing — raqam o‘zi yuboriladi.",
    };

    public static string PhoneButton(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "📱 Рақамни юбориш",
        BotLanguage.Russian => "📱 Отправить номер",
        _ => "📱 Raqamni yuborish",
    };

    /// <summary>Raqam qo'lda yozilganda — tugmani bosish so'raladi.</summary>
    public static string PhoneButtonOnly(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Илтимос, рақамни қўлда ёзманг — пастдаги тугмани босинг.\n"
            + "Шунда рақам хатосиз тушади.",
        BotLanguage.Russian =>
            "Пожалуйста, не пишите номер вручную — нажмите кнопку ниже.\n"
            + "Так номер придёт без ошибок.",
        _ =>
            "Iltimos, raqamni qo‘lda yozmang — pastdagi tugmani bosing.\n"
            + "Shunda raqam xatosiz tushadi.",
    };

    /// <summary>Boshqa odamning kontakti yuborilganda.</summary>
    public static string OwnPhoneOnly(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Бу бошқа одамнинг рақами. Илтимос, ўз рақамингизни юборинг.",
        BotLanguage.Russian =>
            "Это номер другого человека. Пожалуйста, отправьте свой номер.",
        _ =>
            "Bu boshqa odamning raqami. Iltimos, o‘z raqamingizni yuboring.",
    };

    /// <summary>Manzil so'raladi — telefon raqamidan keyingi oxirgi qadam.</summary>
    /// <remarks>
    /// <para>
    /// <b>Nega manzil so'raladi.</b> Korxona Rishtonda va nomzodlarning
    /// bir qismi uzoq tumanlardan keladi. HR birinchi qo'ng'iroqda aynan
    /// shuni so'rardi — endi u arizaning o'zida turadi va suhbat qisqaradi.
    /// </para>
    /// <para>
    /// <b>Nega tuman darajasida yetarli.</b> Aniq ko'cha va uy raqami
    /// ishga qabul qaroriga ta'sir qilmaydi, lekin uni yozish anketani
    /// uzaytiradi va nomzodni qo'rqitadi. Namuna ataylab qisqa berilgan.
    /// </para>
    /// </remarks>
    public static string AskAddress(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Раҳмат! Охирги савол: яшаш манзилингизни ёзинг.\n\n"
            + "Масалан: <i>Риштон тумани, Работ МФЙ</i>",
        BotLanguage.Russian =>
            "Спасибо! Последний вопрос: напишите адрес проживания.\n\n"
            + "Например: <i>Риштанский район, МФЙ Работ</i>",
        _ =>
            "Rahmat! Oxirgi savol: yashash manzilingizni yozing.\n\n"
            + "Masalan: <i>Rishton tumani, Rabot MFY</i>",
    };

    /// <summary>Manzil juda qisqa yoki tushunarsiz.</summary>
    /// <remarks>
    /// Tekshiruv ATAYLAB yumshoq — faqat uzunlik. «Tuman nomi bormi»
    /// kabi qoida haqiqiy manzilni («Работ МФЙ, 12-уй») rad etardi va
    /// nomzod nima yozishni bilmay ketib qolardi.
    /// </remarks>
    public static string AddressNotUnderstood(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Манзил жуда қисқа. Камида туман ёки шаҳар номини ёзинг.\n\n"
            + "Масалан: <i>Риштон тумани, Работ МФЙ</i>",
        BotLanguage.Russian =>
            "Адрес слишком короткий. Напишите хотя бы район или город.\n\n"
            + "Например: <i>Риштанский район, МФЙ Работ</i>",
        _ =>
            "Manzil juda qisqa. Kamida tuman yoki shahar nomini yozing.\n\n"
            + "Masalan: <i>Rishton tumani, Rabot MFY</i>",
    };

    public static string Accepted(BotLanguage language, string vacancy) => language switch
    {
        BotLanguage.Cyrillic =>
            $"✅ Аризангиз қабул қилинди!\n\nВакансия: <b>{Escape(vacancy)}</b>\n\n"
            + "ХР бўлими кўриб чиқади ва сиз билан телефон орқали боғланади.",
        BotLanguage.Russian =>
            $"✅ Ваша заявка принята!\n\nВакансия: <b>{Escape(vacancy)}</b>\n\n"
            + "Отдел кадров рассмотрит её и свяжется с вами по телефону.",
        _ =>
            $"✅ Arizangiz qabul qilindi!\n\nVakansiya: <b>{Escape(vacancy)}</b>\n\n"
            + "HR bo‘limi ko‘rib chiqadi va siz bilan telefon orqali bog‘lanadi.",
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
                + "Бошқа вакансияни танлашингиз мумкин.",
            BotLanguage.Russian =>
                $"⚠️ Вы уже подавали заявку на вакансию «{Escape(vacancy)}» {date}.\n\n"
                + (stage is null ? string.Empty : $"Текущий статус: <b>{Escape(stage)}</b>\n\n")
                + "Вы можете выбрать другую вакансию.",
            _ =>
                $"⚠️ Siz «{Escape(vacancy)}» vakansiyasiga {date} da ariza topshirgansiz.\n\n"
                + (stage is null ? string.Empty : $"Hozirgi holat: <b>{Escape(stage)}</b>\n\n")
                + "Boshqa vakansiyani tanlashingiz mumkin.",
        };
    }

    /// <summary>Server javob bermadi — ariza YO'QOLDI deb aytilmaydi.</summary>
    public static string Failed(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Кечирасиз, ариза юборилмади. 😔\n\n"
            + "Бироздан кейин яна уриниб кўринг.",
        BotLanguage.Russian =>
            "Извините, заявку отправить не удалось. 😔\n\n"
            + "Попробуйте ещё раз чуть позже.",
        _ =>
            "Kechirasiz, ariza yuborilmadi. 😔\n\n"
            + "Birozdan keyin yana urinib ko‘ring.",
    };

    /// <summary>Bosh menyu savoli.</summary>
    public static string MenuPrompt(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "Нима қилмоқчисиз?",
        BotLanguage.Russian => "Что вы хотите сделать?",
        _ => "Nima qilmoqchisiz?",
    };

    /// <summary>Menyudagi «Ariza topshirish» tugmasi.</summary>
    public static string ApplyButton(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "📝 Ариза топшириш",
        BotLanguage.Russian => "📝 Подать заявку",
        _ => "📝 Ariza topshirish",
    };

    /// <summary>Menyudagi «Kompaniya haqida» tugmasi.</summary>
    public static string AboutButton(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "ℹ️ Компания ҳақида",
        BotLanguage.Russian => "ℹ️ О компании",
        _ => "ℹ️ Kompaniya haqida",
    };

    /// <summary>Menyudagi «Kontaktlar / Manzil» tugmasi.</summary>
    public static string ContactsButton(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "📞 Контактлар / Манзил",
        BotLanguage.Russian => "📞 Контакты / Адрес",
        _ => "📞 Kontaktlar / Manzil",
    };

    /// <summary>Menyudagi til almashtirish tugmasi.</summary>
    /// <remarks>
    /// Matn UCHALA tilda ham bir xil: nomzod tilni almashtirmoqchi
    /// bo'lganda u hozirgi tilni tushunmayotgan bo'lishi mumkin —
    /// aynan shuning uchun tugmani izlayapti. Uchala yozuv birga
    /// turgani uni so'zdan ko'ra tez tanitadi.
    /// </remarks>
    public const string LanguageButton = "🌐 Til / Тил / Язык";

    /// <summary>Instagram tugmasi.</summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Emoji ATAYLAB YO'Q va uni qo'shishga urinmang.</b> Telegram
    /// tugmasining yorlig'i — faqat MATN: unga rasm ham, maxsus emoji
    /// ham qo'yib bo'lmaydi (bu Bot API ning cheklovi, bizning
    /// tanlovimiz emas). Unicode'da esa Instagram va Facebook'ning
    /// emojisi umuman yo'q.
    /// </para>
    /// <para>
    /// Ilgari bu yerda o'xshatib olingan emojilar turardi — 📷, ✈️, 👍.
    /// Ular brendni ko'rsatmasdi, aksincha: fotoapparat, samolyot va
    /// «yoqdi» barmog'i tugmalarni havaskorona qilib ko'rsatardi.
    /// Brend nomining O'ZI esa allaqachon tanish belgi — u hech qanday
    /// ikonaga muhtoj emas.
    /// </para>
    /// <para>
    /// Nomlar TARJIMA ham QILINMAYDI: ular uchala tilda bir xil
    /// yoziladi va «Инстаграм» deb yozish tanish yozuvni tanimas
    /// qilib qo'yardi.
    /// </para>
    /// </remarks>
    public const string InstagramButton = "Instagram";

    /// <summary>Telegram kanali tugmasi.</summary>
    public const string TelegramButton = "Telegram";

    /// <summary>Facebook tugmasi.</summary>
    public const string FacebookButton = "Facebook";

    /// <summary>
    /// Sayt tugmasi — yagona tarjima qilinadigani va yagona ikonalisi.
    /// </summary>
    /// <remarks>
    /// Globus bu yerda BRENDNI emas, TUSHUNCHANI bildiradi: «bu
    /// kompaniyaning sayti». Shu sababli u qoladi — yuqoridagi uchtasi
    /// bilan farqi aynan shunda.
    /// </remarks>
    public static string WebsiteButton(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "🌐 Сайт",
        BotLanguage.Russian => "🌐 Сайт",
        _ => "🌐 Sayt",
    };

    /// <summary>
    /// Rad etish xabari — nomzodga avtomatik boradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>SABAB YOZILMAYDI</b> (2026-08-31, foydalanuvchi qarori).
    /// HR rad etishda yozadigan izoh ICHKI maydon: unga «tajribasi
    /// yetarli emas» kabi baho yoziladi va u nomzodga borishi
    /// mo'ljallanmagan. Bunday matnni to'g'ridan-to'g'ri yuborish HR
    /// ni har bir izohni «nomzod o'qiydi» deb yozishga majburlardi va
    /// izohning ichki qiymatini yo'qotardi.
    /// </para>
    /// <para>
    /// <b>Ohangi ataylab iliq va yopiq.</b> Xabar javobsizlikni
    /// tugatadi — nomzod kutishdan qutuladi. Oxiridagi taklif esa
    /// bo'sh muomala emas: MBF doimiy ravishda ishga oladi va
    /// bugun to'g'ri kelmagan odam olti oydan keyin to'g'ri kelishi
    /// mumkin.
    /// </para>
    /// </remarks>
    public static string RejectionNotice(BotLanguage language, string fullName, string vacancy) => language switch
    {
        BotLanguage.Cyrillic =>
            $"Ҳурматли {Escape(fullName)},\n\n"
            + $"«{Escape(vacancy)}» вакансияси бўйича аризангиз кўриб чиқилди. "
            + "Афсуски, бу сафар сизнинг номзодингиз танланмади.\n\n"
            + "Вақтингиз ва қизиқишингиз учун раҳмат. Янги иш ўринлари очилганда "
            + "яна ариза топширишингиз мумкин.",
        BotLanguage.Russian =>
            $"Уважаемый(ая) {Escape(fullName)},\n\n"
            + $"Ваша заявка на вакансию «{Escape(vacancy)}» рассмотрена. "
            + "К сожалению, в этот раз ваша кандидатура не была выбрана.\n\n"
            + "Благодарим за уделённое время и интерес к нашей компании. "
            + "Вы можете подать заявку снова, когда откроются новые вакансии.",
        _ =>
            $"Hurmatli {Escape(fullName)},\n\n"
            + $"«{Escape(vacancy)}» vakansiyasi bo‘yicha arizangiz ko‘rib chiqildi. "
            + "Afsuski, bu safar sizning nomzodingiz tanlanmadi.\n\n"
            + "Vaqtingiz va qiziqishingiz uchun rahmat. Yangi ish o‘rinlari ochilganda "
            + "yana ariza topshirishingiz mumkin.",
    };

    /// <summary>Suhbat unutilgan yoki tushunilmagan xabar.</summary>
    public static string Restart(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "Пастдаги менюдан керакли бўлимни танланг.",
        BotLanguage.Russian => "Выберите нужный раздел в меню ниже.",
        _ => "Pastdagi menyudan kerakli bo‘limni tanlang.",
    };
}
