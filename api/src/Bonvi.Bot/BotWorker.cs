using Bonvi.Bot.Bonvi;
using Bonvi.Bot.Conversation;
using Bonvi.Bot.Telegram;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bonvi.Bot;

/// <summary>
/// Botning asosiy halqasi: Telegram'dan xabar oladi, javob yozadi va
/// tugallangan arizani API ga yuboradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Anketa 22 qadamdan iborat</b> (SPEC-anketa, 4-bo'lim) va bu
/// ataylab: eski bot (@MBF_jbot) aynan shu ma'lumotni so'raydi, HR esa
/// nomzodni birinchi qo'ng'iroqdan OLDIN baholay olishi kerak. Ilgari
/// botda to'rt qadam bor edi va qolgan hamma narsani HR telefonda
/// so'rardi.
/// </para>
/// <para>
/// <b>Qadam tartibi BITTA joyda</b> — <c>ConversationStep</c> sanog'ida,
/// o'tish esa <c>ContinueAsync</c> da. Shartli qadamlar (vakansiya,
/// guvohnoma toifasi, ommaviy oferta) ham faqat o'sha yerda o'tkazib
/// yuboriladi.
/// </para>
/// <para>
/// <b>Xato halqani to'xtatmaydi.</b> Bitta suhbatdagi muammo qolganlarga
/// tegmasligi kerak: istisno tutiladi, jurnalga yoziladi va bot ishlashda
/// davom etadi.
/// </para>
/// </remarks>
public sealed partial class BotWorker : BackgroundService
{
    [LoggerMessage(
        EventId = 3201,
        Level = LogLevel.Error,
        Message = "Telegram token QABUL QILINMADI. Bot kutmoqda — «Tizim sozlamalari» ekranida "
            + "tokenni to'g'rilang, bot o'zi qayta ulanadi.")]
    private partial void LogBadToken();

    [LoggerMessage(
        EventId = 3207,
        Level = LogLevel.Warning,
        Message = "Bot hisobi SOZLANMAGAN — {Missing} berilmagan. «.env» faylini to'ldiring: "
            + "BOOTSTRAP__BOTLOGIN, BOOTSTRAP__BOTPASSWORD. Bu ikkisi tizim sozlamalariga "
            + "KO'CHIRILMAYDI: bot aynan shular bilan tizimga kirib tokenni oladi. "
            + "Tizimning qolgan qismi bundan ta'sirlanmaydi.")]
    private partial void LogNotConfigured(string missing);

    [LoggerMessage(
        EventId = 3208,
        Level = LogLevel.Warning,
        Message = "Telegram tokeni QO'YILMAGAN — bot kutmoqda. «Tizim sozlamalari» ekranida token "
            + "kiriting (yoki «.env» dagi TELEGRAM__BOTTOKEN ni to'ldiring). Qayta ishga "
            + "tushirish shart emas.")]
    private partial void LogTokenMissing();

    [LoggerMessage(
        EventId = 3209,
        Level = LogLevel.Information,
        Message = "Telegram tokeni almashtirildi — bot yangi token bilan ulanmoqda")]
    private partial void LogTokenChanged();

    [LoggerMessage(
        EventId = 3202,
        Level = LogLevel.Warning,
        Message = "Bonvi API ga kirib bo'lmadi — birinchi arizada qayta uriniladi")]
    private partial void LogApiUnavailable();

    [LoggerMessage(EventId = 3291, Level = LogLevel.Information, Message = "Interview reminder sent: {Vacancy}")]
    private partial void LogInterviewReminderSent(string vacancy);

    [LoggerMessage(EventId = 3292, Level = LogLevel.Warning, Message = "Interview map point not sent: {Vacancy}")]
    private partial void LogLocationNotSent(string vacancy);

    [LoggerMessage(EventId = 3293, Level = LogLevel.Warning, Message = "Interview message went without a map point: bot.location is empty or unreadable")]
    private partial void LogNoMapPoint();

    [LoggerMessage(EventId = 3203, Level = LogLevel.Information, Message = "Bot ishga tushdi: @{Username}")]
    private partial void LogStarted(string username);

    [LoggerMessage(EventId = 3204, Level = LogLevel.Error, Message = "Yangilikka ishlov berilmadi: {UpdateId}")]
    private partial void LogUpdateFailed(Exception exception, long updateId);

    [LoggerMessage(EventId = 3205, Level = LogLevel.Debug, Message = "{Count} ta eskirgan suhbat unutildi")]
    private partial void LogExpired(int count);

    [LoggerMessage(
        EventId = 3221,
        Level = LogLevel.Information,
        Message = "Xabarnoma yetkazilmadi (Telegram rad etdi — nomzod botni bloklagan bo'lishi mumkin): «{Vacancy}». Qayta yuborilmaydi")]
    private partial void LogNoticeUndelivered(string vacancy);

    [LoggerMessage(EventId = 3206, Level = LogLevel.Information, Message = "Yangi ariza: «{Vacancy}»")]
    private partial void LogApplication(string vacancy);

    [LoggerMessage(
        EventId = 3220,
        Level = LogLevel.Information,
        Message = "Ariza qabul qilinmadi: anketa to'ldirish paytida qabul yopildi (vakansiya va «Umumiy anketa»)")]
    private partial void LogApplicationsClosed();

    [LoggerMessage(
        EventId = 3210,
        Level = LogLevel.Information,
        Message = "Rad etish xabari yuborildi: «{Vacancy}»")]
    private partial void LogNoticeSent(string vacancy);

    [LoggerMessage(
        EventId = 3212,
        Level = LogLevel.Information,
        Message = "Suhbat taklifi yuborildi: «{Vacancy}»")]
    private partial void LogInterviewNoticeSent(string vacancy);

    [LoggerMessage(
        EventId = 3211,
        Level = LogLevel.Warning,
        Message = "Rad etish xabarlari yuborilmadi — keyingi aylanishda qayta uriniladi")]
    private partial void LogNoticesFailed(Exception exception);

    [LoggerMessage(
        EventId = 3212,
        Level = LogLevel.Warning,
        Message = "Nomzod surati saqlanmadi: {CandidateId}. ARIZA YARATILGAN va nomzod voronkada — "
            + "rasmni HR keyin qo'lda qo'sha oladi.")]
    private partial void LogPhotoFailed(Guid candidateId);

    /// <summary>Tugma ma'lumotidagi ajratgich: «v:<c>guid</c>».</summary>
    private const string VacancyPrefix = "v:";

    /// <summary>Tanlangan vakansiyaga ariza boshlash tugmasi.</summary>
    /// <remarks>
    /// Vakansiyaning O'ZI holatda saqlanadi, shu sababli belgida
    /// identifikator kerak emas. Bu ataylab: tugma faqat «hozir
    /// tanlangan vakansiyaga» tegishli va eski xabardagi tugma bosilsa,
    /// u holatdagi joriy tanlovni ishlatadi — ikkita har xil vakansiya
    /// aralashib ketmaydi.
    /// </remarks>
    private const string ApplyData = "apply";

    private const string LanguageCyrillicData = "lang:cyrl";
    private const string LanguageLatinData = "lang:latn";
    private const string LanguageRussianData = "lang:rus";

    /*
      ANKETA TUGMALARINING MA'LUMOTI.

      Har bir sanoqli javob o'z old qo'shimchasi bilan yuboriladi, qiymat
      esa sanoq a'zosining NOMI bo'ladi («edu:HigherComplete»). Ikkita
      foydasi bor: tugma qaysi QADAMGA tegishli ekani ma'lumotning
      o'zidan ko'rinadi (eski xabardagi tugma bosilganda bu muhim), va
      ma'lumotni ko'rgan odam uning nimani bildirishini o'qiy oladi.

      Telegram chegarasi 64 bayt — eng uzun qiymat («src:» + kanal
      identifikatori, 36 bayt) undan uzoq.
    */
    private const string SkipData = "skip";
    private const string EducationPrefix = "edu:";
    private const string MaritalPrefix = "mar:";
    private const string RussianPrefix = "rus:";
    private const string LicenseYesData = "lic:yes";
    private const string LicenseNoData = "lic:no";
    private const string LicenseCategoryPrefix = "cat:";
    private const string SalaryPrefix = "sal:";
    /// <summary>
    /// «Qayerdan bilgan» tugmasi: «src:<c>guid</c>» — kanal serverdagi ma'lumotnomadan.
    /// </summary>
    /// <remarks>
    /// Identifikator bo'lmagan qiymat (deploydan oldingi xabardagi «src:Telegram» yoki
    /// <see cref="ReferralRetryData"/>) savolni YANGI tugmalar bilan qayta chizadi.
    /// </remarks>
    private const string ReferralPrefix = "src:";

    /// <summary>Kanallar ro'yxati yuklanmaganda — «Qayta urinish».</summary>
    private const string ReferralRetryData = ReferralPrefix + "retry";

    /// <summary>
    /// Server shu kodlar bilan qaytarsa — «qayerdan bilgan» javobi yaroqsiz: kanal shu
    /// orada faolsizlantirilgan yoki aniqlik yetishmaydi. Nomzoddan faqat shu savol qayta
    /// so'raladi.
    /// </summary>
    private static readonly HashSet<string> ReferralErrorCodes =
        new(StringComparer.Ordinal) { "qayerdan.majburiy", "qayerdan.topilmadi", "qayerdan.izoh_majburiy" };
    private const string OfferData = "offer";
    private const string SubmitData = "submit";
    private const string StartOverData = "restart";

    /// <summary>
    /// Serverning surat uchun hajm chegarasi (10 MB).
    /// </summary>
    /// <remarks>
    /// Qiymat <c>PhotoLimits.MaxBytes</c> bilan bir xil, lekin bu yerda
    /// qaytadan yozilgan: bot boshqa loyihalarga havola qilmaydi
    /// (<c>Bonvi.Bot.csproj</c> izohi). U FAQAT Telegram bergan
    /// nusxalardan birini tanlashda ishlatiladi — haqiqiy tekshiruv
    /// baribir serverda.
    /// </remarks>
    private const long MaxPhotoBytes = 10L * 1024 * 1024;

    /// <summary>
    /// Vakansiyasiz ariza JURNALDA shunday ko'rinadi.
    /// </summary>
    /// <remarks>
    /// Faqat jurnal uchun: bot «Umumiy anketa» vakansiyasining
    /// identifikatorini bilmaydi va bilishi ham shart emas — arizani
    /// o'sha yerga server qo'yadi.
    /// </remarks>
    private const string GeneralPoolLabel = "Umumiy anketa";

    /// <summary>Server kodi: vakansiya yopiq — nomzod qabul qilmaydi.</summary>
    private const string VacancyNotAcceptingCode = "vakansiya.qabul_qilmaydi";

    /// <summary>Server kodi: vakansiya topilmadi (o'chirilgan).</summary>
    private const string VacancyNotFoundCode = "vakansiya.topilmadi";

    /// <summary>
    /// «Umumiy anketa» yopiq (yoki sozlanmagan) — vakansiyasiz ariza qabul qilinmaydi.
    /// </summary>
    private const string GeneralPoolUnavailableCode = "nomzod.umumiy_anketa_yoq";

    /// <summary>
    /// Server vaqtincha javob bermaganda arizani qayta yuborish oraliqlari.
    /// </summary>
    /// <remarks>
    /// Jami ~1 daqiqa — deploy'da API ning qayta ko'tarilishiga (migratsiya
    /// bilan birga) yetadigan vaqt. Nomzod shu orada «yuborilmoqda» ni
    /// ko'rib turadi. Uzunroq qilinmaydi: kutish paytida bot boshqa
    /// nomzodlarga javob bermaydi. <c>internal</c> — testlar kutmasdan
    /// tekshirishi uchun.
    /// </remarks>
    internal IReadOnlyList<TimeSpan> SubmitRetryDelays { get; init; } =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30),
    ];

    /// <summary>
    /// To'liq kutishdan keyin ham API javob bermasa, shuncha vaqt yangi
    /// kutish qilinmaydi (<see cref="RegisterWithRetryAsync"/>).
    /// </summary>
    private static readonly TimeSpan ApiDownPause = TimeSpan.FromMinutes(2);

    /// <summary>Shu paytgacha ariza yuborishda kutilmaydi.</summary>
    private DateTimeOffset _apiDownUntil = DateTimeOffset.MinValue;

    /// <summary>
    /// Telegram izohining (caption) eng katta uzunligi.
    /// </summary>
    /// <remarks>
    /// Telegram chegarasi 1024 belgi. 1000 — unga tayanadi va HTML
    /// teglari uchun ozgina zaxira qoldiradi: matndagi
    /// <c>&lt;b&gt;</c> Telegram uchun ham belgi bo'lib sanaladi.
    /// </remarks>
    private const int MaxCaptionLength = 1000;

    /// <summary>Manzil deb qabul qilinadigan eng qisqa matn.</summary>
    /// <remarks>
    /// Ma'noli eng qisqa javob — <c>«Риштон»</c> (6 belgi). Undan
    /// qisqasi («ха», «-», «уй») manzil emas va HR uni ishlata olmaydi.
    /// Chegara ataylab past: qat'iyroq qoida haqiqiy qisqa nomlarni
    /// («Қўқон») rad etardi.
    /// </remarks>
    private const int MinAddressLength = 5;

    /// <summary>
    /// Manzilning eng katta uzunligi — bazadagi ustun bilan bir xil.
    /// </summary>
    /// <remarks>
    /// Nomzod uzun matn yozsa u KESILADI, ariza esa baribir yuboriladi.
    /// API'ga uzunroq matn borsa 400 qaytardi va nomzod ekranda
    /// sababsiz «xatolik» ko'rardi — anketaning oxirgi qadamida bu
    /// butun arizani yo'qotishga teng.
    /// </remarks>
    private const int MaxAddressLength = 500;

    private readonly TelegramClient _telegram;
    private readonly TelegramToken _token;
    private readonly BonviClient _bonvi;
    private readonly ConversationStore _conversations;
    private readonly BotOptions _options;
    private readonly ILogger<BotWorker> _logger;

    private long _offset;

    /// <summary>Ketma-ket nechta «getUpdates» chaqiruvi yiqildi.</summary>
    /// <remarks>
    /// Orqaga chekinish uchun (izohi halqaning ichida): muvaffaqiyatli
    /// javob kelishi bilan nolga qaytadi.
    /// </remarks>
    private int _pollFailures;

    /// <summary>Tizimdan olingan sozlama va u qachon o'qilgani.</summary>
    /// <remarks>
    /// Har `/start` da so'rov yuborish kerak emas — matn kamdan-kam
    /// o'zgaradi. Besh daqiqalik kesh: administrator ekranda
    /// o'zgartirsa, u shu muddat ichida botga yetib boradi va nomzod
    /// buni sezmaydi ham.
    /// </remarks>
    private BotSettings? _settings;

    /// <summary>
    /// Telegram'ga yuklangan logotipning belgisi.
    /// </summary>
    /// <remarks>
    /// Xotirada saqlanadi va bot qayta ishga tushganda yo'qoladi —
    /// bu ATAYLAB. Belgi bot TOKENIGA bog'langan: token almashsa u
    /// baribir eskiradi, ya'ni uni bazaga yozib qo'yish ishonchni
    /// oshirmasdi, faqat yana bir eskiradigan qiymat qo'shardi.
    /// </remarks>
    private string? _logoFileId;
    private DateTimeOffset _settingsReadAt;

    /// <summary>Hozir ISHLAYOTGAN token. <c>null</c> — bot ulanmagan.</summary>
    /// <remarks>
    /// Bu «tizimda yozilgan token» emas, «Telegram qabul qilgan va shu
    /// bilan so'rov ketayotgan token». Ikkalasi farq qilganda bot qayta
    /// ulanadi (<c>EnsureTokenAsync</c>).
    /// </remarks>
    private string? _activeToken;

    /// <summary>The username still to pass to the server; <c>null</c> once it took it.</summary>
    private string? _usernameToReport;

    /// <summary>Tizimdan oxirgi marta muvaffaqiyatli o'qilgan token.</summary>
    /// <remarks>
    /// <c>null</c> ikki xil ma'no tashiydi va ular
    /// <see cref="_tokenEverRead"/> bilan ajratiladi: hali o'qilmagan,
    /// yoki o'qildi-yu ekranda qo'yilmagan.
    /// </remarks>
    private string? _settingsToken;
    private bool _tokenEverRead;
    private DateTimeOffset _tokenReadAt;

    /// <summary>Telegram rad etgan token — xato JURNALGA bir marta tushsin uchun.</summary>
    /// <remarks>
    /// Halqa har 15 soniyada aylanadi. Bu maydonsiz noto'g'ri token
    /// bir kechada minglab bir xil satr yozib, jurnal saqlash
    /// chegarasini (<c>LOG_MAX_FILE_COUNT</c>) yeb qo'yardi va haqiqiy
    /// xatolarni surib chiqarardi. Qiymatning O'ZI hech qachon
    /// jurnalga tushmaydi — u faqat «bu haqda aytganmidim?» degan
    /// savolga javob berish uchun xotirada turadi.
    /// </remarks>
    private string? _complainedAbout;

    public BotWorker(
        TelegramClient telegram,
        TelegramToken token,
        BonviClient bonvi,
        ConversationStore conversations,
        IOptions<BotOptions> options,
        ILogger<BotWorker> logger)
    {
        _telegram = telegram;
        _token = token;
        _bonvi = bonvi;
        _conversations = conversations;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Token yo'q yoki yaroqsiz bo'lganda qayta urinish oralig'i.</summary>
    /// <remarks>
    /// Qisqa ATAYLAB: bu holatda odam ekran oldida turib tokenni endi
    /// kiritgan bo'ladi va botning tirilishini kutadi. 15 soniya —
    /// «darhol» bilan «API ni bekorga urish» orasidagi o'rta yo'l.
    /// </remarks>
    private static readonly TimeSpan TokenRetryDelay = TimeSpan.FromSeconds(15);

    /// <summary>Ishlab turgan bot tokenni qayta o'qish oralig'i.</summary>
    /// <remarks>
    /// Bot ishlayotganda shoshilishning hojati yo'q, lekin bir daqiqa
    /// «matn» sozlamasidagi besh daqiqadan qisqa: matn eskirsa nomzod
    /// bir marta eski matnni o'qiydi, token eskirsa esa bot BEGONA
    /// hisobga xabar yuborishda davom etadi — masalan token o'g'irlangan
    /// deb bekor qilingan bo'lsa.
    /// </remarks>
    private static readonly TimeSpan TokenRefresh = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        /*
          ⭐ ISHGA TUSHISH TARTIBI O'ZGARDI: avval BONVI, keyin TELEGRAM.

          Ilgari teskari edi — token `.env` dan kelardi va u bilan darhol
          Telegram'ga ulanish mumkin edi. Endi token tizim sozlamalarida
          turadi (`SystemSettingKeys.BotToken`), ya'ni uni bilish uchun
          avval Bonvi API ga kirish kerak.

          Shundan kelib chiqib «sozlanmagan» tushunchasi ham toraydi: bot
          hisobining logini va paroli bo'lmasa haqiqatan hech nima qilib
          bo'lmaydi va jarayon to'xtaydi. Token esa endi to'xtash sababi
          EMAS — u har lahzada ekranda paydo bo'lishi mumkin.
        */
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(_options.Login)) missing.Add("bot hisobi logini");
        if (string.IsNullOrWhiteSpace(_options.Password)) missing.Add("bot hisobi paroli");

        if (missing.Count > 0)
        {
            LogNotConfigured(string.Join(", ", missing));
            return;
        }

        if (!await _bonvi.SignInAsync(stoppingToken).ConfigureAwait(false))
        {
            // Bu YOMON, lekin halokat emas: API keyinroq ko'tarilishi
            // mumkin va birinchi so'rovda qayta kiriladi.
            LogApiUnavailable();
        }

        var lastCleanup = DateTimeOffset.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            /*
              Har aylanishda tekshiriladi: token bormi, o'zgardimi.
              Yo'q yoki yaroqsiz bo'lsa — kutib, qaytadan uriniladi.

              NEGA `return` emas (ilgari shunday edi): tokenni endi ekrandan
              qo'yish mumkin va bot buni sezishi kerak. `return` bilan
              «tokenni kiritdim, nega ishlamayapti?» degan savolning
              yagona javobi «konteynerni qayta ishga tushiring» bo'lardi —
              ya'ni butun o'zgarishning ma'nosi yo'qolardi.
            */
            if (!await EnsureTokenAsync(stoppingToken).ConfigureAwait(false))
            {
                await Task.Delay(TokenRetryDelay, stoppingToken).ConfigureAwait(false);
                continue;
            }

            var updates = await _telegram
                .GetUpdatesAsync(_offset, _options.PollTimeoutSeconds, stoppingToken)
                .ConfigureAwait(false);

            /*
              ⭐ CHAQIRUV YIQILGANDA ORQAGA CHEKINISH.

              `null` — Telegram javob bermadi yoki rad etdi (eng ko'p
              uchraydigani: 409 «terminated by other getUpdates request»,
              ya'ni bitta token bilan ikkinchi nusxa ishga tushgan).
              Bunday paytda uzoq so'rov KUTMAYDI, darhol qaytadi — halqa
              esa avval bu farqni ko'rmasdi va to'xtovsiz aylanardi.

              Narxi o'lchangan: bot API'ga sekundiga yuzlab so'rov urardi,
              tezlik cheklovi ishga tushardi va xato jurnaliga kuniga
              400 000+ ogohlantirish yozilardi — haqiqiy xatolar o'sha
              shovqin ichida ko'rinmay qolardi.

              Kutish vaqti bosqichma-bosqich o'sadi (5 → 10 → 20 → 30 s):
              qisqa uzilish tez tiklanadi, uzoq muammo esa tinch kutiladi.
              Yangilik muvaffaqiyatli olinishi bilan hisob nolga qaytadi.
            */
            if (updates is null)
            {
                _pollFailures++;

                var kutish = TimeSpan.FromSeconds(Math.Min(5 * Math.Pow(2, _pollFailures - 1), 30));

                await Task.Delay(kutish, stoppingToken).ConfigureAwait(false);
                continue;
            }

            _pollFailures = 0;

            foreach (var update in updates)
            {
                try
                {
                    await HandleAsync(update, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    LogUpdateFailed(ex, update.UpdateId);
                }

                // Tasdiqlash faqat ishlov berilgandan KEYIN (izohi
                // `TelegramClient.GetUpdatesAsync` da).
                _offset = update.UpdateId + 1;
            }

            if (DateTimeOffset.UtcNow - lastCleanup > TimeSpan.FromMinutes(10))
            {
                var removed = _conversations.RemoveExpired();
                lastCleanup = DateTimeOffset.UtcNow;

                if (removed > 0)
                {
                    LogExpired(removed);
                }
            }

            /*
              RAD ETISH XABARLARI — halqaning shu yerida, yangiliklardan
              KEYIN.

              Nega alohida taymer emas: `GetUpdatesAsync` uzoq so'rov
              (long polling) va u aylanishning tezligini o'zi
              belgilaydi — xabar kelmasa 30 soniya kutadi. Ya'ni bu
              tekshiruv ham har 30 soniyada bir marta bo'ladi va
              nomzod javobni yarim daqiqa ichida oladi. Alohida taymer
              qo'shish ikkita ish jadvalini bir-biriga urishtirardi.

              Xato butun halqani to'xtatmaydi. Har nomzodga BITTA urinish
              (egasining qarori, 2026-10-05): Telegram aniq rad etsa (botni
              bloklagan) natija «yetkazilmadi» bo'lib yoziladi va qayta
              yuborilmaydi; faqat Telegram'ga umuman yetib bo'lmagan xabar
              (tarmoq, 5xx) keyingi aylanishda yuboriladi — u ketmagan,
              ya'ni nomzod takroriy xabar olmaydi.
            */
            try
            {
                await SendNoticesAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogNoticesFailed(ex);
            }
        }
    }

    /// <summary>
    /// Navbatdagi xabarnomalar: rad etish, keyin suhbat taklifi.
    /// </summary>
    /// <remarks>⭐ <c>internal</c> — TESTLAR uchun (<c>InternalsVisibleTo</c>).</remarks>
    internal async Task SendNoticesAsync(CancellationToken cancellationToken)
    {
        await ReportUsernameAsync(cancellationToken).ConfigureAwait(false);
        await SendRejectionNoticesAsync(cancellationToken).ConfigureAwait(false);
        await SendInterviewNoticesAsync(cancellationToken).ConfigureAwait(false);
        await SendInterviewRemindersAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Xabarnoma bilan ketadigan menyu — «Umumiy anketa» yopiq bo'lsa
    /// «Anketa to'ldirish» siz (<see cref="MenuLabelsAsync"/> bilan bir qoida).
    /// </summary>
    private async Task<List<string>> NoticeMenuAsync(BotLanguage language, CancellationToken cancellationToken)
        => BotMenu.Labels(
            language,
            hasAbout: false,
            hasContacts: false,
            hasQuestionnaire: await GeneralPoolMenuStateAsync(cancellationToken).ConfigureAwait(false) != false);

    /// <summary>
    /// Rad etilgan nomzodlarga javob yozadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tartib QAT'IY: avval xabar yuboriladi, KEYIN belgilanadi.
    /// Teskarisi bo'lsa tarmoq uzilganda nomzod javobsiz qolardi va
    /// buni hech kim bilmasdi — belgi esa «yuborildi» deb turardi.
    /// </para>
    /// <para>
    /// Aksincha xato ham bor: xabar ketib, belgilash bajarilmasligi
    /// mumkin. Unda nomzod xabarni IKKI marta oladi. Ikkalasidan
    /// birini tanlash kerak edi va takroriy xabar javobsiz
    /// qoldirishdan yaxshiroq.
    /// </para>
    /// </remarks>
    private async Task SendRejectionNoticesAsync(CancellationToken cancellationToken)
    {
        var pending = await _bonvi.GetPendingRejectionNoticesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var notice in pending)
        {
            /*
              Til FAQAT menyu yorliqlari uchun: rad etish matnining
              o'zi hammaga bir xil va ikki tilli
              (`BotTexts.RejectionNotice` izohi). Menyu esa nomzod
              tanlagan tilda qolaveradi — u xabarning bir qismi emas,
              botning doimiy tugmalari.
            */
            var language = BotLanguages.Parse(notice.Language);

            var outcome = await _telegram
                .SendNoticeAsync(
                    notice.ChatId,
                    BotTexts.RejectionNotice(notice.FullName),
                    await NoticeMenuAsync(language, cancellationToken).ConfigureAwait(false),
                    cancellationToken)
                .ConfigureAwait(false);

            if (outcome == TelegramDelivery.Unreachable)
            {
                // Telegram'ga yetib bo'lmadi — xabar KETMAGAN. Navbatda qoladi
                // va keyingi aylanishda yuboriladi (nomzod takror olmaydi).
                continue;
            }

            /*
              Bitta urinish (egasining qarori, 2026-10-05): Telegram aniq rad
              etsa ham (nomzod botni bloklagan) yozuv navbatdan chiqadi va
              natija «yetkazilmadi» bo'lib qoladi. Ilgari u belgilanmasdi va
              bot uni har ~30 soniyada qayta urardi — kuniga ~6000 marta;
              20 dan ortiq bloklagan nomzod yig'ilsa navbat (20 tadan)
              tiqilib, YANGI rad xabarlari umuman ketmay qolardi.
            */
            var delivered = outcome == TelegramDelivery.Delivered;

            await _bonvi
                .MarkRejectionNoticeSentAsync(notice.CandidateId, cancellationToken, delivered)
                .ConfigureAwait(false);

            if (!delivered)
            {
                LogNoticeUndelivered(notice.VacancyTitle);
                continue;
            }

            LogNoticeSent(notice.VacancyTitle);
        }
    }

    /// <summary>
    /// Suhbat takliflarini yuboradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="SendRejectionNoticesAsync"/> ning aynan naqshi — o'sha
    /// izohdagi barcha qoidalar (belgilash xabardan KEYIN, har nomzodga
    /// BITTA urinish — bloklagan nomzod «yetkazilmadi» bo'lib navbatdan
    /// chiqadi) shu yerda ham amal qiladi.
    /// </para>
    /// <para>
    /// <b>Xaritadagi nuqta ALOHIDA xabar bo'lib ketadi</b> — kontaktlar
    /// bo'limidagi bilan bir xil: Telegram uni ochib navigatorga uzatadi
    /// va matn ichidagi havola bunday ishlamaydi. Nuqta yuborilmasa ham
    /// taklif YUBORILGAN hisoblanadi: asosiy ma'lumot — vaqt — nomzodga
    /// allaqachon yetgan.
    /// </para>
    /// </remarks>
    private async Task SendInterviewNoticesAsync(CancellationToken cancellationToken)
    {
        var pending = await _bonvi.GetPendingInterviewNoticesAsync(cancellationToken).ConfigureAwait(false);

        if (pending.Count == 0)
        {
            return;
        }

        var settings = await SettingsAsync(cancellationToken).ConfigureAwait(false);
        var point = MapPoint.TryParse(settings?.Location);

        foreach (var notice in pending)
        {
            var language = BotLanguages.Parse(notice.Language);

            var outcome = await _telegram
                .SendNoticeAsync(
                    notice.ChatId,
                    BotTexts.InterviewNotice(
                        language,
                        notice.FullName,
                        notice.VacancyTitle,
                        notice.InterviewAt,
                        settings?.InterviewPhone ?? string.Empty,
                        settings?.InterviewAddress ?? string.Empty),
                    await NoticeMenuAsync(language, cancellationToken).ConfigureAwait(false),
                    cancellationToken)
                .ConfigureAwait(false);

            if (outcome == TelegramDelivery.Unreachable)
            {
                continue;
            }

            // Bitta urinish — rad etish xabaridagi qoida. Yetkazilmasa HR
            // ekranida «yetkazilmadi — telefon qiling» chiqadi.
            var delivered = outcome == TelegramDelivery.Delivered;

            if (delivered)
            {
                await SendPlaceAsync(notice, point, cancellationToken).ConfigureAwait(false);
            }

            await _bonvi
                .MarkInterviewNoticeSentAsync(notice.CandidateId, cancellationToken, delivered)
                .ConfigureAwait(false);

            if (!delivered)
            {
                LogNoticeUndelivered(notice.VacancyTitle);
                continue;
            }

            LogInterviewNoticeSent(notice.VacancyTitle);
        }
    }

    /// <summary>
    /// The day-before reminder: same text parts and map point as the invitation, one attempt.
    /// </summary>
    private async Task SendInterviewRemindersAsync(CancellationToken cancellationToken)
    {
        var pending = await _bonvi.GetPendingInterviewRemindersAsync(cancellationToken).ConfigureAwait(false);

        if (pending.Count == 0)
        {
            return;
        }

        var settings = await SettingsAsync(cancellationToken).ConfigureAwait(false);
        var point = MapPoint.TryParse(settings?.Location);

        foreach (var notice in pending)
        {
            var language = BotLanguages.Parse(notice.Language);

            var outcome = await _telegram
                .SendNoticeAsync(
                    notice.ChatId,
                    BotTexts.InterviewReminder(
                        language,
                        notice.FullName,
                        notice.VacancyTitle,
                        notice.InterviewAt,
                        settings?.InterviewPhone ?? string.Empty,
                        settings?.InterviewAddress ?? string.Empty),
                    await NoticeMenuAsync(language, cancellationToken).ConfigureAwait(false),
                    cancellationToken)
                .ConfigureAwait(false);

            if (outcome == TelegramDelivery.Unreachable)
            {
                continue;
            }

            var delivered = outcome == TelegramDelivery.Delivered;

            if (delivered)
            {
                await SendPlaceAsync(notice, point, cancellationToken).ConfigureAwait(false);
            }

            await _bonvi
                .MarkInterviewReminderSentAsync(notice.CandidateId, cancellationToken, delivered)
                .ConfigureAwait(false);

            if (delivered)
            {
                LogInterviewReminderSent(notice.VacancyTitle);
            }
            else
            {
                LogNoticeUndelivered(notice.VacancyTitle);
            }
        }
    }

    /// <summary>The map point as its own message; the time already reached them, so a failure is only logged.</summary>
    private async Task SendPlaceAsync(InterviewNotice notice, MapPoint? point, CancellationToken cancellationToken)
    {
        if (point is not { } place)
        {
            LogNoMapPoint();
            return;
        }

        var sent = await _telegram
            .SendLocationAsync(notice.ChatId, place.Latitude, place.Longitude, cancellationToken)
            .ConfigureAwait(false);

        if (!sent)
        {
            LogLocationNotSent(notice.VacancyTitle);
        }
    }

    /// <summary>Passes on the username <c>getMe</c> gave, retrying each round until the server took it.</summary>
    private async Task ReportUsernameAsync(CancellationToken cancellationToken)
    {
        if (_usernameToReport is not { } username)
        {
            return;
        }

        if (await _bonvi.ReportBotUsernameAsync(username, cancellationToken).ConfigureAwait(false))
        {
            _usernameToReport = null;
        }
    }

    /// <summary>
    /// Bitta yangilikka ishlov beradi.
    /// </summary>
    /// <remarks>
    /// ⭐ <c>internal</c> — TESTLAR uchun (<c>InternalsVisibleTo</c>,
    /// izohi <c>Bonvi.Bot.csproj</c> da). Anketaning 22 qadamini
    /// tekshirishning yagona halol yo'li — botga xuddi Telegram
    /// yuborgandek yangilik berish va u qaysi savolni chizganiga qarash.
    /// Qadam mantig'ini alohida sinfga ko'chirish esa uni ikkinchi marta
    /// yozish bo'lardi.
    /// </remarks>
    internal async Task HandleAsync(TelegramUpdate update, CancellationToken cancellationToken)
    {
        if (update.CallbackQuery is { } callback)
        {
            await HandleCallbackAsync(callback, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (update.Message is { } message)
        {
            await HandleMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandleCallbackAsync(TelegramCallbackQuery callback, CancellationToken cancellationToken)
    {
        // Birinchi navbatda tugmaning «soatini» o'chiramiz — javob
        // kechiksa ham foydalanuvchi bosish qabul qilinganini ko'radi.
        await _telegram.AnswerCallbackAsync(callback.Id, cancellationToken).ConfigureAwait(false);

        if (callback.Message?.Chat is not { } chat || callback.Data is not { } data)
        {
            return;
        }

        var state = _conversations.Find(chat.Id);

        if (data is LanguageCyrillicData or LanguageLatinData or LanguageRussianData)
        {
            await ChooseLanguageAsync(chat.Id, state, data, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (state is null)
        {
            // Bot qayta ishga tushgan yoki suhbat eskirgan: eski tugma
            // hamon ekranda turibdi va bosilishi mumkin.
            await _telegram
                .SendMessageAsync(chat.Id, BotTexts.Restart(BotLanguage.Cyrillic), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        if (data == StartOverData)
        {
            // «Boshidan boshlash» — `/start` bilan AYNAN bir xil ish.
            // Til esa saqlanadi: nomzod uni endigina tanlagan.
            _conversations.Save(
                chat.Id,
                new ConversationState { Step = ConversationStep.Language, Language = state.Language });

            await AskLanguageAsync(chat.Id, cancellationToken).ConfigureAwait(false);
            return;
        }

        await HandleAnswerAsync(chat.Id, state, data, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Til tanlandi — yarim to'ldirilgan anketa SAQLANADI.</summary>
    /// <remarks>
    /// Til almashtirish arizani bekor qilish emas: nomzod o'n savolga
    /// javob berib bo'lib tilni almashtirsa, u hammasini qaytadan
    /// yozishi kerak bo'lmasligi kerak — shu sababli mavjud holat
    /// ustiga FAQAT til yoziladi va qadam ham o'zgarmaydi.
    /// </remarks>
    private async Task ChooseLanguageAsync(
        long chatId,
        ConversationState? state,
        string data,
        CancellationToken cancellationToken)
    {
        var language = data switch
        {
            LanguageCyrillicData => BotLanguage.Cyrillic,
            LanguageRussianData => BotLanguage.Russian,
            _ => BotLanguage.Latin,
        };

        _conversations.Save(
            chatId,
            state is null
                ? new ConversationState { Step = ConversationStep.Vacancy, Language = language }
                : state with { Language = language });

        await SendWithMenuAsync(chatId, BotTexts.Greeting(language), language, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Tugma javobini QADAMGA qarab qabul qiladi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Tugma hozirgi qadamga tegishli bo'lmasa JIMGINA e'tiborsiz
    /// qoldiriladi.</b> Eski xabarlar suhbatda qolib ketadi va nomzod
    /// yuqoriga surib, allaqachon o'tilgan savolning tugmasini bosishi
    /// mumkin. Uni qabul qilish anketani orqaga qaytarardi, xato yozish
    /// esa nomzodni chalg'itardi — javob bermaslik eng tinch yo'l.
    /// </para>
    /// <para>
    /// «O'tkazib yuborish» bosilganda javob YOZILMAYDI: maydon
    /// <c>null</c> bo'lib qoladi va yakuniy xulosada umuman
    /// chizilmaydi (<see cref="AnswerSummary"/>).
    /// </para>
    /// </remarks>
    private async Task HandleAnswerAsync(
        long chatId,
        ConversationState state,
        string data,
        CancellationToken cancellationToken)
    {
        switch (state.Step)
        {
            case ConversationStep.Vacancy when data == ApplyData && state.VacancyId is not null:
                // Vakansiya allaqachon holatda — anketa 3-qadamdan ketadi.
                await ContinueAsync(chatId, state, ConversationStep.Name, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.Vacancy when data.StartsWith(VacancyPrefix, StringComparison.Ordinal):
                await ChooseVacancyAsync(chatId, state, data[VacancyPrefix.Length..], cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.Education
                when ParseAnswer<EducationAnswer>(data, EducationPrefix) is { } education:
                await AcceptAsync(chatId, state with { Education = education }, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.MaritalStatus
                when ParseAnswer<MaritalAnswer>(data, MaritalPrefix) is { } marital:
                await AcceptAsync(chatId, state with { MaritalStatus = marital }, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.RussianLevel
                when ParseAnswer<RussianLevelAnswer>(data, RussianPrefix) is { } russian:
                await AcceptAsync(chatId, state with { RussianLevel = russian }, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.DriverLicense when data is LicenseYesData or LicenseNoData:
            {
                /*
                  Toifa ATAYLAB tozalanadi: nomzod «Ha» deb toifani
                  tanlab, keyin savolga qaytib «Yo'q» ni bosishi mumkin.
                  Eski toifa qolib ketsa, xulosada «guvohnoma: yo'q, BC»
                  degan qarama-qarshi qator chiqardi.
                */
                var hasLicense = data == LicenseYesData;

                await AcceptAsync(
                        chatId,
                        state with { HasDriverLicense = hasLicense, DriverLicenseCategory = null },
                        cancellationToken)
                    .ConfigureAwait(false);

                break;
            }

            case ConversationStep.DriverLicenseCategory when ParseCategory(data) is { } category:
                await AcceptAsync(chatId, state with { DriverLicenseCategory = category }, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.ExpectedSalary
                when ParseAnswer<SalaryAnswer>(data, SalaryPrefix) is { } salary:
                await AcceptAsync(chatId, state with { ExpectedSalary = salary }, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.ReferralSource when data.StartsWith(ReferralPrefix, StringComparison.Ordinal):
                await ChooseReferralAsync(chatId, state, data[ReferralPrefix.Length..], cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.Offer when data == OfferData:
                // Rozilik VAQTI — huquqiy fakt, bayroq emas.
                await AcceptAsync(chatId, state with { OfferAcceptedAt = BotClock.Now }, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.Confirm when data == SubmitData:
                await SubmitAsync(chatId, state, cancellationToken).ConfigureAwait(false);
                break;

            case ConversationStep.Speciality when data == SkipData:
            case ConversationStep.WorkExperience when data == SkipData:
            case ConversationStep.OtherLanguages when data == SkipData:
            case ConversationStep.OfficeSkills when data == SkipData:
            case ConversationStep.LastSalary when data == SkipData:
            case ConversationStep.ExtraNote when data == SkipData:
            case ConversationStep.Photo when data == SkipData:
                await AcceptAsync(chatId, state, cancellationToken).ConfigureAwait(false);
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// «Qayerdan bilgan» kanali tanlandi (20-qadam).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kanal serverdagi FAOL ro'yxatdan qayta tekshiriladi: tugma bosilguncha HR uni
    /// faolsizlantirgan bo'lishi mumkin, server esa bunday tanlovni rad etardi —
    /// anketaning OXIRIDA. Topilmasa (yoki tugma deploydan oldingi «src:Telegram»
    /// bo'lsa) savol yangi tugmalar bilan qayta chiziladi.
    /// </para>
    /// <para>
    /// Aniqlik har yangi tanlovda TOZALANADI: «Tanishlar orqali — Rustamov» dan keyin
    /// «Telegram» tanlansa, eski ism xulosada qolib ketmasin.
    /// </para>
    /// </remarks>
    private async Task ChooseReferralAsync(
        long chatId,
        ConversationState state,
        string rawId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ReferralChannel> channels = Guid.TryParse(rawId, out var channelId)
            ? await _bonvi.GetReferralChannelsAsync(cancellationToken).ConfigureAwait(false)
            : [];

        if (channels.FirstOrDefault(channel => channel.Id == channelId) is not { } chosen)
        {
            await ContinueAsync(chatId, state, ConversationStep.ReferralSource, cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        await AcceptAsync(
                chatId,
                state with
                {
                    ReferralChannelId = chosen.Id,
                    ReferralChannelName = chosen.Name,
                    ReferralAsksDetail = chosen.AsksDetail,
                    ReferralDetail = null,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Vakansiya tanlandi.</summary>
    private async Task ChooseVacancyAsync(
        long chatId,
        ConversationState state,
        string rawId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(rawId, out var vacancyId))
        {
            return;
        }

        var vacancies = await _bonvi.GetOpenVacanciesAsync(cancellationToken).ConfigureAwait(false);
        var chosen = vacancies.FirstOrDefault(vacancy => vacancy.Id == vacancyId);

        if (chosen is null)
        {
            /*
              Vakansiya tugma bosilgunicha yopilgan bo'lishi mumkin.
              Ro'yxat qaytadan chiziladi — agar u shu orada butunlay
              bo'shab qolgan bo'lsa, anketa TO'XTAMAYDI: qadam o'tkazib
              yuboriladi va ariza «Umumiy anketa»ga ketadi.
            */
            await ContinueAsync(chatId, state, ConversationStep.Vacancy, cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        /*
          ⭐ Anketa BU YERDA boshlanmaydi. Nomzod vakansiya nomini
          ko'rib, «Ariza topshirish» tugmasini bosgandan keyingina
          savollar ketadi (foydalanuvchi qarori, 2026-09-07).

          Sabab: ro'yxatdan tugma bosish «qiziqdim» degani, «ariza
          beraman» degani emas. Ilgari bir bosish darhol yigirmata
          savolni boshlab yuborardi va nomzod ortga qayta olmasdi.

          Holat SAQLANADI (qadam o'zgarmaydi): tanlangan vakansiya
          yozib qo'yiladi va tugma bosilganda o'sha ishlatiladi.
        */
        var chosenState = state with { VacancyId = chosen.Id, VacancyTitle = chosen.Title };

        _conversations.Save(chatId, chosenState);

        await SendAsync(
                chatId,
                BotTexts.VacancyChosen(state.Language, chosen.Title),
                [[new InlineButton(BotTexts.ApplyButton(state.Language), ApplyData)]],
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Ochiq ish o'rinlarini ko'rsatadi — anketani boshlamasdan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ro'yxat BO'SH bo'lsa nomzod «anketa to'ldirish» ga
    /// yo'naltiriladi va menyu qayta chiziladi: yo'l oxiri emas, boshqa
    /// yo'l taklif qilinadi (izohi <see cref="BotTexts.NoOpenVacancies"/>).
    /// </para>
    /// <para>
    /// Holat <see cref="ConversationStep.Vacancy"/> da qoladi — nomzod
    /// tugmani hozir bosmasdan, keyinroq ham bosishi mumkin.
    /// </para>
    /// </remarks>
    private async Task ShowVacanciesAsync(
        long chatId,
        ConversationState state,
        CancellationToken cancellationToken)
    {
        var vacancies = await _bonvi.GetOpenVacanciesAsync(cancellationToken).ConfigureAwait(false);

        if (vacancies.Count == 0)
        {
            // «Anketa to'ldirish» faqat «Umumiy anketa» ochiq bo'lsa tavsiya
            // qilinadi — yopiq anketaga yo'naltirish nomzodni boshi berk
            // ko'chaga olib kirardi.
            var questionnaireOpen = await _bonvi.IsGeneralPoolOpenAsync(cancellationToken).ConfigureAwait(false) != false;

            await SendWithMenuAsync(
                    chatId,
                    questionnaireOpen
                        ? BotTexts.NoOpenVacancies(state.Language)
                        : BotTexts.NoOpenVacanciesNoQuestionnaire(state.Language),
                    state.Language,
                    cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        await SendAsync(
                chatId,
                BotTexts.ChooseVacancy(state.Language),
                VacancyButtons(vacancies),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// «📋 Anketa to'ldirish» bosildi, lekin «Umumiy anketa» yopiq: holat aytiladi
    /// va ochiq vakansiyalar bo'lsa ular ko'rsatiladi. Holat
    /// <see cref="ConversationStep.Vacancy"/> da qoladi — nomzod ro'yxatdan
    /// tanlashi mumkin.
    /// </summary>
    private async Task QuestionnaireClosedAsync(
        long chatId,
        ConversationState state,
        CancellationToken cancellationToken)
    {
        var vacancies = await _bonvi.GetOpenVacanciesAsync(cancellationToken).ConfigureAwait(false);

        if (vacancies.Count == 0)
        {
            await SendWithMenuAsync(
                    chatId,
                    BotTexts.NoOpenVacanciesNoQuestionnaire(state.Language),
                    state.Language,
                    cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        await SendWithMenuAsync(
                chatId,
                BotTexts.QuestionnaireClosed(state.Language, hasOpenVacancies: true),
                state.Language,
                cancellationToken)
            .ConfigureAwait(false);

        await SendAsync(
                chatId,
                BotTexts.ChooseVacancy(state.Language),
                VacancyButtons(vacancies),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Javob qabul qilindi — keyingi qadamga o'tiladi.</summary>
    private Task AcceptAsync(long chatId, ConversationState answered, CancellationToken cancellationToken)
        => ContinueAsync(chatId, answered, NextStep(answered.Step), cancellationToken);

    /// <summary>
    /// Chizilishi kerak bo'lgan birinchi qadamni topadi va uni chizadi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>SHARTLI qadamlar aynan shu yerda o'tkazib yuboriladi</b> va
    /// boshqa hech qayerda: ochiq vakansiya bo'lmasa 2-qadam, guvohnoma
    /// yo'q bo'lsa 17-qadam, oferta havolasi sozlanmagan bo'lsa
    /// 21-qadam. Har bir chaqiruv joyida takrorlangan <c>if</c> lar
    /// vaqt o'tib bir-biridan farq qila boshlardi va nomzod qadamsiz
    /// qolib ketardi.
    /// </para>
    /// <para>
    /// <b>Halqa CHEKSIZ emas:</b> 22-qadam (yakuniy tasdiq) hech qachon
    /// o'tkazib yuborilmaydi va halqa unga yetganda albatta to'xtaydi.
    /// </para>
    /// </remarks>
    private async Task ContinueAsync(
        long chatId,
        ConversationState state,
        ConversationStep step,
        CancellationToken cancellationToken)
    {
        var settings = await SettingsAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<OpenVacancy> vacancies = [];
        var current = step;

        while (current != ConversationStep.Confirm)
        {
            if (current == ConversationStep.Vacancy)
            {
                vacancies = await _bonvi.GetOpenVacanciesAsync(cancellationToken).ConfigureAwait(false);

                if (vacancies.Count > 0)
                {
                    break;
                }

                /*
                  Vakansiya qadami o'tkazib yuborilmoqda — ariza vakansiyasiz,
                  ya'ni «Umumiy anketa»ga ketadi. U yopiq bo'lsa anketa SHU
                  YERDA to'xtaydi (egasining qarori, 2026-10-05), 22 ta
                  savoldan keyin emas.
                */
                if (await _bonvi.IsGeneralPoolOpenAsync(cancellationToken).ConfigureAwait(false) == false)
                {
                    _conversations.Save(chatId, state with { Step = ConversationStep.Vacancy });

                    await SendWithMenuAsync(
                            chatId,
                            BotTexts.NoOpenVacanciesNoQuestionnaire(state.Language),
                            state.Language,
                            cancellationToken)
                        .ConfigureAwait(false);

                    return;
                }
            }
            else if (Applies(current, state, settings))
            {
                break;
            }

            current = NextStep(current);
        }

        var next = state with { Step = current };

        // Holat SAVOLDAN OLDIN yoziladi: xabar yuborilmasa ham nomzod
        // javobi to'g'ri qadamga tushadi (savolni u ekranda ko'rmagan
        // bo'lsa ham, «/start» bilan qayta boshlay oladi).
        _conversations.Save(chatId, next);

        await AskAsync(chatId, next, vacancies, settings, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Shu qadam nomzodga ko'rsatiladimi.</summary>
    /// <remarks>
    /// <para>
    /// Vakansiya qadami bu yerda YO'Q: uning javobi ochiq vakansiyalar
    /// ro'yxatiga bog'liq, ro'yxat esa API so'rovi — ya'ni javob bu
    /// sinxron funksiyada berilib bo'lmaydi (<see cref="ContinueAsync"/>).
    /// </para>
    /// <para>
    /// Oferta allaqachon qabul qilingan bo'lsa qayta so'ralmaydi: bunga faqat «qayerdan
    /// bilgan» savoli tasdiq qadamidan QAYTA so'ralganda kelinadi
    /// (<see cref="SubmitAsync"/>) — nomzod ofertani bir necha daqiqa oldin ko'rgan.
    /// </para>
    /// </remarks>
    private static bool Applies(ConversationStep step, ConversationState state, BotSettings? settings)
        => step switch
        {
            ConversationStep.DriverLicenseCategory => state.HasDriverLicense == true,
            ConversationStep.ReferralDetail => state.ReferralAsksDetail,
            ConversationStep.Offer => !string.IsNullOrWhiteSpace(settings?.OfferUrl) && state.OfferAcceptedAt is null,
            _ => true,
        };

    /// <summary>Ro'yxatdagi keyingi qadam — oxirgisida o'zi qoladi.</summary>
    private static ConversationStep NextStep(ConversationStep step)
        => step == ConversationStep.Confirm ? step : step + 1;

    /// <summary>Tugma ma'lumotidan sanoq qiymatini o'qiydi.</summary>
    /// <remarks>
    /// Qiymat NOM bilan yuboriladi («edu:HigherComplete»), raqam bilan
    /// emas: shu tufayli tugma ma'lumotini ko'rgan odam uning nimani
    /// bildirishini darhol tushunadi. <c>Enum.IsDefined</c> shart —
    /// tugma ma'lumoti tashqaridan keladi va o'ylab topilgan qiymat
    /// sanoqqa jimgina o'tib ketmasligi kerak.
    /// </remarks>
    private static T? ParseAnswer<T>(string data, string prefix)
        where T : struct, Enum
    {
        if (!data.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        return Enum.TryParse<T>(data[prefix.Length..], out var value) && Enum.IsDefined(value)
            ? value
            : null;
    }

    /// <summary>Guvohnoma toifasi — faqat ro'yxatdagi qiymat qabul qilinadi.</summary>
    private static string? ParseCategory(string data)
    {
        if (!data.StartsWith(LicenseCategoryPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var value = data[LicenseCategoryPrefix.Length..];

        return Array.Exists(Answers.DriverLicenseCategories, item => item == value) ? value : null;
    }

    /// <summary>
    /// Qadamning savolini chizadi.
    /// </summary>
    /// <remarks>
    /// Bu metod HECH NARSANI hal qilmaydi — qaysi qadam chizilishini
    /// <see cref="ContinueAsync"/> allaqachon aytgan. Shu bo'linish
    /// tufayli «qadam tartibi» va «savol qanday ko'rinadi» degan ikki
    /// masala bir-biriga aralashmaydi.
    /// </remarks>
    private async Task AskAsync(
        long chatId,
        ConversationState state,
        IReadOnlyList<OpenVacancy> vacancies,
        BotSettings? settings,
        CancellationToken cancellationToken)
    {
        var language = state.Language;

        switch (state.Step)
        {
            case ConversationStep.Vacancy:
                await SendAsync(chatId, BotTexts.ChooseVacancy(language), VacancyButtons(vacancies), cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.Name:
                await SendAsync(chatId, BotTexts.AskName(language), null, cancellationToken).ConfigureAwait(false);
                break;

            case ConversationStep.BirthDate:
                await SendAsync(chatId, BotTexts.AskBirthDate(language), null, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.Address:
                await SendAsync(chatId, BotTexts.AskAddress(language), null, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.Phone:
                await _telegram
                    .SendMessageAsync(
                        chatId,
                        BotTexts.AskPhone(language),
                        requestContact: true,
                        contactButtonText: BotTexts.PhoneButton(language),
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.Education:
                await SendAsync(
                        chatId,
                        BotTexts.AskEducation(language),
                        Choices(
                            Enum.GetValues<EducationAnswer>(),
                            EducationPrefix,
                            answer => BotTexts.EducationLabel(language, answer)),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.Speciality:
                await SendAsync(chatId, BotTexts.AskSpeciality(language), SkipButtons(language), cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.WorkExperience:
                await SendAsync(chatId, BotTexts.AskWorkExperience(language), SkipButtons(language), cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.MaritalStatus:
                await SendAsync(
                        chatId,
                        BotTexts.AskMaritalStatus(language),
                        Choices(
                            Enum.GetValues<MaritalAnswer>(),
                            MaritalPrefix,
                            answer => BotTexts.MaritalLabel(language, answer)),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.RussianLevel:
                await SendAsync(
                        chatId,
                        BotTexts.AskRussianLevel(language),
                        Choices(
                            Enum.GetValues<RussianLevelAnswer>(),
                            RussianPrefix,
                            answer => BotTexts.RussianLevelLabel(language, answer)),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.OtherLanguages:
                await SendAsync(chatId, BotTexts.AskOtherLanguages(language), SkipButtons(language), cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.OfficeSkills:
                await SendAsync(chatId, BotTexts.AskOfficeSkills(language), SkipButtons(language), cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.LastSalary:
                await SendAsync(chatId, BotTexts.AskLastSalary(language), SkipButtons(language), cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.ExtraNote:
                await SendAsync(chatId, BotTexts.AskExtraNote(language), SkipButtons(language), cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.DriverLicense:
                // «Ha» va «Yo'q» BITTA qatorda: javob qisqa va ikkitasi
                // yonma-yon turgani tanlashni tezlashtiradi.
                await SendAsync(
                        chatId,
                        BotTexts.AskDriverLicense(language),
                        [
                            [
                                new InlineButton(BotTexts.YesButton(language), LicenseYesData),
                                new InlineButton(BotTexts.NoButton(language), LicenseNoData),
                            ],
                        ],
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.DriverLicenseCategory:
                await SendAsync(
                        chatId,
                        BotTexts.AskDriverLicenseCategory(language),
                        [
                            [
                                .. Answers.DriverLicenseCategories.Select(category =>
                                    new InlineButton(category, LicenseCategoryPrefix + category)),
                            ],
                        ],
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.Photo:
                await SendAsync(chatId, BotTexts.AskPhoto(language), SkipButtons(language), cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.ExpectedSalary:
                await SendAsync(
                        chatId,
                        BotTexts.AskExpectedSalary(language),
                        Choices(
                            Enum.GetValues<SalaryAnswer>(),
                            SalaryPrefix,
                            answer => BotTexts.SalaryLabel(language, answer)),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.ReferralSource:
            {
                // Tugmalar ma'lumotnomadan — har safar yangisi (`GetReferralChannelsAsync` izohi).
                // Ro'yxat kelmasa savolsiz qolmasin: bitta «Qayta urinish» tugmasi.
                var channels = await _bonvi.GetReferralChannelsAsync(cancellationToken).ConfigureAwait(false);

                await SendAsync(
                        chatId,
                        channels.Count == 0 ? BotTexts.ReferralUnavailable(language) : BotTexts.AskReferralSource(language),
                        channels.Count == 0
                            ? [[new InlineButton(BotTexts.RetryButton(language), ReferralRetryData)]]
                            : ReferralButtons(channels),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            }

            case ConversationStep.ReferralDetail:
                await SendAsync(
                        chatId,
                        BotTexts.AskReferralDetail(language, state.ReferralChannelName ?? string.Empty),
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.Offer:
                await SendAsync(
                        chatId,
                        BotTexts.Offer(language, settings?.OfferUrl ?? string.Empty),
                        [[new InlineButton(BotTexts.AgreeButton(language), OfferData)]],
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.Confirm:
                await SendAsync(
                        chatId,
                        AnswerSummary.Build(state) + "\n\n" + BotTexts.ConfirmQuestion(language),
                        [
                            [new InlineButton(BotTexts.SubmitButton(language), SubmitData)],
                            [new InlineButton(BotTexts.StartOverButton(language), StartOverData)],
                        ],
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            default:
                // Til qadami bu yerga tushmaydi — u `AskLanguageAsync`
                // bilan alohida chiziladi.
                break;
        }
    }

    /// <summary>Savolni yuboradi.</summary>
    private async Task SendAsync(
        long chatId,
        string text,
        IReadOnlyList<IReadOnlyList<InlineButton>>? buttons,
        CancellationToken cancellationToken)
        => await _telegram
            .SendMessageAsync(chatId, text, buttons, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    /// <summary>«O'tkazib yuborish» tugmasi — yagona qator.</summary>
    private static IReadOnlyList<IReadOnlyList<InlineButton>> SkipButtons(BotLanguage language)
        => [[new InlineButton(BotTexts.SkipButton(language), SkipData)]];

    /// <summary>
    /// Sanoqning har bir qiymatidan bittadan tugma yasaydi.
    /// </summary>
    /// <remarks>
    /// Har biri ALOHIDA qatorda: javoblar uzun («Ўрта махсус (коллеж,
    /// лицей)») va ikkitasi telefon ekranida bir qatorga sig'maydi —
    /// sig'magan matn esa kesilib ko'rinadi.
    /// </remarks>
    private static List<IReadOnlyList<InlineButton>> Choices<T>(T[] answers, string prefix, Func<T, string> label)
        where T : struct, Enum
        =>
        [
            .. answers.Select(answer => (IReadOnlyList<InlineButton>)
                [new InlineButton(label(answer), prefix + answer.ToString())]),
        ];

    /// <summary>«Qayerdan bilgan» kanallarining tugmalari — har biri alohida qatorda.</summary>
    /// <remarks>
    /// Nom saqlanganidek, uchala tilda ham (<see cref="BotTexts.AskReferralSource"/> izohi).
    /// </remarks>
    private static List<IReadOnlyList<InlineButton>> ReferralButtons(IReadOnlyList<ReferralChannel> channels)
        =>
        [
            .. channels.Select(channel => (IReadOnlyList<InlineButton>)
                [new InlineButton(channel.Name, ReferralPrefix + channel.Id.ToString("N"))]),
        ];

    /// <summary>Ochiq vakansiyalarning tugmalari.</summary>
    /// <remarks>
    /// Har bir vakansiya ALOHIDA qatorda: nomlar uzun («2 линия
    /// назратчиси») va ikkitasi bir qatorga sig'maydi.
    /// </remarks>
    private static List<IReadOnlyList<InlineButton>> VacancyButtons(IReadOnlyList<OpenVacancy> vacancies)
        =>
        [
            .. vacancies.Select(vacancy => (IReadOnlyList<InlineButton>)
                [new InlineButton(vacancy.Title, VacancyPrefix + vacancy.Id.ToString("N"))]),
        ];

    /// <summary>
    /// Connects the person to their application: by the token in <c>/start token</c> (the link
    /// from the online form or HR), otherwise by the username they wrote on the form.
    /// </summary>
    /// <returns><c>true</c> when the token linked them and the usual start isn't needed.</returns>
    private async Task<bool> LinkFromStartAsync(long chatId, TelegramMessage message, CancellationToken cancellationToken)
    {
        var parts = message.Text!.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 2)
        {
            var result = await _bonvi.LinkTelegramAsync(parts[1], chatId, cancellationToken).ConfigureAwait(false);

            if (result is { Linked: true })
            {
                var language = BotLanguages.Parse(result.Language);

                await _telegram
                    .SendNoticeAsync(
                        chatId,
                        BotTexts.TelegramLinked(language),
                        await NoticeMenuAsync(language, cancellationToken).ConfigureAwait(false),
                        cancellationToken)
                    .ConfigureAwait(false);

                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(message.From?.Username))
        {
            await _bonvi.MatchTelegramAsync(chatId, message.From.Username, null, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private async Task HandleMessageAsync(TelegramMessage message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;

        // `/start` HAR QANDAY qadamda suhbatni boshidan boshlaydi —
        // adashib qolgan odam uchun yagona ishonchli chiqish yo'li.
        if (message.Text?.StartsWith("/start", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (await LinkFromStartAsync(chatId, message, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            _conversations.Save(chatId, new ConversationState { Step = ConversationStep.Language });
            await AskLanguageAsync(chatId, cancellationToken).ConfigureAwait(false);
            return;
        }

        var state = _conversations.Find(chatId);

        /*
          MENYU QADAMDAN OLDIN tekshiriladi va bu tartib ataylab.

          Doimiy menyu tugmasi oddiy MATN bo'lib keladi. Agar avval
          qadam ishlansa, F.I.Sh. kutilayotgan paytda bosilgan
          «📞 Kontaktlar / Manzil» tugmasi NOMZODNING ISMI sifatida
          o'qilardi va arizaga shu yozilardi. Ya'ni tugmani bosgan odam
          arizadan chiqolmay qolardi — MBF ning botida bu yo'l bor va
          u yerda ham menyu qadamdan ustun turadi.

          ⚠️ Anketa 22 qadamga o'sgach bu qoida YANADA muhim bo'ldi:
          endi tugma matni «mutaxassislik», «ish tajribasi» yoki
          «qo'shimcha ma'lumot» javobiga ham aylanib ketishi mumkin edi.
        */
        var action = BotMenu.Resolve(message.Text);

        if (action != MenuAction.None)
        {
            await HandleMenuAsync(chatId, state, action, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (state is null)
        {
            // Bot qayta ishga tushgan yoki suhbat eskirgan. Til noma'lum,
            // shu sababli darhol til so'raladi — «/start» ni eslashga
            // majburlashdan ko'ra bir qadam kam.
            await AskLanguageAsync(chatId, cancellationToken).ConfigureAwait(false);
            return;
        }

        switch (state.Step)
        {
            case ConversationStep.Name:
                await HandleNameAsync(chatId, state, message.Text, cancellationToken).ConfigureAwait(false);
                break;

            case ConversationStep.BirthDate:
                await HandleBirthDateAsync(chatId, state, message.Text, cancellationToken).ConfigureAwait(false);
                break;

            case ConversationStep.Address:
                await HandleAddressAsync(chatId, state, message.Text, cancellationToken).ConfigureAwait(false);
                break;

            case ConversationStep.Phone:
                await HandlePhoneAsync(chatId, state, message, cancellationToken).ConfigureAwait(false);
                break;

            case ConversationStep.Speciality:
                await SaveTextAsync(
                        chatId,
                        state,
                        message.Text,
                        Answers.MaxSpecialityLength,
                        (current, value) => current with { Speciality = value },
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.WorkExperience:
                await SaveTextAsync(
                        chatId,
                        state,
                        message.Text,
                        Answers.MaxWorkExperienceLength,
                        (current, value) => current with { WorkExperience = value },
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.OtherLanguages:
                await SaveTextAsync(
                        chatId,
                        state,
                        message.Text,
                        Answers.MaxOtherLanguagesLength,
                        (current, value) => current with { OtherLanguages = value },
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.OfficeSkills:
                await SaveTextAsync(
                        chatId,
                        state,
                        message.Text,
                        Answers.MaxOfficeSkillsLength,
                        (current, value) => current with { OfficeSkills = value },
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.LastSalary:
                await SaveTextAsync(
                        chatId,
                        state,
                        message.Text,
                        Answers.MaxLastSalaryLength,
                        (current, value) => current with { LastSalary = value },
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.ExtraNote:
                await SaveTextAsync(
                        chatId,
                        state,
                        message.Text,
                        Answers.MaxExtraNoteLength,
                        (current, value) => current with { ExtraNote = value },
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case ConversationStep.Photo:
                await HandlePhotoAsync(chatId, state, message, cancellationToken).ConfigureAwait(false);
                break;

            // «Kim tavsiya qildi?» — majburiy: bo'sh javobda savol qayta beriladi
            case ConversationStep.ReferralDetail:
                await SaveTextAsync(
                        chatId,
                        state,
                        message.Text,
                        Answers.MaxReferralDetailLength,
                        (current, value) => current with { ReferralDetail = value },
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            default:
                /*
                  Tugma kutilyapti, nomzod esa matn yozdi. Javob qadamga
                  QARAB tanlanadi va bu farq muhim:

                  • til va vakansiya — bular hali anketa emas, nomzod
                    menyu oldida turibdi va unga menyu ko'rsatiladi;
                  • qolgan hamma qadam — anketaning O'RTASI. U yerda
                    «menyudan bo'lim tanlang» deyish yarim to'ldirilgan
                    anketadan chiqib ketishga undardi.
                */
                await _telegram
                    .SendMessageAsync(
                        chatId,
                        state.Step is ConversationStep.Language or ConversationStep.Vacancy
                            ? BotTexts.Restart(state.Language)
                            : BotTexts.UseButtons(state.Language),
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                break;
        }
    }

    private async Task HandleNameAsync(
        long chatId,
        ConversationState state,
        string? text,
        CancellationToken cancellationToken)
    {
        var name = NameParser.TryParse(text);

        if (name is null)
        {
            await _telegram
                .SendMessageAsync(
                    chatId,
                    BotTexts.NameNotUnderstood(state.Language),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        await AcceptAsync(chatId, state with { Name = name }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Tug'ilgan sana (4-qadam).</summary>
    /// <remarks>
    /// Sana <c>DateOnly</c> bo'lib saqlanadi va HECH QAYERDA
    /// siljitilmaydi (<see cref="BotClock"/> izohi): vaqt mintaqasi
    /// tug'ilgan kunga tegishli emas.
    /// </remarks>
    private async Task HandleBirthDateAsync(
        long chatId,
        ConversationState state,
        string? text,
        CancellationToken cancellationToken)
    {
        if (BirthDateParser.TryParse(text) is not { } birthDate)
        {
            await _telegram
                .SendMessageAsync(
                    chatId,
                    BotTexts.BirthDateNotUnderstood(state.Language),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        await AcceptAsync(chatId, state with { BirthDate = birthDate }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Yashash manzili (5-qadam).</summary>
    private async Task HandleAddressAsync(
        long chatId,
        ConversationState state,
        string? text,
        CancellationToken cancellationToken)
    {
        var address = text?.Trim();

        if (string.IsNullOrEmpty(address) || address.Length < MinAddressLength)
        {
            await _telegram
                .SendMessageAsync(
                    chatId,
                    BotTexts.AddressNotUnderstood(state.Language),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        // Uzunlik chegarasi bazadagi ustun bilan bir xil (500). Uzunroq
        // matn API'da xatoga aylanardi va nomzod sababini bilmasdi.
        if (address.Length > MaxAddressLength)
        {
            address = address[..MaxAddressLength];
        }

        await AcceptAsync(chatId, state with { Address = address }, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandlePhoneAsync(
        long chatId,
        ConversationState state,
        TelegramMessage message,
        CancellationToken cancellationToken)
    {
        if (message.Contact is not { } contact)
        {
            // Yagona istisno: Telegram raqami chet elniki — o'zbek raqamini
            // qo'lda yozish so'ralgan (`BotTexts.ForeignPhone` izohi).
            if (state.PhoneTypingAllowed)
            {
                if (UzbekPhone.Normalize(message.Text) is { } typed)
                {
                    await CheckAndAcceptPhoneAsync(chatId, state, typed, cancellationToken).ConfigureAwait(false);
                    return;
                }

                await _telegram
                    .SendMessageAsync(chatId, BotTexts.PhoneFormat(state.Language), cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                return;
            }

            // Raqam qo'lda yozilgan: qabul qilmaymiz. Terish xatosi
            // butun arizani qidirib bo'lmaydigan qilib qo'yardi va HR
            // nomzodga umuman qo'ng'iroq qila olmasdi.
            await _telegram
                .SendMessageAsync(
                    chatId,
                    BotTexts.PhoneButtonOnly(state.Language),
                    requestContact: true,
                    contactButtonText: BotTexts.PhoneButton(state.Language),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        // Telegram'da BOSHQA odamning kontaktini ham yuborib bo'ladi
        // (izohi `TelegramContact` da). Bunda ariza begona raqam bilan
        // yozilib, HR noto'g'ri odamga qo'ng'iroq qilardi.
        if (contact.UserId is null || contact.UserId != chatId)
        {
            await _telegram
                .SendMessageAsync(
                    chatId,
                    BotTexts.OwnPhoneOnly(state.Language),
                    requestContact: true,
                    contactButtonText: BotTexts.PhoneButton(state.Language),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        /*
          Chet el raqami (masalan +7) — server uni oxirida baribir rad
          etardi va nomzod 22 ta savoldan keyin «ariza yuborilmadi» ni
          olardi (prod jurnali, 2026-09-29). Endi shu yerning o'zida
          aytiladi va o'zbek raqamini qo'lda yozish so'raladi.
        */
        // The contact is confirmed as the sender's own: an online-form application under this
        // number now has a chat, so its invitation can reach them.
        await _bonvi.MatchTelegramAsync(chatId, null, contact.PhoneNumber, cancellationToken).ConfigureAwait(false);

        if (UzbekPhone.Normalize(contact.PhoneNumber) is not { } phone)
        {
            _conversations.Save(chatId, state with { PhoneTypingAllowed = true });

            await _telegram
                .SendMessageAsync(chatId, BotTexts.ForeignPhone(state.Language), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        await CheckAndAcceptPhoneAsync(chatId, state, phone, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Takroriy arizani tekshiradi va telefonni qabul qiladi.
    /// </summary>
    private async Task CheckAndAcceptPhoneAsync(
        long chatId,
        ConversationState state,
        string phone,
        CancellationToken cancellationToken)
    {
        /*
          ⭐ TAKRORIY ARIZA AYNAN SHU YERDA tekshiriladi — 6-qadamda,
          telefon raqami ma'lum bo'lgan zahoti.

          Tekshiruvni oxirga (22-qadamga) qoldirish eng shafqatsiz
          variant bo'lardi: nomzod o'n oltita savolga javob berib bo'lib,
          «siz allaqachon ariza bergansiz» degan xabarni olardi va
          hamma mehnati bekorga ketardi.
        */
        /*
          ⭐ Qarorni SERVER beradi, bot emas (izohi
          `BonviClient.CheckApplicationAsync` da). Ikkita to'siq bor —
          jarayondagi ariza va qayta ariza muddati — va ikkinchisining
          qoidasi sozlamadan o'zgaradi.

          F.I. va tug'ilgan sana ham ketadi (ular 3–4-qadamda olingan):
          odam raqamni VA Telegram hisobini almashtirib qaytib kelardi.

          `null` — javob umuman kelmadi. Unda O'TKAZAMIZ: server
          yiqilganida odamni yolg'on to'siq bilan qaytarishdan ko'ra
          anketani davom ettirgan yaxshiroq.
        */
        var check = await _bonvi
            .CheckApplicationAsync(phone, chatId, state.Name, state.BirthDate, cancellationToken)
            .ConfigureAwait(false);

        if (check is { Allowed: false })
        {
            _conversations.Forget(chatId);

            await SendWithMenuAsync(
                    chatId,
                    BlockedText(state, check),
                    state.Language,
                    cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        await AcceptAsync(
                chatId,
                state with { Phone = phone, PhoneTypingAllowed = false },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// To'silgan arizaga qaysi matn boradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Har bir sabab boshqacha xabar talab qiladi.</b> Jarayondagi
    /// arizada bosqich aytiladi («ko'rib chiqilmoqda»), muddatda esa
    /// qaytish SANASI — u aytilmasa nomzod qachon urinishini bilmay
    /// qolardi va bot bilan har kuni gaplashib turardi.
    /// </para>
    /// <para>
    /// <b>Tafsilot yetmasa — quruq rad javobi.</b> Server sanani
    /// yubormagan bo'lsa (nazariy holat yoki javob shakli
    /// o'zgargan), xabarni chizib bo'lmaydi. To'siqni jimgina ochib
    /// yuborish esa MUMKIN emas: server «yo'q» degan bo'lsa, javob
    /// «yo'q» bo'lib qoladi, faqat sababsiz.
    /// </para>
    /// </remarks>
    private static string BlockedText(ConversationState state, ApplicationCheck check) => check switch
    {
        /*
          Vakansiya nomi SERVERDAN olinadi: nomzod hozir vakansiya
          tanlamagan bo'lishi mumkin (ochiq ish o'rni yo'q), lekin eski
          arizasi baribir biror vakansiyada turibdi va aynan o'sha nom
          unga ma'noli.
        */
        { Reason: ApplicationBlockReason.Active, AppliedAt: { } appliedAt } =>
            BotTexts.AlreadyApplied(
                state.Language,
                check.VacancyTitle ?? state.VacancyTitle,
                appliedAt,
                check.StageName),

        { Reason: ApplicationBlockReason.Cooldown, AppliedAt: { } appliedAt, AvailableFrom: { } availableFrom } =>
            BotTexts.ReapplyLater(state.Language, appliedAt, availableFrom),

        _ => BotTexts.ApplicationNotAccepted(state.Language),
    };

    /// <summary>
    /// Erkin matnli javobni yozadi (8, 9, 12, 13, 14, 15-qadamlar).
    /// </summary>
    /// <remarks>
    /// Matn tozalanadi va chegaraga sig'diriladi (<see cref="Answers"/>).
    /// Bo'sh javob — javob EMAS: unda savol qaytadan beriladi, chunki
    /// «o'tkazib yuborish» uchun alohida tugma bor va nomzod uni
    /// ko'rmagan bo'lishi mumkin.
    /// </remarks>
    private async Task SaveTextAsync(
        long chatId,
        ConversationState state,
        string? text,
        int maxLength,
        Func<ConversationState, string, ConversationState> apply,
        CancellationToken cancellationToken)
    {
        if (Answers.Clean(text, maxLength) is not { } value)
        {
            var settings = await SettingsAsync(cancellationToken).ConfigureAwait(false);

            await AskAsync(chatId, state, [], settings, cancellationToken).ConfigureAwait(false);
            return;
        }

        await AcceptAsync(chatId, apply(state, value), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Surat (18-qadam) — bu yerda faqat BELGI olinadi.
    /// </summary>
    /// <remarks>
    /// Baytlar hozir yuklab olinmaydi: ariza yuborilmasa ular bekorga
    /// olingan bo'lardi va yarim qolgan har bir suhbat xotirada bir
    /// necha yuz kilobayt ushlab turardi. Rasm ariza YARATILGANDAN
    /// keyin olinadi (<see cref="TryUploadPhotoAsync"/>).
    /// </remarks>
    private async Task HandlePhotoAsync(
        long chatId,
        ConversationState state,
        TelegramMessage message,
        CancellationToken cancellationToken)
    {
        if (TelegramPhotos.BestFileId(message.Photo, MaxPhotoBytes) is not { } fileId)
        {
            await SendAsync(
                    chatId,
                    BotTexts.PhotoNotUnderstood(state.Language),
                    SkipButtons(state.Language),
                    cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        await AcceptAsync(chatId, state with { PhotoFileId = fileId }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Tugallangan anketani yuboradi (22-qadam).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ariza BIR MARTA, to'liq holda yoziladi.</b> Har bir qadamda
    /// serverga tegib borish yarim arizalarni HR ro'yxatida qoldirardi:
    /// nomzodlarning bir qismi anketani albatta tashlab ketadi.
    /// </para>
    /// <para>
    /// <b>Xatoda suhbat SAQLANADI</b> — nomzod «Yuborish»ni qayta
    /// bosishi kifoya, o'n oltita savolga qayta javob berish shart
    /// emas.
    /// </para>
    /// <para>
    /// ⚠️ <b>Surat arizadan KEYIN va u arizaga ta'sir qilmaydi</b>
    /// (<see cref="TryUploadPhotoAsync"/>).
    /// </para>
    /// </remarks>
    private async Task SubmitAsync(long chatId, ConversationState state, CancellationToken cancellationToken)
    {
        if (state.Name is not { } name
            || state.Phone is not { } phone
            || state.Education is not { } education
            || state.MaritalStatus is not { } marital
            || state.RussianLevel is not { } russianLevel
            || state.ExpectedSalary is not { } expectedSalary)
        {
            /*
              Majburiy javob yo'q. Bunga faqat holat buzilganda kelib
              bo'ladi (masalan eski tugma bosilgan). Serverga chala
              ariza yuborishdan ko'ra boshidan boshlash to'g'ri:
              yarim ariza HR ro'yxatida tuzatib bo'lmaydigan yozuv
              bo'lib qolardi.
            */
            await _telegram
                .SendMessageAsync(chatId, BotTexts.Restart(state.Language), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        /*
          «Qayerdan bilgan» yo'q — anketaning QOLGANI joyida, shu sababli boshidan emas,
          faqat SHU savol qayta so'raladi. Bunga ikki yo'l bilan kelinadi: deploydan oldin
          boshlangan suhbat (Redis'dagi nusxada eski sanoq qiymati turibdi va u endi
          tanilmaydi — `ConversationState.ReferralChannelId` izohi) va buzilgan holat
          (aniqlik so'ralgan, lekin yo'q). Javobdan keyin oferta qayta so'ralmaydi
          (`Applies`), nomzod darhol yana tasdiq qadamiga qaytadi.
        */
        if (state.ReferralChannelId is not { } referralChannelId
            || (state.ReferralAsksDetail && string.IsNullOrWhiteSpace(state.ReferralDetail)))
        {
            await ContinueAsync(chatId, WithoutReferral(state), ConversationStep.ReferralSource, cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        var answers = new QuestionnaireAnswers(
            education,
            marital,
            russianLevel,
            expectedSalary,

            /*
              ⭐ Oferta qadami O'TKAZIB YUBORILGAN bo'lsa (havola
              sozlanmagan) qiymat `null` bo'lib ketadi va maydon so'rov
              tanasiga umuman tushmaydi (`BonviClient.RequestJson`).

              Zaxira vaqt QO'YILMAYDI. Ilgari bu yerda `?? BotClock.Now`
              turardi — nomzod hech qanday oferta ko'rmagan holda ham
              bazaga «rozilik shu lahzada berildi» deb yozilardi, ya'ni
              huquqiy fakt soxtalashtirilardi. Endi server `null` ni
              qabul qiladi va u «rozilik SO'RALMAGAN» degani.
            */
            state.OfferAcceptedAt,
            state.WorkExperience,
            state.OtherLanguages,
            state.OfficeSkills,
            state.LastSalary,
            state.ExtraNote,
            state.HasDriverLicense == true,
            state.DriverLicenseCategory);

        var registration = new CandidateRegistration(
            state.VacancyId,
            name.LastName,
            name.FirstName,
            name.MiddleName,
            state.BirthDate,
            phone,
            state.Address,
            state.Speciality,
            chatId,
            BotLanguages.Code(state.Language),
            answers,
            referralChannelId,
            state.ReferralAsksDetail ? state.ReferralDetail : null);

        /*
          ⭐ TAKRORIY YUBORISHDAN HIMOYA.

          Yuborish ilgari boshlangan (belgi bor) — demak bot o'sha paytda
          to'xtatilgan va Telegram «Yuborish» ni qayta yetkazgan, yoki
          nomzod uni qayta bosgan. Server nomzodni yozib ulgurgan bo'lishi
          mumkin, u esa takrorni o'zi tekshirmaydi. Shu sababli avval
          so'raladi: bu telefonning JARAYONDAGI arizasi bormi? Telefon
          qadamida bu tekshiruv o'tgan edi, ya'ni endi «bor» degan javob —
          shu suhbatning o'z arizasi.
        */
        if (state.SubmitStartedAt is not null)
        {
            var check = await _bonvi.CheckApplicationAsync(phone, chatId, state.Name, state.BirthDate, cancellationToken).ConfigureAwait(false);

            if (check is { Allowed: false, Reason: ApplicationBlockReason.Active })
            {
                _conversations.Forget(chatId);

                await SendWithMenuAsync(
                        chatId,
                        state.VacancyId is null
                            ? BotTexts.AcceptedToGeneralPool(state.Language)
                            : BotTexts.Accepted(state.Language, state.VacancyTitle),
                        state.Language,
                        cancellationToken)
                    .ConfigureAwait(false);

                return;
            }
        }
        else
        {
            state = state with { SubmitStartedAt = BotClock.Now };
            _conversations.Save(chatId, state);
        }

        var result = await RegisterWithRetryAsync(chatId, state.Language, registration, cancellationToken)
            .ConfigureAwait(false);

        /*
          Tanlangan vakansiya anketa to'ldirilayotganda YOPILGAN (yoki
          o'chirilgan) — ariza «Umumiy anketa»ga qabul qilinadi.

          Suhbat endi bir sutka yashaydi va HR vakansiyani bir bosishda
          yopadi, ya'ni bu holat kutilgan. Ilgari nomzod 22 ta savoldan
          keyin «ariza yuborilmadi» ni olardi va mehnati bekorga ketardi;
          HR esa bu odamni umuman ko'rmasdi.
        */
        var closedVacancy = false;

        if (result.CandidateId is null
            && state.VacancyId is not null
            && result.ErrorCode is VacancyNotAcceptingCode or VacancyNotFoundCode)
        {
            closedVacancy = true;

            result = await RegisterWithRetryAsync(
                    chatId,
                    state.Language,
                    registration with { VacancyId = null },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (result.CandidateId is null && result.ErrorCode is { } code && ReferralErrorCodes.Contains(code))
        {
            /*
              Kanal anketa to'ldirilayotganda faolsizlantirilgan (yoki aniqlik yetishmadi) —
              server arizani yozmadi. Faqat shu savol qayta so'raladi, qolgan javoblar joyida.
            */
            await ContinueAsync(chatId, WithoutReferral(state), ConversationStep.ReferralSource, cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        if (result.CandidateId is null && result.ErrorCode == GeneralPoolUnavailableCode)
        {
            /*
              Qabul YOPIQ: nomzod to'ldirayotgan paytda vakansiya ham,
              «Umumiy anketa» ham yopilgan (egasining qarori, 2026-10-05 —
              qaysi vakansiya yopiq bo'lsa, unga anketa topshirib bo'lmaydi).
              Bu vaqtinchalik nosozlik EMAS: «keyinroq qayta urinib ko'ring»
              degan umumiy xabar nomzodni bekorga qayta bosishga undardi.
              Suhbat unutiladi va sabab aniq aytiladi.
            */
            _conversations.Forget(chatId);

            await SendWithMenuAsync(chatId, BotTexts.ApplicationsClosed(state.Language), state.Language, cancellationToken)
                .ConfigureAwait(false);

            LogApplicationsClosed();
            return;
        }

        if (result.CandidateId is not { } created)
        {
            // Suhbat SAQLANADI: nomzod «Yuborish» ni keyinroq qayta bosa
            // oladi — holat Redis'da, ya'ni bot qayta ishga tushsa ham.
            await _telegram
                .SendMessageAsync(chatId, BotTexts.Failed(state.Language), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        // Suhbat DARHOL unutiladi — suratdan OLDIN: surat bir-ikki soniya
        // oladi va shu orada bot to'xtatilsa, qayta ishga tushgan bot
        // «tasdiq» qadamidagi holatni topib, arizani ikkinchi marta
        // yuborardi. Surat belgisi `state` da qoladi.
        _conversations.Forget(chatId);

        await TryUploadPhotoAsync(created, state.PhotoFileId, cancellationToken).ConfigureAwait(false);

        /*
          Menyu bilan: telefon so'ralganda Telegram uni kontakt tugmasi
          bilan almashtirgan edi. Menyusiz nomzod ariza tugagach bo'sh
          ekran oldida qolardi va ikkinchi vakansiyaga ariza berish
          uchun `/start` yozishi kerak bo'lardi.
        */
        var accepted = closedVacancy
            ? BotTexts.AcceptedVacancyClosed(state.Language, state.VacancyTitle)
            : state.VacancyId is null
                ? BotTexts.AcceptedToGeneralPool(state.Language)
                : BotTexts.Accepted(state.Language, state.VacancyTitle);

        await SendWithMenuAsync(chatId, accepted, state.Language, cancellationToken).ConfigureAwait(false);

        LogApplication(state.VacancyId is null || closedVacancy ? GeneralPoolLabel : state.VacancyTitle);
    }

    /// <summary>«Qayerdan bilgan» javobi tozalangan holat — savol qayta so'raladi.</summary>
    private static ConversationState WithoutReferral(ConversationState state) => state with
    {
        ReferralChannelId = null,
        ReferralChannelName = null,
        ReferralAsksDetail = false,
        ReferralDetail = null,
    };

    /// <summary>
    /// Arizani yuboradi — server VAQTINCHA javob bermasa, kutib qayta uradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega.</b> Deploy paytida API bir necha soniyadan bir daqiqagacha
    /// javob bermaydi. Egasining talabi (2026-09-29): serverning qayta
    /// ishga tushishi anketa topshirishga ta'sir qilmasligi kerak.
    /// </para>
    /// <para>
    /// Faqat so'rov serverga YETMAGANI aniq bo'lgan xatolar qayta
    /// yuboriladi (<see cref="CandidateRegistrationResult.Transient"/>) —
    /// aks holda bitta odam ikki marta yozilib qolishi mumkin edi. Birinchi
    /// muvaffaqiyatsiz urinishdan keyin nomzodga «yuborilmoqda» deyiladi.
    /// </para>
    /// </remarks>
    private async Task<CandidateRegistrationResult> RegisterWithRetryAsync(
        long chatId,
        BotLanguage language,
        CandidateRegistration registration,
        CancellationToken cancellationToken)
    {
        var result = await _bonvi.RegisterCandidateAsync(registration, cancellationToken).ConfigureAwait(false);

        /*
          API yaqinda to'liq kutishdan keyin ham javob bermagan — yana
          kutilmaydi. Bot yangiliklarni birma-bir ishlaydi: har bir
          «Yuborish» bosilishi boshqa barcha nomzodlarni yana bir daqiqaga
          to'xtatib qo'yardi (masalan API qayta-qayta yiqilayotganda).
        */
        var retries = DateTimeOffset.UtcNow < _apiDownUntil ? 0 : SubmitRetryDelays.Count;

        for (var attempt = 0; result.Transient && attempt < retries; attempt++)
        {
            if (attempt == 0)
            {
                await _telegram
                    .SendMessageAsync(chatId, BotTexts.Submitting(language), cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            await Task.Delay(SubmitRetryDelays[attempt], cancellationToken).ConfigureAwait(false);

            result = await _bonvi.RegisterCandidateAsync(registration, cancellationToken).ConfigureAwait(false);
        }

        _apiDownUntil = result.Transient ? DateTimeOffset.UtcNow + ApiDownPause : DateTimeOffset.MinValue;

        return result;
    }

    /// <summary>
    /// Suratni Telegram'dan olib, nomzod kartasiga qo'yadi.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Xato YUTILADI va bu ataylab.</b> Ariza allaqachon
    /// yaratilgan: rasm yetib bormasa ham nomzod voronkada turadi va
    /// HR unga qo'ng'iroq qiladi. Bu yerda istisno tashlash yoki
    /// nomzodga «xatolik» deb yozish uni arizasi yo'qolgan deb
    /// o'ylashga majburlardi — holbuki yo'qolgani atigi surat.
    /// Xato jurnalga tushadi va HR rasmni keyin qo'lda qo'sha oladi.
    /// </remarks>
    private async Task TryUploadPhotoAsync(Guid candidateId, string? fileId, CancellationToken cancellationToken)
    {
        if (fileId is null)
        {
            // Nomzod suratni o'tkazib yuborgan — bu odatiy hol.
            return;
        }

        var bytes = await _telegram.DownloadFileAsync(fileId, cancellationToken).ConfigureAwait(false);

        if (bytes is null || bytes.Length == 0)
        {
            LogPhotoFailed(candidateId);
            return;
        }

        if (!await _bonvi.UploadCandidatePhotoAsync(candidateId, bytes, cancellationToken).ConfigureAwait(false))
        {
            LogPhotoFailed(candidateId);
        }
    }

    /// <summary>
    /// Ishchi token borligini va DOLZARBLIGINI ta'minlaydi.
    /// </summary>
    /// <returns>Bot xabar qabul qilishga tayyormi.</returns>
    /// <remarks>
    /// <para>
    /// Uch holat: token yo'q, token o'zgarmagan, token yangi. Faqat
    /// uchinchisida Telegram bilan qo'l berishiladi
    /// (<c>deleteWebhook</c> + <c>getMe</c>) — ya'ni odatiy aylanishda
    /// bu metod hech qanday tarmoq so'rovi qilmaydi.
    /// </para>
    /// <para>
    /// <b>Xato holatida ham jurnal tinch.</b> Halqa 15 soniyada
    /// aylanadi, shu sababli har bir shikoyat token bo'yicha BIR MARTA
    /// yoziladi (<see cref="_complainedAbout"/>).
    /// </para>
    /// </remarks>
    private async Task<bool> EnsureTokenAsync(CancellationToken cancellationToken)
    {
        // Bot ulanmagan bo'lsa kesh chetlab o'tiladi: odam ekranda
        // tokenni endigina kiritgan bo'lishi mumkin va uni bir daqiqa
        // kutishga majburlashning sababi yo'q.
        var desired = await DesiredTokenAsync(force: _activeToken is null, cancellationToken)
            .ConfigureAwait(false);

        if (desired is null)
        {
            if (_complainedAbout != string.Empty)
            {
                LogTokenMissing();
                _complainedAbout = string.Empty;
            }

            _activeToken = null;
            return false;
        }

        if (desired == _activeToken)
        {
            return true;
        }

        if (_activeToken is not null)
        {
            LogTokenChanged();
        }

        _token.Value = desired;

        // Webhook faol bo'lsa `getUpdates` umuman ishlamaydi (izohi
        // `TelegramClient.DeleteWebhookAsync` da). Har YANGI tokenda
        // takrorlanadi: webhook bot tomonida yozilgan bo'ladi va yangi
        // token boshqa botga tegishli bo'lishi mumkin.
        await _telegram.DeleteWebhookAsync(cancellationToken).ConfigureAwait(false);

        var username = await _telegram.GetMeAsync(cancellationToken).ConfigureAwait(false);

        if (username is null)
        {
            if (_complainedAbout != desired)
            {
                LogBadToken();
                _complainedAbout = desired;
            }

            _activeToken = null;
            return false;
        }

        /*
          Yangi token — yangi hisob bo'lishi mumkin (sinovdan ishchi
          botga o'tish). Eski hisobning `offset` i yangisida ma'nosiz:
          u boshqa botning yangilik raqamlari edi. Nolga qaytarish
          xavfsiz — Telegram allaqachon TASDIQLANGAN yangiliklarni
          qaytarmaydi, ya'ni eski xabarlar ikkinchi marta kelmaydi.
        */
        _offset = 0;
        _activeToken = desired;
        _complainedAbout = null;

        LogStarted(username);
        _usernameToReport = username;
        return true;
    }

    /// <summary>
    /// Bot ISHLATISHI kerak bo'lgan token: tizimdagisi, bo'lmasa <c>.env</c> dagisi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Ustuvorlik: EKRAN &gt; <c>.env</c>.</b> Ekrandagi qiymat
    /// ataylab, hozir va aniq bir odam tomonidan qo'yilgan; <c>.env</c>
    /// esa o'rnatishda bir marta yozilgan va odatda unutiladi. Teskarisi
    /// bo'lsa, ekranda tokenni almashtirgan odam hech qanday natija
    /// ko'rmasdi va sababini topa olmasdi.
    /// </para>
    /// <para>
    /// <b>So'rov bajarilmasa ESKI qiymat qoladi</b> (matn sozlamasidagi
    /// bilan bir xil qoida): API ga borib kelolmaslik ishlab turgan
    /// botni to'xtatishga sabab emas.
    /// </para>
    /// </remarks>
    private async Task<string?> DesiredTokenAsync(bool force, CancellationToken cancellationToken)
    {
        if (force || !_tokenEverRead || DateTimeOffset.UtcNow - _tokenReadAt >= TokenRefresh)
        {
            var fresh = await _bonvi.GetBotTokenAsync(cancellationToken).ConfigureAwait(false);

            if (fresh is not null)
            {
                _settingsToken = string.IsNullOrWhiteSpace(fresh.Token) ? null : fresh.Token;
                _tokenEverRead = true;
                _tokenReadAt = DateTimeOffset.UtcNow;
            }
        }

        if (_settingsToken is not null)
        {
            return _settingsToken;
        }

        return string.IsNullOrWhiteSpace(_options.Token) ? null : _options.Token;
    }

    /// <summary>
    /// Sozlamani tizimdan oladi (besh daqiqalik kesh bilan).
    /// </summary>
    /// <remarks>
    /// So'rov bajarilmasa ESKI qiymat qoladi: sozlama uchun API ga
    /// borib kelolmaslik nomzodning arizasini to'xtatishga sabab emas.
    /// </remarks>
    private async Task<BotSettings?> SettingsAsync(CancellationToken cancellationToken)
    {
        if (_settings is not null && DateTimeOffset.UtcNow - _settingsReadAt < TimeSpan.FromMinutes(5))
        {
            return _settings;
        }

        var fresh = await _bonvi.GetBotSettingsAsync(cancellationToken).ConfigureAwait(false);

        if (fresh is not null)
        {
            _settings = fresh;
            _settingsReadAt = DateTimeOffset.UtcNow;

            // «Suhbat muddati» — ekrandagi sozlama ishga tushishdagi
            // qiymatdan ustun (`ConversationStore.Lifetime` izohi).
            // Nol yoki manfiy qiymat — buzilgan javob, u qabul qilinmaydi.
            if (fresh.ConversationTimeoutMinutes > 0)
            {
                _conversations.Lifetime = TimeSpan.FromMinutes(Math.Min(fresh.ConversationTimeoutMinutes, 1440));
            }
        }

        return _settings;
    }

    /// <summary>
    /// Tanlangan tildagi kompaniya matni — sozlanmagan bo'lsa <c>null</c>.
    /// </summary>
    private static string? About(BotSettings? settings, BotLanguage language)
        => Section(settings?.AboutCyrillic, settings?.AboutLatin, settings?.AboutRussian, language);

    /// <summary>Tanlangan tildagi kontakt matni — sozlanmagan bo'lsa <c>null</c>.</summary>
    private static string? Contacts(BotSettings? settings, BotLanguage language)
        => Section(settings?.ContactsCyrillic, settings?.ContactsLatin, settings?.ContactsRussian, language);

    /// <summary>
    /// Uch tildan keraklisini tanlaydi.
    /// </summary>
    /// <remarks>
    /// Tanlangan til bo'sh bo'lsa KIRILLGA tushadi: bitta matn yozib
    /// qo'yish uchtasini majburlashdan foydaliroq. Kirill tayanch til
    /// sifatida olingan, chunki tizimdagi ma'lumotning deyarli
    /// hammasi shu yozuvda.
    /// </remarks>
    private static string? Section(
        string? cyrillic,
        string? latin,
        string? russian,
        BotLanguage language)
    {
        var chosen = language switch
        {
            BotLanguage.Latin => latin,
            BotLanguage.Russian => russian,
            _ => cyrillic,
        };

        var text = string.IsNullOrWhiteSpace(chosen) ? cyrillic : chosen;

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        /*
          `.env` va docker-compose yangi QATORNI uzata olmaydi: ular
          qiymatni bir satrda saqlaydi va `\n` ikkita BELGI bo'lib keladi
          (teskari chiziq va «n»). Shu sababli u shu yerda haqiqiy qator
          ko'chishiga aylantiriladi — aks holda kompaniya matni Telegram'da
          bitta uzun satr bo'lib chiqardi.
        */
        return text
            .Replace("\\r\\n", "\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal);
    }

    /// <summary>Bosh menyu: ariza topshirish yoki kompaniya haqida.</summary>
    /// <summary>Til tanlash tugmalarini ko'rsatadi.</summary>
    private async Task AskLanguageAsync(long chatId, CancellationToken cancellationToken)
        => await _telegram
            .SendMessageAsync(
                chatId,
                BotTexts.ChooseLanguage,
                [
                    [new InlineButton(BotTexts.LanguageCyrillic, LanguageCyrillicData)],
                    [new InlineButton(BotTexts.LanguageLatin, LanguageLatinData)],
                    [new InlineButton(BotTexts.LanguageRussian, LanguageRussianData)],
                ],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Xabarni DOIMIY MENYU bilan birga yuboradi.
    /// </summary>
    /// <remarks>
    /// Menyu har safar qayta yuboriladi va bu ortiqcha emas: telefon
    /// so'ralganda Telegram menyuni kontakt tugmasi bilan ALMASHTIRADI
    /// (bir chatda bir vaqtda bitta klaviatura bo'ladi). Ariza
    /// tugagach menyu o'zi qaytmasa, nomzod bo'sh ekran oldida
    /// qolardi.
    /// </remarks>
    private async Task SendWithMenuAsync(
        long chatId,
        string text,
        BotLanguage language,
        CancellationToken cancellationToken)
    {
        var settings = await SettingsAsync(cancellationToken).ConfigureAwait(false);

        await _telegram
            .SendMessageAsync(
                chatId,
                text,
                menu: await MenuLabelsAsync(settings, language, cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Sozlama va «Umumiy anketa» holatiga qarab menyu tugmalarini yig'adi.</summary>
    private async Task<List<string>> MenuLabelsAsync(
        BotSettings? settings,
        BotLanguage language,
        CancellationToken cancellationToken)
        => BotMenu.Labels(
            language,
            hasAbout: About(settings, language) is not null,
            hasContacts: Contacts(settings, language) is not null,
            hasQuestionnaire: await GeneralPoolMenuStateAsync(cancellationToken).ConfigureAwait(false) != false);

    /// <summary>
    /// «Umumiy anketa» ochiqmi — MENYU uchun, qisqa kesh bilan.
    /// </summary>
    /// <remarks>
    /// Menyu deyarli har xabar bilan chiziladi, ya'ni har safar API'ni so'rash
    /// ortiqcha. 30 soniyalik eskirish xavfsiz: HR anketani yopgandan keyin
    /// tugmani ko'rgan nomzod bosganda JONLI tekshiruv ishlaydi
    /// (<see cref="MenuAction.Questionnaire"/>) va bot holatni aytib, menyuni
    /// yangilaydi. <c>null</c> — noma'lum (tugma ko'rsatiladi).
    /// </remarks>
    private async Task<bool?> GeneralPoolMenuStateAsync(CancellationToken cancellationToken)
    {
        if (_generalPoolMenuState is { } cached && BotClock.Now - cached.At < GeneralPoolMenuCacheLifetime)
        {
            return cached.Open;
        }

        var open = await _bonvi.IsGeneralPoolOpenAsync(cancellationToken).ConfigureAwait(false);

        _generalPoolMenuState = new GeneralPoolMenuState(open, BotClock.Now);
        return open;
    }

    private static readonly TimeSpan GeneralPoolMenuCacheLifetime = TimeSpan.FromSeconds(30);

    private sealed record GeneralPoolMenuState(bool? Open, DateTimeOffset At);

    // Havola turi — almashtirish atomar (yarim yozilgan qiymat o'qilmaydi).
    private volatile GeneralPoolMenuState? _generalPoolMenuState;

    private async Task HandleMenuAsync(
        long chatId,
        ConversationState? state,
        MenuAction action,
        CancellationToken cancellationToken)
    {
        var language = state?.Language ?? BotLanguage.Cyrillic;

        if (action == MenuAction.Language)
        {
            await AskLanguageAsync(chatId, cancellationToken).ConfigureAwait(false);
            return;
        }

        /*
          Menyudan kelgan har qanday amal yarim qolgan ANKETANI bekor
          qiladi: nomzod «Kontaktlar» ni ochib, keyin biror javob
          yuborsa, u eski anketaga yozilib ketardi. Til bundan mustasno
          (yuqorida) — u anketaning davomi.

          ⚠️ Javoblar ham SHU YERDA yo'qoladi: yangi holat bo'sh
          yaratiladi. Bu ataylab — «Kontaktlar» ni ko'rgan nomzod
          anketaga qaytganda uning yarmi eski javoblar bilan to'lgan
          bo'lsa, u nima yozganini eslay olmasdi.
        */
        var fresh = new ConversationState { Step = ConversationStep.Vacancy, Language = language };

        _conversations.Save(chatId, fresh);

        switch (action)
        {
            case MenuAction.Vacancies:
                /*
                  ⭐ Bu amal ANKETANI BOSHLAMAYDI — faqat ochiq
                  o'rinlarni ko'rsatadi. Ariza nomzod ro'yxatdan birini
                  tanlagandan keyin, o'sha xabar ostidagi tugma bilan
                  boshlanadi.

                  Ochiq o'rin bo'lmasa ham anketa boshlanmaydi: nomzodga
                  holat aytiladi va u «Anketa to'ldirish» ga
                  yo'naltiriladi. Ilgari bu yerda anketa jimgina
                  boshlanib ketardi va «vakansiyalarni ko'rmoqchi
                  edim» degan odam o'zini yigirmata savol oldida
                  ko'rardi.
                */
                await ShowVacanciesAsync(chatId, fresh, cancellationToken).ConfigureAwait(false);
                break;

            case MenuAction.Questionnaire:
                /*
                  ⭐ Vakansiya qadami UMUMAN o'tkazib yuboriladi —
                  ochiq ish o'rni bor-yo'qligidan qat'i nazar. Ariza
                  vakansiyasiz ketadi va server uni «Umumiy anketa»ga
                  qo'yadi.

                  Farqi `Apply` dan aynan shu: u vakansiya BO'LSA
                  tanlashni so'raydi, bu esa hech qachon so'ramaydi.
                  Savollarning qolgani ikkalasida bir xil.

                  «Umumiy anketa» YOPIQ bo'lsa anketa BOSHLANMAYDI
                  (egasining qarori, 2026-10-05): nomzodga darhol aytiladi
                  va ochiq vakansiyalar bo'lsa ular ko'rsatiladi. Javob
                  noma'lum bo'lsa (server javob bermadi) anketa davom
                  etadi — qaror baribir serverda.
                */
                if (await _bonvi.IsGeneralPoolOpenAsync(cancellationToken).ConfigureAwait(false) == false)
                {
                    await QuestionnaireClosedAsync(chatId, fresh, cancellationToken).ConfigureAwait(false);
                    break;
                }

                await ContinueAsync(chatId, fresh, ConversationStep.Name, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case MenuAction.About:
                await SendAboutAsync(chatId, language, cancellationToken).ConfigureAwait(false);
                break;

            case MenuAction.Contacts:
                await SendSectionAsync(chatId, language, Contacts, cancellationToken).ConfigureAwait(false);
                await SendLocationAsync(chatId, cancellationToken).ConfigureAwait(false);
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// «Kompaniya haqida» — logotip, matn va ijtimoiy tarmoq tugmalari.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Uchtasi BITTA xabarda.</b> Rasm, matn va tugmalar alohida
    /// yuborilsa, ular suhbatda uchta yozuv bo'lib turardi va tugmalar
    /// matndan uzilib qolardi. Telegram esa rasmga izoh (caption) va
    /// tugmalar qo'shishga ruxsat beradi — natijada bitta, yaxlit
    /// karta chiqadi.
    /// </para>
    /// <para>
    /// ⚠️ <b>Izoh uzunligi 1024 belgi bilan cheklangan</b>, matnli
    /// xabarniki esa 4096. Uzun matn rasm bilan birga ketolmaydi va
    /// bunday holatda Telegram BUTUN xabarni rad etardi — shu sababli
    /// uzun matn alohida yuboriladi va rasm izohsiz qoladi.
    /// </para>
    /// <para>
    /// <b>Logotip bir marta yuklanadi.</b> Birinchi yuborishda baytlar
    /// ketadi, Telegram <c>file_id</c> qaytaradi va keyingi
    /// yuborishlar shu belgi bilan bo'ladi (<see cref="_logoFileId"/>).
    /// Har safar bayt uzatish har bir nomzod uchun ikkita ortiqcha
    /// so'rov degani bo'lardi.
    /// </para>
    /// </remarks>
    private async Task SendAboutAsync(long chatId, BotLanguage language, CancellationToken cancellationToken)
    {
        var settings = await SettingsAsync(cancellationToken).ConfigureAwait(false);
        var text = About(settings, language) ?? BotTexts.Restart(language);
        var menu = await MenuLabelsAsync(settings, language, cancellationToken).ConfigureAwait(false);
        var social = SocialButtons(settings, language);

        var caption = text.Length <= MaxCaptionLength ? text : null;

        var sent = settings?.HasLogo == true
            && await SendLogoAsync(chatId, caption, social, cancellationToken).ConfigureAwait(false);

        if (!sent)
        {
            // Logotip yo'q yoki yuborilmadi — matn baribir yetib borishi
            // kerak. Rasm bezak, xabar esa mazmun.
            await _telegram
                .SendMessageAsync(chatId, text, social, menu: menu, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        if (caption is null)
        {
            // Matn izohga sig'madi — alohida xabar bo'lib ketadi.
            await _telegram
                .SendMessageAsync(chatId, text, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        /*
          Doimiy menyu ALOHIDA yuboriladi: rasmli xabar uni ko'tara
          olmaydi (`reply_markup` da bir vaqtda yo bir turdagi tugma,
          yo boshqasi bo'ladi). Matn qisqa — u faqat menyuni qaytarish
          uchun, ya'ni suhbatda ortiqcha shovqin qilmaydi.
        */
        await SendWithMenuAsync(chatId, BotTexts.MenuPrompt(language), language, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Logotipni yuboradi — belgisi bo'lsa u bilan, bo'lmasa yuklab.
    /// </summary>
    /// <returns>Rasm yetib bordimi.</returns>
    private async Task<bool> SendLogoAsync(
        long chatId,
        string? caption,
        IReadOnlyList<IReadOnlyList<InlineButton>>? buttons,
        CancellationToken cancellationToken)
    {
        if (_logoFileId is { } fileId
            && await _telegram.SendPhotoByIdAsync(chatId, fileId, caption, buttons, cancellationToken)
                .ConfigureAwait(false))
        {
            return true;
        }

        /*
          Belgi ishlamadi yoki hali yo'q. Sabablari: bot birinchi marta
          ishga tushdi, HR logotipni almashtirdi yoki token o'zgardi
          (belgi tokenga bog'langan). Uchalasida ham javob bitta —
          qaytadan yuklash.
        */
        _logoFileId = null;

        var bytes = await _bonvi.GetBotLogoAsync(cancellationToken).ConfigureAwait(false);

        if (bytes is null || bytes.Length == 0)
        {
            return false;
        }

        using var content = new MemoryStream(bytes, writable: false);

        _logoFileId = await _telegram
            .UploadPhotoAsync(chatId, content, caption, buttons, cancellationToken)
            .ConfigureAwait(false);

        return _logoFileId is not null;
    }

    /// <summary>Sozlamadagi havolalardan ijtimoiy tarmoq tugmalari.</summary>
    private static IReadOnlyList<IReadOnlyList<InlineButton>>? SocialButtons(
        BotSettings? settings,
        BotLanguage language)
        => settings is null
            ? null
            : SocialLinks.Buttons(
                language,
                settings.Instagram,
                settings.Telegram,
                settings.Facebook,
                settings.Website);

    /// <summary>
    /// Sozlamadagi matnli bo'limni ko'rsatadi.
    /// </summary>
    /// <remarks>
    /// Bo'lim bo'sh bo'lsa tugma umuman chizilmagan bo'lardi
    /// (<see cref="MenuLabelsAsync"/>), lekin HR matnni bot ochiq turganda
    /// o'chirishi mumkin va eski tugma nomzod ekranida qolib ketadi.
    /// Shunda bo'sh xabar o'rniga menyu qayta ko'rsatiladi.
    /// </remarks>
    private async Task SendSectionAsync(
        long chatId,
        BotLanguage language,
        Func<BotSettings?, BotLanguage, string?> read,
        CancellationToken cancellationToken)
    {
        var settings = await SettingsAsync(cancellationToken).ConfigureAwait(false);
        var text = read(settings, language);

        await _telegram
            .SendMessageAsync(
                chatId,
                text ?? BotTexts.Restart(language),
                menu: await MenuLabelsAsync(settings, language, cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Bosh ofisning xaritadagi nuqtasi — sozlangan bo'lsa.</summary>
    private async Task SendLocationAsync(long chatId, CancellationToken cancellationToken)
    {
        var settings = await SettingsAsync(cancellationToken).ConfigureAwait(false);

        if (MapPoint.TryParse(settings?.Location) is not { } point)
        {
            return;
        }

        await _telegram
            .SendLocationAsync(chatId, point.Latitude, point.Longitude, cancellationToken)
            .ConfigureAwait(false);
    }

}
