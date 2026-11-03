using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Blanks.Submissions;

/// <summary>Rolga biriktirilganlarni almashtirish (§7 #5).</summary>
/// <param name="RoleCode">Ofis roli.</param>
/// <param name="UserIds">Yangi to'plam (≤ 20).</param>
public sealed record SetBlankRoleUsersCommand(string? RoleCode, IReadOnlyList<Guid>? UserIds);

public sealed class ListBlankTemplatesQueryHandler
{
    private readonly IBlankReadModel _readModel;

    public ListBlankTemplatesQueryHandler(IBlankReadModel readModel) => _readModel = readModel;

    public async Task<Result<IReadOnlyList<BlankTemplateDto>>> HandleAsync(CancellationToken cancellationToken = default)
        => Result.Success(await _readModel.ListTemplatesAsync(cancellationToken).ConfigureAwait(false));
}

public sealed class GetBlankTemplateVersionQueryHandler
{
    private readonly IBlankReadModel _readModel;

    public GetBlankTemplateVersionQueryHandler(IBlankReadModel readModel) => _readModel = readModel;

    public async Task<Result<BlankTemplateVersionDto>> HandleAsync(Guid id, CancellationToken cancellationToken = default)
        => await _readModel.GetVersionAsync(id, cancellationToken).ConfigureAwait(false) is { } dto
            ? dto
            : BlankErrors.TemplateNotFound;
}

/// <summary>Shablonni yoqish/o'chirish (§7 #3) — o'chirilganda yangi blanka ochilmaydi, borlari ishlayveradi.</summary>
public sealed class SetBlankTemplateActiveCommandHandler
{
    private readonly IBlankTemplateRepository _templates;
    private readonly IBlankReadModel _readModel;
    private readonly IUnitOfWork _unitOfWork;

    public SetBlankTemplateActiveCommandHandler(
        IBlankTemplateRepository templates,
        IBlankReadModel readModel,
        IUnitOfWork unitOfWork)
    {
        _templates = templates;
        _readModel = readModel;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BlankTemplateDto>> HandleAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var template = await _templates.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            return BlankErrors.TemplateNotFound;
        }

        if (isActive)
        {
            template.Activate();
        }
        else
        {
            template.Deactivate();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _readModel.GetTemplateAsync(id, cancellationToken).ConfigureAwait(false) is { } dto
            ? dto
            : BlankErrors.TemplateNotFound;
    }
}

public sealed class ListBlankRolesQueryHandler
{
    private readonly IBlankReadModel _readModel;

    public ListBlankRolesQueryHandler(IBlankReadModel readModel) => _readModel = readModel;

    public async Task<Result<IReadOnlyList<BlankRoleDto>>> HandleAsync(CancellationToken cancellationToken = default)
        => Result.Success(await _readModel.ListRolesAsync(cancellationToken).ConfigureAwait(false));
}

/// <summary>Ofis roliga biriktirilganlar to'plamini almashtiradi (§6.2: bo'sh — har qanday tasdiqlovchi).</summary>
public sealed class SetBlankRoleUsersCommandHandler
{
    /// <summary>Bitta rolga eng ko'pi.</summary>
    public const int MaxUsers = 20;

    private readonly IBlankRoleAssignmentRepository _assignments;
    private readonly IBlankReadModel _readModel;
    private readonly IUnitOfWork _unitOfWork;

    public SetBlankRoleUsersCommandHandler(
        IBlankRoleAssignmentRepository assignments,
        IBlankReadModel readModel,
        IUnitOfWork unitOfWork)
    {
        _assignments = assignments;
        _readModel = readModel;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BlankRoleDto>> HandleAsync(SetBlankRoleUsersCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var roleCode = command.RoleCode?.Trim();
        if (!BlankActorRoles.IsOfficeRole(roleCode))
        {
            return BlankErrors.InvalidRole;
        }

        var wanted = (command.UserIds ?? []).Distinct().ToList();
        if (wanted.Count > MaxUsers || wanted.Contains(Guid.Empty))
        {
            return BlankErrors.Invalid;
        }

        if ((await _readModel.MissingUserIdsAsync(wanted, cancellationToken).ConfigureAwait(false)).Count > 0)
        {
            return BlankErrors.UserNotFound;
        }

        var current = (await _assignments.ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(assignment => assignment.RoleCode == roleCode)
            .ToList();

        foreach (var stale in current.Where(assignment => !wanted.Contains(assignment.UserId)))
        {
            _assignments.Remove(stale);
        }

        foreach (var userId in wanted.Where(id => current.All(assignment => assignment.UserId != id)))
        {
            var created = BlankRoleAssignment.Create(roleCode, userId);
            if (created.IsFailure)
            {
                return created.Error;
            }

            _assignments.Add(created.Value);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var roles = await _readModel.ListRolesAsync(cancellationToken).ConfigureAwait(false);
        return roles.First(role => role.RoleCode == roleCode);
    }
}
