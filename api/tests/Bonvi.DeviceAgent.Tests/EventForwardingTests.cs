using System.Text.Json;
using Bonvi.DeviceAgent.Hikvision;

namespace Bonvi.DeviceAgent.Tests;

/// <summary>
/// O'tishlar terminaldan Bonvi ga YO'QOTISHSIZ yetib boradimi.
/// </summary>
/// <remarks>
/// <para>
/// Egasining savoli (2026-09-26): «internet bo'lmay qolsa, o'tishlar
/// terminalda saqlanib turadimi va aloqa qaytgach serverga yuklanadimi?»
/// Kodni o'qiganda uchta teshik topildi va bu testlar aynan ularni
/// qadab turadi:
/// </para>
/// <list type="number">
///   <item>sahifa oxiri «so'ralgan 50 tadan kam keldi» bilan aniqlanardi —
///         firmware sahifaga 30 tadan bersa, uzilishdan keyin faqat
///         birinchi 30 ta ketardi, qolgani jimgina tashlanardi;</item>
///   <item>butun o'qishga 8 soniya berilardi — bir necha kunlik qoldiq
///         bu vaqtga sig'masdi va agent abadiy aylanardi;</item>
///   <item>qidiruv oralig'i agent soatida edi — terminal soati bir
///         daqiqadan ko'proq orqada bo'lsa, uzilishsiz ham o'tish
///         yo'qolardi.</item>
/// </list>
/// </remarks>
public sealed class EventForwardingTests
{
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Uzilishdan_keyin_30_tadan_ortiq_otish_hammasi_yetib_boradi()
    {
        using var stand = new AgentHarness();

        stand.Terminal.Pass("900000001");
        await stand.ForwardAsync();

        stand.Bonvi.Offline = true;

        for (var index = 0; index < 95; index++)
        {
            stand.Clock.Advance(TimeSpan.FromSeconds(20));
            stand.Terminal.Pass("900000002");
        }

        // Aloqa yo'q — agent urinadi, lekin kursorni SURMAYDI
        await Should.ThrowAsync<HttpRequestException>(stand.ForwardAsync);

        stand.Bonvi.Offline = false;
        await stand.ForwardAsync();

        stand.Bonvi.Stored.Count.ShouldBe(96);
        stand.Bonvi.Duplicates.ShouldBe(0);
    }

    [Fact]
    public async Task Terminal_sahifa_holatini_bermasa_ham_hammasi_oqiladi()
    {
        using var stand = new AgentHarness();
        stand.Terminal.ReportsSearchStatus = false;

        stand.Terminal.Pass("900000001");
        await stand.ForwardAsync();

        for (var index = 0; index < 70; index++)
        {
            stand.Clock.Advance(TimeSpan.FromSeconds(20));
            stand.Terminal.Pass("900000002");
        }

        await stand.ForwardAsync();

        stand.Bonvi.Stored.Count.ShouldBe(71);
    }

    /// <summary>
    /// Uch kunlik qoldiq bo'laklab ketadi va har bo'lak saqlanadi.
    /// </summary>
    /// <remarks>
    /// O'rtada terminal ham uziladi: keyingi urinish (agent qayta ishga
    /// tushgandan keyin ham) BOSHIDAN emas, to'xtagan joyidan davom etadi.
    /// </remarks>
    [Fact]
    public async Task Katta_qoldiq_bolaklab_ketadi_va_uzilgan_joydan_davom_etadi()
    {
        using var stand = new AgentHarness();

        stand.Terminal.Pass("900000001");
        await stand.ForwardAsync();

        for (var index = 0; index < 1000; index++)
        {
            stand.Clock.Advance(TimeSpan.FromMinutes(4));
            stand.Terminal.Pass("900000002");
        }

        stand.Terminal.FailFromEventRequest = stand.Terminal.EventRequests + 25;
        await Should.ThrowAsync<HttpRequestException>(stand.ForwardAsync);

        var partial = stand.Bonvi.Stored.Count;
        partial.ShouldBeGreaterThanOrEqualTo(AgentWorker.MaxEventsPerBatch + 1, "kamida bitta bo'lak saqlangan bo'lishi kerak");
        partial.ShouldBeLessThan(1001);

        stand.Terminal.FailFromEventRequest = null;
        stand.Restart();
        await stand.ForwardAsync();

        stand.Bonvi.Stored.Count.ShouldBe(1001);
        stand.Bonvi.Duplicates.ShouldBe(0);
    }

    [Fact]
    public async Task Terminal_soati_orqada_bolsa_ham_otish_yoqolmaydi()
    {
        using var stand = new AgentHarness();
        stand.Terminal.ClockOffset = TimeSpan.FromMinutes(-5);

        await stand.PassesWithCyclesAsync(10, Step);

        stand.Bonvi.Stored.Count.ShouldBe(10);
        stand.Bonvi.Duplicates.ShouldBe(0);
    }

    [Fact]
    public async Task Terminal_soati_oldinda_bolsa_olchovdan_keyin_otish_darhol_keladi()
    {
        using var stand = new AgentHarness();
        stand.Terminal.ClockOffset = TimeSpan.FromMinutes(20);

        await stand.MeasureClockAsync();
        await stand.PassesWithCyclesAsync(5, Step);

        stand.Bonvi.Stored.Count.ShouldBe(5);
    }

    /// <summary>
    /// Soat o'lchanmasa ham oldinda yurgan terminalning o'tishi YO'QOLMAYDI — faqat kechikadi.
    /// </summary>
    [Fact]
    public async Task Terminal_soati_oldinda_va_olchanmagan_bolsa_otish_kechikadi_lekin_yoqolmaydi()
    {
        using var stand = new AgentHarness();
        stand.Terminal.ClockOffset = TimeSpan.FromMinutes(20);

        await stand.PassesWithCyclesAsync(3, Step);
        stand.Bonvi.Stored.Count.ShouldBe(0);

        stand.Clock.Advance(TimeSpan.FromMinutes(20));
        await stand.ForwardAsync();

        stand.Bonvi.Stored.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Terminal_soati_orqaga_togrilansa_keyingi_otishlar_yoqolmaydi()
    {
        using var stand = new AgentHarness();

        await stand.PassesWithCyclesAsync(3, Step);

        stand.Terminal.ClockOffset = TimeSpan.FromHours(-1);
        await stand.MeasureClockAsync();

        await stand.PassesWithCyclesAsync(3, Step);

        stand.Bonvi.Stored.Count.ShouldBe(6);
    }

    [Fact]
    public async Task Jurnal_tozalanib_raqamlar_qayta_boshlansa_ham_otish_keladi()
    {
        using var stand = new AgentHarness();

        await stand.PassesWithCyclesAsync(5, Step);

        stand.Terminal.ClearLog();
        await stand.PassesWithCyclesAsync(3, Step);

        stand.Bonvi.Stored.Count.ShouldBe(8);
    }

    /// <summary>
    /// Buzilgan soatli (2000 yoki kelgusi yil) hodisa yuborilmaydi va keyingilarini tiqib qo'ymaydi.
    /// </summary>
    /// <remarks>
    /// Server bunday hodisani rad etadi va bitta shunday hodisa BUTUN
    /// so'rovni yiqitadi. Agent uni yuborganida bo'lak cheksiz qayta
    /// yuborilib, soat tuzatilgandan keyingi o'tishlar ham hech qachon
    /// yetib bormasdi.
    /// </remarks>
    [Theory]
    [InlineData(-9500)]
    [InlineData(365)]
    public async Task Buzilgan_soatli_hodisa_keyingi_otishlarni_tiqib_qoymaydi(int days)
    {
        using var stand = new AgentHarness();

        await stand.PassesWithCyclesAsync(1, Step);

        stand.Terminal.ClockOffset = TimeSpan.FromDays(days);
        await stand.MeasureClockAsync();
        await stand.PassesWithCyclesAsync(2, Step);

        stand.Bonvi.RejectedRequests.ShouldBe(0);
        stand.Bonvi.Stored.Count.ShouldBe(1);

        stand.Terminal.ClockOffset = TimeSpan.Zero;
        await stand.MeasureClockAsync();
        await stand.PassesWithCyclesAsync(2, Step);

        stand.Bonvi.Stored.Count.ShouldBe(3);
    }

    /// <summary>
    /// Eski holat fayli (faqat tartib raqami va agent vaqti) bilan yangi agent qayta yubormaydi.
    /// </summary>
    [Fact]
    public async Task Eski_holat_faylidan_otishda_takror_yuborilmaydi()
    {
        using var stand = new AgentHarness();

        for (var index = 0; index < 5; index++)
        {
            stand.Clock.Advance(Step);
            stand.Terminal.Pass("900000001");
        }

        await File.WriteAllTextAsync(
            stand.StatePath,
            JsonSerializer.Serialize(new { LastSerialNo = 5L, LastReadAt = stand.Clock.GetUtcNow() }),
            TestContext.Current.CancellationToken);

        stand.Restart();
        await stand.PassesWithCyclesAsync(1, Step);

        stand.Bonvi.Stored.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Odamlar_royxati_30_tadan_ortiq_bolsa_ham_toliq_oqiladi()
    {
        using var stand = new AgentHarness();
        stand.Terminal.Persons.AddRange(Enumerable.Range(1, 75).Select(index => $"9{index:D8}"));

        var people = await stand.Device.ListPersonsAsync(TestContext.Current.CancellationToken);

        people.Count.ShouldBe(75);
        people.Select(person => person.EmployeeNo).Distinct().Count().ShouldBe(75);
    }

    [Theory]
    [InlineData("MORE", 100, 30, true)]
    [InlineData("OK", 100, 30, false)]
    [InlineData("NO MATCH", null, 0, false)]
    [InlineData(null, 100, 30, true)]
    [InlineData(null, 100, 100, false)]
    [InlineData(null, null, 30, true)]
    public void Sahifa_oxirini_terminal_javobi_aytadi_son_emas(string? status, int? total, int read, bool expected)
        => HikvisionClient.HasMore(status, total, read).ShouldBe(expected);
}
