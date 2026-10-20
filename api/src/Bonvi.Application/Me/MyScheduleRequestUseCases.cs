using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Requests;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Me;

// ════════════════════════════════════════════════════════════════════
//  Xodim ilovasi — jadvalni o'zgartirish so'rovlari (SPEC-verifikatsiya-jadval-sorovi §C3)
// ════════════════════════════════════════════════════════════════════

/// <summary>Xodim o'zi so'ray oladigan sanalar oynasi — bugundan atrofida.</summary>
/// <remarks>
/// HR uchun chegara yo'q (o'tgan oy tabelini tuzatadi). Xodimga esa: o'tgan oy (kechikkan
/// so'rov uchun yetarli) va qayta hisoblash oynasicha oldinga. Shundan tashqari sana — HR
/// navbatini to'ldirish yoki uzoq o'tmishni qayta yozish, ikkalasi ham ilovadan kerak emas.
/// </remarks>
internal static class MyScheduleRequestWindow
{
    public const int PastDays = 31;
    public const int FutureDays = AttendanceRules.MaxRecomputeDays;

    public static readonly Error OutOfWindow = Error.Validation(
        "davomat.jadval_sorovi_sana_oynasi",
        $"Sana bugundan {PastDays} kun oldin va {FutureDays} kun keyin oralig'ida bo'lishi kerak.");

    public static bool Contains(DateOnly today, DateOnly date)
        => date >= today.AddDays(-PastDays) && date <= today.AddDays(FutureDays);
}

/// <summary>Xodimning o'z jadval so'rovlari.</summary>
/// <param name="Status">Holat.</param>
/// <param name="Page">Sahifa.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
public sealed record GetMyScheduleRequestsQuery(AbsenceRequestStatus? Status, int? Page, int? PageSize);

/// <summary><see cref="GetMyScheduleRequestsQuery"/> ni bajaradi — HR ro'yxatining o'zi, xodim bilan kesilgan.</summary>
public sealed class GetMyScheduleRequestsQueryHandler
{
    private readonly CurrentEmployeeResolver _resolver;
    private readonly SearchScheduleChangeRequestsQueryHandler _search;

    public GetMyScheduleRequestsQueryHandler(
        CurrentEmployeeResolver resolver,
        SearchScheduleChangeRequestsQueryHandler search)
    {
        _resolver = resolver;
        _search = search;
    }

    public async Task<Result<PagedResult<ScheduleChangeRequestDto>>> HandleAsync(
        GetMyScheduleRequestsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var employeeId = await _resolver.EmployeeIdAsync(cancellationToken).ConfigureAwait(false);
        if (employeeId.IsFailure)
        {
            return employeeId.Error;
        }

        return await _search
            .HandleAsync(
                new SearchScheduleChangeRequestsQuery(
                    EmployeeId: employeeId.Value,
                    Status: query.Status,
                    Page: query.Page,
                    PageSize: query.PageSize),
                cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>Xodim ilovadan jadval so'rovi yuboradi.</summary>
/// <param name="Kind">Tur.</param>
/// <param name="Days">«Kunni o'zgartirish» kunlari.</param>
/// <param name="Swaps">«Kun almashtirish» juftlari.</param>
/// <param name="Note">Izoh.</param>
public sealed record CreateMyScheduleRequestCommand(
    ScheduleChangeKind Kind,
    IReadOnlyList<ScheduleChangeDayInput>? Days,
    IReadOnlyList<ScheduleSwapInput>? Swaps,
    string? Note);

/// <summary><see cref="CreateMyScheduleRequestCommand"/> ni bajaradi — xodim tokendan, muallif hisobi bilan.</summary>
public sealed class CreateMyScheduleRequestCommandHandler
{
    private readonly CurrentEmployeeResolver _resolver;
    private readonly CreateScheduleChangeRequestCommandHandler _create;
    private readonly IClock _clock;

    public CreateMyScheduleRequestCommandHandler(
        CurrentEmployeeResolver resolver,
        CreateScheduleChangeRequestCommandHandler create,
        IClock clock)
    {
        _resolver = resolver;
        _create = create;
        _clock = clock;
    }

    public async Task<Result<ScheduleChangeRequestDto>> HandleAsync(
        CreateMyScheduleRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ids = await _resolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (ids.IsFailure)
        {
            return ids.Error;
        }

        // Faqat xodimning sanalarini qaraydi; ro'yxat hajmi va davr umumiy qoidada (o'qishdan oldin).
        if (UpdateMyScheduleRequestCommandHandler.OutsideWindow(command.Kind, command.Days, command.Swaps, _clock.Today))
        {
            return MyScheduleRequestWindow.OutOfWindow;
        }

        return await _create
            .HandleAsync(
                new CreateScheduleChangeRequestCommand(
                    ids.Value.EmployeeId,
                    command.Kind,
                    command.Days,
                    command.Swaps,
                    command.Note,
                    SubmittedByAccountId: ids.Value.AccountId),
                cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>Xodim o'zining KUTAYOTGAN jadval so'rovini o'zgartiradi (SPEC-verifikatsiya-sorov-tahriri §3).</summary>
/// <param name="RequestId">So'rov.</param>
/// <param name="Version">Ilova ko'rsatgan <c>version</c> — majburiy.</param>
/// <param name="Kind">Tur — o'zgarishi mumkin (Change ⇄ Swap).</param>
/// <param name="Days">«Kunni o'zgartirish» kunlari.</param>
/// <param name="Swaps">«Kun almashtirish» juftlari.</param>
/// <param name="Note">Izoh.</param>
public sealed record UpdateMyScheduleRequestCommand(
    Guid RequestId,
    int Version,
    ScheduleChangeKind Kind,
    IReadOnlyList<ScheduleChangeDayInput>? Days,
    IReadOnlyList<ScheduleSwapInput>? Swaps,
    string? Note);

/// <summary>
/// <see cref="UpdateMyScheduleRequestCommand"/> ni bajaradi — egalik, holat, versiya va xodimning sana
/// oynasi shu yerda, qolgan hamma qoida (shakl, almashtirishning qayta surati, tanaffus qoidasi,
/// kesishish, izoh) HR tahrir ishlovchisida (SPEC-sorov-tahriri §3.4).
/// </summary>
/// <remarks>
/// Tartib: 404 → 409 holat → 409 versiya → 400 <c>davomat.jadval_sorovi_sana_oynasi</c> → HR ishlovchisi.
/// Begona so'rov — «topilmadi», bekor qilishdagi kabi.
/// </remarks>
public sealed class UpdateMyScheduleRequestCommandHandler
{
    private readonly CurrentEmployeeResolver _resolver;
    private readonly IScheduleChangeRequestRepository _requests;
    private readonly UpdateScheduleChangeRequestCommandHandler _update;
    private readonly IClock _clock;

    public UpdateMyScheduleRequestCommandHandler(
        CurrentEmployeeResolver resolver,
        IScheduleChangeRequestRepository requests,
        UpdateScheduleChangeRequestCommandHandler update,
        IClock clock)
    {
        _resolver = resolver;
        _requests = requests;
        _update = update;
        _clock = clock;
    }

    public async Task<Result<ScheduleChangeRequestDto>> HandleAsync(
        UpdateMyScheduleRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ids = await _resolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (ids.IsFailure)
        {
            return ids.Error;
        }

        var request = await _requests.GetByIdAsync(command.RequestId, cancellationToken).ConfigureAwait(false);
        if (request is null || request.EmployeeId != ids.Value.EmployeeId)
        {
            return ScheduleChangeRequestErrors.NotFound;
        }

        if (ScheduleChangeRequestErrors.PendingAt(request, command.Version) is { } notEditable)
        {
            return notEditable;
        }

        if (OutsideWindow(command.Kind, command.Days, command.Swaps, _clock.Today))
        {
            return MyScheduleRequestWindow.OutOfWindow;
        }

        return await _update
            .HandleAsync(
                new UpdateScheduleChangeRequestCommand(
                    request.Id,
                    command.Kind,
                    command.Days,
                    command.Swaps,
                    command.Note,
                    ExpectedVersion: command.Version,
                    EditedByAccountId: ids.Value.AccountId),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Xodim sanalaridan birortasi oynadan tashqaridami — yaratishdagi tekshiruvning o'zi.</summary>
    internal static bool OutsideWindow(
        ScheduleChangeKind kind,
        IReadOnlyList<ScheduleChangeDayInput>? days,
        IReadOnlyList<ScheduleSwapInput>? swaps,
        DateOnly today)
        => (kind == ScheduleChangeKind.Swap
                ? (swaps ?? []).SelectMany(pair => new[] { pair.Date, pair.WithDate })
                : (days ?? []).Select(day => day.Date))
            .Take(ScheduleChangeRequest.MaxDays + 1)
            .Any(date => !MyScheduleRequestWindow.Contains(today, date));
}

/// <summary>Xodim o'z jadval so'rovini qaytarib oladi.</summary>
/// <param name="RequestId">So'rov.</param>
public sealed record CancelMyScheduleRequestCommand(Guid RequestId);

/// <summary>
/// <see cref="CancelMyScheduleRequestCommand"/> ni bajaradi — faqat o'ziniki va faqat qaror kutayotgani
/// (yo'qlik so'rovidagi bilan bir xil qoida: begona so'rov «topilmadi»).
/// </summary>
public sealed class CancelMyScheduleRequestCommandHandler
{
    private readonly CurrentEmployeeResolver _resolver;
    private readonly IScheduleChangeRequestRepository _requests;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IDatabaseFault _fault;

    public CancelMyScheduleRequestCommandHandler(
        CurrentEmployeeResolver resolver,
        IScheduleChangeRequestRepository requests,
        IUnitOfWork unitOfWork,
        IClock clock,
        IDatabaseFault fault)
    {
        _resolver = resolver;
        _requests = requests;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _fault = fault;
    }

    public async Task<Result> HandleAsync(
        CancelMyScheduleRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ids = await _resolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (ids.IsFailure)
        {
            return ids.Error;
        }

        var request = await _requests.GetByIdAsync(command.RequestId, cancellationToken).ConfigureAwait(false);
        if (request is null || request.EmployeeId != ids.Value.EmployeeId)
        {
            return Error.NotFound("davomat.sorov_topilmadi", "So'rov topilmadi.");
        }

        var cancelled = request.CancelBySubmitter(ids.Value.AccountId, _clock.UtcNow);
        if (cancelled.IsFailure)
        {
            return cancelled.Error;
        }

        if (await ScheduleChangeRequestErrors.SaveAsync(_unitOfWork, _fault, cancellationToken).ConfigureAwait(false)
            is { } stale)
        {
            return stale;
        }

        return Result.Success();
    }
}

/// <summary>Xodimning bir sanadagi amaldagi rejasi.</summary>
/// <param name="Date">Sana.</param>
public sealed record GetMyPlanQuery(DateOnly Date);

/// <summary><see cref="GetMyPlanQuery"/> ni bajaradi — jadval so'rovi formasini to'ldirish uchun.</summary>
public sealed class GetMyPlanQueryHandler
{
    private readonly CurrentEmployeeResolver _resolver;
    private readonly EmployeePlanReader _plans;
    private readonly IClock _clock;

    public GetMyPlanQueryHandler(CurrentEmployeeResolver resolver, EmployeePlanReader plans, IClock clock)
    {
        _resolver = resolver;
        _plans = plans;
        _clock = clock;
    }

    public async Task<Result<DayPlanDto>> HandleAsync(GetMyPlanQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!MyScheduleRequestWindow.Contains(_clock.Today, query.Date))
        {
            return MyScheduleRequestWindow.OutOfWindow;
        }

        var employeeId = await _resolver.EmployeeIdAsync(cancellationToken).ConfigureAwait(false);
        if (employeeId.IsFailure)
        {
            return employeeId.Error;
        }

        var plans = await _plans.ReadAsync(employeeId.Value, [query.Date], cancellationToken).ConfigureAwait(false);

        return plans[query.Date];
    }
}
