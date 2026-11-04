namespace Bonvi.Domain.Identity;

/// <summary>
/// Tizim bo'limlarining (modullarining) kalitlari — rollar muharriri va audit jurnali uchun BITTA ro'yxat.
/// </summary>
/// <remarks>
/// <para>
/// Ikki iste'molchi bor: <see cref="PermissionCatalog"/> (rolga qaysi bo'lim
/// ochiq) va audit klassifikatsiyasi (<c>AuditCatalog</c> — o'zgarish qaysi
/// bo'limga tegishli, ya'ni jurnalda uni kim ko'radi). Kalit ikki joyda
/// qo'lda yozilsa, bittasida harf xatosi bo'lib, audit yozuvi hech kimga
/// ko'rinmaydigan bo'limga tushib qolardi — va buni hech qanday xato
/// ko'rsatmasdi.
/// </para>
/// <para>
/// Qiymatlar o'zgarmaydi: ular interfeysdagi tarjima kaliti
/// (<c>roles.module.&lt;key&gt;</c>) va keyin audit so'rovining filtri.
/// </para>
/// </remarks>
public static class AccessModuleKeys
{
    public const string Employees = "employees";
    public const string Employment = "employment";
    public const string Lists = "lists";
    public const string Incentives = "incentives";
    public const string Feedback = "feedback";
    public const string Safety = "safety";
    public const string Recruiting = "recruiting";
    public const string Blanks = "blanks";
    public const string Attendance = "attendance";
    public const string Catalog = "catalog";
    public const string Reports = "reports";
    public const string Users = "users";
    public const string Roles = "roles";
    public const string Audit = "audit";
    public const string Settings = "settings";

    /// <summary>Hamma kalitlar — muharrirdagi tartibda.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Employees, Employment, Lists, Incentives, Feedback, Safety, Recruiting, Blanks,
        Attendance, Catalog, Reports, Users, Roles, Audit, Settings,
    ];
}
