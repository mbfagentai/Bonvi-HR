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
    /// <param name="search">
    /// F.I.Sh. yoki LOGIN (telefon raqami) bo'yicha qidiruv. <c>null</c>
    /// yoki bo'sh — qidiruvsiz.
    /// <para>
    /// <b>Nega xodimlar qidiruvidan farq qiladi.</b> Foydalanuvchi — bu
    /// tizimga kiradigan hisob; unda JSHSHIR ham, pasport ham yo'q va
    /// bo'lmaydi (hisob xodimga ixtiyoriy bog'lanadi, uning nusxasi emas).
    /// Shu sababli bu yerda ikki maydon qoladi va telefon qoidasi
    /// xodimlarnikidan nusxa OLINMAYDI — bir xil yordamchidan keladi.
    /// </para>
    /// <para>
    /// <b>Nega sahifalanmagan ro'yxatda ham serverda.</b> Ro'yxat kichik,
    /// lekin qidiruv qoidasi (ism yig'ish, telefon ajratgichlari) bazada
    /// yashaydi. Uni klientga ko'chirish qoidaning ikkinchi nusxasini
    /// yaratardi va ular vaqt o'tib ajralib ketardi.
    /// </para>
    /// </param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    Task<IReadOnlyList<User>> ListAsync(
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Foydalanuvchining barcha ruxsatlari — rollari orqali yig'ilgan.
    /// </summary>
    /// <remarks>
    /// Alohida metod, chunki bu har bir kirishda va token yangilashda kerak.
    /// Bitta so'rov bilan olinadi — rollarni yuklab, keyin C# da yig'ish emas.
    /// </remarks>
    Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Foydalanuvchining rol NOMLARI — ekranda ko'rsatish uchun.
    /// </summary>
    /// <remarks>
    /// Rollarni egasi o'zi yaratadi va nomlaydi (2026-09-26), ya'ni yangi
    /// rolning tarjima kaliti yo'q — interfeys uni faqat nomi bilan ko'rsata
    /// oladi. Tartib <see cref="GetRoleCodesAsync"/> bilan bir xil (kod bo'yicha).
    /// </remarks>
    Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Foydalanuvchining rol kodlari.</summary>
    Task<IReadOnlyList<string>> GetRoleCodesAsync(Guid userId, CancellationToken cancellationToken = default);
}
