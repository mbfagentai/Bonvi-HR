using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Employees.Flags;
using Bonvi.Application.Reports.Export;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Reports.Queries;

/// <summary>
/// Belgi ro'yxatlarini — rezerv / qora ro'yxatning «Xodimlar» bo'limi,
/// nazoratdagilar, qobiliyatlilar — EKRANDAGI filtr bilan Excel faylga
/// eksport qiladi (<c>SPEC-excel-eksport.md</c> §2 #5).
/// </summary>
/// <remarks>
/// <para>
/// Qatorlar ro'yxat handler'ining O'ZIDAN (<see cref="ListFlaggedQueryHandler"/>)
/// — «kuchda» qoidasi va tartib (yangi belgi tepada) o'sha yerda.
/// </para>
/// <para>
/// Ro'yxat SAHIFALANMAYDI (ekran ham hammasini oladi), shu sababli chegara
/// qatorlar sonining o'zi bilan tekshiriladi.
/// </para>
/// <para>
/// Fayl nomi va varaq nomi belgi turidan: to'rt ro'yxat — to'rt xil fayl,
/// HR ularni yuklab olgach bir-biridan ajrata olsin.
/// </para>
/// </remarks>
public sealed class ExportFlaggedEmployeesQueryHandler
{
    private static readonly IReadOnlyList<ExcelColumn<FlaggedEmployeeDto>> Columns =
    [
        // Ekrandagi ustunlar — o'sha tartibda
        new("F.I.Sh.", row => row.EmployeeName),
        new("Sabab", row => row.Reason, ExcelCellKind.LongText),
        new("Qo'yilgan", row => row.StartDate, ExcelCellKind.Date),
        new("Yopilgan", row => row.EndDate, ExcelCellKind.Date),

        // Qatorning o'zidagi qo'shimchalar
        new("Belgi", row => TypeLabel(row.Type)),
        new("Holati", row => StateLabel(row)),
    ];

    private readonly ListFlaggedQueryHandler _list;
    private readonly IExcelTableWriter _writer;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ExportFlaggedEmployeesQueryHandler(
        ListFlaggedQueryHandler list,
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
        ListFlaggedQuery query,
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

        var content = _writer.Write(TypeLabel(query.Type), Columns, rows);

        _auditLogger.Record(
            AuditAction.Export,
            nameof(EmployeeFlag),
            entityId: null,
            newValues: JsonSerializer.Serialize(
                new ExportAuditDetail(ExportAuditFilters.Of(query), rows.Count),
                ExportAuditJson));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ExcelExportFile(content, ExcelExport.FileName(Slug(query.Type), _clock.Today));
    }

    /// <summary>Belgi turi — xodimlar eksportidagi «Belgilar» ustuni bilan bir xil yorliq.</summary>
    private static string TypeLabel(EmployeeFlagType type) => type switch
    {
        EmployeeFlagType.Reserve => "Rezerv",
        EmployeeFlagType.Blacklist => "Qora ro'yxat",
        EmployeeFlagType.Monitoring => "Nazoratda",
        EmployeeFlagType.Capable => "Qobiliyatli",
        _ => type.ToString(),
    };

    /// <summary>Fayl nomidagi qism: <c>Bonvi_qora_royxat_xodimlar_2026-10-22.xlsx</c>.</summary>
    private static string Slug(EmployeeFlagType type) => type switch
    {
        // «_xodimlar» — o'sha bo'limning nomzodlar fayli (`rezerv_nomzodlar`) bilan adashmasin
        EmployeeFlagType.Reserve => "rezerv_xodimlar",
        EmployeeFlagType.Blacklist => "qora_royxat_xodimlar",
        EmployeeFlagType.Monitoring => "nazoratdagilar",
        EmployeeFlagType.Capable => "qobiliyatli",
        _ => "belgilar",
    };

    /// <summary>
    /// Kuchdami: «Faol», «Yopilgan» yoki «Boshlanmagan».
    /// </summary>
    /// <remarks>
    /// «Kuchda» ni ro'yxat handler'i hisoblaydi (<see cref="FlaggedEmployeeDto.IsActive"/>),
    /// bu yerda qayta hisoblanmaydi. Kuchda bo'lmagan va tugash sanasi YO'Q belgi
    /// faqat bitta holatda bo'ladi — boshlanish sanasi hali kelmagan.
    /// </remarks>
    private static string StateLabel(FlaggedEmployeeDto row)
        => row.IsActive ? "Faol" : row.EndDate is null ? "Boshlanmagan" : "Yopilgan";

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
        EmployeeFlagType Type,
        bool OnlyActive,
        bool? HasSearch,
        DateOnly? From,
        DateOnly? To)
    {
        public static ExportAuditFilters Of(ListFlaggedQuery query) => new(
            query.Type,
            query.OnlyActive,
            string.IsNullOrWhiteSpace(query.Search) ? null : true,
            query.From,
            query.To);
    }
}
