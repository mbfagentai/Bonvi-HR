using Bonvi.Domain.Identity;

namespace Bonvi.Domain.Tests.Attendance;

/// <summary>
/// Davomat ruxsatlari va ularning standart rollarda taqsimlanishi — SPEC §7.1, §7.2; §11.1 #78, #82.
/// </summary>
/// <remarks>
/// Bazadagi (urug'dan keyingi) holat <c>IdentitySeederPersistenceTests</c> da; bu yerda —
/// koddagi ta'rifning o'zi. Ikkalasi kerak: ta'rif to'g'ri-yu, urug' uni bazaga
/// yetkazmasa, HR menejeri davomat bo'limini umuman ko'rmasdi.
/// </remarks>
public sealed class AttendancePermissionTests
{
    /// <summary>#82. Ikkala kod ro'yxatda va <c>Exists</c> ularni taniydi — aks holda rolga berib bo'lmasdi.</summary>
    [Fact]
    public void Ikkala_davomat_ruxsati_royxatda_va_tanilgan()
    {
        Permissions.AttendanceView.ShouldBe("attendance.view");
        Permissions.AttendanceManage.ShouldBe("attendance.manage");

        Permissions.All.ShouldContain(Permissions.AttendanceView);
        Permissions.All.ShouldContain(Permissions.AttendanceManage);

        Permissions.Exists(Permissions.AttendanceView).ShouldBeTrue();
        Permissions.Exists(Permissions.AttendanceManage).ShouldBeTrue();

        // Eksport alohida ruxsat EMAS — u `reports.export` ostida (§7.3).
        Permissions.Exists("attendance.export").ShouldBeFalse();
    }

    /// <summary>#78. HR menejeri — ikkalasi ham, qo'lda yozilgan ro'yxatda (§7.2).</summary>
    [Fact]
    public void HR_menejerida_ikkala_davomat_ruxsati_bor()
    {
        var (_, ruxsatlar) = SystemRoles.Defaults[SystemRoles.HrManager];

        ruxsatlar.ShouldContain(Permissions.AttendanceView);
        ruxsatlar.ShouldContain(Permissions.AttendanceManage);

        // Tizim sozlamalari (saqlash muddatlari ham) — HR menejerida YO'Q (§11.1 #80).
        ruxsatlar.ShouldNotContain(Permissions.SettingsManage);
    }

    /// <summary>#78. Bot roli — hech biri: xizmat hisobining zarar radiusi o'smasin.</summary>
    [Fact]
    public void Bot_rolida_davomat_ruxsati_yoq()
    {
        var (_, ruxsatlar) = SystemRoles.Defaults[SystemRoles.RecruitingBot];

        ruxsatlar.ShouldNotContain(Permissions.AttendanceView);
        ruxsatlar.ShouldNotContain(Permissions.AttendanceManage);
    }

    /// <summary>Rahbariyat rollari ikkalasini avtomatik oladi (<c>Permissions.All</c> orqali).</summary>
    [Theory]
    [InlineData(SystemRoles.SuperAdmin)]
    [InlineData(SystemRoles.MainBoss)]
    public void Rahbariyat_rollarida_ikkala_davomat_ruxsati_bor(string rol)
    {
        var (_, ruxsatlar) = SystemRoles.Defaults[rol];

        ruxsatlar.ShouldContain(Permissions.AttendanceView);
        ruxsatlar.ShouldContain(Permissions.AttendanceManage);
    }
}
