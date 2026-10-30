using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Application.Recruiting.VacancyRequests;
using Bonvi.Application.Reports.Export;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Recruiting;

namespace Bonvi.Application.Reports.Queries;

/// <summary>
/// «Vakansiya so'rovlari» ro'yxatini EKRANDAGI filtr bilan Excel faylga eksport qiladi.
/// </summary>
/// <remarks>
/// Qatorlar ro'yxat handler'ining o'zidan — <see cref="ExportVacanciesQueryHandler"/> bilan bir xil qoida.
/// </remarks>
public sealed class ExportVacancyRequestsQueryHandler
{
    private const string Slug = "vakansiya-sorovlari";
    private const string SheetName = "Vakansiya so'rovlari";

    private static readonly IReadOnlyList<ExcelColumn<VacancyRequestDto>> Columns =
    [
        new("Yuborilgan", row => row.CreatedAt, ExcelCellKind.DateTime),
        new("Bo'lim", row => row.DepartmentName),
        new("So'ragan", row => row.RequestedByName),
        new("Lavozim", row => row.Title),
        new("Soni", row => row.Openings, ExcelCellKind.WholeNumber),
        new("Qachondan kerak", row => row.NeededBy, ExcelCellKind.Date),
        new("Izoh", row => row.Note, ExcelCellKind.LongText),
        new("Holati", row => StatusLabel(row.Status)),
        new("Qaror sanasi", row => row.DecidedAt, ExcelCellKind.DateTime),
        new("Qaror qilgan", row => row.DecidedByName),
        new("Rad etish sababi", row => row.DecisionNote, ExcelCellKind.LongText),
        new("Ochilgan vakansiya", row => row.VacancyTitle),
        new("Nomzodlar", row => row.CandidateCount, ExcelCellKind.WholeNumber),
        new("Ishga olingan", row => row.HiredCount, ExcelCellKind.WholeNumber),
    ];

    private readonly ListVacancyRequestsQueryHandler _list;
    private readonly IExcelTableWriter _writer;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ExportVacancyRequestsQueryHandler(
        ListVacancyRequestsQueryHandler list,
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
        ListVacancyRequestsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

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
            nameof(VacancyRequest),
            entityId: null,
            newValues: JsonSerializer.Serialize(
                new ExportAuditDetail(ExportAuditFilters.Of(query), rows.Count),
                ExportAuditJson));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ExcelExportFile(content, ExcelExport.FileName(Slug, _clock.Today));
    }

    /// <summary>Holat — ekrandagi yorliq bilan bir xil.</summary>
    private static string StatusLabel(VacancyRequestStatus status) => status switch
    {
        VacancyRequestStatus.Pending => "Yangi",
        VacancyRequestStatus.Approved => "Vakansiya ochildi",
        VacancyRequestStatus.Rejected => "Rad etildi",
        VacancyRequestStatus.Cancelled => "Bekor qilindi",
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
    private sealed record ExportAuditFilters(bool? HasSearch, string? Status, DateOnly? From, DateOnly? To)
    {
        public static ExportAuditFilters Of(ListVacancyRequestsQuery query) => new(
            string.IsNullOrWhiteSpace(query.Search) ? null : true,
            string.IsNullOrWhiteSpace(query.Status) ? null : query.Status,
            query.From,
            query.To);
    }
}
