using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Requests;
using Bonvi.Application.Reports.Export;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Reports.Queries;

/// <summary>
/// «So'rovlar → Yo'qlik» ro'yxatini EKRANDAGI filtr bilan Excel faylga eksport qiladi
/// (<c>SPEC-excel-eksport.md</c> §2 #6).
/// </summary>
/// <remarks>
/// <para>
/// Filtr — <see cref="SearchAbsenceRequestsQueryHandler.BuildFilter"/> orqali, ro'yxat
/// handler'ining o'zi emas: u sahifani 200 bilan qisadi, eksportga esa chegara + 1 kerak.
/// Tartib ham o'qish modelining o'zidan — yangisi birinchi.
/// </para>
/// <para>
/// Ekranda IKKI jadval bor (kutayotganlar va qaror qabul qilinganlar) — ular bitta endpoint,
/// faqat <c>status</c> / <c>decided</c> boshqa. Fayl so'ralgan parametrlarning o'zini oladi:
/// ikkalasisiz — butun navbat, bitta faylda.
/// </para>
/// <para>
/// Ekrandagi «Davr» katagi faylda UCHTA ustun: ikki haqiqiy Excel sanasi va vaqt (yoki «Butun
/// kun») — HR faylni sana bo'yicha saralay olishi kerak. Ekrandagi qaror katagi (izoh, kim,
/// qachon) ham alohida ustunlarda.
/// </para>
/// </remarks>
public sealed class ExportAbsenceRequestsQueryHandler
{
    private const string Slug = "yoqlik_sorovlari";
    private const string SheetName = "Yo'qlik so'rovlari";

    private static readonly IReadOnlyList<ExcelColumn<AbsenceRequestDto>> Columns =
    [
        new("Xodim", row => row.EmployeeName),
        new("Bo'limi", row => row.DepartmentName),
        new("Yo'qlik turi", row => row.AbsenceTypeName),
        new("Sanadan", row => row.FromDate, ExcelCellKind.Date),
        new("Sanagacha", row => row.ToDate, ExcelCellKind.Date),
        new("Vaqt", row => TimeText(row)),
        new("Izoh", row => row.Note, ExcelCellKind.LongText),
        new("Holat", row => AttendanceRequestExportText.Status(row.Status)),
        new("Qaror izohi", row => row.DecisionNote, ExcelCellKind.LongText),
        new("Qaror qilgan", row => AttendanceRequestExportText.DecidedBy(row.DecidedByName, row.CancelledByEmployee)),
        new("Qaror sanasi", row => row.DecidedAt, ExcelCellKind.DateTime),
        new("Ilovadan", row => row.SubmittedByEmployee),
    ];

    private readonly IAttendanceReadModel _readModel;
    private readonly IExcelTableWriter _writer;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ExportAbsenceRequestsQueryHandler(
        IAttendanceReadModel readModel,
        IExcelTableWriter writer,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _readModel = readModel;
        _writer = writer;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<ExcelExportFile>> HandleAsync(
        SearchAbsenceRequestsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // `MaxRows + 1`: chegaradan oshganini bilish uchun bittasi ortiqcha yetarli.
        var filter = SearchAbsenceRequestsQueryHandler.BuildFilter(query, page: 1, pageSize: ExcelExport.MaxRows + 1);
        if (filter.IsFailure)
        {
            return filter.Error;
        }

        var found = await _readModel.SearchAbsenceRequestsAsync(filter.Value, cancellationToken).ConfigureAwait(false);

        if (found.Total > ExcelExport.MaxRows)
        {
            return ExcelExport.TooMany(found.Total);
        }

        var rows = found.Items;
        var content = _writer.Write(SheetName, Columns, rows);

        _auditLogger.Record(
            AuditAction.Export,
            nameof(AbsenceRequest),
            entityId: null,
            newValues: JsonSerializer.Serialize(
                new ExportAuditDetail(ExportAuditFilters.Of(query), rows.Count),
                ExportAuditJson));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ExcelExportFile(content, ExcelExport.FileName(Slug, _clock.Today));
    }

    /// <summary>
    /// «08:30 – 11:57» yoki «Butun kun» — ekrandagi davr katagining vaqt qismi. Vaqt faqat
    /// bir kunlik so'rovda bo'ladi (domen qoidasi), ko'p kunlik so'rov doim butun kunlar.
    /// </summary>
    private static string TimeText(AbsenceRequestDto row)
        => row.FromTime is { } from && row.ToTime is { } to
            ? string.Create(CultureInfo.InvariantCulture, $"{from:HH\\:mm} – {to:HH\\:mm}")
            : "Butun kun";

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
    private sealed record ExportAuditFilters(
        bool? HasSearch,
        Guid? EmployeeId,
        AbsenceRequestStatus? Status,
        bool? Decided,
        DateOnly? From,
        DateOnly? To,
        Guid? AbsenceTypeId,
        AbsenceRequestMode? Mode)
    {
        public static ExportAuditFilters Of(SearchAbsenceRequestsQuery query) => new(
            string.IsNullOrWhiteSpace(query.Search) ? null : true,
            query.EmployeeId,
            query.Status,
            query.Decided,
            query.From,
            query.To,
            query.AbsenceTypeId,
            query.Mode);
    }
}
