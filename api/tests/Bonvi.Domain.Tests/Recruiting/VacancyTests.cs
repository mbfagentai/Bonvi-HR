using Bonvi.Domain.Recruiting;

namespace Bonvi.Domain.Tests.Recruiting;

/// <summary>Vakansiya — ochilgan ish o'rni va uning holati.</summary>
public sealed class VacancyTests
{
    private static readonly Guid LavozimId = Guid.CreateVersion7();
    private static readonly Guid FirmaId = Guid.CreateVersion7();
    private static readonly DateOnly Sana = new(2026, 3, 1);

    [Fact]
    public void Lavozimsiz_ochilmaydi()
    {
        var natija = Vacancy.Open(Guid.Empty, FirmaId, Sana);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("vakansiya.lavozim_yoq");
    }

    [Fact]
    public void Yuridik_shaxssiz_ochilmaydi()
    {
        var natija = Vacancy.Open(LavozimId, Guid.Empty, Sana);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("vakansiya.yuridik_shaxs_yoq");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(Vacancy.MaxOpenings + 1)]
    public void Orin_soni_chegaradan_chiqmaydi(int orinlar)
    {
        var natija = Vacancy.Open(LavozimId, FirmaId, Sana, openings: orinlar);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("vakansiya.orin_soni");
    }

    [Fact]
    public void Ochilgan_vakansiya_nomzod_qabul_qiladi()
    {
        var natija = Vacancy.Open(LavozimId, FirmaId, Sana, openings: 3);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Status.ShouldBe(VacancyStatus.Open);
        natija.Value.AcceptsCandidates.ShouldBeTrue();
        natija.Value.Openings.ShouldBe(3);
        natija.Value.ClosedAt.ShouldBeNull();
    }

    [Fact]
    public void Toxtatilgan_vakansiya_nomzod_qabul_qilmaydi()
    {
        var vakansiya = Vacancy.Open(LavozimId, FirmaId, Sana).Value;

        var natija = vakansiya.Pause("Бюджет кутилмоқда");

        natija.IsSuccess.ShouldBeTrue();
        vakansiya.Status.ShouldBe(VacancyStatus.Paused);
        vakansiya.AcceptsCandidates.ShouldBeFalse("to'xtatishning ma'nosi — hozircha kerak emas");
        vakansiya.Note.ShouldBe("Бюджет кутилмоқда");
    }

    [Fact]
    public void Yopilgan_vakansiya_toxtatilmaydi()
    {
        var vakansiya = Vacancy.Open(LavozimId, FirmaId, Sana).Value;
        vakansiya.Close(Sana.AddDays(10));

        var natija = vakansiya.Pause();

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("vakansiya.yopiq");
    }

    [Fact]
    public void Ikki_marta_yopilmaydi()
    {
        var vakansiya = Vacancy.Open(LavozimId, FirmaId, Sana).Value;
        vakansiya.Close(Sana.AddDays(10));

        var natija = vakansiya.Close(Sana.AddDays(11));

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("vakansiya.allaqachon_yopiq");
    }

    [Fact]
    public void Ochilishdan_oldin_yopilmaydi()
    {
        var vakansiya = Vacancy.Open(LavozimId, FirmaId, Sana).Value;

        var natija = vakansiya.Close(Sana.AddDays(-1));

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("vakansiya.yopish_sanasi");
    }

    [Fact]
    public void Yopilgan_vakansiya_qayta_ochiladi()
    {
        // Bonvi'da odatiy hol: olingan odam bir haftada ketib qoladi
        // («91.8 Стажировкадан кетганлар»), o'rin esa yana bo'sh qoladi.
        var vakansiya = Vacancy.Open(LavozimId, FirmaId, Sana).Value;
        vakansiya.Close(Sana.AddDays(10));

        var natija = vakansiya.Resume();

        natija.IsSuccess.ShouldBeTrue();
        vakansiya.Status.ShouldBe(VacancyStatus.Open);
        vakansiya.ClosedAt.ShouldBeNull("qayta ochilgan vakansiyada yopilish sanasi bo'lmaydi");
        vakansiya.AcceptsCandidates.ShouldBeTrue();
    }

    [Fact]
    public void Tahrirlashda_lavozim_va_firma_ham_ozgaradi()
    {
        // Vakansiya — REJA, fakt emas: «sotuvchi emas, katta sotuvchi
        // kerak ekan» — odatiy tuzatish.
        var vakansiya = Vacancy.Open(LavozimId, FirmaId, Sana).Value;
        var yangiLavozim = Guid.CreateVersion7();

        var natija = vakansiya.Update(yangiLavozim, FirmaId, Sana, openings: 2, note: "Шошилинч");

        natija.IsSuccess.ShouldBeTrue();
        vakansiya.PositionId.ShouldBe(yangiLavozim);
        vakansiya.Openings.ShouldBe(2);
        vakansiya.Note.ShouldBe("Шошилинч");
        vakansiya.Status.ShouldBe(VacancyStatus.Open, "holat tahrirlashda o'zgarmaydi");
    }
}
