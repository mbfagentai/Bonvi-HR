using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Agent;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Ingest;
using Bonvi.Application.Attendance.Recompute;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Identities;

/// <summary>Qurilma raqamini xodimga qo'lda bog'laydi.</summary>
/// <param name="Provider">Raqamlar fazosi.</param>
/// <param name="ExternalPersonId">Qurilmadagi shaxs raqami.</param>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="DisplayNameInDevice">Qurilma ro'yxatidagi F.I.Sh.</param>
/// <param name="Note">Izoh.</param>
public sealed record CreateIdentityCommand(
    AttendanceIdentityProvider Provider,
    string? ExternalPersonId,
    Guid EmployeeId,
    string? DisplayNameInDevice = null,
    string? Note = null);

/// <summary>Moslikni uzadi (yumshoq o'chirish).</summary>
/// <param name="IdentityId">Moslik.</param>
public sealed record DeleteIdentityCommand(Guid IdentityId);

/// <summary><see cref="CreateIdentityCommand"/> ni bajaradi.</summary>
/// <remarks>
/// <para>
/// ⭐ <b>Saqlashdan keyin UCH narsa, shu tartibda.</b>
/// (1) Qabul keshidan shu raqam o'chiriladi (<see cref="AttendanceIdentityCache.Evict"/>)
/// — kesh faqat topilganlarni saqlaydi, lekin tartib qoidasi bitta: moslik
/// o'zgarsa, kesh unutadi. (2) To'ldirish vazifasi navbatga qo'yiladi
/// (<see cref="AttendanceBackfillJob"/>, SPEC §4.3): shu raqamning eski
/// moslanmagan o'tishlari xodimga bog'lanadi va kunlari qayta quriladi.
/// (3) Javob — ro'yxatdagi qator bilan bir xil shakl.
/// </para>
/// <para>
/// Sinov muhitida (va Hangfire ulanmaguncha hamma joyda) navbat SINXRON:
/// javob qaytganda tabel katagi allaqachon bor (C6 nazorat nuqtasi).
/// </para>
/// </remarks>
public sealed class CreateIdentityCommandHandler
{
    private readonly IAttendanceIdentityRepository _identities;
    private readonly IEmployeeRepository _employees;
    private readonly IAttendanceReadModel _readModel;
    private readonly AttendanceIdentityCache _cache;
    private readonly IBackgroundJobQueue _jobs;
    private readonly IUnitOfWork _unitOfWork;

    public CreateIdentityCommandHandler(
        IAttendanceIdentityRepository identities,
        IEmployeeRepository employees,
        IAttendanceReadModel readModel,
        AttendanceIdentityCache cache,
        IBackgroundJobQueue jobs,
        IUnitOfWork unitOfWork)
    {
        _identities = identities;
        _employees = employees;
        _readModel = readModel;
        _cache = cache;
        _jobs = jobs;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AttendanceIdentityDto>> HandleAsync(
        CreateIdentityCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Domen tekshiruvi BIRINCHI: bo'sh xodim identifikatori 400
        // (`davomat.xodim_yoq`) bo'lishi kerak, «bunday xodim yo'q» (404) emas.
        var linked = AttendanceIdentity.Link(
            command.Provider,
            command.ExternalPersonId,
            command.EmployeeId,
            IdentitySource.Manual,
            command.DisplayNameInDevice,
            command.Note);

        if (linked.IsFailure)
        {
            return linked.Error;
        }

        var identity = linked.Value;

        if (identity.ExternalPersonId.Length > AttendanceIdentity.MaxExternalPersonIdLength)
        {
            return AttendanceErrors.PersonIdTooLong;
        }

        if (!await _employees.ExistsAsync(identity.EmployeeId, cancellationToken).ConfigureAwait(false))
        {
            return AttendanceErrors.EmployeeNotFound;
        }

        var taken = await _identities
            .FindAsync(identity.Provider, identity.ExternalPersonId, cancellationToken)
            .ConfigureAwait(false);

        if (taken is not null)
        {
            return AttendanceErrors.IdentityTaken;
        }

        /*
          Bonvi fazasidagi raqam xodimga BITTA (bazadagi
          `ux_attendance_identities_managed_employee` indeksi). Ikkinchisi
          qo'lda qo'shilsa, indeks uni baribir rad etardi — lekin 500 bilan;
          bu yerda tushunarli 409.
        */
        if (DevicePersonNo.IsManaged(identity.ExternalPersonId))
        {
            var existing = await _identities
                .ListByEmployeeAsync(identity.EmployeeId, cancellationToken)
                .ConfigureAwait(false);

            if (existing.Any(row => row.Provider == identity.Provider && DevicePersonNo.IsManaged(row.ExternalPersonId)))
            {
                return AttendanceErrors.ManagedIdentityExists;
            }
        }

        _identities.Add(identity);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _cache.Evict(identity.Provider, identity.ExternalPersonId);

        await _jobs
            .EnqueueAsync<AttendanceBackfillJob>(job => job.RunAsync(
                identity.Provider,
                identity.ExternalPersonId,
                identity.EmployeeId,
                CancellationToken.None))
            .ConfigureAwait(false);

        var dto = await _readModel.FindIdentityAsync(identity.Id, cancellationToken).ConfigureAwait(false);

        return dto!;
    }
}

/// <summary><see cref="DeleteIdentityCommand"/> ni bajaradi.</summary>
/// <remarks>
/// <para>
/// ⚠️ <b>O'tgan o'tishlar bog'langanicha QOLADI</b> (SPEC §4.3): ular haqiqatan
/// o'sha odam edi, va bog'lanishni yechish allaqachon topshirilgan oylarning
/// tabelini jimgina qayta yozardi. Moslik rostdan xato bo'lsa — HR uni uzadi,
/// to'g'risini yaratadi va davrni qo'lda qayta hisoblaydi: aniq, sanali va
/// kuzatiladigan amal.
/// </para>
/// <para>
/// Keshdan o'chirish SHART: aks holda uzilgan moslik yana 60 soniya yangi
/// o'tishlarni eski xodimga yozardi.
/// </para>
/// <para>
/// ⚠️ <b>Tizim terminalga yozgan raqam (Bonvi fazasi) UZILMAYDI</b>
/// (<see cref="AttendanceErrors.ManagedIdentityLocked"/>). Uzilsa, keyingi
/// ro'yxatda xodimga YANGI raqam ajratilardi, agent esa odamni terminalda
/// yangi raqam bilan qayta yaratib, eskisini o'chirardi — «raqam hech qachon
/// o'zgarmaydi» qoidasi (SPEC-agent §E) buzilardi va oraliqda odam eshikni
/// ocha olmasdi. Ishdan ketganda raqam terminaldan avtomatik olib tashlanadi.
/// </para>
/// </remarks>
public sealed class DeleteIdentityCommandHandler
{
    private readonly IAttendanceIdentityRepository _identities;
    private readonly AttendanceIdentityCache _cache;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteIdentityCommandHandler(
        IAttendanceIdentityRepository identities,
        AttendanceIdentityCache cache,
        IUnitOfWork unitOfWork)
    {
        _identities = identities;
        _cache = cache;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        DeleteIdentityCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var identity = await _identities.GetByIdAsync(command.IdentityId, cancellationToken).ConfigureAwait(false);
        if (identity is null)
        {
            return AttendanceErrors.IdentityNotFound;
        }

        if (DevicePersonNo.IsManaged(identity.ExternalPersonId))
        {
            return AttendanceErrors.ManagedIdentityLocked;
        }

        _identities.Remove(identity);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _cache.Evict(identity.Provider, identity.ExternalPersonId);

        return Result.Success();
    }
}
