using Bonvi.Application.Abstractions;
using Bonvi.Application.Identity.Common;
using Bonvi.Application.Identity.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.Queries.GetCurrentUser;

/// <summary>Joriy foydalanuvchi haqidagi ma'lumotni so'rash.</summary>
public sealed record GetCurrentUserQuery;

/// <summary>
/// Tizimga kirgan foydalanuvchini qaytaradi.
/// </summary>
/// <remarks>
/// Frontend sahifa yangilanganda shu endpoint'ni chaqiradi: token
/// brauzerda saqlangan, lekin foydalanuvchi ma'lumoti yo'q.
/// Ruxsatlar token ichida ham bor, lekin bu yerda bazadan olinadi —
/// administrator ruxsatni o'zgartirgan bo'lsa, darhol kuchga kiradi.
/// </remarks>
public sealed class GetCurrentUserQueryHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;

    public GetCurrentUserQueryHandler(ICurrentUser currentUser, IUserRepository users)
    {
        _currentUser = currentUser;
        _users = users;
    }

    public async Task<Result<CurrentUserDto>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var account = await CurrentAccount.LoadAsync(_currentUser, _users, cancellationToken).ConfigureAwait(false);
        if (account.IsFailure)
        {
            return account.Error;
        }

        var user = account.Value;

        var roleCodes = await _users.GetRoleCodesAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var permissions = await _users.GetPermissionsAsync(user.Id, cancellationToken).ConfigureAwait(false);

        return new CurrentUserDto(
            user.Id,
            user.Login.Value,
            user.FullName.Full,
            user.FullName.ShortForm,
            user.EmployeeId,
            user.PhotoKey,
            roleCodes,
            [.. permissions]);
    }
}
