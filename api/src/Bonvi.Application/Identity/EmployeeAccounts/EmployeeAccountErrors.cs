using Bonvi.Domain.Common;

namespace Bonvi.Application.Identity.EmployeeAccounts;

/// <summary>
/// Xodim ilovasi hisoblarining xatolari — matn bitta joyda.
/// </summary>
/// <remarks>
/// ⚠️ Har bir kod TO'RT tilda tarjima qilinishi shart, aks holda
/// <c>./bonvi check</c> yiqiladi va foydalanuvchi ekranda xom kodni o'qiydi.
/// </remarks>
public static class EmployeeAccountErrors
{
    // ── Kirish ──────────────────────────────────────────────────────

    /// <summary>
    /// Kirish uchun YAGONA rad javobi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Bu kod beshta boshqa-boshqa holatni qoplaydi: bunday login yo'q,
    /// parol noto'g'ri, hisob yumshoq o'chirilgan xodimga tegishli, xodim
    /// ishdan ketgan (INV-2) va telefon raqami umuman noto'g'ri formatda.
    /// Ularni ajratish kirish endpointini «bu raqam Bonvi xodimimi?» degan
    /// savolga javob beradigan ochiq API ga aylantirardi — 950 ta telefon
    /// raqamli korxonada bu jiddiy sizib chiqish.
    /// </para>
    /// <para>
    /// Haqiqiy sabab LOGGA yoziladi, javobga emas.
    /// </para>
    /// </remarks>
    public static Error InvalidCredentials { get; } = Error.Validation(
        "xodim.notogri_malumot",
        "Telefon raqami yoki parol noto'g'ri.");

    /// <summary>Ketma-ket xatolardan keyin vaqtincha bloklangan.</summary>
    /// <param name="minutes">Necha daqiqa qolgani.</param>
    public static Error LockedOut(int minutes) => Error.Forbidden(
        "xodim.bloklangan",
        $"Ko'p marta noto'g'ri urinish. {minutes} daqiqadan keyin qayta urinib ko'ring.")
        .WithDetails(new Dictionary<string, object> { ["retryAfterMinutes"] = minutes });

    /// <summary>HR hisobni yopib qo'ygan.</summary>
    /// <remarks>
    /// <see cref="InvalidCredentials"/> dan ATAYLAB ajratilgan va bu oshkorlik
    /// EMAS: bu javobni faqat TO'G'RI parol kiritgan odam ko'radi, ya'ni
    /// hisobning egasi. Unga «parolingiz noto'g'ri» deyish uni parolni
    /// qayta-qayta terishga va oxirida o'zini bloklab qo'yishga olib borardi.
    /// </remarks>
    public static Error AccountInactive { get; } = Error.Forbidden(
        "xodim.hisob_faol_emas",
        "Hisobingiz faol emas. HR bo'limiga murojaat qiling.");

    /// <summary>Yangilash tokeni yaroqsiz, muddati tugagan yoki allaqachon ishlatilgan.</summary>
    /// <remarks>
    /// Uch holat bitta kodda: mijoz uchun ularning oqibati bir xil — qaytadan
    /// kirish kerak. Qayta ishlatilgan token esa alohida ish ham bajaradi:
    /// hisobning BARCHA tokenlari bekor qilinadi (o'g'irlik belgisi).
    /// </remarks>
    public static Error InvalidRefreshToken { get; } = Error.Forbidden(
        "xodim.token_yaroqsiz",
        "Sessiya muddati tugadi. Qaytadan kiring.");

    /// <summary>Joriy parol noto'g'ri.</summary>
    public static Error WrongPassword { get; } = Error.Validation(
        "xodim.parol_notogri",
        "Joriy parol noto'g'ri.");

    /// <summary>Bir martalik parol hali almashtirilmagan.</summary>
    /// <remarks>
    /// HR bergan parol o'qib yozib olinadigan satr — u almashtirilmaguncha
    /// hisob faqat parol o'zgartirish endpointiga ruxsat beradi. 403:
    /// autentifikatsiya joyida, faqat amal taqiqlangan.
    /// </remarks>
    public static Error PasswordChangeRequired { get; } = Error.Forbidden(
        "xodim.parol_almashtirilsin",
        "Avval bir martalik parolni almashtiring.");

    // ── HR tomoni ───────────────────────────────────────────────────

    /// <summary>Bunday hisob yo'q.</summary>
    public static Error NotFound { get; } = Error.NotFound(
        "xodim.hisob_topilmadi",
        "Bu xodimning ilova hisobi yo'q.");

    /// <summary>Xodimda hisob allaqachon bor.</summary>
    /// <remarks>
    /// Bitta xodimda bitta hisob (<c>ux_employee_accounts_employee</c>).
    /// Parolni unutgan odamga YANGI hisob emas, parol tiklash kerak — aks
    /// holda «qaysi hisob haqiqiy» degan savol tug'ilardi.
    /// </remarks>
    public static Error AlreadyExists { get; } = Error.Conflict(
        "xodim.hisob_bor",
        "Bu xodimda ilova hisobi allaqachon bor. Parolni tiklang.");

    /// <summary>Bu telefon raqami boshqa hisobga biriktirilgan.</summary>
    /// <remarks>
    /// ⚠️ Bu birinchi haftadayoq uchraydi: <c>employees.phone</c> UNIKAL EMAS
    /// va ko'chirilgan ma'lumotda takroriy raqamlar bor. Xabar aynan shuni
    /// aytadi — «to'qnashuv» demaydi — aks holda HR imkoniyatni buzuq deb
    /// o'ylardi.
    /// </remarks>
    public static Error LoginTaken { get; } = Error.Conflict(
        "xodim.login_band",
        "Bu telefon raqami boshqa xodimning ilova hisobiga biriktirilgan. " +
        "Xodim kartasidagi raqamni tekshiring yoki boshqa raqam kiriting.");
}
