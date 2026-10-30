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

    // ────────────────────── O'tishlar ro'yxati uchun yo'nalish ──────────────────────

    /// <summary>
    /// Yo'nalishi berilmagan terminal (2026-10-12): ro'yxatda har bir o'tish navbat bilan
    /// kirish/chiqish bo'lib ko'rinadi; tartibsiz kelsa ham vaqt bo'yicha sanaladi.
    /// </summary>
    [Fact]
    public void EffectiveDirections_noma_lumni_navbat_bilan_toldiradi()
    {
        var a = P("09:00", PassDirection.Unknown);
        var b = P("13:00", PassDirection.Unknown);
        var c = P("14:00", PassDirection.Unknown);
        var d = P("18:00", PassDirection.Unknown);

        var natija = PassPairing.EffectiveDirections([d, b, a, c]);

        natija[a.Id].ShouldBe(PassDirection.In);
        natija[b.Id].ShouldBe(PassDirection.Out);
        natija[c.Id].ShouldBe(PassDirection.In);
        natija[d.Id].ShouldBe(PassDirection.Out);
    }

    [Fact]
    public void EffectiveDirections_malum_yonalishni_ozgartirmaydi_va_navbatni_undan_davom_ettiradi()
    {
        var kirish = P("09:00", PassDirection.In);
        var takror = P("09:01", PassDirection.In);
        var noma1 = P("12:00", PassDirection.Unknown);
        var chiqish = P("13:00", PassDirection.Out);
        var noma2 = P("14:00", PassDirection.Unknown);

        var natija = PassPairing.EffectiveDirections([kirish, takror, noma1, chiqish, noma2]);

        natija[kirish.Id].ShouldBe(PassDirection.In);
        natija[takror.Id].ShouldBe(PassDirection.In, "tashlangan takror o'z yo'nalishida qoladi");
        natija[noma1.Id].ShouldBe(PassDirection.Out);
        natija[chiqish.Id].ShouldBe(PassDirection.Out);
        natija[noma2.Id].ShouldBe(PassDirection.In);
    }

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
    /// Egasining qarori (2026-09-27): yo'nalishi MA'LUM ikki kirish ketma-ket bo'la olmaydi —
    /// chiqishsiz takror kirish birinchisining nusxasi. Ilgari bu «ochiq oraliq ustiga kirish»
    /// deb <c>MissingExit</c> bo'lardi va oraliq 09:30 dan boshlanardi; endi oraliq 09:00 dan.
    /// </summary>
    [Fact]
    public void Chiqishsiz_takror_kirish_birinchi_kirishning_nusxasi()
    {
        var natija = Pair(
            P("09:00", PassDirection.In),
            P("09:30", PassDirection.In),
            P("18:00", PassDirection.Out));

        natija.MissingExit.ShouldBeFalse();
        natija.ClosedMinutes.ShouldBe(540, "oraliq BIRINCHI kirishdan");
        natija.FirstInAt.ShouldBe(At(Monday, "09:00"));
        natija.IgnoredPassCount.ShouldBe(1, "takror kirish jurnalda qoladi, juftlashda tashlanadi");
        natija.PassCount.ShouldBe(3);
    }

    /// <summary>
    /// Aralash yo'nalish (egasi o'zgartirmagan, ochiq savol): navbat bilan kirish deb taxmin
    /// qilingan o'tishdan keyin MA'LUM kirish — oldingi oraliqning chiqishi yozilmagan. U daqiqa
    /// bermaydi va belgilanadi — biz bilmagan 30 daqiqa «ishlangan» bo'lmaydi.
    /// </summary>
    [Fact]
    public void Aralash_yonalishda_ochiq_oraliq_ustiga_kirish_oldingisini_chiqishsiz_deb_belgilaydi()
    {
        var natija = Pair(
            P("09:00", PassDirection.Unknown),
            P("09:30", PassDirection.In),
            P("18:00", PassDirection.Out));

        natija.MissingExit.ShouldBeTrue();
        natija.ClosedMinutes.ShouldBe(510, "oraliq ma'lum kirishdan hisoblanadi");
        natija.FirstInAt.ShouldBe(At(Monday, "09:00"), "birinchi kirish baribir birinchi kirish");
        natija.IgnoredPassCount.ShouldBe(0, "noma'lum yo'nalish hech narsani yutmaydi va yutilmaydi");
    }

    /// <summary>
    /// Egasining qarori (2026-09-27): kirishsiz takror CHIQISH oldingisining o'rnini oladi —
    /// odam OXIRGI chiqishda ketgan. Ilgari ikkinchi chiqish yetim bo'lib <c>MissingEntry</c> berardi.
    /// </summary>
    [Fact]
    public void Kirishsiz_takror_chiqish_oxirgisi_yutadi()
    {
        var natija = Pair(
            P("09:00", PassDirection.In),
            P("17:00", PassDirection.Out),
            P("18:05", PassDirection.Out));

        natija.MissingEntry.ShouldBeFalse();
        natija.ClosedMinutes.ShouldBe(545, "09:00–18:05");
        natija.LastOutAt.ShouldBe(At(Monday, "18:05"));
        natija.IgnoredPassCount.ShouldBe(1);
    }

    /// <summary>Kunning boshidagi yetim chiqishlar ham oxirgisiga qisqaradi — lekin yetimligicha qoladi.</summary>
    [Fact]
    public void Kirishdan_oldingi_takror_chiqishlar_bitta_yetim_chiqish()
    {
        var natija = Pair(
            P("08:00", PassDirection.Out),
            P("08:30", PassDirection.Out),
            P("09:00", PassDirection.In),
            P("18:00", PassDirection.Out));

        natija.MissingEntry.ShouldBeTrue("kirishsiz chiqish baribir yetim");
        natija.IgnoredPassCount.ShouldBe(1);
        natija.ClosedMinutes.ShouldBe(540);
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

    // ────────────────────── Takrorlar (#38; egasining qarori, 2026-09-27) ──────────────────────

    /// <summary>§5.7: 20 soniya farqli ikki CHIQISH — oldingisi tashlanadi, oxirgisi qoladi.</summary>
    [Fact]
    public void Yigirma_soniya_ichidagi_ikki_chiqishdan_oxirgisi_qoladi()
    {
        var natija = Pair(
            P("09:00:00", PassDirection.In),
            P("18:00:00", PassDirection.Out),
            P("18:00:20", PassDirection.Out));

        natija.IgnoredPassCount.ShouldBe(1);
        natija.LastOutAt.ShouldBe(At(Monday, "18:00:20"));
        natija.MissingEntry.ShouldBeFalse();
    }

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

    /// <summary>
    /// Egasining qarori (2026-09-27): 60 soniyalik chegara endi yo'q — 20 daqiqa farqli ikki
    /// ma'lum kirish ham bitta (ilgari ikkalasi qolardi).
    /// </summary>
    [Fact]
    public void Yigirma_daqiqa_farqli_ikki_kirish_ham_bitta_hisoblanadi()
    {
        var natija = Pair(
            P("09:00", PassDirection.In),
            P("09:20", PassDirection.In),
            P("18:00", PassDirection.Out));

        natija.IgnoredPassCount.ShouldBe(1);
        natija.ClosedMinutes.ShouldBe(540);
        natija.MissingExit.ShouldBeFalse();
    }

    /// <summary>
    /// Egasining qarori (2026-09-27): ma'lum yo'nalishli takrorda vaqt chegarasi yo'q — 60 soniya,
    /// 61 soniya va uch soat bir xil (ilgari 61 soniya ikki o'tish edi).
    /// </summary>
    [Fact]
    public void Malum_yonalishli_takror_uchun_vaqt_chegarasi_yoq()
    {
        Pair(P("09:00:00", PassDirection.In), P("09:01:00", PassDirection.In))
            .IgnoredPassCount.ShouldBe(1);

        Pair(P("09:00:00", PassDirection.In), P("09:01:01", PassDirection.In))
            .IgnoredPassCount.ShouldBe(1);

        Pair(P("09:00:00", PassDirection.In), P("12:00:00", PassDirection.In))
            .IgnoredPassCount.ShouldBe(1);
    }

    /// <summary>
    /// Egasining qarori (2026-09-27): ketma-ket uchta kirishdan faqat BIRINCHISI qoladi (ilgari
    /// «oxirgi saqlangan bilan 60 soniya» zanjiri uchinchisini alohida o'tish qilardi).
    /// </summary>
    [Fact]
    public void Ketma_ket_uch_kirishdan_faqat_birinchisi_qoladi()
    {
        var natija = Pair(
            P("09:00:00", PassDirection.In),
            P("09:00:50", PassDirection.In),
            P("09:01:40", PassDirection.In));

        natija.IgnoredPassCount.ShouldBe(2);
        natija.FirstInAt.ShouldBe(At(Monday, "09:00"));
    }

    [Fact]
    public void Turli_yonalishdagi_yaqin_otishlar_sakrash_emas()
    {
        var natija = Pair(P("09:00:00", PassDirection.In), P("09:00:30", PassDirection.Out));

        natija.IgnoredPassCount.ShouldBe(0);
    }

    /// <summary>
    /// ⭐ Egasining qarori (2026-09-27), ATAYLAB: bitta umumiy terminal (yo'nalish noma'lum) —
    /// 20 soniya farqli ikki o'qish ham navbat bilan kirish va chiqish bo'laveradi, birlashtirilmaydi.
    /// 2026-10-30 dan faqat 15 soniya ichidagi takror bitta o'tish — 20 s hamon ikkita.
    /// </summary>
    [Fact]
    public void Nomalum_yonalish_hech_narsani_yutmaydi()
    {
        var natija = Pair(P("09:00:00", PassDirection.Unknown), P("09:00:20", PassDirection.Unknown));

        natija.IgnoredPassCount.ShouldBe(0);
        natija.MissingExit.ShouldBeFalse("navbat bilan kirish va chiqish bo'ldi");
    }

    /// <summary>
    /// ⭐ Egasining qarori (2026-10-30): noma'lum terminalda 15 soniya ichidagi takror o'qish — bitta
    /// o'tish. Propan-Rispetto'dagi haqiqiy hol: 08:50:13 va 08:50:16, keyin 18:05 da ketdi.
    /// Ilgari 08:50:16 «chiqish», 18:05 esa «kirish» bo'lib, kun bo'sh qolardi.
    /// </summary>
    [Fact]
    public void Nomalum_terminalda_15_soniyalik_takror_bitta_otish()
    {
        var natija = Pair(
            P("08:50:13", PassDirection.Unknown),
            P("08:50:16", PassDirection.Unknown),
            P("18:05:00", PassDirection.Unknown));

        natija.IgnoredPassCount.ShouldBe(1);
        natija.MissingExit.ShouldBeFalse();
        natija.MissingEntry.ShouldBeFalse();
        natija.WorkedMinutes.ShouldBe(9 * 60 + 14, "08:50:13 dan 18:05 gacha");
    }

    /// <summary>Chegara: aynan 15 soniya — takror; 16 soniya — ikkinchi o'tish.</summary>
    [Fact]
    public void Nomalum_takror_chegarasi_15_soniya()
    {
        Pair(P("09:00:00", PassDirection.Unknown), P("09:00:15", PassDirection.Unknown))
            .IgnoredPassCount.ShouldBe(1);
        Pair(P("09:00:00", PassDirection.Unknown), P("09:00:16", PassDirection.Unknown))
            .IgnoredPassCount.ShouldBe(0);
    }

    /// <summary>Zanjir: har o'qish o'zidan oldingisi bilan solishtiriladi — 0, 10, 20 s bitta o'tish.</summary>
    [Fact]
    public void Nomalum_takror_zanjiri_bitta_otish()
    {
        var natija = Pair(
            P("09:00:00", PassDirection.Unknown),
            P("09:00:10", PassDirection.Unknown),
            P("09:00:20", PassDirection.Unknown));

        natija.IgnoredPassCount.ShouldBe(2);
        natija.MissingExit.ShouldBeTrue("bitta kirish — chiqish yo'q");
    }

    /// <summary>Ma'lum yo'nalish aralashsa 15 soniyalik qoida qo'llanmaydi (telefonda «Kirish», keyin terminal).</summary>
    [Fact]
    public void Malum_va_nomalum_aralashsa_15_soniyalik_qoida_yoq()
    {
        Pair(P("09:00:00", PassDirection.In), P("09:00:05", PassDirection.Unknown))
            .IgnoredPassCount.ShouldBe(0);
    }

    /// <summary>Ro'yxatdagi yo'nalish: tashlangan takror oldingi o'qishning yo'nalishida, «Noma'lum» emas.</summary>
    [Fact]
    public void Samarali_yonalishda_takror_oldingisining_yonalishida()
    {
        var birinchi = P("08:50:13", PassDirection.Unknown);
        var takror = P("08:50:16", PassDirection.Unknown);
        var ketdi = P("18:05:00", PassDirection.Unknown);

        var yonalish = PassPairing.EffectiveDirections([birinchi, takror, ketdi]);

        yonalish[birinchi.Id].ShouldBe(PassDirection.In);
        yonalish[takror.Id].ShouldBe(PassDirection.In);
        yonalish[ketdi.Id].ShouldBe(PassDirection.Out);
    }

    /// <summary>Ochiq oraliq tekshiruvi juftlash bilan bir xil qadamlardan o'tadi.</summary>
    [Fact]
    public void Ochiq_oraliq_tekshiruvi_takror_va_navbatni_hisobga_oladi()
    {
        PassPairing.EndsOpen([]).ShouldBeFalse();
        PassPairing.EndsOpen([P("09:00", PassDirection.Unknown)]).ShouldBeTrue();
        PassPairing.EndsOpen([P("09:00", PassDirection.Unknown), P("18:00", PassDirection.Unknown)]).ShouldBeFalse();
        PassPairing.EndsOpen([P("09:00", PassDirection.In), P("12:00", PassDirection.In)])
            .ShouldBeTrue("takror kirish bitta — kun ochiq");
        PassPairing.EndsOpen([P("09:00", PassDirection.In), P("17:00", PassDirection.Out), P("18:00", PassDirection.Out)])
            .ShouldBeFalse();
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
