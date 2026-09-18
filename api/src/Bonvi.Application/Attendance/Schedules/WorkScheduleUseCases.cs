using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Schedules;

/// <summary>Jadvalning tahrirlanadigan maydonlari — yaratish va tahrir uchun bir xil tana.</summary>
/// <param name="Name">Nom — takrorlanmas.</param>
/// <param name="GraceInMinutes">Kelishdagi yon berish (0…120).</param>
/// <param name="GraceOutMinutes">Ketishdagi yon berish (0…120).</param>
/// <param name="Note">Izoh.</param>
/// <param name="Days">Yettala kun, har biri bir martadan.</param>
public sealed record WorkScheduleSettings(
    string? Name,
    int GraceInMinutes,
    int GraceOutMinutes,
    string? Note,
    IReadOnlyList<WorkScheduleDayInput>? Days);

/// <summary>Yangi haftalik jadval.</summary>
/// <param name="Settings">Maydonlar.</param>
public sealed record CreateWorkScheduleCommand(WorkScheduleSettings Settings);

/// <summary>Jadvalni qayta yozadi.</summary>
/// <param name="WorkScheduleId">Jadval.</param>
/// <param name="Settings">Yangi maydonlar.</param>
public sealed record UpdateWorkScheduleCommand(Guid WorkScheduleId, WorkScheduleSettings Settings);

/// <summary>Jadvalni yoqadi yoki o'chiradi.</summary>
/// <param name="WorkScheduleId">Jadval.</param>
/// <param name="Active"><c>true</c> — faollashtirish.</param>
public sealed record SetWorkScheduleActiveCommand(Guid WorkScheduleId, bool Active);

/// <summary>Jadvalni yumshoq o'chiradi.</summary>
/// <param name="WorkScheduleId">Jadval.</param>
public sealed record DeleteWorkScheduleCommand(Guid WorkScheduleId);

/// <summary>Barcha jadvallar (<c>GET /schedules</c>).</summary>
public sealed record ListWorkSchedulesQuery;

/// <summary>Bitta jadval (<c>GET /schedules/{id}</c>).</summary>
/// <param name="WorkScheduleId">Jadval.</param>
public sealed record GetWorkScheduleQuery(Guid WorkScheduleId);

/// <summary><see cref="CreateWorkScheduleCommand"/> ni bajaradi.</summary>
/// <remarks>
/// Domen tekshiruvi (kunlar, vaqtlar, tanaffus oynasi) nom bandligidan
/// OLDIN: noto'g'ri formada bazaga bitta ham so'rov yuborilmaydi.
/// </remarks>
public sealed class CreateWorkScheduleCommandHandler
{
    private readonly IWorkScheduleRepository _schedules;
    private readonly IUnitOfWork _unitOfWork;

    public CreateWorkScheduleCommandHandler(IWorkScheduleRepository schedules, IUnitOfWork unitOfWork)
    {
        _schedules = schedules;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<WorkScheduleDto>> HandleAsync(
        CreateWorkScheduleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var settings = command.Settings;

        var created = WorkSchedule.Create(
            settings.Name,
            settings.Days ?? [],
            WorkScheduleKind.Weekly,
            settings.GraceInMinutes,
            settings.GraceOutMinutes,
            settings.Note);

        if (created.IsFailure)
        {
            return created.Error;
        }

        if (await _schedules.NameExistsAsync(created.Value.Name, null, cancellationToken).ConfigureAwait(false))
        {
            return AttendanceErrors.ScheduleNameTaken;
        }

        _schedules.Add(created.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return WorkScheduleMapper.ToDto(created.Value);
    }
}

/// <summary><see cref="UpdateWorkScheduleCommand"/> ni bajaradi.</summary>
/// <remarks>
/// ⚠️ Tahrir O'TGAN kunlarni avtomatik qayta hisoblamaydi: jadval
/// versiyalanmaydi va qaysi davr qayta hisoblanishi kerakligini faqat HR
/// biladi (SPEC §5.8, 2-band — tahrir, keyin <c>POST /recompute</c>).
/// </remarks>
public sealed class UpdateWorkScheduleCommandHandler
{
    private readonly IWorkScheduleRepository _schedules;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateWorkScheduleCommandHandler(IWorkScheduleRepository schedules, IUnitOfWork unitOfWork)
    {
        _schedules = schedules;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<WorkScheduleDto>> HandleAsync(
        UpdateWorkScheduleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var schedule = await _schedules.GetByIdAsync(command.WorkScheduleId, cancellationToken).ConfigureAwait(false);
        if (schedule is null)
        {
            return AttendanceErrors.ScheduleNotFound;
        }

        var settings = command.Settings;

        var updated = schedule.Update(
            settings.Name,
            settings.Days ?? [],
            settings.GraceInMinutes,
            settings.GraceOutMinutes,
            settings.Note);

        if (updated.IsFailure)
        {
            return updated.Error;
        }

        if (await _schedules.NameExistsAsync(schedule.Name, schedule.Id, cancellationToken).ConfigureAwait(false))
        {
            return AttendanceErrors.ScheduleNameTaken;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return WorkScheduleMapper.ToDto(schedule);
    }
}

/// <summary><see cref="SetWorkScheduleActiveCommand"/> ni bajaradi.</summary>
/// <remarks>
/// ⚠️ <b>Biriktirmasi bor jadval faolsizlantirilmaydi</b> (koordinator qarori,
/// 2026-09-17). Hisoblash <c>IsActive</c> ga QARAMAYDI (§5.2 shart qo'ymaydi),
/// ya'ni ruxsat berilsa ekran «faolsiz» deb ko'rsatar, tabel esa jadvalni
/// ishlatishda davom etardi — ikkalasi bir-biriga zid gapirardi.
/// </remarks>
public sealed class SetWorkScheduleActiveCommandHandler
{
    private readonly IWorkScheduleRepository _schedules;
    private readonly IScheduleAssignmentRepository _assignments;
    private readonly IUnitOfWork _unitOfWork;

    public SetWorkScheduleActiveCommandHandler(
        IWorkScheduleRepository schedules,
        IScheduleAssignmentRepository assignments,
        IUnitOfWork unitOfWork)
    {
        _schedules = schedules;
        _assignments = assignments;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<WorkScheduleDto>> HandleAsync(
        SetWorkScheduleActiveCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var schedule = await _schedules.GetByIdAsync(command.WorkScheduleId, cancellationToken).ConfigureAwait(false);
        if (schedule is null)
        {
            return AttendanceErrors.ScheduleNotFound;
        }

        if (command.Active)
        {
            schedule.Activate();
        }
        else
        {
            if (await _assignments.AnyForScheduleAsync(schedule.Id, cancellationToken).ConfigureAwait(false))
            {
                return AttendanceErrors.ScheduleAssigned;
            }

            schedule.Deactivate();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return WorkScheduleMapper.ToDto(schedule);
    }
}

/// <summary><see cref="DeleteWorkScheduleCommand"/> ni bajaradi.</summary>
/// <remarks>
/// Tirik biriktirma ishora qilgan jadval o'chirilmaydi (SPEC §6.4) —
/// muddati o'tganlari ham sanaladi, chunki o'tgan davrni qayta hisoblash
/// ularni hali ishlatadi. Yumshoq o'chirilgan jadval kunlari bilan bazada
/// qoladi: o'tgan kun yozuvlari unga <c>work_schedule_id</c> orqali ishora
/// qiladi.
/// </remarks>
public sealed class DeleteWorkScheduleCommandHandler
{
    private readonly IWorkScheduleRepository _schedules;
    private readonly IScheduleAssignmentRepository _assignments;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteWorkScheduleCommandHandler(
        IWorkScheduleRepository schedules,
        IScheduleAssignmentRepository assignments,
        IUnitOfWork unitOfWork)
    {
        _schedules = schedules;
        _assignments = assignments;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        DeleteWorkScheduleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var schedule = await _schedules.GetByIdAsync(command.WorkScheduleId, cancellationToken).ConfigureAwait(false);
        if (schedule is null)
        {
            return AttendanceErrors.ScheduleNotFound;
        }

        if (await _assignments.AnyForScheduleAsync(schedule.Id, cancellationToken).ConfigureAwait(false))
        {
            return AttendanceErrors.ScheduleAssigned;
        }

        _schedules.Remove(schedule);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary><see cref="ListWorkSchedulesQuery"/> ni bajaradi.</summary>
public sealed class ListWorkSchedulesQueryHandler
{
    private readonly IWorkScheduleRepository _schedules;

    public ListWorkSchedulesQueryHandler(IWorkScheduleRepository schedules) => _schedules = schedules;

    public async Task<Result<IReadOnlyList<WorkScheduleDto>>> HandleAsync(
        ListWorkSchedulesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var schedules = await _schedules.ListAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success<IReadOnlyList<WorkScheduleDto>>([.. schedules.Select(WorkScheduleMapper.ToDto)]);
    }
}

/// <summary><see cref="GetWorkScheduleQuery"/> ni bajaradi.</summary>
public sealed class GetWorkScheduleQueryHandler
{
    private readonly IWorkScheduleRepository _schedules;

    public GetWorkScheduleQueryHandler(IWorkScheduleRepository schedules) => _schedules = schedules;

    public async Task<Result<WorkScheduleDto>> HandleAsync(
        GetWorkScheduleQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var schedule = await _schedules.GetByIdAsync(query.WorkScheduleId, cancellationToken).ConfigureAwait(false);
        if (schedule is null)
        {
            return AttendanceErrors.ScheduleNotFound;
        }

        return WorkScheduleMapper.ToDto(schedule);
    }
}
