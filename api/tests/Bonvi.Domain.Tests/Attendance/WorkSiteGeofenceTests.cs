using Bonvi.Domain.Attendance;

namespace Bonvi.Domain.Tests.Attendance;

/// <summary>
/// Geo-to'siq qoidasi — SPEC-joylashuvlar §B, §F: ko'pburchak ichida, zaxira ichida, zaxiradan
/// tashqarida, doira o'zgarmagani va doira bilan ko'pburchak aralash turganda tanlov.
/// </summary>
/// <remarks>
/// <para>
/// Nuqtalar obyektdan SHIMOLGA metr bilan suriladi
/// (<see cref="ObyektShakllari.MetrKenglikda"/>): kenglik bo'yicha metr har
/// joyda bir xil, ya'ni kutilgan masofani test o'zi geodeziya qilmasdan
/// biladi. Tekis proyeksiya va haversine ikkalasi ham kenglik bo'yicha shu
/// sonni beradi — farq faqat yaxlitlashda.
/// </para>
/// </remarks>
public sealed class WorkSiteGeofenceTests
{
    /// <summary>Kutilgan va o'lchangan masofa orasidagi ruxsat etilgan farq, metr.</summary>
    private const double Aniqlik = 0.5;

    /// <summary><see cref="ObyektShakllari.Kvadrat"/> ning shimoliy qirrasi.</summary>
    private const decimal ShimoliyQirra = 41.301m;

    private const decimal Markaz = 69.28m;

    // ────────────────────── Ko'pburchak ──────────────────────

    [Fact]
    public void Kopburchak_ichidagi_nuqta_masofasi_nol_va_qabul_qilinadi()
    {
        var obyekt = Kopburchak(zaxira: 50);

        var olchov = obyekt.Measure(41.3m, Markaz);

        olchov.OutsideMeters.ShouldBe(0);
        olchov.Accepted.ShouldBeTrue();
        olchov.FrozenDistanceMeters.ShouldBe(0, "ichkarida dalil masofasi 0");
        olchov.FrozenAllowanceMeters.ShouldBe(50, "ko'pburchakda ruxsat — zaxira");
    }

    /// <summary>Ichkarida, lekin chetga juda yaqin — nur otish chetdagi nuqtani ham to'g'ri ko'radi.</summary>
    [Fact]
    public void Kopburchak_chetidan_bir_metr_ichkaridagi_nuqta_ichkarida()
    {
        var olchov = Kopburchak(zaxira: 0).Measure(ShimoliyQirra - ObyektShakllari.MetrKenglikda(1), Markaz);

        olchov.OutsideMeters.ShouldBe(0);
        olchov.Accepted.ShouldBeTrue("zaxira 0 bo'lsa ham ichkaridagi nuqta qabul qilinadi");
    }

    /// <summary>
    /// Zaxira 0, nuqta QIYA qirraning aynan ustida: suzuvchi nuqta hisobi
    /// masofani 0 emas, ~3·10⁻¹⁰ m beradi — chegara qo'shib olinishi uchun
    /// qaror santimetrgacha yaxlitlab qilinadi.
    /// </summary>
    [Fact]
    public void Zaxirasiz_kopburchakning_qiya_qirrasi_ustidagi_nuqta_qabul_qilinadi()
    {
        GeoPoint[] uchburchak =
        [
            new(41.299m, 69.2787m),
            new(41.301m, 69.2813m),
            new(41.301m, 69.2787m),
        ];
        var obyekt = WorkSite.CreatePolygon("Uchburchak", uchburchak, 0).Value;

        var olchov = obyekt.Measure(41.300m, 69.2800m);

        olchov.Accepted.ShouldBeTrue("qiya qirra ustidagi nuqta — chegarada, ya'ni ichkarida");
    }

    [Fact]
    public void Kopburchakdan_tashqarida_lekin_zaxira_ichida_qabul_qilinadi()
    {
        var olchov = Kopburchak(zaxira: 50).Measure(Shimolda(30), Markaz);

        olchov.OutsideMeters.ShouldBe(30, Aniqlik);
        olchov.Accepted.ShouldBeTrue();
        olchov.FrozenDistanceMeters.ShouldBe(30, Aniqlik, "dalil — chetgacha masofa");
        olchov.FrozenAllowanceMeters.ShouldBe(50);
    }

    [Fact]
    public void Zaxiradan_tashqaridagi_nuqta_rad_etiladi_va_chetgacha_masofa_beriladi()
    {
        var olchov = Kopburchak(zaxira: 50).Measure(Shimolda(120), Markaz);

        olchov.Accepted.ShouldBeFalse();
        olchov.OutsideMeters.ShouldBe(120, Aniqlik, "markazgacha (~231 m) emas, CHETGACHA");
    }

    /// <summary>Zaxira chegarasi QO'SHIB olinadi — doiradagi <c>&lt;=</c> bilan bir xil.</summary>
    [Fact]
    public void Zaxira_nolga_teng_bolsa_faqat_ichkari_qabul_qilinadi()
    {
        var obyekt = Kopburchak(zaxira: 0);

        obyekt.Measure(Shimolda(5), Markaz).Accepted.ShouldBeFalse();
        obyekt.Measure(ShimoliyQirra, Markaz).Accepted.ShouldBeTrue("qirraning ustidagi nuqta — ichkarida");
    }

    /// <summary>Burchak yonidagi nuqta — eng yaqin qirra emas, eng yaqin UCH hal qiladi.</summary>
    [Fact]
    public void Burchak_tashqarisidagi_nuqtada_masofa_uchgacha_olchanadi()
    {
        // Shimoli-sharqiy burchakdan 60 m shimolga va ~80 m sharqqa.
        var sharqqa = 80 / (Geo.MetersPerDegree * Math.Cos(double.DegreesToRadians(41.301 + (60 / Geo.MetersPerDegree))));
        var olchov = Kopburchak(zaxira: 50).Measure(Shimolda(60), 69.2813m + (decimal)Math.Round(sharqqa, 9));

        olchov.OutsideMeters.ShouldBe(100, 1, "√(60² + 80²)");
        olchov.Accepted.ShouldBeFalse();
    }

    /// <summary>
    /// Botiq ko'pburchakning «o'yig'i» — tashqi to'rtburchak ichida, lekin ko'pburchakdan tashqarida.
    /// </summary>
    /// <remarks>
    /// Faqat qavariq shakllarda o'tadigan usul (masalan chegaralovchi
    /// to'rtburchak) bu nuqtani ichkarida deb biladi. Eng yaqin qirra —
    /// o'yiqning janubiy tomoni (kenglik 41.300), ~55,6 m.
    /// </remarks>
    [Fact]
    public void Botiq_kopburchakning_oyigidagi_nuqta_tashqarida()
    {
        var obyekt = WorkSite.CreatePolygon("L-ombor", ObyektShakllari.LShakl, 0).Value;

        var olchov = obyekt.Measure(41.3005m, 69.2807m);

        olchov.Accepted.ShouldBeFalse();
        olchov.OutsideMeters.ShouldBe(0.0005 * Geo.MetersPerDegree, Aniqlik);
    }

    // ────────────────────── Doira — o'zgarmagan ──────────────────────

    /// <summary>
    /// Doira qarori avvalgidek: markazgacha ≤ radius. Dalil — MARKAZGACHA masofa va radius.
    /// </summary>
    [Fact]
    public void Doira_ichidagi_nuqta_avvalgidek_qabul_qilinadi()
    {
        var obyekt = Doira(41.3m, radius: 200);

        var olchov = obyekt.Measure(41.3m + ObyektShakllari.MetrKenglikda(90), Markaz);

        olchov.Accepted.ShouldBeTrue();
        olchov.OutsideMeters.ShouldBe(-110, Aniqlik, "chegaradan 110 m ichkarida");
        olchov.ToleranceMeters.ShouldBe(0);
        olchov.FrozenDistanceMeters.ShouldBe(90, Aniqlik, "doirada dalil — markazgacha");
        olchov.FrozenDistanceMeters.ShouldBe(obyekt.DistanceMetersTo(41.3m + ObyektShakllari.MetrKenglikda(90), Markaz));
        olchov.FrozenAllowanceMeters.ShouldBe(200);
    }

    [Fact]
    public void Doiradan_tashqaridagi_nuqta_rad_etiladi_va_chegaragacha_masofa_beriladi()
    {
        var olchov = Doira(41.3m, radius: 200).Measure(41.3m + ObyektShakllari.MetrKenglikda(250), Markaz);

        olchov.Accepted.ShouldBeFalse();
        olchov.OutsideMeters.ShouldBe(50, Aniqlik, "250 m markazgacha − 200 m radius");
    }

    /// <summary>Doirada chet zaxirasi ishlatilmaydi — qiymati qanday bo'lmasin.</summary>
    [Fact]
    public void Doirada_chet_zaxirasi_hisobga_olinmaydi()
    {
        var obyekt = WorkSite.CreatePolygon("Avval ko'pburchak", ObyektShakllari.Kvadrat, 500).Value;
        obyekt.Reconfigure(41.3m, Markaz, 200).IsSuccess.ShouldBeTrue();
        obyekt.EdgeBufferMeters.ShouldBe(500, "qiymat joyida, lekin ishlatilmaydi");

        obyekt.Measure(41.3m + ObyektShakllari.MetrKenglikda(250), Markaz).Accepted.ShouldBeFalse();
    }

    // ────────────────────── Tanlov: doira va ko'pburchak aralash ──────────────────────

    [Fact]
    public void Obyekt_bolmasa_natija_yoq()
        => Geofence.Locate([], 41.3m, Markaz).ShouldBeNull();

    /// <summary>
    /// Doira nuqtaga YAQINROQ (5 m tashqarida), ko'pburchak uzoqroq (40 m) — lekin ko'pburchakning
    /// 50 m zaxirasi bor. Qabul qiladigani tanlanadi.
    /// </summary>
    [Fact]
    public void Zaxirasi_bor_kopburchak_yaqinroq_lekin_qabul_qilmaydigan_doiradan_ustun()
    {
        var kopburchak = Kopburchak(zaxira: 50);
        var nuqta = Shimolda(40);

        // Doira markazi nuqtadan 105 m shimolda, radiusi 100 — nuqta 5 m tashqarida.
        var doira = Doira(nuqta + ObyektShakllari.MetrKenglikda(105), radius: 100);

        var natija = Geofence.Locate([doira, kopburchak], nuqta, Markaz)!;

        natija.Site.ShouldBeSameAs(kopburchak);
        natija.Accepted.ShouldBeTrue();
        natija.OutsideMeters.ShouldBe(40, Aniqlik);
    }

    /// <summary>Ikkalasi ham qabul qilsa, zaxira ayirilgandan keyin eng «chuqur» obyekt tanlanadi.</summary>
    [Fact]
    public void Ikkalasi_qabul_qilsa_zaxiradan_keyingi_eng_kichik_kalit_tanlanadi()
    {
        var kopburchak = Kopburchak(zaxira: 50);

        // Nuqta — kvadratning markazi (kalit: 0 − 50 = −50). Doira ham shu
        // markazda, radiusi 300 (kalit: 0 − 300 = −300).
        var doira = Doira(41.3m, radius: 300);

        var natija = Geofence.Locate([kopburchak, doira], 41.3m, Markaz)!;

        natija.Site.ShouldBeSameAs(doira);
        natija.Accepted.ShouldBeTrue();
    }

    /// <summary>
    /// Hech biri qabul qilmasa — kaliti eng kichik obyekt va uning CHEGARASIGACHA masofasi qaytadi.
    /// </summary>
    [Fact]
    public void Hech_biri_qabul_qilmasa_eng_yaqin_obyekt_va_chegaragacha_masofa_qaytadi()
    {
        // Ko'pburchakdan 120 m (zaxira 50 → kalit 70).
        var kopburchak = Kopburchak(zaxira: 50);
        var nuqta = Shimolda(120);

        // Doiradan 60 m tashqarida (kalit 60): markazi 160 m shimolda, radiusi 100.
        var doira = Doira(nuqta + ObyektShakllari.MetrKenglikda(160), radius: 100);

        var natija = Geofence.Locate([kopburchak, doira], nuqta, Markaz)!;

        natija.Site.ShouldBeSameAs(doira);
        natija.Accepted.ShouldBeFalse();
        natija.OutsideMeters.ShouldBe(60, Aniqlik);
    }

    /// <summary>Zaxira HAR BIR obyektning O'ZINIKI — kattaroq zaxira uzoqroq ko'pburchakni ham qabul qildiradi.</summary>
    [Fact]
    public void Har_bir_kopburchak_oz_zaxirasi_bilan_olchanadi()
    {
        var torZaxira = Kopburchak(zaxira: 0, nom: "Tor");
        var kengZaxira = WorkSite.CreatePolygon(
            "Keng",
            [.. ObyektShakllari.Kvadrat.Select(uch => uch with { Latitude = uch.Latitude + 0.003m })],
            200).Value;

        // Tor zaxirali kvadratdan 20 m (kalit 20), keng zaxiralisi 0,003° (~333 m)
        // shimolroqda — undan ~91 m (kalit 91 − 200 = −109).
        var nuqta = Shimolda(20);

        var natija = Geofence.Locate([torZaxira, kengZaxira], nuqta, Markaz)!;

        natija.Site.ShouldBeSameAs(kengZaxira);
        natija.Accepted.ShouldBeTrue();
    }

    // ────────────────────── Yordamchilar ──────────────────────

    private static WorkSite Kopburchak(int zaxira, string nom = "Kvadrat")
        => WorkSite.CreatePolygon(nom, ObyektShakllari.Kvadrat, zaxira).Value;

    private static WorkSite Doira(decimal kenglik, int radius)
        => WorkSite.Create("Doira " + kenglik, kenglik, Markaz, radius).Value;

    /// <summary>Kvadratning shimoliy qirrasidan <paramref name="metr"/> shimoldagi kenglik.</summary>
    private static decimal Shimolda(double metr) => ShimoliyQirra + ObyektShakllari.MetrKenglikda(metr);
}
