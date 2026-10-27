using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Application.Recruiting.Vacancies;
using Bonvi.Application.Reports.Export;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Reports.Queries;

/// <summary>
/// «Vakansiyalar» ro'yxatini EKRANDAGI filtr va saralash bilan Excel faylga
/// eksport qiladi (<c>SPEC-excel-eksport.md</c> §2 #2).
/// </summary>
/// <remarks>
/// <para>
/// Qatorlar ro'yxat handler'ining O'ZIDAN (<see cref="ListVacanciesQueryHandler"/>)
/// — sababi <see cref="ExportCandidatesQueryHandler"/> izohida: alohida filtr
/// quruvchi yo'q, nusxa esa vaqt o'tib ro'yxatdan ajralib ketardi.
/// </para>
/// <para>
/// Ustunlar — faqat ekrandagilar. «To'ldirildi» katagi ekranda «2 / 3» —
/// faylda u IKKITA son ustuni: ishga olinganlar va o'rinlar soni. Matn
/// bo'lib qolsa, HR Excel'da uni yig'indilay olmasdi.
/// </para>
/// </remarks>
public sealed class ExportVacanciesQueryHandler
{
    private const string Slug = "vakansiyalar";
    private const string SheetName = "Vakansiyalar";

    private static readonly IReadOnlyList<ExcelColumn<VacancyDto>> Columns =
    [
        new("Vakansiya nomi", row => row.Title),
        new("Holati", row => StatusLabel(row.Status)),
        new("To'ldirildi", row => row.HiredCount, ExcelCellKind.WholeNumber),
        new("O'rinlar soni", row => row.Openings, ExcelCellKind.WholeNumber),
        new("Nomzodlar", row => row.CandidateCount, ExcelCellKind.WholeNumber),
        new("Ochilgan", row => row.OpenedAt, ExcelCellKind.Date),
    ];

    private readonly ListVacanciesQueryHandler _list;
    private readonly IExcelTableWriter _writer;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ExportVacanciesQueryHandler(
        ListVacanciesQueryHandler list,
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
        ListVacanciesQuery query,
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
            nameof(Vacancy),
            entityId: null,
            newValues: JsonSerializer.Serialize(
                new ExportAuditDetail(ExportAuditFilters.Of(query), rows.Count),
                ExportAuditJson));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ExcelExportFile(content, ExcelExport.FileName(Slug, _clock.Today));
    }

    /// <summary>Holat — ekrandagi yorliq bilan bir xil.</summary>
    private static string StatusLabel(VacancyStatus status) => status switch
    {
        VacancyStatus.Open => "Ochiq",
        VacancyStatus.Closed => "Yopilgan",
        _ => status.ToString(),
    };

    /// <summary>Bo'sh filtrlar yozilmaydi — tafsilot qisqa va o'qilarli bo'lsin.</summary>
    private static readonly JsonSerializerOptions ExportAuditJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Eksport hodisasining tafsiloti.</summary>
    /// <param name="Filters">Qo'llangan filtrlar (qidiruv matnisiz).</param>
    /// <param name="Rows">Fayldagi qatorlar soni.</param>
    private sealed record ExportAuditDetail(ExportAuditFilters Filters, int Rows);

    /// <summary>Filtrlar — qidiruv matnining O'ZI emas, faqat «qidiruv bor edi».</summary>
    private sealed record ExportAuditFilters(
        bool? HasSearch,
        string? Status,
        string? Sort,
        bool? ExcludeGeneralPool)
    {
        public static ExportAuditFilters Of(ListVacanciesQuery query) => new(
            string.IsNullOrWhiteSpace(query.Search) ? null : true,
            string.IsNullOrWhiteSpace(query.Status) ? null : query.Status,
            string.IsNullOrWhiteSpace(query.Sort) ? null : query.Sort,
            query.ExcludeGeneralPool ? true : null);
    }
}
