using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Catalog.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Schedules;

/// <summary>Jadvalni xodim, lavozim, bo'lim yoki hammaga (sukut) biriktiradi.</summary>
/// <param name="WorkScheduleId">Jadval.</param>
/// <param name="Scope">Pog'ona.</param>
/// <param name="EmployeeId">Xodim — faqat xodim pog'onasida.</param>
/// <param name="PositionId">Lavozim — faqat lavozim pog'onasida.</param>
/// <param name="DepartmentId">Bo'lim — faqat bo'lim pog'onasida.</param>
/// <param name="EffectiveFrom">Kuchga kirish sanasi.</param>
/// <param name="EffectiveTo">Tugash sanasi (shu kun ham kiradi); <c>null</c> — muddatsiz.</param>
public sealed record CreateScheduleAssignmentCommand(
    Guid WorkScheduleId,
    ScheduleScope Scope,
    Guid? EmployeeId,
    Guid? PositionId,
    Guid? DepartmentId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

/// <summary>Biriktirmani olib tashlaydi (yumshoq o'chirish).</summary>
/// <param name="AssignmentId">Biriktirma.</param>
public sealed record DeleteScheduleAssignmentCommand(Guid AssignmentId);

/// <summary>Biriktirmalar ro'yxati (<c>GET /schedule-assignments</c>).</summary>
/// <param name="Scope">Pog'ona.</param>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="DepartmentId">Bo'lim.</param>
/// <param name="PositionId">Lavozim.</param>
/// <param name="OnDate">Shu sanada kuchda bo'lganlar.</param>
public sealed record ListScheduleAssignmentsQuery(
    ScheduleScope? Scope = null,
    Guid? EmployeeId = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    DateOnly? OnDate = null);

/// <summary><see cref="CreateScheduleAssignmentCommand"/> ni bajaradi.</summary>
/// <remarks>
/// <para>
/// Tekshiruv tartibi: shakl (400) → mavjudlik (404) → holat va takror (409).
/// Shunda bo'sh jadval identifikatori «jadval topilmadi» emas, «jadval
/// ko'rsatilmagan» bo'lib chiqadi (SPEC §6.9.1).
/// </para>
/// <para>
/// ⚠️ Biriktirish O'TGAN kunlarni avtomatik qayta hisoblamaydi (SPEC §5.8,
/// 2-band): orqaga sanali biriktirma — ongli qaror, va qaysi davr qayta
/// hisoblanishini HR o'zi tanlaydi.
/// </para>
/// </remarks>
public sealed class CreateScheduleAssignmentCommandHandler
{
    private readonly IScheduleAssignmentRepository _assignments;
    private readonly IWorkScheduleRepository _schedules;
    private readonly IEmployeeRepository _employees;
    private readonly IPositionRepository _positions;
    private readonly IDepartmentRepository _departments;
    private readonly IAttendanceReadModel _readModel;
    private readonly IUnitOfWork _unitOfWork;

    public CreateScheduleAssignmentCommandHandler(
        IScheduleAssignmentRepository assignments,
        IWorkScheduleRepository schedules,
        IEmployeeRepository employees,
        IPositionRepository positions,
        IDepartmentRepository departments,
        IAttendanceReadModel readModel,
        IUnitOfWork unitOfWork)
    {
        _assignments = assignments;
        _schedules = schedules;
        _employees = employees;
        _positions = positions;
        _departments = departments;
        _readModel = readModel;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ScheduleAssignmentDto>> HandleAsync(
        CreateScheduleAssignmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // So'rovda uchta obyekt maydoni, domenda bitta: pog'onaga tegishli
        // bo'lmagan maydon to'ldirilgan bo'lsa — bu ham «mos emas».
        var (targetId, extraFilled) = command.Scope switch
        {
            ScheduleScope.Employee => (command.EmployeeId, command.PositionId is not null || command.DepartmentId is not null),
            ScheduleScope.Position => (command.PositionId, command.EmployeeId is not null || command.DepartmentId is not null),
            ScheduleScope.Department => (command.DepartmentId, command.EmployeeId is not null || command.PositionId is not null),
            _ => ((Guid?)null, command.EmployeeId is not null || command.PositionId is not null || command.DepartmentId is not null),
        };

        if (extraFilled)
        {
            return AttendanceErrors.AssignmentTargetMismatch;
        }

        var assigned = ScheduleAssignment.Assign(
            command.WorkScheduleId,
            command.Scope,
            targetId,
            command.EffectiveFrom,
            command.EffectiveTo);

        if (assigned.IsFailure)
        {
            return assigned.Error;
        }

        var schedule = await _schedules.GetByIdAsync(command.WorkScheduleId, cancellationToken).ConfigureAwait(false);
        if (schedule is null)
        {
            return AttendanceErrors.ScheduleNotFound;
        }

        var targetExists = await TargetExistsAsync(command.Scope, targetId, cancellationToken).ConfigureAwait(false);
        if (targetExists.IsFailure)
        {
            return targetExists.Error;
        }

        if (!schedule.IsActive)
        {
            return AttendanceErrors.ScheduleInactive;
        }

        var duplicate = await _assignments
            .SameStartExistsAsync(command.Scope, targetId, command.EffectiveFrom, cancellationToken)
            .ConfigureAwait(false);

        if (duplicate)
        {
            return AttendanceErrors.AssignmentTaken;
        }

        _assignments.Add(assigned.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await _readModel.FindAssignmentAsync(assigned.Value.Id, cancellationToken).ConfigureAwait(false);

        return dto!;
    }

    private async Task<Result> TargetExistsAsync(ScheduleScope scope, Guid? targetId, CancellationToken cancellationToken)
    {
        if (targetId is not { } id)
        {
            return Result.Success();
        }

        if (scope == ScheduleScope.Employee
            && !await _employees.ExistsAsync(id, cancellationToken).ConfigureAwait(false))
        {
            return AttendanceErrors.EmployeeNotFound;
        }

        if (scope == ScheduleScope.Position
            && !await _positions.ExistsAsync(id, cancellationToken).ConfigureAwait(false))
        {
            return CatalogErrors.PositionNotFound;
        }

        if (scope == ScheduleScope.Department
            && !await _departments.ExistsAsync(id, cancellationToken).ConfigureAwait(false))
        {
            return CatalogErrors.DepartmentNotFound;
        }

        return Result.Success();
    }
}

/// <summary><see cref="DeleteScheduleAssignmentCommand"/> ni bajaradi.</summary>
/// <remarks>
/// Yumshoq o'chirish: tarixda «kim, qachon olib tashladi» qoladi. Olib
/// tashlangan biriktirma hisoblashda endi qatnashmaydi — o'tgan davr
/// qo'lda qayta hisoblanmaguncha saqlangan kunlar o'zgarmaydi.
/// </remarks>
public sealed class DeleteScheduleAssignmentCommandHandler
{
    private readonly IScheduleAssignmentRepository _assignments;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteScheduleAssignmentCommandHandler(IScheduleAssignmentRepository assignments, IUnitOfWork unitOfWork)
    {
        _assignments = assignments;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        DeleteScheduleAssignmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var assignment = await _assignments.GetByIdAsync(command.AssignmentId, cancellationToken).ConfigureAwait(false);
        if (assignment is null)
        {
            return AttendanceErrors.AssignmentNotFound;
        }

        _assignments.Remove(assignment);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary><see cref="ListScheduleAssignmentsQuery"/> ni bajaradi.</summary>
public sealed class ListScheduleAssignmentsQueryHandler
{
    private readonly IAttendanceReadModel _readModel;

    public ListScheduleAssignmentsQueryHandler(IAttendanceReadModel readModel) => _readModel = readModel;

    public async Task<Result<IReadOnlyList<ScheduleAssignmentDto>>> HandleAsync(
        ListScheduleAssignmentsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var items = await _readModel
            .ListAssignmentsAsync(
                new AssignmentFilter(query.Scope, query.EmployeeId, query.DepartmentId, query.PositionId, query.OnDate),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(items);
    }
}
