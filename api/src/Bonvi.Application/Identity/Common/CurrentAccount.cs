using Bonvi.Application.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.Common;

/// <summary>
/// «So'rovni kim yubordi» degan savolga javob — bitta joyda.
/// </summary>
/// <remarks>
/// <para>
/// Bu tekshiruv hisob bilan ishlaydigan har bir use-case'da takrorlanadi:
/// token bormi, undagi foydalanuvchi bazada bormi va u faolmi. Har birida
/// qo'lda yozilsa, bir kun kelib biror joyda «faolmi» tekshiruvi unutilardi
/// — va bloklangan hisob o'z rasmini almashtiraverardi.
/// </para>
/// <para>
/// <b>Xavfsizlik nuqtai nazaridan eng muhimi:</b> identifikator so'rov
/// tanasidan yoki manzilidan EMAS, tokendan olinadi. Shu sababli
/// «/api/auth/me/...» endpoint'lari boshqa foydalanuvchiga tegishli
/// ma'lumotni umuman ko'ra olmaydi — ruxsat kodi kerak emas.
/// </para>
/// </remarks>
public static class CurrentAccount
{
    public static async Task<Result<User>> LoadAsync(
        ICurrentUser currentUser,
        IUserRepository users,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(users);

        if (currentUser.UserId is not { } userId)
        {
            return Error.Forbidden("auth.kirilmagan", "Tizimga kirilmagan.");
        }

        var user = await users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);

        if (user is null || !user.IsActive)
        {
            return Error.Forbidden("auth.hisob_faol_emas", "Hisob faol emas.");
        }

        return Result.Success(user);
    }
}
