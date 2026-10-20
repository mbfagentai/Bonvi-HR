using Bonvi.Domain.Attendance;

namespace Bonvi.Domain.Tests.Attendance;

/// <summary>
/// Yo'qlik so'rovi — parallellik hisoblagichi, tahrir belgisi va izoh uzunligi
/// (SPEC-verifikatsiya-sorov-tahriri §2.3, §3.3).
/// </summary>
public sealed class AbsenceRequestTests
{
    private static readonly Guid Xodim = Guid.CreateVersion7();
    private static readonly Guid Tur = Guid.CreateVersion7();
    private static readonly DateOnly Sana = new(2026, 10, 21);
    private static readonly DateTimeOffset Lahza = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private static AbsenceRequest Yangi() => AbsenceRequest.Create(Xodim, Tur, Sana, Sana).Value;

    [Fact]
    public void Yangi_sorov_nolinchi_versiyada_va_tahrirlanmagan()
    {
        var sorov = Yangi();

        sorov.Version.ShouldBe(0);
        sorov.EditedAt.ShouldBeNull();
        sorov.EditedByAccountId.ShouldBeNull();
    }

    [Fact]
    public void Tahrir_versiyani_oshiradi()
    {
        var sorov = Yangi();

        sorov.Update(Tur, Sana, Sana.AddDays(1), null, null, "yangi").IsSuccess.ShouldBeTrue();

        sorov.Version.ShouldBe(1);
        sorov.EditedAt.ShouldBeNull("lahza berilmasa tahrir belgisi qo'yilmaydi");
    }

    [Fact]
    public void Ilovadagi_tahrir_lahza_va_hisobni_yozadi()
    {
        var sorov = Yangi();
        var hisob = Guid.CreateVersion7();

        sorov.Update(Tur, Sana, Sana, new TimeOnly(14, 0), new TimeOnly(16, 0), null, Lahza, hisob).IsSuccess.ShouldBeTrue();

        sorov.EditedAt.ShouldBe(Lahza);
        sorov.EditedByAccountId.ShouldBe(hisob);
        sorov.Status.ShouldBe(AbsenceRequestStatus.Pending);
    }

    [Fact]
    public void HR_tahriri_faqat_lahzani_yozadi_va_ilova_belgisini_tozalaydi()
    {
        var sorov = Yangi();
        sorov.Update(Tur, Sana, Sana, null, null, null, Lahza, Guid.CreateVersion7());

        sorov.Update(Tur, Sana, Sana, null, null, null, Lahza.AddHours(1)).IsSuccess.ShouldBeTrue();

        sorov.EditedAt.ShouldBe(Lahza.AddHours(1));
        sorov.EditedByAccountId.ShouldBeNull("oxirgi tahrir HR niki");
        sorov.Version.ShouldBe(2);
    }

    [Fact]
    public void Tasdiqlash_versiyani_oshiradi()
    {
        var sorov = Yangi();
        sorov.Approve(Guid.CreateVersion7(), Lahza).IsSuccess.ShouldBeTrue();
        sorov.Version.ShouldBe(1);
    }

    [Fact]
    public void Rad_etish_versiyani_oshiradi()
    {
        var sorov = Yangi();
        sorov.Reject(Guid.CreateVersion7(), Lahza).IsSuccess.ShouldBeTrue();
        sorov.Version.ShouldBe(1);
    }

    [Fact]
    public void HR_bekor_qilishi_versiyani_oshiradi()
    {
        var sorov = Yangi();
        sorov.Approve(Guid.CreateVersion7(), Lahza);
        sorov.Cancel(Guid.CreateVersion7(), Lahza).IsSuccess.ShouldBeTrue();
        sorov.Version.ShouldBe(2);
    }

    [Fact]
    public void Xodim_bekor_qilishi_versiyani_oshiradi()
    {
        var sorov = Yangi();
        sorov.CancelBySubmitter(Guid.CreateVersion7(), Lahza).IsSuccess.ShouldBeTrue();
        sorov.Version.ShouldBe(1);
    }

    [Fact]
    public void Rad_etilgan_amal_versiyaga_tegmaydi()
    {
        var sorov = Yangi();
        sorov.Approve(Guid.CreateVersion7(), Lahza);

        sorov.Update(Tur, Sana, Sana, null, null, null, Lahza).Error.Code.ShouldBe("davomat.sorov_qaror_qabul_qilingan");
        sorov.Reject(Guid.CreateVersion7(), Lahza).Error.Code.ShouldBe("davomat.sorov_qaror_qabul_qilingan");
        sorov.CancelBySubmitter(Guid.CreateVersion7(), Lahza).Error.Code.ShouldBe("davomat.sorov_qaror_qabul_qilingan");

        sorov.Version.ShouldBe(1);
        sorov.EditedAt.ShouldBeNull();
    }

    [Fact]
    public void Uzun_izoh_yaratishda_tahrirda_va_qarorda_rad_etiladi()
    {
        var uzun = new string('a', AbsenceRequest.MaxNoteLength + 1);
        var chegarada = new string('a', AbsenceRequest.MaxNoteLength);

        AbsenceRequest.Create(Xodim, Tur, Sana, Sana, note: uzun).Error.Code.ShouldBe("davomat.izoh_uzun");
        AbsenceRequest.Create(Xodim, Tur, Sana, Sana, note: chegarada).IsSuccess.ShouldBeTrue();

        var sorov = Yangi();
        sorov.Update(Tur, Sana, Sana, null, null, uzun, Lahza).Error.Code.ShouldBe("davomat.izoh_uzun");
        sorov.Approve(Guid.CreateVersion7(), Lahza, uzun).Error.Code.ShouldBe("davomat.izoh_uzun");
        sorov.Reject(Guid.CreateVersion7(), Lahza, uzun).Error.Code.ShouldBe("davomat.izoh_uzun");

        sorov.Status.ShouldBe(AbsenceRequestStatus.Pending);
        sorov.Version.ShouldBe(0);
    }
}
