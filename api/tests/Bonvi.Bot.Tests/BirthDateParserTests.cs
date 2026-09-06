using Bonvi.Bot.Conversation;

namespace Bonvi.Bot.Tests;

/// <summary>
/// Tug'ilgan sanani o'qish (4-qadam).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bu test kerak.</b> Sana — anketadagi yagona maydon bo'lib,
/// unda XATO JIMGINA o'tib ketishi mumkin: <c>10.11.1990</c> ni bir
/// madaniyat 11-oktabr, boshqasi 10-noyabr deb o'qiydi. Bunday xato
/// hech qayerda ko'rinmasdi — nomzod kartasida shunchaki boshqa sana
/// turardi.
/// </para>
/// <para>
/// Ikkinchi masala — chegara: yosh 14 dan 80 gacha. Undan tashqarisi
/// deyarli har doim terish xatosi.
/// </para>
/// </remarks>
public sealed class BirthDateParserTests
{
    [Theory]
    [InlineData("10.10.1990")]
    [InlineData("1.1.1990")]
    [InlineData("10/10/1990")]
    [InlineData("10-10-1990")]
    [InlineData("  10.10.1990  ")]
    public void Turli_ajratgichlar_qabul_qilinadi(string input)
        => BirthDateParser.TryParse(input).ShouldNotBeNull();

    [Fact]
    public void Kun_va_oy_ORNI_bilan_oqiladi()
    {
        // «kk.oo.yyyy» — kun BIRINCHI. Amerikacha o'qilsa 11-oktabr
        // chiqardi va nomzodning tug'ilgan kuni boshqa bo'lib qolardi.
        var parsed = BirthDateParser.TryParse("10.11.1990").ShouldNotBeNull();

        parsed.Day.ShouldBe(10);
        parsed.Month.ShouldBe(11);
        parsed.Year.ShouldBe(1990);
    }

    [Theory]
    [InlineData("kecha")]
    [InlineData("1990")]
    [InlineData("10.10")]
    [InlineData("31.02.1990")]
    [InlineData("")]
    [InlineData(null)]
    public void Tushunarsiz_matn_rad_etiladi(string? input)
        => BirthDateParser.TryParse(input).ShouldBeNull();

    [Fact]
    public void Kelajakdagi_sana_rad_etiladi()
    {
        var tomorrow = DateOnly.FromDateTime(BotClock.Now.Date).AddDays(1);

        BirthDateParser.TryParse(tomorrow.ToString("dd.MM.yyyy", null)).ShouldBeNull();
    }

    [Fact]
    public void Juda_yosh_va_juda_katta_sana_rad_etiladi()
    {
        var today = DateOnly.FromDateTime(BotClock.Now.Date);

        var tooYoung = today.AddYears(-(BirthDateParser.MinAge - 1));
        var tooOld = today.AddYears(-(BirthDateParser.MaxAge + 1)).AddDays(-1);

        BirthDateParser.TryParse(tooYoung.ToString("dd.MM.yyyy", null)).ShouldBeNull();
        BirthDateParser.TryParse(tooOld.ToString("dd.MM.yyyy", null)).ShouldBeNull();
    }

    [Fact]
    public void Chegaradagi_yosh_qabul_qilinadi()
    {
        var today = DateOnly.FromDateTime(BotClock.Now.Date);

        var youngest = today.AddYears(-BirthDateParser.MinAge);

        BirthDateParser.TryParse(youngest.ToString("dd.MM.yyyy", null)).ShouldBe(youngest);
    }
}
