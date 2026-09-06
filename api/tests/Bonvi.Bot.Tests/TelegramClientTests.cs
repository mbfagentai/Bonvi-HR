using Bonvi.Bot.Telegram;

namespace Bonvi.Bot.Tests;

/// <summary>
/// Telegram chaqiruvining manzili.
/// </summary>
/// <remarks>
/// <para>
/// Butun sinfda test qilishga arziydigan yagona sof funksiya shu — qolgani
/// tarmoqqa chiqadi. Lekin aynan shu yerda haqiqiy nosozlik bo'ldi va u
/// ishga tushirilgunicha ko'rinmadi.
/// </para>
/// <para>
/// <b>Nima bo'lgandi.</b> Token endi ish paytida almashadi, shu sababli u
/// <c>HttpClient.BaseAddress</c> dan olinib, har chaqiruvda yig'iladigan
/// bo'ldi. Yo'l <c>$"bot{token}/{method}"</c> deb yozilgandi — token ichida
/// esa IKKI NUQTA bor (<c>8123456789:AAH…</c>) va <c>Uri</c> uni sxema
/// ajratgichi deb o'qidi: <c>bot8123456789</c> protokol nomiga aylanib,
/// har bir chaqiruv istisno bilan yiqildi.
/// </para>
/// <para>
/// Yechim bitta belgi — boshidagi qiya chiziq. Bu test aynan shuni qadab
/// turadi: u olib tashlansa, testlar ishga tushirishdan OLDIN yiqiladi.
/// </para>
/// </remarks>
public sealed class TelegramClientTests
{
    /// <summary>Haqiqiy ko'rinishdagi, lekin O'YLAB TOPILGAN token.</summary>
    private const string Token = "8123456789:AAHtestTOKENqiymati_1234567890abcd";

    [Fact]
    public void Ikki_nuqtali_token_bilan_ham_nisbiy_yol_yasaladi()
    {
        var path = TelegramClient.RelativePath(Token, "getUpdates");

        path.IsAbsoluteUri.ShouldBeFalse("token ichidagi «:» sxema deb o'qilmasligi kerak");
        path.OriginalString.ShouldBe("/bot8123456789:AAHtestTOKENqiymati_1234567890abcd/getUpdates");
    }

    /// <summary>
    /// Manzil bazasi bilan qo'shilganda to'g'ri to'liq manzil chiqadi.
    /// </summary>
    /// <remarks>
    /// Nisbiy yo'lning o'zi to'g'ri bo'lishi yetarli emas: boshidagi qiya
    /// chiziq «ildizdan» degani va manzil bazasida yo'l bo'lsa, u kesib
    /// tashlanardi. Bu test ikkalasini BIRGA tekshiradi.
    /// </remarks>
    [Fact]
    public void Manzil_bazasi_bilan_toliq_manzil_togri_chiqadi()
    {
        var baseAddress = new Uri("https://api.telegram.org/");

        var full = new Uri(baseAddress, TelegramClient.RelativePath(Token, "sendMessage"));

        full.ToString().ShouldBe(
            "https://api.telegram.org/bot8123456789:AAHtestTOKENqiymati_1234567890abcd/sendMessage");
    }

    /// <summary>
    /// Fayl BOSHQA manzildan olinadi.
    /// </summary>
    /// <remarks>
    /// Nomzodning surati (18-qadam) ikkita so'rov bilan olinadi:
    /// <c>getFile</c> yo'lni aytadi, fayl esa <c>/file/bot…</c> dan
    /// yuklanadi. Ikkalasini adashtirish oson va natijasi bir xil —
    /// 404, ya'ni rasmsiz nomzod.
    /// </remarks>
    [Fact]
    public void Fayl_manzili_alohida_yol_bilan_yasaladi()
    {
        var path = TelegramClient.FilePath(Token, "photos/file_12.jpg");

        path.IsAbsoluteUri.ShouldBeFalse();
        path.OriginalString.ShouldBe(
            "/file/bot8123456789:AAHtestTOKENqiymati_1234567890abcd/photos/file_12.jpg");
    }
}
