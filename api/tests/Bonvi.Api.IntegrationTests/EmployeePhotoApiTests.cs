using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Xodim kartasi (test uchun — faqat kerakli maydonlar).</summary>
public sealed record EmployeeResponse(Guid Id, string FullName);

/// <summary>Yuklangan rasmning saqlash kaliti (test uchun).</summary>
public sealed record EmployeePhotoResponse(string PhotoKey);

/// <summary>Xato javobi — <c>ProblemDetails</c> (test uchun).</summary>
public sealed record ProblemResponse(int Status, string Title, string? Detail);

/// <summary>
/// Xodim rasmining uchi-uchiga testlari: yuklash, berish, o'chirish.
/// </summary>
/// <remarks>
/// <para>
/// Bu yerda tekshiriladigan narsalarning hech biri mock bilan ko'rinmaydi:
/// haqiqiy PNG SkiaSharp orqali ochiladi, JPEG'ga qayta kodlanadi, diskka
/// yoziladi va HTTP orqali qaytariladi. Ya'ni test yig'ilgan zanjirning
/// HAMMASINI — nativ kutubxona konteynerda mavjudligini ham — mahkamlaydi.
/// </para>
/// <para>
/// Manba rasm kodda yasaladi (<see cref="TestImages"/>): tashqi fayl testni
/// repozitoriyning holatiga bog'lab qo'yardi va Docker tasviriga nusxalash
/// unutilishi mumkin edi.
/// </para>
/// </remarks>
public sealed class EmployeePhotoApiTests : IClassFixture<BonviApiFactory>
{
    /// <summary>Serverdan kutiladigan tomon o'lchami.</summary>
    private const int KutilganTomon = 512;

    private readonly BonviApiFactory _factory;

    public EmployeePhotoApiTests(BonviApiFactory factory) => _factory = factory;

    // ────────────────────────────────────────────────────────────
    //  To'liq hayot sikli
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Yuklash → berish → keshni tekshirish → o'chirish.
    /// </summary>
    /// <remarks>
    /// Bosqichlar ataylab bitta testda: ular bir-birining natijasiga
    /// tayanadi (kalitsiz «berish» ni, rasm yuklamasdan «o'chirish» ni
    /// tekshirib bo'lmaydi) va alohida testlar bir xil tayyorgarlikni
    /// takrorlagan bo'lardi.
    /// </remarks>
    [Fact]
    public async Task Rasmning_toliq_hayot_sikli_ishlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var client = await AdminMijozAsync();
        var xodim = await XodimYaratAsync(client, "Суратли");
        var yol = new Uri($"/api/employees/{xodim.Id}/photo", UriKind.Relative);

        // 1. Yuklash — 40×20 PNG, ya'ni ATAYLAB kvadrat emas
        using (var yuklash = await RasmYuborAsync(client, yol, TestImages.PngYasa(40, 20), "image/png"))
        {
            yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklash));

            var yuklangan = await yuklash.Content.ReadFromJsonAsync<EmployeePhotoResponse>(bekorQilish);

            yuklangan.ShouldNotBeNull();
            yuklangan.PhotoKey.ShouldNotBeNullOrWhiteSpace();

            // Kalitni server yasaydi: sana bo'yicha papka va JPEG kengaytmasi.
            // Foydalanuvchi bergan «surat.png» nomi unda ko'rinmasligi SHART.
            yuklangan.PhotoKey.ShouldEndWith(".jpg");
            yuklangan.PhotoKey.ShouldNotContain("surat");
        }

        // 2. Berish — mazmun haqiqatan JPEG va 512×512 bo'lishi kerak
        string belgi;

        using (var oqish = await client.GetAsync(yol, bekorQilish))
        {
            oqish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(oqish));
            oqish.Content.Headers.ContentType.ShouldNotBeNull();
            oqish.Content.Headers.ContentType.MediaType.ShouldBe("image/jpeg");

            oqish.Headers.CacheControl.ShouldNotBeNull();
            oqish.Headers.CacheControl.Private.ShouldBeTrue("surat — shaxsiy ma'lumot");
            oqish.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromDays(1));

            oqish.Headers.ETag.ShouldNotBeNull();
            belgi = oqish.Headers.ETag.Tag;

            var baytlar = await oqish.Content.ReadAsByteArrayAsync(bekorQilish);

            // JPEG imzosi: PNG yuborilgan bo'lsa ham javob qayta kodlangan
            baytlar.Length.ShouldBeGreaterThan(0);
            baytlar[0].ShouldBe((byte)0xFF);
            baytlar[1].ShouldBe((byte)0xD8);

            var (eni, boyi) = TestImages.JpegOlchami(baytlar);
            eni.ShouldBe(KutilganTomon);
            boyi.ShouldBe(KutilganTomon, "markazdan kvadrat qirqilishi kerak");
        }

        // 3. Kesh — o'sha belgi bilan qayta so'ralganda mazmun yuborilmaydi
        using (var soorov = new HttpRequestMessage(HttpMethod.Get, yol))
        {
            soorov.Headers.TryAddWithoutValidation("If-None-Match", belgi);

            using var javob = await client.SendAsync(soorov, bekorQilish);

            javob.StatusCode.ShouldBe(HttpStatusCode.NotModified);
            javob.Headers.ETag.ShouldNotBeNull();
            javob.Headers.ETag.Tag.ShouldBe(belgi);
        }

        // 4. O'chirish
        using (var ochirish = await client.DeleteAsync(yol, bekorQilish))
        {
            ochirish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(ochirish));
        }

        using (var qaytaOchirish = await client.DeleteAsync(yol, bekorQilish))
        {
            qaytaOchirish.StatusCode.ShouldBe(
                HttpStatusCode.NoContent,
                "amal idempotent: rasm yo'q bo'lsa ham xato emas");
        }

        using (var ochirilgandanKeyin = await client.GetAsync(yol, bekorQilish))
        {
            ochirilgandanKeyin.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await XatoKodiAsync(ochirilgandanKeyin)).ShouldBe("xodim.rasm_yoq");
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Rasm bo'lmagan fayl
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Rasm bo'lmagan fayl ikki bosqichda ham to'xtatiladi.
    /// </summary>
    /// <remarks>
    /// Ikkinchi holat asosiysi: mijoz turni «image/png» deb E'LON QILADI,
    /// lekin ichida oddiy matn. Agar server sarlavhaga ishonsa, saqlash
    /// jildiga rasm bo'lmagan fayl tushib qolardi. Shu sababli hal qiluvchi
    /// tekshiruv mazmun bo'yicha bajariladi.
    /// </remarks>
    [Fact]
    public async Task Rasm_bolmagan_fayl_qabul_qilinmaydi()
    {
        using var client = await AdminMijozAsync();
        var xodim = await XodimYaratAsync(client, "Матнли");
        var yol = new Uri($"/api/employees/{xodim.Id}/photo", UriKind.Relative);

        var matn = Encoding.UTF8.GetBytes("Bu rasm emas, oddiy matn.");

        using (var eLonQilinganTur = await RasmYuborAsync(client, yol, matn, "text/plain"))
        {
            eLonQilinganTur.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await XatoKodiAsync(eLonQilinganTur)).ShouldBe("xodim.rasm_turi");
        }

        using (var soxtaTur = await RasmYuborAsync(client, yol, matn, "image/png"))
        {
            soxtaTur.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await XatoKodiAsync(soxtaTur)).ShouldBe("xodim.rasm_yaroqsiz");
        }

        // Hech qanday fayl saqlanmagan bo'lishi kerak
        using var oqish = await client.GetAsync(yol, TestContext.Current.CancellationToken);
        oqish.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ────────────────────────────────────────────────────────────
    //  Topilmagan holatlar
    // ────────────────────────────────────────────────────────────

    /// <summary>«Xodim yo'q» va «rasm yo'q» — ikki xil xato.</summary>
    [Fact]
    public async Task Xodim_va_rasm_topilmagan_holatlar_ajratiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var client = await AdminMijozAsync();

        var notanish = new Uri($"/api/employees/{Guid.CreateVersion7()}/photo", UriKind.Relative);

        using (var oqish = await client.GetAsync(notanish, bekorQilish))
        {
            oqish.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await XatoKodiAsync(oqish)).ShouldBe("xodim.topilmadi");
        }

        using (var ochirish = await client.DeleteAsync(notanish, bekorQilish))
        {
            ochirish.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await XatoKodiAsync(ochirish)).ShouldBe("xodim.topilmadi");
        }

        using (var yuklash = await RasmYuborAsync(client, notanish, TestImages.PngYasa(8, 8), "image/png"))
        {
            yuklash.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await XatoKodiAsync(yuklash)).ShouldBe("xodim.topilmadi");
        }

        // Xodim bor, lekin rasmi hech qachon yuklanmagan
        var xodim = await XodimYaratAsync(client, "Сурацсиз");

        using var rasmsiz = await client.GetAsync(
            new Uri($"/api/employees/{xodim.Id}/photo", UriKind.Relative),
            bekorQilish);

        rasmsiz.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await XatoKodiAsync(rasmsiz)).ShouldBe("xodim.rasm_yoq");
    }

    // ────────────────────────────────────────────────────────────
    //  Kartani tahrirlash bilan chegara
    // ────────────────────────────────────────────────────────────

    /// <summary>Kartani tahrirlash rasmni O'CHIRMAYDI.</summary>
    /// <remarks>
    /// <para>
    /// Rasm alohida endpoint bilan boshqariladi, ya'ni tahrirlash
    /// formasida u umuman yo'q. Shu sababli «photoKey» yuborilmagan
    /// <c>PUT</c> so'rovi suratga tegmasligi kerak.
    /// </para>
    /// <para>
    /// Bu test bir marta yiqilgan holatni qadaydi: kalit shartsiz
    /// o'rnatilganda HR familiyadagi bitta harfni tuzatib, xodimni
    /// suratidan ayirardi — va bu jimgina, xatosiz sodir bo'lardi.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Kartani_tahrirlash_rasmni_ochirmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var client = await AdminMijozAsync();
        var xodim = await XodimYaratAsync(client, "Суратли");
        var rasmYoli = new Uri($"/api/employees/{xodim.Id}/photo", UriKind.Relative);

        string kalit;

        using (var yuklash = await RasmYuborAsync(client, rasmYoli, TestImages.PngYasa(32, 32), "image/png"))
        {
            yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklash));

            var yuklangan = await yuklash.Content.ReadFromJsonAsync<EmployeePhotoResponse>(bekorQilish);
            yuklangan.ShouldNotBeNull();
            kalit = yuklangan.PhotoKey;
        }

        // Tanada «photoKey» ATAYLAB yo'q — aynan shu holat rasmni yo'qotardi
        using (var tahrir = await client.PutAsJsonAsync(
            new Uri($"/api/employees/{xodim.Id}", UriKind.Relative),
            new { lastName = "Тахрирланган", firstName = "Ходим" },
            bekorQilish))
        {
            tahrir.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tahrir));

            // Javob — xodim kartasi, lekin bu yerda undan faqat «photoKey»
            // kerak, shu sababli o'sha bitta maydonli shakl qayta ishlatiladi.
            var karta = await tahrir.Content.ReadFromJsonAsync<EmployeePhotoResponse>(bekorQilish);
            karta.ShouldNotBeNull();
            karta.PhotoKey.ShouldBe(kalit, "tahrir kalitni o'zgartirmasligi kerak");
        }

        // Fayl ham joyida: kalit qolib, surat o'chib ketmaganiga ishonch
        using var rasm = await client.GetAsync(rasmYoli, bekorQilish);
        rasm.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(rasm));
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar — HTTP
    // ────────────────────────────────────────────────────────────

    private async Task<HttpClient> AdminMijozAsync()
    {
        var client = _factory.CreateClient();

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = BonviApiFactory.TestAdminLogin, password = BonviApiFactory.TestAdminPassword },
            TestContext.Current.CancellationToken);

        javob.EnsureSuccessStatusCode();

        var auth = await javob.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    private static async Task<EmployeeResponse> XodimYaratAsync(HttpClient client, string familiya)
    {
        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/employees", UriKind.Relative),
            new
            {
                lastName = familiya + " " + Guid.NewGuid().ToString("N")[..6],
                firstName = "Ходим",
            },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var xodim = await javob.Content.ReadFromJsonAsync<EmployeeResponse>(TestContext.Current.CancellationToken);

        return xodim!;
    }

    /// <summary>Faylni «multipart/form-data» bo'lib, «file» maydonida yuboradi.</summary>
    private static async Task<HttpResponseMessage> RasmYuborAsync(
        HttpClient client,
        Uri yol,
        byte[] baytlar,
        string tur)
    {
        using var forma = new MultipartFormDataContent();
        using var qism = new ByteArrayContent(baytlar);

        qism.Headers.ContentType = new MediaTypeHeaderValue(tur);

        // Fayl nomi ataylab beriladi: u javobdagi kalitga TUSHMASLIGI kerak.
        forma.Add(qism, "file", "surat.png");

        return await client.PostAsync(yol, forma, TestContext.Current.CancellationToken);
    }

    /// <summary>Xato javobidan kodni oladi — tekshiruv aniq bo'lishi uchun.</summary>
    private static async Task<string?> XatoKodiAsync(HttpResponseMessage javob)
    {
        var muammo = await javob.Content.ReadFromJsonAsync<ProblemResponse>(
            TestContext.Current.CancellationToken);

        return muammo?.Title;
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
