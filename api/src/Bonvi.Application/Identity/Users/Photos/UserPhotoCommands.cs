using Bonvi.Application.Abstractions;
using Bonvi.Application.Common.Photos;
using Bonvi.Application.Identity.Common;
using Bonvi.Application.Identity.Users.Common;
using Bonvi.Application.Identity.Users.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.Users.Photos;

/// <summary>Boshqa foydalanuvchining rasmini yuklash buyrug'i.</summary>
/// <param name="UserId">Kimning rasmi almashtirilmoqda.</param>
/// <param name="Content">Yuklangan faylning oqimi.</param>
/// <param name="ContentType">Mijoz e'lon qilgan MIME turi.</param>
/// <param name="SizeBytes">Faylning e'lon qilingan hajmi.</param>
/// <remarks>
/// Bu yerda identifikator BOR va bo'lishi ham kerak — «/api/auth/me/photo»
/// dan farqi aynan shu: administrator BOSHQA hisobga rasm qo'yadi. Shu
/// sababli u yerda keraksiz bo'lgan qoida bu yerda majburiy:
/// <see cref="UserRoleRules.EnsureCanActOn"/>.
/// </remarks>
public sealed record UploadUserPhotoCommand(Guid UserId, Stream Content, string? ContentType, long SizeBytes);

/// <summary>
/// Foydalanuvchi rasmini qabul qiladi, kvadrat qilib kichraytiradi va saqlaydi.
/// </summary>
/// <remarks>
/// <para>
/// Quvurning o'zi hisob rasmi va xodim rasmi bilan bir xil
/// (<see cref="SquarePhotoStore"/>), farqi faqat kalit qayerga yozilishida
/// va KIM yozayotganida. Eski fayl bazadan KEYIN o'chiriladi: teskari
/// tartibda saqlash yiqilsa, hisob mavjud bo'lmagan faylga ishora qilib
/// qolardi.
/// </para>
/// <para>
/// <b>Nega rasm ham «kim kimga tega oladi» qoidasi ostida.</b> Rasm —
/// hisobning ko'rinadigan yuzi: uni almashtira olgan odam ro'yxatda
/// boshqa birovni istagan qiyofada ko'rsata oladi. Bosh rahbar super
/// administratorning ismini tahrirlay olmasa, suratini ham almashtira
/// olmasligi kerak — aks holda qoida chala bo'lardi.
/// </para>
/// </remarks>
public sealed class UploadUserPhotoCommandHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly SquarePhotoStore _photos;
    private readonly IUnitOfWork _unitOfWork;

    public UploadUserPhotoCommandHandler(
        ICurrentUser currentUser,
        IUserRepository users,
        IRoleRepository roles,
        SquarePhotoStore photos,
        IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _users = users;
        _roles = roles;
        _photos = photos;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserPhotoDto>> HandleAsync(
        UploadUserPhotoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var target = await UserPhotoAccess
            .LoadTargetAsync(_currentUser, _users, _roles, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (target.IsFailure)
        {
            return target.Error;
        }

        var user = target.Value;

        var accepted = await _photos
            .AcceptAsync(command.Content, command.ContentType, command.SizeBytes, cancellationToken)
            .ConfigureAwait(false);

        if (accepted.Key is not { } key)
        {
            return UserPhotoErrors.Rejected(accepted.Rejection!.Value);
        }

        var previousKey = user.PhotoKey;
        user.SetPhoto(key);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (previousKey is not null)
        {
            await _photos.DeleteAsync(previousKey, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new UserPhotoDto(key));
    }
}

/// <summary>
/// Foydalanuvchi rasmini o'chiradi.
/// </summary>
/// <remarks>
/// Amal <b>idempotent</b>: rasm allaqachon yo'q bo'lsa ham muvaffaqiyat
/// qaytadi — so'rovning maqsadi rasm qolmasligi, u esa bajarilgan. Ruxsat
/// tekshiruvi esa idempotentlikdan OLDIN: «rasm baribir yo'q edi» degan
/// sabab bilan qoidani chetlab o'tishga yo'l qoldirmaslik uchun.
/// </remarks>
public sealed class DeleteUserPhotoCommandHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly SquarePhotoStore _photos;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteUserPhotoCommandHandler(
        ICurrentUser currentUser,
        IUserRepository users,
        IRoleRepository roles,
        SquarePhotoStore photos,
        IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _users = users;
        _roles = roles;
        _photos = photos;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var target = await UserPhotoAccess
            .LoadTargetAsync(_currentUser, _users, _roles, userId, cancellationToken)
            .ConfigureAwait(false);

        if (target.IsFailure)
        {
            return target.Error;
        }

        var user = target.Value;

        if (user.PhotoKey is not { } key)
        {
            return Result.Success();
        }

        user.SetPhoto(null);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _photos.DeleteAsync(key, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary>
/// Rasm yozadigan ikkala buyruqning umumiy kirish tekshiruvi.
/// </summary>
/// <remarks>
/// Yuklash va o'chirish uchun qadamlar AYNAN bir xil: so'rovchini aniqlash,
/// nishonni topish, «kim kimga tega oladi» qoidasini qo'llash. Ikki joyda
/// takrorlansa, bir kun kelib biri (ehtimol o'chirish — u soddaroq
/// ko'rinadi) unutilardi va aynan o'sha yo'l orqali bosh rahbar super
/// administratorning suratini o'chirib yuborardi.
/// </remarks>
internal static class UserPhotoAccess
{
    public static async Task<Result<User>> LoadTargetAsync(
        ICurrentUser currentUser,
        IUserRepository users,
        IRoleRepository roles,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        var acting = await CurrentAccount.LoadAsync(currentUser, users, cancellationToken).ConfigureAwait(false);
        if (acting.IsFailure)
        {
            return acting.Error;
        }

        var target = await users.GetByIdAsync(targetId, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return UserErrors.NotFound;
        }

        var allRoles = await roles.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var allowed = UserRoleRules.EnsureCanActOn(acting.Value, target, allRoles);
        if (allowed.IsFailure)
        {
            return allowed.Error;
        }

        return Result.Success(target);
    }
}
