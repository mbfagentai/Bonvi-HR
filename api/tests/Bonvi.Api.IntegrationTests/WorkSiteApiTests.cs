using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Identity;
using Bonvi.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Obyektlar API si — <c>/api/attendance/work-sites</c> (SPEC-joylashuvlar §C, §F).
/// </summary>
/// <remarks>
/// <para>
/// Ruxsat: o'qish <c>attendance.view</c> YOKI <c>catalog.view</c>, yozish
/// <c>attendance.manage</c>. Har bir holat faqat SHU ruxsatli token bilan
/// tekshiriladi — administrator tokeni hamma eshikni ochib, ruxsat xatosini
/// yashirib qo'yardi.
/// </para>
/// <para>
/// Sinf bitta bazani bo'lishadi va obyektlar ro'yxati global; shu sababli
/// nomlar takrorlanmas (<see cref="Belgi"/>) va koordinatalar boshqa
/// testlarning nuqtalaridan uzoqda (kenglik 38°).
/// </para>
/// </remarks>
public sealed class WorkSiteApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public WorkSiteApiTests(BonviApiFactory factory) => _factory = factory;

    // ════════════════════════════════════════════════════════════
    //  Ruxsatlar
    // ════════════════════════════════════════════════════════════

    /// <summary><c>attendance.view</c> — ro'yxat va bitta yozuvni o'qiydi, hech narsa yoza olmaydi.</summary>
    [Fact]
    public async Task Davomat_korish_ruxsati_royxatni_oqiydi_lekin_yarata_olmaydi()
    {
        var ct = TestContext.Current.CancellationToken;

        using var boshqaruvchi = await MijozAsync(Permissions.AttendanceManage);
        var mavjud = await YaratAsync(boshqaruvchi, Doira("O'qish " + Belgi()));

        using var mijoz = await MijozAsync(Permissions.AttendanceView);

        using (var royxat = await mijoz.GetAsync(Yol(""), ct))
        {
            royxat.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(royxat));
            (await JsonAsync(royxat)).EnumerateArray()
                .ShouldContain(item => item.GetProperty("id").GetGuid() == mavjud);
        }

        using (var bitta = await mijoz.GetAsync(Yol($"/{mavjud}"), ct))
        {
            bitta.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(bitta));
        }

        using (var yaratish = await mijoz.PostAsJsonAsync(Yol(""), Doira("Taqiq " + Belgi()), ct))
        {
            yaratish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        foreach (var (usul, yol) in YozishYollari(mavjud))
        {
            using var sorov = new HttpRequestMessage(usul, Yol(yol))
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
            using var javob = await mijoz.SendAsync(sorov, ct);

            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"{usul} {yol}");
        }
    }

    /// <summary><c>catalog.view</c> ham o'qiydi — bugungidek («Ma'lumotnomalar» tanlash ro'yxatlari).</summary>
    [Fact]
    public async Task Malumotnoma_korish_ruxsati_ham_royxatni_oqiydi()
    {
        using var mijoz = await MijozAsync(Permissions.CatalogView);

        using var javob = await mijoz.GetAsync(Yol(""), TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
    }

    /// <summary>Ikkala o'qish ruxsati ham yo'q — 403; tokensiz — 401.</summary>
    [Fact]
    public async Task Oqish_ruxsatisiz_403_tokensiz_401()
    {
        var ct = TestContext.Current.CancellationToken;

        // `attendance.manage` YOLG'IZ o'qishni bermaydi — o'qish ruxsati alohida.
        using (var boshqaruvchi = await MijozAsync(Permissions.AttendanceManage, Permissions.ReportsExport))
        using (var javob = await boshqaruvchi.GetAsync(Yol(""), ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using var anonim = _factory.CreateClient();
        using var anonimJavob = await anonim.GetAsync(Yol(""), ct);
        anonimJavob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ════════════════════════════════════════════════════════════
    //  Ikkala shakl — attendance.manage bilan
    // ════════════════════════════════════════════════════════════

    [Fact]
    public async Task Boshqaruv_ruxsati_doira_yaratadi()
    {
        using var mijoz = await MijozAsync(Permissions.AttendanceManage);
        var nom = "Doira " + Belgi();

        using var javob = await mijoz.PostAsJsonAsync(
            Yol(""),
            new
            {
                name = nom,
                shape = "Circle",
                address = "  Toshkent, Sergeli 7  ",
                latitude = 38.1m,
                longitude = 66.5m,
                radiusMeters = 300,
            },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        var obyekt = await JsonAsync(javob);

        obyekt.GetProperty("shape").GetString().ShouldBe("Circle");
        obyekt.GetProperty("address").GetString().ShouldBe("Toshkent, Sergeli 7");
        obyekt.GetProperty("latitude").GetDecimal().ShouldBe(38.1m);
        obyekt.GetProperty("radiusMeters").GetInt32().ShouldBe(300);
        obyekt.GetProperty("polygon").ValueKind.ShouldBe(JsonValueKind.Null);
        obyekt.GetProperty("edgeBufferMeters").GetInt32().ShouldBe(50);
    }

    /// <summary>
    /// Ko'pburchak: markazni server hisoblaydi (mijoz yuborgani e'tiborga olinmaydi), uchlar bazadan
    /// aynan qaytadi, zaxira sukut bo'yicha 50.
    /// </summary>
    [Fact]
    public async Task Boshqaruv_ruxsati_kopburchak_yaratadi_va_markazni_server_hisoblaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var mijoz = await MijozAsync(Permissions.AttendanceManage);

        using var javob = await mijoz.PostAsJsonAsync(
            Yol(""),
            new
            {
                name = "Ko'pburchak " + Belgi(),
                shape = "Polygon",
                latitude = 1m, // e'tiborga olinmaydi
                longitude = 1m,
                polygon = Kvadrat(38.2m),
            },
            ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        var yaratilgan = await JsonAsync(javob);

        yaratilgan.GetProperty("shape").GetString().ShouldBe("Polygon");
        yaratilgan.GetProperty("latitude").GetDecimal().ShouldBe(38.2m);
        yaratilgan.GetProperty("longitude").GetDecimal().ShouldBe(66.5m);
        yaratilgan.GetProperty("edgeBufferMeters").GetInt32().ShouldBe(50);

        // Bazadan qayta o'qiladi — `jsonb` orqali aylanib kelgan uchlar.
        var id = yaratilgan.GetProperty("id").GetGuid();

        using var oquvchi = await MijozAsync(Permissions.AttendanceView);
        using var oqilgan = await oquvchi.GetAsync(Yol($"/{id}"), ct);
        oqilgan.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(oqilgan));

        var uchlar = (await JsonAsync(oqilgan)).GetProperty("polygon").EnumerateArray()
            .Select(uch => (uch.GetProperty("lat").GetDecimal(), uch.GetProperty("lng").GetDecimal()))
            .ToArray();

        uchlar.ShouldBe(Kvadrat(38.2m).Select(uch => (uch.Lat, uch.Lng)).ToArray());
    }

    /// <summary>Shaklni almashtirish: ko'pburchak → doira → ko'pburchak; zaxira berilmasa hozirgisi qoladi.</summary>
    [Fact]
    public async Task Tahrirda_shakl_almashadi_va_berilmagan_zaxira_saqlanadi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var mijoz = await MijozAsync(Permissions.AttendanceManage);
        var nom = "Almashuvchi " + Belgi();

        var id = await YaratAsync(mijoz, new { name = nom, shape = "Polygon", polygon = Kvadrat(38.3m), edgeBufferMeters = 0 });

        using (var doira = await mijoz.PutAsJsonAsync(
                   Yol($"/{id}"),
                   new { name = nom, shape = "Circle", latitude = 38.3m, longitude = 66.5m, radiusMeters = 150 },
                   ct))
        {
            doira.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(doira));
            var natija = await JsonAsync(doira);
            natija.GetProperty("shape").GetString().ShouldBe("Circle");
            natija.GetProperty("polygon").ValueKind.ShouldBe(JsonValueKind.Null);
            natija.GetProperty("edgeBufferMeters").GetInt32().ShouldBe(0);
        }

        using var kopburchak = await mijoz.PutAsJsonAsync(
            Yol($"/{id}"),
            new { name = nom, shape = "Polygon", polygon = Kvadrat(38.3m) },
            ct);

        kopburchak.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(kopburchak));
        var oxirgi = await JsonAsync(kopburchak);
        oxirgi.GetProperty("edgeBufferMeters").GetInt32()
            .ShouldBe(0, "HR qo'ygan 0 jimgina 50 ga qaytmasligi kerak");
        oxirgi.GetProperty("radiusMeters").GetInt32().ShouldBe(150, "radius joyida qoladi");
    }

    // ════════════════════════════════════════════════════════════
    //  Noto'g'ri tana — 400 va kod
    // ════════════════════════════════════════════════════════════

    [Fact]
    public async Task Ozini_kesib_otgan_kopburchak_400_va_kod()
    {
        using var mijoz = await MijozAsync(Permissions.AttendanceManage);
        var k = Kvadrat(38.4m);

        using var javob = await mijoz.PostAsJsonAsync(
            Yol(""),
            new { name = "Kapalak " + Belgi(), shape = "Polygon", polygon = new[] { k[0], k[2], k[1], k[3] } },
            TestContext.Current.CancellationToken);

        await XatoKutAsync(javob, HttpStatusCode.BadRequest, "obyekt.polygon_kesishadi");
    }

    [Fact]
    public async Task Ikki_uchli_kopburchak_400_va_kod()
    {
        using var mijoz = await MijozAsync(Permissions.AttendanceManage);

        using var javob = await mijoz.PostAsJsonAsync(
            Yol(""),
            new { name = "Kesma " + Belgi(), shape = "Polygon", polygon = Kvadrat(38.4m)[..2] },
            TestContext.Current.CancellationToken);

        await XatoKutAsync(javob, HttpStatusCode.BadRequest, "obyekt.polygon_nuqtalar_soni");
    }

    [Fact]
    public async Task Uchlarsiz_kopburchak_400_va_kod()
    {
        using var mijoz = await MijozAsync(Permissions.AttendanceManage);

        using var javob = await mijoz.PostAsJsonAsync(
            Yol(""),
            new { name = "Bo'sh " + Belgi(), shape = "Polygon", latitude = 38.4m, longitude = 66.5m, radiusMeters = 200 },
            TestContext.Current.CancellationToken);

        await XatoKutAsync(javob, HttpStatusCode.BadRequest, "obyekt.polygon_nuqtalar_soni");
    }

    /// <summary>Shakl yuborilmasa — jimgina «doira» emas, 400.</summary>
    [Fact]
    public async Task Shaklsiz_tana_400_va_kod()
    {
        using var mijoz = await MijozAsync(Permissions.AttendanceManage);

        using var javob = await mijoz.PostAsJsonAsync(
            Yol(""),
            new { name = "Shaklsiz " + Belgi(), latitude = 38.4m, longitude = 66.5m, radiusMeters = 200 },
            TestContext.Current.CancellationToken);

        await XatoKutAsync(javob, HttpStatusCode.BadRequest, "obyekt.shakl_notogri");
    }

    /// <summary>Doirada radius majburiy (SPEC §C) — sukut 200 endi jimgina qo'yilmaydi.</summary>
    [Fact]
    public async Task Radiussiz_doira_400_va_kod()
    {
        using var mijoz = await MijozAsync(Permissions.AttendanceManage);

        using var javob = await mijoz.PostAsJsonAsync(
            Yol(""),
            new { name = "Radiussiz " + Belgi(), shape = "Circle", latitude = 38.4m, longitude = 66.5m },
            TestContext.Current.CancellationToken);

        await XatoKutAsync(javob, HttpStatusCode.BadRequest, "davomat.obyekt_radiusi");
    }

    [Fact]
    public async Task Oraliqdan_tashqari_zaxira_400_va_kod()
    {
        using var mijoz = await MijozAsync(Permissions.AttendanceManage);

        using var javob = await mijoz.PostAsJsonAsync(
            Yol(""),
            new { name = "Zaxira " + Belgi(), shape = "Polygon", polygon = Kvadrat(38.4m), edgeBufferMeters = 501 },
            TestContext.Current.CancellationToken);

        await XatoKutAsync(javob, HttpStatusCode.BadRequest, "obyekt.polygon_zaxira");
    }

    /// <summary>
    /// Uchda <c>lat</c> tushib qolsa — bog'lashda 400. Aks holda u jimgina 0 bo'lardi va (0, lng)
    /// haqiqiy nuqta sifatida tekshiruvdan o'tishi mumkin edi.
    /// </summary>
    [Fact]
    public async Task Kengligi_tushib_qolgan_uch_400()
    {
        using var mijoz = await MijozAsync(Permissions.AttendanceManage);

        using var javob = await mijoz.PostAsync(
            Yol(""),
            new StringContent(
                """
                {
                  "name": "Chala uch",
                  "shape": "Polygon",
                  "polygon": [ { "lat": 38.4, "lng": 66.49 }, { "lat": 38.4, "lng": 66.51 }, { "lng": 66.5 } ]
                }
                """,
                Encoding.UTF8,
                "application/json"),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    /// <summary><c>attendance.manage</c> ostidagi yozish yo'llari — o'qish ruxsati ularni ochmasligi kerak.</summary>
    private static (HttpMethod Usul, string Yol)[] YozishYollari(Guid id) =>
    [
        (HttpMethod.Put, $"/{id}"),
        (HttpMethod.Post, $"/{id}/deactivate"),
        (HttpMethod.Post, $"/{id}/activate"),
    ];

    private static Uri Yol(string yol) => new("/api/attendance/work-sites" + yol, UriKind.Relative);

    private static string Belgi() => Guid.NewGuid().ToString("N")[..8];

    private static object Doira(string nom)
        => new { name = nom, shape = "Circle", latitude = 38.0m, longitude = 66.5m, radiusMeters = 200 };

    /// <summary>~220 × 175 m kvadrat; markazi (<paramref name="kenglik"/>, 66.5).</summary>
    private static Uch[] Kvadrat(decimal kenglik) =>
    [
        new(kenglik - 0.001m, 66.499m),
        new(kenglik - 0.001m, 66.501m),
        new(kenglik + 0.001m, 66.501m),
        new(kenglik + 0.001m, 66.499m),
    ];

    /// <summary>So'rovdagi bitta uch — JSON da <c>{ "lat": …, "lng": … }</c>.</summary>
    private sealed record Uch(decimal Lat, decimal Lng);

    private static async Task<Guid> YaratAsync(HttpClient mijoz, object tana)
    {
        using var javob = await mijoz.PostAsJsonAsync(Yol(""), tana, TestContext.Current.CancellationToken);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return (await JsonAsync(javob)).GetProperty("id").GetGuid();
    }

    /// <summary>Faqat berilgan ruxsatlar bilan token olgan mijoz.</summary>
    /// <remarks>
    /// <c>AttendanceManagementApiTests.TokenAsync</c> bilan bir xil yo'l: administrator hisobidan,
    /// lekin ruxsatlar ro'yxati QO'LDA — roldan emas.
    /// </remarks>
    // Ruxsatlar endi har so'rovda BAZADAN o'qiladi (2026-09-26, `TestAccounts` izohi):
    // administrator tokeniga yozilgan ro'yxat e'tiborsiz qolardi. Har chaqiruv o'z
    // rolini va hisobini yaratadi.
    private async Task<HttpClient> MijozAsync(params string[] ruxsatlar)
    {
        var mijoz = _factory.CreateClient();
        mijoz.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await TestAccounts.TokenAsync(_factory, ruxsatlar));

        return mijoz;
    }

    private static async Task XatoKutAsync(HttpResponseMessage javob, HttpStatusCode holat, string kod)
    {
        var matn = await MatnAsync(javob);
        javob.StatusCode.ShouldBe(holat, matn);
        JsonDocument.Parse(matn).RootElement.GetProperty("title").GetString().ShouldBe(kod, matn);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage javob)
        => JsonDocument.Parse(await MatnAsync(javob)).RootElement.Clone();

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
