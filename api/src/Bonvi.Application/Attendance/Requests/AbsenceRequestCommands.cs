using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Recompute;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Requests;

/// <summary>Yangi yo'qlik so'rovi (SPEC-so'rovlar §B2).</summary>
/// <param name="EmployeeId">Kim yo'q bo'ladi.</param>
/// <param name="AbsenceTypeId">Yo'qlik turi.</param>
/// <param name="FromDate">Davr boshi.</param>
/// <param name="ToDate">Davr oxiri.</param>
/// <param name="FromTime">Kun ichidagi boshlanish — faqat bir kunlik so'rovda.</param>
/// <param name="ToTime">Kun ichidagi tugash — faqat bir kunlik so'rovda.</param>
/// <param name="Note">Izoh.</param>
public sealed record CreateAbsenceRequestCommand(
    Guid EmployeeId,
    Guid AbsenceTypeId,
    DateOnly FromDate,
    DateOnly ToDate,
    TimeOnly? FromTime = null,
    TimeOnly? ToTime = null,
    string? Note = null);

/// <summary>Kutayotgan so'rovni tahrirlash.</summary>
public sealed record UpdateAbsenceRequestCommand(
    Guid RequestId,
    Guid AbsenceTypeId,
    DateOnly FromDate,
    DateOnly ToDate,
    TimeOnly? FromTime = null,
    TimeOnly? ToTime = null,
    string? Note = null);

/// <summary>Qaror: tasdiqlash yoki rad etish.</summary>
/// <param name="RequestId">So'rov.</param>
/// <param name="Approve">Tasdiqlansinmi.</param>
/// <param name="DecisionNote">HR izohi.</param>
public sealed record DecideAbsenceRequestCommand(Guid RequestId, bool Approve, string? DecisionNote = null);

/// <summary>So'rovni bekor qilish.</summary>
public sealed record CancelAbsenceRequestCommand(Guid RequestId);

/// <summary>Yo'qlik so'rovlarining xatolari — matn bitta joyda.</summary>
internal static class AbsenceRequestErrors
{
    public static readonly Error NotFound =
        Error.NotFound("davomat.sorov_topilmadi", "So'rov topilmadi.");

    public static readonly Error EmployeeNotFound =
        Error.NotFound("davomat.xodim_topilmadi", "Bunday xodim topilmadi.");

    public static readonly Error TypeNotFound =
        Error.NotFound("davomat.yoqlik_turi_topilmadi", "Yo'qlik turi topilmadi.");

    public static readonly Error TypeInactive =
        Error.Validation("davomat.yoqlik_turi_faolsiz", "Bu yo'qlik turi faol emas.");

    public static readonly Error Overlaps =
        Error.Conflict("davomat.sorov_takroriy", "Bu kunlar uchun xodimda faol so'rov allaqachon bor.");
}

/// <summary><see cref="CreateAbsenceRequestCommand"/> ni bajaradi.</summary>
/// <remarks>
/// <para>
/// Birinchi bosqichda so'rovni HR kiritadi (SPEC-so'rovlar §A8) — u sababni
/// baribir telefonda eshitadi. Ikkinchi bosqichda xuddi shu buyruqni xodimning
/// ilovasi chaqiradi va yozuv o'zgarmaydi.
/// </para>
/// <para>
/// ⚠️ Yaratish HECH NARSANI qayta hisoblatmaydi: kutayotgan so'rov kunning
/// holatiga ta'sir qilmaydi (§B3). Hisob faqat QARORdan keyin o'zgaradi.
/// </para>
/// </remarks>
public sealed class CreateAbsenceRequestCommandHandler
{
    private readonly IAbsenceRequestRepository _requests;
    private readonly IEmployeeRepository _employees;
    private readonly IAbsenceTypeLookup _types;
    private readonly IAttendanceReadModel _readModel;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAbsenceRequestCommandHandler(
        IAbsenceRequestRepository requests,
        IEmployeeRepository employees,
        IAbsenceTypeLookup types,
        IAttendanceReadModel readModel,
        IUnitOfWork unitOfWork)
    {
        _requests = requests;
        _employees = employees;
        _types = types;
        _readModel = readModel;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AbsenceRequestDto>> HandleAsync(
        CreateAbsenceRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Domen tekshiruvi birinchi: shakl xatosi 400 bo'lishi kerak,
        // «xodim topilmadi» (404) emas (`CreateIdentityCommandHandler` qoidasi).
        var created = AbsenceRequest.Create(
            command.EmployeeId,
            command.AbsenceTypeId,
            command.FromDate,
            command.ToDate,
            command.FromTime,
            command.ToTime,
            command.Note);

        if (created.IsFailure)
        {
            return created.Error;
        }

        var request = created.Value;

        if (!await _employees.ExistsAsync(request.EmployeeId, cancellationToken).ConfigureAwait(false))
        {
            return AbsenceRequestErrors.EmployeeNotFound;
        }

        var type = await _types.FindAsync(request.AbsenceTypeId, cancellationToken).ConfigureAwait(false);
        if (type is null)
        {
            return AbsenceRequestErrors.TypeNotFound;
        }

        if (!type.IsActive)
        {
            return AbsenceRequestErrors.TypeInactive;
        }

        if (await _requests
                .OverlapsAsync(request.EmployeeId, request.FromDate, request.ToDate, null, cancellationToken)
                .ConfigureAwait(false))
        {
            return AbsenceRequestErrors.Overlaps;
        }

        _requests.Add(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await _readModel.FindAbsenceRequestAsync(request.Id, cancellationToken).ConfigureAwait(false);

        return dto!;
    }
}

/// <summary><see cref="UpdateAbsenceRequestCommand"/> ni bajaradi.</summary>
/// <remarks>
/// Faqat KUTAYOTGAN so'rov tahrirlanadi (domen qoidasi): qaror qabul
/// qilingandan keyin davr o'zgarsa, allaqachon qayta hisoblangan kunlar
/// jimgina boshqacha bo'lib qolardi.
/// </remarks>
public sealed class UpdateAbsenceRequestCommandHandler
{
    private readonly IAbsenceRequestRepository _requests;
    private readonly IAbsenceTypeLookup _types;
    private readonly IAttendanceReadModel _readModel;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAbsenceRequestCommandHandler(
        IAbsenceRequestRepository requests,
        IAbsenceTypeLookup types,
        IAttendanceReadModel readModel,
        IUnitOfWork unitOfWork)
    {
        _requests = requests;
        _types = types;
        _readModel = readModel;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AbsenceRequestDto>> HandleAsync(
        UpdateAbsenceRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = await _requests.GetByIdAsync(command.RequestId, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return AbsenceRequestErrors.NotFound;
        }

        var type = await _types.FindAsync(command.AbsenceTypeId, cancellationToken).ConfigureAwait(false);
        if (type is null)
        {
            return AbsenceRequestErrors.TypeNotFound;
        }

        if (!type.IsActive)
        {
            return AbsenceRequestErrors.TypeInactive;
        }

        if (await _requests
                .OverlapsAsync(request.EmployeeId, command.FromDate, command.ToDate, request.Id, cancellationToken)
                .ConfigureAwait(false))
        {
            return AbsenceRequestErrors.Overlaps;
        }

        var updated = request.Update(
            command.AbsenceTypeId,
            command.FromDate,
            command.ToDate,
            command.FromTime,
            command.ToTime,
            command.Note);

        if (updated.IsFailure)
        {
            return updated.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await _readModel.FindAbsenceRequestAsync(request.Id, cancellationToken).ConfigureAwait(false);

        return dto!;
    }
}

/// <summary><see cref="DecideAbsenceRequestCommand"/> ni bajaradi.</summary>
/// <remarks>
/// <para>
/// ⭐ <b>Qarordan keyin kunlar QAYTA HISOBLANADI</b> (§B3): tasdiqlangan
/// so'rov «Kelmadi» ni «Sababli» ga aylantiradi, rad etilgani esa —
/// teskarisiga. Saqlangan kun qo'lda tahrirlanmaydi, hamma narsa
/// <see cref="AttendanceRecomputeRangeJob"/> orqali: tabelni qo'lda yozish
/// modulda umuman yo'q qoida.
/// </para>
/// <para>
/// Tasdiqlash ham, rad etish ham BITTA handler: ikkalasidan keyin ham ayni
/// bir ish bajariladi va ikki nusxada yozilsa, biriga qayta hisoblash
/// qo'shilib, ikkinchisiga qo'shilmay qolishi mumkin edi.
/// </para>
/// </remarks>
public sealed class DecideAbsenceRequestCommandHandler
{
    private readonly IAbsenceRequestRepository _requests;
    private readonly IAttendanceReadModel _readModel;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IBackgroundJobQueue _jobs;
    private readonly IUnitOfWork _unitOfWork;

    public DecideAbsenceRequestCommandHandler(
        IAbsenceRequestRepository requests,
        IAttendanceReadModel readModel,
        ICurrentUser currentUser,
        IClock clock,
        IBackgroundJobQueue jobs,
        IUnitOfWork unitOfWork)
    {
        _requests = requests;
        _readModel = readModel;
        _currentUser = currentUser;
        _clock = clock;
        _jobs = jobs;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AbsenceRequestDto>> HandleAsync(
        DecideAbsenceRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = await _requests.GetByIdAsync(command.RequestId, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return AbsenceRequestErrors.NotFound;
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

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await EnqueueRecomputeAsync(_jobs, request).ConfigureAwait(false);

        var dto = await _readModel.FindAbsenceRequestAsync(request.Id, cancellationToken).ConfigureAwait(false);

        return dto!;
    }

    /// <summary>So'rov qamragan kunlarni qayta hisoblashga qo'yadi.</summary>
    /// <remarks>
    /// Bekor qilish ham shu yerdan chaqiriladi (<see cref="CancelAbsenceRequestCommandHandler"/>):
    /// tasdiqlangan so'rov bekor qilinsa, kun «Sababli» dan qaytishi kerak.
    /// </remarks>
    internal static Task EnqueueRecomputeAsync(IBackgroundJobQueue jobs, AbsenceRequest request)
        => jobs.EnqueueAsync<AttendanceRecomputeRangeJob>(job => job.RunAsync(
            request.FromDate,
            request.ToDate,
            request.EmployeeId,
            CancellationToken.None));
}

/// <summary><see cref="CancelAbsenceRequestCommand"/> ni bajaradi.</summary>
/// <remarks>
/// Tasdiqlangan so'rov ham bekor qilinadi (domen izohi) va shu sababli bu
/// yerda ham qayta hisoblash bor: aks holda bekor qilingan so'rovning kunlari
/// «Sababli» bo'lib qolardi.
/// </remarks>
public sealed class CancelAbsenceRequestCommandHandler
{
    private readonly IAbsenceRequestRepository _requests;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IBackgroundJobQueue _jobs;
    private readonly IUnitOfWork _unitOfWork;

    public CancelAbsenceRequestCommandHandler(
        IAbsenceRequestRepository requests,
        ICurrentUser currentUser,
        IClock clock,
        IBackgroundJobQueue jobs,
        IUnitOfWork unitOfWork)
    {
        _requests = requests;
        _currentUser = currentUser;
        _clock = clock;
        _jobs = jobs;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        CancelAbsenceRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = await _requests.GetByIdAsync(command.RequestId, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return AbsenceRequestErrors.NotFound;
        }

        var wasApproved = request.Status == AbsenceRequestStatus.Approved;

        var cancelled = request.Cancel(_currentUser.UserId ?? Guid.Empty, _clock.UtcNow);
        if (cancelled.IsFailure)
        {
            return cancelled.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Faqat TASDIQLANGAN so'rov kunlarga ta'sir qilgan edi — qolganida
        // qayta hisoblash bir necha yuz kunni sababsiz qayta yozardi.
        if (wasApproved)
        {
            await DecideAbsenceRequestCommandHandler.EnqueueRecomputeAsync(_jobs, request).ConfigureAwait(false);
        }

        return Result.Success();
    }
}

/// <summary>Yo'qlik turini o'qish porti — buyruqlar uchun.</summary>
/// <remarks>
/// Ma'lumotnoma <c>Catalog</c> modulida yashaydi va davomat moduli unga
/// to'g'ridan-to'g'ri bog'lanmasligi kerak: bu yerda faqat «bor-yo'qligi va
/// faolligi» so'raladi.
/// </remarks>
public interface IAbsenceTypeLookup
{
    /// <summary>Turni topadi — yo'q bo'lsa <c>null</c>.</summary>
    Task<AbsenceTypeState?> FindAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Yo'qlik turining buyruqlarga kerakli qismi.</summary>
public sealed record AbsenceTypeState(Guid Id, string Name, bool IsActive);
