using Bonvi.Application.Abstractions;
using Bonvi.Application.Catalog.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;

namespace Bonvi.Application.Catalog.Usage;

/// <summary>
/// Yozuvdagi hamma ishlayotgan xodimni boshqa yozuvga o'tkazib, keyin uni faol emas qilish
/// (docs/SPEC-malumotnoma-faolsizlantirish.md §C; egasi, 2026-10-31).
/// </summary>
/// <param name="SourceId">Faol emas qilinadigan yozuv.</param>
/// <param name="TargetId">Xodimlar o'tadigan faol yozuv.</param>
/// <param name="EffectiveDate">O'tkazish sanasi; bo'sh — bugun. Yo'nalishda ishlatilmaydi.</param>
/// <param name="OrderNo">Buyruq raqami (ixtiyoriy).</param>
/// <param name="OrderDate">Buyruq sanasi (ixtiyoriy).</param>
public sealed record ReassignReferenceCommand(
    Guid SourceId,
    Guid TargetId,
    DateOnly? EffectiveDate = null,
    string? OrderNo = null,
    DateOnly? OrderDate = null);

/// <summary>
/// Bo'lim: o'zi va quyi bo'limlaridagi hamma ishlayotgan xodim boshqa bo'limga o'tadi, keyin butun tarmoq
/// faol emas bo'ladi.
/// </summary>
/// <remarks>
/// Har xodim tarixiga oddiy «o'tkazish» yozuvi tushadi — xodim kartasidagi o'tkazishning o'zi
/// (<see cref="Bonvi.Domain.Employees.Employment.Transfer"/>). O'tkazishlar va faol emas qilish BITTA saqlashda:
/// yarmi o'tib, yarmi qolib ketgan holat bo'lmaydi.
/// </remarks>
public sealed class ReassignDepartmentCommandHandler
{
    private readonly IDepartmentRepository _departments;
    private readonly IEmploymentRepository _employments;
    private readonly DepartmentStateRule _rule;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ReassignDepartmentCommandHandler(
        IDepartmentRepository departments,
        IEmploymentRepository employments,
        DepartmentStateRule rule,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _departments = departments;
        _employments = employments;
        _rule = rule;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(ReassignReferenceCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_currentUser.HasPermission(Permissions.EmploymentTransfer))
        {
            return CatalogErrors.ReassignPermissionRequired;
        }

        var source = await _departments.GetByIdAsync(command.SourceId, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            return CatalogErrors.DepartmentNotFound;
        }

        var descendants = await _departments.GetDescendantIdsAsync(source.Id, cancellationToken).ConfigureAwait(false);

        var target = await _departments.GetByIdAsync(command.TargetId, cancellationToken).ConfigureAwait(false);
        if (target is not { IsActive: true } || target.Id == source.Id || descendants.Contains(target.Id))
        {
            return CatalogErrors.ReassignTargetInvalid;
        }

        var employments = await _employments
            .GetActiveInDepartmentsAsync([source.Id, .. descendants], cancellationToken)
            .ConfigureAwait(false);

        var date = command.EffectiveDate ?? _clock.Today;
        foreach (var employment in employments)
        {
            var moved = employment.Transfer(date, target.Id, null, null, command.OrderNo, command.OrderDate);
            if (moved.IsFailure)
            {
                return moved.Error;
            }
        }

        await _rule.DeactivateDescendantsAsync(descendants, cancellationToken).ConfigureAwait(false);
        source.Deactivate();

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary>Lavozim: hamma ishlayotgan xodim boshqa lavozimga o'tadi, keyin lavozim faol emas bo'ladi.</summary>
/// <remarks>
/// Lavozim almashgani uchun tarixda «lavozim o'zgarishi» (<c>Promotion</c>) — xodim kartasidagi qoidaning o'zi.
/// </remarks>
public sealed class ReassignPositionCommandHandler
{
    private readonly IPositionRepository _positions;
    private readonly IEmploymentRepository _employments;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ReassignPositionCommandHandler(
        IPositionRepository positions,
        IEmploymentRepository employments,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _positions = positions;
        _employments = employments;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(ReassignReferenceCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_currentUser.HasPermission(Permissions.EmploymentTransfer))
        {
            return CatalogErrors.ReassignPermissionRequired;
        }

        var source = await _positions.GetByIdAsync(command.SourceId, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            return CatalogErrors.PositionNotFound;
        }

        var target = await _positions.GetByIdAsync(command.TargetId, cancellationToken).ConfigureAwait(false);
        if (target is not { IsActive: true } || target.Id == source.Id)
        {
            return CatalogErrors.ReassignTargetInvalid;
        }

        var employments = await _employments.GetActiveInPositionAsync(source.Id, cancellationToken).ConfigureAwait(false);

        var date = command.EffectiveDate ?? _clock.Today;
        foreach (var employment in employments)
        {
            var moved = employment.Transfer(date, null, target.Id, null, command.OrderNo, command.OrderDate);
            if (moved.IsFailure)
            {
                return moved.Error;
            }
        }

        source.Deactivate();
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary>
/// Yo'nalish: ishlayotgan xodimlarda eski yo'nalish yangisiga almashadi, keyin yo'nalish faol emas bo'ladi.
/// </summary>
/// <remarks>
/// Yo'nalish — xodimga qo'yilgan belgi, kadr harakati emas: tarixga yozuv tushmaydi (egasi, 2026-10-31).
/// Ishdan ketganlarda eski yo'nalish qoladi — ularning o'tmishi o'zgarmasin.
/// </remarks>
public sealed class ReassignDirectionCommandHandler
{
    private readonly IReferenceRepository<Direction> _directions;
    private readonly IEmployeeRepository _employees;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public ReassignDirectionCommandHandler(
        IReferenceRepository<Direction> directions,
        IEmployeeRepository employees,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _directions = directions;
        _employees = employees;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(ReassignReferenceCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_currentUser.HasPermission(Permissions.EmployeesEdit))
        {
            return CatalogErrors.ReassignPermissionRequired;
        }

        var source = await _directions.GetByIdAsync(command.SourceId, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            return CatalogErrors.NotFound;
        }

        var target = await _directions.GetByIdAsync(command.TargetId, cancellationToken).ConfigureAwait(false);
        if (target is not { IsActive: true } || target.Id == source.Id)
        {
            return CatalogErrors.ReassignTargetInvalid;
        }

        var employees = await _employees.GetWorkingWithDirectionAsync(source.Id, cancellationToken).ConfigureAwait(false);
        foreach (var employee in employees)
        {
            employee.RemoveDirection(source.Id);
            employee.AssignDirection(target.Id);
        }

        source.Deactivate();
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
