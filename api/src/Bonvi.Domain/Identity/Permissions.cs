namespace Bonvi.Domain.Identity;

/// <summary>
/// Tizimdagi barcha ruxsatlar.
/// </summary>
/// <remarks>
/// <para>
/// Ruxsatlar alohida jadval emas, <b>konstanta</b>: ular kod bilan birga
/// o'zgaradi va ma'lumot sifatida tahrirlanmaydi. Rolga biriktirilganda
/// esa faqat kod (satr) saqlanadi.
/// </para>
/// <para>
/// Nega bunday: «yangi ruxsat qo'shildi» — bu kod o'zgarishi. Agar u
/// bazada tursa, migratsiya va kod bir-biridan uzoqlashib ketadi.
/// </para>
/// </remarks>
public static class Permissions
{
    // ---------- Xodimlar ----------
    public const string EmployeesView = "employees.view";
    public const string EmployeesCreate = "employees.create";
    public const string EmployeesEdit = "employees.edit";
    public const string EmployeesDelete = "employees.delete";

    // ---------- Mehnat munosabatlari va buyruqlar ----------
    public const string EmploymentView = "employment.view";
    public const string EmploymentManage = "employment.manage";

    /// <summary>
    /// Oylik ish haqini ko'rish. ALOHIDA ruxsat — HR so'ragan talab:
    /// Excel'da oklad hammaga ochiq edi, tizimda esa cheklanadi.
    /// </summary>
    public const string SalaryView = "salary.view";

    // ---------- Ro'yxatlar va teglar ----------
    public const string ListsView = "lists.view";
    public const string ListsManage = "lists.manage";

    /// <summary>
    /// Rag'bat va jarima. ALOHIDA ruxsat — pul (jarima/mukofot summasi) va
    /// intizomiy chora bilan bog'liq, oklad kabi nozik ma'lumot. Qobiliyatlar
    /// profili esa alohida ruxsat OLMAYDI: u xodim kartasining bir qismi va
    /// <see cref="EmployeesView"/>/<see cref="EmployeesEdit"/> orqali boshqariladi.
    /// </summary>
    public const string IncentivesView = "incentives.view";

    public const string IncentivesManage = "incentives.manage";

    // ---------- Ma'lumotnomalar ----------
    public const string CatalogView = "catalog.view";
    public const string CatalogManage = "catalog.manage";

    // ---------- Hisobotlar ----------
    public const string ReportsView = "reports.view";
    public const string ReportsExport = "reports.export";

    // ---------- Tizim ----------
    public const string UsersManage = "users.manage";
    public const string AuditView = "audit.view";

    /// <summary>Barcha ruxsatlar — tekshirish va seed uchun.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        EmployeesView, EmployeesCreate, EmployeesEdit, EmployeesDelete,
        EmploymentView, EmploymentManage, SalaryView,
        ListsView, ListsManage,
        IncentivesView, IncentivesManage,
        CatalogView, CatalogManage,
        ReportsView, ReportsExport,
        UsersManage, AuditView,
    };

    public static bool Exists(string permission) => All.Contains(permission);
}

/// <summary>
/// Standart rollar va ularning ruxsatlari.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tizimdan kim foydalanadi.</b> Xodimlar bu tizimga kirmaydi va ularga
/// login-parol berilmaydi. Tizim faqat <b>xodimlarni boshqarish</b> uchun:
/// undan HR bo'limi xodimlari va asosiy rahbar foydalanadi.
/// </para>
/// <para>
/// Shu sababli rollar atigi <b>ikkita</b>: asosiy rahbar va HR menejeri
/// (uni bir necha kishi oladi). Bo'lim boshliqlari, xodimlar yoki alohida
/// "operator" darajasi uchun rol ataylab yaratilmagan — kerak bo'lganda
/// qo'shiladi, lekin ishlatilmaydigan rolni oldindan yaratish faqat
/// chalkashlik beradi.
/// </para>
/// <para>
/// Bu ro'yxat birinchi ishga tushirishda bazaga yoziladi (seed).
/// Keyinchalik administrator rolga ruxsat qo'shishi/olib tashlashi mumkin —
/// bu yerdagi qiymatlar faqat boshlang'ich holat.
/// </para>
/// </remarks>
public static class SystemRoles
{
    /// <summary>Asosiy rahbar va tizim administratori. Barcha ruxsatlar.</summary>
    public const string Admin = "admin";

    /// <summary>
    /// HR menejeri. Butun kadrlar ishi, oylik ish haqi ham ko'rinadi.
    /// Bonvi'da bu rolni bir necha kishi oladi.
    /// </summary>
    public const string HrManager = "hr_manager";

    public static IReadOnlyDictionary<string, (string Name, string[] Permissions)> Defaults { get; } =
        new Dictionary<string, (string, string[])>(StringComparer.Ordinal)
        {
            [Admin] = ("Asosiy rahbar / Administrator", [.. Permissions.All]),

            [HrManager] = ("HR menejeri",
            [
                Permissions.EmployeesView, Permissions.EmployeesCreate,
                Permissions.EmployeesEdit, Permissions.EmployeesDelete,
                Permissions.EmploymentView, Permissions.EmploymentManage,
                Permissions.SalaryView,
                Permissions.ListsView, Permissions.ListsManage,
                Permissions.IncentivesView, Permissions.IncentivesManage,
                Permissions.CatalogView, Permissions.CatalogManage,
                Permissions.ReportsView, Permissions.ReportsExport,
                Permissions.AuditView,
            ]),
        };
}
