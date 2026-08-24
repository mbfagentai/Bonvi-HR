using Bonvi.Application.Identity.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Bonvi.Infrastructure.Identity;

/// <summary>Parol xeshlash uchun "egasi" bo'lmagan belgi tipi.</summary>
internal sealed class PasswordOwner;

/// <summary>
/// ASP.NET Core Identity'ning parol xeshlagichi ustidagi qobiq.
/// </summary>
/// <remarks>
/// <para>
/// Algoritm: PBKDF2 + HMAC-SHA512, 100 000 iteratsiya, 128-bitli tasodifiy tuz.
/// Har bir parol uchun tuz alohida — bir xil parollar bazada har xil ko'rinadi,
/// shu sababli oldindan hisoblangan jadvallar (rainbow table) ish bermaydi.
/// </para>
/// <para>
/// Nega o'z algoritmimizni yozmadik: parol xeshlash — bu xato qilish juda oson
/// bo'lgan soha. Microsoft'ning amalga oshirilishi tekshirilgan, doimiy
/// yangilanadi va iteratsiya sonini vaqt o'tishi bilan oshirib boradi.
/// </para>
/// <para>
/// <c>Verify</c> ikkinchi qiymat sifatida <c>NeedsRehash</c> qaytaradi —
/// xesh eski format bilan yaratilgan bo'lsa, muvaffaqiyatli kirishdan keyin
/// uni jimgina yangilash mumkin. Foydalanuvchi buni sezmaydi.
/// </para>
/// </remarks>
public sealed class BonviPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<PasswordOwner> _inner = new();
    private static readonly PasswordOwner Owner = new();

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return _inner.HashPassword(Owner, password);
    }

    public (bool Succeeded, bool NeedsRehash) Verify(string passwordHash, string password)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || string.IsNullOrEmpty(password))
        {
            return (false, false);
        }

        var result = _inner.VerifyHashedPassword(Owner, passwordHash, password);

        return result switch
        {
            PasswordVerificationResult.Success => (true, false),
            PasswordVerificationResult.SuccessRehashNeeded => (true, true),
            _ => (false, false),
        };
    }
}
