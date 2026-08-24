using Bonvi.Application.Abstractions;
using Bonvi.Application.Common.Photos;
using Bonvi.Application.Identity.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.Photos;

/// <summary>Joriy foydalanuvchining rasmini o'qish so'rovi.</summary>
/// <param name="KnownVersion">
/// Mijozdagi nusxaning versiya belgisi. Serverdagi bilan mos kelsa, rasm
/// qayta yuborilmaydi (HTTP darajasida bu <c>If-None-Match</c>).
/// </param>
public sealed record GetAccountPhotoQuery(string? KnownVersion = null);

/// <summary>
/// Hisob rasmini qaytaradi — faqat SO'ROVCHINING o'zinikini.
/// </summary>
/// <remarks>
/// Boshqa foydalanuvchining rasmini so'rash yo'li yo'q: manzilda
/// identifikator umuman qatnashmaydi. Shu sababli bu yerda ruxsat kodi
/// tekshirilmaydi — chegara identifikatorning O'ZI bilan qo'yilgan.
/// </remarks>
public sealed class GetAccountPhotoQueryHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly SquarePhotoStore _photos;

    public GetAccountPhotoQueryHandler(
        ICurrentUser currentUser,
        IUserRepository users,
        SquarePhotoStore photos)
    {
        _currentUser = currentUser;
        _users = users;
        _photos = photos;
    }

    public async Task<Result<PhotoContent>> HandleAsync(
        GetAccountPhotoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var account = await CurrentAccount.LoadAsync(_currentUser, _users, cancellationToken).ConfigureAwait(false);
        if (account.IsFailure)
        {
            return account.Error;
        }

        if (account.Value.PhotoKey is not { } key)
        {
            return AccountPhotoErrors.NotFound;
        }

        var content = await _photos.OpenAsync(key, query.KnownVersion, cancellationToken).ConfigureAwait(false);

        if (content is null)
        {
            return AccountPhotoErrors.NotFound;
        }

        return Result.Success(content);
    }
}
