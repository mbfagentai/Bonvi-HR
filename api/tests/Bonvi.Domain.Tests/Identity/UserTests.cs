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
}
