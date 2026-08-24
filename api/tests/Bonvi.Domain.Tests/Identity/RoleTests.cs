using Bonvi.Domain.Identity;

namespace Bonvi.Domain.Tests.Identity;

/// <summary>Rol va ruxsatlar mantig'i.</summary>
public sealed class RoleTests
{
    [Fact]
    public void Rol_kodi_kichik_harfga_keltiriladi()
    {
        var rol = Role.Create("  HR_Manager  ", "HR menejeri").Value;

        rol.Code.ShouldBe("hr_manager");
        rol.Name.ShouldBe("HR menejeri");
    }

    [Theory]
    [InlineData(null, "rol.kod_bosh")]
    [InlineData("", "rol.kod_bosh")]
    public void Kodsiz_rol_yaratilmaydi(string? kod, string kutilgan)
    {
        var natija = Role.Create(kod, "Nom");

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe(kutilgan);
    }

    [Fact]
    public void Mavjud_ruxsat_beriladi()
    {
        var rol = Role.Create("hr_operator", "Operator").Value;

        var natija = rol.Grant(Permissions.EmployeesView);

        natija.IsSuccess.ShouldBeTrue();
        rol.Has(Permissions.EmployeesView).ShouldBeTrue();
    }

    [Fact]
    public void Notanish_ruxsat_rad_etiladi()
    {
        var rol = Role.Create("hr_operator", "Operator").Value;

        var natija = rol.Grant("xodimlar.hammasini_ochirish");

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("rol.notanish_ruxsat");
        rol.Permissions.ShouldBeEmpty();
    }

    [Fact]
    public void Bir_xil_ruxsat_ikki_marta_qoshilmaydi()
    {
        var rol = Role.Create("hr_operator", "Operator").Value;

        rol.Grant(Permissions.EmployeesView);
        rol.Grant(Permissions.EmployeesView);

        rol.Permissions.Count.ShouldBe(1);
    }

    [Fact]
    public void Administrator_rolidan_ruxsat_olib_tashlanmaydi()
    {
        // Bu himoya: administrator o'zini tasodifan huquqsiz qoldirmasligi kerak
        var rol = Role.Create(SystemRoles.Admin, "Administrator", isSystem: true).Value;
        rol.Grant(Permissions.UsersManage);

        var natija = rol.Revoke(Permissions.UsersManage);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("rol.admin_ozgartirilmaydi");
        rol.Has(Permissions.UsersManage).ShouldBeTrue();
    }

    [Fact]
    public void Oddiy_roldan_ruxsat_olib_tashlanadi()
    {
        var rol = Role.Create("hr_operator", "Operator").Value;
        rol.Grant(Permissions.EmployeesView);

        var natija = rol.Revoke(Permissions.EmployeesView);

        natija.IsSuccess.ShouldBeTrue();
        rol.Has(Permissions.EmployeesView).ShouldBeFalse();
    }

    // ────────── Standart rollar ──────────

    [Fact]
    public void Barcha_standart_rollarning_ruxsatlari_mavjud()
    {
        foreach (var (kod, (nom, ruxsatlar)) in SystemRoles.Defaults)
        {
            nom.ShouldNotBeNullOrWhiteSpace($"«{kod}» roli nomsiz");

            foreach (var ruxsat in ruxsatlar)
            {
                Permissions.Exists(ruxsat)
                    .ShouldBeTrue($"«{kod}» rolida notanish ruxsat: «{ruxsat}»");
            }
        }
    }

    [Fact]
    public void Administrator_barcha_ruxsatlarga_ega()
    {
        var (_, ruxsatlar) = SystemRoles.Defaults[SystemRoles.Admin];

        ruxsatlar.Length.ShouldBe(Permissions.All.Count);
    }

    [Fact]
    public void Faqat_ikkita_rol_mavjud()
    {
        // Xodimlar tizimga kirmaydi — undan faqat HR menejerlari (3 kishi)
        // va asosiy rahbar foydalanadi. Ishlatilmaydigan rol yaratilmaydi.
        SystemRoles.Defaults.Count.ShouldBe(2);
        SystemRoles.Defaults.Keys.ShouldBe(
            [SystemRoles.Admin, SystemRoles.HrManager],
            ignoreOrder: true);
    }

    [Fact]
    public void HR_menejeri_foydalanuvchilarni_boshqara_olmaydi()
    {
        // Yangi hisob ochish va rol berish — faqat asosiy rahbarning ishi
        var (_, ruxsatlar) = SystemRoles.Defaults[SystemRoles.HrManager];

        ruxsatlar.ShouldNotContain(Permissions.UsersManage);
        ruxsatlar.ShouldContain(Permissions.SalaryView);
    }
}
