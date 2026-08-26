using Bonvi.Application.Identity.Users.Common;
using Bonvi.Application.Identity.Users.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.Users.Queries.ListUsers;

/// <summary>Foydalanuvchilar ro'yxatini so'rash.</summary>
public sealed record ListUsersQuery;

/// <summary>
/// Tizimdagi barcha hisoblarni qaytaradi — faol va faolsizlarini ham.
/// </summary>
/// <remarks>
/// <para>
/// Faolsizlar ATAYLAB yashirilmaydi: administrator xato bloklangan hisobni
/// shu ro'yxatning o'zidan qayta faollashtiradi. Filtrlash, qidiruv va
/// sahifalash yo'q — tizimda bor-yo'g'i bir nechta hisob, ular hammasi
/// bitta ekranga sig'adi.
/// </para>
/// <para>
/// Rollar bitta qo'shimcha so'rov bilan olinadi va xotirada moslashtiriladi:
/// foydalanuvchilar soni ham, rollar soni ham juda kichik (2 ta rol), shu
/// sababli har bir foydalanuvchi uchun alohida so'rov ortiqcha bo'lardi.
/// </para>
/// </remarks>
public sealed class ListUsersQueryHandler
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;

    public ListUsersQueryHandler(IUserRepository users, IRoleRepository roles)
    {
        _users = users;
        _roles = roles;
    }

    public async Task<Result<IReadOnlyList<UserSummaryDto>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await _users.ListAsync(cancellationToken).ConfigureAwait(false);
        var roles = await _roles.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var dtos = users
            .Select(user => UserSummaryDtoFactory.Create(user, roles))
            .OrderBy(dto => dto.FullName, StringComparer.CurrentCulture)
            .ToList();

        return Result.Success<IReadOnlyList<UserSummaryDto>>(dtos);
    }
}
