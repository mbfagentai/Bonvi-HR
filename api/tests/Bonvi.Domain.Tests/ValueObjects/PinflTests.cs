using Bonvi.Domain.ValueObjects;

namespace Bonvi.Domain.Tests.ValueObjects;

/// <summary>
/// JSHSHIR qiymat obyekti testlari.
/// Namunalar Bonvi'ning haqiqiy «№91 бланк» faylidan olingan.
/// </summary>
public sealed class PinflTests
{
    [Theory]
    [InlineData("31501914320045")]   // Мамажонов Б.Ф. — 15.01.1991, erkak
    [InlineData("30203964320064")]   // Тошпулатов А.А. — 02.03.1996, erkak
    [InlineData("40701774250027")]   // Хасанова С.Т.  — 07.01.1977, ayol
    [InlineData("51906047050023")]   // Мухаммадрахимов Б. — 19.06.2004, erkak
    [InlineData("61904056980028")]
    public void Haqiqiy_JSHSHIR_qabul_qilinadi(string qiymat)
    {
        var natija = Pinfl.Create(qiymat);

        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Message : null);
        natija.Value.Value.ShouldBe(qiymat);
    }

    [Fact]
    public void JSHSHIR_dan_tugilgan_sana_togri_ochiladi()
    {
        // Мамажонов Бобуржон — Excel'da tug'ilgan sanasi 15.01.1991
        var pinfl = Pinfl.Create("31501914320045").Value;

        pinfl.BirthDate.ShouldBe(new DateOnly(1991, 1, 15));
        pinfl.Gender.ShouldBe(Gender.Male);
    }

    [Theory]
    [InlineData('1', 1800)]
    [InlineData('3', 1900)]
    [InlineData('5', 2000)]
    public void Birinchi_raqam_tugilgan_asrni_belgilaydi(char birinchi, int asr)
    {
        // 01.01.YY sanasi bilan to'g'ri nazorat raqamini hisoblaymiz
        var asos = $"{birinchi}010195000000";
        var pinfl = Pinfl.Create(asos + HisoblaNazoratRaqami(asos + "0")).Value;

        pinfl.BirthDate.Year.ShouldBe(asr + 95);
    }

    [Fact]
    public void Toq_raqam_erkak_juft_raqam_ayol_degani()
    {
        Pinfl.Create("31501914320045").Value.Gender.ShouldBe(Gender.Male);   // 3 — toq
        Pinfl.Create("40701774250027").Value.Gender.ShouldBe(Gender.Female); // 4 — juft
        Pinfl.Create("51906047050023").Value.Gender.ShouldBe(Gender.Male);   // 5 — toq
        Pinfl.Create("61904056980028").Value.Gender.ShouldBe(Gender.Female); // 6 — juft
    }

    [Theory]
    [InlineData(null, "pinfl.bosh")]
    [InlineData("", "pinfl.bosh")]
    [InlineData("   ", "pinfl.bosh")]
    [InlineData("315019143200", "pinfl.uzunlik")]         // 12 xona
    [InlineData("3150191432004567", "pinfl.uzunlik")]     // 16 xona
    [InlineData("71501914320045", "pinfl.birinchi_raqam")] // 7 — bunday asr yo'q
    public void Notogri_kirish_rad_etiladi(string? kirish, string kutilgan)
    {
        var natija = Pinfl.Create(kirish);

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe(kutilgan);
    }

    [Fact]
    public void Nazorat_raqami_notogri_bolsa_rad_etiladi()
    {
        // 31501914320045 — haqiqiy. Oxirgi raqamni buzamiz.
        var natija = Pinfl.Create("31501914320046");

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("pinfl.nazorat_raqami");
    }

    [Fact]
    public void Mavjud_bolmagan_sana_rad_etiladi()
    {
        // 32-yanvar
        var natija = Pinfl.Create("33201914320045");

        natija.IsFailure.ShouldBeTrue();
        natija.Error.Code.ShouldBe("pinfl.sana");
    }

    [Theory]
    [InlineData("3150-1914-3200-45")]
    [InlineData("3 1501 9143 20045")]
    [InlineData("  31501914320045  ")]
    public void Ajratgichlar_va_boshliqlar_tozalanadi(string kirish)
    {
        var natija = Pinfl.Create(kirish);

        natija.IsSuccess.ShouldBeTrue();
        natija.Value.Value.ShouldBe("31501914320045");
    }

    [Fact]
    public void Tugilgan_sana_mosligini_tekshiradi()
    {
        var pinfl = Pinfl.Create("31501914320045").Value;

        pinfl.MatchesBirthDate(new DateOnly(1991, 1, 15)).ShouldBeTrue();
        pinfl.MatchesBirthDate(new DateOnly(1991, 1, 16)).ShouldBeFalse();
    }

    [Fact]
    public void Bir_xil_qiymatli_JSHSHIRlar_teng()
    {
        var a = Pinfl.Create("31501914320045").Value;
        var b = Pinfl.Create("3150 1914 3200 45").Value;

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    /// <summary>Test ma'lumoti tayyorlash uchun nazorat raqamini hisoblaydi.</summary>
    private static char HisoblaNazoratRaqami(string ottizUchRaqam)
    {
        int[] ogirliklar = [7, 3, 1, 7, 3, 1, 7, 3, 1, 7, 3, 1, 7];
        var yigindi = 0;
        for (var i = 0; i < 13; i++)
        {
            yigindi += (ottizUchRaqam[i] - '0') * ogirliklar[i];
        }

        return (char)('0' + (yigindi % 10));
    }
}
