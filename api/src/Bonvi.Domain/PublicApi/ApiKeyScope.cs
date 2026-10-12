namespace Bonvi.Domain.PublicApi;

/// <summary>
/// Tashqi API kaliti nimani o'qiy oladi.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Basic"/> — holdingning boshqa loyihalari uchun (2026-09-18):
/// faqat xodimlar ro'yxati, tashkiliy ma'lumot va suratlar. Oklad, manzil,
/// izohlar bu doirada HECH QACHON chiqmaydi.
/// </para>
/// <para>
/// <see cref="FullRead"/> — egasining qarori (2026-10-11): rahbarning AI
/// yordamchisi «barcha ma'lumotni» o'qishi kerak. U AI ulanish (MCP) bilan
/// aynan bir xil o'qish vositalarini oladi — barcha bo'limlar, oklad ham.
/// Pasport, JSHSHIR va yuklangan fayllar bu doirada ham YO'Q (baza roli va
/// javob tozalagichi darajasida yopiq).
/// </para>
/// </remarks>
public enum ApiKeyScope
{
    /// <summary>Faqat xodimlar va ma'lumotnomalar (eski kalitlar shu doirada).</summary>
    Basic = 1,

    /// <summary>Barcha bo'limlarni o'qish — pasport, JSHSHIR va fayllardan tashqari.</summary>
    FullRead = 2,
}

/// <summary>Doiralarning mazmuni — kim nimani o'qiydi va kim nimani bera oladi.</summary>
public static class ApiKeyScopes
{
    /// <summary>
    /// <see cref="ApiKeyScope.FullRead"/> kalit qaysi «ko'rish» ruxsatlari bilan
    /// ishlaydi — AI ulanish (MCP) vositalari tekshiradigan HAMMASI, oklad ham.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Boshqarish (<c>*.manage</c>) ruxsatlari yo'q: vositalar faqat o'qiydi,
    /// ro'yxat esa «kalit nimani ko'radi» degan savolning yagona javobi.
    /// </para>
    /// <para>
    /// Shu ro'yxat kalitni YARATISH shartini ham belgilaydi: to'liq kalitni
    /// faqat bularning hammasi (va <see cref="Identity.Permissions.McpConnect"/>)
    /// bor odam yarata oladi — «o'zingda yo'q huquqni bera olmaysan»
    /// (rol muharriridagi <c>rol.ortiqcha_ruxsat</c> bilan bir xil qoida).
    /// Yangi vosita yangi ruxsat bilan qo'shilsa, u shu yerga yoziladi.
    /// </para>
    /// </remarks>
    public static readonly IReadOnlyList<string> FullReadPermissions =
    [
        Identity.Permissions.EmployeesView,
        Identity.Permissions.EmploymentView,
        Identity.Permissions.SalaryView,
        Identity.Permissions.SalaryExtraView,
        Identity.Permissions.ListsView,
        Identity.Permissions.IncentivesView,
        Identity.Permissions.FeedbackView,
        Identity.Permissions.SafetyView,
        Identity.Permissions.RecruitingView,
        Identity.Permissions.AttendanceView,
        Identity.Permissions.CatalogView,
    ];

    /// <summary>To'liq kalitni yaratish uchun yaratuvchida bo'lishi shart bo'lgan ruxsatlar.</summary>
    /// <remarks>
    /// <see cref="Identity.Permissions.McpConnect"/> qo'shimcha: ma'lumotni AI
    /// provayderiga chiqarish shu ruxsat bilan cheklangan (egasining qoidasi).
    /// </remarks>
    public static IEnumerable<string> RequiredToIssueFullRead =>
        FullReadPermissions.Append(Identity.Permissions.McpConnect);
}
