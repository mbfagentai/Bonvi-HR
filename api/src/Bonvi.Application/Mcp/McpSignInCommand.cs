using Bonvi.Application.Identity.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Mcp;

/// <summary>
/// AI ulanish sahifasidagi kirish: telefon + parol (<c>docs/SPEC-mcp.md</c> §5).
/// </summary>
public sealed record McpSignInCommand(string Login, string Password);

/// <summary>Kirish natijasi — token EMAS, faqat «kim ekani».</summary>
/// <param name="UserId">Foydalanuvchi.</param>
/// <param name="FullName">Rozilik sahifasida ko'rsatiladigan ism.</param>
/// <param name="CanConnect">Uning rolida <see cref="Permissions.McpConnect"/> bormi.</param>
public sealed record McpSignInResult(Guid UserId, string FullName, bool CanConnect);

/// <summary>
/// HR kirishi bilan AYNAN bir xil qoidalar: noto'g'ri telefon formati ham
/// umumiy xato, mavjud bo'lmagan hisobda ham xesh tekshiriladi (vaqt bo'yicha
/// oshkor qilmaslik), 5 xatodan keyin 15 daqiqalik qulf, faol bo'lmagan hisob
/// rad etiladi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega <c>LoginCommandHandler</c> ning o'zi emas.</b> U JWT va yangilash
/// tokenini CHIQARADI va <c>refresh_tokens</c> ga yozadi — AI ulanishiga esa
/// veb-sessiya kerak emas, unga faqat «parol to'g'rimi va bu kim» degan javob
/// kerak. Sessiya ochilsa, foydalanuvchining «faol sessiyalar» ro'yxatida
/// hech qachon ishlatilmaydigan yozuv paydo bo'lardi.
/// </para>
/// <para>
/// Qulf hisoblagichi UMUMIY: AI sahifasidagi xato urinish veb-kirishdagi
/// hisoblagichga ham qo'shiladi va aksincha — aks holda ikkinchi sahifa
/// parol tanlashning chetlab o'tish yo'li bo'lardi.
/// </para>
/// </remarks>
public sealed class McpSignInCommandHandler
{
    private static readonly Lock DummyHashLock = new();
    private static string? _dummyHash;

    private readonly IUserRepository _users;
    private readonly IUserAccessReader _access;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public McpSignInCommandHandler(
        IUserRepository users,
        IUserAccessReader access,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _users = users;
        _access = access;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<McpSignInResult>> HandleAsync(
        McpSignInCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var login = PhoneNumber.Create(command.Login);
        if (login.IsFailure)
        {
            _ = _passwordHasher.Verify(GetDummyHash(), command.Password ?? string.Empty);
            return InvalidCredentials();
        }

        var now = _clock.UtcNow;
        var user = await _users.GetByLoginAsync(login.Value, cancellationToken).ConfigureAwait(false);

        if (user is null)
        {
            _ = _passwordHasher.Verify(GetDummyHash(), command.Password ?? string.Empty);
            return InvalidCredentials();
        }

        if (user.IsLockedAt(now))
        {
            var minutes = (int)Math.Ceiling((user.LockedUntil!.Value - now).TotalMinutes);
            return Error.Forbidden(
                "auth.bloklangan",
                $"Ko'p marta noto'g'ri urinish. {minutes} daqiqadan keyin qayta urinib ko'ring.");
        }

        if (!user.IsActive)
        {
            return Error.Forbidden("auth.hisob_faol_emas", "Hisob faol emas. Administratorga murojaat qiling.");
        }

        var (succeeded, needsRehash) = _passwordHasher.Verify(user.PasswordHash, command.Password ?? string.Empty);

        if (!succeeded)
        {
            user.RecordFailedLogin(now);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return InvalidCredentials();
        }

        if (needsRehash)
        {
            user.ChangePassword(_passwordHasher.Hash(command.Password!));
        }

        user.RecordSuccessfulLogin(now);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var access = await _access.ReadAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var canConnect = access?.Permissions.Contains(Permissions.McpConnect) == true;

        return new McpSignInResult(user.Id, user.FullName.Full, canConnect);
    }

    private string GetDummyHash()
    {
        if (_dummyHash is not null)
        {
            return _dummyHash;
        }

        lock (DummyHashLock)
        {
            _dummyHash ??= _passwordHasher.Hash(Guid.NewGuid().ToString("N"));
        }

        return _dummyHash;
    }

    private static Error InvalidCredentials()
        => Error.Validation("auth.notogri_malumot", "Telefon raqami yoki parol noto'g'ri.");
}
