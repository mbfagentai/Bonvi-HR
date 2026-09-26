using System.Text.Json;
using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.Roles;

/// <summary>Yangi rol yaratish so'rovi.</summary>
/// <param name="Name">Egasi yozgan nom — unikal (katta-kichik harfsiz).</param>
/// <param name="Permissions">Rolning ruxsat kodlari — to'liq ro'yxat.</param>
public sealed record CreateRoleCommand(string? Name, IReadOnlyList<string?>? Permissions);

/// <summary>Rolni tahrirlash so'rovi — nom va ruxsatlar TO'LIQ almashtiriladi.</summary>
/// <param name="Id">Tahrirlanadigan rol.</param>
/// <param name="Name">Yangi nom.</param>
/// <param name="Permissions">Yangi ruxsatlar ro'yxati — eskisi to'liq almashtiriladi.</param>
public sealed record UpdateRoleCommand(Guid Id, string? Name, IReadOnlyList<string?>? Permissions);

/// <summary>Rolni o'chirish so'rovi.</summary>
/// <param name="Id">O'chiriladigan rol.</param>
public sealed record DeleteRoleCommand(Guid Id);

/// <summary>
/// Egasi yangi rol yaratadi — deploy va qayta ishga tushirishsiz.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tekshiruvlar tartibi:</b> avval qiymatning o'zi (nom, ruxsat kodlari —
/// 400), keyin tizim holati bilan to'qnashuv (o'zida yo'q ruxsat, band nom —
/// 409). Hammasi rol YARATILISHIDAN OLDIN: rad etilgan so'rov hech narsa
/// qoldirmaydi.
/// </para>
/// <para>
/// Rol kodi avtomatik (<see cref="RoleEditorRules.NewCode"/>) va hech
/// qachon o'zgarmaydi — nom esa istalgancha o'zgaradi.
/// </para>
/// </remarks>
public sealed class CreateRoleCommandHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;

    public CreateRoleCommandHandler(
        ICurrentUser currentUser,
        IUserRepository users,
        IRoleRepository roles,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _users = users;
        _roles = roles;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RoleDto>> HandleAsync(
        CreateRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var acting = await CurrentAccount.LoadAsync(_currentUser, _users, cancellationToken).ConfigureAwait(false);
        if (acting.IsFailure)
        {
            return acting.Error;
        }

        var name = Role.ValidateName(command.Name);
        if (name.IsFailure)
        {
            return name.Error;
        }

        var permissions = Role.ValidatePermissions(command.Permissions);
        if (permissions.IsFailure)
        {
            return permissions.Error;
        }

        var roles = await _roles.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var withinOwn = RoleEditorRules.EnsureWithinOwn(
            RoleEditorRules.PermissionsOf(acting.Value, roles),
            permissions.Value);

        if (withinOwn.IsFailure)
        {
            return withinOwn.Error;
        }

        var nameFree = RoleEditorRules.EnsureNameFree(roles, name.Value, exceptRoleId: null);
        if (nameFree.IsFailure)
        {
            return nameFree.Error;
        }

        var created = Role.Create(RoleEditorRules.NewCode(roles), name.Value);
        if (created.IsFailure)
        {
            return created.Error;
        }

        var role = created.Value;

        var granted = role.ReplacePermissions(permissions.Value);
        if (granted.IsFailure)
        {
            return granted.Error;
        }

        _roles.Add(role);

        _auditLogger.Record(
            AuditAction.Create,
            nameof(Role),
            role.Id.ToString(),
            oldValues: null,
            newValues: JsonSerializer.Serialize(RoleEditorRules.Snapshot(role)));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Yangi rolni hali hech kim egallamagan — hisoblar ro'yxati so'ralmaydi.
        return RoleEditorRules.ToDto(role, []);
    }
}

/// <summary>
/// Rolning nomi va ruxsatlarini almashtiradi — o'zgarish shu rol egalarining
/// KEYINGI so'rovidayoq kuchga kiradi.
/// </summary>
/// <remarks>
/// <para><b>Qo'riqlar tartibi</b> (har biri o'z sababi bilan):</para>
/// <list type="number">
///   <item><b>Himoyalangan rol</b> — super administrator va bot:
///   <see cref="RoleErrors.Protected"/>. Super administratorning ruxsatlarini
///   seeder boshqaradi (har ishga tushishda <see cref="Permissions.All"/>).</item>
///   <item><b>O'z roli</b> — <see cref="RoleErrors.OwnRole"/>. Odam o'zini
///   huquqsiz qoldira olmasligi (yoki o'ziga huquq qo'sha olmasligi) kerak.</item>
///   <item><b>Qiymatlar</b> — nom va ruxsat kodlari (400).</item>
///   <item><b>O'zida yo'q ruxsat</b> — <see cref="RoleErrors.ExceedsOwn"/>.</item>
///   <item><b>Band nom</b> — <see cref="RoleErrors.NameTaken"/>.</item>
/// </list>
/// <para>
/// Rolga faqat hamma tekshiruvdan o'tgandan KEYIN tegiladi: rad etilgan
/// so'rov agregatni yarim o'zgargan holda qoldirmaydi.
/// </para>
/// <para>
/// <b>«Darhol kuchga kiradi»</b> — bu handler'ning ishi emas: saqlash rol
/// o'zgarganini sezadigan interceptor orqali ruxsatlar keshini eskiradi va
/// token tekshiruvi keyingi so'rovda yangi ro'yxatni oladi. Qo'lda chaqiruv
/// yo'q — uni biror handler'da unutib qo'yish imkoni ham yo'q.
/// </para>
/// </remarks>
public sealed class UpdateRoleCommandHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateRoleCommandHandler(
        ICurrentUser currentUser,
        IUserRepository users,
        IRoleRepository roles,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _users = users;
        _roles = roles;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RoleDto>> HandleAsync(
        UpdateRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var acting = await CurrentAccount.LoadAsync(_currentUser, _users, cancellationToken).ConfigureAwait(false);
        if (acting.IsFailure)
        {
            return acting.Error;
        }

        var role = await _roles.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return RoleErrors.NotFound;
        }

        if (role.IsProtected)
        {
            return RoleErrors.Protected;
        }

        if (RoleEditorRules.Holds(acting.Value, role))
        {
            return RoleErrors.OwnRole;
        }

        var name = Role.ValidateName(command.Name);
        if (name.IsFailure)
        {
            return name.Error;
        }

        var permissions = Role.ValidatePermissions(command.Permissions);
        if (permissions.IsFailure)
        {
            return permissions.Error;
        }

        var roles = await _roles.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var own = RoleEditorRules.PermissionsOf(acting.Value, roles);

        // Avval rolning HOZIRGI holati: o'zidan yuqori rolni hech kim tahrirlamaydi
        var targetWithinOwn = RoleEditorRules.EnsureTargetWithinOwn(own, role);
        if (targetWithinOwn.IsFailure)
        {
            return targetWithinOwn.Error;
        }

        var withinOwn = RoleEditorRules.EnsureWithinOwn(own, permissions.Value);

        if (withinOwn.IsFailure)
        {
            return withinOwn.Error;
        }

        var nameFree = RoleEditorRules.EnsureNameFree(roles, name.Value, exceptRoleId: role.Id);
        if (nameFree.IsFailure)
        {
            return nameFree.Error;
        }

        var oldSnapshot = JsonSerializer.Serialize(RoleEditorRules.Snapshot(role));

        // Ikkala chaqiruv ham yuqorida tekshirilgan qiymatni oladi — bu
        // yerda rad etilishi dastur xatosi bo'lardi (himoyalangan rol esa
        // allaqachon qaytarilgan).
        var renamed = role.Rename(name.Value);
        if (renamed.IsFailure)
        {
            return renamed.Error;
        }

        var replaced = role.ReplacePermissions(permissions.Value);
        if (replaced.IsFailure)
        {
            return replaced.Error;
        }

        _auditLogger.Record(
            AuditAction.Update,
            nameof(Role),
            role.Id.ToString(),
            oldValues: oldSnapshot,
            newValues: JsonSerializer.Serialize(RoleEditorRules.Snapshot(role)));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var users = await _users.ListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        return RoleEditorRules.ToDto(role, users);
    }
}

/// <summary>
/// Ishlatilmayotgan rolni o'chiradi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rolni biror hisob egallab tursa — o'chirilmaydi</b>, hisob faolsiz
/// bo'lsa ham (<see cref="RoleErrors.InUse"/>). Faolsiz hisob qayta
/// faollashtirilganda rolsiz qolardi.
/// </para>
/// <para>
/// O'chirish yumshoq emas, haqiqiy: rol jadvalida o'chirilganlik belgisi
/// yo'q va uni qo'shish sxema o'zgarishini talab qilardi. Audit yozuvida
/// rolning kodi, nomi va ruxsatlari saqlanib qoladi — «kim, qaysi rolni
/// o'chirdi» degan savolga javob yo'qolmaydi.
/// </para>
/// </remarks>
public sealed class DeleteRoleCommandHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteRoleCommandHandler(
        ICurrentUser currentUser,
        IUserRepository users,
        IRoleRepository roles,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _users = users;
        _roles = roles;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        DeleteRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var acting = await CurrentAccount.LoadAsync(_currentUser, _users, cancellationToken).ConfigureAwait(false);
        if (acting.IsFailure)
        {
            return acting.Error;
        }

        var role = await _roles.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return RoleErrors.NotFound;
        }

        if (role.IsProtected)
        {
            return RoleErrors.Protected;
        }

        if (RoleEditorRules.Holds(acting.Value, role))
        {
            return RoleErrors.OwnRole;
        }

        // O'zidan yuqori rolni o'chirib ham bo'lmaydi (tahrirdagi bilan bir xil qoida)
        var roles = await _roles.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var targetWithinOwn = RoleEditorRules.EnsureTargetWithinOwn(
            RoleEditorRules.PermissionsOf(acting.Value, roles), role);
        if (targetWithinOwn.IsFailure)
        {
            return targetWithinOwn.Error;
        }

        var users = await _users.ListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var holders = users.Count(user => user.RoleIds.Contains(role.Id));
        if (holders > 0)
        {
            return RoleErrors.InUse(holders);
        }

        _auditLogger.Record(
            AuditAction.Delete,
            nameof(Role),
            role.Id.ToString(),
            oldValues: JsonSerializer.Serialize(RoleEditorRules.Snapshot(role)),
            newValues: null);

        _roles.Remove(role);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
