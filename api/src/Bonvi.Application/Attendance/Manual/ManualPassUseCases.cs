using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Recompute;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Manual;

/// <summary>Qo'lda belgi xatolari (SPEC-qolda-belgi §B).</summary>
public static class ManualPassErrors
{
    public static readonly Error EmployeeNotFound =
        Error.NotFound("davomat.xodim_topilmadi", "Bunday xodim topilmadi.");

    public static readonly Error PassNotFound =
        Error.NotFound("davomat.otish_topilmadi", "Belgi topilmadi.");

    public static readonly Error NotManual =
        Error.Validation("davomat.belgi_qolda_emas", "Faqat HR qo'lda qo'shgan belgini o'chirish mumkin — terminal o'tishi o'zgarmaydi.");

    public static readonly Error InFuture =
        Error.Validation("davomat.belgi_kelajakda", "Kelajakdagi vaqtga belgi qo'yib bo'lmaydi.");

    public static readonly Error TooOld =
        Error.Validation(
            "davomat.belgi_juda_eski",
            $"Belgi {AttendanceRules.MaxRecomputeDays} kundan eski sanaga qo'yilmaydi.");

    public static readonly Error NotEmployed =
        Error.Validation("davomat.belgi_ishlamagan", "Xodim bu sanada ishlamagan — belgi qo'yib bo'lmaydi.");

    public static readonly Error Duplicate =
        Error.Conflict("davomat.belgi_takror", "Shu daqiqada shu yo'nalishdagi belgi allaqachon bor.");
}

/// <summary>HR qo'lda belgi qo'shadi.</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="Date">Toshkent sanasi.</param>
/// <param name="Time">Toshkent vaqti (daqiqagacha; soniyalar tashlanadi).</param>
/// <param name="Direction">Kirish yoki chiqish.</param>
/// <param name="Note">Sabab — majburiy.</param>
public sealed record AddManualPassCommand(
    Guid EmployeeId,
    DateOnly Date,
    TimeOnly Time,
    PassDirection Direction,
    string? Note);

/// <summary>
/// HR qo'lda belgi qo'shadi va kunlarni SHU YERNING O'ZIDA qayta hisoblaydi (SPEC-qolda-belgi §B).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ Terminal o'tishi hech qachon tahrirlanmaydi (egasi, 2026-11-03): tuzatish faqat YANGI
/// belgi bilan. Belgi oddiy o'tish bo'lib yoziladi, ya'ni tabel uni terminal o'tishidek
/// juftlaydi; HR aytgan yo'nalish ma'lum yo'nalish sifatida ishlatiladi.
/// </para>
/// <para>
/// Qayta hisoblash sinxron (jadval so'rovi qarori bilan bir xil): HR oynani yopmasdan yangi
/// sonni ko'rishi kerak. Oraliq ±1 kun — tungi smenada belgi qo'shni kunga tushadi.
/// </para>
/// </remarks>
public sealed class AddManualPassCommandHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly IEmploymentRepository _employments;
    private readonly IAttendancePassRepository _passes;
    private readonly IAttendanceManualPassRepository _manual;
    private readonly IAttendanceReadModel _readModel;
    private readonly AttendanceDayRecomputer _recomputer;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDatabaseFault _databaseFault;
    private readonly IClock _clock;
    private readonly IAttendanceScope _scope;

    public AddManualPassCommandHandler(
        IEmployeeRepository employees,
        IEmploymentRepository employments,
        IAttendancePassRepository passes,
        IAttendanceManualPassRepository manual,
        IAttendanceReadModel readModel,
        AttendanceDayRecomputer recomputer,
        IUnitOfWork unitOfWork,
        IDatabaseFault databaseFault,
        IClock clock,
        IAttendanceScope scope)
    {
        _employees = employees;
        _employments = employments;
        _passes = passes;
        _manual = manual;
        _readModel = readModel;
        _recomputer = recomputer;
        _unitOfWork = unitOfWork;
        _databaseFault = databaseFault;
        _clock = clock;
        _scope = scope;
    }

    public async Task<Result<AttendancePassDto>> HandleAsync(
        AddManualPassCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Scope comes before every other rule, so a date error cannot confirm that someone
        // outside it exists. It also keeps `FindPassAsync` below, which applies the same
        // scope, from returning null on a row this request has just written.
        if (command.EmployeeId == Guid.Empty
            || !await _employees.ExistsAsync(command.EmployeeId, cancellationToken).ConfigureAwait(false)
            || !await _scope.CanAccessEmployeeAsync(command.EmployeeId, cancellationToken).ConfigureAwait(false))
        {
            return ManualPassErrors.EmployeeNotFound;
        }

        var now = _clock.UtcNow;
        var time = new TimeOnly(command.Time.Hour, command.Time.Minute);
        var occurredAt = new DateTimeOffset(command.Date.ToDateTime(time), BonviTime.TashkentOffset);

        if (occurredAt > now)
        {
            return ManualPassErrors.InFuture;
        }

        if (command.Date < now.TashkentDate().AddDays(-AttendanceRules.MaxRecomputeDays))
        {
            return ManualPassErrors.TooOld;
        }

        if (!await _employments.IsEmployedOnAsync(command.EmployeeId, command.Date, cancellationToken).ConfigureAwait(false))
        {
            return ManualPassErrors.NotEmployed;
        }

        var recorded = AttendancePass.RecordByHr(command.EmployeeId, occurredAt, now, command.Direction);
        if (recorded.IsFailure)
        {
            return recorded.Error;
        }

        var pass = recorded.Value;

        // Izoh tekshiruvi (bo'sh, uzun) saqlashdan OLDIN — yaroqsiz so'rov hech narsa yozmaydi.
        var evidence = AttendanceManualPass.Create(pass.Id, command.EmployeeId, pass.OccurredAt, pass.Direction, command.Note);
        if (evidence.IsFailure)
        {
            return evidence.Error;
        }

        _passes.Add(pass);
        _manual.Add(evidence.Value);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (_databaseFault.IsDuplicatePass(exception))
        {
            return ManualPassErrors.Duplicate;
        }

        await _recomputer
            .RecomputeAsync([command.EmployeeId], ManualPassDates.Around(pass.LocalDate), cancellationToken)
            .ConfigureAwait(false);

        await _passes.MarkProcessedAsync([pass.Id], _clock.UtcNow, cancellationToken).ConfigureAwait(false);

        return (await _readModel.FindPassAsync(pass.Id, cancellationToken).ConfigureAwait(false))!;
    }
}

/// <summary>HR qo'lda qo'shgan belgini o'chiradi.</summary>
/// <param name="PassId">O'tish.</param>
public sealed record DeleteManualPassCommand(Guid PassId);

/// <summary>
/// Faqat HR qo'shgan belgini o'chiradi (terminal o'tishi — 400) va kunlarni qayta hisoblaydi.
/// </summary>
/// <remarks>
/// Ikkala qator ham haqiqiy o'chiriladi; «kim, qachon, nega» audit jurnalida dalil qatori
/// (<see cref="AttendanceManualPass"/>) orqali qoladi. Usiz noto'g'ri yozilgan belgi abadiy
/// qolardi — teskari belgi uni bekor qilmaydi, faqat juftlikni buzadi.
/// </remarks>
public sealed class DeleteManualPassCommandHandler
{
    private readonly IAttendancePassRepository _passes;
    private readonly IAttendanceManualPassRepository _manual;
    private readonly AttendanceDayRecomputer _recomputer;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAttendanceScope _scope;

    public DeleteManualPassCommandHandler(
        IAttendancePassRepository passes,
        IAttendanceManualPassRepository manual,
        AttendanceDayRecomputer recomputer,
        IUnitOfWork unitOfWork,
        IAttendanceScope scope)
    {
        _passes = passes;
        _manual = manual;
        _recomputer = recomputer;
        _unitOfWork = unitOfWork;
        _scope = scope;
    }

    public async Task<Result> HandleAsync(
        DeleteManualPassCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pass = await _passes.GetByIdAsync(command.PassId, cancellationToken).ConfigureAwait(false);
        if (pass is null)
        {
            return ManualPassErrors.PassNotFound;
        }

        // Before the «not manual» rule: a 400 on someone else's terminal pass would confirm
        // that the id exists. Unmatched passes are hidden from a scoped user in the same way.
        if (_scope.IsScoped
            && (pass.ResolvedEmployeeId is not { } owner
                || !await _scope.CanAccessEmployeeAsync(owner, cancellationToken).ConfigureAwait(false)))
        {
            return ManualPassErrors.PassNotFound;
        }

        if (!pass.IsHrManual || pass.ResolvedEmployeeId is not { } employeeId)
        {
            return ManualPassErrors.NotManual;
        }

        if (await _manual.GetByIdAsync(pass.Id, cancellationToken).ConfigureAwait(false) is { } evidence)
        {
            // Dalil alohida o'chiriladi (kaskadga qoldirilmaydi) — audit jurnali o'chirishni shu qator orqali yozadi.
            _manual.Remove(evidence);
        }

        _passes.Remove(pass);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _recomputer
            .RecomputeAsync([employeeId], ManualPassDates.Around(pass.LocalDate), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary>Qo'lda belgi tegadigan kunlar.</summary>
internal static class ManualPassDates
{
    /// <summary>Belgi sanasi va uning ikki qo'shnisi — tungi smena qo'shni kunga tushadi.</summary>
    public static IReadOnlyCollection<DateOnly> Around(DateOnly date) => [date.AddDays(-1), date, date.AddDays(1)];
}

/// <summary>Tabel katagining barcha belgilari.</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="Date">Katak sanasi.</param>
public sealed record GetDayPassesQuery(Guid EmployeeId, DateOnly Date);

/// <summary>Kun oynasi uchun belgilar (SPEC-qolda-belgi §B).</summary>
public sealed class GetDayPassesQueryHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly IAttendanceReadModel _readModel;
    private readonly IAttendanceScope _scope;

    public GetDayPassesQueryHandler(
        IEmployeeRepository employees,
        IAttendanceReadModel readModel,
        IAttendanceScope scope)
    {
        _employees = employees;
        _readModel = readModel;
        _scope = scope;
    }

    public async Task<Result<AttendanceDayPassesDto>> HandleAsync(
        GetDayPassesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!await _employees.ExistsAsync(query.EmployeeId, cancellationToken).ConfigureAwait(false) ||
            !await _scope.CanAccessEmployeeAsync(query.EmployeeId, cancellationToken).ConfigureAwait(false))
        {
            return ManualPassErrors.EmployeeNotFound;
        }

        return await _readModel.GetDayPassesAsync(query.EmployeeId, query.Date, cancellationToken).ConfigureAwait(false);
    }
}
