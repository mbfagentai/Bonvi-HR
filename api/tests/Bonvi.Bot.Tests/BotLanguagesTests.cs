using Bonvi.Bot.Conversation;

namespace Bonvi.Bot.Tests;

/// <summary>
/// Til va tizimdagi kodi orasidagi moslik.
/// </summary>
/// <remarks>
/// <b>Nega test kerak.</b> Kod nomzod bilan birga BAZAGA yoziladi va
/// undan qaytib o'qiladi. Yozishda bir shakl, o'qishda boshqasi
/// ishlatilsa, rad etilgan ruscha nomzod kirillcha xabar olardi va bu
/// faqat nomzodda ko'rinardi.
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
    public void Rad_etish_xabari_uchala_tilda_ham_boshqacha()
    {
        /*
          Yangi til qo'shilganda `_ =>` tarmog'i uni jimgina lotincha
          matn bilan qoldirib ketishi mumkin — kompilyator bunday
          xatoni ushlamaydi (`BotLanguage` izohidagi ogohlantirish).
        */
        var texts = new[] { BotLanguage.Cyrillic, BotLanguage.Latin, BotLanguage.Russian }
            .Select(language => BotTexts.RejectionNotice(language, "Каримов Азиз", "Ҳисобчи"))
            .ToList();

        texts.Distinct(StringComparer.Ordinal).Count().ShouldBe(3);
    }

    [Fact]
    public void Rad_etish_xabarida_ism_va_vakansiya_bor()
    {
        var text = BotTexts.RejectionNotice(BotLanguage.Latin, "Karimov Aziz", "Hisobchi");

        // Nomzod xabarni o'ziga tegishli deb tanishi kerak.
        text.ShouldContain("Karimov Aziz");
        text.ShouldContain("Hisobchi");
    }
}
