using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Tests.Recruiting;

/// <summary>Ish taklifi — voronkaning oxirgi rasmiy qadami.</summary>
public sealed class JobOfferTests
{
    private static readonly Guid NomzodId = Guid.CreateVersion7();
    private static readonly Guid VakansiyaId = Guid.CreateVersion7();
    private static readonly DateOnly Sana = new(2026, 3, 1);

    private static Money Oklad(decimal summa = 5_000_000m) => Money.Create(summa).Value;

    private static JobOffer Taklif()
        => JobOffer.Send(NomzodId, VakansiyaId, Oklad(), Sana.AddDays(10), Sana).Value;

    [Fact]
    public void Nomzodsiz_yuborilmaydi()
    {
        var natija = JobOffer.Send(Guid.Empty, VakansiyaId, Oklad(), Sana, Sana);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("taklif.nomzod_yoq");
    }

    [Fact]
    public void Vakansiyasiz_yuborilmaydi()
    {
        var natija = JobOffer.Send(NomzodId, Guid.Empty, Oklad(), Sana, Sana);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("taklif.vakansiya_yoq");
    }

    [Fact]
    public void Ish_boshlash_sanasi_yuborilgan_sanadan_oldin_bolmaydi()
    {
        var natija = JobOffer.Send(NomzodId, VakansiyaId, Oklad(), Sana.AddDays(-1), Sana);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("taklif.boshlash_sanasi");
    }

    [Fact]
    public void Javob_muddati_yuborilgan_sanadan_oldin_bolmaydi()
    {
        var natija = JobOffer.Send(NomzodId, VakansiyaId, Oklad(), Sana, Sana, expiresAt: Sana.AddDays(-2));

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("taklif.muddat_sanasi");
    }

    [Fact]
    public void Yuborilgan_taklif_javob_kutadi()
    {
        var taklif = Taklif();

        taklif.Status.ShouldBe(JobOfferStatus.Sent);
        taklif.IsPending.ShouldBeTrue();
        taklif.RespondedAt.ShouldBeNull();
        taklif.Salary.Amount.ShouldBe(5_000_000m);
    }

    [Fact]
    public void Qabul_qilingan_taklif_qulflanadi()
    {
        var taklif = Taklif();

        var natija = taklif.Accept(Sana.AddDays(2), "Розилик берди");

        natija.IsSuccess.ShouldBeTrue();
        taklif.Status.ShouldBe(JobOfferStatus.Accepted);
        taklif.RespondedAt.ShouldBe(Sana.AddDays(2));
        taklif.ResponseNote.ShouldBe("Розилик берди");
        taklif.IsPending.ShouldBeFalse();
    }

    [Fact]
    public void Ikki_marta_javob_berilmaydi()
    {
        var taklif = Taklif();
        taklif.Decline(Sana.AddDays(1));

        var natija = taklif.Accept(Sana.AddDays(2));

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("taklif.javob_berilgan");
        taklif.Status.ShouldBe(JobOfferStatus.Declined, "birinchi javob kuchda qoladi");
    }

    [Fact]
    public void Javob_sanasi_yuborilgan_sanadan_oldin_bolmaydi()
    {
        var taklif = Taklif();

        var natija = taklif.Accept(Sana.AddDays(-1));

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("taklif.javob_sanasi");
    }

    [Fact]
    public void Muddati_otgan_deb_belgilanadi()
    {
        var taklif = Taklif();

        var natija = taklif.MarkExpired(Sana.AddDays(30), "Жавоб келмади");

        natija.IsSuccess.ShouldBeTrue();
        taklif.Status.ShouldBe(JobOfferStatus.Expired);
    }

    [Fact]
    public void Javob_kelmaguncha_shartlar_tuzatiladi()
    {
        var taklif = Taklif();

        var natija = taklif.Update(Oklad(6_500_000m), Sana.AddDays(15), Sana.AddDays(5), "Оклад оширилди");

        natija.IsSuccess.ShouldBeTrue();
        taklif.Salary.Amount.ShouldBe(6_500_000m);
        taklif.StartDate.ShouldBe(Sana.AddDays(15));
        taklif.ExpiresAt.ShouldBe(Sana.AddDays(5));
    }

    [Fact]
    public void Javob_berilgan_taklif_tahrirlanmaydi()
    {
        // Aks holda qabul qilingan taklifning okladi keyin o'zgartirilib,
        // xodim kartasidagi summa bilan jimgina farq qilib qolardi.
        var taklif = Taklif();
        taklif.Accept(Sana.AddDays(1));

        var natija = taklif.Update(Oklad(9_000_000m), Sana.AddDays(20));

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("taklif.javob_berilgan");
        taklif.Salary.Amount.ShouldBe(5_000_000m);
    }
}
