using System.Globalization;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Queries;

/// <summary>
/// Davomat eksportlarining umumiy chegaralari va fayl nomlari (SPEC §6.7).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Har bir eksport ekrandagi so'rovning O'ZINI oladi</b> va o'sha
/// handlerning <c>BuildFilter</c> metodini chaqiradi — faqat sahifa
/// boshqacha (<c>ExportEmployeesQueryHandler</c> precedenti). Alohida so'rov
/// turi yangi filtr qo'shilganda Excel'ni jimgina eskirtirib qo'yardi.
/// </para>
/// <para>
/// <b>Chegaraga urilganda fayl QIRQILMAYDI</b> — aniq xato qaytadi. «20 000
/// o'rniga 18 000 qator» jimgina eksport qilinsa, HR buni sezmay qolardi.
/// </para>
/// </remarks>
public static class AttendanceExport
{
    /// <summary>
    /// Qator eksportlarining (o'tishlar, chetlanishlar, xulosa) chegarasi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hajm: kuniga ~5 400 o'tish, ya'ni 20 000 qator — butun korxonaning
    /// ~4 kuni yoki 25 kishilik bo'limning bir oyi. HR amalda bo'lim yoki
    /// xodim bo'yicha eksport qiladi.
    /// </para>
    /// <para>
    /// Nega kattaroq emas: <c>.xlsx</c> fayl xotirada to'liq quriladi
    /// (ClosedXML), 20 000 × 13 ustun ≈ 260 ming katak. Server 2 GB
    /// xotirali VPS va eksport bir vaqtda bir necha kishidan kelishi
    /// mumkin. Chegarani ko'tarish bir qatorlik o'zgarish — lekin avval
    /// serverda o'lchab ko'rish kerak.
    /// </para>
    /// </remarks>
    public const int MaxRows = 20_000;

    /// <summary>Tabel eksportidagi xodimlar chegarasi.</summary>
    /// <remarks>
    /// 2 000 xodim × 31 kun ≈ 62 ming batafsil qator. Bugun faol xodimlar
    /// ~540, arxiv bilan ~2 350 — lekin bir oyda munosabati bo'lganlar
    /// faqat o'sha oyning xodimlari, ya'ni chegara amalda urilmaydi.
    /// </remarks>
    public const int MaxTimesheetEmployees = 2_000;

    private const string Prefix = "Bonvi_davomat_";

    internal static string TimesheetFileName(int year, int month)
        => string.Create(CultureInfo.InvariantCulture, $"{Prefix}tabel_{year:0000}-{month:00}.xlsx");

    internal static string PeriodFileName(string report, DateOnly from, DateOnly to)
        => string.Create(CultureInfo.InvariantCulture, $"{Prefix}{report}_{from:yyyy-MM-dd}_{to:yyyy-MM-dd}.xlsx");
}

/// <summary>Tabel eksporti (<c>GET /export/timesheet</c>) — <see cref="GetTimesheetQuery"/> bilan bir xil parametrlar.</summary>
public sealed class ExportTimesheetQueryHandler
{
    private readonly IAttendanceReadModel _readModel;
    private readonly IAttendanceExcelWriter _writer;

    public ExportTimesheetQueryHandler(IAttendanceReadModel readModel, IAttendanceExcelWriter writer)
    {
        _readModel = readModel;
        _writer = writer;
    }

    public async Task<Result<AttendanceExportFile>> HandleAsync(
        GetTimesheetQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // `+ 1`: chegaradan oshganini bilish uchun bitta ortiqcha yetarli.
        var pageSize = AttendanceExport.MaxTimesheetEmployees + 1;

        var filter = GetTimesheetQueryHandler.BuildFilter(query, page: 1, pageSize);
        if (filter.IsFailure)
        {
            return filter.Error;
        }

        var found = await _readModel.GetTimesheetAsync(filter.Value, cancellationToken).ConfigureAwait(false);

        if (found.Total > AttendanceExport.MaxTimesheetEmployees)
        {
            return AttendanceErrors.ExportTooLarge(found.Total, AttendanceExport.MaxTimesheetEmployees);
        }

        var timesheet = GetTimesheetQueryHandler.ToDto(filter.Value, found, page: 1, pageSize: found.Total);

        return new AttendanceExportFile(
            _writer.WriteTimesheet(timesheet),
            AttendanceExport.TimesheetFileName(query.Year, query.Month));
    }
}

/// <summary>O'tishlar eksporti (<c>GET /export/passes</c>) — <see cref="SearchPassesQuery"/> bilan bir xil parametrlar.</summary>
public sealed class ExportPassesQueryHandler
{
    private readonly IAttendanceReadModel _readModel;
    private readonly IAttendanceExcelWriter _writer;
    private readonly IClock _clock;

    public ExportPassesQueryHandler(IAttendanceReadModel readModel, IAttendanceExcelWriter writer, IClock clock)
    {
        _readModel = readModel;
        _writer = writer;
        _clock = clock;
    }

    public async Task<Result<AttendanceExportFile>> HandleAsync(
        SearchPassesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filter = SearchPassesQueryHandler.BuildFilter(query, _clock.Today, page: 1, AttendanceExport.MaxRows + 1);
        if (filter.IsFailure)
        {
            return filter.Error;
        }

        var found = await _readModel.SearchPassesAsync(filter.Value, cancellationToken).ConfigureAwait(false);

        if (found.Total > AttendanceExport.MaxRows)
        {
            return AttendanceErrors.ExportTooLarge(found.Total, AttendanceExport.MaxRows);
        }

        return new AttendanceExportFile(
            _writer.WritePasses(found.Items),
            AttendanceExport.PeriodFileName("otishlar", filter.Value.From, filter.Value.To));
    }
}

/// <summary>Chetlanishlar eksporti (<c>GET /export/exceptions</c>) — <see cref="GetAttendanceExceptionsQuery"/> bilan bir xil parametrlar.</summary>
public sealed class ExportAttendanceExceptionsQueryHandler
{
    private readonly IAttendanceReadModel _readModel;
    private readonly IAttendanceExcelWriter _writer;
    private readonly IClock _clock;

    public ExportAttendanceExceptionsQueryHandler(
        IAttendanceReadModel readModel,
        IAttendanceExcelWriter writer,
        IClock clock)
    {
        _readModel = readModel;
        _writer = writer;
        _clock = clock;
    }

    public async Task<Result<AttendanceExportFile>> HandleAsync(
        GetAttendanceExceptionsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filter = GetAttendanceExceptionsQueryHandler.BuildFilter(
            query,
            _clock.Today,
            page: 1,
            AttendanceExport.MaxRows + 1);
        if (filter.IsFailure)
        {
            return filter.Error;
        }

        var found = await _readModel.GetExceptionsAsync(filter.Value, cancellationToken).ConfigureAwait(false);

        if (found.Total > AttendanceExport.MaxRows)
        {
            return AttendanceErrors.ExportTooLarge(found.Total, AttendanceExport.MaxRows);
        }

        return new AttendanceExportFile(
            _writer.WriteExceptions(filter.Value.Kind, found.Items),
            AttendanceExport.PeriodFileName("chetlanishlar", filter.Value.From, filter.Value.To));
    }
}

/// <summary>Xulosa eksporti (<c>GET /export/summary</c>) — <see cref="GetAttendanceSummaryQuery"/> bilan bir xil parametrlar.</summary>
public sealed class ExportAttendanceSummaryQueryHandler
{
    private readonly IAttendanceReadModel _readModel;
    private readonly IAttendanceExcelWriter _writer;
    private readonly IClock _clock;

    public ExportAttendanceSummaryQueryHandler(
        IAttendanceReadModel readModel,
        IAttendanceExcelWriter writer,
        IClock clock)
    {
        _readModel = readModel;
        _writer = writer;
        _clock = clock;
    }

    public async Task<Result<AttendanceExportFile>> HandleAsync(
        GetAttendanceSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filter = GetAttendanceSummaryQueryHandler.BuildFilter(
            query,
            _clock.Today,
            page: 1,
            AttendanceExport.MaxRows + 1);
        if (filter.IsFailure)
        {
            return filter.Error;
        }

        var found = await _readModel.GetSummaryAsync(filter.Value, cancellationToken).ConfigureAwait(false);

        if (found.Total > AttendanceExport.MaxRows)
        {
            return AttendanceErrors.ExportTooLarge(found.Total, AttendanceExport.MaxRows);
        }

        return new AttendanceExportFile(
            _writer.WriteSummary(found.Items),
            AttendanceExport.PeriodFileName("xulosa", filter.Value.From, filter.Value.To));
    }
}
