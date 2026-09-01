using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bonvi.Bot.Bonvi;

/// <summary>Ochiq vakansiya — botda ko'rsatiladigan minimal ma'lumot.</summary>
public sealed record OpenVacancy(Guid Id, string Title);

/// <summary>Botning tizimdan olinadigan sozlamalari.</summary>
public sealed record BotSettings(
    string AboutCyrillic,
    string AboutLatin,
    string AboutRussian,
    string ContactsCyrillic,
    string ContactsLatin,
    string ContactsRussian,
    string Location,
    bool HasLogo,
    string Instagram,
    string Telegram,
    string Facebook,
    string Website,
    int ConversationTimeoutMinutes);

/// <summary>Tizimda saqlangan Telegram tokeni. Qo'yilmagan bo'lsa <c>null</c>.</summary>
/// <remarks>
/// Yalang'och satr emas, O'RAM: shu tufayli «so'rov bajarilmadi»
/// (<c>null</c> javob) va «token qo'yilmagan» (<c>Token = null</c>)
/// bir-biridan farq qiladi. Sabab <c>BonviClient.GetBotTokenAsync</c>
/// izohida.
/// </remarks>
public sealed record BotTokenResponse(string? Token);

/// <summary>Nomzodning mavjud arizasi — takror tekshiruvi uchun.</summary>
public sealed record ExistingApplication(DateTimeOffset AppliedAt, string? StageName);

/// <summary>
/// Rad etilgan, lekin xabarni hali olmagan nomzod.
/// </summary>
/// <param name="CandidateId">Xabar ketgach shu bilan belgilanadi.</param>
/// <param name="ChatId">Telegram suhbati.</param>
/// <param name="Language">Nomzod tanlagan til: «uz-Cyrl», «uz-Latn», «ru».</param>
/// <param name="FullName">Nomzodning ismi — xabar unga murojaat bilan boshlanadi.</param>
/// <param name="VacancyTitle">Qaysi vakansiyaga ariza bergani.</param>
public sealed record RejectionNotice(
    Guid CandidateId,
    long ChatId,
    string? Language,
    string FullName,
    string VacancyTitle);

/// <summary>
/// Bonvi API bilan ishlaydigan mijoz — bot xizmat hisobi nomidan.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bot bazaga tegmaydi.</b> Barcha amallar API orqali va
/// <c>recruiting_bot</c> rolining ikkita ruxsati doirasida bajariladi
/// (izohi <c>Bonvi.Bot.csproj</c> da). Ya'ni bu sinfdagi xato ham,
/// tokenning sizib ketishi ham xodimlar ma'lumotiga yeta olmaydi.
/// </para>
/// <para>
/// <b>Token yopiq holda yangilanadi.</b> Kirish tokeni muddati tugasa
/// API <c>401</c> qaytaradi; shunda mijoz BIR MARTA qayta kirib, so'rovni
/// takrorlaydi. Har so'rovdan oldin muddatni sanab o'tirish
/// (soatlarni solishtirish) ikkita soat orasidagi farqqa bog'liq bo'lardi
/// — javobning o'ziga tayanish ishonchliroq.
/// </para>
/// </remarks>
public sealed partial class BonviClient : IDisposable
{
    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Error,
        Message = "Bot hisobi bilan kirib bo'lmadi ({Status}). «Bootstrap:BotLogin» va «Bot:Login» bir xilmi?")]
    private partial void LogSignInRejected(int status);

    [LoggerMessage(
        EventId = 3102,
        Level = LogLevel.Error,
        Message = "Bonvi API ga ulanib bo'lmadi: {Url}")]
    private partial void LogSignInFailed(Exception exception, string url);

    [LoggerMessage(
        EventId = 3103,
        Level = LogLevel.Warning,
        Message = "Bonvi API {Status} qaytardi: {Detail}")]
    private partial void LogApiError(int status, string detail);

    [LoggerMessage(
        EventId = 3104,
        Level = LogLevel.Warning,
        Message = "Bonvi API so'rovi bajarilmadi")]
    private partial void LogRequestFailed(Exception exception);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly BotOptions _options;
    private readonly ILogger<BonviClient> _logger;

    /// <summary>Kirish tokenini bir vaqtda ikkita so'rov yangilab yubormasin.</summary>
    private readonly SemaphoreSlim _loginLock = new(1, 1);

    private string? _accessToken;

    public BonviClient(HttpClient http, IOptions<BotOptions> options, ILogger<BonviClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Xizmat hisobi bilan kiradi. Ishga tushishda tekshiriladi.</summary>
    public async Task<bool> SignInAsync(CancellationToken cancellationToken)
    {
        await _loginLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var response = await _http
                .PostAsJsonAsync(
                    new Uri("api/auth/login", UriKind.Relative),
                    new { login = _options.Login, password = _options.Password },
                    Json,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                LogSignInRejected((int)response.StatusCode);

                _accessToken = null;
                return false;
            }

            var payload = await response.Content
                .ReadFromJsonAsync<LoginResponse>(Json, cancellationToken)
                .ConfigureAwait(false);

            _accessToken = payload?.AccessToken;
            return _accessToken is not null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogSignInFailed(ex, _options.ApiBaseUrl);
            _accessToken = null;
            return false;
        }
        finally
        {
            _ = _loginLock.Release();
        }
    }

    /// <summary>
    /// Ochiq vakansiyalar ro'yxati.
    /// </summary>
    /// <remarks>
    /// FAQAT <c>Open</c>: yopilgan vakansiyaga ariza qabul qilish nomzodni
    /// ham, HR ni ham bekorga ovora qilardi.
    /// </remarks>
    public async Task<IReadOnlyList<OpenVacancy>> GetOpenVacanciesAsync(CancellationToken cancellationToken)
    {
        var page = await SendAsync<VacancyPage>(
                () => new HttpRequestMessage(
                    HttpMethod.Get,
                    new Uri("api/recruiting/vacancies?status=Open&page=1&pageSize=50", UriKind.Relative)),
                cancellationToken)
            .ConfigureAwait(false);

        if (page?.Items is null)
        {
            return [];
        }

        return
        [
            .. page.Items
                .Where(item => !string.IsNullOrWhiteSpace(item.Title))
                .Select(item => new OpenVacancy(item.Id, item.Title!)),
        ];
    }

    /// <summary>
    /// Shu telefon bilan shu vakansiyaga ariza allaqachon bormi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Qidiruv API ning O'Z qoidasi bilan ketadi (F.I.Sh. yoki TELEFON,
    /// ajratgichlarni hisobga oladi) — bot uni takrorlamaydi.
    /// </para>
    /// <para>
    /// <c>status=InProgress</c>: rad etilgan yoki o'zi voz kechgan nomzod
    /// qayta ariza topshira olishi KERAK — odamning fikri o'zgaradi va
    /// vakansiya ham boshqa bo'ladi. To'siq faqat HALI KO'RILAYOTGAN
    /// ariza uchun.
    /// </para>
    /// </remarks>
    public async Task<ExistingApplication?> FindActiveApplicationAsync(
        string phone,
        Guid vacancyId,
        CancellationToken cancellationToken)
    {
        var query = $"api/recruiting/candidates?search={Uri.EscapeDataString(phone)}"
            + $"&vacancyId={vacancyId}&status=InProgress&page=1&pageSize=1";

        var page = await SendAsync<CandidatePage>(
                () => new HttpRequestMessage(HttpMethod.Get, new Uri(query, UriKind.Relative)),
                cancellationToken)
            .ConfigureAwait(false);

        var found = page?.Items?.FirstOrDefault();

        return found is null ? null : new ExistingApplication(found.CreatedAt, found.CurrentStage);
    }

    /// <summary>
    /// Botning sozlamalarini oladi — kompaniya matni va suhbat muddati.
    /// </summary>
    /// <remarks>
    /// Sozlama endi TIZIMDA saqlanadi, `.env` da emas: super
    /// administrator uni ekrandan o'zgartiradi va bot qayta ishga
    /// tushirilmaydi. Javob kelmasa (API yiqilgan) — chaqiruvchi eski
    /// qiymat bilan davom etadi.
    /// </remarks>
    public async Task<BotSettings?> GetBotSettingsAsync(CancellationToken cancellationToken)
        => await SendAsync<BotSettings>(
                () => new HttpRequestMessage(
                    HttpMethod.Get,
                    new Uri("api/settings/bot", UriKind.Relative)),
                cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Rad etish xabari kutayotgan nomzodlar.
    /// </summary>
    /// <remarks>
    /// Odatda BO'SH ro'yxat qaytaradi — bu so'rov halqaning har
    /// aylanishida takrorlanadi. Javob kelmasa (API yiqilgan) bo'sh
    /// ro'yxat qaytadi va bot keyingi aylanishda qayta uriniladi:
    /// nomzodlar navbatda qolaveradi.
    /// </remarks>
    public async Task<IReadOnlyList<RejectionNotice>> GetPendingRejectionNoticesAsync(
        CancellationToken cancellationToken)
        => await SendAsync<List<RejectionNotice>>(
                () => new HttpRequestMessage(
                    HttpMethod.Get,
                    new Uri("api/recruiting/candidates/rejection-notices", UriKind.Relative)),
                cancellationToken)
            .ConfigureAwait(false)
            ?? [];

    /// <summary>
    /// Rad etish xabari yuborilganini belgilaydi.
    /// </summary>
    /// <remarks>
    /// Xabar HAQIQATAN ketgandan KEYIN chaqiriladi. Belgilash
    /// bajarilmasa (tarmoq uzildi), nomzod navbatda qoladi va xabar
    /// TAKRORLANADI — bu yoqimsiz, lekin javobsiz qoldirishdan
    /// yaxshiroq va shu sababli tartib aynan shunday.
    /// </remarks>
    public async Task<bool> MarkRejectionNoticeSentAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        var path = $"api/recruiting/candidates/{candidateId:D}/rejection-notice-sent";

        return await SendNoContentAsync(
                () => new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Kompaniya logotipining baytlarini oladi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bayt oqimi <see cref="SendAsync{T}"/> dan o'tmaydi: u javobni
    /// JSON deb o'qiydi. Shu sababli so'rov shu yerda qo'lda
    /// yig'iladi — lekin qayta kirish qoidasi bir xil: 401 kelsa bir
    /// marta qayta kiriladi.
    /// </para>
    /// <para>
    /// Rasm YO'Q bo'lishi odatiy hol (404) va u xato sifatida
    /// jurnalga yozilmaydi: logotip qo'yilmagan bo'lsa bot
    /// «Kompaniya haqida» ni faqat matn bilan yuboradi.
    /// </para>
    /// </remarks>
    /// <returns>Rasm baytlari, yo'q bo'lsa <c>null</c>.</returns>
    public async Task<byte[]?> GetBotLogoAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var message = new HttpRequestMessage(
                    HttpMethod.Get,
                    new Uri("api/settings/bot/logo", UriKind.Relative));

                if (_accessToken is not null)
                {
                    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                }

                using var response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
                {
                    if (!await SignInAsync(cancellationToken).ConfigureAwait(false))
                    {
                        return null;
                    }

                    continue;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    LogApiError((int)response.StatusCode, "logotip olinmadi");
                    return null;
                }

                return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogRequestFailed(ex);
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Telegram tokenini tizimdan oladi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega alohida so'rov, <see cref="GetBotSettingsAsync"/> ichida
    /// emas.</b> Token — SIR va u <c>/api/settings/bot</c> javobiga
    /// qo'shilmaydi: o'sha endpoint <c>recruiting.view</c> ruxsati ostida,
    /// bu ruxsat esa HR menejerlarida ham bor. Token faqat
    /// <c>recruiting_bot</c> ROLI ostidagi hisobga ochiq endpoint'dan
    /// keladi (sabab serverda, <c>SettingsEndpoints</c> da).
    /// </para>
    /// <para>
    /// <b>Uch xil javobning uch xil ma'nosi bor va ular chalkashmasligi
    /// kerak:</b>
    /// <list type="bullet">
    ///   <item>qiymatli satr — ekranda qo'yilgan token;</item>
    ///   <item><c>Token = null</c> bilan javob — ekranda QO'YILMAGAN,
    ///         bot <c>.env</c> dagi zaxiraga tushadi;</item>
    ///   <item><c>null</c> javob (so'rov bajarilmadi) — bilib bo'lmadi,
    ///         eski qiymat saqlanadi.</item>
    /// </list>
    /// Uchinchi holatni ikkinchisi bilan qo'shib yuborish eng zararli
    /// xato bo'lardi: API bir daqiqaga yiqilganda ishlab turgan bot
    /// tokenini o'zi tashlab yuborardi.
    /// </para>
    /// </remarks>
    public async Task<BotTokenResponse?> GetBotTokenAsync(CancellationToken cancellationToken)
        => await SendAsync<BotTokenResponse>(
                () => new HttpRequestMessage(
                    HttpMethod.Get,
                    new Uri("api/settings/bot/token", UriKind.Relative)),
                cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Nomzodni voronkaga kiritadi.</summary>
    /// <returns>Yozuv yaratildimi.</returns>
    public async Task<bool> RegisterCandidateAsync(
        Guid vacancyId,
        string lastName,
        string firstName,
        string? middleName,
        string phone,
        string? address,
        long chatId,
        string language,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            vacancyId,
            lastName,
            firstName,
            middleName,
            phone,

            /*
              Manzil — anketaning oxirgi savoli. HR uchun u shu yerda
              turishi shart: nomzod uzoq tumandan bo'lsa, birinchi
              qo'ng'iroqdayoq yo'l masalasi hal qilinadi.
            */
            address,

            // «Bot qancha nomzod keltirdi» degan savolning javobi shu
            // maydonda — HR nomzodlar ro'yxatida manba bo'yicha filtrlaydi.
            source = "TelegramBot",

            /*
              Suhbat raqami va til — nomzod RAD ETILGANDA unga javob
              yozish uchun. Busiz bot uni topa olmaydi: telefon raqami
              Telegram'da manzil emas.

              Til ham shu yerda yoziladi, keyin emas: nomzod uni AYNAN
              shu suhbatda tanlagan va boshqa hech qayerdan bilib
              bo'lmaydi.
            */
            telegramChatId = chatId,
            telegramLanguage = language,
        };

        var created = await SendAsync<JsonElement>(
                () => new HttpRequestMessage(
                    HttpMethod.Post,
                    new Uri("api/recruiting/candidates", UriKind.Relative))
                {
                    Content = JsonContent.Create(body, options: Json),
                },
                cancellationToken)
            .ConfigureAwait(false);

        return created.ValueKind == JsonValueKind.Object;
    }

    /// <summary>
    /// So'rovni yuboradi; <c>401</c> bo'lsa bir marta qayta kirib takrorlaydi.
    /// </summary>
    /// <remarks>
    /// So'rov FUNKSIYA bo'lib beriladi, tayyor obyekt bo'lib emas:
    /// <c>HttpRequestMessage</c> ni ikkinchi marta yuborib bo'lmaydi va
    /// takrorlash uchun uni qaytadan yasash kerak.
    /// </remarks>
    /// <summary>
    /// TANASIZ javob qaytaradigan so'rov — muvaffaqiyat holat kodidan.
    /// </summary>
    /// <remarks>
    /// <see cref="SendAsync{T}"/> javobni JSON deb o'qiydi, 204 esa
    /// bo'sh tana bilan keladi va o'qishga urinish ma'nosiz. Qolgan
    /// qoidalar bir xil: 401 da bir marta qayta kiriladi, tarmoq
    /// xatosi jurnalga tushadi va <c>false</c> qaytadi.
    /// </remarks>
    private async Task<bool> SendNoContentAsync(
        Func<HttpRequestMessage> request,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var message = request();

                if (_accessToken is not null)
                {
                    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                }

                using var response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
                {
                    if (!await SignInAsync(cancellationToken).ConfigureAwait(false))
                    {
                        return false;
                    }

                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    LogApiError((int)response.StatusCode, "so'rov bajarilmadi");
                    return false;
                }

                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogRequestFailed(ex);
                return false;
            }
        }

        return false;
    }

    private async Task<T?> SendAsync<T>(
        Func<HttpRequestMessage> request,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var message = request();

                if (_accessToken is not null)
                {
                    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                }

                using var response = await _http
                    .SendAsync(message, cancellationToken)
                    .ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
                {
                    if (!await SignInAsync(cancellationToken).ConfigureAwait(false))
                    {
                        return default;
                    }

                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var detail = await response.Content
                        .ReadAsStringAsync(cancellationToken)
                        .ConfigureAwait(false);

                    LogApiError((int)response.StatusCode, detail.Length > 300 ? detail[..300] : detail);

                    return default;
                }

                return await response.Content
                    .ReadFromJsonAsync<T>(Json, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogRequestFailed(ex);
                return default;
            }
        }

        return default;
    }

    /// <summary>
    /// <see cref="_loginLock"/> ni bo'shatadi.
    /// </summary>
    /// <remarks>
    /// Mijoz butun jarayon davomida yashaydi va amalda hech qachon
    /// tozalanmaydi, lekin `IDisposable` bo'lmasa tahlilchi (CA1001)
    /// haqli ravishda e'tiroz bildiradi: sinf tozalanadigan maydonga ega.
    /// </remarks>
    public void Dispose() => _loginLock.Dispose();

    private sealed record LoginResponse(
        [property: JsonPropertyName("accessToken")] string? AccessToken);

    private sealed record VacancyPage(List<VacancyItem>? Items);

    private sealed record VacancyItem(Guid Id, string? Title);

    private sealed record CandidatePage(List<CandidateItem>? Items);

    private sealed record CandidateItem(Guid Id, DateTimeOffset CreatedAt, string? CurrentStage);
}
