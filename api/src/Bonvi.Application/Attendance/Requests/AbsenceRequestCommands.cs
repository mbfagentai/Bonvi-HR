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
/// <param name="SubmittedByAccountId">Xodim ilovadan yuborganda — uning ilova hisobi; HR kiritganda <c>null</c>.</param>
public sealed record CreateAbsenceRequestCommand(
    Guid EmployeeId,
    Guid AbsenceTypeId,
    DateOnly FromDate,
    DateOnly ToDate,
    TimeOnly? FromTime = null,
    TimeOnly? ToTime = null,
    string? Note = null,
    Guid? SubmittedByAccountId = null);

/// <summary>Kutayotgan so'rovni tahrirlash.</summary>
/// <param name="RequestId">So'rov.</param>
/// <param name="AbsenceTypeId">Yo'qlik turi.</param>
/// <param name="FromDate">Davr boshi.</param>
/// <param name="ToDate">Davr oxiri.</param>
/// <param name="FromTime">Kun ichidagi boshlanish.</param>
/// <param name="ToTime">Kun ichidagi tugash.</param>
/// <param name="Note">Izoh.</param>
/// <param name="ExpectedVersion">
/// Mijoz ko'rgan <c>version</c> (SPEC-sorov-tahriri §3.3, 2-qatlam); farq qilsa — 409
/// <c>davomat.sorov_ozgargan</c>. <c>null</c> — tekshirilmaydi (eski mijoz).
/// </param>
/// <param name="EditedByAccountId">Ilovadan tahrirlagan xodim hisobi; HR tahririda <c>null</c>.</param>
public sealed record UpdateAbsenceRequestCommand(
    Guid RequestId,
    Guid AbsenceTypeId,
    DateOnly FromDate,
    DateOnly ToDate,
    TimeOnly? FromTime = null,
    TimeOnly? ToTime = null,
    string? Note = null,
    int? ExpectedVersion = null,
    Guid? EditedByAccountId = null);

/// <summary>Qaror: tasdiqlash yoki rad etish.</summary>
/// <param name="RequestId">So'rov.</param>
/// <param name="Approve">Tasdiqlansinmi.</param>
/// <param name="DecisionNote">HR izohi.</param>
/// <param name="ExpectedVersion">HR ko'rgan <c>version</c>; farq qilsa — 409 <c>davomat.sorov_ozgargan</c>.</param>
public sealed record DecideAbsenceRequestCommand(
    Guid RequestId,
    bool Approve,
    string? DecisionNote = null,
    int? ExpectedVersion = null);

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

    public static readonly Error TypeClosedForEmployees =
        Error.Validation("davomat.yoqlik_turi_xodimga_yopiq", "Bu yo'qlik turini faqat HR kirita oladi.");

    public static readonly Error Overlaps =
        Error.Conflict("davomat.sorov_takroriy", "Bu kunlar uchun xodimda faol so'rov allaqachon bor.");

    /// <summary>So'rov endi kutayotgan emas — domenning o'sha kodi (tahrir va qaror uchun).</summary>
    public static readonly Error NotPending =
        Error.Conflict("davomat.sorov_qaror_qabul_qilingan", "So'rov bo'yicha qaror allaqachon qabul qilingan yoki u bekor qilingan.");

    /// <summary>
    /// So'rov boshqa joyda o'zgargan: mijoz ko'rgan versiya eskirgan yoki parallel yozuv
    /// yutib ketdi (SPEC-sorov-tahriri §3.3). Jadval so'rovida ham AYNAN shu kod.
    /// </summary>
    public static readonly Error Stale =
        Error.Conflict(
            "davomat.sorov_ozgargan",
            "So'rov boshqa joyda o'zgartirilgan — yangilangan holatini ko'rib, qaytadan urinib ko'ring.");

    /// <summary>
    /// Kutayotgan so'rov holati va mijoz ko'rgan versiya — tahrir va qarorning BOSHIDA, shu
    /// tartibda (§3.3 javoblar ustuvorligi): avval «qaror qabul qilingan», keyin «o'zgargan».
    /// </summary>
    public static Error? PendingAt(AbsenceRequestStatus status, int version, int? expectedVersion)
    {
        if (status != AbsenceRequestStatus.Pending)
        {
            return NotPending;
        }

        return expectedVersion is { } expected && expected != version ? Stale : null;
    }
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
            command.Note,
            command.SubmittedByAccountId);

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
    private readonly IClock _clock;
    private readonly IDatabaseFault _fault;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAbsenceRequestCommandHandler(
        IAbsenceRequestRepository requests,
        IAbsenceTypeLookup types,
        IAttendanceReadModel readModel,
        IClock clock,
        IDatabaseFault fault,
        IUnitOfWork unitOfWork)
    {
        _requests = requests;
        _types = types;
        _readModel = readModel;
        _clock = clock;
        _fault = fault;
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

        // Holat va versiya BIRINCHI (SPEC-sorov-tahriri §3.3): qaror qabul qilingan so'rovda
        // tur yoki kesishish xatosi emas, «qaror qabul qilingan» javobi kerak.
        if (AbsenceRequestErrors.PendingAt(request.Status, request.Version, command.ExpectedVersion) is { } notEditable)
        {
            return notEditable;
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
            command.Note,
            _clock.UtcNow,
            command.EditedByAccountId);

        if (updated.IsFailure)
        {
            return updated.Error;
        }

        // Kutayotgan so'rov kunlarga ta'sir qilmaydi — tahrir hech narsani qayta hisoblatmaydi (§3.1).
        if (await SaveAsync(_unitOfWork, _fault, cancellationToken).ConfigureAwait(false) is { } stale)
        {
            return stale;
        }

        var dto = await _readModel.FindAbsenceRequestAsync(request.Id, cancellationToken).ConfigureAwait(false);

        return dto!;
    }

    /// <summary>
    /// So'rovni saqlaydi; parallel yozuv yutib ketgan bo'lsa — 409 <c>davomat.sorov_ozgargan</c>, 500 emas
    /// (SPEC-sorov-tahriri §3.3, 1-qatlam). Yo'qlik so'rovining barcha yozuvchilari shu yerdan.
    /// </summary>
    /// <remarks>
    /// Qayta urinish YO'Q: kechikkan yozuv boshqa odamning qarorini ko'rmagan — uni takrorlash o'sha
    /// qarorni ustidan yozardi. Mijoz yangilangan holatni o'qib, qaytadan uradi.
    /// </remarks>
    internal static async Task<Error?> SaveAsync(
        IUnitOfWork unitOfWork,
        IDatabaseFault fault,
        CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception exception) when (fault.IsStaleRequestWrite(exception))
        {
            return AbsenceRequestErrors.Stale;
        }
    }
}

/// <summary><see cref="DecideAbsenceRequestCommand"/> ni bajaradi.</summary>
/// <remarks>
/// <para>
/// ⭐ <b>Qarordan keyin kunlar QAYTA HISOBLANADI</b> (§B3): tasdiqlangan
/// so'rov «Kelmadi» ni «Sababli» ga aylantiradi, rad etilgani esa —
/// teskarisiga. Saqlangan kun qo'lda tahrirlanmaydi, hamma narsa yagona
/// yozuvchi <see cref="AttendanceDayRecomputer"/> orqali: tabelni qo'lda
/// yozish modulda umuman yo'q qoida.
/// </para>
/// <para>
/// ⭐ <b>Javobdan OLDIN, shu so'rovning o'zida</b> (bitta xodim, ko'pi bilan
/// <see cref="AttendanceRules.MaxRecomputeDays"/> kun — bir necha so'rov).
/// Ilgari qayta hisoblash Hangfire navbatiga qo'yilardi, veb esa javob
/// kelishi bilan xodim kartasini qayta so'rardi: karta ~bir daqiqa «Kelmadi»
/// bo'lib turardi va HR tugmani ishlamadi deb o'ylardi. Undan uzun davr
/// (domen buni hozir rad etadi, lekin ko'chirilgan eski yozuv bo'lishi
/// mumkin) — navbatga, so'rovni ushlab turmasdan.
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
    private readonly AttendanceDayRecomputer _recomputer;
    private readonly IBackgroundJobQueue _jobs;
    private readonly IDatabaseFault _fault;
    private readonly IUnitOfWork _unitOfWork;

    public DecideAbsenceRequestCommandHandler(
        IAbsenceRequestRepository requests,
        IAttendanceReadModel readModel,
        ICurrentUser currentUser,
        IClock clock,
        AttendanceDayRecomputer recomputer,
        IBackgroundJobQueue jobs,
        IDatabaseFault fault,
        IUnitOfWork unitOfWork)
    {
        _requests = requests;
        _readModel = readModel;
        _currentUser = currentUser;
        _clock = clock;
        _recomputer = recomputer;
        _jobs = jobs;
        _fault = fault;
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

        // HR o'zi ko'rgan nusxa ustida qaror qiladi: xodim shu orada tahrirlagan bo'lsa — 409 (§3.6).
        if (AbsenceRequestErrors.PendingAt(request.Status, request.Version, command.ExpectedVersion) is { } notDecidable)
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

        if (await UpdateAbsenceRequestCommandHandler.SaveAsync(_unitOfWork, _fault, cancellationToken).ConfigureAwait(false)
            is { } stale)
        {
            return stale;
        }

        // Qaror SAQLANGACH: yozuvchi tasdiqlangan so'rovlarni bazadan o'qiydi.
        await RecomputeDaysAsync(_recomputer, _jobs, request, cancellationToken).ConfigureAwait(false);

        var dto = await _readModel.FindAbsenceRequestAsync(request.Id, cancellationToken).ConfigureAwait(false);

        return dto!;
    }

    /// <summary>So'rov qamragan kunlarni qayta hisoblaydi — shu yerda yoki (uzun davrda) navbatda.</summary>
    /// <remarks>
    /// <para>
    /// Bekor qilish ham shu yerdan chaqiriladi (<see cref="CancelAbsenceRequestCommandHandler"/>):
    /// tasdiqlangan so'rov bekor qilinsa, kun «Sababli» dan qaytishi kerak.
    /// </para>
    /// <para>
    /// So'rov sanalari, qo'shnilarsiz: qaror o'tishlarning qaysi kunga
    /// tushishini o'zgartirmaydi. Bitta istisno — <see cref="AffectedDays"/>.
    /// </para>
    /// </remarks>
    internal static async Task RecomputeDaysAsync(
        AttendanceDayRecomputer recomputer,
        IBackgroundJobQueue jobs,
        AbsenceRequest request,
        CancellationToken cancellationToken)
    {
        var (from, to) = AffectedDays(request);

        if (to.DayNumber - from.DayNumber + 1 <= AttendanceRules.MaxRecomputeDays)
        {
            await recomputer
                .RecomputeAsync(
                    [request.EmployeeId],
                    AttendanceDayRecomputer.DaysBetween(from, to),
                    cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        await jobs.EnqueueAsync<AttendanceRecomputeRangeJob>(job => job.RunAsync(
                from,
                to,
                request.EmployeeId,
                CancellationToken.None))
            .ConfigureAwait(false);
    }

    /// <summary>So'rov qaysi kunlarning hisobiga ta'sir qiladi.</summary>
    /// <remarks>
    /// Vaqtli so'rov kechagi TUNGI smenaga ham tushishi mumkin (D + 1 dagi
    /// 02:00–04:00 — D ning 20:00–08:00 smenasi, <see cref="Computation.WorkCreditRule"/>),
    /// shuning uchun undan bir kun oldingisi ham qo'shiladi.
    /// </remarks>
    /// <param name="request">So'rov.</param>
    internal static (DateOnly From, DateOnly To) AffectedDays(AbsenceRequest request)
        => (request.FromTime is null ? request.FromDate : request.FromDate.AddDays(-1), request.ToDate);
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
    private readonly AttendanceDayRecomputer _recomputer;
    private readonly IBackgroundJobQueue _jobs;
    private readonly IDatabaseFault _fault;
    private readonly IUnitOfWork _unitOfWork;

    public CancelAbsenceRequestCommandHandler(
        IAbsenceRequestRepository requests,
        ICurrentUser currentUser,
        IClock clock,
        AttendanceDayRecomputer recomputer,
        IBackgroundJobQueue jobs,
        IDatabaseFault fault,
        IUnitOfWork unitOfWork)
    {
        _requests = requests;
        _currentUser = currentUser;
        _clock = clock;
        _recomputer = recomputer;
        _jobs = jobs;
        _fault = fault;
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

        // Bekor qilish mazmunga bog'liq emas — versiya so'ralmaydi, faqat parallel yozuv (§3.3, 1-qatlam).
        if (await UpdateAbsenceRequestCommandHandler.SaveAsync(_unitOfWork, _fault, cancellationToken).ConfigureAwait(false)
            is { } stale)
        {
            return stale;
        }

        // Faqat TASDIQLANGAN so'rov kunlarga ta'sir qilgan edi — qolganida
        // qayta hisoblash bir necha yuz kunni sababsiz qayta yozardi.
        if (wasApproved)
        {
            await DecideAbsenceRequestCommandHandler
                .RecomputeDaysAsync(_recomputer, _jobs, request, cancellationToken)
                .ConfigureAwait(false);
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
/// <param name="Id">Tur.</param>
/// <param name="Name">Nomi.</param>
/// <param name="IsActive">Faolmi.</param>
/// <param name="EmployeeCanRequest">Xodim ilovadan o'zi so'ray oladimi (SPEC-jadval-so'rovi §A).</param>
public sealed record AbsenceTypeState(Guid Id, string Name, bool IsActive, bool EmployeeCanRequest = true);
