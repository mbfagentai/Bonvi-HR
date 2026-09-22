namespace Bonvi.Application.Identity.EmployeeAccounts;

/// <summary>
/// Ilova hisobining HR ko'radigan ko'rinishi.
/// </summary>
/// <remarks>
/// ⚠️ Parol ham, uning xeshi ham bu yerda YO'Q va hech qachon bo'lmaydi:
/// bir martalik parol faqat <see cref="EmployeeAccountCreatedDto"/> da,
/// faqat bir marta qaytadi (qurilma qabul tokeni bilan bir xil marosim).
/// </remarks>
/// <param name="Id">Hisob identifikatori.</param>
/// <param name="EmployeeId">Kimga tegishli.</param>
/// <param name="Login">Kirish uchun telefon raqami (+998…).</param>
/// <param name="MustChangePassword">Bir martalik parol hali almashtirilmaganmi.</param>
/// <param name="PasswordSetAt">Joriy parol qachon yozilgan.</param>
/// <param name="IsActive">HR o'chirgichi.</param>
/// <param name="LastLoginAt"><c>null</c> — hech qachon kirmagan.</param>
/// <param name="LockedUntil">Vaqtincha blok tugash lahzasi; <c>null</c> — blok yo'q.</param>
public sealed record EmployeeAccountDto(
    Guid Id,
    Guid EmployeeId,
    string Login,
    bool MustChangePassword,
    DateTimeOffset PasswordSetAt,
    bool IsActive,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset? LockedUntil);

/// <summary>Yangi berilgan (yoki tiklangan) hisob va uning BIR MARTALIK paroli.</summary>
/// <remarks>
/// ⭐ <see cref="Password"/> shu javobdan boshqa hech qayerda ko'rinmaydi va
/// uni qaytarib beradigan endpoint YO'Q. Yo'qolsa — yagona yo'l qaytadan
/// tiklash. Aynan shu marosim HR uchun qurilma tokenidan tanish.
/// </remarks>
/// <param name="Account">Hisob.</param>
/// <param name="Password">Ochiq bir martalik parol — faqat shu javobda.</param>
public sealed record EmployeeAccountCreatedDto(EmployeeAccountDto Account, string Password);

/// <summary>Xodim kartasidagi «Ilova» bo'limining holati.</summary>
/// <remarks>
/// Ikkita mustaqil holat bitta javobda, chunki ekranda ular yonma-yon
/// turadi va ikkalasi ham bir-birini almashtirmaydi: hisobi bor odamda
/// belgilash o'chirilgan bo'lishi ham, hisobi yo'q odamga ruxsat oldindan
/// berilgan bo'lishi ham mumkin (sababi <c>Employee.MobileCheckInAllowed</c>
/// izohida).
/// </remarks>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="MobileCheckInAllowed">«Telefondan belgilash» o'chirgichi.</param>
/// <param name="Account">Ilova hisobi; yo'q bo'lsa <c>null</c>.</param>
public sealed record EmployeeAppAccessDto(
    Guid EmployeeId,
    bool MobileCheckInAllowed,
    EmployeeAccountDto? Account);
