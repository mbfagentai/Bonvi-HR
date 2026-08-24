using Bonvi.Application.Identity.Abstractions;
using Bonvi.Application.Identity.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Identity.Commands.Login;

/// <summary>Tizimga kirish so'rovi.</summary>
/// <param name="Login">Telefon raqami. 9 xona yoki +998 bilan.</param>
/// <param name="Password">Ochiq parol. Hech qayerga yozilmaydi va loglanmaydi.</param>
/// <param name="IpAddress">So'rov kelgan IP — audit va token zanjiri uchun.</param>
public sealed record LoginCommand(string Login, string Password, string? IpAddress);

/// <summary>
/// Kirishni bajaradi.
/// </summary>
/// <remarks>
/// <para><b>Xavfsizlik qarorlari va ularning sabablari:</b></para>
/// <list type="number">
///   <item>
///     <b>Bir xil xato.</b> «Bunday foydalanuvchi yo'q» va «parol noto'g'ri»
///     uchun bitta xato kodi qaytariladi. Aks holda hujumchi qaysi
///     raqamlar tizimda ro'yxatdan o'tganini aniqlab olishi mumkin.
///   </item>
///   <item>
///     <b>Vaqt bo'yicha oshkor qilmaslik.</b> Foydalanuvchi topilmasa ham
///     soxta xesh ustida tekshiruv bajariladi. Aks holda javob vaqtidagi
///     farq bo'yicha login mavjudligini bilib olish mumkin.
///   </item>
///   <item>
///     <b>Bloklash.</b> 5 ta ketma-ket xatodan keyin 15 daqiqa. Butunlay
///     bloklanmaydi — aks holda raqib xodimni ataylab qulflab qo'yishi mumkin.
///   </item>
///   <item>
///     <b>Xesh yangilash.</b> Parol eski algoritm bilan xeshlangan bo'lsa,
///     muvaffaqiyatli kirishdan keyin jimgina yangilanadi.
///   </item>
/// </list>
/// </remarks>
public sealed class LoginCommandHandler
{
    /// <summary>
    /// Foydalanuvchi topilmaganda tekshiriladigan soxta xesh.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Xesh <b>xeshlagichning o'zi orqali</b> hosil qilinadi — qo'lda yozilgan
    /// satr emas. Sabab: xesh formati algoritmga bog'liq (versiya bayti,
    /// tuz, iteratsiya soni). To'qib chiqarilgan satr dekodlashda istisno
    /// tashlaydi va «foydalanuvchi topilmadi» holati 500 xatoga aylanadi.
    /// </para>
    /// <para>
    /// Tasodifiy qiymatdan hosil qilinadi, shu sababli hech qanday parolga
    /// mos kelmaydi. Bir marta hisoblanib keshlanadi: xeshlash qimmat amal,
    /// lekin bizga aynan uning <b>vaqti</b> kerak — u haqiqiy tekshiruv
    /// bilan bir xil davom etadi.
    /// </para>
    /// </remarks>
    private static readonly Lock DummyHashLock = new();
    private static string? _dummyHash;

    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public LoginCommandHandler(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        ITokenService tokens,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<AuthResultDto>> HandleAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loginResult = PhoneNumber.Create(command.Login);
        if (loginResult.IsFailure)
        {
            // Telefon raqami formati noto'g'ri bo'lsa ham umumiy xato beriladi —
            // hujumchiga qo'shimcha ma'lumot bermaslik uchun
            return InvalidCredentials();
        }

        var now = _clock.UtcNow;
        var user = await _users.GetByLoginAsync(loginResult.Value, cancellationToken).ConfigureAwait(false);

        if (user is null)
        {
            // Vaqt bo'yicha oshkor qilmaslik: foydalanuvchi bo'lmasa ham
            // xesh tekshiruvi bajariladi (u bir necha o'nlab millisekund oladi)
            _ = _passwordHasher.Verify(GetDummyHash(), command.Password);
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

        var (succeeded, needsRehash) = _passwordHasher.Verify(user.PasswordHash, command.Password);

        if (!succeeded)
        {
            user.RecordFailedLogin(now);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return InvalidCredentials();
        }

        if (needsRehash)
        {
            user.ChangePassword(_passwordHasher.Hash(command.Password));
        }

        user.RecordSuccessfulLogin(now);

        var result = await IssueTokensAsync(user, command.IpAddress, cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }

    private async Task<AuthResultDto> IssueTokensAsync(
        User user,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var roleCodes = await _users.GetRoleCodesAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var permissions = await _users.GetPermissionsAsync(user.Id, cancellationToken).ConfigureAwait(false);

        var pair = _tokens.CreateTokens(user, roleCodes, permissions);

        _refreshTokens.Add(RefreshToken.Issue(
            user.Id,
            pair.RefreshTokenHash,
            pair.RefreshTokenExpiresAt,
            ipAddress));

        return new AuthResultDto(
            pair.AccessToken,
            pair.AccessTokenExpiresAt,
            pair.RefreshToken,
            new CurrentUserDto(
                user.Id,
                user.Login.Value,
                user.FullName.Full,
                user.FullName.ShortForm,
                user.EmployeeId,
                user.PhotoKey,
                roleCodes,
                [.. permissions]));
    }

    /// <summary>Soxta xeshni bir marta hisoblab, keshlab qo'yadi.</summary>
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

    /// <summary>
    /// Login va parol xatolari uchun YAGONA javob.
    /// Ikkisini ajratish hujumchiga foyda beradi, foydalanuvchiga esa yo'q.
    /// </summary>
    private static Error InvalidCredentials()
        => Error.Validation("auth.notogri_malumot", "Telefon raqami yoki parol noto'g'ri.");
}
