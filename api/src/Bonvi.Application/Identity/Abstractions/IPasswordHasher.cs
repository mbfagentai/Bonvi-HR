namespace Bonvi.Application.Identity.Abstractions;

/// <summary>
/// Parolni xeshlash va tekshirish porti.
/// </summary>
/// <remarks>
/// Algoritm Infrastructure qatlamida tanlanadi. Kelajakda uni almashtirish
/// (masalan PBKDF2 dan Argon2 ga) biznes mantiqqa tegmaydi.
/// </remarks>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Parolni tekshiradi.
    /// </summary>
    /// <returns>
    /// <c>NeedsRehash</c> — xesh eski algoritm bilan yaratilgan bo'lsa <c>true</c>.
    /// U holda muvaffaqiyatli kirishdan keyin xeshni yangilash mumkin.
    /// </returns>
    (bool Succeeded, bool NeedsRehash) Verify(string passwordHash, string password);
}
