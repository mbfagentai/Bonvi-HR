using Bonvi.Application.Attendance.Scheduling;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Tests.Attendance.Scheduling;

/// <summary>
/// Munosabatning SANADAGI bo'limi va lavozimi — ko'chirish hodisalaridan (audit topilmasi #1).
/// </summary>
/// <remarks>
/// <c>Employment.Transfer</c> maydonni joyida almashtiradi; sana faqat hodisada. Bu testlar
/// hisoblash (jadval pog'onasi va kun surati) shu sanani hurmat qilishini qotiradi.
/// </remarks>
public sealed class EmploymentPlacementTests
{
    private static readonly Guid EskiBolim = Guid.CreateVersion7();
    private static readonly Guid YangiBolim = Guid.CreateVersion7();
    private static readonly Guid EskiLavozim = Guid.CreateVersion7();
    private static readonly Guid YangiLavozim = Guid.CreateVersion7();

    private static readonly DateOnly Kochish = new(2026, 9, 20);

    private static Employment Munosabat()
        => Employment.Hire(Guid.CreateVersion7(), Guid.CreateVersion7(), new DateOnly(2025, 1, 1), EskiBolim, EskiLavozim).Value;

    [Fact]
    public void Kochirishsiz_munosabatda_joriy_qiymat()
    {
        var munosabat = Munosabat();

        EmploymentPlacement.On(munosabat, new DateOnly(2025, 6, 1))
            .ShouldBe(new EmploymentPlacement(EskiBolim, EskiLavozim));
    }

    /// <summary>Davr o'rtasidagi ko'chirish: kuchga kirishidan oldingi kun — eski joy, o'sha kuni va keyin — yangi.</summary>
    [Fact]
    public void Davr_ortasidagi_kochirishdan_oldingi_kun_eski_joyda()
    {
        var munosabat = Munosabat();
        munosabat.Transfer(Kochish, YangiBolim, YangiLavozim).IsSuccess.ShouldBeTrue();

        munosabat.DepartmentId.ShouldBe(YangiBolim, "sinov sharti: domen maydonni joyida almashtiradi");

        EmploymentPlacement.On(munosabat, Kochish.AddDays(-1)).ShouldBe(new EmploymentPlacement(EskiBolim, EskiLavozim));
        EmploymentPlacement.On(munosabat, Kochish).ShouldBe(new EmploymentPlacement(YangiBolim, YangiLavozim), "kuchga kirish kuni — yangi joy");
        EmploymentPlacement.On(munosabat, Kochish.AddDays(1)).ShouldBe(new EmploymentPlacement(YangiBolim, YangiLavozim));
    }

    /// <summary>
    /// Oldinga sanalangan ko'chirish (buyruq bugun kiritilgan, kuchga kirishi keyingi oyda) — undan
    /// oldingi hamma kun, bugun ham, eski joyda qoladi.
    /// </summary>
    [Fact]
    public void Oldinga_sanalangan_kochirish_sanasigacha_qollanmaydi()
    {
        var munosabat = Munosabat();
        var kelgusiOy = new DateOnly(2026, 10, 1);
        munosabat.Transfer(kelgusiOy, YangiBolim, null).IsSuccess.ShouldBeTrue();

        EmploymentPlacement.On(munosabat, new DateOnly(2026, 9, 27)).DepartmentId.ShouldBe(EskiBolim);
        EmploymentPlacement.On(munosabat, kelgusiOy).DepartmentId.ShouldBe(YangiBolim);
    }

    [Fact]
    public void Ikki_kochirish_orasidagi_kun_birinchisining_yangi_joyida()
    {
        var munosabat = Munosabat();
        var oraliqBolim = Guid.CreateVersion7();
        munosabat.Transfer(new DateOnly(2026, 8, 1), oraliqBolim, null).IsSuccess.ShouldBeTrue();
        munosabat.Transfer(new DateOnly(2026, 9, 1), YangiBolim, null).IsSuccess.ShouldBeTrue();

        EmploymentPlacement.On(munosabat, new DateOnly(2026, 7, 15)).DepartmentId.ShouldBe(EskiBolim);
        EmploymentPlacement.On(munosabat, new DateOnly(2026, 8, 15)).DepartmentId.ShouldBe(oraliqBolim);
        EmploymentPlacement.On(munosabat, new DateOnly(2026, 9, 15)).DepartmentId.ShouldBe(YangiBolim);
    }

    /// <summary>Faqat lavozim o'zgargan hodisa bo'lim tarixiga tegmaydi — va aksincha.</summary>
    [Fact]
    public void Har_bir_maydon_oz_hodisalari_boyicha()
    {
        var munosabat = Munosabat();
        munosabat.Transfer(new DateOnly(2026, 8, 1), null, YangiLavozim).IsSuccess.ShouldBeTrue();
        munosabat.Transfer(new DateOnly(2026, 9, 1), YangiBolim, null).IsSuccess.ShouldBeTrue();

        EmploymentPlacement.On(munosabat, new DateOnly(2026, 8, 15))
            .ShouldBe(new EmploymentPlacement(EskiBolim, YangiLavozim));
        EmploymentPlacement.On(munosabat, new DateOnly(2026, 7, 15))
            .ShouldBe(new EmploymentPlacement(EskiBolim, EskiLavozim));
    }

    /// <summary>Faqat oklad o'zgargan «ko'chirish» joyni o'zgartirmaydi.</summary>
    [Fact]
    public void Joyni_ozgartirmagan_hodisa_hisobga_olinmaydi()
    {
        var munosabat = Munosabat();
        munosabat.Transfer(Kochish, null, null).IsSuccess.ShouldBeTrue();

        EmploymentPlacement.On(munosabat, Kochish.AddDays(-1)).ShouldBe(new EmploymentPlacement(EskiBolim, EskiLavozim));
    }

    /// <summary>
    /// Excel'dan ko'chirilgan hodisada «eski» qiymat bo'sh (noma'lum) — taxmin yasalmaydi,
    /// joriy qiymat olinadi (tuzatishdan oldingi xatti-harakat).
    /// </summary>
    [Fact]
    public void Eski_qiymati_nomalum_hodisada_joriy_qiymat()
    {
        var munosabat = Munosabat();
        munosabat.ImportEvent(EmploymentEvent.Import(
            munosabat.Id,
            EmploymentEventType.Promotion,
            Kochish,
            toPositionId: EskiLavozim));

        EmploymentPlacement.On(munosabat, Kochish.AddDays(-1)).PositionId.ShouldBe(EskiLavozim);
    }
}
