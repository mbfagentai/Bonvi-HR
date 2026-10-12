using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Common;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Identity;

namespace Bonvi.Application.Audit;

/// <summary>
/// So'rovchining jurnaldagi ko'rinishi — ruxsatlaridan (SPEC-rollar-va-audit §7.10).
/// </summary>
/// <remarks>
/// <para>
/// <b>Qator ko'rinadi</b>, agar so'rovchida qatorning bo'limiga KIRISH kodi bo'lsa —
/// bo'limning eng quyi darajasi beradigan kodlar (<c>employees.view</c>,
/// <c>attendance.view</c>, <c>users.manage</c>…). Ro'yxat qo'lda yozilmaydi, u
/// <see cref="PermissionCatalog"/> dan o'qiladi: SPEC dan keyin qo'shilgan bo'lim
/// (davomat) va kelajakdagisi ham o'z-o'zidan to'g'ri ishlaydi. Qo'lda yozilgan ro'yxatda
/// yangi bo'lim unutilsa, uning tarixi hech kimga ko'rinmasdi va buni hech narsa
/// ko'rsatmasdi.
/// </para>
/// <para>
/// <c>audit.view</c> ning o'zi bu yerda tekshirilmaydi — u endpoint siyosatida.
/// </para>
/// </remarks>
public static class AuditVisibility
{
    /// <summary>So'rovchining ko'rinishi.</summary>
    public static AuditViewer ViewerOf(ICurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var modules = PermissionCatalog.Modules
            .Where(module => module.Levels.Count > 0 && module.Levels[0].Permissions.All(user.HasPermission))
            .Select(module => module.Key)
            .ToHashSet(StringComparer.Ordinal);

        return new AuditViewer(
            modules,
            PersonalData: user.CanSeePersonalData(),
            Salary: user.CanSeeSalary(),
            ExtraPay: user.CanSeeExtraPay(),
            Security: user.HasPermission(Permissions.UsersManage));
    }
}

/// <summary>Amallarning jurnaldagi guruhlari.</summary>
public static class AuditActionGroups
{
    /// <summary>
    /// Kirish va xavfsizlik hodisalari — «Kirish va xavfsizlik» ko'rinishi, faqat
    /// <c>users.manage</c> bilan.
    /// </summary>
    /// <remarks>
    /// Excel eksporti bu yerda YO'Q: u ma'lumot bilan qilingan amal va hisobotlar bo'limiga
    /// tegishli (SPEC §7.3) — «O'zgarishlar» ko'rinishida, <c>reports.view</c> bilan.
    /// </remarks>
    public static IReadOnlySet<AuditAction> Security { get; } = new HashSet<AuditAction>
    {
        AuditAction.Login,
        AuditAction.LoginFailed,
        AuditAction.Logout,
        AuditAction.TokenReuse,
        AuditAction.PermissionDenied,
    };

    /// <summary>Entity o'zgarishlari — yaratish, tahrir, o'chirish.</summary>
    public static IReadOnlySet<AuditAction> Changes { get; } = new HashSet<AuditAction>
    {
        AuditAction.Create,
        AuditAction.Update,
        AuditAction.Delete,
    };

    /// <summary>
    /// Ilova parolini ko'rish hodisasining obyekti — u ham <see cref="AuditAction.Export"/>, lekin
    /// Excel emas: davomat bo'limiga tegishli (parolni o'sha bo'lim egalari ko'radi).
    /// </summary>
    public const string PasswordViewEntity = nameof(EmployeeAccount);

    /// <summary>Qatorning bo'limi — entity turidan emas, amaldan aniqlanadigan holatlar.</summary>
    /// <param name="action">Amal.</param>
    /// <param name="entityName">Obyekt turi — parol ko'rishni Excel eksportidan ajratish uchun.</param>
    /// <returns><c>users</c>, <c>reports</c>, <c>attendance</c> yoki <c>null</c> (bo'lim entity turidan olinadi).</returns>
    public static string? ModuleOfEvent(AuditAction action, string? entityName)
        => action == AuditAction.Export
            ? entityName == PasswordViewEntity ? AccessModuleKeys.Attendance : AccessModuleKeys.Reports
            : Security.Contains(action) ? AccessModuleKeys.Users
            : null;
}
