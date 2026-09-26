using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.Roles;

/// <summary>
/// Rollar ro'yxati — «Rollar» menyusi va foydalanuvchi formasidagi tanlash uchun.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bot roli ko'rinmaydi.</b> U odam uchun emas (<see cref="SystemRoles.RecruitingBot"/>
/// izohi): egasi uni na tahrirlaydi, na odamga beradi. Ro'yxatda tursa,
/// foydalanuvchi formasida ham tanlanadigan bo'lib qolardi.
/// </para>
/// <para>
/// <b>Tartib:</b> himoyalangan (super administrator) birinchi, qolganlari
/// nom bo'yicha — ro'yxatning tepasida doim «tegib bo'lmaydigan» rol turadi.
/// </para>
/// <para>
/// Hisoblar soni bitta qo'shimcha so'rov bilan olinadi va xotirada
/// sanaladi: tizimda bir nechta hisob bor, har bir rol uchun alohida
/// so'rov ortiqcha bo'lardi.
/// </para>
/// </remarks>
public sealed class ListRolesQueryHandler
{
    private readonly IRoleRepository _roles;
    private readonly IUserRepository _users;

    public ListRolesQueryHandler(IRoleRepository roles, IUserRepository users)
    {
        _roles = roles;
        _users = users;
    }

    public async Task<Result<IReadOnlyList<RoleDto>>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _roles.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var users = await _users.ListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var dtos = roles
            .Where(role => !string.Equals(role.Code, SystemRoles.RecruitingBot, StringComparison.Ordinal))
            .Select(role => RoleEditorRules.ToDto(role, users))
            .OrderByDescending(dto => dto.IsProtected)
            .ThenBy(dto => dto.Name, StringComparer.CurrentCulture)
            .ToList();

        return Result.Success<IReadOnlyList<RoleDto>>(dtos);
    }
}

/// <summary>
/// Ruxsatlar xaritasi — rollar muharriri shundan quriladi.
/// </summary>
/// <remarks>
/// Bazaga murojaat yo'q: xarita kodda (<see cref="PermissionCatalog"/>).
/// Baribir alohida use-case — endpoint domen turini to'g'ridan-to'g'ri
/// serializatsiya qilmasligi va javob shakli bir joyda belgilanishi uchun.
/// Statik: holati ham, bog'liqligi ham yo'q, DI orqali yasashning
/// ma'nosi yo'q.
/// </remarks>
public static class GetPermissionCatalogQuery
{
    public static Result<PermissionCatalogDto> Handle()
    {
        var modules = PermissionCatalog.Modules
            .Select(module => new PermissionModuleDto(
                module.Key,
                module.Levels.ToDictionary(
                    level => level.Key,
                    level => level.Permissions,
                    StringComparer.Ordinal),
                module.Actions.Count == 0 ? null : module.Actions))
            .ToList();

        return new PermissionCatalogDto(modules, PermissionCatalog.Extras);
    }
}
