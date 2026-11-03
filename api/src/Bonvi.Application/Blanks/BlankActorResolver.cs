using System.Text.Json.Nodes;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Blanks.Submissions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Blanks;

/// <summary>Chaqiruvchi — ruxsatlari va bog'langan xodimi (§6.2).</summary>
/// <param name="UserId">Foydalanuvchi.</param>
/// <param name="EmployeeId"><c>users.employee_id</c> — shaxs rollari shu orqali aniqlanadi.</param>
/// <param name="CanFill"><c>blanks.fill</c>.</param>
/// <param name="CanManage"><c>blanks.manage</c>.</param>
/// <param name="CanApprove"><c>blanks.approve</c>.</param>
public sealed record BlankCaller(Guid? UserId, Guid? EmployeeId, bool CanFill, bool CanManage, bool CanApprove);

/// <summary>Chaqiruvchi qadamni qanday bajara oladi.</summary>
/// <param name="Electronic">Elektron (o'z nomidan).</param>
/// <param name="InPerson">Planshetda imzolatish (operator sifatida).</param>
public sealed record BlankStepAbility(bool Electronic, bool InPerson)
{
    public bool Any => Electronic || InPerson;
}

/// <summary>
/// Qadamni KIM bajarishi mumkinligi — yagona joy (SPEC-blankalar §6.2, §6.4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Ofis roli</b> (hr1 … lawyer): <c>blanks.approve</c> VA (rolga kimdir biriktirilgan bo'lsa — faqat
/// biriktirilganlar, aks holda har qanday <c>blanks.approve</c> egasi).
/// </para>
/// <para>
/// <b>Shaxs roli</b> (subjectEmployee, departmentHead, <c>field:x</c>): chaqiruvchining
/// <c>users.employee_id</c> si aniqlangan xodimga teng bo'lsa — <c>blanks.approve</c> shart emas.
/// Aniqlab bo'lmasa (boshliq yo'q, maydon bo'sh) yoki guvoh — faqat planshetda.
/// </para>
/// <para>
/// Phase 3 dagi xodim ilovasi kanali ham shu sinfdan foydalanadi (§6.4) — mantiq takrorlanmasin.
/// </para>
/// </remarks>
public sealed class BlankActorResolver
{
    private readonly ICurrentUser _currentUser;
    private readonly IBlankReadModel _readModel;
    private readonly IDepartmentRepository _departments;
    private readonly IBlankRoleAssignmentRepository _assignments;

    private BlankCaller? _caller;
    private IReadOnlyCollection<string>? _officeRoles;

    public BlankActorResolver(
        ICurrentUser currentUser,
        IBlankReadModel readModel,
        IDepartmentRepository departments,
        IBlankRoleAssignmentRepository assignments)
    {
        _currentUser = currentUser;
        _readModel = readModel;
        _departments = departments;
        _assignments = assignments;
    }

    /// <summary>Joriy foydalanuvchi (so'rov davomida bir marta o'qiladi).</summary>
    public async Task<BlankCaller> CallerAsync(CancellationToken cancellationToken = default)
    {
        if (_caller is not null)
        {
            return _caller;
        }

        var userId = _currentUser.UserId;
        var employeeId = userId is { } id
            ? await _readModel.GetUserEmployeeIdAsync(id, cancellationToken).ConfigureAwait(false)
            : null;

        _caller = new BlankCaller(
            userId,
            employeeId,
            _currentUser.HasPermission(Permissions.BlanksFill),
            _currentUser.HasPermission(Permissions.BlanksManage),
            _currentUser.HasPermission(Permissions.BlanksApprove));

        return _caller;
    }

    /// <summary>Chaqiruvchi ELEKTRON bajara oladigan ofis rollari.</summary>
    public async Task<IReadOnlyCollection<string>> OfficeRolesAsync(BlankCaller caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (_officeRoles is not null)
        {
            return _officeRoles;
        }

        if (!caller.CanApprove || caller.UserId is not { } userId)
        {
            return _officeRoles = [];
        }

        var assignments = await _assignments.ListAsync(cancellationToken).ConfigureAwait(false);
        var assignedRoles = assignments.Select(assignment => assignment.RoleCode).ToHashSet(StringComparer.Ordinal);
        var mine = assignments.Where(assignment => assignment.UserId == userId)
            .Select(assignment => assignment.RoleCode)
            .ToHashSet(StringComparer.Ordinal);

        return _officeRoles = BlankActorRoles.OfficeRoles
            .Where(role => !assignedRoles.Contains(role) || mine.Contains(role))
            .ToList();
    }

    /// <summary>Shaxs rolini xodimga aylantiradi; aniqlab bo'lmasa yoki ofis/guvoh roli bo'lsa — <c>null</c>.</summary>
    /// <param name="actorRole">Qadam roli.</param>
    /// <param name="subjectEmployeeId">Blanka sub'yekti.</param>
    /// <param name="departmentId">Blanka bo'limi (nusxa).</param>
    /// <param name="answers">Joriy javoblar (<c>field:x</c> uchun).</param>
    /// <param name="cancellationToken">Bekor qilish.</param>
    public async Task<Guid?> ResolvePersonAsync(
        string actorRole,
        Guid? subjectEmployeeId,
        Guid? departmentId,
        JsonObject answers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actorRole);
        ArgumentNullException.ThrowIfNull(answers);

        if (actorRole == BlankActorRoles.SubjectEmployee)
        {
            return subjectEmployeeId;
        }

        if (actorRole == BlankActorRoles.DepartmentHead)
        {
            if (departmentId is not { } id)
            {
                return null;
            }

            var department = await _departments.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return department?.HeadEmployeeId;
        }

        if (actorRole.StartsWith(BlankActorRoles.FieldPrefix, StringComparison.Ordinal))
        {
            var field = actorRole[BlankActorRoles.FieldPrefix.Length..];
            return answers[field] is JsonObject reference
                && reference["id"] is JsonValue value
                && value.TryGetValue<string>(out var text)
                && Guid.TryParse(text, out var employeeId)
                ? employeeId
                : null;
        }

        return null;
    }

    /// <summary>Chaqiruvchi shu qadamni qanday bajara oladi (qadam holati bu yerda tekshirilmaydi).</summary>
    public async Task<BlankStepAbility> AbilityAsync(
        BlankCaller caller,
        string actorRole,
        BlankStepMode mode,
        Guid? subjectEmployeeId,
        Guid? departmentId,
        JsonObject answers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(actorRole);

        var inPerson = mode != BlankStepMode.Electronic && caller.CanFill;
        if (mode == BlankStepMode.InPerson)
        {
            return new BlankStepAbility(false, inPerson);
        }

        bool electronic;
        if (BlankActorRoles.IsOfficeRole(actorRole))
        {
            electronic = (await OfficeRolesAsync(caller, cancellationToken).ConfigureAwait(false)).Contains(actorRole);
        }
        else
        {
            var person = await ResolvePersonAsync(actorRole, subjectEmployeeId, departmentId, answers, cancellationToken)
                .ConfigureAwait(false);
            electronic = person is { } resolved && caller.EmployeeId == resolved;
        }

        return new BlankStepAbility(electronic, inPerson);
    }
}
