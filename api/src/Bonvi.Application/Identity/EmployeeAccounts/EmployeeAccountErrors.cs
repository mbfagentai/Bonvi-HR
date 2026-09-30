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
    /// ⭐ Bu kod bir nechta boshqa-boshqa holatni qoplaydi: bunday raqamli
    /// xodim yo'q, raqam BIR NECHTA xodimda bor (profilda takror), parol
    /// noto'g'ri, xodim yumshoq o'chirilgan, xodim ishdan ketgan (INV-2) va
    /// telefon raqami umuman noto'g'ri formatda. Ularni ajratish kirish
    /// endpointini «bu raqam Bonvi xodimimi?» degan savolga javob beradigan
    /// ochiq API ga aylantirardi — 950 ta telefon raqamli korxonada bu
    /// jiddiy sizib chiqish.
    /// </para>
    /// <para>
    /// Haqiqiy sabab LOGGA yoziladi yoki HR ning «Ilova» bo'limida
    /// ko'rinadi (raqam yo'q / takror), javobga emas.
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

    /// <summary>Hisob endi ishlamaydi — xodim ishdan ketgan yoki o'chirilgan.</summary>
    /// <remarks>
    /// <para>
    /// Faqat ALLAQACHON kirgan odam ko'radi: <c>/api/me/*</c> ning har bir
    /// so'rovidagi to'siq (<c>EmployeeAccessGate</c>) — ishdan bo'shatish
    /// access token muddati tugashini kutmasdan, SHU ZAHOTI kuchga kiradi.
    /// Kirishning o'zida esa bu holat <see cref="InvalidCredentials"/>
    /// bilan qaytadi (oshkor qilmaslik).
    /// </para>
    /// <para>
    /// Qo'lda «hisobni yopish» YO'Q (egasining qarori, 2026-09-27): yagona
    /// o'chirgich — ishdan ketish.
    /// </para>
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

    // ── HR tomoni: parol ────────────────────────────────────────────

    /// <summary>HR parolni bo'sh qoldirdi.</summary>
    public static Error PasswordEmpty { get; } = Error.Validation(
        "xodim.parol_bosh",
        "Parol kiritilmagan.");

    /// <summary>HR yozgan parol juda qisqa.</summary>
    public static Error PasswordTooShort { get; } = Error.Validation(
        "xodim.parol_qisqa",
        $"Parol kamida {EmployeePassword.MinLength} belgidan iborat bo'lishi kerak.")
        .WithDetails(new Dictionary<string, object> { ["min"] = EmployeePassword.MinLength });

    /// <summary>HR yozgan parol juda uzun.</summary>
    public static Error PasswordTooLong { get; } = Error.Validation(
        "xodim.parol_uzun",
        $"Parol {EmployeePassword.MaxLength} belgidan oshmasligi kerak.")
        .WithDetails(new Dictionary<string, object> { ["max"] = EmployeePassword.MaxLength });

    /// <summary>Saqlangan parolni ko'rsatib bo'lmaydi.</summary>
    /// <remarks>
    /// Ikki holat: shifrlangan nusxa umuman yo'q (bu qaror oldidan berilgan
    /// parol) yoki shifrlash kaliti almashgan (<c>JWT__SIGNINGKEY</c>).
    /// Ikkalasida ham yagona chiqish — yangi parol yasash; xodimning hozirgi
    /// paroli esa ishlashda davom etadi.
    /// </remarks>
    public static Error PasswordUnavailable { get; } = Error.Conflict(
        "xodim.parol_korinmaydi",
        "Parolni ko'rsatib bo'lmaydi. Yangi parol yasang va xodimga ayting.");
}
