namespace Bonvi.Domain.Blanks;

/// <summary>
/// Imzo qadamining bajaruvchi rollari (SPEC-blankalar §6.1) — shablondagi <c>actor</c> qiymatlari.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lavozim rollari</b> (<see cref="OfficeRoles"/>) foydalanuvchilarga «Blankalar → Rollar» da
/// biriktiriladi (<see cref="BlankRoleAssignment"/>). <b>Shaxs rollari</b> esa har bir blankada
/// alohida aniqlanadi (xodimning o'zi, bo'lim boshlig'i, javobdagi xodim maydoni, guvoh) —
/// ularni biriktirib bo'lmaydi.
/// </para>
/// <para>
/// Qiymatlar shablon JSON'ida va <c>blank_submission_steps.actor_role</c> da yashaydi —
/// mavjudini o'zgartirish eski blankalarni egasiz qoldiradi.
/// </para>
/// </remarks>
public static class BlankActorRoles
{
    /// <summary><c>actor_role</c> va <c>role_code</c> ustunlarining uzunligi.</summary>
    public const int MaxLength = 32;

    public const string Hr1 = "hr1";
    public const string Hr2 = "hr2";
    public const string Hr3 = "hr3";
    public const string Hr4 = "hr4";
    public const string Hr5 = "hr5";
    public const string Hr8 = "hr8";
    public const string PayrollAccountant = "payrollAccountant";
    public const string ChiefAccountant = "chiefAccountant";
    public const string Director = "director";
    public const string Lawyer = "lawyer";

    /// <summary>Occupational safety engineer — safety briefing steps (docs/SPEC-mening-ishlarim.md §9.1).</summary>
    public const string SafetyEngineer = "safetyEngineer";

    /// <summary>Hiring council (members assigned in «Rollar») — the chair step of HR-F04 (docs/SPEC-mening-ishlarim.md §9.1).</summary>
    public const string Council = "council";

    /// <summary>Blankaning sub'yekti — xodimning o'zi.</summary>
    public const string SubjectEmployee = "subjectEmployee";

    /// <summary>Blanka bo'limining boshlig'i (<c>Department.HeadEmployeeId</c>).</summary>
    public const string DepartmentHead = "departmentHead";

    /// <summary>Guvoh — faqat qo'lda imzo, ismi yoziladi.</summary>
    public const string Witness = "witness";

    /// <summary>Nomzodning o'zi (2-bosqich).</summary>
    public const string Candidate = "candidate";

    /// <summary>Javobdagi xodim maydoniga ishora: <c>field:mentor</c>.</summary>
    public const string FieldPrefix = "field:";

    /// <summary>Foydalanuvchiga biriktiriladigan lavozim rollari — muharrirdagi tartibda.</summary>
    public static IReadOnlyList<string> OfficeRoles { get; } =
    [
        Hr1, Hr2, Hr3, Hr4, Hr5, Hr8, PayrollAccountant, ChiefAccountant, Director, Lawyer,
        SafetyEngineer, Council,
    ];

    /// <summary>Lavozim roli (biriktiriladigan) mi.</summary>
    public static bool IsOfficeRole(string? role)
        => role is not null && OfficeRoles.Contains(role, StringComparer.Ordinal);

    /// <summary>Qadam bajaruvchisi sifatida ma'noli qiymatmi (lavozim yoki shaxs roli).</summary>
    public static bool IsKnown(string? role)
        => IsOfficeRole(role)
           || role is SubjectEmployee or DepartmentHead or Witness or Candidate
           || (role is { Length: > 6 and <= MaxLength } && role.StartsWith(FieldPrefix, StringComparison.Ordinal));
}
