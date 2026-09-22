using System.ComponentModel.DataAnnotations;

namespace Bonvi.Infrastructure.Identity;

/// <summary>
/// Xodim ilovasi tokenlarining sozlamalari (<c>appsettings.json</c> dagi «EmployeeJwt»).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Imzolash kaliti bu yerda YO'Q</b> va bo'lmasligi ham kerak: ikkala
/// token ham BIR XIL kalit bilan imzolanadi (<c>Jwt:SigningKey</c>), ularni
/// bir-biridan AUDITORIYA ajratib turadi. Ikkinchi kalit ikkinchi sir
/// degani — uni aylantirish, zaxiralash va muhitlar orasida sinxron
/// saqlash kerak bo'lardi, hech qanday yangi himoya bermay: auditoriya
/// tekshiruvi kalitdan mustaqil va u ham xuddi shunday qat'iy.
/// </para>
/// <para>
/// <b>Muddatlar HR tokenidan boshqa.</b> Access token bir xil qisqa
/// (uni serverda bekor qilib bo'lmaydi), yangilash tokeni esa uzunroq:
/// xodim ilovani kuniga ikki marta ochadi va har haftada qaytadan parol
/// terishga majburlash uni terminalga qaytarardi — ya'ni imkoniyat
/// ishlatilmay qolardi. Yo'qotilgan telefonga javob tayyor: HR hisobni
/// yopadi va barcha tokenlar o'sha lahzada o'ladi.
/// </para>
/// </remarks>
public sealed class EmployeeJwtOptions
{
    public const string SectionName = "EmployeeJwt";

    /// <summary>
    /// Token kim uchun — HR auditoriyasidan FARQ QILISHI shart.
    /// </summary>
    /// <remarks>
    /// ⚠️ Bu qiymat HR tokeni bilan bir xil bo'lib qolsa, xodim tokeni HR
    /// endpointlariga kira boshlaydi. Aynan shuning uchun ishga tushishda
    /// tekshiriladi (<c>AuthenticationExtensions</c>).
    /// </remarks>
    [Required]
    public string Audience { get; set; } = "bonvi-employee";

    /// <summary>Access token muddati (daqiqa).</summary>
    [Range(1, 120)]
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Yangilash tokeni muddati (kun).</summary>
    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = 30;
}
