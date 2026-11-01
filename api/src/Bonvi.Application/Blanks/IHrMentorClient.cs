using Bonvi.Domain.Common;

namespace Bonvi.Application.Blanks;

/// <summary>
/// HRMentor ga ulanish: manzil va faqat <c>forms:sync</c> ruxsatli kalit (SPEC-blankalar-hrmentor §2).
/// </summary>
/// <param name="BaseUrl">HRMentor manzili, oxirida «/» siz (<c>https://docs.bonvi.uz</c>).</param>
/// <param name="ApiKey">API kaliti — hech qachon jurnalga yozilmaydi.</param>
public sealed record HrMentorConnection(Uri BaseUrl, string ApiKey)
{
    /// <summary>Ikkala sozlamadan ulanish; biri bo'sh yoki yaroqsiz bo'lsa <c>null</c> (ulanish o'chiq).</summary>
    public static HrMentorConnection? From(string? baseUrl, string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        if (!Uri.TryCreate(baseUrl.Trim().TrimEnd('/'), UriKind.Absolute, out var uri)
            || !(uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && IsSameMachine(uri))))
        {
            return null;
        }

        return new HrMentorConnection(uri, apiKey.Trim());
    }

    /// <summary>
    /// Shifrsiz http faqat shu mashina ichida: kalit tarmoqqa ochiq chiqmaydi
    /// (<c>SystemSettingKeys.HrMentorUrlPattern</c> bilan bir qoida).
    /// </summary>
    private static bool IsSameMachine(Uri uri) =>
        uri.IsLoopback || string.Equals(uri.Host, "host.docker.internal", StringComparison.OrdinalIgnoreCase);

    /// <summary>Kalitni yashiradi: yozuv tasodifan jurnalga tushsa ham sir chiqmaydi.</summary>
    public override string ToString() => $"HrMentorConnection {{ BaseUrl = {BaseUrl} }}";
}

/// <summary>HRMentor ro'yxatidagi bitta blanka (<c>GET /api/v1/blanks</c>).</summary>
public sealed record HrMentorBlankItem(
    Guid DocumentId,
    string? Code,
    string Title,
    Guid VersionId,
    int VersionNo,
    DateTimeOffset? PublishedAt,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256);

/// <summary>
/// HRMentor da shu platformaga biriktirilgan hujjatlar (<c>GET /api/v1/platform/documents</c>,
/// SPEC-hujjat-manbai §2). Har biri blanka ro'yxatidagi bilan bir xil fayl ma'lumotlari bilan.
/// </summary>
/// <param name="PlatformId">HRMentor dagi platforma identifikatori.</param>
/// <param name="PlatformName">HRMentor dagi platforma nomi («hr.bonvi.uz»).</param>
/// <param name="Documents">Biriktirilgan hujjatlar va joriy versiyalarining fayl ma'lumotlari.</param>
public sealed record HrMentorPlatformDocuments(
    Guid PlatformId,
    string PlatformName,
    IReadOnlyList<HrMentorBlankItem> Documents);

/// <summary>Platformada turgan bitta hujjat versiyasi (<c>PUT /api/v1/platform/holdings</c>).</summary>
public sealed record HrMentorHolding(Guid DocumentId, Guid VersionId);

/// <summary>HRMentor qayd etgani va e'tiborsiz qoldirgani (boshqa hujjat yoki biriktirilmagan).</summary>
public sealed record HrMentorHoldingsReport(int Recorded, int Ignored);

/// <summary>
/// HRMentor ning HTTP mijozi (SPEC-blankalar-hrmentor §3). Har bir chaqiruv ulanishni o'zi
/// oladi: manzil yoki kalit Sozlamalarda almashsa, keyingi yurish qayta ishga tushirishsiz
/// yangisini ishlatadi.
/// </summary>
/// <remarks>
/// Hech bir usul istisno otmaydi (bekor qilishdan tashqari): tarmoq, vaqt, HTTP holati va
/// buzuq javob <see cref="HrMentorErrors"/> dagi KOD bo'lib qaytadi — sinxronlash shu kodni
/// «oxirgi xato» sifatida yozadi va qolgan qadamlarni davom ettiradi.
/// </remarks>
public interface IHrMentorClient
{
    /// <summary><c>PUT /api/v1/digital-forms/{code}/catalog</c> — katalogni yuboradi.</summary>
    Task<Result> PushCatalogAsync(
        HrMentorConnection connection,
        string formCode,
        string catalogJson,
        CancellationToken cancellationToken);

    /// <summary>
    /// <c>GET /api/v1/digital-forms/{code}/texts</c> — HR o'zgarishlarining xom JSON i;
    /// 404 (katalog hali yuborilmagan) — <c>null</c>.
    /// </summary>
    Task<Result<string?>> GetServedTextsAsync(
        HrMentorConnection connection,
        string formCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// <c>PUT /api/v1/digital-forms/{code}/profile</c> — bog'lanishlar va urug'ni yuboradi
    /// (SPEC-anketa-konstruktori §3, §4). Katalogdan keyin: undan oldin HRMentor 404 qaytaradi.
    /// </summary>
    Task<Result> PushProfileAsync(
        HrMentorConnection connection,
        string formCode,
        string profileJson,
        CancellationToken cancellationToken);

    /// <summary>
    /// <c>GET /api/v1/digital-forms/{code}/definition</c> — e'lon qilingan ta'rifning xom JSON i;
    /// 404 (katalog hali yuborilmagan) — <c>null</c>.
    /// </summary>
    Task<Result<string?>> GetPublishedDefinitionAsync(
        HrMentorConnection connection,
        string formCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// <c>GET /api/v1/digital-forms/{code}/draft</c> — konstruktordagi qoralama (ko'rinish uchun);
    /// 404 — <c>null</c>.
    /// </summary>
    Task<Result<string?>> GetDraftDefinitionAsync(
        HrMentorConnection connection,
        string formCode,
        CancellationToken cancellationToken);

    /// <summary><c>GET /api/v1/blanks</c> — blankalar va joriy versiyalarining fayl ma'lumotlari.</summary>
    Task<Result<IReadOnlyList<HrMentorBlankItem>>> ListBlanksAsync(
        HrMentorConnection connection,
        CancellationToken cancellationToken);

    /// <summary>
    /// <c>GET /api/v1/blanks/{id}/file</c> — joriy versiyaning fayli; <paramref name="maxBytes"/>
    /// dan katta bo'lsa <see cref="HrMentorErrors.BlankTooLarge"/>.
    /// </summary>
    Task<Result<byte[]>> DownloadBlankAsync(
        HrMentorConnection connection,
        Guid documentId,
        long maxBytes,
        CancellationToken cancellationToken);

    /// <summary>
    /// <c>GET /api/v1/platform/documents</c> — shu platformaga biriktirilgan hujjatlar
    /// (SPEC-hujjat-manbai §2). Kalit hali platformaga bog'lanmagan (403
    /// <c>platforms.key_not_bound</c>), kalitda <c>platform:sync</c> yo'q (403
    /// <c>app.permission_denied</c>) yoki HRMentor bu yo'lni bilmaydi (404) —
    /// <see cref="HrMentorErrors.PlatformNotReady"/>: sinxronlash eski ro'yxatga o'tadi.
    /// Platforma o'chirilgan — <see cref="HrMentorErrors.PlatformOff"/>.
    /// </summary>
    Task<Result<HrMentorPlatformDocuments>> ListPlatformDocumentsAsync(
        HrMentorConnection connection,
        CancellationToken cancellationToken);

    /// <summary>
    /// <c>GET /api/v1/platform/documents/{id}/file</c> — platforma hujjatining joriy fayli;
    /// <paramref name="maxBytes"/> dan katta bo'lsa <see cref="HrMentorErrors.BlankTooLarge"/>.
    /// </summary>
    Task<Result<byte[]>> DownloadPlatformDocumentAsync(
        HrMentorConnection connection,
        Guid documentId,
        long maxBytes,
        CancellationToken cancellationToken);

    /// <summary>
    /// <c>PUT /api/v1/platform/holdings</c> — hozir qaysi hujjatning qaysi versiyasi bizda
    /// turgani, TO'LIQ to'plam (HRMentor panelida «Yangi / Eski versiya / Olmagan»).
    /// </summary>
    Task<Result<HrMentorHoldingsReport>> ReportHoldingsAsync(
        HrMentorConnection connection,
        IReadOnlyList<HrMentorHolding> holdings,
        CancellationToken cancellationToken);
}

/// <summary>
/// HRMentor bilan aloqa xatolari. Kod <c>hrmentor.lastError</c> sozlamasiga va «Blankalar»
/// sahifasiga tushadi — shu sababli faqat KOD, hech qachon javob tanasi yoki sir.
/// </summary>
public static class HrMentorErrors
{
    /// <summary>Manzil yoki kalit qo'yilmagan.</summary>
    public static readonly Error NotConnected =
        Error.Validation("hrmentor.not_connected", "HRMentor ulanmagan: Sozlamalar → HRMentor.");

    /// <summary>Tarmoq xatosi — server topilmadi yoki ulanish uzildi.</summary>
    public static readonly Error Unreachable =
        new("hrmentor.unreachable", "HRMentor ga ulanib bo'lmadi.");

    /// <summary>Javob vaqtida kelmadi.</summary>
    public static readonly Error Timeout =
        new("hrmentor.timeout", "HRMentor vaqtida javob bermadi.");

    /// <summary>401 — kalit noto'g'ri yoki bekor qilingan.</summary>
    public static readonly Error Unauthorized =
        new("hrmentor.unauthorized", "HRMentor kalitni qabul qilmadi.");

    /// <summary>403 — kalitda <c>forms:sync</c> ruxsati yo'q.</summary>
    public static readonly Error Forbidden =
        new("hrmentor.forbidden", "HRMentor kalitida «forms:sync» ruxsati yo'q.");

    /// <summary>Kutilmagan 404.</summary>
    public static readonly Error NotFound =
        new("hrmentor.not_found", "HRMentor da so'ralgan narsa topilmadi.");

    /// <summary>Boshqa 4xx — so'rov rad etildi (masalan, katalog 422).</summary>
    public static readonly Error Rejected =
        new("hrmentor.rejected", "HRMentor so'rovni rad etdi.");

    /// <summary>5xx — HRMentor ning o'zida xato.</summary>
    public static readonly Error ServerError =
        new("hrmentor.server_error", "HRMentor ichki xato qaytardi.");

    /// <summary>Javobni o'qib bo'lmadi, u juda katta yoki boshqa joyga yo'naltirildi.</summary>
    public static readonly Error BadResponse =
        new("hrmentor.bad_response", "HRMentor javobi tushunarsiz.");

    /// <summary>Blanka fayli chegaradan katta.</summary>
    public static readonly Error BlankTooLarge =
        new("hrmentor.blank_too_large", "Blanka fayli juda katta.");

    /// <summary>Yuklab olingan fayl ro'yxatdagi SHA-256 ga mos emas.</summary>
    public static readonly Error BlankChecksum =
        new("hrmentor.blank_checksum", "Blanka fayli ro'yxatdagisiga mos kelmadi.");

    /// <summary>HR matnlari katalogga mos kelmadi (reviziya, shakl).</summary>
    public static readonly Error TextsInvalid =
        new("hrmentor.texts_invalid", "HRMentor matnlari katalogga mos kelmadi.");

    /// <summary>
    /// HRMentor dagi anketa ta'rifi qabul qilinmadi (SPEC-anketa-konstruktori §5.2): sarlavha,
    /// SHA-256, hajm, BonviHR tekshiruvi yoki bir xil versiya boshqa mazmun bilan.
    /// </summary>
    public static readonly Error DefinitionInvalid =
        new("hrmentor.definition_invalid", "HRMentor dagi anketa ta'rifi qabul qilinmadi.");

    /// <summary>
    /// Kalit hali HRMentor da platformaga bog'lanmagan yoki unda <c>platform:sync</c> yo'q, yoki
    /// HRMentor platformalarni bilmaydi (SPEC-hujjat-manbai §2). «Oxirgi xato» ga yozilmaydi:
    /// sinxronlash shu belgi bilan eski ro'yxatga o'tadi.
    /// </summary>
    public static readonly Error PlatformNotReady =
        new("hrmentor.platform_not_ready", "HRMentor kaliti hali platformaga bog'lanmagan.");

    /// <summary>HRMentor da bu platforma o'chirilgan — blankalar olinmaydi, oxirgi nusxa qoladi.</summary>
    public static readonly Error PlatformOff =
        new("hrmentor.platform_off", "HRMentor da bu platforma o'chirilgan.");

    /// <summary>Boshqa yangilash hali tugamagan.</summary>
    public static readonly Error Busy =
        Error.Conflict("hrmentor.busy", "Yangilash allaqachon ketyapti — birozdan keyin qayta urining.");
}
