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

    /*
      ⚠️ «Ochiq vakansiya yo'q» MATNI OLIB TASHLANDI va uni qaytarmang.

      Ilgari bot shu xabar bilan suhbatni tugatardi. Endi ochiq ish o'rni
      bo'lmasligi anketani TO'XTATMAYDI: vakansiya qadami jimgina
      o'tkazib yuboriladi va nomzod to'liq anketani to'ldiradi — ariza
      esa serverda «Umumiy anketa»ga tushadi (SPEC-anketa, 3-bo'lim).
      Nomzod bu haqda faqat OXIRIDA, `AcceptedToGeneralPool` xabarida
      biladi; boshida aytilsa u anketani umuman boshlamasdi.
    */

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

    /// <summary>
    /// Telegram hisobining raqami O'zbekiston raqami emas — uni QO'LDA
    /// yozish so'raladi.
    /// </summary>
    /// <remarks>
    /// Odatda raqamni qo'lda yozish qabul qilinmaydi (<see cref="PhoneButtonOnly"/>):
    /// terish xatosi arizani qidirib bo'lmaydigan qiladi. Bu yagona
    /// istisno — boshqa yo'l bilan bunday nomzod umuman ariza bera
    /// olmasdi (server faqat +998 ni qabul qiladi).
    /// </remarks>
    public static string ForeignPhone(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Бу рақам Ўзбекистон рақами эмас. Биз фақат +998 рақамларни қабул қиламиз.\n\n"
            + "Илтимос, Ўзбекистондаги рақамингизни ёзиб юборинг, масалан: +998 90 123 45 67",
        BotLanguage.Russian =>
            "Это не узбекский номер. Мы принимаем только номера +998.\n\n"
            + "Пожалуйста, напишите ваш номер в Узбекистане, например: +998 90 123 45 67",
        _ =>
            "Bu raqam O‘zbekiston raqami emas. Biz faqat +998 raqamlarni qabul qilamiz.\n\n"
            + "Iltimos, O‘zbekistondagi raqamingizni yozib yuboring, masalan: +998 90 123 45 67",
    };

    /// <summary>Qo'lda yozilgan raqam noto'g'ri.</summary>
    public static string PhoneFormat(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Рақам нотўғри. Ўзбекистон рақамини тўлиқ ёзинг, масалан: +998 90 123 45 67",
        BotLanguage.Russian =>
            "Номер неверный. Напишите полный узбекский номер, например: +998 90 123 45 67",
        _ =>
            "Raqam noto‘g‘ri. O‘zbekiston raqamini to‘liq yozing, masalan: +998 90 123 45 67",
    };

    /// <summary>Manzil so'raladi — anketaning 5-qadami.</summary>
    /// <remarks>
    /// <para>
    /// ⚠️ Matnda «oxirgi savol» degan gap ATAYLAB YO'Q: u ilgari bor
    /// edi, chunki anketa manzil bilan tugardi. Endi undan keyin yana
    /// o'n yettita savol bor va eski va'da nomzodni aldab qo'yardi.
    /// </para>
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
            "🏠 Яшаш манзилингизни ёзинг.\n\n"
            + "Масалан: <i>Риштон тумани, Работ МФЙ</i>",
        BotLanguage.Russian =>
            "🏠 Напишите адрес проживания.\n\n"
            + "Например: <i>Риштанский район, МФЙ Работ</i>",
        _ =>
            "🏠 Yashash manzilingizni yozing.\n\n"
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
            + "Бўш иш ўрнимизга қизиққанингиз учун ташаккур. Анкетангиз кадрлар "
            + "бўлимига юборилди — тез орада сиз билан боғланамиз.\n\n"
            + Signature(language),
        BotLanguage.Russian =>
            $"✅ Ваша заявка принята!\n\nВакансия: <b>{Escape(vacancy)}</b>\n\n"
            + "Спасибо за проявленный интерес. Ваша анкета передана в отдел "
            + "кадров — в скором времени мы с Вами свяжемся.\n\n"
            + Signature(language),
        _ =>
            $"✅ Arizangiz qabul qilindi!\n\nVakansiya: <b>{Escape(vacancy)}</b>\n\n"
            + "Bo‘sh ish o‘rnimizga qiziqqaningiz uchun tashakkur. Anketangiz "
            + "kadrlar bo‘limiga yuborildi — tez orada siz bilan bog‘lanamiz.\n\n"
            + Signature(language),
    };

    /// <summary>
    /// «Umumiy anketa»ga tushgan nomzodga — vakansiya nomi O'RNIGA.
    /// </summary>
    /// <remarks>
    /// ⭐ <b>Nega alohida matn.</b> Ochiq ish o'rni bo'lmaganda arizada
    /// vakansiya YO'Q va uning o'rniga bo'sh joy qoldirib bo'lmaydi.
    /// «Hozircha ochiq vakansiya yo'q» degan quruq gap esa nomzodga
    /// «arizangiz rad etildi» bo'lib tuyulardi — shu sababli xabar
    /// anketa SAQLANGANINI va HR o'zi bog'lanishini aniq aytadi.
    /// </remarks>
    public static string AcceptedToGeneralPool(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "✅ Анкетангиз қабул қилинди!\n\n"
            + "Ҳозирча очиқ иш ўрни йўқ, лекин анкетангиз базада сақланади. "
            + "Мос вакансия чиққанда HR бўлими сиз билан боғланади.",
        BotLanguage.Russian =>
            "✅ Ваша анкета принята!\n\n"
            + "Сейчас открытых вакансий нет, но анкета сохранится в базе. "
            + "Когда появится подходящая вакансия, отдел кадров свяжется с вами.",
        _ =>
            "✅ Anketangiz qabul qilindi!\n\n"
            + "Hozircha ochiq ish o‘rni yo‘q, lekin anketangiz bazada saqlanadi. "
            + "Mos vakansiya chiqqanda HR bo‘limi siz bilan bog‘lanadi.",
    };

    /// <summary>
    /// Takroriy ariza — oldingisi HALI KO'RILAYOTGANDA.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Sana TOSHKENT vaqtida chiziladi</b> (<see cref="BotClock"/>).
    /// API qiymatni UTC da qaytaradi va siljitilmagan sana soat 19:00 dan
    /// keyin topshirilgan arizani KECHAGI kun deb ko'rsatardi — nomzod
    /// esa buni «bu men emasman» deb o'qirdi.
    /// </para>
    /// <para>
    /// ⚠️ <b>«Boshqa vakansiyani tanlang» degan gap OLIB TASHLANDI</b>
    /// (2026-09-08). To'siq endi ODAMGA qo'yiladi, arizaga emas: server
    /// vakansiya bo'yicha ajratmaydi (<c>CheckApplicationQuery</c>) va
    /// nomzod boshqa o'rinni tanlab kelganda ayni shu telefon qadamida
    /// yana to'silardi. Ya'ni matn uni aylanma yo'lga solib qo'yardi.
    /// O'rniga aniq va yopiq gap: ariza ko'rilmoqda, javobni o'zimiz
    /// aytamiz.
    /// </para>
    /// <para>
    /// Bosqich esa QOLDI va bu <see cref="ReapplyLater"/> dan farqi:
    /// bu yerda ariza HAQIQATAN davom etyapti, u yerda esa allaqachon
    /// yopilgan.
    /// </para>
    /// </remarks>
    public static string AlreadyApplied(
        BotLanguage language,
        string vacancy,
        DateTimeOffset appliedAt,
        string? stage)
    {
        var date = BotClock.Local(appliedAt).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        return language switch
        {
            BotLanguage.Cyrillic =>
                $"⚠️ Сиз «{Escape(vacancy)}» вакансиясига {date} да ариза топширгансиз.\n\n"
                + (stage is null ? string.Empty : $"Ҳозирги ҳолат: <b>{Escape(stage)}</b>\n\n")
                + "Аризангиз кўриб чиқилмоқда — натижа ҳақида ўзимиз хабар берамиз.",
            BotLanguage.Russian =>
                $"⚠️ Вы уже подавали заявку на вакансию «{Escape(vacancy)}» {date}.\n\n"
                + (stage is null ? string.Empty : $"Текущий статус: <b>{Escape(stage)}</b>\n\n")
                + "Заявка на рассмотрении — о результате мы сообщим сами.",
            _ =>
                $"⚠️ Siz «{Escape(vacancy)}» vakansiyasiga {date} da ariza topshirgansiz.\n\n"
                + (stage is null ? string.Empty : $"Hozirgi holat: <b>{Escape(stage)}</b>\n\n")
                + "Arizangiz ko\u2018rib chiqilmoqda — natija haqida o\u2018zimiz xabar beramiz.",
        };
    }

    /// <summary>
    /// Qayta ariza muddati hali tugamagan — qachondan keyin mumkinligi
    /// bilan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega <see cref="AlreadyApplied"/> yaramaydi.</b> U bosqichni
    /// ko'rsatadi va oxirida «boshqa vakansiyani tanlashingiz mumkin»
    /// deydi. Muddat esa ODAMGA qo'yiladi, arizaga emas: server
    /// vakansiya bo'yicha ajratmaydi va nomzod boshqa o'rinni tanlab
    /// kelganda ayni shu joyda yana to'siladi. Ya'ni eski matn uni
    /// aylanma yo'lga solib qo'yardi. Shu sababli bu yerda BITTA aniq
    /// gap bor: qaysi sanadan keyin qayta urinish mumkin.
    /// </para>
    /// <para>
    /// <b>Bosqich ATAYLAB aytilmaydi.</b> Ariza allaqachon yopilgan va
    /// «Ҳозирги ҳолат: Анкета» degan qator nomzodga jarayon hali
    /// davom etyapti degan yolg'on umid berardi.
    /// </para>
    /// <para>
    /// ⚠️ <b>Ikkita sana ikki xil qoidada chiziladi.</b>
    /// <paramref name="appliedAt"/> — LAHZA, u Toshkent vaqtiga
    /// keltiriladi (<see cref="BotClock"/>, sabab
    /// <see cref="AlreadyApplied"/> da). <paramref name="availableFrom"/>
    /// esa <c>DateOnly</c>: unga vaqt mintaqasi umuman tegishli emas va
    /// siljitish uni bir kunga surib yuborardi.
    /// </para>
    /// </remarks>
    /// <param name="language">Nomzod tanlagan til.</param>
    /// <param name="appliedAt">Oldingi ariza qachon topshirilgan.</param>
    /// <param name="availableFrom">Qaysi sanadan keyin qayta topshirsa bo'ladi.</param>
    public static string ReapplyLater(
        BotLanguage language,
        DateTimeOffset appliedAt,
        DateOnly availableFrom)
    {
        var applied = BotClock.Local(appliedAt).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        var available = availableFrom.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        return language switch
        {
            BotLanguage.Cyrillic =>
                $"⚠️ Сиз {applied} да ариза топширгансиз ва у кўриб чиқилган.\n\n"
                + $"Янги анкетани <b>{available}</b> дан кейин топширишингиз мумкин.\n\n"
                + Signature(language),
            BotLanguage.Russian =>
                $"⚠️ Вы подавали заявку {applied}, она уже рассмотрена.\n\n"
                + $"Новую анкету можно подать после <b>{available}</b>.\n\n"
                + Signature(language),
            _ =>
                $"⚠️ Siz {applied} da ariza topshirgansiz va u ko‘rib chiqilgan.\n\n"
                + $"Yangi anketani <b>{available}</b> dan keyin topshirishingiz mumkin.\n\n"
                + Signature(language),
        };
    }

    /// <summary>
    /// Ariza qabul qilinmaydi — TAFSILOTSIZ tayanch javob.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Qachon ishlatiladi.</b> Server «yo'q» degan, lekin xabar
    /// yozish uchun kerakli maydonni yubormagan: sana yoki qaytish
    /// muddati yetishmayapti (javob shakli o'zgargan yoki nazariy
    /// nosozlik). Bunday holatda to'siqni jimgina OCHIB YUBORISH
    /// mumkin emas — server «yo'q» degan bo'lsa, javob «yo'q» bo'lib
    /// qoladi, faqat sababsiz.
    /// </para>
    /// <para>
    /// <b>Nega taxmin qilinmaydi.</b> «Olti oydan keyin urinib
    /// ko'ring» deb yozish oson bo'lardi, lekin muddat SOZLAMADA
    /// turadi va botda takrorlansa, HR uni o'zgartirgach nomzodga
    /// yolg'on sana ketardi.
    /// </para>
    /// <para>
    /// <b>Ohang xushmuomala.</b> Nomzod aybdor emas — u shunchaki javob
    /// oladi. Qo'pol matn kompaniya haqidagi fikrni buzardi va u
    /// ijtimoiy tarmoqqa tushardi.
    /// </para>
    /// </remarks>
    public static string ApplicationNotAccepted(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Мурожаатингиз учун ташаккур.\n\n"
            + "Афсуски, ҳозирда аризангизни қабул қила олмаймиз.\n\n"
            + Signature(language),
        BotLanguage.Russian =>
            "Благодарим за обращение.\n\n"
            + "К сожалению, сейчас мы не можем принять вашу заявку.\n\n"
            + Signature(language),
        _ =>
            "Murojaatingiz uchun tashakkur.\n\n"
            + "Afsuski, hozirda arizangizni qabul qila olmaymiz.\n\n"
            + Signature(language),
    };

    /// <summary>
    /// Server hozir javob bermayapti — bot kutib, qayta urinmoqda.
    /// </summary>
    /// <remarks>
    /// Odatda bu deploy paytidagi bir necha soniyalik uzilish. Xabar
    /// BIR MARTA, birinchi muvaffaqiyatsiz urinishdan keyin yuboriladi:
    /// jimlik nomzodni «Yuborish» ni qayta-qayta bosishga majburlardi.
    /// </remarks>
    public static string Submitting(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "⏳ Аризангиз юборилмоқда… Бир оз кутинг.",
        BotLanguage.Russian => "⏳ Отправляем вашу заявку… Подождите немного.",
        _ => "⏳ Arizangiz yuborilmoqda… Bir oz kuting.",
    };

    /// <summary>
    /// Tanlangan vakansiya anketa to'ldirilayotganda yopilgan — ariza
    /// «Umumiy anketa»ga qabul qilindi.
    /// </summary>
    /// <remarks>
    /// Suhbat endi bir sutka yashaydi va HR vakansiyani bir bosishda
    /// yopadi — ya'ni bu holat kamdan-kam emas. Nomzodning mehnati
    /// bekorga ketmasligi kerak: anketa bazaga tushadi, u buni biladi.
    /// </remarks>
    public static string AcceptedVacancyClosed(BotLanguage language, string vacancy) => language switch
    {
        BotLanguage.Cyrillic =>
            $"✅ Анкетангиз қабул қилинди!\n\n«{Escape(vacancy)}» вакансияси сиз анкета тўлдираётган пайтда ёпилди. "
            + "Анкетангиз базада сақланади — мос иш ўрни чиққанда HR бўлими сиз билан боғланади.\n\n"
            + Signature(language),
        BotLanguage.Russian =>
            $"✅ Ваша анкета принята!\n\nВакансия «{Escape(vacancy)}» закрылась, пока вы заполняли анкету. "
            + "Анкета сохранится в базе — когда появится подходящее место, отдел кадров свяжется с вами.\n\n"
            + Signature(language),
        _ =>
            $"✅ Anketangiz qabul qilindi!\n\n«{Escape(vacancy)}» vakansiyasi siz anketa to‘ldirayotgan paytda yopildi. "
            + "Anketangiz bazada saqlanadi — mos ish o‘rni chiqqanda HR bo‘limi siz bilan bog‘lanadi.\n\n"
            + Signature(language),
    };

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
    /// <summary>
    /// Menyudagi «Vakansiyalar» tugmasi.
    /// </summary>
    /// <remarks>
    /// ⚠️ Ilgari bu yerda «Ariza topshirish» turardi va u bosilishi
    /// bilan anketa boshlanardi. Foydalanuvchi qarori (2026-09-07):
    /// menyudan avval OCHIQ O'RINLAR ko'rsatilsin, ariza esa nomzod
    /// aniq bir vakansiyani tanlagandan KEYIN taklif qilinsin. Sabab:
    /// «ariza topshirish» hali qaysi o'ringa ekani noma'lum bo'lganda
    /// ma'nosiz va nomzodni ro'yxatdan birini tanlashga majburlab
    /// qo'yardi.
    /// </remarks>
    public static string VacanciesButton(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "💼 Вакансиялар",
        BotLanguage.Russian => "💼 Вакансии",
        _ => "💼 Vakansiyalar",
    };

    /// <summary>
    /// Vakansiya tanlangandan keyingi «Ariza topshirish» tugmasi.
    /// </summary>
    /// <remarks>
    /// Bu MENYU tugmasi emas, xabar ostidagi tugma: u aynan bitta
    /// vakansiyaga tegishli va o'sha xabar bilan birga yashaydi.
    /// </remarks>
    public static string ApplyButton(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "📝 Ариза топшириш",
        BotLanguage.Russian => "📝 Подать заявку",
        _ => "📝 Ariza topshirish",
    };

    /// <summary>Tanlangan vakansiya — ariza berishdan oldingi tasdiq.</summary>
    public static string VacancyChosen(BotLanguage language, string vacancy) => language switch
    {
        BotLanguage.Cyrillic =>
            $"💼 <b>{Escape(vacancy)}</b>\n\nШу иш ўрнига ариза топширасизми?",
        BotLanguage.Russian =>
            $"💼 <b>{Escape(vacancy)}</b>\n\nПодать заявку на эту вакансию?",
        _ =>
            $"💼 <b>{Escape(vacancy)}</b>\n\nShu ish o‘rniga ariza topshirasizmi?",
    };

    /// <summary>
    /// Ochiq ish o'rni yo'q — lekin bu YO'L OXIRI emas.
    /// </summary>
    /// <remarks>
    /// Xabar albatta anketaga yo'naltiradi: «vakansiya yo'q» degan
    /// quruq javob nomzodni botdan ketkazardi, holbuki uning
    /// ma'lumotini olish HR uchun aynan shu holatda qimmatli.
    /// </remarks>
    public static string NoOpenVacancies(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Ҳозирча очиқ иш ўрни йўқ. 🙁\n\n«📋 Анкета тўлдириш» тугмасини боссангиз, "
            + "маълумотингиз базада сақланади ва мос вакансия чиққанда HR бўлими сиз билан боғланади.",
        BotLanguage.Russian =>
            "Сейчас открытых вакансий нет. 🙁\n\nНажмите «📋 Заполнить анкету» — ваши данные "
            + "сохранятся в базе, и когда появится подходящая вакансия, отдел кадров свяжется с вами.",
        _ =>
            "Hozircha ochiq ish o‘rni yo‘q. 🙁\n\n«📋 Anketa to‘ldirish» tugmasini bossangiz, "
            + "ma’lumotingiz bazada saqlanadi va mos vakansiya chiqqanda HR bo‘limi siz bilan bog‘lanadi.",
    };

    /*
      «UMUMIY ANKETA» YOPIQ (egasining qarori, 2026-10-05): qaysi vakansiya
      yopiq bo'lsa, unga anketa topshirib bo'lmaydi. Nomzodga bu anketa
      BOSHIDA aytiladi — 22 ta savoldan keyin emas (2026-09 da aynan shu
      jimgina 67 ta arizani yo'qotgan).
    */

    /// <summary>
    /// «📋 Anketa to'ldirish» bosildi, lekin «Umumiy anketa» yopiq.
    /// </summary>
    /// <param name="language">Nomzod tili.</param>
    /// <param name="hasOpenVacancies">
    /// Ochiq vakansiya bor — matn ularni tanlashga yo'naltiradi (ro'yxat shu
    /// xabardan keyin chiqadi); yo'q bo'lsa — keyinroq qaytishga.
    /// </param>
    public static string QuestionnaireClosed(BotLanguage language, bool hasOpenVacancies) => language switch
    {
        BotLanguage.Cyrillic =>
            "📋 Ҳозирча умумий анкета қабул қилинмаяпти.\n\n"
            + (hasOpenVacancies
                ? "Қуйидаги очиқ вакансиялардан бирини танлаб ариза топширишингиз мумкин."
                : "Очиқ вакансия чиққанда «💼 Вакансиялар» бўлимида кўринади — ўшанда ариза топширишингиз мумкин."),
        BotLanguage.Russian =>
            "📋 Сейчас общая анкета не принимается.\n\n"
            + (hasOpenVacancies
                ? "Вы можете выбрать одну из открытых вакансий ниже и подать заявку."
                : "Когда появится открытая вакансия, она будет в разделе «💼 Вакансии» — тогда вы сможете подать заявку."),
        _ =>
            "📋 Hozircha umumiy anketa qabul qilinmayapti.\n\n"
            + (hasOpenVacancies
                ? "Quyidagi ochiq vakansiyalardan birini tanlab ariza topshirishingiz mumkin."
                : "Ochiq vakansiya chiqqanda «💼 Vakansiyalar» bo‘limida ko‘rinadi — o‘shanda ariza topshirishingiz mumkin."),
    };

    /// <summary>
    /// Ochiq vakansiya yo'q VA «Umumiy anketa» ham yopiq — «Anketa to'ldirish»
    /// tavsiya QILINMAYDI (<see cref="NoOpenVacancies"/> dan farqi shu).
    /// </summary>
    public static string NoOpenVacanciesNoQuestionnaire(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Ҳозирча очиқ иш ўрни йўқ ва анкета қабул қилинмаяпти. 🙁\n\n"
            + "Очиқ вакансия чиққанда «💼 Вакансиялар» бўлимида кўринади.",
        BotLanguage.Russian =>
            "Сейчас открытых вакансий нет, и анкеты не принимаются. 🙁\n\n"
            + "Когда появится открытая вакансия, она будет в разделе «💼 Вакансии».",
        _ =>
            "Hozircha ochiq ish o‘rni yo‘q va anketa qabul qilinmayapti. 🙁\n\n"
            + "Ochiq vakansiya chiqqanda «💼 Vakansiyalar» bo‘limida ko‘rinadi.",
    };

    /// <summary>
    /// Nomzod anketani to'ldirayotgan paytda qabul YOPILDI (vakansiya ham,
    /// «Umumiy anketa» ham) — ariza yuborilmadi va qayta urinish foyda bermaydi.
    /// </summary>
    /// <remarks>
    /// <see cref="Failed"/> dan farqi: u «keyinroq qayta urinib ko'ring»
    /// deydi va suhbatni SAQLAYDI (vaqtincha nosozlik uchun). Bu yerda sabab
    /// vaqtinchalik emas — nomzodni bekorga qayta-qayta bosishga undamaslik kerak.
    /// </remarks>
    public static string ApplicationsClosed(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Кечирасиз, сиз анкета тўлдираётган пайтда ариза қабул қилиш ёпилди — аризангиз юборилмади. 😔\n\n"
            + "Очиқ вакансия чиққанда қайта топширишингиз мумкин.",
        BotLanguage.Russian =>
            "Извините, пока вы заполняли анкету, приём заявок закрылся — заявка не отправлена. 😔\n\n"
            + "Когда появится открытая вакансия, вы сможете подать заявку снова.",
        _ =>
            "Kechirasiz, siz anketa to‘ldirayotgan paytda ariza qabul qilish yopildi — arizangiz yuborilmadi. 😔\n\n"
            + "Ochiq vakansiya chiqqanda qayta topshirishingiz mumkin.",
    };

    /// <summary>Menyudagi «Anketa to'ldirish» tugmasi — vakansiyasiz.</summary>
    /// <remarks>
    /// Matn «Ariza topshirish» dan ANIQ farq qilishi kerak: ikkalasi
    /// bir xil savollarni beradi va farqi faqat natijada (vakansiyaga
    /// yoki umumiy bazaga). Nomzod tugmani bosishdan OLDIN qaysi biri
    /// nima qilishini tushunishi kerak, aks holda ikkalasini ham bosib
    /// ko'rardi. Shu sababli emoji ham boshqacha.
    /// </remarks>
    public static string QuestionnaireButton(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "📋 Анкета тўлдириш",
        BotLanguage.Russian => "📋 Заполнить анкету",
        _ => "📋 Anketa to‘ldirish",
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
    /// izohning ichki qiymatini yo'qotardi. Shu sababli sabab hatto
    /// botga BERILMAYDI ham: <c>RejectionNoticeDto</c> da bunday
    /// maydon yo'q.
    /// </para>
    /// <para>
    /// ⭐ <b>Matn HAMMAGA bir xil</b> (2026-09-08, foydalanuvchi
    /// qarori: «bir xil holatda borishi kerak»). Xabar ikki tilda —
    /// o'zbekcha va ruscha — BITTA xabarda ketadi va nomzod bot
    /// boshida qaysi tilni tanlaganiga bog'liq emas. Sabab: bu
    /// kompaniyaning rasmiy javobi, uning matni HR da qog'ozda ham
    /// shu ko'rinishda turadi va nomzoddan nomzodga farq qilmasligi
    /// kerak. Til bo'yicha uchta boshqa matn esa aynan shu farqni
    /// tug'dirardi.
    /// </para>
    /// <para>
    /// ⭐ <b>Vakansiya nomi ham YO'Q.</b> Matn «vakansiyaga bo'lgan
    /// qiziqishingiz uchun» deydi, nomini aytmaydi — talab qilingan
    /// shablon shunday. Nomzod odatda bitta vakansiyaga ariza beradi
    /// va nomni takrorlash xabarni ikki tilda ikki marta
    /// uzaytirardi.
    /// </para>
    /// <para>
    /// <b>Ohangi ataylab iliq va yopiq.</b> Xabar javobsizlikni
    /// tugatadi — nomzod kutishdan qutuladi. Oxiridagi taklif esa
    /// bo'sh muomala emas: MBF doimiy ravishda ishga oladi va
    /// bugun to'g'ri kelmagan odam olti oydan keyin to'g'ri kelishi
    /// mumkin.
    /// </para>
    /// </remarks>
    /// <param name="fullName">
    /// Nomzodning ismi — xabarning YAGONA o'zgaruvchi qismi. U ikkala
    /// tilda ham bir xil yoziladi: ism tarjima qilinmaydi.
    /// </param>
    public static string RejectionNotice(string fullName)
    {
        var name = Escape(fullName);

        return $"🇺🇿 Assalomu alaykum, {name}!\n\n"
            + "Vakansiyaga bo\u2018lgan qiziqishingiz uchun katta rahmat. Afsuski, "
            + "hozirda biz sizni ushbu vakansiya uchun keyingi suhbatga taklif "
            + "qilishga tayyor emasmiz. Biz sizning rezyumeingizni diqqat bilan "
            + "ko\u2018rib chiqdik va agar kerak bo\u2018lsa, sizning nomzodingizga "
            + "qaytishimiz mumkin.\n\n"
            + "Hurmat bilan,\nHR MBF company!\n"
            + RejectionDivider + "\n"
            + $"🇷🇺 Здравствуйте, {name}!\n\n"
            + "Большое спасибо за интерес, проявленный к вакансии. К сожалению, в "
            + "настоящий момент мы не готовы пригласить Вас на дальнейшее интервью. "
            + "Мы внимательно ознакомились с Вашей анкетой и, возможно, вернёмся к "
            + "Вашей кандидатуре, когда у нас возникнет такая потребность.\n\n"
            + "С уважением,\nHR MBF company!";
    }

    /// <summary>
    /// Ikki tilni ajratuvchi chiziq.
    /// </summary>
    /// <remarks>
    /// Uzunligi ataylab QISQA: Telegram matnni telefon ekrani bo'yicha
    /// o'raydi va uzun chiziq ikkiga bo'linib, ajratkich o'rniga ikkita
    /// sinuq qatorga aylanardi.
    /// </remarks>
    private const string RejectionDivider = "------------------------------";

    /// <summary>
    /// Xabar oxiridagi imzo — «Ҳурмат билан, HR MBF company».
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega alohida funksiya.</b> Imzo IKKI xabarda takrorlanadi
    /// (ariza qabul qilindi, suhbatga taklif) va ikkalasi ham nomzod
    /// bilan RASMIY muloqot. Har birida qo'lda yozilsa, kompaniya nomi
    /// bir kun bir joyda o'zgarib, boshqasida eski holida qolardi.
    /// Rad etish xabari bu imzoni ISHLATMAYDI: u ikki tilli va qat'iy
    /// shablon, imzosi ham shablonning bir qismi
    /// (<see cref="RejectionNotice"/>).
    /// </para>
    /// <para>
    /// <b>Nega sozlamaga chiqarilmagan.</b> Bu kompaniyaning O'Z nomi,
    /// HR ning matni emas: uni ekrandan o'zgartirish imkoniyati faqat
    /// tasodifiy o'zgartirish yo'lini ochardi. Nom o'zgarsa — bitta
    /// qatorli o'zgarish.
    /// </para>
    /// </remarks>
    public static string Signature(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "Ҳурмат билан,\nHR MBF company",
        BotLanguage.Russian => "С уважением,\nHR MBF company",
        _ => "Hurmat bilan,\nHR MBF company",
    };

    /// <summary>
    /// Suhbatga taklif — HR «Кенгашга чақирилди» tugmasini bosgach
    /// nomzodga avtomatik boradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Vaqt Toshkent bo'yicha chiziladi.</b> Server lahzani UTC da
    /// qaytaradi va siljitilmagan qiymat nomzodni suhbatga BESH SOAT
    /// erta chaqirardi — bu xatoning eng qimmat turi. Siljish
    /// <see cref="BotClock"/> dan keladi, ya'ni botda yagona nusxada.
    /// </para>
    /// <para>
    /// <b>Telefon va manzil qatorlari SHARTLI.</b> Ikkalasi ham
    /// sozlamadan keladi (<c>bot.interview.phone</c>,
    /// <c>bot.interview.address</c>) va bo'sh bo'lishi mumkin. Bo'sh
    /// qiymat bilan «📍 » degan yolg'iz belgi qolardi — botning boshqa
    /// hamma joyidagi qoida bir xil: sozlanmagan narsa umuman
    /// chizilmaydi.
    /// </para>
    /// <para>
    /// <b>Tasdiqlash TUGMA bilan emas, qo'ng'iroq bilan</b>
    /// (foydalanuvchi qarori, 2026-09-08). Tugma bot suhbatiga yangi
    /// holat qo'shardi va «tasdiqladi, lekin kelmadi» degan yozuvni
    /// haqiqat deb ko'rsatardi; qo'ng'iroqda esa HR odam bilan
    /// gaplashadi va rejasini o'sha yerda tuzatadi.
    /// </para>
    /// </remarks>
    public static string InterviewNotice(
        BotLanguage language,
        string fullName,
        string vacancy,
        DateTimeOffset interviewAt,
        string phone,
        string address)
    {
        var local = BotClock.Local(interviewAt);
        var when = local.ToString("dd.MM.yyyy, HH:mm", CultureInfo.InvariantCulture);

        var contact = string.IsNullOrWhiteSpace(phone)
            ? string.Empty
            : language switch
            {
                BotLanguage.Cyrillic => $"📞 Масъул: Кадрлар бўлими {Escape(phone)}\n",
                BotLanguage.Russian => $"📞 Контакты: Отдел кадров {Escape(phone)}\n",
                _ => $"📞 Mas’ul: Kadrlar bo‘limi {Escape(phone)}\n",
            };

        var place = string.IsNullOrWhiteSpace(address)
            ? string.Empty
            : $"📍 {Escape(address)}\n";

        return language switch
        {
            BotLanguage.Cyrillic =>
                $"Ассалому алайкум, {Escape(fullName)}!\n\n"
                + $"Сизни «{Escape(vacancy)}» вакансияси бўйича суҳбатга таклиф қиламиз.\n\n"
                + $"🗓 Суҳбат санаси ва вақти: <b>{when}</b>\n"
                + contact
                + place
                + "\nИлтимос, суҳбат вақтини тасдиқлаш учун кўрсатилган рақамга "
                + "қўнғироқ қилинг.\n\n"
                + Signature(language),
            BotLanguage.Russian =>
                $"Здравствуйте, {Escape(fullName)}!\n\n"
                + $"Приглашаем Вас на интервью по вакансии «{Escape(vacancy)}».\n\n"
                + $"🗓 Дата и время интервью: <b>{when}</b>\n"
                + contact
                + place
                + "\nПожалуйста, позвоните по указанному номеру, чтобы подтвердить "
                + "время встречи.\n\n"
                + Signature(language),
            _ =>
                $"Assalomu alaykum, {Escape(fullName)}!\n\n"
                + $"Sizni «{Escape(vacancy)}» vakansiyasi bo‘yicha suhbatga taklif qilamiz.\n\n"
                + $"🗓 Suhbat sanasi va vaqti: <b>{when}</b>\n"
                + contact
                + place
                + "\nIltimos, suhbat vaqtini tasdiqlash uchun ko‘rsatilgan raqamga "
                + "qo‘ng‘iroq qiling.\n\n"
                + Signature(language),
        };
    }

    private static readonly string[] CouncilMonths =
        ["январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"];

    /// <summary>The council group's message. Its members read Cyrillic, like the bot's default.</summary>
    public static string CouncilDigest(DateOnly date, bool isToday, int count)
    {
        var day = $"{date.Day} {CouncilMonths[date.Month - 1]}";

        return count == 0
            ? $"📋 <b>{day} кенгаши</b>\n\nНомзодлар қолмади."
            : $"📋 <b>{(isToday ? "Бугун" : "Эртага")} кенгаш — {day}</b>\nНомзодлар: {count} та\n\n"
                + "Номзодни босинг: анкетаси очилади, хулосангизни ёзасиз.";
    }

    /// <summary>The button that opens every candidate.</summary>
    public const string CouncilAllButton = "📋 Барча номзодлар";

    /// <summary>The group was bound.</summary>
    public const string CouncilBound = "✅ Гуруҳ кенгашга уланди. Бот бу ерга фақат кенгашдан бир кун олдин рўйхат ташлайди.";

    /// <summary>The binding code was refused.</summary>
    public const string CouncilBindFailed = "Код нотўғри ёки эскирган. Тизимдан янги код олинг.";

    /// <summary>A member starts linking.</summary>
    public const string CouncilAskContact = "Сизни кенгаш аъзоси сифатида таниш учун рақамингизни бир марта юборинг. Пастдаги тугмани босинг.";

    /// <summary>The contact button.</summary>
    public const string CouncilContactButton = "📱 Рақамни юбориш";

    /// <summary>The contact was not the sender's own.</summary>
    public const string CouncilContactNotOwn = "Ўз рақамингизни юборинг: пастдаги тугмани босинг.";

    /// <summary>The phone is not on the roster.</summary>
    public const string CouncilContactUnknown = "Бу рақам кенгаш аъзолари рўйхатида йўқ. Кадрлар бўлимига айтинг.";

    /// <summary>The server did not answer.</summary>
    public const string CouncilContactRetry = "Ҳозир текшириб бўлмади. Бироздан кейин қайта уриниб кўринг.";

    /// <summary>Linked.</summary>
    public static string CouncilLinked(string fullName) => $"✅ Танидик, {Escape(fullName)}. Энди номзодларни шу тугма орқали ёки гуруҳдан очасиз.";

    /// <summary>Opening the Mini App from the private chat.</summary>
    public const string CouncilOpenText = "Анкетани очиш учун тугмани босинг.";

    /// <summary>The Mini App button in the private chat.</summary>
    public const string CouncilOpenButton = "📋 Очиш";

    /// <summary>
    /// The day-before reminder of the interview: time, contact and place again, the map point
    /// follows as its own message.
    /// </summary>
    public static string InterviewReminder(
        BotLanguage language,
        string fullName,
        string vacancy,
        DateTimeOffset interviewAt,
        string phone,
        string address)
    {
        var when = BotClock.Local(interviewAt).ToString("dd.MM.yyyy, HH:mm", CultureInfo.InvariantCulture);

        var contact = string.IsNullOrWhiteSpace(phone)
            ? string.Empty
            : language switch
            {
                BotLanguage.Cyrillic => $"📞 Масъул: Кадрлар бўлими {Escape(phone)}\n",
                BotLanguage.Russian => $"📞 Контакты: Отдел кадров {Escape(phone)}\n",
                _ => $"📞 Mas’ul: Kadrlar bo‘limi {Escape(phone)}\n",
            };

        var place = string.IsNullOrWhiteSpace(address)
            ? string.Empty
            : $"📍 {Escape(address)}\n";

        return language switch
        {
            BotLanguage.Cyrillic =>
                $"Ассалому алайкум, {Escape(fullName)}!\n\n"
                + $"Эслатма: эртага «{Escape(vacancy)}» вакансияси бўйича суҳбатга кутамиз.\n\n"
                + $"🗓 Суҳбат санаси ва вақти: <b>{when}</b>\n"
                + contact
                + place
                + "\nКела олмасангиз, илтимос, кўрсатилган рақамга қўнғироқ қилинг.\n\n"
                + Signature(language),
            BotLanguage.Russian =>
                $"Здравствуйте, {Escape(fullName)}!\n\n"
                + $"Напоминаем: завтра ждём Вас на интервью по вакансии «{Escape(vacancy)}».\n\n"
                + $"🗓 Дата и время интервью: <b>{when}</b>\n"
                + contact
                + place
                + "\nЕсли не сможете прийти, пожалуйста, позвоните по указанному номеру.\n\n"
                + Signature(language),
            _ =>
                $"Assalomu alaykum, {Escape(fullName)}!\n\n"
                + $"Eslatma: ertaga «{Escape(vacancy)}» vakansiyasi bo‘yicha suhbatga kutamiz.\n\n"
                + $"🗓 Suhbat sanasi va vaqti: <b>{when}</b>\n"
                + contact
                + place
                + "\nKela olmasangiz, iltimos, ko‘rsatilgan raqamga qo‘ng‘iroq qiling.\n\n"
                + Signature(language),
        };
    }

    /// <summary>The candidate opened the link from the form: from now on the bot can reach them.</summary>
    public static string TelegramLinked(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "✅ Аризангиз шу Telegram ҳисобига уланди. Кенгашга чақирилсангиз, сана, вақт ва манзил шу ерга келади.",
        BotLanguage.Russian =>
            "✅ Ваша анкета привязана к этому Telegram. Если Вас пригласят на совет, дата, время и адрес придут сюда.",
        _ =>
            "✅ Arizangiz shu Telegram hisobiga ulandi. Kengashga chaqirilsangiz, sana, vaqt va manzil shu yerga keladi.",
    };

    /// <summary>Suhbat unutilgan yoki tushunilmagan xabar.</summary>
    public static string Restart(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "Пастдаги менюдан керакли бўлимни танланг.",
        BotLanguage.Russian => "Выберите нужный раздел в меню ниже.",
        _ => "Pastdagi menyudan kerakli bo‘limni tanlang.",
    };

    // ═══════════════════════════════════════════════════════════
    //  ANKETA (22 qadam) — matnlar `docs/BOT-ANKETA-MATNLARI.md` dan.
    //  Ular O'YLAB TOPILMAYDI: hujjatdagi matn HR bilan kelishilgan
    //  va uchala tilda qo'lda yozilgan.
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// «O'tkazib yuborish» — javob bermaslik ham JAVOB.
    /// </summary>
    /// <remarks>
    /// Beshta savolda bu tugma bor va u tasodifiy emas: ish tajribasi
    /// yo'q nomzod («ilk ishim») savolga umuman javob bera olmaydi va
    /// tugmasiz u yo yolg'on yozardi, yo anketani tashlab ketardi.
    /// </remarks>
    public static string SkipButton(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "Ўтказиб юбориш",
        BotLanguage.Russian => "Пропустить",
        _ => "O‘tkazib yuborish",
    };

    /// <summary>«Ha» tugmasi.</summary>
    public static string YesButton(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "Ҳа",
        BotLanguage.Russian => "Да",
        _ => "Ha",
    };

    /// <summary>«Yo'q» tugmasi.</summary>
    public static string NoButton(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "Йўқ",
        BotLanguage.Russian => "Нет",
        _ => "Yo‘q",
    };

    /// <summary>Yakuniy «Yuborish» tugmasi (22-qadam).</summary>
    public static string SubmitButton(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "📨 Юбориш",
        BotLanguage.Russian => "📨 Отправить",
        _ => "📨 Yuborish",
    };

    /// <summary>Ommaviy oferta bilan rozilik tugmasi (21-qadam).</summary>
    public static string AgreeButton(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "Розиман",
        BotLanguage.Russian => "Согласен",
        _ => "Roziman",
    };

    /// <summary>«Boshidan boshlash» — <c>/start</c> bilan bir xil ish.</summary>
    public static string StartOverButton(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "Бошидан бошлаш",
        BotLanguage.Russian => "Начать заново",
        _ => "Boshidan boshlash",
    };

    /// <summary>
    /// Tugma kutilayotgan qadamda matn yozilganda.
    /// </summary>
    /// <remarks>
    /// <see cref="Restart"/> bu yerda TO'G'RI KELMAYDI: u «menyudan
    /// bo'lim tanlang» deydi va nomzodni yarim to'ldirilgan anketadan
    /// chiqib ketishga undardi. Bu yerda esa aytiladigan gap boshqa —
    /// javob yuqoridagi tugmalar orqali beriladi.
    /// </remarks>
    public static string UseButtons(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "Илтимос, юқоридаги тугмалардан бирини танланг.",
        BotLanguage.Russian => "Пожалуйста, выберите один из вариантов выше.",
        _ => "Iltimos, yuqoridagi tugmalardan birini tanlang.",
    };

    /// <summary>4-qadam: tug'ilgan sana.</summary>
    public static string AskBirthDate(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "🗓 Туғилган кунингизни ёзинг (кк.оо.йййй).\n\nМасалан: <i>10.10.1990</i>",
        BotLanguage.Russian =>
            "🗓 Укажите дату рождения (дд.мм.гггг).\n\nНапример: <i>10.10.1990</i>",
        _ =>
            "🗓 Tug‘ilgan kuningizni yozing (kk.oo.yyyy).\n\nMasalan: <i>10.10.1990</i>",
    };

    /// <summary>Sana tushunilmadi yoki chegaradan chiqdi.</summary>
    /// <remarks>
    /// Kelajakdagi sana ham, 14 yoshdan kichik va 80 yoshdan katta sana
    /// ham AYNAN shu xabar bilan rad etiladi
    /// (<see cref="BirthDateParser"/>): nomzodga «yoshingiz to'g'ri
    /// kelmadi» deb yozish uni haqoratlardi, holbuki amalda bu deyarli
    /// har doim terish xatosi.
    /// </remarks>
    public static string BirthDateNotUnderstood(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "Сана тушунарсиз. Илтимос, <i>кк.оо.йййй</i> кўринишида ёзинг — масалан <i>10.10.1990</i>.",
        BotLanguage.Russian =>
            "Не удалось разобрать дату. Напишите в виде <i>дд.мм.гггг</i> — например <i>10.10.1990</i>.",
        _ =>
            "Sana tushunarsiz. Iltimos, <i>kk.oo.yyyy</i> ko‘rinishida yozing — masalan <i>10.10.1990</i>.",
    };

    /// <summary>7-qadam: ma'lumot darajasi.</summary>
    public static string AskEducation(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "🎓 Маълумот даражангиз қандай?",
        BotLanguage.Russian => "🎓 Какой у вас уровень образования?",
        _ => "🎓 Ma’lumot darajangiz qanday?",
    };

    /// <summary>Ma'lumot darajasining tugmadagi va xulosadagi nomi.</summary>
    public static string EducationLabel(BotLanguage language, EducationAnswer answer) => language switch
    {
        BotLanguage.Cyrillic => answer switch
        {
            EducationAnswer.SecondarySchool => "Ўрта (мактаб)",
            EducationAnswer.VocationalSchool => "Ўрта махсус (коллеж, лицей)",
            EducationAnswer.HigherComplete => "Олий (бакалавр)",
            EducationAnswer.HigherIncomplete => "Олий (тугалланмаган)",
            _ => string.Empty,
        },
        BotLanguage.Russian => answer switch
        {
            EducationAnswer.SecondarySchool => "Среднее (школа)",
            EducationAnswer.VocationalSchool => "Среднее специальное",
            EducationAnswer.HigherComplete => "Высшее (бакалавр)",
            EducationAnswer.HigherIncomplete => "Высшее (незаконченное)",
            _ => string.Empty,
        },
        _ => answer switch
        {
            EducationAnswer.SecondarySchool => "O‘rta (maktab)",
            EducationAnswer.VocationalSchool => "O‘rta maxsus (kollej, litsey)",
            EducationAnswer.HigherComplete => "Oliy (bakalavr)",
            EducationAnswer.HigherIncomplete => "Oliy (tugallanmagan)",
            _ => string.Empty,
        },
    };

    /// <summary>8-qadam: mutaxassisligi (o'tkazib yuborish mumkin).</summary>
    public static string AskSpeciality(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "📌 Мутахассислигингиз нима? Бўлмаса, «Ўтказиб юбориш»ни босинг.",
        BotLanguage.Russian => "📌 Ваша специальность? Если нет — нажмите «Пропустить».",
        _ => "📌 Mutaxassisligingiz nima? Bo‘lmasa, «O‘tkazib yuborish»ni bosing.",
    };

    /// <summary>9-qadam: ish tajribasi (o'tkazib yuborish mumkin).</summary>
    public static string AskWorkExperience(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "💼 Иш тажрибангизни ёзинг: корхона, лавозим ва йиллар.\n\n"
            + "Масалан: <i>Dream Job МЧЖ, кассир, 2015–2018</i>",
        BotLanguage.Russian =>
            "💼 Опишите опыт работы: компания, должность и годы.\n\n"
            + "Например: <i>Dream Job MChJ, кассир, 2015–2018</i>",
        _ =>
            "💼 Ish tajribangizni yozing: korxona, lavozim va yillar.\n\n"
            + "Masalan: <i>Dream Job MChJ, kassir, 2015–2018</i>",
    };

    /// <summary>10-qadam: oilaviy holat.</summary>
    public static string AskMaritalStatus(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "👨‍👩‍👧 Оилавий ҳолатингиз?",
        BotLanguage.Russian => "👨‍👩‍👧 Ваше семейное положение?",
        _ => "👨‍👩‍👧 Oilaviy holatingiz?",
    };

    /// <summary>Oilaviy holatning tugmadagi va xulosadagi nomi.</summary>
    public static string MaritalLabel(BotLanguage language, MaritalAnswer answer) => language switch
    {
        BotLanguage.Cyrillic => answer switch
        {
            MaritalAnswer.Married => "Оилали",
            MaritalAnswer.Unmarried => "Турмуш қурмаган",
            MaritalAnswer.Divorced => "Ажрашган",
            _ => string.Empty,
        },
        BotLanguage.Russian => answer switch
        {
            MaritalAnswer.Married => "Женат / замужем",
            MaritalAnswer.Unmarried => "Холост / не замужем",
            MaritalAnswer.Divorced => "В разводе",
            _ => string.Empty,
        },
        _ => answer switch
        {
            MaritalAnswer.Married => "Oilali",
            MaritalAnswer.Unmarried => "Turmush qurmagan",
            MaritalAnswer.Divorced => "Ajrashgan",
            _ => string.Empty,
        },
    };

    /// <summary>11-qadam: rus tili darajasi.</summary>
    public static string AskRussianLevel(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "🇷🇺 Рус тилини қай даражада биласиз?",
        BotLanguage.Russian => "🇷🇺 Насколько хорошо вы владеете русским языком?",
        _ => "🇷🇺 Rus tilini qay darajada bilasiz?",
    };

    /// <summary>Rus tili darajasining tugmadagi va xulosadagi nomi.</summary>
    public static string RussianLevelLabel(BotLanguage language, RussianLevelAnswer answer) => language switch
    {
        BotLanguage.Cyrillic => answer switch
        {
            RussianLevelAnswer.Elementary => "Бошланғич — тушунмайман",
            RussianLevelAnswer.Intermediate => "Ўрта — тушунаман, қийналиб гапираман",
            RussianLevelAnswer.Advanced => "Яхши — эркин гаплашаман",
            _ => string.Empty,
        },
        BotLanguage.Russian => answer switch
        {
            RussianLevelAnswer.Elementary => "Начальный — не понимаю",
            RussianLevelAnswer.Intermediate => "Средний — понимаю, говорю с трудом",
            RussianLevelAnswer.Advanced => "Свободный — говорю свободно",
            _ => string.Empty,
        },
        _ => answer switch
        {
            RussianLevelAnswer.Elementary => "Boshlang‘ich — tushunmayman",
            RussianLevelAnswer.Intermediate => "O‘rta — tushunaman, qiynalib gapiraman",
            RussianLevelAnswer.Advanced => "Yaxshi — erkin gaplashaman",
            _ => string.Empty,
        },
    };

    /// <summary>12-qadam: boshqa tillar (o'tkazib yuborish mumkin).</summary>
    public static string AskOtherLanguages(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "🌐 Яна қайси тилларни биласиз ва қай даражада?\n\n"
            + "Масалан: <i>инглиз — ўрта, тожик — эркин</i>",
        BotLanguage.Russian =>
            "🌐 Какими ещё языками владеете и на каком уровне?\n\n"
            + "Например: <i>английский — средний, таджикский — свободно</i>",
        _ =>
            "🌐 Yana qaysi tillarni bilasiz va qay darajada?\n\n"
            + "Masalan: <i>ingliz — o‘rta, tojik — erkin</i>",
    };

    /// <summary>13-qadam: Office dasturlari (o'tkazib yuborish mumkin).</summary>
    public static string AskOfficeSkills(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "💻 Офис дастурларини қай даражада биласиз? Фоизда ёзинг.\n\n"
            + "Масалан: <i>Word 80%, Excel 60%, Photoshop 20%</i>",
        BotLanguage.Russian =>
            "💻 Насколько владеете офисными программами? Укажите в процентах.\n\n"
            + "Например: <i>Word 80%, Excel 60%, Photoshop 20%</i>",
        _ =>
            "💻 Ofis dasturlarini qay darajada bilasiz? Foizda yozing.\n\n"
            + "Masalan: <i>Word 80%, Excel 60%, Photoshop 20%</i>",
    };

    /// <summary>14-qadam: oxirgi maosh (o'tkazib yuborish mumkin).</summary>
    public static string AskLastSalary(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "💵 Охирги иш жойингизда ойлик маошингиз қанча эди?",
        BotLanguage.Russian => "💵 Какой была ваша зарплата на последнем месте работы?",
        _ => "💵 Oxirgi ish joyingizda oylik maoshingiz qancha edi?",
    };

    /// <summary>15-qadam: o'zi haqida qo'shimcha (o'tkazib yuborish mumkin).</summary>
    public static string AskExtraNote(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic =>
            "✍️ Ўзингиз ҳақингизда яна нима қўшимча қилмоқчисиз?\n\n"
            + "Масалан: <i>кучли томоним — тез ўрганаман; заиф томоним — шошқалоқлик</i>",
        BotLanguage.Russian =>
            "✍️ Что ещё хотите рассказать о себе?\n\n"
            + "Например: <i>сильная сторона — быстро учусь; слабая — тороплюсь</i>",
        _ =>
            "✍️ O‘zingiz haqingizda yana nima qo‘shimcha qilmoqchisiz?\n\n"
            + "Masalan: <i>kuchli tomonim — tez o‘rganaman; zaif tomonim — shoshqaloqlik</i>",
    };

    /// <summary>16-qadam: haydovchilik guvohnomasi.</summary>
    public static string AskDriverLicense(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "🚗 Ҳайдовчилик гувоҳномангиз борми?",
        BotLanguage.Russian => "🚗 У вас есть водительское удостоверение?",
        _ => "🚗 Haydovchilik guvohnomangiz bormi?",
    };

    /// <summary>17-qadam: guvohnoma toifasi — faqat «Ha» dan keyin.</summary>
    public static string AskDriverLicenseCategory(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "🚗 Гувоҳнома тоифаси қайси?",
        BotLanguage.Russian => "🚗 Какой категории удостоверение?",
        _ => "🚗 Guvohnoma toifasi qaysi?",
    };

    /// <summary>18-qadam: surat (o'tkazib yuborish mumkin).</summary>
    public static string AskPhoto(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "🤳 Суратингизни юборинг. Телефон камерасидан олсангиз ҳам бўлади.",
        BotLanguage.Russian => "🤳 Отправьте свою фотографию. Можно сделать селфи на телефон.",
        _ => "🤳 Suratingizni yuboring. Telefon kamerasidan olsangiz ham bo‘ladi.",
    };

    /// <summary>Rasm o'rniga matn kelganda.</summary>
    public static string PhotoNotUnderstood(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "Бу расм эмас. Расм юборинг ёки «Ўтказиб юбориш»ни босинг.",
        BotLanguage.Russian => "Это не фотография. Отправьте фото или нажмите «Пропустить».",
        _ => "Bu rasm emas. Rasm yuboring yoki «O‘tkazib yuborish»ni bosing.",
    };

    /// <summary>19-qadam: kutilayotgan maosh.</summary>
    public static string AskExpectedSalary(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "💰 Қанча ойлик маош кутяпсиз?",
        BotLanguage.Russian => "💰 На какую зарплату вы рассчитываете?",
        _ => "💰 Qancha oylik maosh kutyapsiz?",
    };

    /// <summary>
    /// Maosh oralig'ining tugmadagi va xulosadagi nomi.
    /// </summary>
    /// <remarks>
    /// Sonlar uchala tilda bir xil, faqat qisqartma farq qiladi:
    /// «млн» kirill va ruschada, «mln» lotinda.
    /// </remarks>
    public static string SalaryLabel(BotLanguage language, SalaryAnswer answer) => language switch
    {
        BotLanguage.Cyrillic => answer switch
        {
            SalaryAnswer.From1To2Million => "1–2 млн",
            SalaryAnswer.From2To3Million => "2–3 млн",
            SalaryAnswer.From3To4Million => "3–4 млн",
            SalaryAnswer.From5To7Million => "5–7 млн",
            _ => string.Empty,
        },
        BotLanguage.Russian => answer switch
        {
            SalaryAnswer.From1To2Million => "1–2 млн",
            SalaryAnswer.From2To3Million => "2–3 млн",
            SalaryAnswer.From3To4Million => "3–4 млн",
            SalaryAnswer.From5To7Million => "5–7 млн",
            _ => string.Empty,
        },
        _ => answer switch
        {
            SalaryAnswer.From1To2Million => "1–2 mln",
            SalaryAnswer.From2To3Million => "2–3 mln",
            SalaryAnswer.From3To4Million => "3–4 mln",
            SalaryAnswer.From5To7Million => "5–7 mln",
            _ => string.Empty,
        },
    };

    /// <summary>20-qadam: ish haqida qayerdan bilgan.</summary>
    /// <remarks>
    /// Tugmalar SERVERDAN — «Qayerdan bilgan» ma'lumotnomasining faol kanallari, nomi
    /// saqlanganidek (SPEC-qayerdan-bilgan §5). Kirill tilida ham shunday: loyihada lotindan
    /// kirillga o'giruvchi yordamchi yo'q va brend nomlari («Telegram», «OLX.uz») baribir
    /// o'girilmaydi (<see cref="InstagramButton"/> izohi).
    /// </remarks>
    public static string AskReferralSource(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "📣 Иш ҳақида қаердан билдингиз?",
        BotLanguage.Russian => "📣 Откуда вы узнали о вакансии?",
        _ => "📣 Ish haqida qayerdan bildingiz?",
    };

    /// <summary>
    /// 20a-qadam: tanlangan kanal aniqlik so'raydi («Tanishlar orqali» — kim?).
    /// </summary>
    /// <remarks>
    /// Kanal nomi savolning ichida: nomzod nimani aniqlashtirayotganini ko'radi. «O'tkazib
    /// yuborish» tugmasi YO'Q — server bu aniqlikni majburiy qiladi
    /// (<c>qayerdan.izoh_majburiy</c>).
    /// </remarks>
    public static string AskReferralDetail(BotLanguage language, string channel) => language switch
    {
        BotLanguage.Cyrillic =>
            $"✍️ «{Escape(channel)}» — аниқлаштиринг: ким тавсия қилди ёки қайси манба?\n\n"
            + "Масалан: <i>Рустамов Баҳодир, омборчи</i>",
        BotLanguage.Russian =>
            $"✍️ «{Escape(channel)}» — уточните: кто порекомендовал или какой источник?\n\n"
            + "Например: <i>Рустамов Баходир, кладовщик</i>",
        _ =>
            $"✍️ «{Escape(channel)}» — aniqlashtiring: kim tavsiya qildi yoki qaysi manba?\n\n"
            + "Masalan: <i>Rustamov Bahodir, omborchi</i>",
    };

    /// <summary>
    /// 20-qadam tugmalarini chizib bo'lmadi — server kanallar ro'yxatini bermadi.
    /// </summary>
    /// <remarks>
    /// Kanallar serverdagi ma'lumotnomada (SPEC-qayerdan-bilgan §5) va ular botda qattiq
    /// yozilmagan: API bir lahzaga javob bermasa, nomzod savolsiz qolmasin — bitta tugma
    /// savolni qayta so'raydi, javoblar esa joyida.
    /// </remarks>
    public static string ReferralUnavailable(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "⚠️ Рўйхатни ҳозир юклаб бўлмади. Бир оздан кейин «Қайта уриниш» тугмасини босинг.",
        BotLanguage.Russian => "⚠️ Не удалось загрузить список. Через минуту нажмите «Повторить».",
        _ => "⚠️ Ro‘yxatni hozir yuklab bo‘lmadi. Birozdan keyin «Qayta urinish» tugmasini bosing.",
    };

    /// <summary>«Qayta urinish» — 20-qadam savolini qayta chizadi.</summary>
    public static string RetryButton(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "🔄 Қайта уриниш",
        BotLanguage.Russian => "🔄 Повторить",
        _ => "🔄 Qayta urinish",
    };

    /// <summary>
    /// 21-qadam: ommaviy oferta.
    /// </summary>
    /// <remarks>
    /// Havola sozlamadan keladi (<c>bot.offerUrl</c>) va BO'SH bo'lsa bu
    /// qadam umuman chizilmaydi — bo'sh havolali xabar nomzodni
    /// boshi berk ko'chaga olib borardi.
    /// </remarks>
    public static string Offer(BotLanguage language, string url) => language switch
    {
        BotLanguage.Cyrillic =>
            $"📄 Юборишдан олдин оммавий оферта билан танишиб чиқинг:\n{Escape(url)}\n\n"
            + "Давом этиш учун «Розиман»ни босинг.",
        BotLanguage.Russian =>
            $"📄 Перед отправкой ознакомьтесь с публичной офертой:\n{Escape(url)}\n\n"
            + "Нажмите «Согласен», чтобы продолжить.",
        _ =>
            $"📄 Yuborishdan oldin ommaviy oferta bilan tanishib chiqing:\n{Escape(url)}\n\n"
            + "Davom etish uchun «Roziman»ni bosing.",
    };

    /// <summary>22-qadam: xulosadan keyingi savol.</summary>
    public static string ConfirmQuestion(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "Барча маълумот тўғрими?",
        BotLanguage.Russian => "Все данные указаны верно?",
        _ => "Barcha ma’lumot to‘g‘rimi?",
    };

    /// <summary>Xulosadagi «сурат: юборилди» qiymati.</summary>
    public static string PhotoSent(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "юборилди",
        BotLanguage.Russian => "отправлено",
        _ => "yuborildi",
    };

    /// <summary>Xulosadagi «оферта: розилик берилди» qiymati.</summary>
    public static string OfferAccepted(BotLanguage language) => language switch
    {
        BotLanguage.Cyrillic => "розилик берилди",
        BotLanguage.Russian => "согласие дано",
        _ => "rozilik berildi",
    };

    /// <summary>
    /// Xulosa qatorining sarlavhasi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bitta metod, o'n to'qqizta alohida metod emas: sarlavhalar bir
    /// xil vazifani bajaradi va ular yonma-yon turgani ularni
    /// solishtirishni osonlashtiradi — bitta tilda unutilgan qator shu
    /// yerda darhol ko'rinadi.
    /// </para>
    /// <para>
    /// Belgi (emoji) sarlavhaning bir qismi: xulosa uzun ro'yxat va
    /// belgisiz uni ko'z bilan o'qib bo'lmasdi.
    /// </para>
    /// </remarks>
    public static string SummaryLabel(BotLanguage language, SummaryLine line) => language switch
    {
        BotLanguage.Cyrillic => line switch
        {
            SummaryLine.Name => "👤 Ф.И.Ш.",
            SummaryLine.BirthDate => "🗓 Туғилган кун",
            SummaryLine.Address => "🏠 Манзил",
            SummaryLine.Phone => "📱 Телефон",
            SummaryLine.Vacancy => "💼 Вакансия",
            SummaryLine.Education => "🎓 Маълумот",
            SummaryLine.Speciality => "📌 Мутахассислик",
            SummaryLine.WorkExperience => "💼 Иш тажрибаси",
            SummaryLine.MaritalStatus => "👨‍👩‍👧 Оилавий ҳолат",
            SummaryLine.RussianLevel => "🇷🇺 Рус тили",
            SummaryLine.OtherLanguages => "🌐 Бошқа тиллар",
            SummaryLine.OfficeSkills => "💻 Офис дастурлари",
            SummaryLine.LastSalary => "💵 Охирги маош",
            SummaryLine.ExtraNote => "✍️ Қўшимча",
            SummaryLine.DriverLicense => "🚗 Ҳайдовчилик гувоҳномаси",
            SummaryLine.Photo => "🤳 Сурат",
            SummaryLine.ExpectedSalary => "💰 Кутилаётган маош",
            SummaryLine.ReferralSource => "📣 Қаердан билди",
            SummaryLine.Offer => "📄 Оммавий оферта",
            _ => string.Empty,
        },
        BotLanguage.Russian => line switch
        {
            SummaryLine.Name => "👤 Ф.И.О.",
            SummaryLine.BirthDate => "🗓 Дата рождения",
            SummaryLine.Address => "🏠 Адрес",
            SummaryLine.Phone => "📱 Телефон",
            SummaryLine.Vacancy => "💼 Вакансия",
            SummaryLine.Education => "🎓 Образование",
            SummaryLine.Speciality => "📌 Специальность",
            SummaryLine.WorkExperience => "💼 Опыт работы",
            SummaryLine.MaritalStatus => "👨‍👩‍👧 Семейное положение",
            SummaryLine.RussianLevel => "🇷🇺 Русский язык",
            SummaryLine.OtherLanguages => "🌐 Другие языки",
            SummaryLine.OfficeSkills => "💻 Офисные программы",
            SummaryLine.LastSalary => "💵 Последняя зарплата",
            SummaryLine.ExtraNote => "✍️ Дополнительно",
            SummaryLine.DriverLicense => "🚗 Водительское удостоверение",
            SummaryLine.Photo => "🤳 Фотография",
            SummaryLine.ExpectedSalary => "💰 Ожидаемая зарплата",
            SummaryLine.ReferralSource => "📣 Откуда узнал(а)",
            SummaryLine.Offer => "📄 Публичная оферта",
            _ => string.Empty,
        },
        _ => line switch
        {
            SummaryLine.Name => "👤 F.I.Sh.",
            SummaryLine.BirthDate => "🗓 Tug‘ilgan kun",
            SummaryLine.Address => "🏠 Manzil",
            SummaryLine.Phone => "📱 Telefon",
            SummaryLine.Vacancy => "💼 Vakansiya",
            SummaryLine.Education => "🎓 Ma’lumot",
            SummaryLine.Speciality => "📌 Mutaxassislik",
            SummaryLine.WorkExperience => "💼 Ish tajribasi",
            SummaryLine.MaritalStatus => "👨‍👩‍👧 Oilaviy holat",
            SummaryLine.RussianLevel => "🇷🇺 Rus tili",
            SummaryLine.OtherLanguages => "🌐 Boshqa tillar",
            SummaryLine.OfficeSkills => "💻 Ofis dasturlari",
            SummaryLine.LastSalary => "💵 Oxirgi maosh",
            SummaryLine.ExtraNote => "✍️ Qo‘shimcha",
            SummaryLine.DriverLicense => "🚗 Haydovchilik guvohnomasi",
            SummaryLine.Photo => "🤳 Surat",
            SummaryLine.ExpectedSalary => "💰 Kutilayotgan maosh",
            SummaryLine.ReferralSource => "📣 Qayerdan bildi",
            SummaryLine.Offer => "📄 Ommaviy oferta",
            _ => string.Empty,
        },
    };
}
