using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Requests;
using Bonvi.Application.Reports.Export;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Reports.Queries;

/// <summary>
/// «So'rovlar → Jadval o'zgarishi» ro'yxatini EKRANDAGI filtr bilan Excel faylga eksport
/// qiladi (<c>SPEC-excel-eksport.md</c> §2 #7).
/// </summary>
/// <remarks>
/// <para>
/// Filtr — <see cref="SearchScheduleChangeRequestsQueryHandler.BuildFilter"/> orqali
/// (yo'qlik so'rovlari eksporti bilan bir xil sabab: ro'yxat sahifani 200 bilan qisadi).
/// </para>
/// <para>
/// Bitta qator — bitta SO'ROV, ekrandagidek; uning kunlari esa katak ICHIDA, har biri o'z
/// qatorida. «Sanalar», «Ish vaqti» va «Tanaffus» qatorma-qator mos keladi: uchinchi sananing
/// rejasi uchinchi qatorda. Almashtirishda (<see cref="ScheduleChangeKind.Swap"/>) ekrandagidek
/// faqat juftlar («05.10.2026 ⇄ 25.10.2026») — vaqtlar o'sha sanalarning o'z rejasidan olinadi
/// va so'rovning gapi emas.
/// </para>
/// <para>
/// Matn ekrandagi <c>schedule-request-text.ts</c> qoidasi bilan: tungi smenada «(ertasi kuni)»,
/// erkin kunda «erkin 7:30», tanaffus uzunligi oynasidan farq qilsa — daqiqasi ham. Sana esa
/// yil bilan: fayl yillar o'tib ham o'qiladi.
/// </para>
/// </remarks>
public sealed class ExportScheduleChangeRequestsQueryHandler
{
    private const string Slug = "jadval_sorovlari";
    private const string SheetName = "Jadval so'rovlari";

    /// <summary>Bir katak ichidagi qatorlar ajratgichi — <see cref="ExcelCellKind.LongText"/> o'raydi.</summary>
    private const string LineBreak = "\n";

    /// <summary>Bo'sh qiymat o'rniga chiziqcha — katakdagi qatorlar «Sanalar» bilan mos tursin.</summary>
    private const string NoValue = "—";

    private const int MinutesPerDay = 24 * 60;

    private static readonly IReadOnlyList<ExcelColumn<ScheduleChangeRequestDto>> Columns =
    [
        new("Xodim", row => row.EmployeeName),
        new("Bo'limi", row => row.DepartmentName),
        new("So'rov turi", row => KindLabel(row.Kind)),
        new("Sanalar", DaysText, ExcelCellKind.LongText),
        new("Ish vaqti", WorkTimeText, ExcelCellKind.LongText),
        new("Tanaffus", BreakText, ExcelCellKind.LongText),
        new("Izoh", row => row.Note, ExcelCellKind.LongText),
        new("Holat", row => AttendanceRequestExportText.Status(row.Status)),
        new("Qaror izohi", row => row.DecisionNote, ExcelCellKind.LongText),
        new("Qaror qilgan", row => AttendanceRequestExportText.DecidedBy(row.DecidedByName, row.CancelledByEmployee)),
        new("Qaror sanasi", row => row.DecidedAt, ExcelCellKind.DateTime),
        new("Ilovadan", row => row.SubmittedByEmployee),
    ];

    private readonly IScheduleChangeRequestReadModel _readModel;
    private readonly IExcelTableWriter _writer;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ExportScheduleChangeRequestsQueryHandler(
        IScheduleChangeRequestReadModel readModel,
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
        SearchScheduleChangeRequestsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // `MaxRows + 1`: chegaradan oshganini bilish uchun bittasi ortiqcha yetarli.
        var filter = SearchScheduleChangeRequestsQueryHandler.BuildFilter(query, page: 1, pageSize: ExcelExport.MaxRows + 1);
        if (filter.IsFailure)
        {
            return filter.Error;
        }

        var found = await _readModel.SearchScheduleChangeRequestsAsync(filter.Value, cancellationToken).ConfigureAwait(false);

        if (found.Total > ExcelExport.MaxRows)
        {
            return ExcelExport.TooMany(found.Total);
        }

        var rows = found.Items;
        var content = _writer.Write(SheetName, Columns, rows);

        _auditLogger.Record(
            AuditAction.Export,
            nameof(ScheduleChangeRequest),
            entityId: null,
            newValues: JsonSerializer.Serialize(
                new ExportAuditDetail(ExportAuditFilters.Of(query), rows.Count),
                ExportAuditJson));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ExcelExportFile(content, ExcelExport.FileName(Slug, _clock.Today));
    }

    private static string KindLabel(ScheduleChangeKind kind) => kind switch
    {
        ScheduleChangeKind.Change => "Kunni o'zgartirish",
        ScheduleChangeKind.Swap => "Kun almashtirish",
        _ => kind.ToString(),
    };

    /// <summary>Har sana yoki har juft — bittadan qator.</summary>
    private static string DaysText(ScheduleChangeRequestDto row)
        => row.Kind == ScheduleChangeKind.Swap
            ? string.Join(
                LineBreak,
                row.Days
                    .Where(day => day.SwapWith is { } pair && day.Date < pair)
                    .Select(day => $"{DateText(day.Date)} ⇄ {DateText(day.SwapWith!.Value)}"))
            : string.Join(LineBreak, row.Days.Select(day => DateText(day.Date)));

    /// <summary>Kunning rejasi: «08:30–18:00», «22:00–06:00 (ertasi kuni)», «… , erkin 7:30», «Dam olish kuni».</summary>
    private static string? WorkTimeText(ScheduleChangeRequestDto row)
        => row.Kind == ScheduleChangeKind.Swap ? null : string.Join(LineBreak, row.Days.Select(PlanText));

    /// <summary>Tanaffus: «12:00–13:00», «60 daq (12:00–15:00 oralig'ida)», «60 daq» yoki «—».</summary>
    private static string? BreakText(ScheduleChangeRequestDto row)
        => row.Kind == ScheduleChangeKind.Swap ? null : string.Join(LineBreak, row.Days.Select(BreakOf));

    private static string PlanText(ScheduleChangeDayDto day)
    {
        if (!day.IsWorking)
        {
            return "Dam olish kuni";
        }

        var text = $"{TimeText(day.StartTime)}–{TimeText(day.EndTime)}";

        // Tugash boshlanishdan oldin — tungi smena (jadvaldagi qoida)
        if (day.StartTime is { } start && day.EndTime is { } end && end <= start)
        {
            text += " (ertasi kuni)";
        }

        if (day.IsFlexible && day.RequiredMinutes is { } required)
        {
            text += string.Create(CultureInfo.InvariantCulture, $", erkin {required / 60}:{required % 60:00}");
        }

        return text;
    }

    private static string BreakOf(ScheduleChangeDayDto day)
    {
        if (!day.IsWorking)
        {
            return NoValue;
        }

        if (day.BreakStart is { } from && day.BreakEnd is { } to)
        {
            var range = $"{TimeText(from)}–{TimeText(to)}";
            var window = ((to.Hour * 60) + to.Minute - (from.Hour * 60) - from.Minute + MinutesPerDay) % MinutesPerDay;

            // Ayiriladigan tanaffus oynadan farq qilsa — daqiqasi ham (60 daqiqalik tushlik 12:00–15:00 oynasida)
            return day.BreakMinutes == window
                ? range
                : string.Create(CultureInfo.InvariantCulture, $"{day.BreakMinutes} daq ({range} oralig'ida)");
        }

        return day.BreakMinutes > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{day.BreakMinutes} daq")
            : NoValue;
    }

    private static string DateText(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    private static string TimeText(TimeOnly? time)
        => time is { } value ? value.ToString("HH:mm", CultureInfo.InvariantCulture) : NoValue;

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
        DateOnly? To)
    {
        public static ExportAuditFilters Of(SearchScheduleChangeRequestsQuery query) => new(
            string.IsNullOrWhiteSpace(query.Search) ? null : true,
            query.EmployeeId,
            query.Status,
            query.Decided,
            query.From,
            query.To);
    }
}
