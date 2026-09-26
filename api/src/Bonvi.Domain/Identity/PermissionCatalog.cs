namespace Bonvi.Domain.Identity;

/// <summary>
/// Rollar muharririning xaritasi: har bir ruxsat qaysi bo'limda va qaysi
/// darajada turadi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega kodda, bazada emas.</b> Ruxsatlarning o'zi kod konstantasi
/// (<see cref="Permissions"/>) — ular bilan birga o'zgaradi. Xarita
/// bazada tursa, yangi ruxsat qo'shilganda uni migratsiya bilan alohida
/// «joylashtirish» kerak bo'lardi va bu qadam bir kun unutilardi.
/// </para>
/// <para>
/// <b>Uch joy, har bir ruxsat — AYNAN BITTASIDA:</b> bo'limning
/// darajalari (<see cref="PermissionModule.Levels"/>), bo'limning alohida
/// amallari (<see cref="PermissionModule.Actions"/>) yoki qo'shimchalar
/// (<see cref="Extras"/>). Buni test qadab turadi: ro'yxatga kiritilmagan
/// ruxsatni egasi muharrirda umuman ko'rmasdi va uni hech kimga bera
/// olmasdi.
/// </para>
/// <para>
/// Nomlar (sarlavhalar) bu yerda YO'Q: interfeys ularni kalit bo'yicha
/// to'rt tilda o'zi tarjima qiladi (<c>roles.module.&lt;key&gt;</c>,
/// <c>roles.level.&lt;level&gt;</c>, ...).
/// </para>
/// </remarks>
public static class PermissionCatalog
{
    /// <summary>«Ko'rish» darajasi.</summary>
    public const string View = "view";

    /// <summary>«O'zgartirish» darajasi.</summary>
    public const string Edit = "edit";

    /// <summary>«To'liq» darajasi.</summary>
    public const string Full = "full";

    /// <summary>Bo'limlar — muharrirdagi tartibda.</summary>
    public static IReadOnlyList<PermissionModule> Modules { get; } =
    [
        Module(
            "employees",
            view: [Permissions.EmployeesView],
            edit: [Permissions.EmployeesCreate, Permissions.EmployeesEdit],
            full: [Permissions.EmployeesDelete]),

        // Mehnat munosabati — yagona bo'lim, darajasidan tashqari AMALLARI
        // bor: egasi har bir buyruq turini alohida beradi (sabab
        // `Permissions.EmploymentActions` izohida).
        Module(
            "employment",
            view: [Permissions.EmploymentView],
            actions: Permissions.EmploymentActions),

        Module("lists", view: [Permissions.ListsView], edit: [Permissions.ListsManage]),
        Module("incentives", view: [Permissions.IncentivesView], edit: [Permissions.IncentivesManage]),
        Module("feedback", view: [Permissions.FeedbackView], edit: [Permissions.FeedbackManage]),
        Module("safety", view: [Permissions.SafetyView], edit: [Permissions.SafetyManage]),
        Module("recruiting", view: [Permissions.RecruitingView], edit: [Permissions.RecruitingManage]),
        Module("catalog", view: [Permissions.CatalogView], edit: [Permissions.CatalogManage]),

        // Hisobotda «o'zgartirish» yo'q: ko'rish yoki ko'rish + Excel eksporti.
        Module("reports", view: [Permissions.ReportsView], full: [Permissions.ReportsExport]),

        Module("users", full: [Permissions.UsersManage]),
        Module("roles", full: [Permissions.RolesManage]),
        Module("audit", view: [Permissions.AuditView]),
        Module("settings", full: [Permissions.SettingsManage]),
    ];

    /// <summary>
    /// Bironta bo'limga tegishli bo'lmagan ruxsatlar — muharrirda alohida kalit.
    /// </summary>
    /// <remarks>
    /// <see cref="Permissions.SalaryView"/> — oklad xodim kartasida ham,
    /// buyruqlarda ham, eksportda ham ko'rinadi, ya'ni u bitta bo'limning
    /// darajasi emas, ko'rinish bayrog'i.
    /// </remarks>
    public static IReadOnlyList<string> Extras { get; } = [Permissions.SalaryView];

    /// <summary>
    /// Bo'lim yig'uvchisi: har bir daraja faqat O'ZI QO'SHADIGAN kodlarni
    /// oladi, ro'yxat esa yig'ilib boradi.
    /// </summary>
    /// <remarks>
    /// Darajalar KUMULYATIV («to'liq» — «o'zgartirish» + o'chirish). Ular
    /// qo'lda to'liq sanab yozilsa, yangi kod quyi darajaga qo'shilib, yuqorisiga
    /// unutilishi mumkin edi — u holda «to'liq» darajali rol «ko'rish»
    /// darajalisidan kamroq ko'rardi.
    /// </remarks>
    private static PermissionModule Module(
        string key,
        IReadOnlyList<string>? view = null,
        IReadOnlyList<string>? edit = null,
        IReadOnlyList<string>? full = null,
        IReadOnlyList<string>? actions = null)
    {
        var levels = new List<PermissionLevel>(3);
        var accumulated = new List<string>();

        foreach (var (level, adds) in new[] { (View, view), (Edit, edit), (Full, full) })
        {
            if (adds is null)
            {
                continue;
            }

            accumulated.AddRange(adds);
            levels.Add(new PermissionLevel(level, [.. accumulated]));
        }

        return new PermissionModule(key, levels, actions ?? []);
    }
}

/// <summary>Muharrirdagi bitta bo'lim.</summary>
/// <param name="Key">Bo'lim kaliti — interfeys uni tarjima qiladi.</param>
/// <param name="Levels">
/// Bo'limda BOR darajalar, pastdan yuqoriga. Har birining ro'yxati
/// kumulyativ. «Ruxsat yo'q» darajasi ro'yxatda yo'q — u bo'limning
/// bironta kodi ham yo'qligi.
/// </param>
/// <param name="Actions">
/// Alohida beriladigan amallar. Faqat bo'limning «ko'rish» darajasi bilan
/// ma'noli: ko'ra olmagan narsasiga amal qilish mumkin emas.
/// </param>
public sealed record PermissionModule(
    string Key,
    IReadOnlyList<PermissionLevel> Levels,
    IReadOnlyList<string> Actions);

/// <summary>Bo'limning bitta darajasi.</summary>
/// <param name="Key"><see cref="PermissionCatalog.View"/>, <see cref="PermissionCatalog.Edit"/> yoki <see cref="PermissionCatalog.Full"/>.</param>
/// <param name="Permissions">Shu darajaga yetgan rol oladigan BARCHA kodlar.</param>
public sealed record PermissionLevel(string Key, IReadOnlyList<string> Permissions);
