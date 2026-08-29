using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace Bonvi.Api.IntegrationTests;

/// <summary>mehnat.uz skrinshotini yuklash javobi (test uchun).</summary>
public sealed record MehnatUzScreenshotResponse(string ScreenshotKey);

/// <summary>Xodim kartasi — faqat shu testga kerakli maydonlar.</summary>
public sealed record MehnatUzCardResponse(
    Guid Id,
    string? MehnatUzUrl,
    string? MehnatUzScreenshotKey,
    bool? RegisteredOnMehnatUz);

/// <summary>
/// mehnat.uz havolasi va profil skrinshotining uchi-uchiga testlari.
/// </summary>
/// <remarks>
/// <para>
/// Eng muhim tekshiruv — <see cref="Skrinshot_qirqilmaydi_va_qayta_kodlanmaydi"/>:
/// skrinshot xodim rasmi quvuridan o'tmasligi KERAK. Rasm quvuri
/// (<c>SquarePhotoStore</c>) faylni markazdan kvadrat qirqib 512×512 JPEG
/// qiladi va u holda skrinshotdagi matn — uning butun qiymati — yo'qoladi.
/// Bu test aynan shu xatoning qaytib kelishini to'sadi: kvadrat bo'lmagan
/// PNG yuboriladi va AYNAN o'sha baytlar qaytishi tekshiriladi.
/// </para>
/// <para>
/// <see cref="EmployeeDocumentApiTests"/> bilan bir xil falsafa: mock emas,
/// haqiqiy baytlar server orqali chinakam saqlanadi va qaytariladi.
/// </para>
/// </remarks>
public sealed class MehnatUzApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public MehnatUzApiTests(BonviApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Skrinshot_qirqilmaydi_va_qayta_kodlanmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var client = await AdminMijozAsync();
        var xodim = await XodimYaratAsync(client, "Скриншотли");
        var yol = new Uri($"/api/employees/{xodim.Id}/mehnat-uz-screenshot", UriKind.Relative);

        // Skrinshot KVADRAT EMAS — bu ataylab: rasm quvuri uni qirqib
        // yuborardi va aynan shu farq testning butun ma'nosi.
        var asl = TestImages.PngYasa(320, 120);

        using (var yuklash = await FaylYuborAsync(client, yol, asl, "mehnat.png", "image/png"))
        {
            yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklash));

            var javob = await yuklash.Content.ReadFromJsonAsync<MehnatUzScreenshotResponse>(bekorQilish);
            javob!.ScreenshotKey.ShouldNotBeNullOrWhiteSpace();
        }

        using (var olish = await client.GetAsync(yol, bekorQilish))
        {
            olish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(olish));
            olish.Content.Headers.ContentType!.MediaType.ShouldBe(
                "image/png", "fayl JPEG'ga qayta kodlanmasligi kerak");

            var qaytgan = await olish.Content.ReadAsByteArrayAsync(bekorQilish);
            qaytgan.ShouldBe(asl, "skrinshot baytlari O'ZGARISHSIZ saqlanishi kerak");
        }

        // Kalit kartada ham ko'rinadi — interfeys so'rovni shunga qarab yuboradi
        var karta = await KartaAsync(client, xodim.Id);
        karta.MehnatUzScreenshotKey.ShouldNotBeNull();

        // O'chirish — idempotent
        using (var ochirish = await client.DeleteAsync(yol, bekorQilish))
        {
            ochirish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(ochirish));
        }

        using (var ikkinchiOchirish = await client.DeleteAsync(yol, bekorQilish))
        {
            ikkinchiOchirish.StatusCode.ShouldBe(
                HttpStatusCode.NoContent, "amal idempotent: fayl yo'q bo'lsa ham muvaffaqiyat");
        }

        using (var ochirilgandanKeyin = await client.GetAsync(yol, bekorQilish))
        {
            ochirilgandanKeyin.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await XatoKodiAsync(ochirilgandanKeyin)).ShouldBe("xodim.mehnat_rasm_yoq");
        }
    }

    [Fact]
    public async Task Skrinshot_pdf_bolib_ham_saqlanadi()
    {
        // Skrinshotni PDF bo'lib chop etish HR uchun odatiy yo'l —
        // hujjat quvuri uni allaqachon qabul qiladi.
        using var client = await AdminMijozAsync();
        var xodim = await XodimYaratAsync(client, "ПДФли");
        var yol = new Uri($"/api/employees/{xodim.Id}/mehnat-uz-screenshot", UriKind.Relative);

        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\n%%EOF");

        using var yuklash = await FaylYuborAsync(client, yol, pdf, "mehnat.pdf", "application/pdf");
        yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklash));

        using var olish = await client.GetAsync(yol, TestContext.Current.CancellationToken);
        olish.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
    }

    [Fact]
    public async Task Soxta_tur_bilan_yuborilgan_fayl_qabul_qilinmaydi()
    {
        using var client = await AdminMijozAsync();
        var xodim = await XodimYaratAsync(client, "Сохта");
        var yol = new Uri($"/api/employees/{xodim.Id}/mehnat-uz-screenshot", UriKind.Relative);

        var matn = Encoding.UTF8.GetBytes("Bu na PDF, na rasm.");

        using var yuklash = await FaylYuborAsync(client, yol, matn, "mehnat.png", "image/png");

        yuklash.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await XatoKodiAsync(yuklash)).ShouldBe("hujjat.yaroqsiz");
    }

    [Fact]
    public async Task Havola_sxemasiz_kiritilsa_https_qoshiladi_va_belgi_ozgarmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var client = await AdminMijozAsync();
        var xodim = await XodimYaratAsync(client, "Ҳаволали");

        using (var saqlash = await client.PutAsJsonAsync(
            new Uri($"/api/employees/{xodim.Id}", UriKind.Relative),
            new
            {
                lastName = "Ҳаволали",
                firstName = "Ходим",
                registeredOnMehnatUz = true,
                mehnatUzUrl = "mehnat.uz/uz/profile/42",
            },
            bekorQilish))
        {
            saqlash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(saqlash));
        }

        var karta = await KartaAsync(client, xodim.Id);

        karta.MehnatUzUrl.ShouldBe("https://mehnat.uz/uz/profile/42");

        // Excel'dan kelgan uch holatli belgi ALMASHTIRILMAYDI — havola
        // uning o'rniga emas, yoniga qo'shiladi.
        karta.RegisteredOnMehnatUz.ShouldBe(true);
    }

    [Fact]
    public async Task Shifrlanmagan_havola_rad_etiladi()
    {
        using var client = await AdminMijozAsync();
        var xodim = await XodimYaratAsync(client, "Шифрсиз");

        using var saqlash = await client.PutAsJsonAsync(
            new Uri($"/api/employees/{xodim.Id}", UriKind.Relative),
            new
            {
                lastName = "Шифрсиз",
                firstName = "Ходим",
                mehnatUzUrl = "http://mehnat.uz/profile",
            },
            TestContext.Current.CancellationToken);

        saqlash.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await XatoKodiAsync(saqlash)).ShouldBe("xodim.mehnat_havola");
    }

    // ────────────────────────────────────────────────────────────
    //  Muhit tayyorlash
    // ────────────────────────────────────────────────────────────

    private static async Task<MehnatUzCardResponse> KartaAsync(HttpClient client, Guid xodimId)
    {
        using var javob = await client.GetAsync(
            new Uri($"/api/employees/{xodimId}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        // Karta konvert ichida: `{ employee: {...}, employments: [...] }`
        var konvert = await javob.Content.ReadFromJsonAsync<CardEnvelope>(
            TestContext.Current.CancellationToken);

        return konvert!.Employee;
    }

    private sealed record CardEnvelope(MehnatUzCardResponse Employee);

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

    private static async Task<HttpResponseMessage> FaylYuborAsync(
        HttpClient client,
        Uri yol,
        byte[] baytlar,
        string fileName,
        string tur)
    {
        using var forma = new MultipartFormDataContent();
        using var qism = new ByteArrayContent(baytlar);
        qism.Headers.ContentType = new MediaTypeHeaderValue(tur);

        forma.Add(qism, "file", fileName);

        return await client.PostAsync(yol, forma, TestContext.Current.CancellationToken);
    }

    private static async Task<string?> XatoKodiAsync(HttpResponseMessage javob)
    {
        var muammo = await javob.Content.ReadFromJsonAsync<ProblemResponse>(TestContext.Current.CancellationToken);
        return muammo?.Title;
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
