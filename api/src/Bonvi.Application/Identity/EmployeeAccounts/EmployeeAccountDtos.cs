namespace Bonvi.Application.Identity.EmployeeAccounts;

/// <summary>
/// Ilova hisobining HR ko'radigan ko'rinishi.
/// </summary>
/// <remarks>
/// ⚠️ Parol ham, uning xeshi ham bu yerda YO'Q: ochiq parol faqat alohida
/// endpointdan (<see cref="EmployeePasswordDto"/>) va faqat HR ko'z
/// tugmasini bosganda keladi — keshga, jurnalga va ro'yxat javobiga
/// tushmasligi uchun.
/// </remarks>
/// <param name="Id">Hisob identifikatori.</param>
/// <param name="PasswordSetAt">Joriy parol qachon berilgan.</param>
/// <param name="PasswordAvailable">
/// Parolni ko'rsatish mumkinmi. <c>false</c> — shifrlangan nusxa yo'q yoki
/// kalit almashgan; ekran «Yangi parol yasash» ni taklif qiladi.
/// </param>
/// <param name="LastLoginAt"><c>null</c> — hech qachon kirmagan.</param>
public sealed record EmployeeAccountDto(
    Guid Id,
    DateTimeOffset PasswordSetAt,
    bool PasswordAvailable,
    DateTimeOffset? LastLoginAt);

/// <summary>Kirish loginining holati — profil telefoni bo'yicha.</summary>
public enum EmployeeLoginIssue
{
    /// <summary>Raqam bor va faqat shu xodimda — kirish ishlaydi.</summary>
    None = 0,

    /// <summary>Profilda asosiy telefon yo'q — kirib bo'lmaydi.</summary>
    NoPhone = 1,

    /// <summary>Raqam boshqa o'chirilmagan xodimda ham bor — kirib bo'lmaydi.</summary>
    SharedPhone = 2,
}

/// <summary>Shu telefon raqami yozilgan BOSHQA xodim.</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="FullName">To'liq ism-familiya.</param>
public sealed record EmployeePhoneHolderDto(Guid EmployeeId, string FullName);

/// <summary>Xodim kartasidagi «Ilova» bo'limining holati.</summary>
/// <remarks>
/// <para>
/// ⭐ Hisob HAR DOIM bor: yo'q bo'lsa, shu javob yig'ilayotganda ochiladi
/// (fon vazifasini kutmasdan).
/// </para>
/// <para>
/// <see cref="Login"/> — profildagi ASOSIY telefon, alohida saqlanmaydi.
/// U bo'sh yoki takror bo'lsa kirish rad etiladi, sababi esa
/// <see cref="LoginIssue"/> va <see cref="SharedWith"/> da — HR profilni
/// tuzatishi bilan kirish o'zi ishlab ketadi.
/// </para>
/// </remarks>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="MobileCheckInAllowed">«Telefondan belgilash» o'chirgichi.</param>
/// <param name="Login">Kirish raqami (+998…); profilda raqam bo'lmasa <c>null</c>.</param>
/// <param name="LoginIssue">Nega bu raqam bilan kirib bo'lmaydi; <c>None</c> — kirsa bo'ladi.</param>
/// <param name="SharedWith">Shu raqam yozilgan boshqa o'chirilmagan xodimlar.</param>
/// <param name="Account">Ilova hisobi.</param>
public sealed record EmployeeAppAccessDto(
    Guid EmployeeId,
    bool MobileCheckInAllowed,
    string? Login,
    EmployeeLoginIssue LoginIssue,
    IReadOnlyList<EmployeePhoneHolderDto> SharedWith,
    EmployeeAccountDto Account);

/// <summary>Ochiq parol — faqat alohida so'rov bilan va faqat <c>attendance.manage</c> ga.</summary>
/// <param name="Password">Parol.</param>
public sealed record EmployeePasswordDto(string Password);
