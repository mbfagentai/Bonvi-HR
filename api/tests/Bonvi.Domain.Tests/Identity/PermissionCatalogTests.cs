using Bonvi.Domain.Identity;

namespace Bonvi.Domain.Tests.Identity;

/// <summary>
/// Rollar muharririning xaritasi — <see cref="PermissionCatalog"/>.
/// </summary>
/// <remarks>
/// Asosiy qo'riq: har bir ruxsat xaritada AYNAN BIR JOYDA. Kodga yangi
/// ruxsat qo'shib, uni xaritaga joylashtirishni unutgan odam shu yerda
/// to'xtaydi — aks holda egasi uni muharrirda ko'rmasdi va hech kimga
/// bera olmasdi.
/// </remarks>
public sealed class PermissionCatalogTests
{
    [Fact]
    public void Har_bir_ruxsat_xaritada_aynan_bir_marta_uchraydi()
    {
        // Bo'limning darajalari kumulyativ (yuqorisi quyisini o'z ichiga
        // oladi) — shu sababli bo'lim ichida kod BIR joylashuv sanaladi.
        var joylashuvlar = PermissionCatalog.Modules
            .SelectMany(module => module.Levels
                .SelectMany(level => level.Permissions)
                .Concat(module.Actions)
                .Distinct(StringComparer.Ordinal))
            .Concat(PermissionCatalog.Extras)
            .ToList();

        var takrorlar = joylashuvlar
            .GroupBy(code => code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        takrorlar.ShouldBeEmpty("bir kod ikki joyda — muharrir uni ikki marta ko'rsatardi");

        joylashuvlar.ShouldBe(Permissions.All, ignoreOrder: true);
    }

    [Fact]
    public void Xaritada_notanish_kod_yoq()
    {
        foreach (var module in PermissionCatalog.Modules)
        {
            foreach (var code in module.Levels.SelectMany(level => level.Permissions).Concat(module.Actions))
            {
                Permissions.Exists(code).ShouldBeTrue($"«{module.Key}» bo'limida notanish kod: «{code}»");
            }
        }
    }

    /// <summary>
    /// Daraja kalitlari faqat <c>view</c> | <c>edit</c> | <c>full</c>, pastdan
    /// yuqoriga, va har biri quyisini TO'LIQ o'z ichiga oladi.
    /// </summary>
    [Fact]
    public void Darajalar_tartibli_va_kumulyativ()
    {
        string[] tartib = [PermissionCatalog.View, PermissionCatalog.Edit, PermissionCatalog.Full];

        foreach (var module in PermissionCatalog.Modules)
        {
            module.Levels.ShouldNotBeEmpty(module.Key);

            var kalitlar = module.Levels.Select(level => level.Key).ToList();
            kalitlar.ShouldBe(tartib.Where(kalitlar.Contains).ToList(), $"«{module.Key}»: darajalar tartibi");

            for (var i = 1; i < module.Levels.Count; i++)
            {
                foreach (var code in module.Levels[i - 1].Permissions)
                {
                    module.Levels[i].Permissions.ShouldContain(
                        code,
                        $"«{module.Key}»: «{module.Levels[i].Key}» darajasi «{code}» ni yo'qotgan");
                }
            }
        }
    }

    [Fact]
    public void Bolim_kalitlari_takrorlanmaydi()
        => PermissionCatalog.Modules.Select(module => module.Key).ShouldBeUnique();

    /// <summary>
    /// Shartnomadagi ko'rinish (frontend xuddi shunga qurilgan) — bir
    /// nechta bo'lim bo'yicha AYNAN tekshiriladi.
    /// </summary>
    [Fact]
    public void Shartnomadagi_bolimlar_aynan_mos()
    {
        var xodimlar = Bolim("employees");
        Daraja(xodimlar, PermissionCatalog.View).ShouldBe([Permissions.EmployeesView]);
        Daraja(xodimlar, PermissionCatalog.Edit).ShouldBe(
            [Permissions.EmployeesView, Permissions.EmployeesCreate, Permissions.EmployeesEdit]);
        Daraja(xodimlar, PermissionCatalog.Full).ShouldBe(
            [Permissions.EmployeesView, Permissions.EmployeesCreate, Permissions.EmployeesEdit, Permissions.EmployeesDelete]);
        xodimlar.Actions.ShouldBeEmpty();

        var mehnat = Bolim("employment");
        mehnat.Levels.Select(level => level.Key).ShouldBe([PermissionCatalog.View]);
        Daraja(mehnat, PermissionCatalog.View).ShouldBe([Permissions.EmploymentView]);
        mehnat.Actions.ShouldBe(
        [
            "employment.hire", "employment.transfer", "employment.duty", "employment.salary",
            "employment.terminate", "employment.probation.extend", "employment.probation.complete",
            "employment.correct",
        ]);

        var hisobotlar = Bolim("reports");
        hisobotlar.Levels.Select(level => level.Key).ShouldBe([PermissionCatalog.View, PermissionCatalog.Full]);
        Daraja(hisobotlar, PermissionCatalog.Full).ShouldBe([Permissions.ReportsView, Permissions.ReportsExport]);

        Bolim("users").Levels.Select(level => level.Key).ShouldBe([PermissionCatalog.Full]);
        Bolim("roles").Levels.Single().Permissions.ShouldBe([Permissions.RolesManage]);
        Bolim("audit").Levels.Single().Key.ShouldBe(PermissionCatalog.View);
        Bolim("settings").Levels.Single().Permissions.ShouldBe([Permissions.SettingsManage]);

        // Davomat (SPEC-rollar-va-audit §14.2): ko'rish → so'rovlar bo'yicha qaror → qurilma, jadval, moslik.
        var davomat = Bolim("attendance");
        davomat.Levels.Select(level => level.Key).ShouldBe([PermissionCatalog.View, PermissionCatalog.Edit, PermissionCatalog.Full]);
        Daraja(davomat, PermissionCatalog.View).ShouldBe([Permissions.AttendanceView]);
        Daraja(davomat, PermissionCatalog.Edit).ShouldBe([Permissions.AttendanceView, Permissions.AttendanceRequestsManage]);
        Daraja(davomat, PermissionCatalog.Full).ShouldBe(
            [Permissions.AttendanceView, Permissions.AttendanceRequestsManage, Permissions.AttendanceManage]);
        davomat.Actions.ShouldBeEmpty();

        // Blankalar (SPEC-blankalar §6.3): ko'rish → to'ldirish → boshqarish; imzolash — alohida amal.
        var blankalar = Bolim("blanks");
        blankalar.Levels.Select(level => level.Key).ShouldBe([PermissionCatalog.View, PermissionCatalog.Edit, PermissionCatalog.Full]);
        Daraja(blankalar, PermissionCatalog.View).ShouldBe([Permissions.BlanksView]);
        Daraja(blankalar, PermissionCatalog.Edit).ShouldBe([Permissions.BlanksView, Permissions.BlanksFill]);
        Daraja(blankalar, PermissionCatalog.Full).ShouldBe(
            [Permissions.BlanksView, Permissions.BlanksFill, Permissions.BlanksManage]);
        blankalar.Actions.ShouldBe([Permissions.BlanksApprove]);

        PermissionCatalog.Modules.Select(module => module.Key).ShouldBe(
        [
            "employees", "employment", "lists", "incentives", "feedback", "safety",
            "recruiting", "blanks", "attendance", "catalog", "reports", "users", "roles", "audit", "settings",
        ]);

        PermissionCatalog.Extras.ShouldBe(
            [Permissions.SalaryView, Permissions.SalaryExtraView, Permissions.McpConnect, Permissions.EmployeesPersonalView, Permissions.AttendanceScoped]);
    }

    private static PermissionModule Bolim(string key)
        => PermissionCatalog.Modules.Single(module => module.Key == key);

    private static IReadOnlyList<string> Daraja(PermissionModule module, string level)
        => module.Levels.Single(item => item.Key == level).Permissions;
}
