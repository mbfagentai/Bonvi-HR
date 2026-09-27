using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Bonvi.Domain.Identity;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Tekshiruv materiali (test uchun).</summary>
public sealed record CheckFileResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long? SizeBytes,
    DateTimeOffset CreatedAt);

/// <summary>Xodim kartasi — faqat shu testga kerakli maydonlar.</summary>
public sealed record MehnatUzCardResponse(
    Guid Id,
    string? MehnatUzUrl);

/// <summary>
/// mehnat.uz havolasi va «Tekshiruvlar» kartasi materiallarining uchi-uchiga testlari.
/// </summary>
/// <remarks>
/// <para>
/// Eng muhim tekshiruv — <see cref="Materiallar_royxatga_qoshiladi_va_ozgarishsiz_qaytadi"/>:
/// material xodim rasmi quvuridan o'tmasligi KERAK. Rasm quvuri
/// (<c>SquarePhotoStore</c>) faylni markazdan kvadrat qirqib 512×512 JPEG
/// qiladi va u holda skrinshotdagi matn — uning butun qiymati — yo'qoladi.
/// Bu test aynan shu xatoning qaytib kelishini to'sadi: kvadrat bo'lmagan
/// PNG yuboriladi va AYNAN o'sha baytlar qaytishi tekshiriladi.
/// </para>
/// <para>
/// 2026-09-27 dan materiallar RO'YXAT (ilgari bitta skrinshot edi va
/// yangisi eskisini almashtirardi): «фақат битта юклама камлик қилади».
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
    public async Task Materiallar_royxatga_qoshiladi_va_ozgarishsiz_qaytadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var client = await AdminMijozAsync();
        var xodim = await XodimYaratAsync(client, "Материалли");
        var yol = MateriallarUri(xodim.Id);

        // Skrinshot KVADRAT EMAS — bu ataylab: rasm quvuri uni qirqib
        // yuborardi va aynan shu farq testning butun ma'nosi.
        var png = TestImages.PngYasa(320, 120);

        // Skrinshotni PDF bo'lib chop etish HR uchun odatiy yo'l
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\n%%EOF");

        var birinchi = await YuklaAsync(client, yol, png, "mehnat.png", "image/png");
        var ikkinchi = await YuklaAsync(client, yol, pdf, "qarzdorlik.pdf", "application/pdf");

        birinchi.FileName.ShouldBe("mehnat.png");
        birinchi.SizeBytes.ShouldBe(png.Length);
        ikkinchi.ContentType.ShouldBe("application/pdf");

        // Ikkinchi fayl birinchisini ALMASHTIRMAYDI — ro'yxatga qo'shiladi
        var royxat = await RoyxatAsync(client, xodim.Id);
        royxat.Select(material => material.Id).ShouldBe([birinchi.Id, ikkinchi.Id], "eng eskisi birinchi");

        using (var olish = await client.GetAsync(FaylUri(xodim.Id, birinchi.Id), bekorQilish))
        {
            olish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(olish));
            olish.Content.Headers.ContentType!.MediaType.ShouldBe(
                "image/png", "fayl JPEG'ga qayta kodlanmasligi kerak");

            var qaytgan = await olish.Content.ReadAsByteArrayAsync(bekorQilish);
            qaytgan.ShouldBe(png, "material baytlari O'ZGARISHSIZ saqlanishi kerak");
        }

        using (var olish = await client.GetAsync(FaylUri(xodim.Id, ikkinchi.Id), bekorQilish))
        {
            olish.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        }
    }

    /// <summary>
    /// O'chirish YUMSHOQ: material ro'yxatdan chiqadi, yozuv esa bazada qoladi.
    /// </summary>
    [Fact]
    public async Task Material_ochirilsa_royxatdan_chiqadi_lekin_izi_qoladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var client = await AdminMijozAsync();
        var xodim = await XodimYaratAsync(client, "Ўчирилган");
        var material = await YuklaAsync(
            client, MateriallarUri(xodim.Id), TestImages.PngYasa(40, 20), "eski.png", "image/png");

        using (var ochirish = await client.DeleteAsync(MaterialUri(xodim.Id, material.Id), bekorQilish))
        {
            ochirish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(ochirish));
        }

        (await RoyxatAsync(client, xodim.Id)).ShouldBeEmpty();

        using (var olish = await client.GetAsync(FaylUri(xodim.Id, material.Id), bekorQilish))
        {
            olish.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await XatoKodiAsync(olish)).ShouldBe("tekshiruv_fayli.topilmadi");
        }

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        var yozuv = await kontekst.EmployeeCheckFiles
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == material.Id, bekorQilish);

        yozuv.DeletedAt.ShouldNotBeNull("dalil izsiz yo'qolmasligi kerak");
    }

    /// <summary>
    /// Boshqa xodimning materialini marshrutdagi xodim orqali ochib ham,
    /// o'chirib ham bo'lmaydi — javob «topilmadi», «sizniki emas» emas.
    /// </summary>
    [Fact]
    public async Task Boshqa_xodimning_materiali_ochilmaydi_va_ochirilmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var client = await AdminMijozAsync();
        var egasi = await XodimYaratAsync(client, "Эгаси");
        var begona = await XodimYaratAsync(client, "Бегона");
        var material = await YuklaAsync(
            client, MateriallarUri(egasi.Id), TestImages.PngYasa(40, 20), "dalil.png", "image/png");

        using (var olish = await client.GetAsync(FaylUri(begona.Id, material.Id), bekorQilish))
        {
            olish.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await XatoKodiAsync(olish)).ShouldBe("tekshiruv_fayli.topilmadi");
        }

        using (var ochirish = await client.DeleteAsync(MaterialUri(begona.Id, material.Id), bekorQilish))
        {
            ochirish.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        (await RoyxatAsync(client, egasi.Id)).ShouldHaveSingleItem().Id.ShouldBe(material.Id);
    }

    /// <summary>
    /// Ko'rish — <c>employees.view</c>, yuklash va o'chirish — <c>employees.edit</c>.
    /// </summary>
    [Fact]
    public async Task Materiallar_ruxsatlari()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var admin = await AdminMijozAsync();
        var xodim = await XodimYaratAsync(admin, "Рухсатли");
        var material = await YuklaAsync(
            admin, MateriallarUri(xodim.Id), TestImages.PngYasa(40, 20), "dalil.png", "image/png");

        using (var ruxsatsiz = Mijoz(await TestAccounts.TokenAsync(_factory)))
        using (var javob = await ruxsatsiz.GetAsync(MateriallarUri(xodim.Id), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using var korish = Mijoz(await TestAccounts.TokenAsync(_factory, Permissions.EmployeesView));

        (await RoyxatAsync(korish, xodim.Id)).ShouldHaveSingleItem();

        using (var javob = await korish.GetAsync(FaylUri(xodim.Id, material.Id), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (var javob = await FaylYuborAsync(
            korish, MateriallarUri(xodim.Id), TestImages.PngYasa(40, 20), "yangi.png", "image/png"))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var javob = await korish.DeleteAsync(MaterialUri(xodim.Id, material.Id), bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        (await RoyxatAsync(admin, xodim.Id)).ShouldHaveSingleItem("ruxsatsiz so'rovlar hech narsani o'zgartirmagan");
    }

    [Fact]
    public async Task Soxta_tur_bilan_yuborilgan_fayl_qabul_qilinmaydi()
    {
        using var client = await AdminMijozAsync();
        var xodim = await XodimYaratAsync(client, "Сохта");

        var matn = Encoding.UTF8.GetBytes("Bu na PDF, na rasm.");

        using var yuklash = await FaylYuborAsync(client, MateriallarUri(xodim.Id), matn, "mehnat.png", "image/png");

        yuklash.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await XatoKodiAsync(yuklash)).ShouldBe("hujjat.yaroqsiz");
        (await RoyxatAsync(client, xodim.Id)).ShouldBeEmpty();
    }

    /// <summary>
    /// Sxemasiz kiritilgan havolaga <c>https://</c> qo'shiladi.
    /// </summary>
    /// <remarks>
    /// Test ilgari «belgi almashtirilmaydi» degan qo'shimcha tekshiruvni
    /// ham olib yurardi: kartada «mehnat.uz: ha/yo'q» degan alohida javob
    /// bor edi. U olib tashlandi (foydalanuvchi qarori: bir xil savol
    /// ekranda ikki marta so'ralardi) — endi havolaning O'ZI javob ham,
    /// dalil ham.
    /// </remarks>
    [Fact]
    public async Task Havola_sxemasiz_kiritilsa_https_qoshiladi()
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
                mehnatUzUrl = "mehnat.uz/uz/profile/42",
            },
            bekorQilish))
        {
            saqlash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(saqlash));
        }

        var karta = await KartaAsync(client, xodim.Id);

        karta.MehnatUzUrl.ShouldBe("https://mehnat.uz/uz/profile/42");
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

    private static Uri MateriallarUri(Guid xodimId)
        => new($"/api/employees/{xodimId}/check-files", UriKind.Relative);

    private static Uri MaterialUri(Guid xodimId, Guid materialId)
        => new($"/api/employees/{xodimId}/check-files/{materialId}", UriKind.Relative);

    private static Uri FaylUri(Guid xodimId, Guid materialId)
        => new($"/api/employees/{xodimId}/check-files/{materialId}/content", UriKind.Relative);

    private static async Task<CheckFileResponse> YuklaAsync(
        HttpClient client,
        Uri yol,
        byte[] baytlar,
        string fileName,
        string tur)
    {
        using var javob = await FaylYuborAsync(client, yol, baytlar, fileName, tur);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var material = await javob.Content.ReadFromJsonAsync<CheckFileResponse>(TestContext.Current.CancellationToken);
        return material!;
    }

    private static async Task<List<CheckFileResponse>> RoyxatAsync(HttpClient client, Guid xodimId)
    {
        using var javob = await client.GetAsync(MateriallarUri(xodimId), TestContext.Current.CancellationToken);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var royxat = await javob.Content.ReadFromJsonAsync<List<CheckFileResponse>>(
            TestContext.Current.CancellationToken);
        return royxat!;
    }

    private HttpClient Mijoz(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

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
