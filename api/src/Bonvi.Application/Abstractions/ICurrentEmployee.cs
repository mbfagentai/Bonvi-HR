namespace Bonvi.Application.Abstractions;

/// <summary>
/// Joriy so'rovni yuborayotgan XODIM (ilova hisobi).
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Nega <see cref="ICurrentUser"/> ga qo'shilmadi.</b> Ular ikki xil
/// subyekt: HR foydalanuvchisining roli va ruxsatlari bor, xodimning esa
/// umuman yo'q. Bitta interfeysga qo'shilsa, har bir use-case «bu kim?»
/// degan shartni o'zi yozishi kerak bo'lardi va bitta unutilgan shart HR
/// ma'lumotini 950 kishiga ochib qo'yardi. Ikki interfeys bilan savolning
/// o'zi tug'ilmaydi: <c>/api/me/*</c> ishlovchilari faqat shuni oladi.
/// </para>
/// <para>
/// ⚠️ <c>null</c> qaytishi — dastur xatosi emas, oddiy holat: bu portni HR
/// tokeni bilan kelgan so'rovda ham so'rash mumkin. Marshrutning o'zi
/// <c>bonvi-employee</c> auditoriyasi bilan qo'riqlanadi, ya'ni ishlovchiga
/// yetib kelgan so'rovda qiymat har doim bor.
/// </para>
/// </remarks>
public interface ICurrentEmployee
{
    /// <summary>Ilova hisobining identifikatori (<c>sub</c> da'vosi).</summary>
    /// <remarks>
    /// Tokenning subyekti — XODIM emas, HISOB. Hisob qayta berilganda eski
    /// tokenlar yangilaridan ajralib turishi kerak.
    /// </remarks>
    Guid? AccountId { get; }

    /// <summary>Hisob bog'langan xodim (<c>emp</c> da'vosi).</summary>
    Guid? EmployeeId { get; }
}
