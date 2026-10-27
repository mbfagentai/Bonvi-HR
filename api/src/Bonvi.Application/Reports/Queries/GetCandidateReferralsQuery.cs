using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Common.Lists;
using Bonvi.Application.Reports.Abstractions;
using Bonvi.Application.Reports.Dtos;
using Bonvi.Application.Reports.Export;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Reports.Queries;

/// <summary>«Nomzodlar qayerdan bilgan» hisoboti (SPEC-qayerdan-bilgan §7).</summary>
/// <param name="From">Ariza sanasi — boshi (kiradi, Toshkent kuni); <c>null</c> — ochiq chegara.</param>
/// <param name="To">Ariza sanasi — oxiri (kiradi); <c>null</c> — ochiq chegara.</param>
/// <param name="VacancyId">Faqat shu vakansiyaning nomzodlari; <c>null</c> — hammasi.</param>
public sealed record GetCandidateReferralsQuery(DateOnly? From = null, DateOnly? To = null, Guid? VacancyId = null);

/// <summary>
/// Kanal bo'yicha nomzodlar va ishga olinganlar soni.
/// </summary>
/// <remarks>
/// <para>
/// <b>Kogorta — davrda ARIZA BERGAN nomzodlar</b> (<c>created_at</c>, Toshkent kuni, ro'yxatlardagi
/// «dan–gacha» qoidasi — <see cref="ListDateRange"/>). «Ishga olingan» — ulardan holati
/// <see cref="CandidateStatus.Hired"/> bo'lganlar, bugungi holat bo'yicha: voronka hisoboti
/// (MCP <c>recruiting_funnel</c>) ham aynan shunday sanaydi, ikkinchi ta'rif o'ylab topilmadi.
/// </para>
/// <para>
/// <b>Excel kengash arxivi kirmaydi</b> — voronka hisobotidagi sukut qoida bilan bir xil: arxivning
/// «ariza sanasi» — ko'chirilgan kun, kanali esa umuman yo'q; u bitta kunga minglab
/// «ko'rsatilmagan» qo'shib, hisobotni ma'nosiz qilardi.
/// </para>
/// <para>
/// Qatorlar nomzodlar soni bo'yicha kamayish tartibida (teng bo'lsa — nom bo'yicha);
/// faolsizlantirilgan kanal qatori bo'lsa chiqadi, kanalsiz nomzodlar bitta
/// «Ko'rsatilmagan» qatorida (<c>channelId = null</c>), u ham faqat bo'sh bo'lmasa.
/// </para>
/// </remarks>
public sealed class GetCandidateReferralsQueryHandler
{
    private readonly IReportsReadModel _reports;

    public GetCandidateReferralsQueryHandler(IReportsReadModel reports) => _reports = reports;

    public async Task<Result<CandidateReferralReportDto>> HandleAsync(
        GetCandidateReferralsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var range = ListDateRange.Validate(query.From, query.To);
        if (range.IsFailure)
        {
            return range.Error;
        }

        var buckets = await _reports
            .GetCandidateReferralsAsync(
                new CandidateReferralFilter(
                    ListDateRange.StartUtc(query.From),
                    ListDateRange.EndUtcExclusive(query.To),
                    query.VacancyId),
                cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<CandidateReferralRowDto> rows =
        [
            .. buckets
                .Where(bucket => bucket.Candidates > 0)
                .OrderByDescending(bucket => bucket.Candidates)

                // «Ko'rsatilmagan» teng sonli kanallardan keyin; nomlar esa alifbo tartibida
                .ThenBy(bucket => bucket.ChannelId is null)
                .ThenBy(bucket => bucket.ChannelName, StringComparer.CurrentCulture)
                .Select(bucket => new CandidateReferralRowDto(
                    bucket.ChannelId,
                    bucket.ChannelName,
                    bucket.IsActive,
                    bucket.Candidates,
                    bucket.Hired)),
        ];

        return new CandidateReferralReportDto(
            rows,
            rows.Sum(row => row.Candidates),
            rows.Sum(row => row.Hired));
    }
}

/// <summary>
/// «Nomzodlar qayerdan bilgan» hisobotini Excel faylga chiqaradi — ekrandagi davr va vakansiya bilan.
/// </summary>
/// <remarks>
/// <para>
/// Qatorlar hisobot handler'ining O'ZIDAN (<see cref="GetCandidateReferralsQueryHandler"/>) —
/// ro'yxat eksportlaridagi qoida (<see cref="ExportCandidatesQueryHandler"/> izohi): alohida
/// hisob yozilsa, fayl ekrandan ajralib ketardi. Qatorlar soni kanallar soni bilan cheklangan,
/// ya'ni <c>eksport.juda_kop</c> chegarasi bu yerda kerak emas.
/// </para>
/// <para>
/// Ulush («ishga olingan %») son ustuni: HR uni Excel'da saralaydi. Oxirgi qator — «Jami».
/// Har bir eksport audit jurnaliga yoziladi (nomzod bo'limi ostida — sonlar nomzodlardan).
/// </para>
/// </remarks>
public sealed class ExportCandidateReferralsQueryHandler
{
    private const string Slug = "nomzodlar_qayerdan_bilgan";
    private const string SheetName = "Qayerdan bilgan";

    /// <summary>Kanalsiz nomzodlar qatori — ekrandagi yorliq bilan bir xil.</summary>
    private const string MissingLabel = "Ko'rsatilmagan";

    private const string TotalLabel = "Jami";

    private static readonly IReadOnlyList<ExcelColumn<ReferralSheetRow>> Columns =
    [
        new("Qayerdan bilgan", row => row.Channel),
        new("Nomzodlar", row => row.Candidates, ExcelCellKind.WholeNumber),
        new("Ishga olingan", row => row.Hired, ExcelCellKind.WholeNumber),
        new("Ishga olingan, %", row => row.HiredPercent, ExcelCellKind.FractionalNumber),
    ];

    private readonly GetCandidateReferralsQueryHandler _report;
    private readonly IExcelTableWriter _writer;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ExportCandidateReferralsQueryHandler(
        GetCandidateReferralsQueryHandler report,
        IExcelTableWriter writer,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _report = report;
        _writer = writer;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<ExcelExportFile>> HandleAsync(
        GetCandidateReferralsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var report = await _report.HandleAsync(query, cancellationToken).ConfigureAwait(false);
        if (report.IsFailure)
        {
            return report.Error;
        }

        var value = report.Value;

        IReadOnlyList<ReferralSheetRow> rows =
        [
            .. value.Rows.Select(row => new ReferralSheetRow(
                Name(row),
                row.Candidates,
                row.Hired,
                Percent(row.Hired, row.Candidates))),
            new ReferralSheetRow(
                TotalLabel,
                value.TotalCandidates,
                value.TotalHired,
                Percent(value.TotalHired, value.TotalCandidates)),
        ];

        var content = _writer.Write(SheetName, Columns, rows);

        _auditLogger.Record(
            AuditAction.Export,
            nameof(Candidate),
            entityId: null,
            newValues: JsonSerializer.Serialize(
                new ExportAuditDetail(new ExportAuditFilters(Slug, query.From, query.To, query.VacancyId), value.Rows.Count),
                ExportAuditJson));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ExcelExportFile(content, ExcelExport.FileName(Slug, _clock.Today));
    }

    /// <summary>Kanal nomi; faolsizlantirilgani belgi bilan — ekrandagidek.</summary>
    private static string Name(CandidateReferralRowDto row)
        => row.ChannelName is not { } name
            ? MissingLabel
            : row.IsActive ? name : $"{name} (faol emas)";

    /// <summary>Bir xona aniqlikdagi ulush; nomzod yo'q bo'lsa — bo'sh katak.</summary>
    private static decimal? Percent(int part, int whole)
        => whole == 0 ? null : Math.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero);

    /// <summary>Fayl qatori: kanal, nomzodlar, ishga olinganlar, ulush.</summary>
    private sealed record ReferralSheetRow(string Channel, int Candidates, int Hired, decimal? HiredPercent);

    /// <summary>Bo'sh filtrlar yozilmaydi — tafsilot qisqa va o'qilarli bo'lsin.</summary>
    private static readonly JsonSerializerOptions ExportAuditJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Eksport hodisasining tafsiloti.</summary>
    /// <param name="Filters">Qo'llangan filtrlar.</param>
    /// <param name="Rows">Kanal qatorlari soni («Jami» siz).</param>
    private sealed record ExportAuditDetail(ExportAuditFilters Filters, int Rows);

    /// <summary>Filtrlar va qaysi hisobot (nomzodlar ro'yxati eksportidan ajratish uchun).</summary>
    private sealed record ExportAuditFilters(string Report, DateOnly? From, DateOnly? To, Guid? VacancyId);
}
