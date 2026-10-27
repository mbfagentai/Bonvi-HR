using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Queries;
using Bonvi.Application.Reports.Export;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Reports.Queries;

/// <summary>
/// «Davomat → Statistika» sahifasidagi xodimlar jadvalini EKRANDAGI sana, firma/bo'lim/lavozim,
/// holat chipi va qidiruv bilan Excel faylga eksport qiladi (<c>SPEC-excel-eksport.md</c> §2 #8).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Jadval serverda filtrlanadi</b> — holat chipi ham, qidiruv ham <c>GET /dashboard</c> ning
/// o'z parametrlari va o'qish modelida qo'llanadi (<c>AttendanceReadModel.GetDashboardAsync</c>:
/// to'plam bir marta hisoblanadi, keyin qidiruv va holat bilan toraytiriladi). Shu sababli bu
/// yerda yangi so'rov yo'li YO'Q: filtr — <see cref="GetAttendanceDashboardQueryHandler.BuildFilter"/>,
/// qatorlar — panelning o'sha metodidan, faqat sahifa chegara + 1.
/// </para>
/// <para>
/// Faqat BIR KUN: ekranda davr tanlanganda jadval yo'q (faqat grafik), ya'ni eksport qiladigan
/// ro'yxat ham yo'q. Fayl nomidagi sana — jadvalning SANASI, bugun emas: o'tgan kunning
/// statistikasi bugungi sana bilan nomlansa, chalkashtirardi.
/// </para>
/// <para>
/// Ustunlar ekrandagidek, ustiga ikkitasi qatorning o'zida bor: bo'lim va kechikish daqiqasi
/// (ekranda u «33 daqiqa kechikdi» matnining ichida — faylda son, saralash uchun). Jadvalsiz
/// kunning avtomatik 22:00 dagi «ketdi»si haqiqiy chiqish emas — ekranda «avto» izohi bor,
/// faylda ham shu izoh bilan matn bo'lib yoziladi.
/// </para>
/// </remarks>
public sealed class ExportAttendanceDashboardQueryHandler
{
    private const string Slug = "davomat_statistika";
    private const string SheetName = "Davomat statistikasi";

    private static readonly IReadOnlyList<ExcelColumn<AttendanceDashboardItemDto>> Columns =
    [
        new("F.I.Sh.", row => row.FullName),
        new("Lavozim", row => row.PositionName),
        new("Bo'lim", row => row.DepartmentName),
        new("Kelgan", row => row.ArrivedAt, ExcelCellKind.DateTime),
        new(
            "Ketgan",
            row => row is { LeftAuto: true, LeftAt: { } left } ? AutoExitText(left) : row.LeftAt,
            ExcelCellKind.DateTime),
        new("Holat", row => StatusLabel(row.Status)),
        new("Kechikish (daq.)", row => row.LateMinutes, ExcelCellKind.WholeNumber),
    ];

    private readonly IAttendanceReadModel _readModel;
    private readonly IExcelTableWriter _writer;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ExportAttendanceDashboardQueryHandler(
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
        GetAttendanceDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // `MaxRows + 1`: chegaradan oshganini bilish uchun bittasi ortiqcha yetarli.
        var filter = GetAttendanceDashboardQueryHandler.BuildFilter(
            query,
            _clock.Today,
            page: 1,
            pageSize: ExcelExport.MaxRows + 1);

        var found = await _readModel.GetDashboardAsync(filter, cancellationToken).ConfigureAwait(false);

        // `TotalItems` — jadvalning o'zi (qidiruv va holatdan keyin); `Total` esa diagramma to'plami.
        if (found.TotalItems > ExcelExport.MaxRows)
        {
            return ExcelExport.TooMany(found.TotalItems);
        }

        var rows = found.Items;
        var content = _writer.Write(SheetName, Columns, rows);

        _auditLogger.Record(
            AuditAction.Export,
            nameof(AttendanceDay),
            entityId: null,
            newValues: JsonSerializer.Serialize(
                new ExportAuditDetail(ExportAuditFilters.Of(query, found.Date), rows.Count),
                ExportAuditJson));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ExcelExportFile(content, ExcelExport.FileName(Slug, found.Date));
    }

    /// <summary>
    /// Jadvalsiz kunning avtomatik «ketdi»si — «… (avto)» MATNI, haqiqiy lahza bilan
    /// adashtirilmasin (ekrandagi «avto» izohi). Toshkent vaqtida, boshqa kataklar bilan bir xil.
    /// </summary>
    private static string AutoExitText(DateTimeOffset left)
        => left.ToTashkent().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) + " (avto)";

    /// <summary>Holat — ekrandagi qator yozuvi bilan bir xil (<c>DASHBOARD_STATUS_LOOK.rowKey</c>).</summary>
    /// <remarks>Kechikish daqiqasi o'z ustunida, shu sababli bu yerda faqat «Kechikdi».</remarks>
    private static string StatusLabel(AttendanceDashboardStatus status) => status switch
    {
        AttendanceDashboardStatus.OnTime => "Vaqtida keldi",
        AttendanceDashboardStatus.Late => "Kechikdi",
        AttendanceDashboardStatus.Absent => "Kelmadi",
        AttendanceDashboardStatus.NotStarted => "Ish kuni boshlanmagan",
        AttendanceDashboardStatus.DayOff => "Dam olish kuni",
        AttendanceDashboardStatus.NoSchedule => "Grafik yo'q",
        AttendanceDashboardStatus.Excused => "Sababli",
        AttendanceDashboardStatus.Arrived => "Keldi",
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

    /// <summary>
    /// Filtrlar — qidiruv matnining O'ZI emas, faqat «qidiruv bor edi». Sana — doim aniq
    /// qiymati bilan (berilmagan bo'lsa ham — o'sha kungi «bugun»).
    /// </summary>
    private sealed record ExportAuditFilters(
        bool? HasSearch,
        DateOnly Date,
        Guid? LegalEntityId,
        Guid? DepartmentId,
        Guid? PositionId,
        AttendanceDashboardStatus? Status,
        string? Sort)
    {
        public static ExportAuditFilters Of(GetAttendanceDashboardQuery query, DateOnly date) => new(
            string.IsNullOrWhiteSpace(query.Search) ? null : true,
            date,
            query.LegalEntityId,
            query.DepartmentId,
            query.PositionId,
            query.Status,
            string.IsNullOrWhiteSpace(query.Sort) ? null : query.Sort);
    }
}
