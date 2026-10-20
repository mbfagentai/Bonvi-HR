using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Domain.Tests.Attendance;

/// <summary>
/// Jadval so'rovidagi tanaffus qoidasi — SPEC-verifikatsiya-sorov-tahriri §4.4 ning har bir misoli
/// (B1–B19) alohida, sukut qoida 12:00–15:00 / 60 / jumada 120 bilan.
/// </summary>
/// <remarks>
/// Hamma misol «Kunni o'zgartirish» orqali — qoida aynan saqlanadigan tanaffusni tekshiradi
/// (berilgan <c>breakMinutes</c>, aks holda oyna uzunligi).
/// </remarks>
public sealed class RequestBreakRuleTests
{
    private static readonly Guid Xodim = Guid.CreateVersion7();

    /// <summary>2026-10-19 — dushanba.</summary>
    private static readonly DateOnly Dushanba = new(2026, 10, 19);
    private static readonly DateOnly Seshanba = Dushanba.AddDays(1);
    private static readonly DateOnly Chorshanba = Dushanba.AddDays(2);
    private static readonly DateOnly Payshanba = Dushanba.AddDays(3);
    private static readonly DateOnly Juma = Dushanba.AddDays(4);
    private static readonly DateOnly Shanba = Dushanba.AddDays(5);
    private static readonly DateOnly Yakshanba = Dushanba.AddDays(6);

    private const string Oraligi = "davomat.jadval_sorovi_tanaffus_oraligi";
    private const string Davomiyligi = "davomat.jadval_sorovi_tanaffus_davomiyligi";

    private static TimeOnly T(int soat, int daqiqa = 0) => new(soat, daqiqa);

    /// <summary>08:30–17:30 smenali ish kuni.</summary>
    private static ScheduleChangeDayInput Kun(
        DateOnly sana,
        TimeOnly? tanaffusBoshi = null,
        TimeOnly? tanaffusOxiri = null,
        int? daqiqa = null,
        TimeOnly? boshi = null,
        TimeOnly? oxiri = null)
        => new(sana, true, boshi ?? T(8, 30), oxiri ?? T(17, 30), tanaffusBoshi, tanaffusOxiri, BreakMinutes: daqiqa);

    private static Result<ScheduleChangeRequest> Sorov(ScheduleChangeDayInput kun, RequestBreakRule? qoida = null)
        => ScheduleChangeRequest.Create(Xodim, ScheduleChangeKind.Change, [kun], breakRule: qoida ?? RequestBreakRule.Default);

    private static void Otadi(ScheduleChangeDayInput kun, RequestBreakRule? qoida = null)
    {
        var natija = Sorov(kun, qoida);
        natija.IsSuccess.ShouldBeTrue(natija.IsFailure ? natija.Error.Code : null);
    }

    private static void RadEtiladi(ScheduleChangeDayInput kun, string kod, RequestBreakRule? qoida = null)
        => Sorov(kun, qoida).Error.Code.ShouldBe(kod);

    [Fact]
    public void B1_tanaffussiz_kun_otadi() => Otadi(Kun(Dushanba));

    [Fact]
    public void B2_egasining_tanaffussiz_kuni_13_dan_18_gacha_otadi()
        => Otadi(Kun(Dushanba, boshi: T(13), oxiri: T(18)));

    [Fact]
    public void B3_jadvaldan_tegilmagan_kun_60_daqiqa_12_15_oynasida_otadi()
        => Otadi(Kun(Dushanba, T(12), T(15), daqiqa: 60));

    [Fact]
    public void B4_oyna_uzunligi_60_daqiqa_oraliq_ichida_otadi()
        => Otadi(Kun(Dushanba, T(12, 23), T(13, 23)));

    [Fact]
    public void B5_dushanbada_120_daqiqa_rad_etiladi()
        => RadEtiladi(Kun(Dushanba, T(12), T(14)), Davomiyligi);

    [Fact]
    public void B6_jumada_120_daqiqa_otadi_verifix_juma_namozi()
        => Otadi(Kun(Juma, T(12, 23), T(14, 23)));

    [Fact]
    public void B7_jumada_150_daqiqa_rad_etiladi()
        => RadEtiladi(Kun(Juma, T(12), T(14, 30)), Davomiyligi);

    [Fact]
    public void B8_jumada_30_daqiqa_rad_etiladi_eng_kami_60()
        => RadEtiladi(Kun(Juma, T(12, 30), T(13)), Davomiyligi);

    [Fact]
    public void B9_oraliqdan_oldin_boshlangan_tanaffus_rad_etiladi()
        => RadEtiladi(Kun(Seshanba, T(11, 30), T(12, 30)), Oraligi);

    [Fact]
    public void B10_oraliqdan_keyin_tugagan_tanaffus_rad_etiladi()
        => RadEtiladi(Kun(Chorshanba, T(14, 30), T(15, 30)), Oraligi);

    [Fact]
    public void B11_oynasiz_60_daqiqa_faqat_uzunlik_boyicha_otadi()
        => Otadi(Kun(Payshanba, daqiqa: 60));

    [Fact]
    public void B12_oynasiz_90_daqiqa_rad_etiladi()
        => RadEtiladi(Kun(Payshanba, daqiqa: 90), Davomiyligi);

    [Fact]
    public void B13_oynasidan_uzun_tanaffus_rad_etiladi()
        => RadEtiladi(Kun(Shanba, T(12), T(12, 30), daqiqa: 60, boshi: T(9), oxiri: T(14)), Davomiyligi);

    [Fact]
    public void B14_yakshanbada_60_daqiqa_otadi()
        => Otadi(Kun(Yakshanba, T(12), T(13)));

    [Fact]
    public void B15_tungi_smena_tanaffusi_oraliqqa_sigmaydi()
        => RadEtiladi(Kun(Dushanba, T(0, 30), T(1, 30), daqiqa: 60, boshi: T(20), oxiri: T(8)), Oraligi);

    [Fact]
    public void B16_oyna_bor_lekin_nol_daqiqa_ushlanadi_otadi()
        => Otadi(Kun(Dushanba, T(12), T(13), daqiqa: 0));

    [Fact]
    public void B17_smenadan_tashqaridagi_oyna_mavjud_qoida_bilan_birinchi_rad_etiladi()
        => RadEtiladi(Kun(Dushanba, T(7), T(8)), "davomat.jadval_tanaffus_oynasi");

    [Fact]
    public void B18_kun_almashtirish_qoidadan_ozod()
    {
        // Server jadvaldan ko'chirgan kunlar: juft sananing tanaffusi 12:00–14:00 / 120.
        var natija = ScheduleChangeRequest.Create(
            Xodim,
            ScheduleChangeKind.Swap,
            [
                new ScheduleChangeDayInput(Dushanba, true, T(8, 30), T(17, 30), T(12), T(14), Seshanba, 120),
                new ScheduleChangeDayInput(Seshanba, true, T(8, 30), T(17, 30), T(12), T(14), Dushanba, 120),
            ],
            breakRule: RequestBreakRule.Default);

        natija.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void B19_sozlangan_qoida_dushanbada_rad_jumada_otadi()
    {
        var qoida = new RequestBreakRule(T(13), T(14), 30, 45);
        qoida.IsConsistent.ShouldBeTrue();

        RadEtiladi(Kun(Dushanba, T(13), T(13, 45)), Davomiyligi, qoida);
        Otadi(Kun(Juma, T(13), T(13, 45)), qoida);
    }

    [Fact]
    public void Qoidasiz_yaratish_eskicha_120_daqiqani_qabul_qiladi()
        => ScheduleChangeRequest.Create(Xodim, ScheduleChangeKind.Change, [Kun(Dushanba, T(12), T(14))])
            .IsSuccess.ShouldBeTrue("qoida berilmagan chaqiruvlar o'zgarmaydi");

    [Fact]
    public void Tahrir_ham_qoidaga_boysunadi()
    {
        var sorov = ScheduleChangeRequest.Create(Xodim, ScheduleChangeKind.Change, [Kun(Dushanba)]).Value;

        sorov.Update(ScheduleChangeKind.Change, [Kun(Dushanba, T(12), T(14))], null, RequestBreakRule.Default)
            .Error.Code.ShouldBe(Davomiyligi);
        sorov.Version.ShouldBe(0, "rad etilgan tahrir hech narsani o'zgartirmaydi");
    }

    [Fact]
    public void Sukut_qoida_12_15_60_120()
    {
        RequestBreakRule.Default.ShouldBe(new RequestBreakRule(T(12), T(15), 60, 120));
        RequestBreakRule.Default.IsConsistent.ShouldBeTrue();
    }

    [Theory]
    [InlineData(15, 0, 12, 0, 60, 120)] // boshi oxiridan keyin
    [InlineData(12, 0, 12, 0, 60, 120)] // boshi = oxiri
    [InlineData(12, 0, 15, 0, 90, 60)] // uzunlik jumadagidan katta
    [InlineData(12, 0, 13, 0, 60, 120)] // jumadagi oraliqqa sig'maydi
    public void Mos_kelmagan_qoida_aniqlanadi(int fromH, int fromM, int toH, int toM, int minutes, int fridayMax)
        => new RequestBreakRule(T(fromH, fromM), T(toH, toM), minutes, fridayMax).IsConsistent.ShouldBeFalse();
}
