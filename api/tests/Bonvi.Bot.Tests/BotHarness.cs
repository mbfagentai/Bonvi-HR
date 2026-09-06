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
public sealed record SentMessage(string Text, IReadOnlyList<string> Buttons, bool RequestsContact);

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

    public BotHarness()
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
        _store = new ConversationStore(TimeSpan.FromMinutes(60));

        _worker = new BotWorker(telegram, token, _bonvi, _store, options, NullLogger<BotWorker>.Instance);
    }

    /// <summary>Bot yuborgan xabarlar — birinchisidan oxirgisiga.</summary>
    public List<SentMessage> Sent { get; } = [];

    /// <summary>Bonvi API ga tushgan so'rovlar: «METOD yo'l».</summary>
    /// <remarks>
    /// TARTIB muhim: surat ariza YARATILGANDAN keyin yuborilishi kerak
    /// va buni faqat so'rovlar ketma-ketligi ko'rsatadi.
    /// </remarks>
    public List<string> ApiCalls { get; } = [];

    /// <summary>Ochiq vakansiyalar — bo'sh ro'yxat «ochiq ish o'rni yo'q» degani.</summary>
    public List<OpenVacancy> Vacancies { get; } = [];

    /// <summary>Sozlamadagi ommaviy oferta havolasi.</summary>
    public string OfferUrl { get; set; } = string.Empty;

    /// <summary>Shu telefon bilan ko'rilayotgan ariza topildimi.</summary>
    public bool HasExistingApplication { get; set; }

    /// <summary>Mavjud arizaning sanasi — UTC da, xuddi API qaytargandek.</summary>
    public DateTimeOffset ExistingAppliedAt { get; set; } = new(2026, 9, 5, 20, 30, 0, TimeSpan.Zero);

    /// <summary>Ariza yaratilganda qaytadigan identifikator.</summary>
    public Guid CreatedCandidateId { get; } = Guid.Parse("11111111-2222-3333-4444-555555555555");

    /// <summary>Ariza yaratish so'rovi muvaffaqiyatli bo'ladimi.</summary>
    public bool RegisterSucceeds { get; set; } = true;

    /// <summary>Surat yuklash so'rovi muvaffaqiyatli bo'ladimi.</summary>
    public bool PhotoUploadSucceeds { get; set; } = true;

    /// <summary>Ariza so'rovining TANASI — maydonlar shu yerdan tekshiriladi.</summary>
    public JsonDocument? RegisterBody { get; private set; }

    /// <summary>Suhbatning hozirgi holati — hech narsa bo'lmasa <c>null</c>.</summary>
    public ConversationState? State => _store.Find(ChatId);

    /// <summary>Oxirgi xabar.</summary>
    public SentMessage Last => Sent[^1];

    /// <summary>Nomzod matn yozdi.</summary>
    public Task TextAsync(string text) => SendAsync(new TelegramMessage(1, new TelegramChat(ChatId), text, null));

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
    public Task ApplyAsync() => TextAsync(BotTexts.ApplyButton(BotLanguage.Latin));

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
            Sent.Add(ReadMessage(body));
            return Json("""{"ok":true,"result":{"message_id":1}}""");
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

        if (path == "/api/recruiting/vacancies")
        {
            var items = string.Join(
                ',',
                Vacancies.Select(vacancy =>
                    $$"""{"id":"{{vacancy.Id}}","title":{{JsonSerializer.Serialize(vacancy.Title)}}}"""));

            return Json($$"""{"items":[{{items}}],"total":{{Vacancies.Count}}}""");
        }

        if (path == "/api/recruiting/candidates" && request.Method == HttpMethod.Get)
        {
            return HasExistingApplication
                ? Json($$"""
                    {"items":[{"id":"{{Guid.Empty}}",
                    "createdAt":"{{ExistingAppliedAt.ToString("O", CultureInfo.InvariantCulture)}}",
                    "currentStage":"Anketa","vacancy":"Kassir"}]}
                    """)
                : Json("""{"items":[]}""");
        }

        if (path == "/api/recruiting/candidates" && request.Method == HttpMethod.Post)
        {
            RegisterBody?.Dispose();
            RegisterBody = JsonDocument.Parse(body);

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
            return Json("[]");
        }

        return Json("{}");
    }

    private string Settings() => $$"""
        {
          "aboutCyrillic":"","aboutLatin":"","aboutRussian":"",
          "contactsCyrillic":"","contactsLatin":"","contactsRussian":"",
          "location":"","hasLogo":false,
          "instagram":"","telegram":"","facebook":"","website":"",
          "offerUrl":{{JsonSerializer.Serialize(OfferUrl)}},
          "conversationTimeoutMinutes":60
        }
        """;

    /// <summary>Telegram'ga ketgan «sendMessage» tanasini o'qiydi.</summary>
    private static SentMessage ReadMessage(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        var text = root.TryGetProperty("text", out var value) ? value.GetString() ?? string.Empty : string.Empty;
        var buttons = new List<string>();
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
            }
        }

        return new SentMessage(text, buttons, requestsContact);
    }

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
