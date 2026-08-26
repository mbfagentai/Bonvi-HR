using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Tests.Identity;

/// <summary>
/// Foydalanuvchi entity'sining biznes qoidalari.
/// Asosiy e'tibor — kirishni bloklash mantig'iga (xavfsizlik).
/// </summary>
public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 9, 0, 0, TimeSpan.FromHours(5));

    private static User YaratilganFoydalanuvchi()
    {
        var login = PhoneNumber.Create("905088888").Value;
        var ism = FullName.Parse("Мамажонов Бобуржон Фазлитдин ўғли").Value;
        return User.Create(login, "xesh", ism).Value;
    }

    [Fact]
    public void Yangi_foydalanuvchi_faol_holatda_yaratiladi()
    {
        var user = YaratilganFoydalanuvchi();

        user.IsActive.ShouldBeTrue();
        user.FailedLoginAttempts.ShouldBe(0);
        user.LockedUntil.ShouldBeNull();
        user.LastLoginAt.ShouldBeNull();
    }

    [Fact]
    public void Parolsiz_foydalanuvchi_yaratilmaydi()
    {
        var login = PhoneNumber.Create("905088888").Value;
        var ism = FullName.Parse("Мамажонов Бобуржон").Value;

        var natija = User.Create(login, "   ", ism);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.parol_bosh");
    }

    // ────────── Bloklash mantig'i ──────────

    [Fact]
    public void Tort_marta_xato_urinishdan_keyin_bloklanmaydi()
    {
        var user = YaratilganFoydalanuvchi();

        for (var i = 0; i < 4; i++)
        {
            user.RecordFailedLogin(Now);
        }

        user.IsLockedAt(Now).ShouldBeFalse();
        user.FailedLoginAttempts.ShouldBe(4);
    }

    [Fact]
    public void Besh_marta_xato_urinishdan_keyin_15_daqiqaga_bloklanadi()
    {
        var user = YaratilganFoydalanuvchi();

        for (var i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(Now);
        }

        user.IsLockedAt(Now).ShouldBeTrue();
        user.LockedUntil.ShouldBe(Now.AddMinutes(15));

        // Hisoblagich nolga tushadi — keyingi blok yana 5 ta xatodan keyin
        user.FailedLoginAttempts.ShouldBe(0);
    }

    [Fact]
    public void Blok_muddati_tugagach_kirish_ochiladi()
    {
        var user = YaratilganFoydalanuvchi();
        for (var i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(Now);
        }

        user.IsLockedAt(Now.AddMinutes(14)).ShouldBeTrue();
        user.IsLockedAt(Now.AddMinutes(16)).ShouldBeFalse();
    }

    [Fact]
    public void Muvaffaqiyatli_kirish_blokni_va_hisoblagichni_tozalaydi()
    {
        var user = YaratilganFoydalanuvchi();
        for (var i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(Now);
        }

        user.RecordSuccessfulLogin(Now.AddMinutes(20));

        user.LockedUntil.ShouldBeNull();
        user.FailedLoginAttempts.ShouldBe(0);
        user.LastLoginAt.ShouldBe(Now.AddMinutes(20));
    }

    [Fact]
    public void Parol_almashtirilganda_blok_olib_tashlanadi()
    {
        var user = YaratilganFoydalanuvchi();
        for (var i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(Now);
        }

        user.ChangePassword("yangi_xesh");

        user.PasswordHash.ShouldBe("yangi_xesh");
        user.LockedUntil.ShouldBeNull();
        user.FailedLoginAttempts.ShouldBe(0);
    }

    // ────────── Rollar ──────────

    [Fact]
    public void Bir_xil_rol_ikki_marta_qoshilmaydi()
    {
        var user = YaratilganFoydalanuvchi();
        var roleId = Guid.CreateVersion7();

        user.AssignRole(roleId);
        user.AssignRole(roleId);

        user.RoleIds.Count.ShouldBe(1);
    }

    [Fact]
    public void Rol_olib_tashlanadi()
    {
        var user = YaratilganFoydalanuvchi();
        var roleId = Guid.CreateVersion7();
        user.AssignRole(roleId);

        user.RemoveRole(roleId);

        user.RoleIds.ShouldBeEmpty();
    }

    [Fact]
    public void Faolsizlantirilgan_foydalanuvchi_qayta_faollashtirilganda_blok_tozalanadi()
    {
        var user = YaratilganFoydalanuvchi();
        for (var i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(Now);
        }
        user.Deactivate();

        user.Activate();

        user.IsActive.ShouldBeTrue();
        user.LockedUntil.ShouldBeNull();
    }

    // ────────── O'zini o'zi bloklashdan himoya (foydalanuvchi boshqaruvi) ──────────

    [Fact]
    public void Ozini_ozi_faolsizlantirishga_urinish_rad_etiladi()
    {
        var user = YaratilganFoydalanuvchi();

        var natija = user.Deactivate(user.Id);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.ozini_blokash");
        user.IsActive.ShouldBeTrue("hisob holati o'zgarmasligi kerak");
    }

    [Fact]
    public void Boshqa_foydalanuvchi_tomonidan_faolsizlantirish_ruxsat_etiladi()
    {
        var user = YaratilganFoydalanuvchi();
        var boshqaAdmin = Guid.CreateVersion7();

        var natija = user.Deactivate(boshqaAdmin);

        natija.IsSuccess.ShouldBeTrue();
        user.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Ozidan_administrator_rolini_olib_tashlashga_urinish_rad_etiladi()
    {
        var user = YaratilganFoydalanuvchi();
        var adminRoleId = Guid.CreateVersion7();
        user.AssignRole(adminRoleId);

        var natija = user.RemoveRole(adminRoleId, user.Id, adminRoleId);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("foydalanuvchi.ozini_adminlikdan_chetlashtirish");
        user.RoleIds.ShouldContain(adminRoleId, "rol qolishi kerak, hech narsa o'zgarmagan");
    }

    [Fact]
    public void Ozidan_admin_bolmagan_rolni_olib_tashlash_ruxsat_etiladi()
    {
        var user = YaratilganFoydalanuvchi();
        var adminRoleId = Guid.CreateVersion7();
        var hrRoleId = Guid.CreateVersion7();
        user.AssignRole(adminRoleId);
        user.AssignRole(hrRoleId);

        // O'zining hisobidan, lekin admin bo'lmagan rolni olib tashlaydi — bu xavfsiz
        var natija = user.RemoveRole(hrRoleId, user.Id, adminRoleId);

        natija.IsSuccess.ShouldBeTrue();
        user.RoleIds.ShouldNotContain(hrRoleId);
        user.RoleIds.ShouldContain(adminRoleId);
    }

    [Fact]
    public void Boshqa_foydalanuvchidan_admin_rolini_olib_tashlash_ruxsat_etiladi()
    {
        var user = YaratilganFoydalanuvchi();
        var adminRoleId = Guid.CreateVersion7();
        var boshqaAdmin = Guid.CreateVersion7();
        user.AssignRole(adminRoleId);

        var natija = user.RemoveRole(adminRoleId, boshqaAdmin, adminRoleId);

        natija.IsSuccess.ShouldBeTrue();
        user.RoleIds.ShouldNotContain(adminRoleId);
    }

    // ────────── Profil yangilash ──────────

    [Fact]
    public void Ism_familiya_yangilanadi()
    {
        var user = YaratilganFoydalanuvchi();
        var yangiIsm = FullName.Parse("Тошматов Акбар").Value;

        user.Rename(yangiIsm);

        user.FullName.ShouldBe(yangiIsm);
    }

    [Fact]
    public void Login_yangilanadi()
    {
        var user = YaratilganFoydalanuvchi();
        var yangiLogin = PhoneNumber.Create("901112233").Value;

        user.ChangeLogin(yangiLogin);

        user.Login.ShouldBe(yangiLogin);
    }
}
