using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Abstractions;

/// <summary>Foydalanuvchilar uchun saqlash porti.</summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>Login (telefon raqami) bo'yicha topadi. Rollari bilan birga yuklanadi.</summary>
    Task<User?> GetByLoginAsync(PhoneNumber login, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bunday login band emasligini tekshiradi.
    /// </summary>
    /// <param name="login">Tekshirilayotgan telefon raqami.</param>
    /// <param name="exceptUserId">
    /// Shu identifikatordagi foydalanuvchi e'tiborga olinmaydi — tahrirlashda
    /// foydalanuvchi o'z eski raqamini saqlab qolsa, u "band" deb chiqmasligi kerak.
    /// </param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<bool> LoginExistsAsync(
        PhoneNumber login,
        Guid? exceptUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Barcha foydalanuvchilar (faol va faolsizlar ham) — boshqaruv ro'yxati uchun.
    /// </summary>
    /// <remarks>
    /// Sahifalash yo'q: tizimda bor-yo'g'i bir nechta foydalanuvchi (HR bo'limi
    /// va rahbariyat), yuzlab emas — butun ro'yxatni bitta so'rovda olish yetarli.
    /// </remarks>
    Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Foydalanuvchining barcha ruxsatlari — rollari orqali yig'ilgan.
    /// </summary>
    /// <remarks>
    /// Alohida metod, chunki bu har bir kirishda va token yangilashda kerak.
    /// Bitta so'rov bilan olinadi — rollarni yuklab, keyin C# da yig'ish emas.
    /// </remarks>
    Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Foydalanuvchining rol kodlari.</summary>
    Task<IReadOnlyList<string>> GetRoleCodesAsync(Guid userId, CancellationToken cancellationToken = default);
}
