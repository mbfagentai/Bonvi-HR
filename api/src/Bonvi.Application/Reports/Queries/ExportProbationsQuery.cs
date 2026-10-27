using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Employees.Probations;
using Bonvi.Application.Reports.Export;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Reports.Queries;

/// <summary>
/// «Sinov muddati» ro'yxatini EKRANDAGI filtr bilan Excel faylga eksport
/// qiladi (<c>SPEC-excel-eksport.md</c> §2 #3).
/// </summary>
/// <remarks>
/// <para>
/// Qatorlar ro'yxat handler'ining O'ZIDAN (<see cref="ListProbationsQueryHandler"/>):
/// «bugun», qolgan kun va tartib (tugash sanasi bo'yicha) o'sha yerda —
/// fayl ekrandagi bilan bir xil kunni hisoblaydi.
/// </para>
/// <para>
/// «Qolgan kun» — SON (manfiy — muddati o'tgan), ekrandagi «12 kun o'tdi»
/// matni emas: HR faylni shu ustun bo'yicha saralay olishi kerak. Ekran uni
/// faqat davom etayotgan sinovda ko'rsatadi, fayl ham shunday — yakunlangan
/// sinovda «qolgan kun» ma'nosiz.
/// </para>
/// <para>
/// «Holati» ekranda faqat «Barcha holatlar» filtrida chiqadi; faylda u har
/// doim bor — fayl filtrsiz ham o'qilishi kerak.
/// </para>
/// </remarks>
public sealed class ExportProbationsQueryHandler
{
    private const string Slug = "sinov_muddati";
    private const string SheetName = "Sinov muddati";

    private static readonly IReadOnlyList<ExcelColumn<ProbationListItemDto>> Columns =
    [
        // Ekrandagi ustunlar — o'sha tartibda
        new("F.I.Sh.", row => row.EmployeeName),
        new("Lavozimi", row => row.Position),
        new("Bo'limi", row => row.Department),
        new("Boshlangan", row => row.StartDate, ExcelCellKind.Date),
        new("Tugaydi", row => row.EndDate, ExcelCellKind.Date),
        new(
            "Qolgan kun",
            row => row.Status == ProbationStatus.InProgress ? row.DaysLeft : null,
            ExcelCellKind.WholeNumber),
        new("Holati", row => StatusLabel(row.Status)),

        // Yakunlash oynasida yozilgan xulosa — qatorning o'zida bor
        new("Xulosa", row => row.Conclusion, ExcelCellKind.LongText),
    ];

    private readonly ListProbationsQueryHandler _list;
    private readonly IExcelTableWriter _writer;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ExportProbationsQueryHandler(
        ListProbationsQueryHandler list,
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
        ListProbationsQuery query,
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
            nameof(Probation),
            entityId: null,
            newValues: JsonSerializer.Serialize(
                new ExportAuditDetail(ExportAuditFilters.Of(query), rows.Count),
                ExportAuditJson));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ExcelExportFile(content, ExcelExport.FileName(Slug, _clock.Today));
    }

    /// <summary>Holat — ekrandagi yorliq bilan bir xil.</summary>
    private static string StatusLabel(ProbationStatus status) => status switch
    {
        ProbationStatus.InProgress => "Davom etmoqda",
        ProbationStatus.Passed => "O'tdi",
        ProbationStatus.Failed => "O'tmadi",
        ProbationStatus.LeftEarly => "O'zi ketdi",
        ProbationStatus.Cancelled => "Bekor qilindi",
        _ => status.ToString(),
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

    /// <summary>Filtrlar — shaxsiy ma'lumotsiz.</summary>
    private sealed record ExportAuditFilters(
        bool? HasSearch,
        ProbationStatus? Status,
        int? EndingWithinDays,
        DateOnly? From,
        DateOnly? To)
    {
        public static ExportAuditFilters Of(ListProbationsQuery query) => new(
            string.IsNullOrWhiteSpace(query.Search) ? null : true,
            query.Status,
            query.EndingWithinDays,
            query.From,
            query.To);
    }
}
