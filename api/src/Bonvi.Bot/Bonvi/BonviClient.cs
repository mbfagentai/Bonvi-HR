using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Bot.Conversation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bonvi.Bot.Bonvi;

/// <summary>Ochiq vakansiya — botda ko'rsatiladigan minimal ma'lumot.</summary>
public sealed record OpenVacancy(Guid Id, string Title);

/// <summary>Botning tizimdan olinadigan sozlamalari.</summary>
/// <remarks>
/// Maydonlar <c>GET /api/settings/bot</c> javobidagi nomlar bilan
/// AYNAN bir xil bo'lishi shart: mos kelmagan maydon xatoga aylanmaydi,
/// jimgina TASHLANADI. <c>OfferUrl</c> bilan aynan shunday bo'lgan —
/// server uni qaytarardi, bot esa ko'rmasdi.
/// </remarks>
/// <param name="AboutCyrillic">«Kompaniya haqida» — kirill.</param>
/// <param name="AboutLatin">«Kompaniya haqida» — lotin.</param>
/// <param name="AboutRussian">«Kompaniya haqida» — rus.</param>
/// <param name="ContactsCyrillic">Kontaktlar — kirill.</param>
/// <param name="ContactsLatin">Kontaktlar — lotin.</param>
/// <param name="ContactsRussian">Kontaktlar — rus.</param>
/// <param name="Location">Bosh ofisning xaritadagi nuqtasi.</param>
/// <param name="InterviewPhone">Suhbat taklifidagi mas'ul telefon.</param>
/// <param name="InterviewAddress">Suhbat taklifidagi manzil yorlig'i.</param>
/// <param name="HasLogo">Logotip yuklanganmi.</param>
/// <param name="Instagram">Instagram havolasi.</param>
/// <param name="Telegram">Telegram kanali havolasi.</param>
/// <param name="Facebook">Facebook havolasi.</param>
/// <param name="Website">Sayt havolasi.</param>
/// <param name="OfferUrl">
/// Ommaviy oferta havolasi. BO'SH bo'lsa anketaning 21-qadami umuman
/// chizilmaydi — bo'sh havolali xabar nomzodni boshi berk ko'chaga
/// olib borardi.
/// </param>
/// <param name="ConversationTimeoutMinutes">Suhbat necha daqiqa yashaydi.</param>
public sealed record BotSettings(
    string AboutCyrillic,
    string AboutLatin,
    string AboutRussian,
    string ContactsCyrillic,
    string ContactsLatin,
    string ContactsRussian,
    string Location,
    string InterviewPhone,
    string InterviewAddress,
    bool HasLogo,
    string Instagram,
    string Telegram,
    string Facebook,
    string Website,
    string OfferUrl,
    int ConversationTimeoutMinutes);

/// <summary>Arizani yuborish natijasi.</summary>
/// <param name="CandidateId">Yaratilgan nomzod. Ariza qabul qilinmagan bo'lsa — <c>null</c>.</param>
/// <param name="ErrorCode">
/// Server rad etgan bo'lsa — uning xato kodi (ProblemDetails <c>title</c>),
/// masalan <c>vakansiya.qabul_qilmaydi</c>. Bot unga qarab boshqa yo'l
/// tanlaydi (<c>BotWorker.SubmitAsync</c>).
/// </param>
/// <param name="Transient">
/// Server VAQTINCHA javob bermadi — so'rov unga yetib bormagani ANIQ
/// (ulanib bo'lmadi, nom topilmadi, 503). Bunday so'rovni qayta yuborish
/// xavfsiz: ikkinchi nusxa yaratilmaydi. Deploy paytida API aynan shunday
/// bir necha soniya javob bermaydi.
/// </param>
public sealed record CandidateRegistrationResult(Guid? CandidateId, string? ErrorCode, bool Transient);

/// <summary>Tizimda saqlangan Telegram tokeni. Qo'yilmagan bo'lsa <c>null</c>.</summary>
/// <remarks>
/// Yalang'och satr emas, O'RAM: shu tufayli «so'rov bajarilmadi»
/// (<c>null</c> javob) va «token qo'yilmagan» (<c>Token = null</c>)
/// bir-biridan farq qiladi. Sabab <c>BonviClient.GetBotTokenAsync</c>
/// izohida.
/// </remarks>
public sealed record BotTokenResponse(string? Token);

/// <summary>Yangi ariza nega qabul qilinmadi — server bergan sabab.</summary>
/// <remarks>
/// Qiymatlar server tomonidagi <c>ApplicationBlockReason</c> bilan
/// AYNAN bir xil yozilishi shart: JSON'da ular MATN bo'lib keladi.
/// </remarks>
public enum ApplicationBlockReason
{
    None = 0,
    Active = 1,
    Cooldown = 2,
}

/// <summary>
/// «Bu odam yangi anketa topshira oladimi» degan savolning javobi.
/// </summary>
/// <param name="Allowed">Anketa davom etadimi.</param>
/// <param name="Reason">To'siq sababi.</param>
/// <param name="AppliedAt">
/// Oldingi ariza qachon topshirilgan. Qiymat serverdan UTC da keladi va
/// nomzodga ko'rsatilishidan OLDIN Toshkent vaqtiga keltiriladi
/// (<c>BotClock</c>).
/// </param>
/// <param name="VacancyTitle">
/// Qaysi vakansiyaga topshirgani. Bu SERVERDAN keladi, suhbatdan emas:
/// nomzod hozir vakansiya tanlamagan bo'lishi mumkin (ochiq ish o'rni
/// yo'q), lekin eski arizasi baribir biror vakansiyada turadi.
/// </param>
/// <param name="StageName">Voronkadagi hozirgi bosqichi.</param>
/// <param name="AvailableFrom">
/// Qaysi sanadan keyin qayta topshirish mumkin. Faqat
/// <see cref="ApplicationBlockReason.Cooldown"/> da to'ladi.
/// </param>
public sealed record ApplicationCheck(
    bool Allowed,
    ApplicationBlockReason Reason,
    DateTimeOffset? AppliedAt,
    string? VacancyTitle,
    string? StageName,
    DateOnly? AvailableFrom);

/// <summary>
/// Botda to'ldirilgan anketaning javoblari — arizaning bir qismi.
/// </summary>
/// <remarks>
/// Sanoqlar serverga SATR bo'lib ketadi (a'zoning nomi bilan) — sabab
/// <c>Answers</c> izohida. Bo'sh matnli javoblar so'rov tanasiga umuman
/// qo'shilmaydi: «o'tkazib yuborildi» va «bo'sh satr yozildi» serverda
/// bir xil ma'noga ega bo'lishi kerak.
/// </remarks>
/// <param name="Education">Ma'lumot darajasi.</param>
/// <param name="MaritalStatus">Oilaviy holati.</param>
/// <param name="RussianLevel">Rus tili darajasi.</param>
/// <param name="ExpectedSalary">Kutilayotgan maosh oralig'i.</param>
/// <param name="ReferralSource">Ish haqida qayerdan bilgan.</param>
/// <param name="OfferAcceptedAt">
/// Ommaviy oferta bilan rozilik VAQTI. Server uni o'zi UTC ga
/// keltiradi, ya'ni siljish qanday bo'lishi ahamiyatsiz.
/// <para>
/// <c>null</c> — oferta havolasi sozlanmagan, 21-qadam chizilmagan va
/// nomzoddan rozilik SO'RALMAGAN. U holda maydon so'rov tanasiga
/// umuman tushmaydi (<c>RequestJson</c>) va server uni bo'sh saqlaydi.
/// Zaxira vaqt qo'yish huquqiy faktni soxtalashtirardi.
/// </para>
/// </param>
/// <param name="WorkExperience">Ish tajribasi.</param>
/// <param name="OtherLanguages">Boshqa tillar.</param>
/// <param name="OfficeSkills">Office dasturlari.</param>
/// <param name="LastSalary">Oxirgi maosh.</param>
/// <param name="ExtraNote">O'zi haqida qo'shimcha.</param>
/// <param name="HasDriverLicense">Haydovchilik guvohnomasi bormi.</param>
/// <param name="DriverLicenseCategories">Guvohnoma toifasi.</param>
public sealed record QuestionnaireAnswers(
    EducationAnswer Education,
    MaritalAnswer MaritalStatus,
    RussianLevelAnswer RussianLevel,
    SalaryAnswer ExpectedSalary,
    ReferralAnswer ReferralSource,
    DateTimeOffset? OfferAcceptedAt,
    string? WorkExperience = null,
    string? OtherLanguages = null,
    string? OfficeSkills = null,
    string? LastSalary = null,
    string? ExtraNote = null,
    bool HasDriverLicense = false,
    string? DriverLicenseCategories = null);

/// <summary>
/// Botdan yuboriladigan tayyor ariza.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega record, metodning parametrlari emas.</b> Ariza endi o'n
/// beshdan ortiq maydondan iborat: shuncha parametrli chaqiruvda qaysi
/// qiymat qayerga tushayotganini ko'z bilan tekshirib bo'lmasdi va
/// yonma-yon turgan ikkita <c>string?</c> ni almashtirib qo'yish
/// kompilyator ushlamaydigan xato bo'lardi.
/// </para>
/// <para>
/// ⚠️ <b><see cref="VacancyId"/> bo'sh bo'lishi ODATIY hol.</b> Ochiq
/// ish o'rni bo'lmaganda maydon so'rov tanasiga UMUMAN qo'shilmaydi va
/// server arizani o'zi «Umumiy anketa»ga qo'yadi. Bot bu yerda hech
/// qanday zaxira identifikator o'ylab topmaydi — u qaysi vakansiya
/// ekanini bilmaydi ham.
/// </para>
/// </remarks>
/// <param name="VacancyId">Tanlangan vakansiya — bo'lmasa <c>null</c>.</param>
/// <param name="LastName">Familiya.</param>
/// <param name="FirstName">Ism.</param>
/// <param name="MiddleName">Otasining ismi.</param>
/// <param name="BirthDate">Tug'ilgan sana.</param>
/// <param name="Phone">Telefon raqami — kontakt tugmasidan.</param>
/// <param name="Address">Yashash manzili.</param>
/// <param name="Speciality">Mutaxassisligi.</param>
/// <param name="ChatId">Telegram suhbatining raqami.</param>
/// <param name="Language">Nomzod tanlagan til kodi.</param>
/// <param name="Questionnaire">Anketa javoblari.</param>
public sealed record CandidateRegistration(
    Guid? VacancyId,
    string LastName,
    string FirstName,
    string? MiddleName,
    DateOnly? BirthDate,
    string Phone,
    string? Address,
    string? Speciality,
    long ChatId,
    string Language,
    QuestionnaireAnswers Questionnaire);

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

/// <summary>Suhbat taklifi kutayotgan nomzod.</summary>
/// <remarks>
/// <see cref="RejectionNotice"/> ning egizagi, ustiga bitta maydon —
/// suhbat lahzasi. U serverdan UTC da keladi va nomzodga
/// ko'rsatilishidan oldin Toshkent vaqtiga keltiriladi
/// (<c>BotTexts.InterviewNotice</c>).
/// </remarks>
/// <param name="CandidateId">Xabar ketgach shu bilan belgilanadi.</param>
/// <param name="ChatId">Telegram suhbati.</param>
/// <param name="Language">Nomzod tanlagan til.</param>
/// <param name="FullName">Murojaat uchun ism.</param>
/// <param name="VacancyTitle">Qaysi vakansiyaga.</param>
/// <param name="InterviewAt">Suhbat lahzasi — UTC.</param>
public sealed record InterviewNotice(
    Guid CandidateId,
    long ChatId,
    string? Language,
    string FullName,
    string VacancyTitle,
    DateTimeOffset InterviewAt);

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

    /// <summary>Serverdan kelgan JAVOBNI o'qish uchun sozlama.</summary>
    /// <remarks>
    /// ⭐ <b>Sanoqlar MATN bo'lib keladi</b> — server ularni shunday
    /// yozadi (<c>Program.cs</c>: <c>ConfigureHttpJsonOptions</c> ga
    /// <c>JsonStringEnumConverter</c> qo'shilgan). Converter'siz
    /// <c>"reason": "Cooldown"</c> ni o'qib bo'lmasdi va butun javob
    /// istisnoga aylanardi. Istisno esa <c>SendAsync</c> da <c>null</c>
    /// bo'lib qaytardi, ya'ni to'siq JIMGINA ochilib ketardi: muddat
    /// ichidagi odam anketani to'ldiraverardi va buni hech kim
    /// sezmasdi.
    /// <para>
    /// So'rov TANASI uchun alohida sozlama bor (<see cref="RequestJson"/>)
    /// va unda converter allaqachon bor edi. Ikkalasi bir xil qoidada
    /// yurishi shart — aks holda bot serverga nom yuborib, o'zi son
    /// kutib turardi.
    /// </para>
    /// </remarks>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// So'rov TANASI uchun alohida sozlama.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b><c>null</c> maydon so'rovga UMUMAN qo'shilmaydi.</b> Bu
    /// vakansiyasiz ariza uchun shart: <c>vacancyId</c> bo'sh
    /// yuborilganda server uni o'zi «Umumiy anketa»ga qo'yadi, lekin
    /// buning uchun maydon TANADA BO'LMASLIGI kerak. O'tkazib
    /// yuborilgan savollar ham shu qoida bilan tushib qoladi.
    /// </para>
    /// <para>
    /// <b>Sanoqlar SATR bo'lib ketadi.</b> Server ularni nom bo'yicha
    /// o'qiydi («SecondarySchool»). Raqam yuborish ham ishlardi, lekin
    /// o'shanda so'rovni jurnaldan o'qigan odam «education: 2» ning
    /// nimani bildirishini topa olmasdi.
    /// </para>
    /// </remarks>
    private static readonly JsonSerializerOptions RequestJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

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
    /// <para>
    /// FAQAT <c>Open</c>: yopilgan vakansiyaga ariza qabul qilish nomzodni
    /// ham, HR ni ham bekorga ovora qilardi.
    /// </para>
    /// <para>
    /// <c>excludeGeneralPool=true</c>: «Umumiy anketa» — tanlanadigan ish
    /// o'rni EMAS, vakansiyasiz kelgan anketa tushadigan joy. Filtr
    /// serverda, chunki qaysi vakansiya ekanini faqat server biladi
    /// (sozlamada turadi). Eski server bu parametrni shunchaki
    /// e'tiborsiz qoldiradi.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<OpenVacancy>> GetOpenVacanciesAsync(CancellationToken cancellationToken)
    {
        var page = await SendAsync<VacancyPage>(
                () => new HttpRequestMessage(
                    HttpMethod.Get,
                    new Uri(
                        "api/recruiting/vacancies?status=Open&excludeGeneralPool=true&page=1&pageSize=50",
                        UriKind.Relative)),
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
    /// Bu odam yangi anketa topshira oladimi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Qoida SERVERDA</b> (2026-09-08). Ilgari bot uni O'ZI
    /// yozardi: qidiruv so'roviga <c>status=InProgress</c> qo'shib.
    /// Endi qaror sozlamadagi «qayta ariza muddati» ga tayanadi va bot
    /// uni takrorlaganda HR ekranda muddatni o'zgartirgach, bot eski
    /// qoida bilan ishlab qolardi.
    /// </para>
    /// <para>
    /// <b>Javob kelmasa — O'TKAZAMIZ</b> (<c>null</c>). Bu ataylab:
    /// server javob bermayotganida odamni «siz allaqachon ariza
    /// topshirgansiz» degan YOLG'ON xabar bilan qaytarish eng yomon
    /// natija bo'lardi. Ariza yaratish qadamining o'zi baribir
    /// serverga boradi va u yerda yiqilsa, nomzod to'g'ri xato
    /// xabarini oladi.
    /// </para>
    /// </remarks>
    /// <summary>
    /// «Umumiy anketa» ochiqmi — «📋 Anketa to'ldirish» boshlanishidan OLDIN.
    /// </summary>
    /// <returns>
    /// <c>true</c>/<c>false</c>; server javob bermasa <c>null</c> — noma'lum.
    /// </returns>
    /// <remarks>
    /// Egasining qarori (2026-10-05): «Umumiy anketa» yopiq bo'lsa botda
    /// vakansiyasiz anketa topshirish ham yopiq. Javob noma'lum bo'lsa bot
    /// anketani TO'XTATMAYDI — qaror baribir serverda (yuborishda yopiq
    /// vakansiya rad etiladi va nomzodga aniq sabab aytiladi); vaqtincha
    /// uzilish tufayli ochiq anketani yopiq deb ko'rsatish yomonroq.
    /// </remarks>
    public async Task<bool?> IsGeneralPoolOpenAsync(CancellationToken cancellationToken)
    {
        var status = await SendAsync<GeneralPoolStatus>(
                () => new HttpRequestMessage(
                    HttpMethod.Get,
                    new Uri("api/recruiting/vacancies/general-pool", UriKind.Relative)),
                cancellationToken)
            .ConfigureAwait(false);

        return status?.IsOpen;
    }

    public async Task<ApplicationCheck?> CheckApplicationAsync(
        string phone,
        long chatId,
        CancellationToken cancellationToken)
    {
        var query = $"api/recruiting/candidates/application-check"
            + $"?phone={Uri.EscapeDataString(phone)}&telegramChatId={chatId}";

        return await SendAsync<ApplicationCheck>(
                () => new HttpRequestMessage(HttpMethod.Get, new Uri(query, UriKind.Relative)),
                cancellationToken)
            .ConfigureAwait(false);
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
    public async Task<bool> MarkRejectionNoticeSentAsync(
        Guid candidateId,
        CancellationToken cancellationToken,
        bool delivered = true)
    {
        // `delivered=false` — Telegram xabarni aniq rad etdi; yozuv baribir
        // navbatdan chiqadi (bitta urinish yetarli, 2026-10-05).
        var path = $"api/recruiting/candidates/{candidateId:D}/rejection-notice-sent"
            + (delivered ? string.Empty : "?delivered=false");

        return await SendNoContentAsync(
                () => new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Suhbat taklifi kutayotgan nomzodlarni oladi.
    /// </summary>
    /// <remarks>
    /// <see cref="GetPendingRejectionNoticesAsync"/> bilan bir xil qoida:
    /// odatda BO'SH ro'yxat qaytadi va so'rov halqaning har aylanishida
    /// takrorlanadi.
    /// </remarks>
    public async Task<IReadOnlyList<InterviewNotice>> GetPendingInterviewNoticesAsync(
        CancellationToken cancellationToken)
        => await SendAsync<List<InterviewNotice>>(
                () => new HttpRequestMessage(
                    HttpMethod.Get,
                    new Uri("api/recruiting/candidates/interview-notices", UriKind.Relative)),
                cancellationToken)
            .ConfigureAwait(false)
            ?? [];

    /// <summary>Suhbat taklifi yuborilganini belgilaydi.</summary>
    public async Task<bool> MarkInterviewNoticeSentAsync(
        Guid candidateId,
        CancellationToken cancellationToken,
        bool delivered = true)
    {
        // `delivered=false` — Telegram xabarni aniq rad etdi; yozuv baribir
        // navbatdan chiqadi (bitta urinish yetarli, 2026-10-05).
        var path = $"api/recruiting/candidates/{candidateId:D}/interview-notice-sent"
            + (delivered ? string.Empty : "?delivered=false");

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

    /// <summary>
    /// Nomzodni voronkaga kiritadi — anketasi bilan birga.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ <b>Nega <c>bool</c> emas.</b> Suratni yuborish uchun yaratilgan
    /// nomzodning <c>id</c> si kerak (<see cref="UploadCandidatePhotoAsync"/>),
    /// ya'ni tartib qat'iy: avval yozuv, keyin rasm. Nomzodni telefon
    /// raqami bo'yicha qayta izlash esa boshqa odamni topib qo'yish xavfi.
    /// </para>
    /// <para>
    /// <b>Rad etilganda — SABABI ham.</b> Ilgari bu yerda faqat <c>null</c>
    /// qaytardi va nomzod har qanday holatda «ariza yuborilmadi» ni olardi.
    /// Prod jurnali (2026-09-29) ko'rsatdiki, bu xabarlarning deyarli
    /// hammasi aniq, tuzatsa bo'ladigan sabablardan edi — yopilgan
    /// vakansiya yoki deploy paytida bir necha soniya javob bermagan API.
    /// </para>
    /// </remarks>
    /// <param name="registration">Tayyor ariza.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>Yaratilgan nomzod — yoki nega yaratilmagani (<see cref="CandidateRegistrationResult"/>).</returns>
    public async Task<CandidateRegistrationResult> RegisterCandidateAsync(
        CandidateRegistration registration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);

        var form = registration.Questionnaire;

        var body = new
        {
            // Bo'sh bo'lsa maydon so'rovga UMUMAN tushmaydi
            // (`RequestJson` izohi) va server arizani «Umumiy anketa»ga
            // qo'yadi.
            registration.VacancyId,

            registration.LastName,
            registration.FirstName,
            registration.MiddleName,
            registration.BirthDate,
            registration.Phone,

            /*
              Manzil va mutaxassislik nomzod kartasining O'Z maydonlari,
              anketa bloki emas: HR ularni ro'yxatda ham, qidiruvda ham
              ishlatadi.
            */
            registration.Address,
            registration.Speciality,

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
            telegramChatId = registration.ChatId,
            telegramLanguage = registration.Language,

            questionnaire = new
            {
                form.Education,
                form.MaritalStatus,
                form.RussianLevel,
                form.ExpectedSalary,
                form.ReferralSource,
                form.OfferAcceptedAt,
                form.WorkExperience,
                form.OtherLanguages,
                form.OfficeSkills,
                form.LastSalary,
                form.ExtraNote,
                form.HasDriverLicense,
                form.DriverLicenseCategories,
            },
        };

        var response = await SendCoreAsync<JsonElement>(
                () => new HttpRequestMessage(
                    HttpMethod.Post,
                    new Uri("api/recruiting/candidates", UriKind.Relative))
                {
                    Content = JsonContent.Create(body, options: RequestJson),
                },
                cancellationToken)
            .ConfigureAwait(false);

        var created = response.Value;

        return created.ValueKind == JsonValueKind.Object
            && created.TryGetProperty("id", out var id)
            && id.TryGetGuid(out var candidateId)
                ? new CandidateRegistrationResult(candidateId, null, false)
                : new CandidateRegistrationResult(null, response.ErrorCode, response.Transient);
    }

    /// <summary>
    /// Nomzodning suratini yuklaydi (18-qadam).
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Bu so'rov ARIZANI hal qilmaydi.</b> U ariza YARATILGANDAN
    /// keyin yuboriladi va bajarilmasligi mumkin — nomzod baribir
    /// voronkada, HR baribir unga qo'ng'iroq qiladi. Shu sababli
    /// chaqiruvchi xatoni jurnalga yozib, ishni davom ettiradi: rasm
    /// arizadan muhimroq emas.
    /// </para>
    /// <para>
    /// Baytlar <see cref="SendAsync{T}"/> dan o'tmaydi (u JSON kutadi),
    /// shu sababli so'rov qo'lda yig'iladi — lekin qayta kirish qoidasi
    /// bir xil: <c>401</c> kelsa bir marta qayta kiriladi va konvert
    /// QAYTADAN yasaladi (yuborilgan <c>MultipartFormDataContent</c> ni
    /// ikkinchi marta jo'natib bo'lmaydi).
    /// </para>
    /// </remarks>
    /// <param name="candidateId">Yaratilgan nomzod.</param>
    /// <param name="photo">JPEG baytlari — Telegram'dan yuklab olingan.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    /// <returns>Rasm yetib bordimi.</returns>
    public async Task<bool> UploadCandidatePhotoAsync(
        Guid candidateId,
        byte[] photo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(photo);

        var path = $"api/recruiting/candidates/{candidateId:D}/photo";

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var file = new ByteArrayContent(photo);

                file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

                // Maydon nomi «file» — server aynan shuni kutadi
                // (xodim rasmi bilan bir xil quvur).
                content.Add(file, "file", "photo.jpg");

                using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative))
                {
                    Content = content,
                };

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
                    LogApiError((int)response.StatusCode, "nomzod surati yuklanmadi");
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
        => (await SendCoreAsync<T>(request, cancellationToken).ConfigureAwait(false)).Value;

    /// <summary>
    /// So'rov yuboradi va natijani XATO SABABI bilan qaytaradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ko'p chaqiruvga faqat qiymat kerak (<see cref="SendAsync{T}"/>).
    /// Ariza esa sababni biladi: server rad etdimi (qaysi kod bilan) yoki
    /// umuman javob bermadimi — bot ularga boshqa-boshqa javob beradi.
    /// </para>
    /// <para>
    /// <b>«Vaqtincha» faqat so'rov serverga YETMAGANI aniq bo'lganda.</b>
    /// Ulanib bo'lmadi yoki nom topilmadi — so'rov jo'natilmagan. Javob
    /// kutish muddati o'tgani esa bunday emas: server arizani yozib
    /// ulgurgan bo'lishi mumkin va qayta yuborish ikkinchi nomzod
    /// yaratardi. 503 — server ataylab «hozir emas» degani.
    /// </para>
    /// </remarks>
    private async Task<ApiResponse<T>> SendCoreAsync<T>(
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
                        return ApiResponse<T>.Failed(null, transient: false);
                    }

                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var detail = await response.Content
                        .ReadAsStringAsync(cancellationToken)
                        .ConfigureAwait(false);

                    LogApiError((int)response.StatusCode, detail.Length > 300 ? detail[..300] : detail);

                    return ApiResponse<T>.Failed(
                        ProblemCode(detail),
                        transient: response.StatusCode == HttpStatusCode.ServiceUnavailable);
                }

                var value = await response.Content
                    .ReadFromJsonAsync<T>(Json, cancellationToken)
                    .ConfigureAwait(false);

                return new ApiResponse<T>(value, null, false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex)
                when (ex.HttpRequestError is HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError)
            {
                LogRequestFailed(ex);
                return ApiResponse<T>.Failed(null, transient: true);
            }
            catch (Exception ex)
            {
                LogRequestFailed(ex);
                return ApiResponse<T>.Failed(null, transient: false);
            }
        }

        return ApiResponse<T>.Failed(null, transient: false);
    }

    /// <summary>ProblemDetails'dagi xato kodi (<c>title</c>). Bo'lmasa — <c>null</c>.</summary>
    private static string? ProblemCode(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("title", out var title)
                && title.ValueKind == JsonValueKind.String
                    ? title.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private readonly record struct ApiResponse<T>(T? Value, string? ErrorCode, bool Transient)
    {
        public static ApiResponse<T> Failed(string? errorCode, bool transient) => new(default, errorCode, transient);
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

    private sealed record GeneralPoolStatus(Guid? VacancyId, bool? IsOpen);

    private sealed record VacancyPage(List<VacancyItem>? Items);

    private sealed record VacancyItem(Guid Id, string? Title);

    private sealed record CandidatePage(List<CandidateItem>? Items);

    private sealed record CandidateItem(
        Guid Id,
        DateTimeOffset CreatedAt,
        string? CurrentStage,
        string? Vacancy);
}
