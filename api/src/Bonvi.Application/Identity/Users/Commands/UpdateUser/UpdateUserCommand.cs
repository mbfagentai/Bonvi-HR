using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Identity.Common;
using Bonvi.Application.Identity.Users.AttendanceScope;
using Bonvi.Application.Identity.Users.Common;
using Bonvi.Application.Identity.Users.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Identity.Users.Commands.UpdateUser;

/// <summary>Foydalanuvchi ma'lumotini tahrirlash so'rovi.</summary>
/// <param name="Id">Tahrirlanadigan foydalanuvchi.</param>
/// <param name="Login">Yangi telefon raqami.</param>
/// <param name="LastName">Familiya.</param>
/// <param name="FirstName">Ism.</param>
/// <param name="MiddleName">Otasining ismi (ixtiyoriy).</param>
/// <param name="RoleCode">Yangi rol kodi — eskisi ALMASHTIRILADI.</param>
public sealed record UpdateUserCommand(
    Guid Id,
    string? Login,
    string? LastName,
    string? FirstName,
    string? MiddleName,
    string? RoleCode);

/// <summary>
/// Ism, telefon va rolni yangilaydi. Parol BU YERDA O'ZGARTIRILMAYDI —
/// buning uchun alohida «parolni tiklash» amali bor.
/// </summary>
/// <remarks>
/// <para><b>To'rtta himoya, to'rtta boshqa sabab:</b></para>
/// <list type="number">
///   <item>
///     <b>Kim kimga tega oladi.</b> Ikkita tekshiruv: nishonning JORIY roli
///     (bosh rahbar super administratorni umuman ocha olmaydi) va YANGI rol
///     (bosh rahbar HR menejerni super administratorga ko'tara olmaydi).
///     Qoida <see cref="UserRoleRules"/> da, yagona nusxada.
///   </item>
///   <item>
///     <b>«Atigi bitta».</b> Yangi rol super administrator yoki bosh rahbar
///     bo'lsa, uning boshqa FAOL egasi bo'lmasligi kerak. Tahrirlanayotgan
///     hisobning o'zi sanalmaydi — aks holda rolni o'zgartirmay ismini
///     tuzatish ham rad etilardi.
///   </item>
///   <item>
///     <b>Oxirgi rahbar.</b> Rahbariyat roli tushirilayotgan bo'lsa, tizimda
///     BOSHQA faol rahbar qolishi tekshiriladi. Bu tekshiruv domen darajasida
///     bo'lishi MUMKIN EMAS: <see cref="User"/> agregati boshqa
///     foydalanuvchilar haqida hech narsa bilmaydi (agregatlar bir-biridan
///     mustaqil) — shu sababli bu yerda, repozitoriy orqali bajariladi.
///   </item>
///   <item>
///     <b>O'z rolini o'zgartirish.</b> <see cref="User.ChangeRole"/> — sof
///     domen qoidasi, faqat identifikatorlarni taqqoslaydi.
///   </item>
/// </list>
/// </remarks>
public sealed class UpdateUserCommandHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IUserAttendanceScopeRepository _attendanceScopes;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserCommandHandler(
        ICurrentUser currentUser,
        IUserRepository users,
        IRoleRepository roles,
        IUserAttendanceScopeRepository attendanceScopes,
        IAuditLogger audit,
        IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _users = users;
        _roles = roles;
        _attendanceScopes = attendanceScopes;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserSummaryDto>> HandleAsync(
        UpdateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var acting = await CurrentAccount.LoadAsync(_currentUser, _users, cancellationToken).ConfigureAwait(false);
        if (acting.IsFailure)
        {
            return acting.Error;
        }

        var target = await _users.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return UserErrors.NotFound;
        }

        var loginResult = PhoneNumber.Create(command.Login);
        if (loginResult.IsFailure)
        {
            return loginResult.Error;
        }

        var nameResult = FullName.Create(command.LastName, command.FirstName, command.MiddleName);
        if (nameResult.IsFailure)
        {
            return nameResult.Error;
        }

        var loginTaken = await _users
            .LoginExistsAsync(loginResult.Value, exceptUserId: target.Id, cancellationToken)
            .ConfigureAwait(false);

        if (loginTaken)
        {
            return UserErrors.LoginTaken;
        }

        var roles = await _roles.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var roleResult = RoleResolver.Resolve(roles, command.RoleCode);
        if (roleResult.IsFailure)
        {
            return roleResult.Error;
        }

        var newRole = roleResult.Value;

        // ---------- Kim kimga tega oladi ----------
        // Ikki tomonlama: hozirgi rol egasiga ham, beriladigan yangi rolga ham.
        var canTouch = UserRoleRules.EnsureCanActOn(acting.Value, target, roles);
        if (canTouch.IsFailure)
        {
            return canTouch.Error;
        }

        var canAssign = UserRoleRules.EnsureCanAssign(acting.Value, newRole, roles);
        if (canAssign.IsFailure)
        {
            return canAssign.Error;
        }

        var rolOzgardi = target.RoleId != newRole.Id;

        // Foydalanuvchilar ro'yxati faqat rol HAQIQATAN o'zgarganda kerak —
        // ism yoki telefonni tuzatish uchun butun jadvalni o'qish ortiqcha.
        if (rolOzgardi)
        {
            var allUsers = await _users.ListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            // ---------- «Atigi bitta» ----------
            var single = UserRoleRules.EnsureSingleHolder(newRole, allUsers, exceptUserId: target.Id);
            if (single.IsFailure)
            {
                return single.Error;
            }

            // ---------- Oxirgi rahbarni himoya qilish ----------
            // Faqat rahbariyat roli TUSHIRILAYOTGANDA: yangi rol ham
            // rahbariyatniki bo'lsa, boshqaruvchilar soni kamaymaydi.
            if (!SystemRoles.FullAccess.Contains(newRole.Code))
            {
                var remains = UserRoleRules.EnsureManagerRemains(target, allUsers, roles);
                if (remains.IsFailure)
                {
                    return remains.Error;
                }
            }
        }

        // ---------- O'z rolini o'zgartirishdan himoya (domen) ----------
        // Rolni almashtirish ism/telefondan OLDIN: bu yerda rad etilsa,
        // hisobga hech qanday o'zgarish tegmagan bo'lishi kerak.
        var changed = target.ChangeRole(newRole.Id, acting.Value.Id);
        if (changed.IsFailure)
        {
            return changed.Error;
        }

        if (rolOzgardi && !AttendanceScopeRule.IsAttendanceScopedRole(newRole.Permissions))
        {
            await ClearAttendanceScopeAsync(target, cancellationToken).ConfigureAwait(false);
        }

        // Jurnal yozuvini `AuditTrailInterceptor` qo'yadi — faqat haqiqatan
        // o'zgargan maydonlar (rol, ism, telefon), eski → yangi.
        target.Rename(nameResult.Value);
        target.ChangeLogin(loginResult.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return UserSummaryDtoFactory.Create(target, roles);
    }

    /// <summary>
    /// The assignment means nothing under a role without the marker, and it must not silently
    /// come back into force if the account is later given such a role again. Saved with the
    /// role change; the journal gets the same line as an edit of the assignment itself.
    /// </summary>
    private async Task ClearAttendanceScopeAsync(User target, CancellationToken cancellationToken)
    {
        var rows = await _attendanceScopes.ListForUserAsync(target.Id, cancellationToken).ConfigureAwait(false);

        if (rows.Count == 0)
        {
            return;
        }

        foreach (var row in rows)
        {
            _attendanceScopes.Remove(row);
        }

        _audit.Record(
            AuditAction.Update,
            nameof(User),
            target.Id.ToString(),
            oldValues: UserAttendanceScopeAudit.Snapshot([.. rows.Select(row => row.DepartmentId).Order()]),
            newValues: UserAttendanceScopeAudit.Snapshot([]));
    }
}
