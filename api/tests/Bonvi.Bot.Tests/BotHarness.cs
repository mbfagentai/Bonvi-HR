using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Bonvi.Bot.Bonvi;
using Bonvi.Bot.Conversation;
using Bonvi.Bot.Telegram;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bonvi.Bot.Tests;

/// <summary>Bot yuborgan bitta xabar.</summary>
/// <param name="Text">Xabar matni.</param>
/// <param name="Buttons">Xabar ostidagi tugmalarning ma'lumoti.</param>
/// <param name="RequestsContact">«Raqamni yuborish» tugmasi chizildimi.</param>
/// <param name="Menu">Pastki menyu (reply keyboard) tugmalarining matni; menyusiz xabarda bo'sh.</param>
public sealed record SentMessage(
    string Text,
    IReadOnlyList<string> Buttons,
    bool RequestsContact,
    IReadOnlyList<string>? Menu = null);

/// <summary>
/// Botni TARMOQSIZ ishga tushiradigan sinov stendi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega shunday, «sof mantiq»ni ajratib olish emas.</b> Anketaning
/// qiymati aynan KETMA-KETLIKDA: qaysi savoldan keyin qaysi biri
/// keladi, qaysi biri o'tkazib yuboriladi va menyu tugmasi qadam
/// ishlovidan oldin tekshiriladimi. Bularning hammasi
/// <c>BotWorker</c> ning ichida va uni ikkinchi marta, testda yozib
/// chiqish «test kodni emas, o'zini tekshiradi» degan holatga olib
/// kelardi.
/// </para>
/// <para>
/// Shu sababli bot HAQIQIY <c>HttpClient</c> lar bilan yasaladi, lekin
/// ularning ostida soxta ishlovchi turadi: Telegram ham, Bonvi API ham
/// shu yerda javob beradi va har bir so'rov yozib boriladi.
/// </para>
/// </remarks>
public sealed class BotHarness : IDisposable
{
    /// <summary>Sinovdagi suhbatning raqami.</summary>
    public const long ChatId = 4242;

    private readonly BonviClient _bonvi;
    private readonly HttpClient _telegramHttp;
    private readonly HttpClient _bonviHttp;
    private readonly ConversationStore _store;
    private readonly BotWorker _worker;

    private long _updateId;

    /// <summary>
    /// Sinov stendi. <paramref name="backup"/> — suhbat nusxasi: ikki stend
    /// BIR nusxani baham ko'rsa, ikkinchisi «qayta ishga tushgan bot» bo'ladi.
    /// </summary>
    public BotHarness(IConversationBackup? backup = null)
    {
        var telegramHandler = new FakeHandler(HandleTelegram);
        var bonviHandler = new FakeHandler(HandleBonvi);

        _telegramHttp = new HttpClient(telegramHandler) { BaseAddress = new Uri("https://api.telegram.org/") };
        _bonviHttp = new HttpClient(bonviHandler) { BaseAddress = new Uri("http://api.test/") };

        var token = new TelegramToken { Value = "TOKEN" };
        var telegram = new TelegramClient(_telegramHttp, token, NullLogger<TelegramClient>.Instance);

        var options = Options.Create(new BotOptions
        {
            ApiBaseUrl = "http://api.test",
            Login = "bot",
            Password = "secret",
        });

        _bonvi = new BonviClient(_bonviHttp, options, NullLogger<BonviClient>.Instance);
        _store = new ConversationStore(TimeSpan.FromMinutes(60), backup);

        // Qayta urinishlar KUTMASDAN: sinovda vaqt emas, urinishlar soni muhim.
        _worker = new BotWorker(telegram, token, _bonvi, _store, options, NullLogger<BotWorker>.Instance)
        {
            SubmitRetryDelays = [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero],
        };
    }

    /// <summary>Bot yuborgan xabarlar — birinchisidan oxirgisiga.</summary>
    public List<SentMessage> Sent { get; } = [];

    /// <summary>Bonvi API ga tushgan so'rovlar: «METOD yo'l».</summary>
    /// <remarks>
    /// TARTIB muhim: surat ariza YARATILGANDAN keyin yuborilishi kerak
    /// va buni faqat so'rovlar ketma-ketligi ko'rsatadi.
    /// </remarks>
    public List<string> ApiCalls { get; } = [];

    /// <summary>Takroriy ariza tekshiruviga ketgan so'rov satrlari (qochirilmagan).</summary>
    public List<string> CheckQueries { get; } = [];

    /// <summary>Ochiq vakansiyalar — bo'sh ro'yxat «ochiq ish o'rni yo'q» degani.</summary>
    public List<OpenVacancy> Vacancies { get; } = [];

    /// <summary>«Telegram» kanali — serverdagi seed identifikatori.</summary>
    public static readonly Guid TelegramChannel = Guid.Parse("0199f0a0-5e1d-7c00-8000-000000000005");

    /// <summary>«Tanishlar orqali» — aniqlik so'raydi.</summary>
    public static readonly Guid AcquaintanceChannel = Guid.Parse("0199f0a0-5e1d-7c00-8000-000000000001");

    /// <summary>«OLX.uz».</summary>
    public static readonly Guid OlxChannel = Guid.Parse("0199f0a0-5e1d-7c00-8000-000000000003");

    /// <summary>
    /// «Qayerdan bilgan» kanallari — <c>GET /api/recruiting/referral-channels</c> javobi
    /// (server tartibida, ya'ni alifbo bo'yicha).
    /// </summary>
    public List<ReferralChannel> ReferralChannels { get; } =
    [
        new(OlxChannel, "OLX.uz", false),
        new(AcquaintanceChannel, "Tanishlar orqali", true),
        new(TelegramChannel, "Telegram", false),
    ];

    /// <summary>Kanallar ro'yxati so'rovi javob beradimi (<c>false</c> — 500).</summary>
    public bool ReferralChannelsSucceed { get; set; } = true;

    /// <summary>«Qayerdan bilgan» tugmasining ma'lumoti.</summary>
    public static string Referral(Guid channel) => "src:" + channel.ToString("N");

    /// <summary>Sozlamadagi ommaviy oferta havolasi.</summary>
    public string OfferUrl { get; set; } = string.Empty;

    /// <summary>
    /// «Umumiy anketa» ochiqmi (<c>GET /api/recruiting/vacancies/general-pool</c>).
    /// <c>null</c> — server javob bermaydi (500).
    /// </summary>
    public bool? GeneralPoolOpen { get; set; } = true;

    /// <summary>Navbatdagi rad etish xabarlari (server tartibida — eng eskisi birinchi).</summary>
    public List<RejectionNotice> PendingRejections { get; } = [];

    /// <summary>Navbatdagi suhbat takliflari.</summary>
    public List<InterviewNotice> PendingInterviews { get; } = [];

    /// <summary>Candidates awaiting the day-before reminder.</summary>
    public List<InterviewNotice> PendingReminders { get; } = [];

    /// <summary>The map point in the settings.</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>The token the server accepts in <c>/start token</c>.</summary>
    public string? ValidLinkToken { get; set; }

    /// <summary>The language the server answers a valid token with.</summary>
    public string? LinkedLanguage { get; set; } = "uz-Latn";

    /// <summary>Bodies of the token link requests.</summary>
    public List<string> LinkRequests { get; } = [];

    /// <summary>Bodies of the username or phone match requests.</summary>
    public List<string> MatchRequests { get; } = [];

    /// <summary>Whether the server accepts the username report.</summary>
    public bool UsernameReportSucceeds { get; set; } = true;

    /// <summary>Usernames reported to the server.</summary>
    public List<string> ReportedUsernames { get; } = [];

    /// <summary>Bu suhbatlar botni BLOKLAGAN — Telegram 403 qaytaradi.</summary>
    public HashSet<long> BlockedChats { get; } = [];

    /// <summary>Telegram'ga umuman yetib bo'lmaydi (tarmoq uzilgan).</summary>
    public bool TelegramUnreachable { get; set; }

    /// <summary>Telegram'ga «sendMessage» urinishlari — chat bo'yicha (muvaffaqiyatsizlari ham).</summary>
    public List<long> SendAttempts { get; } = [];

    /// <summary>Joylashuv yuborilgan suhbatlar.</summary>
    public List<long> LocationsSent { get; } = [];

    /// <summary>Belgilangan xabarnomalar: (tur, nomzod, yetkazildimi).</summary>
    public List<(string Kind, Guid CandidateId, bool Delivered)> Marked { get; } = [];

    /// <summary>Navbat bir so'rovda shuncha yozuv beradi — server bilan bir xil.</summary>
    public const int NoticeBatch = 20;

    /// <summary>Halqaning bitta aylanishidagi xabarnomalar qadami.</summary>
    public Task NoticesAsync() => _worker.SendNoticesAsync(CancellationToken.None);

    /// <summary>
    /// Server yangi arizani to'sadimi va NEGA.
    /// </summary>
    /// <remarks>
    /// ⚠️ Ilgari bu yerda <c>HasExistingApplication</c> degan bayroq
    /// turardi va bot qoidani o'zi yozardi. Endi qaror SERVERNIKI
    /// (<c>CheckApplicationQuery</c>), sabablari esa uchta va har
    /// biriga boshqa xabar ketadi — bitta bayroq ularni ajrata
    /// olmasdi.
    /// </remarks>
    public ApplicationBlockReason Block { get; set; } = ApplicationBlockReason.None;

    /// <summary>Mavjud arizaning sanasi — UTC da, xuddi API qaytargandek.</summary>
    public DateTimeOffset ExistingAppliedAt { get; set; } = new(2026, 9, 5, 20, 30, 0, TimeSpan.Zero);

    /// <summary>Muddat tugaydigan sana — faqat «Cooldown» da ma'noli.</summary>
    public DateOnly AvailableFrom { get; set; } = new(2027, 3, 6);

    /// <summary>Server to'siq bilan birga TAFSILOTLARNI ham yuboradimi.</summary>
    /// <remarks>
    /// <c>false</c> — javobda «yo'q» bor, lekin sana yo'q. Bot bunday
    /// javobda tayanch matnga tushishi kerak; to'siqni jimgina ochib
    /// yuborish esa MUMKIN emas.
    /// </remarks>
    public bool WithDetails { get; set; } = true;

    /// <summary>Tekshiruv so'rovi umuman javob beradimi.</summary>
    /// <remarks>
    /// <c>false</c> — server 500 qaytaradi. Bu holat ataylab sinaladi:
    /// javob kelmaganda anketa DAVOM etishi kerak, aks holda API
    /// yiqilgan paytda bot hamma nomzodni to'sib qo'yardi.
    /// </remarks>
    public bool CheckSucceeds { get; set; } = true;

    /// <summary>Ariza yaratilganda qaytadigan identifikator.</summary>
    public Guid CreatedCandidateId { get; } = Guid.Parse("11111111-2222-3333-4444-555555555555");

    /// <summary>Ariza yaratish so'rovi muvaffaqiyatli bo'ladimi.</summary>
    public bool RegisterSucceeds { get; set; } = true;

    /// <summary>Surat yuklash so'rovi muvaffaqiyatli bo'ladimi.</summary>
    public bool PhotoUploadSucceeds { get; set; } = true;

    /// <summary>Ariza so'rovining TANASI — maydonlar shu yerdan tekshiriladi.</summary>
    public JsonDocument? RegisterBody { get; private set; }

    /// <summary>
    /// Arizaga navbatdagi javoblar: <see cref="Unreachable"/> — API ga
    /// ulanib bo'lmaydi (deploy), boshqa qiymat — shu kod bilan 409.
    /// Navbat bo'shagach <see cref="RegisterSucceeds"/> amal qiladi.
    /// </summary>
    public Queue<string> RegisterPlan { get; } = new();

    /// <summary><see cref="RegisterPlan"/> dagi «API ga ulanib bo'lmadi» belgisi.</summary>
    public const string Unreachable = "UNREACHABLE";

    /// <summary>Har bir ariza urinishidagi <c>vacancyId</c> (bo'lmasa <c>null</c>).</summary>
    public List<Guid?> RegisterVacancyIds { get; } = [];

    /// <summary>Sozlamadagi «Suhbat muddati» (daqiqa).</summary>
    public int ConversationTimeoutMinutes { get; set; } = 60;

    /// <summary>Hozirgi suhbat muddati — bot sozlamani o'qigach yangilanadi.</summary>
    public TimeSpan Lifetime => _store.Lifetime;

    /// <summary>Suhbatning hozirgi holati — hech narsa bo'lmasa <c>null</c>.</summary>
    public ConversationState? State => _store.Find(ChatId);

    /// <summary>Oxirgi xabar.</summary>
    public SentMessage Last => Sent[^1];

    /// <summary>Nomzod matn yozdi.</summary>
    public Task TextAsync(string text) => SendAsync(new TelegramMessage(1, new TelegramChat(ChatId), text, null));

    /// <summary>The person opened the bot, with this text and Telegram username.</summary>
    public Task StartAsync(string text, string? username)
        => SendAsync(new TelegramMessage(1, new TelegramChat(ChatId), text, null, From: new TelegramUser(username)));

    /// <summary>Nomzod «Raqamni yuborish» tugmasini bosdi.</summary>
    public Task ContactAsync(string phone = "+998901234567")
        => SendAsync(new TelegramMessage(1, new TelegramChat(ChatId), null, new TelegramContact(phone, ChatId)));

    /// <summary>Nomzod rasm yubordi.</summary>
    public Task PhotoAsync(string fileId = "photo-1")
        => SendAsync(new TelegramMessage(
            1,
            new TelegramChat(ChatId),
            null,
            null,
            [new TelegramPhotoSize(fileId, 800, 600, 120_000)]));

    /// <summary>Nomzod xabar ostidagi tugmani bosdi.</summary>
    public async Task PressAsync(string data)
    {
        var update = new TelegramUpdate(
            ++_updateId,
            null,
            new TelegramCallbackQuery("cb", data, new TelegramMessage(1, new TelegramChat(ChatId), null, null)));

        await _worker.HandleAsync(update, CancellationToken.None);
    }

    /// <summary>Til tanlandi — anketaning 1-qadami.</summary>
    public Task ChooseLanguageAsync() => PressAsync("lang:latn");

    /// <summary>Menyudagi «Ariza topshirish» bosildi.</summary>
    /// <summary>Menyudagi «Vakansiyalar» tugmasi — faqat ro'yxatni ochadi.</summary>
    public Task VacanciesAsync() => TextAsync(BotTexts.VacanciesButton(BotLanguage.Latin));

    /// <summary>Menyudagi «Anketa to'ldirish» tugmasi — vakansiyasiz anketa.</summary>
    public Task QuestionnaireAsync() => TextAsync(BotTexts.QuestionnaireButton(BotLanguage.Latin));

    /// <summary>Oxirgi xabarda shu tugma bormi.</summary>
    public bool HasButton(string data) => Last.Buttons.Contains(data, StringComparer.Ordinal);

    public void Dispose()
    {
        RegisterBody?.Dispose();
        _bonvi.Dispose();
        _telegramHttp.Dispose();
        _bonviHttp.Dispose();
    }

    private async Task SendAsync(TelegramMessage message)
        => await _worker.HandleAsync(new TelegramUpdate(++_updateId, message, null), CancellationToken.None);

    private HttpResponseMessage HandleTelegram(HttpRequestMessage request, string body)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;

        if (path.EndsWith("/sendMessage", StringComparison.Ordinal))
        {
            using var hujjat = JsonDocument.Parse(body);
            var chat = hujjat.RootElement.TryGetProperty("chat_id", out var id) ? id.GetInt64() : 0;

            SendAttempts.Add(chat);

            if (TelegramUnreachable)
            {
                throw new HttpRequestException(HttpRequestError.ConnectionError, "api.telegram.org ga ulanib bo'lmadi");
            }

            if (BlockedChats.Contains(chat))
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent(
                        """{"ok":false,"error_code":403,"description":"Forbidden: bot was blocked by the user"}""",
                        Encoding.UTF8,
                        "application/json"),
                };
            }

            Sent.Add(ReadMessage(body));
            return Json("""{"ok":true,"result":{"message_id":1}}""");
        }

        if (path.EndsWith("/sendLocation", StringComparison.Ordinal))
        {
            using var hujjat = JsonDocument.Parse(body);
            LocationsSent.Add(hujjat.RootElement.GetProperty("chat_id").GetInt64());
            return Json("""{"ok":true,"result":{"message_id":2}}""");
        }

        if (path.EndsWith("/getFile", StringComparison.Ordinal))
        {
            return Json("""{"ok":true,"result":{"file_path":"photos/file_1.jpg"}}""");
        }

        if (path.StartsWith("/file/", StringComparison.Ordinal))
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3, 4]),
            };
        }

        return Json("""{"ok":true,"result":{}}""");
    }

    private HttpResponseMessage HandleBonvi(HttpRequestMessage request, string body)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;

        ApiCalls.Add($"{request.Method} {path}");

        if (path == "/api/auth/login")
        {
            return Json("""{"accessToken":"jwt"}""");
        }

        if (path == "/api/settings/bot")
        {
            return Json(Settings());
        }

        if (path == "/api/recruiting/vacancies/general-pool")
        {
            return GeneralPoolOpen is { } ochiq
                ? Json($$"""{"vacancyId":"99999999-9999-9999-9999-999999999999","isOpen":{{(ochiq ? "true" : "false")}}}""")
                : new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("xato") };
        }

        if (path == "/api/recruiting/vacancies")
        {
            var items = string.Join(
                ',',
                Vacancies.Select(vacancy =>
                    $$"""{"id":"{{vacancy.Id}}","title":{{JsonSerializer.Serialize(vacancy.Title)}}}"""));

            return Json($$"""{"items":[{{items}}],"total":{{Vacancies.Count}}}""");
        }

        if (path == "/api/recruiting/referral-channels")
        {
            if (!ReferralChannelsSucceed)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("xato") };
            }

            var items = string.Join(
                ',',
                ReferralChannels.Select(channel =>
                    $$"""{"id":"{{channel.Id}}","name":{{JsonSerializer.Serialize(channel.Name)}},"isActive":true,"asksDetail":{{(channel.AsksDetail ? "true" : "false")}}}"""));

            return Json($"[{items}]");
        }

        if (path == "/api/recruiting/candidates/application-check")
        {
            CheckQueries.Add(Uri.UnescapeDataString(request.RequestUri?.Query ?? string.Empty));

            return CheckSucceeds
                ? Json(ApplicationCheck())
                : new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("xato"),
                };
        }

        if (path == "/api/recruiting/candidates" && request.Method == HttpMethod.Post)
        {
            RegisterBody?.Dispose();
            RegisterBody = JsonDocument.Parse(body);

            RegisterVacancyIds.Add(
                RegisterBody.RootElement.TryGetProperty("vacancyId", out var vacancy) ? vacancy.GetGuid() : null);

            if (RegisterPlan.TryDequeue(out var planned))
            {
                if (planned == Unreachable)
                {
                    throw new HttpRequestException(
                        HttpRequestError.ConnectionError,
                        "Connection refused (api:8080)");
                }

                return new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent(
                        $$"""{"title":"{{planned}}","status":409}""",
                        Encoding.UTF8,
                        "application/problem+json"),
                };
            }

            return RegisterSucceeds
                ? Json($$"""{"id":"{{CreatedCandidateId}}"}""")
                : new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("xato"),
                };
        }

        if (path.EndsWith("/photo", StringComparison.Ordinal))
        {
            return PhotoUploadSucceeds
                ? Json("""{"key":"photo"}""")
                : new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("xato"),
                };
        }

        if (path.EndsWith("/rejection-notices", StringComparison.Ordinal))
        {
            return Json(JsonSerializer.Serialize(PendingRejections.Take(NoticeBatch), WebJson));
        }

        if (path.EndsWith("/interview-notices", StringComparison.Ordinal))
        {
            return Json(JsonSerializer.Serialize(PendingInterviews.Take(NoticeBatch), WebJson));
        }

        if (path.EndsWith("/interview-reminders", StringComparison.Ordinal))
        {
            return Json(JsonSerializer.Serialize(PendingReminders.Take(NoticeBatch), WebJson));
        }

        if (path.EndsWith("/interview-reminder-sent", StringComparison.Ordinal))
        {
            var candidateId = Guid.Parse(path.Split('/')[^2]);
            var delivered = !(request.RequestUri?.Query ?? string.Empty).Contains("delivered=false", StringComparison.Ordinal);

            Marked.Add(("reminder", candidateId, delivered));
            PendingReminders.RemoveAll(notice => notice.CandidateId == candidateId);

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (path.EndsWith("/candidates/telegram-link", StringComparison.Ordinal))
        {
            LinkRequests.Add(body);

            var linked = ValidLinkToken is not null && body.Contains(ValidLinkToken, StringComparison.Ordinal);

            return Json(JsonSerializer.Serialize(new { linked, language = linked ? LinkedLanguage : null }, WebJson));
        }

        if (path.EndsWith("/candidates/telegram-match", StringComparison.Ordinal))
        {
            MatchRequests.Add(body);

            return Json("1");
        }

        if (path == "/api/settings/bot/username")
        {
            ReportedUsernames.Add(body);

            return new HttpResponseMessage(UsernameReportSucceeds ? HttpStatusCode.NoContent : HttpStatusCode.InternalServerError);
        }

        if (path.EndsWith("/rejection-notice-sent", StringComparison.Ordinal)
            || path.EndsWith("/interview-notice-sent", StringComparison.Ordinal))
        {
            var kind = path.EndsWith("/rejection-notice-sent", StringComparison.Ordinal) ? "rejection" : "interview";
            var candidateId = Guid.Parse(path.Split('/')[^2]);
            var delivered = !(request.RequestUri?.Query ?? string.Empty).Contains("delivered=false", StringComparison.Ordinal);

            Marked.Add((kind, candidateId, delivered));
            PendingRejections.RemoveAll(notice => kind == "rejection" && notice.CandidateId == candidateId);
            PendingInterviews.RemoveAll(notice => kind == "interview" && notice.CandidateId == candidateId);

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        return Json("{}");
    }

    /// <summary>«Bu odam ariza topshira oladimi» so'rovining javobi.</summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Javob QO'LDA yoziladi, obyektdan seriyalanmaydi.</b> Sabab —
    /// aynan shu joyda haqiqiy xato yashiringan edi: server sanoqni
    /// MATN bo'lib qaytaradi (<c>"reason":"Cooldown"</c>) va botning
    /// javob o'qish sozlamasida <c>JsonStringEnumConverter</c> yo'q
    /// edi. Obyektni bot sozlamasi bilan seriyalash bu xatoni yashirib
    /// qo'yardi: ikkala tomon ham bir xil noto'g'ri qoidada yurardi.
    /// </para>
    /// </remarks>
    private string ApplicationCheck()
    {
        var applied = ExistingAppliedAt.ToString("O", CultureInfo.InvariantCulture);
        var available = AvailableFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return Block switch
        {
            ApplicationBlockReason.Active => $$"""
                {"allowed":false,"reason":"Active","appliedAt":"{{applied}}",
                 "vacancyTitle":"Kassir","stageName":"Anketa","availableFrom":null}
                """,
            // Tafsilotsiz to'siq: server «yo'q» dedi, lekin sana
            // yubormadi. Bot bunday javobda tayanch matnga tushishi va
            // to'siqni JIMGINA ochib yubormasligi kerak. Shart avvalroq
            // turadi — quyidagi umumiy tarmoq uni yutib yuborardi.
            ApplicationBlockReason.Cooldown when !WithDetails => """
                {"allowed":false,"reason":"Cooldown","appliedAt":null,
                 "vacancyTitle":null,"stageName":null,"availableFrom":null}
                """,
            ApplicationBlockReason.Cooldown => $$"""
                {"allowed":false,"reason":"Cooldown","appliedAt":"{{applied}}",
                 "vacancyTitle":"Kassir","stageName":"Anketa","availableFrom":"{{available}}"}
                """,
            // Qora ro'yxat — server hech qanday tafsilot yubormaydi.
            ApplicationBlockReason.Blacklisted => """
                {"allowed":false,"reason":"Blacklisted","appliedAt":null,
                 "vacancyTitle":null,"stageName":null,"availableFrom":null}
                """,
            _ => """
                {"allowed":true,"reason":"None","appliedAt":null,
                 "vacancyTitle":null,"stageName":null,"availableFrom":null}
                """,
        };
    }

    private string Settings() => $$"""
        {
          "aboutCyrillic":"","aboutLatin":"","aboutRussian":"",
          "contactsCyrillic":"","contactsLatin":"","contactsRussian":"",
          "location":{{JsonSerializer.Serialize(Location)}},"hasLogo":false,
          "instagram":"","telegram":"","facebook":"","website":"",
          "offerUrl":{{JsonSerializer.Serialize(OfferUrl)}},
          "conversationTimeoutMinutes":{{ConversationTimeoutMinutes}}
        }
        """;

    /// <summary>Telegram'ga ketgan «sendMessage» tanasini o'qiydi.</summary>
    private static SentMessage ReadMessage(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        var text = root.TryGetProperty("text", out var value) ? value.GetString() ?? string.Empty : string.Empty;
        var buttons = new List<string>();
        var menu = new List<string>();
        var requestsContact = false;

        if (root.TryGetProperty("reply_markup", out var markup) && markup.ValueKind == JsonValueKind.Object)
        {
            if (markup.TryGetProperty("inline_keyboard", out var rows))
            {
                buttons.AddRange(
                    from row in rows.EnumerateArray()
                    from button in row.EnumerateArray()
                    where button.TryGetProperty("callback_data", out _)
                    select button.GetProperty("callback_data").GetString() ?? string.Empty);
            }

            if (markup.TryGetProperty("keyboard", out var keyboard))
            {
                requestsContact = keyboard.EnumerateArray()
                    .SelectMany(row => row.EnumerateArray())
                    .Any(button => button.TryGetProperty("request_contact", out var flag) && flag.GetBoolean());

                menu.AddRange(
                    from row in keyboard.EnumerateArray()
                    from button in row.EnumerateArray()
                    where button.ValueKind == JsonValueKind.Object && button.TryGetProperty("text", out _)
                    select button.GetProperty("text").GetString() ?? string.Empty);
            }
        }

        return new SentMessage(text, buttons, requestsContact, menu);
    }

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private static HttpResponseMessage Json(string payload)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

    /// <summary>So'rovni tutib qoladigan ishlovchi.</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string, HttpResponseMessage> _respond;

        public FakeHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond) => _respond = respond;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return _respond(request, body);
        }
    }
}
