namespace Bonvi.DeviceAgent.Tests;

/// <summary>
/// Hodisalar halqasi — 2026-09-27 audit topilmalari (3, 4, 9, 10, 14).
/// </summary>
public sealed class EventForwardingAuditTests
{
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Terminal soati orqaga to'g'rilangandan keyin internet 10 daqiqa uzilsa ham
    /// o'tish yo'qolmaydi (topilma 3).
    /// </summary>
    /// <remarks>
    /// Ilgari qidiruv boshi «terminalning hozirgi vaqti − 2 daqiqa» ga
    /// tushardi va uzilishning ikki daqiqadan eski qismi qidiruvdan tashqarida
    /// qolardi.
    /// </remarks>
    [Fact]
    public async Task Soat_orqaga_togrilangach_uzilishda_otish_yoqolmaydi()
    {
        using var stand = new AgentHarness();

        await stand.PassesWithCyclesAsync(3, Step);

        stand.Terminal.ClockOffset = TimeSpan.FromHours(-1);
        await stand.MeasureClockAsync();

        stand.Bonvi.Offline = true;
        await stand.PassesWithCyclesAsync(20, Step, swallowFailures: true);

        stand.Bonvi.Offline = false;
        await stand.ForwardAsync();

        stand.Bonvi.Stored.Count.ShouldBe(23);
        stand.Bonvi.Duplicates.ShouldBe(0);
    }

    /// <summary>Qotirilgan qidiruv boshi agent qayta ishga tushganda ham saqlanadi.</summary>
    [Fact]
    public async Task Soat_orqaga_togrilangach_qayta_ishga_tushsa_ham_otish_yoqolmaydi()
    {
        using var stand = new AgentHarness();

        await stand.PassesWithCyclesAsync(3, Step);

        stand.Terminal.ClockOffset = TimeSpan.FromHours(-1);
        await stand.MeasureClockAsync();

        stand.Bonvi.Offline = true;
        await stand.PassesWithCyclesAsync(10, Step, swallowFailures: true);

        stand.Restart();
        await stand.MeasureClockAsync();
        await stand.PassesWithCyclesAsync(10, Step, swallowFailures: true);

        stand.Bonvi.Offline = false;
        await stand.ForwardAsync();

        stand.Bonvi.Stored.Count.ShouldBe(23);
    }

    /// <summary>
    /// Rad etilgan urinish (ruxsat yo'q, yuz tasdiqlanmadi) o'tish sifatida
    /// YUBORILMAYDI — kursor esa undan o'tadi (topilma 4).
    /// </summary>
    [Fact]
    public async Task Rad_etilgan_urinish_otish_sifatida_yuborilmaydi()
    {
        using var stand = new AgentHarness();

        stand.Clock.Advance(Step);
        stand.Terminal.Pass("900000001", minor: 6);   // ruxsat yo'q
        stand.Terminal.Pass("900000001", minor: 76);  // yuz tasdiqlanmadi
        stand.Terminal.Pass("900000001", minor: 75);  // o'tdi

        await stand.ForwardAsync();
        await stand.ForwardAsync();

        stand.Bonvi.Stored.Count.ShouldBe(1);
        stand.Bonvi.Minors.ShouldBe([75], "kichik tur server tomonidan ham tekshirilishi uchun uzatiladi");
        stand.Bonvi.Duplicates.ShouldBe(0);
    }

    /// <summary>
    /// Holat fayli yo'q agent jurnalni 48 soat orqagacha o'qiydi — bir soat emas (topilma 9).
    /// </summary>
    [Fact]
    public async Task Holat_fayli_yoq_bolsa_48_soatgacha_orqaga_oqiladi()
    {
        using var stand = new AgentHarness();

        stand.Terminal.Pass("900000001");
        stand.Clock.Advance(TimeSpan.FromHours(30));
        stand.Terminal.Pass("900000002");

        await stand.ForwardAsync();

        stand.Bonvi.Stored.Count.ShouldBe(2);
    }

    /// <summary>Oraliq sozlanadi.</summary>
    [Fact]
    public async Task Birinchi_oqish_oraligi_sozlanadi()
    {
        using var stand = new AgentHarness(options => options.ColdStartHours = 2);

        stand.Terminal.Pass("900000001");
        stand.Clock.Advance(TimeSpan.FromHours(30));
        stand.Terminal.Pass("900000002");

        await stand.ForwardAsync();

        stand.Bonvi.Stored.Values.ShouldBe(["900000002"]);
    }

    /// <summary>
    /// Firmware sahifani yangidan eskiga bersa ham hech narsa yo'qolmaydi;
    /// eskidan yangiga tartib aniq so'raladi (topilma 10).
    /// </summary>
    [Fact]
    public async Task Terminal_teskari_tartibda_bersa_ham_hammasi_yetib_boradi()
    {
        using var stand = new AgentHarness();
        stand.Terminal.Descending = true;

        for (var index = 0; index < 700; index++)
        {
            stand.Clock.Advance(TimeSpan.FromMinutes(1));
            stand.Terminal.Pass("900000001");
        }

        await stand.ForwardAsync();

        stand.Bonvi.Stored.Count.ShouldBe(700);
        stand.Bonvi.Duplicates.ShouldBe(0);
        stand.Terminal.LastEventQuery!.ShouldContain("\"timeReverseOrder\":false");
    }

    /// <summary>
    /// Bir lahzadagi bir nechta o'tishdan faqat ENG YANGISI jonli kadr oladi (topilma 14).
    /// </summary>
    /// <remarks>
    /// Ilgari kadr bo'lakdagi har bir yangi o'tishga biriktirilardi — o'nta
    /// o'tishli bo'lak 2 MB chegarasidan oshib, 413 bilan qaytardi.
    /// </remarks>
    [Fact]
    public async Task Bir_lahzadagi_otishlardan_faqat_eng_yangisiga_kadr_biriktiriladi()
    {
        using var stand = new AgentHarness();

        stand.Clock.Advance(Step);
        for (var index = 0; index < 5; index++)
        {
            stand.Terminal.Pass($"90000000{index + 1}");
        }

        await stand.ForwardAsync();

        stand.Bonvi.Stored.Count.ShouldBe(5);
        stand.Bonvi.StoredWithPhoto.Where(entry => entry.Value).Select(entry => entry.Key)
            .ShouldHaveSingleItem()
            .ShouldStartWith("minmoe-5-");
    }
}
