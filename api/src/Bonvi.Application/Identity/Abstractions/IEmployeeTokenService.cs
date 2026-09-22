using Bonvi.Domain.Identity;

namespace Bonvi.Application.Identity.Abstractions;

/// <summary>Xodim ilovasi uchun token yaratish porti.</summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega <see cref="ITokenService"/> ga metod qo'shilmadi.</b> U
/// <see cref="User"/> ni, rollarni va ruxsatlar to'plamini oladi — ya'ni
/// uning butun imzosi HR hisobiga moslangan. Xodim uchun esa ular YO'Q va
/// bo'lmasligi ham kerak. Bitta interfeysda ikkala metod tursa, birinchi
/// e'tiborsiz chaqiruvda xodim tokeniga ruxsat da'vosi tushib qolishi
/// mumkin edi — bu esa audit chegarasining jimgina yo'qolishi.
/// </para>
/// <para>
/// ⚠️ Yaratilgan token <b>boshqa auditoriya</b> (<c>bonvi-employee</c>)
/// bilan imzolanadi: HR endpointiga ko'rsatilgan xodim tokeni imzosi
/// to'g'ri bo'lsa ham auditoriya tekshiruvidan o'tmaydi. «Deyarli
/// ruxsatli» degan holat texnik jihatdan mavjud emas.
/// </para>
/// </remarks>
public interface IEmployeeTokenService
{
    /// <summary>Hisob uchun token juftligini yaratadi. Ruxsat da'volari YOZILMAYDI.</summary>
    /// <param name="account">Ilova hisobi.</param>
    TokenPair CreateTokens(EmployeeAccount account);

    /// <summary>Ochiq yangilash tokenidan uning xeshini hisoblaydi.</summary>
    /// <param name="refreshToken">Ochiq token.</param>
    string HashRefreshToken(string refreshToken);
}
