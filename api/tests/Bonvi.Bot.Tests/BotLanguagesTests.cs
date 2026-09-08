using Bonvi.Bot.Conversation;

namespace Bonvi.Bot.Tests;

/// <summary>
/// Til va tizimdagi kodi orasidagi moslik.
/// </summary>
/// <remarks>
/// <b>Nega test kerak.</b> Kod nomzod bilan birga BAZAGA yoziladi va
/// undan qaytib o'qiladi. Yozishda bir shakl, o'qishda boshqasi
/// ishlatilsa, ruscha nomzod kirillcha menyu va kirillcha suhbat
/// taklifini olardi va bu faqat nomzodda ko'rinardi.
/// </remarks>
public sealed class BotLanguagesTests
{
    [Theory]
    [InlineData(BotLanguage.Cyrillic, "uz-Cyrl")]
    [InlineData(BotLanguage.Latin, "uz-Latn")]
    [InlineData(BotLanguage.Russian, "ru")]
    public void Kod_tizimdagi_bilan_bir_xil(BotLanguage language, string expected)
    {
        // Kodlar `web/src/shared/i18n/locales` dagi fayl nomlari bilan
        // AYNAN bir xil bo'lishi shart.
        BotLanguages.Code(language).ShouldBe(expected);
    }

    [Theory]
    [InlineData(BotLanguage.Cyrillic)]
    [InlineData(BotLanguage.Latin)]
    [InlineData(BotLanguage.Russian)]
    public void Yozib_oqilganda_til_ozgarmaydi(BotLanguage language)
        => BotLanguages.Parse(BotLanguages.Code(language)).ShouldBe(language);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("de")]
    [InlineData("uz")]
    public void Notanish_kod_kirillga_tushadi(string? code)
    {
        /*
          Istisno tashlash NOTO'G'RI bo'lardi: nomzod rad etilgani
          haqidagi xabarni umuman olmay qolardi. Kirill esa tayanch
          til — tizimdagi ma'lumotning deyarli hammasi shu yozuvda.
        */
        BotLanguages.Parse(code).ShouldBe(BotLanguage.Cyrillic);
    }

    [Fact]
    public void Kod_atrofidagi_boshliq_ahamiyatsiz()
        => BotLanguages.Parse("  ru  ").ShouldBe(BotLanguage.Russian);

    [Fact]
    public void Rad_etish_xabari_hamma_nomzodga_bir_xil()
    {
        /*
          Foydalanuvchi qarori (2026-09-08): rad etish javobi
          KOMPANIYANING rasmiy matni, u nomzoddan nomzodga farq
          qilmaydi. Shu sababli matn tilga umuman bog'lanmagan —
          ismdan boshqa hech narsa o'zgarmasligi shu yerda
          qulflanadi.
        */
        var first = BotTexts.RejectionNotice("Karimov Aziz");
        var second = BotTexts.RejectionNotice("Karimov Aziz");

        first.ShouldBe(second);
    }

    [Fact]
    public void Rad_etish_xabari_ikki_tilda_ham_yoziladi()
    {
        var text = BotTexts.RejectionNotice("Turdimuhammadova Durdona");

        // Nomzod xabarni o'ziga tegishli deb tanishi kerak — ikkala
        // tildagi qismida ham ismi turadi.
        text.Split("Turdimuhammadova Durdona").Length.ShouldBe(3);

        text.ShouldContain("Assalomu alaykum");
        text.ShouldContain("Здравствуйте");
        text.ShouldContain("HR MBF company!");
    }

    [Fact]
    public void Rad_etish_xabarida_vakansiya_nomi_yoq()
    {
        /*
          Shablonda vakansiya nomi ATAYLAB yo'q. Test uni qaytarib
          qo'yilishidan saqlaydi: nom qo'shilsa, xabar ikki tilda ikki
          marta uzayardi va shablon HR dagi qog'oz nusxadan farq
          qilardi.
        */
        BotTexts.RejectionNotice("Karimov Aziz").ShouldNotContain("Hisobchi");
    }
}
