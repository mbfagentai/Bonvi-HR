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
    public void Super_administrator_rolidan_ruxsat_olib_tashlanmaydi()
    {
        // Bu himoya: tizim egasi o'zini tasodifan huquqsiz qoldirmasligi kerak.
        var rol = Role.Create(SystemRoles.SuperAdmin, "Super administrator", isSystem: true).Value;
        rol.Grant(Permissions.UsersManage);

        var natija = rol.Revoke(Permissions.UsersManage);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("rol.admin_ozgartirilmaydi");
        rol.Has(Permissions.UsersManage).ShouldBeTrue();
    }

    /// <summary>
    /// Bosh rahbar endi ODDIY rol: uning ruxsatlarini egasi rollar
    /// muharriridan o'zi belgilaydi (ilgari u ham qulflangan edi).
    /// </summary>
    [Fact]
    public void Bosh_rahbar_rolidan_ruxsat_olib_tashlanadi()
    {
        var rol = Role.Create(SystemRoles.MainBoss, "Bosh rahbar", isSystem: true).Value;
        rol.Grant(Permissions.UsersManage);

        var natija = rol.Revoke(Permissions.UsersManage);

        natija.IsSuccess.ShouldBeTrue();
        rol.Has(Permissions.UsersManage).ShouldBeFalse();
    }

    // ────────── Rollar muharriri uchun domen qoidalari ──────────

    [Fact]
    public void Ruxsatlar_royxati_toliq_almashtiriladi()
    {
        var rol = Role.Create("custom_a1b2c3d4", "Kadr bo'limi boshlig'i").Value;
        rol.Grant(Permissions.EmployeesView);

        var natija = rol.ReplacePermissions(
            [Permissions.EmploymentView, Permissions.EmploymentSalary, Permissions.EmploymentView]);

        natija.IsSuccess.ShouldBeTrue();
        rol.Permissions.ShouldBe([Permissions.EmploymentView, Permissions.EmploymentSalary], "takror olib tashlanadi");
    }

    /// <summary>Bitta notanish kod — butun ro'yxat rad etiladi, rol o'zgarmaydi.</summary>
    [Fact]
    public void Notanish_kod_bilan_almashtirish_hech_narsani_ozgartirmaydi()
    {
        var rol = Role.Create("custom_a1b2c3d4", "Kadr bo'limi boshlig'i").Value;
        rol.Grant(Permissions.EmployeesView);

        var natija = rol.ReplacePermissions([Permissions.EmploymentView, Permissions.LegacyEmploymentManage]);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("rol.notanish_ruxsat");
        rol.Permissions.ShouldBe([Permissions.EmployeesView]);
    }

    /// <summary>
    /// Super administratorning AMALDAGI ruxsati tushmaydi, koddan olib
    /// tashlangan eski kodi esa tushadi (aks holda u abadiy qolardi).
    /// </summary>
    [Fact]
    public void Super_administrator_royxati_faqat_eski_koddan_tozalanadi()
    {
        var rol = Role.Create(SystemRoles.SuperAdmin, "Super administrator", isSystem: true).Value;
        rol.Grant(Permissions.UsersManage);

        rol.ReplacePermissions([Permissions.EmployeesView]).Error.Code.ShouldBe("rol.admin_ozgartirilmaydi");
        rol.Has(Permissions.UsersManage).ShouldBeTrue();

        rol.ReplacePermissions(Permissions.All).IsSuccess.ShouldBeTrue();
        rol.Permissions.Count.ShouldBe(Permissions.All.Count);
    }

    [Theory]
    [InlineData(null, "rol.nom_bosh")]
    [InlineData("   ", "rol.nom_bosh")]
    public void Bosh_nom_rad_etiladi(string? nom, string kutilgan)
    {
        Role.ValidateName(nom).Error.Code.ShouldBe(kutilgan);
        Role.Create("custom_a1b2c3d4", nom).Error.Code.ShouldBe(kutilgan);
    }

    [Fact]
    public void Uzun_nom_rad_etiladi_chegaradagisi_esa_qabul_qilinadi()
    {
        var chegarada = new string('a', Role.MaxNameLength);
        var uzun = new string('a', Role.MaxNameLength + 1);

        Role.ValidateName("  " + chegarada + "  ").Value.ShouldBe(chegarada);
        Role.ValidateName(uzun).Error.Code.ShouldBe("rol.nom_uzun");

        var rol = Role.Create("custom_a1b2c3d4", "Operator").Value;
        rol.Rename(uzun).Error.Code.ShouldBe("rol.nom_uzun");
        rol.Name.ShouldBe("Operator");
    }

    [Theory]
    [InlineData(SystemRoles.SuperAdmin, true)]
    [InlineData(SystemRoles.RecruitingBot, true)]
    [InlineData(SystemRoles.MainBoss, false)]
    [InlineData(SystemRoles.HrManager, false)]
    [InlineData("custom_a1b2c3d4", false)]
    public void Faqat_super_administrator_va_bot_himoyalangan(string kod, bool himoyalangan)
        => Role.Create(kod, "Rol", isSystem: true).Value.IsProtected.ShouldBe(himoyalangan);

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
    /// Bosh rahbarda <c>settings.manage</c> va <c>roles.manage</c> dan
    /// TASHQARI hammasi bor.
    /// </summary>
    /// <remarks>
    /// Ilgari ikkala rahbariyat roli ham `Permissions.All` ni olardi
    /// («ruxsatlar bo'yicha TENG» — foydalanuvchining o'sha paytdagi
    /// talabi). Keyin tizim sozlamalari faqat super administratorga qoldi
    /// (sabab <see cref="Permissions.SettingsManage"/> izohida), 2026-09-26
    /// dan esa rollarni o'zgartirish ham (sabab
    /// <see cref="Permissions.RolesManage"/> izohida).
    ///
    /// Test aynan shu IKKITA istisnoni qadab turadi: uchinchisi
    /// qo'shilsa u yiqiladi va qo'shuvchi odam «rahbariyat nimani
    /// ko'ra olmaydi?» degan savolga javob berishga majbur bo'ladi.
    /// </remarks>
    [Fact]
    public void Bosh_rahbarda_faqat_tizim_sozlamalari_va_rollar_yoq()
    {
        var (_, ruxsatlar) = SystemRoles.Defaults[SystemRoles.MainBoss];

        ruxsatlar.ShouldNotContain(Permissions.SettingsManage);
        ruxsatlar.ShouldNotContain(Permissions.RolesManage);
        ruxsatlar.Length.ShouldBe(Permissions.All.Count - 2);
    }

    /// <summary>
    /// HR menejerining standart ruxsatlari: eski <c>employment.manage</c>
    /// o'rnida sakkizala mehnat amali — bugungi huquqi kamaymaydi.
    /// </summary>
    [Fact]
    public void HR_menejeri_barcha_mehnat_amallarini_oladi()
    {
        var (_, ruxsatlar) = SystemRoles.Defaults[SystemRoles.HrManager];

        foreach (var amal in Permissions.EmploymentActions)
        {
            ruxsatlar.ShouldContain(amal);
        }

        ruxsatlar.ShouldNotContain(Permissions.LegacyEmploymentManage);
        ruxsatlar.ShouldNotContain(Permissions.RolesManage);
    }

    /// <summary>
    /// Eski yagona mehnat ruxsati endi ruxsatlar ro'yxatida YO'Q — uni
    /// hech bir rolga qayta berib bo'lmaydi.
    /// </summary>
    [Fact]
    public void Eski_mehnat_ruxsati_ruxsatlar_orasida_yoq()
    {
        Permissions.All.ShouldNotContain(Permissions.LegacyEmploymentManage);
        Permissions.EmploymentActions.Count.ShouldBe(8);
        Permissions.EmploymentActions.ShouldBeUnique();

        foreach (var amal in Permissions.EmploymentActions)
        {
            Permissions.Exists(amal).ShouldBeTrue(amal);
        }
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
                SystemRoles.AttendanceResponsible,
            ],
            ignoreOrder: true);
    }

    /// <summary>
    /// «Davomat mas'uli» holds exactly this: it reads attendance, writes manual passes,
    /// exports, and carries the marker that restricts it. Nothing else in HR.
    /// </summary>
    [Fact]
    public void Attendance_responsible_holds_exactly_its_four_codes()
    {
        var (_, ruxsatlar) = SystemRoles.Defaults[SystemRoles.AttendanceResponsible];

        ruxsatlar.ShouldBe(
            [
                Permissions.AttendanceView,
                Permissions.AttendanceRequestsManage,
                Permissions.ReportsExport,
                Permissions.AttendanceScoped,
            ],
            ignoreOrder: true);

        ruxsatlar.ShouldNotContain(Permissions.AttendanceManage);
    }

    /// <summary>The marker means nothing next to <c>attendance.manage</c> and is dropped there only.</summary>
    [Fact]
    public void Attendance_marker_is_dropped_only_when_attendance_is_managed()
    {
        var manager = new HashSet<string> { Permissions.AttendanceManage, Permissions.AttendanceScoped };
        Permissions.DropAttendanceMarkerIfManaging(manager);
        manager.ShouldBe([Permissions.AttendanceManage]);

        var scoped = new HashSet<string> { Permissions.AttendanceView, Permissions.AttendanceScoped };
        Permissions.DropAttendanceMarkerIfManaging(scoped);
        scoped.ShouldContain(Permissions.AttendanceScoped);
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
