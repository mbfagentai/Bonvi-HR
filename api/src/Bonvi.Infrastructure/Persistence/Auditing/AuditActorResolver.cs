using Bonvi.Application.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.Auditing;

/// <summary>
/// Bajaruvchining shaxsiga (<see cref="AuditPrincipal"/>) ism va rol NOMINI qo'shadi — qamrov
/// davomida eslab qolinadi.
/// </summary>
/// <remarks>
/// <para>
/// Bir so'rov bir necha marta saqlasa ham foydalanuvchi bir marta o'qiladi
/// (SPEC §7.8 «yozish narxi»). Kesh shu obyekt bilan yashaydi — interceptor
/// esa <c>Scoped</c>, ya'ni kesh so'rov tugashi bilan yo'qoladi va rol nomi
/// keyingi so'rovda yangidan o'qiladi (egasi rolni qayta nomlasa).
/// </para>
/// <para>
/// O'chirilgan yoki faolsizlantirilgan hisob ham topiladi
/// (<c>IgnoreQueryFilters</c>): bajaruvchi nusxasi har holda yozilishi kerak.
/// </para>
/// </remarks>
internal sealed class AuditActorResolver
{
    private readonly Dictionary<AuditPrincipal, ResolvedActor> _cache = [];

    public async Task<ResolvedActor> ResolveAsync(
        DbContext context,
        AuditPrincipal principal,
        bool async,
        CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(principal, out var cached))
        {
            return cached;
        }

        var resolved = principal.Kind switch
        {
            AuditActorKind.User when principal.UserId is { } userId
                => await UserAsync(context, userId, principal.Login, async, cancellationToken).ConfigureAwait(false),

            AuditActorKind.EmployeeAccount
                => new ResolvedActor(
                    AuditActorKind.EmployeeAccount,
                    UserId: null,
                    Login: null,
                    principal.EmployeeId is { } employeeId
                        ? await EmployeeNameAsync(context, employeeId, async, cancellationToken).ConfigureAwait(false)
                        : null,
                    RoleName: null),

            _ => new ResolvedActor(principal.Kind, principal.UserId, principal.Login, Name: null, RoleName: null),
        };

        _cache[principal] = resolved;
        return resolved;
    }

    private static async Task<ResolvedActor> UserAsync(
        DbContext context,
        Guid userId,
        string? login,
        bool async,
        CancellationToken cancellationToken)
    {
        var rows = await AuditLookups.ListAsync(
            context.Set<User>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new
                {
                    user.FullName.LastName,
                    user.FullName.FirstName,
                    user.FullName.MiddleName,
                    user.Login,
                    user.RoleIds,
                }),
            async,
            cancellationToken).ConfigureAwait(false);

        if (rows.Count == 0)
        {
            // Tokenda bor, bazada yo'q — nazariy holat (hisob qo'lda o'chirilgan).
            // Identifikator va login baribir yoziladi.
            return new ResolvedActor(AuditActorKind.User, userId, login, Name: null, RoleName: null);
        }

        var row = rows[0];
        string? roleName = null;

        if (row.RoleIds.Count > 0)
        {
            var roleId = row.RoleIds[0];
            var names = await AuditLookups.ListAsync(
                context.Set<Role>().AsNoTracking().Where(role => role.Id == roleId).Select(role => role.Name),
                async,
                cancellationToken).ConfigureAwait(false);

            roleName = names.Count > 0 ? names[0] : null;
        }

        return new ResolvedActor(
            AuditActorKind.User,
            userId,
            row.Login.Value,
            FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
            roleName);
    }

    private static async Task<string?> EmployeeNameAsync(
        DbContext context,
        Guid employeeId,
        bool async,
        CancellationToken cancellationToken)
    {
        var rows = await AuditLookups.ListAsync(
            context.Set<Employee>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(employee => employee.Id == employeeId)
                .Select(employee => new { employee.FullName.LastName, employee.FullName.FirstName, employee.FullName.MiddleName }),
            async,
            cancellationToken).ConfigureAwait(false);

        return rows.Count == 0
            ? null
            : FullName.FromTrusted(rows[0].LastName, rows[0].FirstName, rows[0].MiddleName).Full;
    }
}

/// <summary>Ismi va rol nomi qo'shilgan bajaruvchi — audit qatoriga shu yoziladi.</summary>
/// <param name="Kind">Bajaruvchi turi.</param>
/// <param name="UserId">Tizim foydalanuvchisi.</param>
/// <param name="Login">Login nusxasi.</param>
/// <param name="Name">F.I.Sh. nusxasi.</param>
/// <param name="RoleName">Rol nomi nusxasi.</param>
internal sealed record ResolvedActor(
    AuditActorKind Kind,
    Guid? UserId,
    string? Login,
    string? Name,
    string? RoleName);
