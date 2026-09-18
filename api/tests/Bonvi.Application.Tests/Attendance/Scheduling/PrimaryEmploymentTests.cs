using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Domain.Employees;
using static Bonvi.Application.Tests.Attendance.Computation.AttendanceScenario;

namespace Bonvi.Application.Tests.Attendance.Scheduling;

/// <summary>
/// Kun qaysi munosabatga tegishli — SPEC §5.1.
/// </summary>
public sealed class PrimaryEmploymentTests
{
    private static readonly Guid Xodim = Guid.CreateVersion7();

    private static Employment Hire(DateOnly hired, bool primary = true)
    {
        var employment = Employment.Hire(Xodim, Guid.CreateVersion7(), hired).Value;
        employment.SetPrimary(primary);
        return employment;
    }

    [Fact]
    public void Asosiy_belgisi_erta_ishga_kirishdan_ustun()
    {
        var eski = Hire(new DateOnly(2024, 1, 1), primary: false);
        var asosiy = Hire(new DateOnly(2026, 1, 1));

        PrimaryEmployment.Select([eski, asosiy], Monday).ShouldBe(asosiy);
    }

    [Fact]
    public void Asosiy_belgilanmagan_bolsa_eng_erta_ishga_kirgani()
    {
        var erta = Hire(new DateOnly(2024, 1, 1), primary: false);
        var kech = Hire(new DateOnly(2026, 1, 1), primary: false);

        PrimaryEmployment.Select([kech, erta], Monday).ShouldBe(erta);
    }

    [Fact]
    public void Hamma_narsa_teng_bolsa_kichik_identifikator_tasodif_emas()
    {
        var birinchi = Hire(LongAgo, primary: false);
        var ikkinchi = Hire(LongAgo, primary: false);
        var kichigi = birinchi.Id.CompareTo(ikkinchi.Id) < 0 ? birinchi : ikkinchi;

        PrimaryEmployment.Select([birinchi, ikkinchi], Monday).ShouldBe(kichigi);
        PrimaryEmployment.Select([ikkinchi, birinchi], Monday).ShouldBe(kichigi);
    }

    [Fact]
    public void Qamramaydigan_asosiy_munosabat_hisobga_olinmaydi()
    {
        var tugagan = Hire(new DateOnly(2024, 1, 1));
        tugagan.Terminate(new DateOnly(2025, 12, 31), reasonId: null).IsSuccess.ShouldBeTrue();
        var orindosh = Hire(new DateOnly(2025, 6, 1), primary: false);

        PrimaryEmployment.Select([tugagan, orindosh], Monday).ShouldBe(orindosh);
    }

    [Fact]
    public void Ishdan_ketish_sanasi_ham_qamraladi_keyingi_kun_emas()
    {
        var munosabat = Hire(LongAgo);
        munosabat.Terminate(Monday, reasonId: null).IsSuccess.ShouldBeTrue();

        PrimaryEmployment.Covers(munosabat, Monday).ShouldBeTrue();
        PrimaryEmployment.Covers(munosabat, Tuesday).ShouldBeFalse();
        PrimaryEmployment.Select([munosabat], Tuesday).ShouldBeNull();
    }

    [Fact]
    public void Ishga_kirishdan_oldingi_sana_qamralmaydi()
    {
        var munosabat = Hire(Tuesday);

        PrimaryEmployment.Select([munosabat], Monday).ShouldBeNull();
        PrimaryEmployment.Select([munosabat], Tuesday).ShouldBe(munosabat);
    }

    [Fact]
    public void Ochirilgan_munosabat_hisobga_olinmaydi()
    {
        var ochirilgan = Hire(new DateOnly(2024, 1, 1));
        ochirilgan.DeletedAt = DateTimeOffset.UtcNow;
        var tirik = Hire(LongAgo, primary: false);

        PrimaryEmployment.Select([ochirilgan, tirik], Monday).ShouldBe(tirik);
    }
}
