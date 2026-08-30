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

    [Theory]
    [InlineData(SystemRoles.SuperAdmin)]
    [InlineData(SystemRoles.MainBoss)]
    public void Rahbariyat_rolidan_ruxsat_olib_tashlanmaydi(string kod)
    {
        // Bu himoya: rahbariyat o'zini tasodifan huquqsiz qoldirmasligi kerak.
        // Bosh rahbar ham shu ro'yxatda — u ruxsatlar bo'yicha super
        // administrator bilan TENG.
        var rol = Role.Create(kod, "Rahbar", isSystem: true).Value;
        rol.Grant(Permissions.UsersManage);

        var natija = rol.Revoke(Permissions.UsersManage);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("rol.admin_ozgartirilmaydi");
        rol.Has(Permissions.UsersManage).ShouldBeTrue();
    }

    /// <summary>
    /// Ko'chirish yo'li: <c>Id</c> o'zgarmaydi, ya'ni foydalanuvchilardagi
    /// <c>role_ids</c> massiviga tegilmaydi.
    /// </summary>
    [Fact]
    public void Rol_kodi_qayta_nomlanadi_identifikator_esa_saqlanadi()
    {
        var rol = Role.Create(SystemRoles.LegacyAdmin, "Asosiy rahbar / Administrator", isSystem: true).Value;
        var eskiId = rol.Id;
        rol.Grant(Permissions.UsersManage);

        var natija = rol.Recode(SystemRoles.SuperAdmin, "Super administrator");

        natija.IsSuccess.ShouldBeTrue();
        rol.Code.ShouldBe(SystemRoles.SuperAdmin);
        rol.Name.ShouldBe("Super administrator");
        rol.Id.ShouldBe(eskiId);
        rol.Has(Permissions.UsersManage).ShouldBeTrue("ruxsatlar ham joyida qolishi kerak");
    }

    [Fact]
    public void Bosh_kod_bilan_qayta_nomlash_rad_etiladi()
    {
        var rol = Role.Create(SystemRoles.LegacyAdmin, "Administrator", isSystem: true).Value;

        var natija = rol.Recode("   ", "Super administrator");

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("rol.kod_bosh");
        rol.Code.ShouldBe(SystemRoles.LegacyAdmin);
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

    /// <summary>
    /// Super administratorda BARCHA ruxsat bor.
    /// </summary>
    [Fact]
    public void Super_administrator_barcha_ruxsatlarga_ega()
    {
        var (_, ruxsatlar) = SystemRoles.Defaults[SystemRoles.SuperAdmin];

        ruxsatlar.Length.ShouldBe(Permissions.All.Count);
    }

    /// <summary>
    /// Bosh rahbarda <c>settings.manage</c> dan TASHQARI hammasi bor.
    /// </summary>
    /// <remarks>
    /// Ilgari ikkala rahbariyat roli ham `Permissions.All` ni olardi
    /// («ruxsatlar bo'yicha TENG» — foydalanuvchining o'sha paytdagi
    /// talabi). Endi yagona istisno bor: tizim sozlamalari faqat super
    /// administratorda (yangi qaror, sabab
    /// <see cref="Permissions.SettingsManage"/> izohida).
    ///
    /// Test aynan shu YAGONALIKNI qadab turadi: ikkinchi istisno
    /// qo'shilsa u yiqiladi va qo'shuvchi odam «rahbariyat nimani
    /// ko'ra olmaydi?» degan savolga javob berishga majbur bo'ladi.
    /// </remarks>
    [Fact]
    public void Bosh_rahbarda_faqat_tizim_sozlamalari_yoq()
    {
        var (_, ruxsatlar) = SystemRoles.Defaults[SystemRoles.MainBoss];

        ruxsatlar.ShouldNotContain(Permissions.SettingsManage);
        ruxsatlar.Length.ShouldBe(Permissions.All.Count - 1);
    }

    /// <summary>
    /// Uchta ODAM roli va bitta XIZMAT roli — boshqasi yo'q.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Xodimlar tizimga kirmaydi: undan faqat HR menejerlari (bir necha
    /// kishi), bosh rahbar va super administrator foydalanadi.
    /// Ishlatilmaydigan rol yaratilmaydi.
    /// </para>
    /// <para>
    /// <see cref="SystemRoles.RecruitingBot"/> — istisno emas, BOSHQA
    /// TOIFA: uning ortida odam turmaydi, Telegram anketa boti turadi.
    /// Unga alohida rol aynan shuning uchun kerak — bot HR menejeri
    /// huquqlari bilan ishlasa, tokeni sizib ketganda xodimlar bazasi
    /// ochilib qolardi.
    /// </para>
    /// </remarks>
    [Fact]
    public void Uchta_odam_va_bitta_xizmat_roli_mavjud()
    {
        SystemRoles.Defaults.Keys.ShouldBe(
            [
                SystemRoles.SuperAdmin,
                SystemRoles.MainBoss,
                SystemRoles.HrManager,
                SystemRoles.RecruitingBot,
            ],
            ignoreOrder: true);
    }

    /// <summary>
    /// Bot roli ATIGI ikkita ruxsatga ega.
    /// </summary>
    /// <remarks>
    /// Bu test qasddan qat'iy: rolga yangi ruxsat qo'shilsa u YIQILADI va
    /// qo'shuvchi odam «bu botga haqiqatan kerakmi?» degan savolga javob
    /// berishga majbur bo'ladi. Xizmat hisobining huquqlari sezilmay
    /// o'sishi — sirning qiymatini oshiradigan eng keng tarqalgan yo'l.
    /// </remarks>
    [Fact]
    public void Bot_roli_faqat_rekrutingga_yetadi()
    {
        var (_, ruxsatlar) = SystemRoles.Defaults[SystemRoles.RecruitingBot];

        ruxsatlar.ShouldBe(
            [Permissions.RecruitingView, Permissions.RecruitingManage],
            ignoreOrder: true);
    }

    /// <summary>
    /// Eski <c>admin</c> kodi standart rollar ro'yxatida BO'LMASLIGI kerak:
    /// u faqat ko'chirish uchun qolgan va yangi bazada hech qachon
    /// yaratilmaydi.
    /// </summary>
    [Fact]
    public void Eski_admin_kodi_standart_rollar_orasida_yoq()
        => SystemRoles.Defaults.Keys.ShouldNotContain(SystemRoles.LegacyAdmin);

    /// <summary>
    /// «Atigi bitta» qoidasi qaysi rollarga tegishli ekani KODDA qayd
    /// etilgan — interfeys va use-case shu ro'yxatga tayanadi.
    /// </summary>
    [Fact]
    public void Atigi_bitta_qoidasi_rahbariyat_rollariga_tegishli()
    {
        SystemRoles.SingleHolder.ShouldBe(
            [SystemRoles.SuperAdmin, SystemRoles.MainBoss],
            ignoreOrder: true);

        SystemRoles.SingleHolder.ShouldNotContain(SystemRoles.HrManager);
    }

    [Fact]
    public void HR_menejeri_foydalanuvchilarni_boshqara_olmaydi()
    {
        // Yangi hisob ochish va rol berish — faqat rahbariyatning ishi
        var (_, ruxsatlar) = SystemRoles.Defaults[SystemRoles.HrManager];

        ruxsatlar.ShouldNotContain(Permissions.UsersManage);
        ruxsatlar.ShouldContain(Permissions.SalaryView);
    }

    /// <summary>
    /// Bosh rahbarda ham <c>users.manage</c> BOR — u foydalanuvchi
    /// boshqaruvi ekraniga kiradi, faqat kimga tegishi cheklangan.
    /// </summary>
    [Fact]
    public void Bosh_rahbar_foydalanuvchilarni_boshqara_oladi()
    {
        var (_, ruxsatlar) = SystemRoles.Defaults[SystemRoles.MainBoss];

        ruxsatlar.ShouldContain(Permissions.UsersManage);
    }
}
