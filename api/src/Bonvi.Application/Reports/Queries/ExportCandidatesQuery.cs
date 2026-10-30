using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Recruiting.Candidates;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Application.Recruiting.Referrals;
using Bonvi.Application.Reports.Export;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Reports.Queries;

/// <summary>
/// «Nomzodlar» ro'yxatini (rad etilganlar, rezerv va qora ro'yxatning
/// «Nomzodlar» bo'limi bilan) EKRANDAGI filtr va saralash bilan Excel faylga
/// eksport qiladi (<c>SPEC-excel-eksport.md</c> §2 #1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Kirish — AYNAN <see cref="SearchCandidatesQuery"/>, qatorlar — ro'yxat
/// handler'ining O'ZIDAN.</b> <see cref="SearchCandidatesQueryHandler"/> da
/// alohida filtr quruvchi yo'q: holat va manba tekshiruvi, sana oralig'ining
/// qaysi sanaga tushishi va «bugun» shu handler ichida. Uni chaqirish —
/// ro'yxatga qo'shilgan har bir yangi filtr eksportga ham o'zi o'tishining
/// yagona kafolati; nusxa yozilsa, ikkalasi vaqt o'tib ajralib ketardi.
/// </para>
/// <para>
/// Sahifalanmaydi: mos yozuvlarning HAMMASI bitta faylda. Chegaradan oshsa
/// fayl jimgina qirqilmaydi — <c>eksport.juda_kop</c> qaytadi.
/// </para>
/// <para>
/// Ustunlar — bir nechta ekranning birlashmasi: «Nomzodlar» jadvali, keyin
/// rezerv / qora ro'yxat / rad etilganlar bo'limlarida kerak bo'ladigan
/// keyingi aloqa, ro'yxatga tushgan sana va sabab. Bitta endpoint to'rtta
/// ekranga xizmat qiladi, shu sababli fayl ham bitta shaklda.
/// </para>
/// </remarks>
public sealed class ExportCandidatesQueryHandler
{
    private const string SheetName = "Nomzodlar";

    private static readonly IReadOnlyList<ExcelColumn<CandidateListItemDto>> Columns =
    [
        // Ekrandagi ustunlar — o'sha tartibda
        new("F.I.Sh.", row => row.FullName),
        new("Vakansiya", row => row.Vacancy),
        new("Bosqich", row => row.CurrentStage),
        new("Holati", row => StatusLabel(row.Status)),
        new("Manba", row => SourceLabel(row.Source)),
        new("Telefon raqami", row => PhoneText(row.Phone)),
        new("Kiritilgan", row => row.CreatedAt, ExcelCellKind.DateTime),

        // Rezerv, qora ro'yxat va rad etilganlar bo'limlarining ustunlari
        new("Keyingi aloqa", row => row.NextContactDate, ExcelCellKind.Date),
        new("Yopilgan sana", row => row.ClosedAt, ExcelCellKind.Date),
        new("Sabab", row => row.ClosingNote, ExcelCellKind.LongText),

        // «Qayerdan bilgan» (SPEC-qayerdan-bilgan §6) — ekranda ustun emas (qator bir qatorli),
        // kartada esa bor; shakl kartadagidek: «Tanishlar orqali — Rustamov Bahodir». OXIRIDA:
        // mavjud ustunlarning o'rni o'zgarmasin (HR fayllarni o'z jadvallariga ko'chiradi).
        new("Qayerdan bilgan", row => ReferralChannelChoice.Label(row.ReferralChannelName, row.ReferralDetail)),

        // Mavjud kartaga keyin qo'shilgan onlayn anketa (2026-10-30) — shu sabab bilan OXIRIDA
        new("Anketa to'ldirgan", row => row.ReappliedAt, ExcelCellKind.DateTime),
    ];

    private readonly SearchCandidatesQueryHandler _list;
    private readonly IExcelTableWriter _writer;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ExportCandidatesQueryHandler(
        SearchCandidatesQueryHandler list,
        IExcelTableWriter writer,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _list = list;
        _writer = writer;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<ExcelExportFile>> HandleAsync(
        SearchCandidatesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // `MaxRows + 1`: chegaradan oshganini bilish uchun bittasi ortiqcha yetarli.
        // Ro'yxatning `MaxPageSize` i bundan katta bo'lishi SHART, aks holda fayl
        // jimgina qirqilardi — `ListExportRowLimitTests` buni qo'riqlaydi.
        var found = await _list
            .HandleAsync(query with { Page = 1, PageSize = ExcelExport.MaxRows + 1 }, cancellationToken)
            .ConfigureAwait(false);

        if (found.IsFailure)
        {
            return found.Error;
        }

        if (found.Value.Total > ExcelExport.MaxRows)
        {
            return ExcelExport.TooMany(found.Value.Total);
        }

        var rows = found.Value.Items;
        var content = _writer.Write(SheetName, Columns, rows);

        _auditLogger.Record(
            AuditAction.Export,
            nameof(Candidate),
            entityId: null,
            newValues: JsonSerializer.Serialize(
                new ExportAuditDetail(ExportAuditFilters.Of(query), rows.Count),
                ExportAuditJson));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ExcelExportFile(content, ExcelExport.FileName(Slug(query), _clock.Today));
    }

    /// <summary>
    /// Fayl nomidagi qism — qaysi ekrandan yuklangani: Rezerv va Qora ro'yxat
    /// nomzodlari bir kunda yuklansa, brauzer ikkinchisiga «(1)» qo'shmasin.
    /// </summary>
    private static string Slug(SearchCandidatesQuery query) =>
        string.Equals(query.Status, nameof(CandidateStatus.Reserve), StringComparison.OrdinalIgnoreCase)
            ? "rezerv_nomzodlar"
            : string.Equals(query.Status, nameof(CandidateStatus.Blacklisted), StringComparison.OrdinalIgnoreCase)
                ? "qora_royxat_nomzodlar"
                : query.Rejected
                    ? "rad_etilgan_nomzodlar"
                    : "nomzodlar";

    /// <summary>Holat — ekrandagi yorliq bilan bir xil.</summary>
    private static string StatusLabel(CandidateStatus status) => status switch
    {
        CandidateStatus.InProgress => "Jarayonda",
        CandidateStatus.Rejected => "Rad etilgan",
        CandidateStatus.Hired => "Ishga olingan",
        CandidateStatus.Withdrawn => "O'zi voz kechgan",
        CandidateStatus.Reserve => "Rezervda",
        CandidateStatus.Blacklisted => "Qora ro'yxatda",
        _ => status.ToString(),
    };

    /// <summary>Manba — ekrandagi yorliq bilan bir xil.</summary>
    private static string SourceLabel(CandidateSource source) => source switch
    {
        CandidateSource.Manual => "Qo'lda",
        CandidateSource.TelegramBot => "Telegram bot",
        CandidateSource.Excel => "Excel arxivi",
        CandidateSource.OnlineForm => "Onlayn anketa",
        _ => source.ToString(),
    };

    /// <summary>
    /// Telefon ekrandagidek: <c>+998 90 508 88 88</c>.
    /// </summary>
    /// <remarks>
    /// Faqat to'liq normallashtirilgan raqam formatlanadi. Boshqa shakldagi
    /// qiymat (eski ko'chirilgan yozuv) O'ZICHA yoziladi — formatlash
    /// <c>Substring</c> bilan ishlaydi va qisqa satrda butun faylni yiqitardi.
    /// </remarks>
    private static string? PhoneText(string? phone)
        => phone is { Length: 13 } && phone.StartsWith("+998", StringComparison.Ordinal)
            ? PhoneNumber.FromTrusted(phone).Formatted
            : phone;

    /// <summary>Bo'sh filtrlar yozilmaydi — tafsilot qisqa va o'qilarli bo'lsin.</summary>
    private static readonly JsonSerializerOptions ExportAuditJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Eksport hodisasining tafsiloti.</summary>
    /// <param name="Filters">Qo'llangan filtrlar (qidiruv matnisiz).</param>
    /// <param name="Rows">Fayldagi qatorlar soni.</param>
    private sealed record ExportAuditDetail(ExportAuditFilters Filters, int Rows);

    /// <summary>Filtrlar — shaxsiy ma'lumotsiz. Bayroqlar faqat yoqilgan bo'lsa yoziladi.</summary>
    private sealed record ExportAuditFilters(
        bool? HasSearch,
        Guid? VacancyId,
        Guid? StageId,
        string? Status,
        string? Source,
        string? Sort,
        bool? IncludeArchive,
        bool? ContactDue,
        bool? Rejected,
        bool? IncludeRejected,
        DateOnly? From,
        DateOnly? To,
        bool? ByClosedDate,
        Guid? ReferralChannelId,
        bool? ReferralMissing,
        bool? Reapplied)
    {
        public static ExportAuditFilters Of(SearchCandidatesQuery query) => new(
            string.IsNullOrWhiteSpace(query.Search) ? null : true,
            query.VacancyId,
            query.StageId,
            string.IsNullOrWhiteSpace(query.Status) ? null : query.Status,
            string.IsNullOrWhiteSpace(query.Source) ? null : query.Source,
            string.IsNullOrWhiteSpace(query.Sort) ? null : query.Sort,
            OnlyTrue(query.IncludeArchive),
            OnlyTrue(query.ContactDue),
            OnlyTrue(query.Rejected),
            OnlyTrue(query.IncludeRejected),
            query.From,
            query.To,
            OnlyTrue(query.ByClosedDate),
            query.ReferralChannelId,
            OnlyTrue(query.ReferralMissing),
            OnlyTrue(query.Reapplied));

        private static bool? OnlyTrue(bool flag) => flag ? true : null;
    }
}
