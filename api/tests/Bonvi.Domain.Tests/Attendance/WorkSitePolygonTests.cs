using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Domain.Tests.Attendance;

/// <summary>
/// Obyekt ko'pburchagining tekshiruvi, markazi va shakl almashishi — SPEC-joylashuvlar §A, §F.
/// </summary>
/// <remarks>
/// <para>
/// Kodlar SPEC dagi uchta (<c>obyekt.polygon_nuqtalar_soni</c>,
/// <c>obyekt.polygon_kesishadi</c>, <c>obyekt.polygon_koordinata</c>) va SPEC
/// javob bermagan joylar uchun uchta qo'shimcha: <c>obyekt.polygon_zaxira</c>
/// (zaxira oralig'i), <c>obyekt.polygon_katta</c> (5 km shifti) va
/// <c>obyekt.manzil_uzun</c>.
/// </para>
/// <para>
/// Koordinatalar Toshkent atrofida: kenglikda 0,001° ≈ 111 m, uzunlikda
/// 0,001° ≈ 84 m.
/// </para>
/// </remarks>
public sealed class WorkSitePolygonTests
{
    private static readonly GeoPoint[] Kvadrat = ObyektShakllari.Kvadrat;

    // ────────────────────── Yaratish va markaz ──────────────────────

    [Fact]
    public void Kopburchak_yaratiladi_va_markazi_uchlarning_ortachasi()
    {
        var obyekt = WorkSite.CreatePolygon("  Ombor   №2 ", Kvadrat).Value;

        obyekt.Name.ShouldBe("Ombor №2");
        obyekt.Shape.ShouldBe(WorkSiteShape.Polygon);
        obyekt.Polygon.ShouldBe(Kvadrat);
        obyekt.Latitude.ShouldBe(41.3m);
        obyekt.Longitude.ShouldBe(69.28m);
        obyekt.EdgeBufferMeters.ShouldBe(WorkSite.DefaultEdgeBufferMeters);
        obyekt.RadiusMeters.ShouldBe(WorkSite.DefaultRadiusMeters, "radius CHECK uchun joyida turadi");
    }

    /// <summary>Markaz yuza markazi emas — uchlarning o'rtachasi (SPEC §A «vertex centroid»).</summary>
    [Fact]
    public void Markaz_notekis_uchburchakda_ham_uchlarning_ortachasi()
    {
        GeoPoint[] uchburchak = [new(41.30m, 69.27m), new(41.30m, 69.29m), new(41.31m, 69.27m)];

        var obyekt = WorkSite.CreatePolygon("Uchburchak", uchburchak).Value;

        // (41.30 + 41.30 + 41.31) / 3 = 41.303333… → olti xonaga yaxlitlanadi.
        obyekt.Latitude.ShouldBe(41.303333m);
        obyekt.Longitude.ShouldBe(69.276667m);
    }

    [Fact]
    public void Uchlar_olti_kasr_xonagacha_yaxlitlanadi()
    {
        GeoPoint[] aniq =
        [
            new(41.2990004m, 69.2787m),
            new(41.299m, 69.28130049m),
            new(41.301m, 69.2813m),
            new(41.301m, 69.2787m),
        ];

        var obyekt = WorkSite.CreatePolygon("Yaxlit", aniq).Value;

        obyekt.Polygon![0].Latitude.ShouldBe(41.299000m);
        obyekt.Polygon[1].Longitude.ShouldBe(69.281300m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(500)]
    public void Zaxira_0_dan_500_gacha_qabul_qilinadi(int zaxira)
        => WorkSite.CreatePolygon("Zaxira", Kvadrat, zaxira).Value.EdgeBufferMeters.ShouldBe(zaxira);

    [Theory]
    [InlineData(-1)]
    [InlineData(501)]
    public void Oraliqdan_tashqari_zaxira_rad_etiladi(int zaxira)
        => Xato(WorkSite.CreatePolygon("Zaxira", Kvadrat, zaxira)).ShouldBe("obyekt.polygon_zaxira");

    [Fact]
    public void Nomsiz_kopburchak_rad_etiladi()
        => Xato(WorkSite.CreatePolygon("   ", Kvadrat)).ShouldBe("malumotnoma.nom_bosh");

    // ────────────────────── Uchlar soni ──────────────────────

    [Fact]
    public void Uchlar_berilmasa_rad_etiladi()
        => Xato(WorkSite.CreatePolygon("Bo'sh", null)).ShouldBe("obyekt.polygon_nuqtalar_soni");

    [Fact]
    public void Ikki_uch_rad_etiladi()
        => Xato(WorkSite.CreatePolygon("Kesma", Kvadrat[..2])).ShouldBe("obyekt.polygon_nuqtalar_soni");

    [Fact]
    public void Ellik_uch_qabul_qilinadi_ellik_birinchisi_rad_etiladi()
    {
        WorkSite.CreatePolygon("Aylana 50", Aylana(50)).IsSuccess.ShouldBeTrue();
        Xato(WorkSite.CreatePolygon("Aylana 51", Aylana(51))).ShouldBe("obyekt.polygon_nuqtalar_soni");
    }

    // ────────────────────── Koordinatalar ──────────────────────

    [Theory]
    [InlineData(90.000001, 69.28)]
    [InlineData(-90.5, 69.28)]
    [InlineData(41.3, 180.1)]
    [InlineData(41.3, -181)]
    public void Oraliqdan_tashqari_uch_rad_etiladi(double kenglik, double uzunlik)
    {
        GeoPoint[] uchlar = [.. Kvadrat[..3], new((decimal)kenglik, (decimal)uzunlik)];

        Xato(WorkSite.CreatePolygon("Chegara", uchlar)).ShouldBe("obyekt.polygon_koordinata");
    }

    [Fact]
    public void Ketma_ket_ikki_bir_xil_uch_rad_etiladi()
    {
        GeoPoint[] uchlar = [Kvadrat[0], Kvadrat[1], Kvadrat[1], Kvadrat[2], Kvadrat[3]];

        Xato(WorkSite.CreatePolygon("Takror", uchlar)).ShouldBe("obyekt.polygon_koordinata");
    }

    /// <summary>
    /// Yaxlitlangandan keyin bir xil bo'lib qolgan uchlar ham takror — 1 sm farq nol uzunlikdagi qirra.
    /// </summary>
    [Fact]
    public void Yaxlitlangach_bir_xil_bolgan_uchlar_rad_etiladi()
    {
        GeoPoint[] uchlar = [Kvadrat[0], new(41.2990001m, 69.2787001m), Kvadrat[1], Kvadrat[2], Kvadrat[3]];

        Xato(WorkSite.CreatePolygon("Takror", uchlar)).ShouldBe("obyekt.polygon_koordinata");
    }

    /// <summary>
    /// Yopiq halqa (GeoJSON uslubi, birinchi = oxirgi) rad etiladi — SPEC ochiq halqani talab qiladi.
    /// </summary>
    [Fact]
    public void Yopiq_halqa_rad_etiladi()
    {
        GeoPoint[] uchlar = [.. Kvadrat, Kvadrat[0]];

        Xato(WorkSite.CreatePolygon("Yopiq", uchlar)).ShouldBe("obyekt.polygon_koordinata");
    }

    // ────────────────────── O'zini kesib o'tish ──────────────────────

    /// <summary>«Kapalak» — ikki qarama-qarshi qirra kesishadi.</summary>
    [Fact]
    public void Kapalak_shakli_rad_etiladi()
    {
        GeoPoint[] kapalak = [Kvadrat[0], Kvadrat[2], Kvadrat[1], Kvadrat[3]];

        Xato(WorkSite.CreatePolygon("Kapalak", kapalak)).ShouldBe("obyekt.polygon_kesishadi");
    }

    /// <summary>Uchta nuqta bir chiziqda — yuza nol.</summary>
    [Fact]
    public void Bir_chiziqdagi_uch_nuqta_rad_etiladi()
    {
        GeoPoint[] chiziq = [new(41.30m, 69.27m), new(41.30m, 69.28m), new(41.30m, 69.29m)];

        Xato(WorkSite.CreatePolygon("Chiziq", chiziq)).ShouldBe("obyekt.polygon_kesishadi");
    }

    /// <summary>Nayza — qirra o'zidan oldingi qirraning ustiga orqaga qaytadi.</summary>
    [Fact]
    public void Orqaga_qaytgan_nayza_rad_etiladi()
    {
        GeoPoint[] nayza =
        [
            Kvadrat[0],
            Kvadrat[1],
            Kvadrat[2],
            new(41.302m, 69.2813m), // shimolga chiqadi…
            new(41.3015m, 69.2813m), // …va o'sha chiziq bo'ylab orqaga qaytadi
            Kvadrat[3],
        ];

        Xato(WorkSite.CreatePolygon("Nayza", nayza)).ShouldBe("obyekt.polygon_kesishadi");
    }

    /// <summary>«Sakkiz» — ikki halqa bitta uchda tutashadi, qirralar kesishmasa ham.</summary>
    [Fact]
    public void Bitta_uchda_tutashgan_ikki_halqa_rad_etiladi()
    {
        var tutash = new GeoPoint(41.300m, 69.280m);
        GeoPoint[] sakkiz =
        [
            tutash,
            new(41.299m, 69.279m),
            new(41.299m, 69.281m),
            tutash,
            new(41.301m, 69.281m),
            new(41.301m, 69.279m),
        ];

        Xato(WorkSite.CreatePolygon("Sakkiz", sakkiz)).ShouldBe("obyekt.polygon_kesishadi");
    }

    /// <summary>
    /// Uch qo'shni bo'lmagan qirraning USTIGA tushadi — nur otish bunday shaklda chalkashadi.
    /// </summary>
    [Fact]
    public void Uchi_boshqa_qirraga_tegib_turgan_shakl_rad_etiladi()
    {
        GeoPoint[] tegish =
        [
            new(41.299m, 69.279m),
            new(41.299m, 69.281m),
            new(41.301m, 69.281m),
            new(41.299m, 69.280m), // pastki qirraning o'rtasiga tushadi
            new(41.301m, 69.279m),
        ];

        Xato(WorkSite.CreatePolygon("Tegish", tegish)).ShouldBe("obyekt.polygon_kesishadi");
    }

    /// <summary>Botiq (L-shakl) ko'pburchak — oddiy, qabul qilinadi.</summary>
    [Fact]
    public void Botiq_kopburchak_qabul_qilinadi()
        => WorkSite.CreatePolygon("L-ombor", ObyektShakllari.LShakl).IsSuccess.ShouldBeTrue();

    /// <summary>To'g'ri davom etgan uch (A → B → C bir chiziqda, orqaga qaytmaydi) zarar qilmaydi.</summary>
    [Fact]
    public void Qirra_ortasidagi_ortiqcha_uch_qabul_qilinadi()
    {
        GeoPoint[] uchlar = [Kvadrat[0], new(41.299m, 69.28m), Kvadrat[1], Kvadrat[2], Kvadrat[3]];

        WorkSite.CreatePolygon("Ortiqcha uch", uchlar).IsSuccess.ShouldBeTrue();
    }

    // ────────────────────── O'lcham ──────────────────────

    /// <summary>
    /// Doiraning 5 km shifti ko'pburchak orqali aylanib o'tilmaydi (SPEC da yo'q, ataylab qo'shilgan).
    /// </summary>
    [Fact]
    public void Markazdan_5_km_dan_uzoq_uchli_kopburchak_rad_etiladi()
    {
        // ~12 km × 12 km — butun bir tuman.
        GeoPoint[] tuman = [new(41.25m, 69.20m), new(41.25m, 69.34m), new(41.36m, 69.34m), new(41.36m, 69.20m)];

        Xato(WorkSite.CreatePolygon("Tuman", tuman)).ShouldBe("obyekt.polygon_katta");
    }

    // ────────────────────── Shakl almashishi ──────────────────────

    [Fact]
    public void Doira_kopburchakka_otkazilganda_radius_joyida_qoladi()
    {
        var obyekt = WorkSite.Create("Almashuvchi", 41.3m, 69.28m, 350).Value;

        obyekt.ReconfigurePolygon(Kvadrat, 0).IsSuccess.ShouldBeTrue();

        obyekt.Shape.ShouldBe(WorkSiteShape.Polygon);
        obyekt.Polygon.ShouldBe(Kvadrat);
        obyekt.EdgeBufferMeters.ShouldBe(0);
        obyekt.RadiusMeters.ShouldBe(350);
    }

    [Fact]
    public void Kopburchak_doiraga_otkazilganda_uchlar_ochadi_zaxira_qoladi()
    {
        var obyekt = WorkSite.CreatePolygon("Almashuvchi", Kvadrat, 75).Value;

        obyekt.Reconfigure(41.31m, 69.29m, 300).IsSuccess.ShouldBeTrue();

        obyekt.Shape.ShouldBe(WorkSiteShape.Circle);
        obyekt.Polygon.ShouldBeNull();
        obyekt.Latitude.ShouldBe(41.31m);
        obyekt.Longitude.ShouldBe(69.29m);
        obyekt.RadiusMeters.ShouldBe(300);
        obyekt.EdgeBufferMeters.ShouldBe(75);
    }

    [Fact]
    public void Notogri_kopburchak_bilan_qayta_sozlash_obyektni_ozgartirmaydi()
    {
        var obyekt = WorkSite.Create("Doira", 41.3m, 69.28m, 200).Value;
        GeoPoint[] kapalak = [Kvadrat[0], Kvadrat[2], Kvadrat[1], Kvadrat[3]];

        var natija = obyekt.ReconfigurePolygon(kapalak, 50);

        natija.Error.Code.ShouldBe("obyekt.polygon_kesishadi");
        obyekt.Shape.ShouldBe(WorkSiteShape.Circle);
        obyekt.Polygon.ShouldBeNull();
        obyekt.Latitude.ShouldBe(41.3m);
    }

    [Fact]
    public void Yangi_doira_sukut_zaxira_bilan_yaratiladi()
    {
        var obyekt = WorkSite.Create("Doira", 41.3m, 69.28m, 200).Value;

        obyekt.Shape.ShouldBe(WorkSiteShape.Circle);
        obyekt.Polygon.ShouldBeNull();
        obyekt.EdgeBufferMeters.ShouldBe(WorkSite.DefaultEdgeBufferMeters);
    }

    // ────────────────────── Manzil ──────────────────────

    [Fact]
    public void Manzil_chetdagi_bosh_joysiz_saqlanadi_bosh_matn_null()
    {
        var obyekt = WorkSite.Create("Manzilli", 41.3m, 69.28m, 200).Value;

        obyekt.SetAddress("  Toshkent, Chilonzor 5  ").IsSuccess.ShouldBeTrue();
        obyekt.Address.ShouldBe("Toshkent, Chilonzor 5");

        obyekt.SetAddress("   ").IsSuccess.ShouldBeTrue();
        obyekt.Address.ShouldBeNull();
    }

    [Fact]
    public void Manzil_300_belgigacha_qabul_qilinadi_301_rad_etiladi()
    {
        var obyekt = WorkSite.Create("Manzilli", 41.3m, 69.28m, 200).Value;

        obyekt.SetAddress(new string('a', WorkSite.MaxAddressLength)).IsSuccess.ShouldBeTrue();

        var uzun = obyekt.SetAddress(new string('a', WorkSite.MaxAddressLength + 1));
        uzun.Error.Code.ShouldBe("obyekt.manzil_uzun");
        obyekt.Address!.Length.ShouldBe(WorkSite.MaxAddressLength, "rad etilgan qiymat yozilmaydi");
    }

    // ────────────────────── Yordamchilar ──────────────────────

    /// <summary>
    /// ~220 m radiusli aylanaga joylashgan <paramref name="soni"/> ta uch — oddiy qavariq ko'pburchak.
    /// </summary>
    /// <remarks>
    /// Radius ataylab katta: 51 uchda qo'shni uchlar orasidagi egilish ~0,4 m,
    /// olti xonali yaxlitlash esa ≤ 0,1 m — uchta qo'shni uch bir chiziqqa
    /// tushib qolmaydi va test faqat SONNI tekshiradi.
    /// </remarks>
    private static GeoPoint[] Aylana(int soni)
        => [.. Enumerable.Range(0, soni).Select(index =>
        {
            var burchak = 2 * Math.PI * index / soni;
            return new GeoPoint(
                41.3m + (decimal)Math.Round(0.002 * Math.Sin(burchak), 6),
                69.28m + (decimal)Math.Round(0.0027 * Math.Cos(burchak), 6));
        })];

    private static string Xato<T>(Result<T> natija)
    {
        natija.IsFailure.ShouldBeTrue();
        return natija.Error.Code;
    }
}
