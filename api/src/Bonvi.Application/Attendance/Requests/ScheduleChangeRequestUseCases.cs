using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Recompute;
using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Requests;

// ════════════════════════════════════════════════════════════════════
//  Jadvalni o'zgartirish so'rovlari — SPEC-verifikatsiya-jadval-sorovi §C
// ════════════════════════════════════════════════════════════════════

/// <summary>Jadval so'rovlarining o'qish porti.</summary>
public interface IScheduleChangeRequestReadModel
{
    /// <summary>Sahifalangan ro'yxat — yangisi birinchi.</summary>
    Task<AttendancePage<ScheduleChangeRequestDto>> SearchScheduleChangeRequestsAsync(
        ScheduleChangeRequestFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>Bitta so'rov; yo'q bo'lsa <c>null</c>.</summary>
    Task<ScheduleChangeRequestDto?> FindScheduleChangeRequestAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

/// <summary>Jadval so'rovlari ro'yxatining filtri.</summary>
/// <param name="EmployeeId">Bitta xodim (karta).</param>
/// <param name="Status">Holat.</param>
/// <param name="Decided">Qaror qabul qilinganlar (true) yoki kutayotganlar (false).</param>
/// <param name="From">Kunlaridan biri shu sanadan keyin.</param>
/// <param name="To">Kunlaridan biri shu sanadan oldin.</param>
/// <param name="Search">Xodim bo'yicha qidiruv.</param>
/// <param name="Page">Sahifa (1 dan).</param>
/// <param name="PageSize">Sahifa hajmi.</param>
public sealed record ScheduleChangeRequestFilter(
    Guid? EmployeeId = null,
    AbsenceRequestStatus? Status = null,
    bool? Decided = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 25)
{
    /// <summary>Nechta yozuv o'tkazib yuboriladi.</summary>
    public int Skip => (Page - 1) * PageSize;
}

/// <summary>Kun almashtirishdagi bitta juft.</summary>
/// <param name="Date">Birinchi sana.</param>
/// <param name="WithDate">U bilan o'rin almashadigan sana.</param>
public sealed record ScheduleSwapInput(DateOnly Date, DateOnly WithDate);

internal static class ScheduleChangeRequestErrors
{
    public static readonly Error NotFound =
        Error.NotFound("davomat.sorov_topilmadi", "So'rov topilmadi.");

    public static readonly Error EmployeeNotFound =
        Error.NotFound("davomat.xodim_topilmadi", "Bunday xodim topilmadi.");

    public static readonly Error Overlaps =
        Error.Conflict("davomat.jadval_sorovi_takroriy", "Bu sanalarning biri uchun xodimda faol jadval so'rovi allaqachon bor.");

    public static readonly Error NoSchedule =
        Error.Validation("davomat.jadval_sorovi_jadval_yoq", "Almashtiriladigan sanada xodimga jadval biriktirilmagan.");

    public static readonly Error SwapSameDate =
        Error.Validation("davomat.jadval_sorovi_bir_xil_sana", "Kun o'zi bilan almashtirilmaydi.");

    /// <summary>
    /// Holat, keyin mijoz ko'rgan versiya — tahrir va qarorning boshida (§3.3 ustuvorlik). Kodlar
    /// yo'qlik so'rovidagining o'zi (<c>davomat.sorov_qaror_qabul_qilingan</c>, <c>davomat.sorov_ozgargan</c>).
    /// </summary>
    public static Error? PendingAt(ScheduleChangeRequest request, int? expectedVersion)
        => AbsenceRequestErrors.PendingAt(request.Status, request.Version, expectedVersion);

    /// <summary>Saqlash; parallel yozuv yutgan bo'lsa — 409 <c>davomat.sorov_ozgargan</c> (§3.3, 1-qatlam).</summary>
    public static Task<Error?> SaveAsync(IUnitOfWork unitOfWork, IDatabaseFault fault, CancellationToken cancellationToken)
        => UpdateAbsenceRequestCommandHandler.SaveAsync(unitOfWork, fault, cancellationToken);
}

/// <summary>
/// Xodimning sanalardagi AMALDAGI rejasi — tabel hisoblagan manbaning o'zidan
/// (biriktirma, to'r katagi, tasdiqlangan jadval so'rovi).
/// </summary>
/// <remarks>
/// Ikki joyda kerak: kun almashtirishda ikkala sana rejasini suratga olish va ilova
/// formasini xodimning hozirgi soatlari bilan to'ldirish (<c>GET /api/me/plan</c>).
/// </remarks>
public sealed class EmployeePlanReader
{
    private readonly IAttendanceComputationSource _source;

    public EmployeePlanReader(IAttendanceComputationSource source) => _source = source;

    /// <summary>Har bir sana uchun reja.</summary>
    /// <param name="employeeId">Xodim.</param>
    /// <param name="dates">Sanalar.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<IReadOnlyDictionary<DateOnly, DayPlanDto>> ReadAsync(
        Guid employeeId,
        IReadOnlyCollection<DateOnly> dates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dates);

        if (dates.Count == 0)
        {
            return new Dictionary<DateOnly, DayPlanDto>();
        }

        var employments = await _source.LoadEmploymentsAsync([employeeId], cancellationToken).ConfigureAwait(false);
        var list = employments.GetValueOrDefault(employeeId) ?? [];

        var catalog = await _source
            .LoadScheduleCatalogAsync(dates.Min(), dates.Max(), cancellationToken)
            .ConfigureAwait(false);

        var resolver = new ScheduleResolver(
            catalog.Assignments,
            catalog.Schedules,
            new DepartmentTree(catalog.Departments),
            catalog.Dates,
            catalog.Overrides);

        var result = new Dictionary<DateOnly, DayPlanDto>();

        foreach (var date in dates.Distinct())
        {
            var primary = PrimaryEmployment.Select(list, date);
            var plan = primary is null ? null : resolver.Resolve(primary, date)?.Day;

            result[date] = plan is null
                ? new DayPlanDto(date, false, false, null, null, null, null)
                : new DayPlanDto(
                    date,
                    true,
                    plan.IsWorking,
                    LocalTime(plan.PlanStartAt),
                    LocalTime(plan.PlanEndAt),
                    LocalTime(plan.BreakWindowStartAt),
                    LocalTime(plan.BreakWindowEndAt),
                    plan.IsWorking ? plan.BreakMinutes : 0,
                    plan.IsFlexible,
                    plan.IsFlexible ? plan.PlannedMinutes : null);
        }

        return result;
    }

    private static TimeOnly? LocalTime(DateTimeOffset? instant)
        => instant is { } value ? TimeOnly.FromDateTime(value.ToTashkent().DateTime) : null;
}

/// <summary>
/// So'rov kunlarini quradi: «kunni o'zgartirish» — kiritilganining o'zi, «kun almashtirish» —
/// ikkala sananing amaldagi rejasi o'rin almashib (SPEC §C1).
/// </summary>
internal static class ScheduleChangeDays
{
    public static async Task<Result<IReadOnlyList<ScheduleChangeDayInput>>> BuildAsync(
        EmployeePlanReader plans,
        Guid employeeId,
        ScheduleChangeKind kind,
        IReadOnlyCollection<ScheduleChangeDayInput>? days,
        IReadOnlyCollection<ScheduleSwapInput>? swaps,
        CancellationToken cancellationToken)
    {
        var isSwap = kind == ScheduleChangeKind.Swap;
        var pairs = swaps ?? [];

        if (isSwap && pairs.Any(pair => pair.Date == pair.WithDate))
        {
            return ScheduleChangeRequestErrors.SwapSameDate;
        }

        // ⭐ Shakl har qanday o'qishdan OLDIN: rejalar har bir sana uchun o'qiladi, takror
        // tekshiruvi esa sanalar ro'yxati bilan bazaga boradi — cheklanmagan ro'yxat yoki keng
        // davr 2 GB serverni og'irlashtirardi (domen ham shu qoidani qaytaradi).
        var shape = ScheduleChangeRequest.ValidateDates(
            kind,
            isSwap
                ? [.. pairs.SelectMany(pair => new[] { pair.Date, pair.WithDate })]
                : [.. (days ?? []).Select(day => day.Date)]);
        if (shape.IsFailure)
        {
            return shape.Error;
        }

        if (!isSwap)
        {
            // Juft maydoni «kunni o'zgartirish» da ma'nosiz — mijoz yuborsa ham olinmaydi.
            return Result.Success<IReadOnlyList<ScheduleChangeDayInput>>(
                [.. (days ?? []).Select(day => day with { SwapWith = null })]);
        }

        var current = await plans
            .ReadAsync(employeeId, [.. pairs.SelectMany(pair => new[] { pair.Date, pair.WithDate })], cancellationToken)
            .ConfigureAwait(false);

        var built = new List<ScheduleChangeDayInput>(pairs.Count * 2);

        foreach (var pair in pairs)
        {
            if (current[pair.Date] is not { HasSchedule: true } first
                || current[pair.WithDate] is not { HasSchedule: true } second)
            {
                return ScheduleChangeRequestErrors.NoSchedule;
            }

            built.Add(Take(pair.Date, second, pair.WithDate));
            built.Add(Take(pair.WithDate, first, pair.Date));
        }

        return built;

        // Tanaffus daqiqasi jadvalnikidek ko'chadi — oynadan qayta hisoblanmaydi. Erkinlik va
        // «Ish vaqti» ham (SPEC-erkin-jadval §4): juft sananing kuni butunligicha o'tadi.
        static ScheduleChangeDayInput Take(DateOnly date, DayPlanDto from, DateOnly swapWith)
            => from.IsWorking
                ? new ScheduleChangeDayInput(
                    date,
                    true,
                    from.StartTime,
                    from.EndTime,
                    from.BreakStart,
                    from.BreakEnd,
                    swapWith,
                    from.BreakMinutes,
                    from.IsFlexible,
                    from.RequiredMinutes)
                : new ScheduleChangeDayInput(date, false, SwapWith: swapWith);
    }

    /// <summary>Qaror tabelga ta'sir qiladigan sanalar: har bir kun ±1 (tungi smena taqsimoti).</summary>
    public static IReadOnlyList<DateOnly> AffectedDates(ScheduleChangeRequest request)
        => [.. request.Days
            .SelectMany(day => new[] { day.Date.AddDays(-1), day.Date, day.Date.AddDays(1) })
            .Distinct()
            .Order()];
}

/// <summary>Yangi jadval so'rovi — HR xodim nomidan yoki (ilova orqali) xodimning o'zi.</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="Kind">Tur.</param>
/// <param name="Days">«Kunni o'zgartirish» kunlari.</param>
/// <param name="Swaps">«Kun almashtirish» juftlari.</param>
/// <param name="Note">Izoh.</param>
/// <param name="SubmittedByAccountId">Ilovadan yuborgan xodim hisobi.</param>
public sealed record CreateScheduleChangeRequestCommand(
    Guid EmployeeId,
    ScheduleChangeKind Kind,
    IReadOnlyList<ScheduleChangeDayInput>? Days,
    IReadOnlyList<ScheduleSwapInput>? Swaps,
    string? Note,
    Guid? SubmittedByAccountId = null);

/// <summary><see cref="CreateScheduleChangeRequestCommand"/> ni bajaradi.</summary>
public sealed class CreateScheduleChangeRequestCommandHandler
{
    private readonly IScheduleChangeRequestRepository _requests;
    private readonly IEmployeeRepository _employees;
    private readonly EmployeePlanReader _plans;
    private readonly RequestBreakRuleReader _breakRule;
    private readonly IScheduleChangeRequestReadModel _readModel;
    private readonly IUnitOfWork _unitOfWork;

    public CreateScheduleChangeRequestCommandHandler(
        IScheduleChangeRequestRepository requests,
        IEmployeeRepository employees,
        EmployeePlanReader plans,
        RequestBreakRuleReader breakRule,
        IScheduleChangeRequestReadModel readModel,
        IUnitOfWork unitOfWork)
    {
        _requests = requests;
        _employees = employees;
        _plans = plans;
        _breakRule = breakRule;
        _readModel = readModel;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ScheduleChangeRequestDto>> HandleAsync(
        CreateScheduleChangeRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.EmployeeId == Guid.Empty
            || !await _employees.ExistsAsync(command.EmployeeId, cancellationToken).ConfigureAwait(false))
        {
            return ScheduleChangeRequestErrors.EmployeeNotFound;
        }

        var days = await ScheduleChangeDays
            .BuildAsync(_plans, command.EmployeeId, command.Kind, command.Days, command.Swaps, cancellationToken)
            .ConfigureAwait(false);

        if (days.IsFailure)
        {
            return days.Error;
        }

        // Tanaffus qoidasi yozish lahzasidagi sozlamadan — ilova va HR uchun bitta (SPEC-sorov-tahriri §4.2).
        var created = ScheduleChangeRequest.Create(
            command.EmployeeId,
            command.Kind,
            days.Value,
            command.Note,
            command.SubmittedByAccountId,
            await _breakRule.ReadAsync(cancellationToken).ConfigureAwait(false));

        if (created.IsFailure)
        {
            return created.Error;
        }

        var request = created.Value;

        if (await _requests
                .OverlapsAsync(request.EmployeeId, [.. request.Days.Select(day => day.Date)], null, cancellationToken)
                .ConfigureAwait(false))
        {
            return ScheduleChangeRequestErrors.Overlaps;
        }

        _requests.Add(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (await _readModel.FindScheduleChangeRequestAsync(request.Id, cancellationToken).ConfigureAwait(false))!;
    }
}

/// <summary>Qaror kutayotgan jadval so'rovini tahrirlash — HR yoki (ilova orqali) xodimning o'zi.</summary>
/// <param name="RequestId">So'rov.</param>
/// <param name="Kind">Tur.</param>
/// <param name="Days">Kunlar.</param>
/// <param name="Swaps">Juftlar.</param>
/// <param name="Note">Izoh.</param>
/// <param name="ExpectedVersion">Mijoz ko'rgan <c>version</c>; farq qilsa — 409 <c>davomat.sorov_ozgargan</c>.</param>
/// <param name="EditedByAccountId">Ilovadan tahrirlagan xodim hisobi; HR tahririda <c>null</c>.</param>
public sealed record UpdateScheduleChangeRequestCommand(
    Guid RequestId,
    ScheduleChangeKind Kind,
    IReadOnlyList<ScheduleChangeDayInput>? Days,
    IReadOnlyList<ScheduleSwapInput>? Swaps,
    string? Note,
    int? ExpectedVersion = null,
    Guid? EditedByAccountId = null);

/// <summary><see cref="UpdateScheduleChangeRequestCommand"/> ni bajaradi.</summary>
public sealed class UpdateScheduleChangeRequestCommandHandler
{
    private readonly IScheduleChangeRequestRepository _requests;
    private readonly EmployeePlanReader _plans;
    private readonly RequestBreakRuleReader _breakRule;
    private readonly IScheduleChangeRequestReadModel _readModel;
    private readonly IClock _clock;
    private readonly IDatabaseFault _fault;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateScheduleChangeRequestCommandHandler(
        IScheduleChangeRequestRepository requests,
        EmployeePlanReader plans,
        RequestBreakRuleReader breakRule,
        IScheduleChangeRequestReadModel readModel,
        IClock clock,
        IDatabaseFault fault,
        IUnitOfWork unitOfWork)
    {
        _requests = requests;
        _plans = plans;
        _breakRule = breakRule;
        _readModel = readModel;
        _clock = clock;
        _fault = fault;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ScheduleChangeRequestDto>> HandleAsync(
        UpdateScheduleChangeRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = await _requests.GetByIdAsync(command.RequestId, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return ScheduleChangeRequestErrors.NotFound;
        }

        // Holat va versiya rejalarni o'qishdan OLDIN (SPEC-sorov-tahriri §3.3 ustuvorlik).
        if (ScheduleChangeRequestErrors.PendingAt(request, command.ExpectedVersion) is { } notEditable)
        {
            return notEditable;
        }

        // Almashtirish kunlari tahrir lahzasidagi JORIY rejalardan qayta suratga olinadi.
        var days = await ScheduleChangeDays
            .BuildAsync(_plans, request.EmployeeId, command.Kind, command.Days, command.Swaps, cancellationToken)
            .ConfigureAwait(false);

        if (days.IsFailure)
        {
            return days.Error;
        }

        if (await _requests
                .OverlapsAsync(request.EmployeeId, [.. days.Value.Select(day => day.Date)], request.Id, cancellationToken)
                .ConfigureAwait(false))
        {
            return ScheduleChangeRequestErrors.Overlaps;
        }

        var updated = request.Update(
            command.Kind,
            days.Value,
            command.Note,
            await _breakRule.ReadAsync(cancellationToken).ConfigureAwait(false),
            _clock.UtcNow,
            command.EditedByAccountId);
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        // Kutayotgan so'rov tabelga ta'sir qilmaydi — tahrir hech narsani qayta hisoblatmaydi.
        if (await ScheduleChangeRequestErrors.SaveAsync(_unitOfWork, _fault, cancellationToken).ConfigureAwait(false)
            is { } stale)
        {
            return stale;
        }

        return (await _readModel.FindScheduleChangeRequestAsync(request.Id, cancellationToken).ConfigureAwait(false))!;
    }
}

/// <summary>Qaror: tasdiqlash yoki rad etish.</summary>
/// <param name="RequestId">So'rov.</param>
/// <param name="Approve">Tasdiqlansinmi.</param>
/// <param name="DecisionNote">HR izohi.</param>
/// <param name="ExpectedVersion">HR ko'rgan <c>version</c>; farq qilsa — 409 <c>davomat.sorov_ozgargan</c>.</param>
public sealed record DecideScheduleChangeRequestCommand(
    Guid RequestId,
    bool Approve,
    string? DecisionNote,
    int? ExpectedVersion = null);

/// <summary><see cref="DecideScheduleChangeRequestCommand"/> ni bajaradi.</summary>
public sealed class DecideScheduleChangeRequestCommandHandler
{
    private readonly IScheduleChangeRequestRepository _requests;
    private readonly IScheduleChangeRequestReadModel _readModel;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly AttendanceDayRecomputer _recomputer;
    private readonly IDatabaseFault _fault;
    private readonly IUnitOfWork _unitOfWork;

    public DecideScheduleChangeRequestCommandHandler(
        IScheduleChangeRequestRepository requests,
        IScheduleChangeRequestReadModel readModel,
        ICurrentUser currentUser,
        IClock clock,
        AttendanceDayRecomputer recomputer,
        IDatabaseFault fault,
        IUnitOfWork unitOfWork)
    {
        _requests = requests;
        _readModel = readModel;
        _currentUser = currentUser;
        _clock = clock;
        _recomputer = recomputer;
        _fault = fault;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ScheduleChangeRequestDto>> HandleAsync(
        DecideScheduleChangeRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = await _requests.GetByIdAsync(command.RequestId, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return ScheduleChangeRequestErrors.NotFound;
        }

        // Mavjud so'rovlar tanaffus qoidasi bo'yicha qayta tekshirilmaydi — faqat holat va versiya.
        if (ScheduleChangeRequestErrors.PendingAt(request, command.ExpectedVersion) is { } notDecidable)
        {
            return notDecidable;
        }

        var decidedBy = _currentUser.UserId ?? Guid.Empty;
        var now = _clock.UtcNow;

        var decision = command.Approve
            ? request.Approve(decidedBy, now, command.DecisionNote)
            : request.Reject(decidedBy, now, command.DecisionNote);

        if (decision.IsFailure)
        {
            return decision.Error;
        }

        if (await ScheduleChangeRequestErrors.SaveAsync(_unitOfWork, _fault, cancellationToken).ConfigureAwait(false)
            is { } stale)
        {
            return stale;
        }

        // Faqat tasdiq rejani o'zgartiradi; rad etish kunlarga tegmaydi.
        if (command.Approve)
        {
            await _recomputer
                .RecomputeAsync([request.EmployeeId], ScheduleChangeDays.AffectedDates(request), cancellationToken)
                .ConfigureAwait(false);
        }

        return (await _readModel.FindScheduleChangeRequestAsync(request.Id, cancellationToken).ConfigureAwait(false))!;
    }
}

/// <summary>HR bekor qiladi (tasdiqlanganini ham).</summary>
/// <param name="RequestId">So'rov.</param>
public sealed record CancelScheduleChangeRequestCommand(Guid RequestId);

/// <summary><see cref="CancelScheduleChangeRequestCommand"/> ni bajaradi.</summary>
public sealed class CancelScheduleChangeRequestCommandHandler
{
    private readonly IScheduleChangeRequestRepository _requests;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly AttendanceDayRecomputer _recomputer;
    private readonly IDatabaseFault _fault;
    private readonly IUnitOfWork _unitOfWork;

    public CancelScheduleChangeRequestCommandHandler(
        IScheduleChangeRequestRepository requests,
        ICurrentUser currentUser,
        IClock clock,
        AttendanceDayRecomputer recomputer,
        IDatabaseFault fault,
        IUnitOfWork unitOfWork)
    {
        _requests = requests;
        _currentUser = currentUser;
        _clock = clock;
        _recomputer = recomputer;
        _fault = fault;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        CancelScheduleChangeRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = await _requests.GetByIdAsync(command.RequestId, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return ScheduleChangeRequestErrors.NotFound;
        }

        var wasApproved = request.Status == AbsenceRequestStatus.Approved;

        var cancelled = request.Cancel(_currentUser.UserId ?? Guid.Empty, _clock.UtcNow);
        if (cancelled.IsFailure)
        {
            return cancelled.Error;
        }

        if (await ScheduleChangeRequestErrors.SaveAsync(_unitOfWork, _fault, cancellationToken).ConfigureAwait(false)
            is { } stale)
        {
            return stale;
        }

        // Tasdiqlangan reja olib tashlandi — kunlar jadvalning o'z rejasiga qaytadi.
        if (wasApproved)
        {
            await _recomputer
                .RecomputeAsync([request.EmployeeId], ScheduleChangeDays.AffectedDates(request), cancellationToken)
                .ConfigureAwait(false);
        }

        return Result.Success();
    }
}

/// <summary>Jadval so'rovlari ro'yxati.</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="Status">Holat.</param>
/// <param name="Decided">Qaror qabul qilinganlar.</param>
/// <param name="From">Davr boshi.</param>
/// <param name="To">Davr oxiri.</param>
/// <param name="Search">Xodim bo'yicha qidiruv.</param>
/// <param name="Page">Sahifa.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
public sealed record SearchScheduleChangeRequestsQuery(
    Guid? EmployeeId = null,
    AbsenceRequestStatus? Status = null,
    bool? Decided = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Search = null,
    int? Page = null,
    int? PageSize = null);

/// <summary><see cref="SearchScheduleChangeRequestsQuery"/> ni bajaradi.</summary>
public sealed class SearchScheduleChangeRequestsQueryHandler
{
    private readonly IScheduleChangeRequestReadModel _readModel;

    public SearchScheduleChangeRequestsQueryHandler(IScheduleChangeRequestReadModel readModel) => _readModel = readModel;

    public async Task<Result<PagedResult<ScheduleChangeRequestDto>>> HandleAsync(
        SearchScheduleChangeRequestsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = AttendanceQueryParsing.Page(query.Page);
        var pageSize = AttendanceQueryParsing.PageSize(query.PageSize);

        var filter = BuildFilter(query, page, pageSize);
        if (filter.IsFailure)
        {
            return filter.Error;
        }

        var found = await _readModel
            .SearchScheduleChangeRequestsAsync(filter.Value, cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<ScheduleChangeRequestDto>(
            found.Items,
            page,
            pageSize,
            found.Total,
            AttendanceQueryParsing.PageCount(found.Total, pageSize));
    }

    /// <summary>
    /// So'rovni filtrga aylantiradi — ekran ham, Excel eksporti ham AYNAN shu metodni chaqiradi.
    /// </summary>
    /// <remarks>
    /// <see cref="SearchAbsenceRequestsQueryHandler.BuildFilter"/> bilan bir xil sabab:
    /// <see cref="Reports.Queries.ExportScheduleChangeRequestsQueryHandler"/> filtrning
    /// ikkinchi nusxasini yozmaydi.
    /// </remarks>
    internal static Result<ScheduleChangeRequestFilter> BuildFilter(
        SearchScheduleChangeRequestsQuery query,
        int page,
        int pageSize)
    {
        if (query.From is { } from && query.To is { } to && from > to)
        {
            return AttendanceErrors.PeriodReversed;
        }

        return new ScheduleChangeRequestFilter(
            query.EmployeeId,
            query.Status,
            query.Decided,
            query.From,
            query.To,
            string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            page,
            pageSize);
    }
}

/// <summary>Bitta jadval so'rovi.</summary>
/// <param name="RequestId">So'rov.</param>
public sealed record GetScheduleChangeRequestQuery(Guid RequestId);

/// <summary><see cref="GetScheduleChangeRequestQuery"/> ni bajaradi.</summary>
public sealed class GetScheduleChangeRequestQueryHandler
{
    private readonly IScheduleChangeRequestReadModel _readModel;

    public GetScheduleChangeRequestQueryHandler(IScheduleChangeRequestReadModel readModel) => _readModel = readModel;

    public async Task<Result<ScheduleChangeRequestDto>> HandleAsync(
        GetScheduleChangeRequestQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var dto = await _readModel.FindScheduleChangeRequestAsync(query.RequestId, cancellationToken).ConfigureAwait(false);

        return dto is null ? ScheduleChangeRequestErrors.NotFound : dto;
    }
}
