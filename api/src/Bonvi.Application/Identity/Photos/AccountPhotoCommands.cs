using Bonvi.Application.Abstractions;
using Bonvi.Application.Common.Photos;
using Bonvi.Application.Identity.Common;
using Bonvi.Application.Identity.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.Photos;

/// <summary>Joriy foydalanuvchining rasmini yuklash buyrug'i.</summary>
/// <param name="Content">Yuklangan faylning oqimi.</param>
/// <param name="ContentType">Mijoz e'lon qilgan MIME turi.</param>
/// <param name="SizeBytes">Faylning e'lon qilingan hajmi.</param>
/// <remarks>
/// Buyruqda foydalanuvchi identifikatori YO'Q va bo'lmasligi ham kerak:
/// u tokendan olinadi. Maydon bo'lsa, uni tashqaridan berish mumkin bo'lardi
/// va boshqa hisobning rasmini almashtirish yo'li ochilardi.
/// </remarks>
public sealed record UploadAccountPhotoCommand(Stream Content, string? ContentType, long SizeBytes);

/// <summary>
/// Hisob rasmini qabul qiladi, kvadrat qilib kichraytiradi va saqlaydi.
/// </summary>
/// <remarks>
/// Quvurning o'zi xodim rasmi bilan bir xil (<see cref="SquarePhotoStore"/>),
/// farqi faqat kalit qayerga yozilishida. Eski fayl bazadan KEYIN o'chiriladi:
/// teskari tartibda saqlash yiqilsa, hisob mavjud bo'lmagan faylga ishora
/// qilib qolardi.
/// </remarks>
public sealed class UploadAccountPhotoCommandHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly SquarePhotoStore _photos;
    private readonly IUnitOfWork _unitOfWork;

    public UploadAccountPhotoCommandHandler(
        ICurrentUser currentUser,
        IUserRepository users,
        SquarePhotoStore photos,
        IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _users = users;
        _photos = photos;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AccountPhotoDto>> HandleAsync(
        UploadAccountPhotoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = await CurrentAccount.LoadAsync(_currentUser, _users, cancellationToken).ConfigureAwait(false);
        if (account.IsFailure)
        {
            return account.Error;
        }

        var user = account.Value;

        var accepted = await _photos
            .AcceptAsync(command.Content, command.ContentType, command.SizeBytes, cancellationToken)
            .ConfigureAwait(false);

        if (accepted.Key is not { } key)
        {
            return AccountPhotoErrors.Rejected(accepted.Rejection!.Value);
        }

        var previousKey = user.PhotoKey;
        user.SetPhoto(key);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (previousKey is not null)
        {
            await _photos.DeleteAsync(previousKey, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new AccountPhotoDto(key));
    }
}

/// <summary>
/// Hisob rasmini o'chiradi.
/// </summary>
/// <remarks>
/// Amal <b>idempotent</b>: rasm allaqachon yo'q bo'lsa ham muvaffaqiyat
/// qaytadi — so'rovning maqsadi rasm qolmasligi, u esa bajarilgan.
/// </remarks>
public sealed class DeleteAccountPhotoCommandHandler
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;
    private readonly SquarePhotoStore _photos;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteAccountPhotoCommandHandler(
        ICurrentUser currentUser,
        IUserRepository users,
        SquarePhotoStore photos,
        IUnitOfWork unitOfWork)
    {
        _currentUser = currentUser;
        _users = users;
        _photos = photos;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(CancellationToken cancellationToken = default)
    {
        var account = await CurrentAccount.LoadAsync(_currentUser, _users, cancellationToken).ConfigureAwait(false);
        if (account.IsFailure)
        {
            return account.Error;
        }

        var user = account.Value;

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
