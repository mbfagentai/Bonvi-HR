using Bonvi.Application.Attendance.Computation;
using Bonvi.Domain.Attendance;
using static Bonvi.Application.Tests.Attendance.Computation.AttendanceScenario;

namespace Bonvi.Application.Tests.Attendance.Computation;

/// <summary>
/// Juftlash algoritmi — SPEC §5.5, 1–5-qadamlar; §11.1 #35–#38, #46, #47.
/// </summary>
/// <remarks>
/// Jadvalsiz (<c>plan = null</c>) chaqiriladi: bu yerda faqat o'tishlardan
/// oraliq yasash tekshiriladi. Reja va tanaffus bilan birgalikdagi holatlar
/// <see cref="AttendanceDayBuilderTests"/> va <see cref="BreakDeductionTests"/> da.
/// </remarks>
public sealed class PassPairingTests
{
    private static PassReading P(string time, PassDirection direction, DateOnly? date = null)
        => new(Guid.CreateVersion7(), Guid.Empty, At(date ?? Monday, time), direction);

    private static PairingResult Pair(params PassReading[] passes) => PassPairing.Pair(passes, plan: null);

    // ────────────────────── Oraliqlar ──────────────────────

    [Fact]
    public void Togri_navbatlashgan_otishlar_yopilgan_oraliqlar_beradi()
    {
        var natija = Pair(P("09:00", PassDirection.In), P("18:00", PassDirection.Out));

        natija.ClosedMinutes.ShouldBe(540);
        natija.WorkedMinutes.ShouldBe(540);
        natija.MissingEntry.ShouldBeFalse();
        natija.MissingExit.ShouldBeFalse();
    }

    /// <summary>#35. Oxirgisi kirish — ochiq oraliq daqiqa bermaydi.</summary>
    [Fact]
    public void Toq_otishlar_kirish_bilan_tugasa_chiqish_yetishmaydi()
    {
        var natija = Pair(
            P("09:00", PassDirection.In),
            P("13:00", PassDirection.Out),
            P("14:00", PassDirection.In));

        natija.MissingExit.ShouldBeTrue();
        natija.MissingEntry.ShouldBeFalse();
        natija.ClosedMinutes.ShouldBe(240, "faqat yopilgan 09:00–13:00");
    }

    /// <summary>#36. Birinchisi chiqish — yetim chiqish daqiqa bermaydi.</summary>
    [Fact]
    public void Birinchi_otish_chiqish_bolsa_kirish_yetishmaydi()
    {
        var natija = Pair(
            P("08:50", PassDirection.Out),
            P("09:00", PassDirection.In),
            P("18:00", PassDirection.Out));

        natija.MissingEntry.ShouldBeTrue();
        natija.MissingExit.ShouldBeFalse();
        natija.ClosedMinutes.ShouldBe(540);
        natija.LastOutAt.ShouldBe(At(Monday, "18:00"));
    }

    /// <summary>#37. O'nta o'tish — bu anomaliya emas, oddiy kun: beshta oraliq qo'shiladi.</summary>
    [Fact]
    public void On_ta_otish_besh_oraliq_bolib_qoshiladi()
    {
        var natija = Pair(
            P("08:00", PassDirection.In), P("09:00", PassDirection.Out),
            P("10:00", PassDirection.In), P("11:00", PassDirection.Out),
            P("12:00", PassDirection.In), P("13:00", PassDirection.Out),
            P("14:00", PassDirection.In), P("15:00", PassDirection.Out),
            P("16:00", PassDirection.In), P("17:30", PassDirection.Out));

        natija.ClosedMinutes.ShouldBe(330);
        natija.PassCount.ShouldBe(10);
        natija.IgnoredPassCount.ShouldBe(0);
    }

    /// <summary>
    /// Ochiq oraliq ustiga yana kirish: oldingi oraliqning chiqishi yozilmagan.
    /// U daqiqa bermaydi va belgilanadi — biz bilmagan 30 daqiqa «ishlangan» bo'lmaydi.
    /// </summary>
    [Fact]
    public void Ochiq_oraliq_ustiga_kirish_oldingisini_chiqishsiz_deb_belgilaydi()
    {
        var natija = Pair(
            P("09:00", PassDirection.In),
            P("09:30", PassDirection.In),
            P("18:00", PassDirection.Out));

        natija.MissingExit.ShouldBeTrue();
        natija.ClosedMinutes.ShouldBe(510, "oraliq ikkinchi kirishdan hisoblanadi");
        natija.FirstInAt.ShouldBe(At(Monday, "09:00"), "birinchi kirish baribir birinchi kirish");
        natija.IgnoredPassCount.ShouldBe(0, "30 daqiqa — sakrash emas");
    }

    /// <summary>SPEC §5.5, 5-qadam: 7 soat 59 daqiqa 30 soniya — 479, yuvarlanmaydi.</summary>
    [Fact]
    public void Daqiqalar_yuvarlanmaydi_kesiladi()
    {
        var natija = Pair(P("09:00:00", PassDirection.In), P("16:59:30", PassDirection.Out));

        natija.ClosedMinutes.ShouldBe(479);
    }

    [Fact]
    public void Soniyalar_avval_qoshilib_keyin_kesiladi()
    {
        // Ikki oraliq: 30 daq 30 s + 29 daq 30 s = roppa-rosa 60 daqiqa.
        // Har birini alohida kesish 59 berardi.
        var natija = Pair(
            P("09:00:00", PassDirection.In), P("09:30:30", PassDirection.Out),
            P("10:00:00", PassDirection.In), P("10:29:30", PassDirection.Out));

        natija.ClosedMinutes.ShouldBe(60);
    }

    [Fact]
    public void Kirish_tartibi_lahza_boyicha_berilgan_tartibga_bogliq_emas()
    {
        var natija = Pair(P("18:00", PassDirection.Out), P("09:00", PassDirection.In));

        natija.ClosedMinutes.ShouldBe(540);
        natija.MissingEntry.ShouldBeFalse();
    }

    [Fact]
    public void Lahzalar_UTC_da_qaytadi()
    {
        var natija = Pair(P("09:00", PassDirection.In), P("18:00", PassDirection.Out));

        natija.FirstInAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
        natija.LastOutAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
        natija.FirstInAt.Value.ShouldBe(At(Monday, "09:00"), "lahzaning o'zi o'zgarmaydi");
    }

    // ────────────────────── Sakrashlar (#38) ──────────────────────

    [Fact]
    public void Yigirma_soniya_ichidagi_ikki_kirish_bitta_hisoblanadi()
    {
        var natija = Pair(
            P("09:00:00", PassDirection.In),
            P("09:00:20", PassDirection.In),
            P("18:00:00", PassDirection.Out));

        natija.PassCount.ShouldBe(3, "sakrash ham kunga tegishli o'tish sifatida sanaladi");
        natija.IgnoredPassCount.ShouldBe(1);
        natija.MissingExit.ShouldBeFalse();
        natija.ClosedMinutes.ShouldBe(540);
    }

    [Fact]
    public void Yigirma_daqiqa_farqli_ikki_kirish_ikkalasi_ham_qoladi()
    {
        var natija = Pair(
            P("09:00", PassDirection.In),
            P("09:20", PassDirection.In),
            P("18:00", PassDirection.Out));

        natija.IgnoredPassCount.ShouldBe(0);
    }

    [Fact]
    public void Sakrash_chegarasi_oltmish_soniyani_oz_ichiga_oladi()
    {
        Pair(P("09:00:00", PassDirection.In), P("09:01:00", PassDirection.In))
            .IgnoredPassCount.ShouldBe(1, "roppa-rosa 60 s — hali bitta o'tish");

        Pair(P("09:00:00", PassDirection.In), P("09:01:01", PassDirection.In))
            .IgnoredPassCount.ShouldBe(0);
    }

    /// <summary>
    /// Solishtirish oxirgi SAQLANGAN bilan: 50 soniyalik zanjir ikki daqiqani
    /// «bitta o'tish» qilib yubormaydi.
    /// </summary>
    [Fact]
    public void Sakrash_oxirgi_saqlangan_otish_bilan_solishtiriladi()
    {
        var natija = Pair(
            P("09:00:00", PassDirection.In),
            P("09:00:50", PassDirection.In),
            P("09:01:40", PassDirection.In));

        natija.IgnoredPassCount.ShouldBe(1);
    }

    [Fact]
    public void Turli_yonalishdagi_yaqin_otishlar_sakrash_emas()
    {
        var natija = Pair(P("09:00:00", PassDirection.In), P("09:00:30", PassDirection.Out));

        natija.IgnoredPassCount.ShouldBe(0);
    }

    [Fact]
    public void Nomalum_yonalish_hech_narsani_yutmaydi()
    {
        var natija = Pair(P("09:00:00", PassDirection.Unknown), P("09:00:20", PassDirection.Unknown));

        natija.IgnoredPassCount.ShouldBe(0);
        natija.MissingExit.ShouldBeFalse("navbat bilan kirish va chiqish bo'ldi");
    }

    // ────────────────────── Noma'lum yo'nalish (#46, #47) ──────────────────────

    /// <summary>#46. Yo'nalish aytmaydigan qurilma: kirish, chiqish, kirish, chiqish.</summary>
    [Fact]
    public void Nomalum_yonalishlar_navbat_bilan_toldiriladi()
    {
        var natija = Pair(
            P("09:00", PassDirection.Unknown),
            P("13:00", PassDirection.Unknown),
            P("14:00", PassDirection.Unknown),
            P("18:00", PassDirection.Unknown));

        natija.ClosedMinutes.ShouldBe(480);
        natija.FirstInAt.ShouldBe(At(Monday, "09:00"));
        natija.LastOutAt.ShouldBe(At(Monday, "18:00"));
        natija.MissingEntry.ShouldBeFalse();
        natija.MissingExit.ShouldBeFalse();
    }

    /// <summary>#46. Taxmin o'tishga QAYTA YOZILMAYDI — jadval tuzatilsa tarix o'zgarmasligi kerak.</summary>
    [Fact]
    public void Taxmin_qilingan_yonalish_otishga_yozilmaydi()
    {
        string[] vaqtlar = ["09:00", "18:00"];

        var otishlar = vaqtlar
            .Select(time => AttendancePass.Record(
                Guid.CreateVersion7(),
                AttendanceIdentityProvider.Verifix,
                "1001",
                At(Monday, time),
                At(Monday, time),
                $"kalit-{time}").Value)
            .ToList();

        PassPairing.Pair(otishlar.Select(PassReading.From), plan: null).ClosedMinutes.ShouldBe(540);

        otishlar.ShouldAllBe(pass => pass.Direction == PassDirection.Unknown);
        otishlar.ShouldAllBe(pass => pass.DirectionSource == DirectionSource.Unknown);
    }

    /// <summary>
    /// #47. Ma'lum yo'nalish ustidan yozilmaydi va navbat undan QAYTA boshlanadi:
    /// 10:00 dagi ma'lum kirishdan keyin noma'lum 18:00 — chiqish. Oddiy sanoq
    /// (juft/toq o'rin) uni kirish qilardi.
    /// </summary>
    [Fact]
    public void Malum_yonalish_ustidan_yozilmaydi_va_navbat_undan_boshlanadi()
    {
        var natija = Pair(
            P("09:00", PassDirection.Unknown),
            P("10:00", PassDirection.In),
            P("18:00", PassDirection.Unknown));

        natija.LastOutAt.ShouldBe(At(Monday, "18:00"), "18:00 chiqish deb to'ldirildi");
        natija.MissingExit.ShouldBeTrue("09:00 dagi taxminiy kirish yopilmadi — 10:00 ma'lum kirish");
        natija.ClosedMinutes.ShouldBe(480);
    }

    [Fact]
    public void Malum_chiqishdan_keyin_navbat_kirish_bilan_davom_etadi()
    {
        var natija = Pair(
            P("09:00", PassDirection.Unknown),
            P("13:00", PassDirection.Out),
            P("14:00", PassDirection.Unknown),
            P("18:00", PassDirection.Unknown));

        natija.ClosedMinutes.ShouldBe(480);
        natija.MissingEntry.ShouldBeFalse();
        natija.MissingExit.ShouldBeFalse();
    }
}
