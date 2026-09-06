using Bonvi.Bot.Conversation;

namespace Bonvi.Bot.Tests;

/// <summary>
/// Botda ko'rsatiladigan vaqt — TOSHKENT vaqti.
/// </summary>
/// <remarks>
/// <para>
/// <b>Haqiqiy xato edi.</b> API sanani UTC da qaytaradi, Toshkent esa
/// undan besh soat OLDINDA. Toshkent yarim tunidan ertalab soat 5
/// gacha UTC hali kechagi kunda turadi va bot «siz 6-sentabrda ariza
/// topshirgansiz» deb yozardi — nomzod esa uni 7-sentabr kechasi
/// topshirgan edi va xabarni «bu men emasman» deb o'qirdi.
/// </para>
/// <para>
/// Test aynan shu chegarani — Toshkent yarim tunidan keyingi vaqtni —
/// qadab turadi.
/// </para>
/// </remarks>
public sealed class BotClockTests
{
    [Fact]
    public void Toshkent_siljishi_besh_soat()
        => BotClock.TashkentOffset.ShouldBe(TimeSpan.FromHours(5));

    [Fact]
    public void Kechqurungi_ariza_bugungi_sana_bilan_chiziladi()
    {
        // UTC bo'yicha 5-sentabr soat 20:30 — Toshkentda 6-sentabr 01:30.
        var appliedAt = new DateTimeOffset(2026, 9, 5, 20, 30, 0, TimeSpan.Zero);

        foreach (var language in Enum.GetValues<BotLanguage>())
        {
            var text = BotTexts.AlreadyApplied(language, "Kassir", appliedAt, stage: null);

            text.ShouldContain("06.09.2026");
            text.ShouldNotContain("05.09.2026");
        }
    }

    [Fact]
    public void Boshqa_siljishdagi_qiymat_ham_togri_chiziladi()
    {
        // Server siljishni o'zgartirsa ham natija bir xil bo'lishi
        // kerak: ahamiyatli narsa LAHZA, uning yozilishi emas.
        var utc = new DateTimeOffset(2026, 9, 5, 20, 30, 0, TimeSpan.Zero);
        var same = utc.ToOffset(TimeSpan.FromHours(3));

        BotTexts.AlreadyApplied(BotLanguage.Latin, "Kassir", same, stage: null)
            .ShouldContain("06.09.2026");
    }

    [Fact]
    public void Hozirgi_vaqt_Toshkent_siljishi_bilan_qaytadi()
        => BotClock.Now.Offset.ShouldBe(BotClock.TashkentOffset);
}
