using Bonvi.Application.Common.Photos;
using Bonvi.Application.Identity.Users.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.Users.Photos;

/// <summary>Boshqa foydalanuvchining rasmini o'qish so'rovi.</summary>
/// <param name="UserId">Kimning rasmi.</param>
/// <param name="KnownVersion">
/// Mijozdagi nusxaning versiya belgisi. Serverdagi bilan mos kelsa, rasm
/// qayta yuborilmaydi (HTTP darajasida bu <c>If-None-Match</c>).
/// </param>
public sealed record GetUserPhotoQuery(Guid UserId, string? KnownVersion = null);

/// <summary>
/// Foydalanuvchi rasmini qaytaradi — boshqaruv ro'yxati va tahrirlash
/// oynasi uchun.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bu yerda «kim kimga tega oladi» qoidasi YO'Q.</b> Ro'yxat
/// (<c>GET /api/users</c>) hamma hisobni QAYTARADI: bosh rahbar super
/// administratorni ro'yxatda ko'radi, faqat unga tegishli amallarni
/// bajara olmaydi. Rasm ham xuddi shu ma'lumotning bir qismi — ismi va
/// telefoni ko'rinib, surati o'rniga bosh harflar qolishi hech kimni
/// himoya qilmaydi, faqat ro'yxatni chala ko'rsatardi. Chegara YOZISHDA
/// qo'yiladi (<see cref="UploadUserPhotoCommandHandler"/>), o'qishda emas.
/// </para>
/// <para>
/// Kalitni topish — shu handler ishi, faylni ochish va versiyani solishtirish
/// esa <see cref="SquarePhotoStore"/> niki. Fayl yo'qolgan holat (kalit bazada
/// bor, fayl esa yo'q) ham «rasm yo'q» deb qaraladi: interfeys uchun natija
/// bir xil, xato ko'rsatishning ma'nosi yo'q.
/// </para>
/// </remarks>
public sealed class GetUserPhotoQueryHandler
{
    private readonly IUserRepository _users;
    private readonly SquarePhotoStore _photos;

    public GetUserPhotoQueryHandler(IUserRepository users, SquarePhotoStore photos)
    {
        _users = users;
        _photos = photos;
    }

    public async Task<Result<PhotoContent>> HandleAsync(
        GetUserPhotoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var user = await _users.GetByIdAsync(query.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (user.PhotoKey is not { } key)
        {
            return UserPhotoErrors.NotFound;
        }

        var content = await _photos.OpenAsync(key, query.KnownVersion, cancellationToken).ConfigureAwait(false);

        if (content is null)
        {
            return UserPhotoErrors.NotFound;
        }

        return Result.Success(content);
    }
}
