using Bonvi.Bot.Conversation;

namespace Bonvi.Bot.Tests;

/// <summary>
/// Doimiy menyu: tugmalar ro'yxati va ularni tanib olish.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nega bu test kerak.</b> Doimiy menyu tugmasi bosilganda Telegram
/// uning MATNINI oddiy xabar qilib yuboradi. Ya'ni tugma bilan
/// nomzodning yozgani orasidagi farq faqat shu taqqoslashda —
/// yiqilsa, bot tugmani nomzodning ismi deb qabul qilardi.
/// </para>
/// <para>
/// Testlar matnning O'ZINI qadamaydi (u o'zgarishi mumkin), balki
/// BOG'LANISHNI: tugma matni qaysi tilda bo'lsa ham o'z amaliga
/// olib borishini.
/// </para>
/// </remarks>
public sealed class BotMenuTests
{
    public static TheoryData<BotLanguage> AllLanguages =>
        [BotLanguage.Cyrillic, BotLanguage.Latin, BotLanguage.Russian];

    [Theory]
    [MemberData(nameof(AllLanguages))]
    public void Ariza_tugmasi_har_qaysi_tilda_tanildi(BotLanguage language)
        => BotMenu.Resolve(BotTexts.ApplyButton(language)).ShouldBe(MenuAction.Apply);

    [Theory]
    [MemberData(nameof(AllLanguages))]
    public void Kompaniya_tugmasi_har_qaysi_tilda_tanildi(BotLanguage language)
        => BotMenu.Resolve(BotTexts.AboutButton(language)).ShouldBe(MenuAction.About);

    [Theory]
    [MemberData(nameof(AllLanguages))]
    public void Kontakt_tugmasi_har_qaysi_tilda_tanildi(BotLanguage language)
        => BotMenu.Resolve(BotTexts.ContactsButton(language)).ShouldBe(MenuAction.Contacts);

    [Fact]
    public void Til_tugmasi_tanildi()
        => BotMenu.Resolve(BotTexts.LanguageButton).ShouldBe(MenuAction.Language);

    [Fact]
    public void Tugma_boshqa_tilda_bosilsa_ham_ishlaydi()
    {
        /*
          Nomzod tilni lotinga o'zgartirgan, lekin ekranida kirillcha
          tugma qolib ketgan (bot qayta ishga tushgan, suhbat
          unutilgan). Tugma baribir ishlashi kerak — aks holda odam
          «bot buzuq» deb o'ylardi.
        */
        BotMenu.Resolve(BotTexts.ApplyButton(BotLanguage.Cyrillic)).ShouldBe(MenuAction.Apply);
        BotMenu.Resolve(BotTexts.ApplyButton(BotLanguage.Russian)).ShouldBe(MenuAction.Apply);
    }

    [Theory]
    [InlineData("Каримов Азиз Акмал ўғли")]
    [InlineData("ariza")]
    [InlineData("Ariza topshirish")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Oddiy_matn_menyu_deb_qabul_qilinmaydi(string? text)
    {
        // «Ariza topshirish» emoji'siz — bu tugma EMAS. Aks holda
        // shunday ism yozgan odam menyuga tashlab yuborilardi.
        BotMenu.Resolve(text).ShouldBe(MenuAction.None);
    }

    [Fact]
    public void Tugma_atrofidagi_boshliq_ahamiyatsiz()
    {
        // Ba'zi klientlar matnni bo'shliq bilan yuboradi.
        BotMenu.Resolve("  " + BotTexts.LanguageButton + "  ").ShouldBe(MenuAction.Language);
    }

    [Fact]
    public void Sozlanmagan_bolim_tugmasi_chizilmaydi()
    {
        var labels = BotMenu.Labels(BotLanguage.Latin, hasAbout: false, hasContacts: false);

        labels.ShouldBe([BotTexts.ApplyButton(BotLanguage.Latin), BotTexts.LanguageButton]);
    }

    [Fact]
    public void Sozlangan_bolimlar_tartib_bilan_qoshiladi()
    {
        var labels = BotMenu.Labels(BotLanguage.Cyrillic, hasAbout: true, hasContacts: true);

        labels.ShouldBe([
            BotTexts.ApplyButton(BotLanguage.Cyrillic),
            BotTexts.AboutButton(BotLanguage.Cyrillic),
            BotTexts.ContactsButton(BotLanguage.Cyrillic),
            BotTexts.LanguageButton,
        ]);
    }

    [Fact]
    public void Har_bir_tugma_ozining_amaliga_olib_boradi()
    {
        /*
          Ikki tugmaning matni bir xil bo'lib qolsa, biri ikkinchisini
          «yeb» qo'yardi va nosozlik faqat nomzodda ko'rinardi.
        */
        foreach (var language in new[] { BotLanguage.Cyrillic, BotLanguage.Latin, BotLanguage.Russian })
        {
            var labels = BotMenu.Labels(language, hasAbout: true, hasContacts: true);

            labels.Distinct(StringComparer.Ordinal).Count().ShouldBe(labels.Count);
            labels.Select(BotMenu.Resolve).ShouldNotContain(MenuAction.None);
        }
    }
}
