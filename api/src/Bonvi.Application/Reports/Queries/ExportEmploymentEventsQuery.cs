using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Employees.Employments;
using Bonvi.Application.Reports.Export;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Reports.Queries;

/// <summary>
/// «Buyruqlar» ro'yxatini EKRANDAGI filtr bilan Excel faylga eksport qiladi
/// (<c>SPEC-excel-eksport.md</c> §2 #4).
/// </summary>
/// <remarks>
/// <para>
/// Qatorlar ro'yxat handler'ining O'ZIDAN (<see cref="ListEmploymentEventsQueryHandler"/>)
/// va bu bu yerda ayniqsa muhim: oklad va qo'shimcha haq o'sha handler'da
/// <c>salary.view</c> / <c>salary.extra.view</c> bo'yicha yashiriladi. Eksport
/// o'qish modelini to'g'ridan-to'g'ri chaqirganida, fayl oklad ruxsatini
/// chetlab o'tadigan yo'l bo'lib qolardi.
/// </para>
/// <para>
/// «Tafsilot» — ekrandagi kabi «eski → yangi», BITTA qatorda. Ekran joy
/// tor bo'lgani uchun ikki qism bilan cheklaydi; faylda hammasi (lavozim,
/// bo'lim, summa), izoh esa alohida ustunda.
/// </para>
/// <para>
/// «Lavozimi» / «Bo'limi» — hodisadagi YANGI qiymat, u bo'lmasa eskisi.
/// Qatorda xodimning joriy lavozimi yo'q (fayl uchun alohida so'rov
/// qilinmaydi — SPEC §1), shu sababli oklad o'zgarishi yoki bo'shatish kabi
/// hodisalarda ular bo'sh qoladi.
/// </para>
/// </remarks>
public sealed class ExportEmploymentEventsQueryHandler
{
    private const string Slug = "buyruqlar";
    private const string SheetName = "Buyruqlar";

    private static readonly IReadOnlyList<ExcelColumn<EmploymentEventListItemDto>> Columns =
    [
        // Ekrandagi ustunlar — o'sha tartibda
        new("F.I.Sh.", row => row.EmployeeName),
        new("Turi", row => TypeLabel(row.Type)),
        new("Kuchga kirish", row => row.EffectiveDate, ExcelCellKind.Date),
        new("Buyruq", row => row.OrderNo),
        new("Buyruq sanasi", row => row.OrderDate, ExcelCellKind.Date),
        new("Tafsilot", Details, ExcelCellKind.LongText),

        // Qatorning o'zidagi qo'shimchalar
        new("Lavozimi", row => row.ToPosition ?? row.FromPosition),
        new("Bo'limi", row => row.ToDepartment ?? row.FromDepartment),
        new("Izoh", row => row.Note, ExcelCellKind.LongText),
    ];

    private readonly ListEmploymentEventsQueryHandler _list;
    private readonly IExcelTableWriter _writer;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ExportEmploymentEventsQueryHandler(
        ListEmploymentEventsQueryHandler list,
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
        ListEmploymentEventsQuery query,
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
            nameof(EmploymentEvent),
            entityId: null,
            newValues: JsonSerializer.Serialize(
                new ExportAuditDetail(ExportAuditFilters.Of(query), rows.Count),
                ExportAuditJson));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ExcelExportFile(content, ExcelExport.FileName(Slug, _clock.Today));
    }

    /// <summary>
    /// «Tafsilot» — nima o'zgargani, bitta qatorda: «Lavozimi: A → B; Bo'limi: …; Oylik ish haqi: …».
    /// </summary>
    /// <remarks>
    /// Ekrandagi <c>DetailsCell</c> qoidasi: lavozim va bo'lim faqat O'ZGARGAN
    /// bo'lsa, summa faqat qatorda bo'lsa (ruxsat bo'lmasa handler uni
    /// allaqachon <c>null</c> qilgan). Eski qiymat bo'lmasa — faqat yangisi.
    /// </remarks>
    private static string? Details(EmploymentEventListItemDto row)
    {
        var parts = new List<string>(3);

        if (row.ToPosition is { } toPosition && toPosition != row.FromPosition)
        {
            parts.Add($"Lavozimi: {Change(row.FromPosition, toPosition)}");
        }

        if (row.ToDepartment is { } toDepartment && toDepartment != row.FromDepartment)
        {
            parts.Add($"Bo'limi: {Change(row.FromDepartment, toDepartment)}");
        }

        if (row.ToSalary is { } toSalary)
        {
            // Qo'shimcha vazifadagi summa — oklad emas, qo'shimcha haq
            var label = row.Type == EmploymentEventType.AdditionalDuty ? "Qo'shimcha haq" : "Oylik ish haqi";
            parts.Add($"{label}: {Change(row.FromSalary?.Formatted, toSalary.Formatted)}");
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>«eski → yangi»; eskisi yo'q yoki bir xil bo'lsa — faqat yangisi.</summary>
    private static string Change(string? from, string to)
        => from is null || from == to ? to : $"{from} → {to}";

    /// <summary>Hodisa turi — ekrandagi yorliq bilan bir xil.</summary>
    private static string TypeLabel(EmploymentEventType type) => type switch
    {
        EmploymentEventType.Hire => "Ishga qabul",
        EmploymentEventType.Transfer => "Bo'lim o'zgarishi",
        EmploymentEventType.Promotion => "Lavozim o'zgarishi",
        EmploymentEventType.AdditionalDuty => "Qo'shimcha vazifa",
        EmploymentEventType.SalaryChange => "Oklad o'zgarishi",
        EmploymentEventType.ProbationResult => "Sinov natijasi",
        EmploymentEventType.Termination => "Ishdan bo'shatish",
        EmploymentEventType.Rehire => "Qayta ishga qabul",
        _ => type.ToString(),
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

    /// <summary>Filtrlar — shaxsiy ma'lumotsiz (qidiruvda ism yoki JSHSHIR bo'ladi).</summary>
    private sealed record ExportAuditFilters(
        bool? HasSearch,
        EmploymentEventType? Type,
        DateOnly? From,
        DateOnly? To,
        Guid? DepartmentId,
        Guid? LegalEntityId)
    {
        public static ExportAuditFilters Of(ListEmploymentEventsQuery query) => new(
            string.IsNullOrWhiteSpace(query.Search) ? null : true,
            query.Type,
            query.From,
            query.To,
            query.DepartmentId,
            query.LegalEntityId);
    }
}
