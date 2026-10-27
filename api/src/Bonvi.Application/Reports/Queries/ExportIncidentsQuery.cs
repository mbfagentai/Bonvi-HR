using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Reports.Export;
using Bonvi.Application.Safety;
using Bonvi.Application.Safety.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Safety;

namespace Bonvi.Application.Reports.Queries;

/// <summary>
/// «Baxtsiz hodisalar» jurnalini EKRANDAGI filtr bilan Excel faylga eksport qiladi
/// (<c>SPEC-excel-eksport.md</c> §2 #11).
/// </summary>
/// <remarks>
/// <para>
/// Qatorlar ro'yxat handler'ining O'ZIDAN (<see cref="ListIncidentsQueryHandler"/>) — ro'yxat
/// sahifalanmaydi, ya'ni ekran bilan fayl bitta chaqiruvning bitta natijasi (qayta aloqa
/// eksporti bilan bir xil; mapper haqidagi izoh ham o'sha).
/// </para>
/// <para>
/// Ekrandagi beshta ustundan keyin SPEC aytgan qo'shimchalar — qatorning o'zida bor: hodisa
/// tavsifi, komissiya xulosasi va xulosa sanasi (ekranda u «Yakunlangan» belgisining ustida).
/// </para>
/// </remarks>
public sealed class ExportIncidentsQueryHandler
{
    private const string Slug = "baxtsiz_hodisalar";
    private const string SheetName = "Baxtsiz hodisalar";

    private static readonly IReadOnlyList<ExcelColumn<IncidentDto>> Columns =
    [
        // Ekrandagi ustunlar — o'sha tartibda
        new("F.I.Sh.", row => row.EmployeeName),
        new("Sana", row => row.IncidentDate, ExcelCellKind.Date),
        new("Jabrlanish darajasi", row => SeverityLabel(row.Severity)),
        new("Joyi", row => row.Location, ExcelCellKind.LongText),
        new("Xulosa", row => row.CommissionConclusion is null ? "Kutilmoqda" : "Yakunlangan"),

        // Qo'shimchalar (SPEC §2 #11)
        new("Tavsif", row => row.Description, ExcelCellKind.LongText),
        new("Komissiya xulosasi", row => row.CommissionConclusion, ExcelCellKind.LongText),
        new("Xulosa sanasi", row => row.ConcludedAt, ExcelCellKind.Date),
    ];

    private readonly ListIncidentsQueryHandler _list;
    private readonly IExcelTableWriter _writer;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ExportIncidentsQueryHandler(
        ListIncidentsQueryHandler list,
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
        ListIncidentsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var found = await _list.HandleAsync(query, cancellationToken).ConfigureAwait(false);

        if (found.IsFailure)
        {
            return found.Error;
        }

        var rows = found.Value;

        if (rows.Count > ExcelExport.MaxRows)
        {
            return ExcelExport.TooMany(rows.Count);
        }

        var content = _writer.Write(SheetName, Columns, rows);

        _auditLogger.Record(
            AuditAction.Export,
            nameof(Incident),
            entityId: null,
            newValues: JsonSerializer.Serialize(
                new ExportAuditDetail(ExportAuditFilters.Of(query), rows.Count),
                ExportAuditJson));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ExcelExportFile(content, ExcelExport.FileName(Slug, _clock.Today));
    }

    /// <summary>Jabrlanish darajasi — ekrandagi belgi bilan bir xil.</summary>
    private static string SeverityLabel(IncidentSeverity severity) => severity switch
    {
        IncidentSeverity.Minor => "Yengil",
        IncidentSeverity.Moderate => "O'rtacha",
        IncidentSeverity.Severe => "Og'ir",
        IncidentSeverity.Fatal => "Halokatli",
        _ => severity.ToString(),
    };

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

    /// <summary>Filtrlar — qidiruv matnining O'ZI emas, faqat «qidiruv bor edi».</summary>
    private sealed record ExportAuditFilters(bool? HasSearch, DateOnly? From, DateOnly? To, IncidentSeverity? Severity)
    {
        public static ExportAuditFilters Of(ListIncidentsQuery query) => new(
            string.IsNullOrWhiteSpace(query.Search) ? null : true,
            query.From,
            query.To,
            query.Severity);
    }
}
