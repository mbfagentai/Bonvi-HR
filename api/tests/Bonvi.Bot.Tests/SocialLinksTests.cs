using Bonvi.Bot.Conversation;

namespace Bonvi.Bot.Tests;

/// <summary>
/// «Kompaniya haqida» ostidagi havolali tugmalar.
/// </summary>
/// <remarks>
/// <b>Nega test kerak.</b> Telegram bo'sh <c>url</c> li tugmani qabul
/// qilmaydi va BUTUN xabarni rad etadi. Ya'ni bitta to'ldirilmagan
/// sozlama «Kompaniya haqida» ni ishlamaydigan qilib qo'yardi va bu
/// faqat nomzodda ko'rinardi — bizda esa hech qanday xato bo'lmasdi.
/// </remarks>
public sealed class SocialLinksTests
{
    private const string Instagram = "https://www.instagram.com/velobonvi";
    private const string Telegram = "https://t.me/VeloBonvi";
    private const string Facebook = "https://www.facebook.com/profile.php?id=61560828833307";
    private const string Website = "https://bonvi.uz/";

    [Fact]
    public void Hech_qaysi_havola_yoq_bolsa_tugma_ham_yoq()
    {
        // `null` qaytishi muhim: bo'sh RO'YXAT yuborilsa, Telegram
        // xabar ostiga bo'sh tugmalar maydonini chizib qo'yardi.
        SocialLinks.Buttons(BotLanguage.Latin, null, null, null, null).ShouldBeNull();
        SocialLinks.Buttons(BotLanguage.Latin, "", "  ", "", null).ShouldBeNull();
    }

    [Fact]
    public void Faqat_toldirilgan_havola_tugma_boladi()
    {
        var rows = SocialLinks.Buttons(BotLanguage.Latin, Instagram, null, null, Website);

        rows.ShouldNotBeNull();

        var buttons = rows.SelectMany(row => row).ToList();

        buttons.Count.ShouldBe(2);
        buttons.ShouldAllBe(button => button.Url != null && button.Url.Length > 0);
        buttons[0].Text.ShouldBe(BotTexts.InstagramButton);
        buttons[1].Text.ShouldBe(BotTexts.WebsiteButton(BotLanguage.Latin));
    }

    [Fact]
    public void Tortta_havola_ikkitalab_qatorga_boladi()
    {
        var rows = SocialLinks.Buttons(BotLanguage.Cyrillic, Instagram, Telegram, Facebook, Website);

        rows.ShouldNotBeNull();
        rows.Count.ShouldBe(2);
        rows.ShouldAllBe(row => row.Count == 2);
    }

    [Fact]
    public void Uchta_havolada_oxirgi_qator_yolgiz_qoladi()
    {
        var rows = SocialLinks.Buttons(BotLanguage.Latin, Instagram, Telegram, Facebook, null);

        rows.ShouldNotBeNull();
        rows.Count.ShouldBe(2);
        rows[0].Count.ShouldBe(2);
        rows[1].Count.ShouldBe(1);
    }

    [Fact]
    public void Havoladagi_ortiqcha_boshliq_kesiladi()
    {
        // HR havolani nusxa olganda oldiga yoki oxiriga bo'shliq
        // qo'shilib qolishi odatiy hol. Telegram esa bunday havolani
        // rad etardi.
        var rows = SocialLinks.Buttons(BotLanguage.Latin, "  " + Instagram + "  ", null, null, null);

        rows.ShouldNotBeNull();
        rows[0][0].Url.ShouldBe(Instagram);
    }

    [Fact]
    public void Havolali_tugmada_amal_belgisi_yoq()
    {
        /*
          Telegram `url` va `callback_data` ni BIRGA qabul qilmaydi.
          Ikkalasi ham berilsa, xabar rad etilardi.
        */
        var rows = SocialLinks.Buttons(BotLanguage.Latin, Instagram, null, null, null);

        rows!.SelectMany(row => row).ShouldAllBe(button => button.CallbackData == null);
    }

    [Fact]
    public void Sayt_tugmasi_tarjima_qilinadi_brendlar_esa_yoq()
    {
        var latin = SocialLinks.Buttons(BotLanguage.Latin, Instagram, null, null, Website)!;
        var russian = SocialLinks.Buttons(BotLanguage.Russian, Instagram, null, null, Website)!;

        // Brend nomi uchala tilda bir xil.
        latin[0][0].Text.ShouldBe(russian[0][0].Text);

        // «Sayt» esa tarjima qilinadi.
        latin[0][1].Text.ShouldNotBe(russian[0][1].Text);
    }
}
