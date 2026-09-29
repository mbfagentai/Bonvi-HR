using Bonvi.Domain.Recruiting;

namespace Bonvi.Domain.Tests.Recruiting;

/// <summary>Vakansiya — ochilgan ish o'rni va uning holati.</summary>
/// <remarks>
/// ⚠️ Vakansiyada endi kompaniya tuzilmasidan HECH NARSA yo'q: lavozim,
/// bo'lim va firma olib tashlangan (sababi <see cref="Vacancy"/> izohida).
/// Shu sababli bu yerdagi «lavozim yo'q» testlari «NOM yo'q» testlariga
/// aylandi va firma bo'yicha test butunlay olib tashlandi — tekshirish
/// mumkin bo'lgan maydonning o'zi qolmadi.
/// <para>
/// Nom 2026-09-29 dan beri vakansiyaning O'ZIDA — «Vakansiya nomlari»
/// ma'lumotnomasi olib tashlandi (sababi <see cref="Vacancy"/> izohida).
/// Nomning bandligi (takror) esa use-case darajasida tekshiriladi —
/// uning testlari <c>RecruitingApiTests</c> da.
/// </para>
/// </remarks>
public sealed class VacancyTests
{
    private const string Nom = "Сотувчи-консультант";
    private static readonly DateOnly Sana = new(2026, 3, 1);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \t ")]
    public void Nomsiz_ochilmaydi(string? nom)
    {
        var natija = Vacancy.Open(nom, Sana);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("vakansiya.nom_yoq");
    }

    [Fact]
    public void Nomdagi_bosh_joylar_yigiladi()
    {
        // Ma'lumotnoma nomidagi qoida (`ReferenceEntity.Normalize`): Excel
        // eksportidagi tabulyatsiya va uzilmaydigan bo'shliq ko'zga oddiy
        // probeldek ko'rinadi va bir nomni ikki xil yozuvga aylantirardi.
        var natija = Vacancy.Open("  Сотувчи \t\u00A0 консультант ", Sana);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Title.ShouldBe("Сотувчи консультант");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(Vacancy.MaxOpenings + 1)]
    public void Orin_soni_chegaradan_chiqmaydi(int orinlar)
    {
        var natija = Vacancy.Open(Nom, Sana, openings: orinlar);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("vakansiya.orin_soni");
    }

    [Fact]
    public void Ochilgan_vakansiya_nomzod_qabul_qiladi()
    {
        var natija = Vacancy.Open(Nom, Sana, openings: 3);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Status.ShouldBe(VacancyStatus.Open);
        natija.Value.AcceptsCandidates.ShouldBeTrue();
        natija.Value.Openings.ShouldBe(3);
        natija.Value.ClosedAt.ShouldBeNull();
        natija.Value.Title.ShouldBe(Nom);
    }

    [Fact]
    public void Yopilgan_vakansiya_nomzod_qabul_qilmaydi()
    {
        // Ilgari bu yerda AYNI shu tekshiruv «to'xtatilgan» holat uchun
        // ham takrorlanardi. Ikkala test bir xil natijani tasdiqlardi —
        // aynan shu sababli holatlardan biri olib tashlandi
        // (`VacancyStatus` izohi).
        var vakansiya = Vacancy.Open(Nom, Sana).Value;

        var natija = vakansiya.Close(Sana.AddDays(10), "Бюджет кутилмоқда");

        natija.IsSuccess.ShouldBeTrue();
        vakansiya.Status.ShouldBe(VacancyStatus.Closed);
        vakansiya.AcceptsCandidates.ShouldBeFalse("yopishning ma'nosi — yangi nomzod kerak emas");
        vakansiya.Note.ShouldBe("Бюджет кутилмоқда", "sabab izohga yoziladi, alohida holatga emas");
        vakansiya.ClosedAt.ShouldBe(Sana.AddDays(10));
    }

    [Fact]
    public void Ikki_marta_yopilmaydi()
    {
        var vakansiya = Vacancy.Open(Nom, Sana).Value;
        vakansiya.Close(Sana.AddDays(10));

        var natija = vakansiya.Close(Sana.AddDays(11));

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("vakansiya.allaqachon_yopiq");
    }

    [Fact]
    public void Ochilishdan_oldin_yopilmaydi()
    {
        var vakansiya = Vacancy.Open(Nom, Sana).Value;

        var natija = vakansiya.Close(Sana.AddDays(-1));

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("vakansiya.yopish_sanasi");
    }

    [Fact]
    public void Yopilgan_vakansiya_qayta_ochiladi()
    {
        // Bonvi'da odatiy hol: olingan odam bir haftada ketib qoladi
        // («91.8 Стажировкадан кетганлар»), o'rin esa yana bo'sh qoladi.
        // Yopish oynasidagi «qayta ochish mumkin emas» degan eski yozuv
        // aynan shu sababli YOLG'ON edi.
        var vakansiya = Vacancy.Open(Nom, Sana).Value;
        vakansiya.Close(Sana.AddDays(10));

        var natija = vakansiya.Reopen();

        natija.IsSuccess.ShouldBeTrue();
        vakansiya.Status.ShouldBe(VacancyStatus.Open);
        vakansiya.ClosedAt.ShouldBeNull("qayta ochilgan vakansiyada yopilish sanasi bo'lmaydi");
        vakansiya.AcceptsCandidates.ShouldBeTrue();
    }

    [Fact]
    public void Ochiq_vakansiyani_qayta_ochish_zararsiz()
    {
        // Yopishdan farqli: qayta ochish sana ham, izoh ham yozmaydi,
        // ya'ni takrorlanishi mavjud faktni buzmaydi. Shu sababli
        // «allaqachon ochiq» xatosi ATAYLAB yo'q (`Vacancy.Reopen` izohi).
        var vakansiya = Vacancy.Open(Nom, Sana, note: "Шошилинч").Value;

        var natija = vakansiya.Reopen();

        natija.IsSuccess.ShouldBeTrue();
        vakansiya.Status.ShouldBe(VacancyStatus.Open);
        vakansiya.Note.ShouldBe("Шошилинч", "qayta ochish izohga tegmaydi");
    }

    [Fact]
    public void Tahrirlashda_nom_ham_ozgaradi()
    {
        // Vakansiya — REJA, fakt emas: «сотувчи эмас, катта сотувчи
        // керак экан» — odatiy tuzatish.
        var vakansiya = Vacancy.Open(Nom, Sana).Value;
        var natija = vakansiya.Update("Катта сотувчи", Sana, openings: 2, note: "Шошилинч");

        natija.IsSuccess.ShouldBeTrue();
        vakansiya.Title.ShouldBe("Катта сотувчи");
        vakansiya.Openings.ShouldBe(2);
        vakansiya.Note.ShouldBe("Шошилинч");
        vakansiya.Status.ShouldBe(VacancyStatus.Open, "holat tahrirlashda o'zgarmaydi");
    }

    [Fact]
    public void Tahrirlashda_ham_nom_bosh_qolmaydi()
    {
        var vakansiya = Vacancy.Open(Nom, Sana).Value;

        var natija = vakansiya.Update("  ", Sana);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("vakansiya.nom_yoq");
        vakansiya.Title.ShouldBe(Nom, "rad etilgan tahrir hech narsani o'zgartirmaydi");
    }
}
