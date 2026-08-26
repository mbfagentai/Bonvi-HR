using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Bonvi.Domain.Catalog;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Xodim hujjati (test uchun).</summary>
public sealed record EmployeeDocumentResponse(
    Guid Id,
    Guid EmployeeId,
    Guid DocumentTypeId,
    string? DocumentTypeName,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Note,
    DateTimeOffset UploadedAt);

/// <summary>
/// Xodim hujjatlarining uchi-uchiga testlari: yuklash, ro'yxat, yuklab olish,
/// tuzatish, o'chirish (S8/M9).
/// </summary>
/// <remarks>
/// <see cref="EmployeePhotoApiTests"/> bilan bir xil falsafa: mock emas,
/// haqiqiy baytlar — PDF imzosi va haqiqiy PNG (<see cref="TestImages"/>)
/// server orqali chinakam saqlanadi va qaytariladi.
/// </remarks>
public sealed class EmployeeDocumentApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public EmployeeDocumentApiTests(BonviApiFactory factory) => _factory = factory;

    // ────────────────────────────────────────────────────────────
    //  To'liq hayot sikli
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Hujjatning_toliq_hayot_sikli_ishlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var client = await AdminMijozAsync();
        var xodim = await XodimYaratAsync(client, "Хужжатли");
        var turi = await TurYaratAsync("Мехнат шартномаси");
        var yol = new Uri($"/api/employees/{xodim.Id}/documents", UriKind.Relative);

        // 1. Yuklash — haqiqiy PDF imzosi bilan
        Guid hujjatId;

        using (var yuklash = await HujjatYuborAsync(
            client, yol, PdfYasa(), "Шартнома 2026.pdf", "application/pdf", turi.Id, "Asl nusxa"))
        {
            yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklash));

            var yuklangan = await yuklash.Content.ReadFromJsonAsync<EmployeeDocumentResponse>(bekorQilish);
            yuklangan.ShouldNotBeNull();
            yuklangan.DocumentTypeName.ShouldBe(turi.Name);
            yuklangan.FileName.ShouldBe("Шартнома 2026.pdf");
            yuklangan.ContentType.ShouldBe("application/pdf");
            yuklangan.Note.ShouldBe("Asl nusxa");

            hujjatId = yuklangan.Id;
        }

        // 2. Ro'yxat — yuklangan hujjat ko'rinishi kerak
        using (var royxat = await client.GetAsync(yol, bekorQilish))
        {
            var qatorlar = await royxat.Content.ReadFromJsonAsync<IReadOnlyList<EmployeeDocumentResponse>>(
                bekorQilish);
            qatorlar!.ShouldContain(q => q.Id == hujjatId);
        }

        // 3. Yuklab olish — mazmun aynan bir xil va asl nom qaytishi kerak
        var faylYoli = new Uri($"/api/employees/{xodim.Id}/documents/{hujjatId}/file", UriKind.Relative);

        using (var yuklabOlish = await client.GetAsync(faylYoli, bekorQilish))
        {
            yuklabOlish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklabOlish));
            yuklabOlish.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");

            yuklabOlish.Content.Headers.ContentDisposition.ShouldNotBeNull();
            yuklabOlish.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");

            // Kirill harflari bor asl nom — RFC 5987 (`filename*`) orqali
            // to'g'ri qaytishi kerak, oddiy `filename` esa mos ASCII zaxira.
            yuklabOlish.Content.Headers.ContentDisposition.FileNameStar.ShouldBe("Шартнома 2026.pdf");

            var baytlar = await yuklabOlish.Content.ReadAsByteArrayAsync(bekorQilish);
            baytlar.ShouldBe(PdfYasa(), "diskka yozilgan mazmun ASL baytlarning O'ZI bo'lishi kerak");
        }

        // 4. Tuzatish — faqat tur va izoh, fayl O'ZGARMAYDI
        var yangiTuri = await TurYaratAsync("Buyruq nusxasi");

        using (var tuzatish = await client.PutAsJsonAsync(
            new Uri($"/api/employees/{xodim.Id}/documents/{hujjatId}", UriKind.Relative),
            new { documentTypeId = yangiTuri.Id, note = "Tuzatildi" },
            bekorQilish))
        {
            tuzatish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tuzatish));

            var tuzatilgan = await tuzatish.Content.ReadFromJsonAsync<EmployeeDocumentResponse>(bekorQilish);
            tuzatilgan!.DocumentTypeName.ShouldBe(yangiTuri.Name);
            tuzatilgan.Note.ShouldBe("Tuzatildi");
            tuzatilgan.FileName.ShouldBe("Шартнома 2026.pdf", "tuzatish fayl nomiga tegmasligi kerak");
        }

        // 5. O'chirish — yumshoq: ro'yxatda ko'rinmay qoladi
        using (var ochirish = await client.DeleteAsync(
            new Uri($"/api/employees/{xodim.Id}/documents/{hujjatId}", UriKind.Relative), bekorQilish))
        {
            ochirish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(ochirish));
        }

        using (var royxatKeyin = await client.GetAsync(yol, bekorQilish))
        {
            var qatorlar = await royxatKeyin.Content.ReadFromJsonAsync<IReadOnlyList<EmployeeDocumentResponse>>(
                bekorQilish);
            qatorlar!.ShouldNotContain(
                q => q.Id == hujjatId, "yumshoq o'chirilgan hujjat ro'yxatda chiqmasligi kerak");
        }

        using (var ochirilgandanKeyin = await client.GetAsync(faylYoli, bekorQilish))
        {
            ochirilgandanKeyin.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await XatoKodiAsync(ochirilgandanKeyin)).ShouldBe("hujjat.topilmadi");
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Mazmun bo'yicha tekshiruv
    // ────────────────────────────────────────────────────────────

    /// <summary>Rasm ham (haqiqiy PNG) qabul qilinadi — faqat PDF emas.</summary>
    [Fact]
    public async Task Rasm_ham_hujjat_sifatida_qabul_qilinadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var client = await AdminMijozAsync();
        var xodim = await XodimYaratAsync(client, "Расмли");
        var turi = await TurYaratAsync("Pasport nusxasi");
        var yol = new Uri($"/api/employees/{xodim.Id}/documents", UriKind.Relative);

        using var yuklash = await HujjatYuborAsync(
            client, yol, TestImages.PngYasa(16, 16), "pasport.png", "image/png", turi.Id);

        yuklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuklash));

        var yuklangan = await yuklash.Content.ReadFromJsonAsync<EmployeeDocumentResponse>(bekorQilish);
        yuklangan!.ContentType.ShouldBe("image/png");
    }

    /// <summary>
    /// Mijoz «application/pdf» deb e'lon qiladi, lekin ichida oddiy matn —
    /// mazmun tekshiruvi buni to'xtatishi kerak.
    /// </summary>
    [Fact]
    public async Task Soxta_tur_bilan_yuborilgan_fayl_qabul_qilinmaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        using var client = await AdminMijozAsync();
        var xodim = await XodimYaratAsync(client, "Сохта");
        var turi = await TurYaratAsync("Boshqa");
        var yol = new Uri($"/api/employees/{xodim.Id}/documents", UriKind.Relative);

        var matn = Encoding.UTF8.GetBytes("Bu na PDF, na rasm — oddiy matn.");

        using var yuklash = await HujjatYuborAsync(client, yol, matn, "hujjat.pdf", "application/pdf", turi.Id);

        yuklash.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await XatoKodiAsync(yuklash)).ShouldBe("hujjat.yaroqsiz");

        using var royxat = await client.GetAsync(yol, bekorQilish);
        var qatorlar = await royxat.Content.ReadFromJsonAsync<IReadOnlyList<EmployeeDocumentResponse>>(bekorQilish);
        qatorlar!.ShouldBeEmpty("hech qanday fayl saqlanmagan bo'lishi kerak");
    }

    // ────────────────────────────────────────────────────────────
    //  Topilmagan holatlar
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Nomavjud_xodimga_yuklash_404_qaytaradi()
    {
        using var client = await AdminMijozAsync();
        var turi = await TurYaratAsync("Boshqa 2");

        var notanish = new Uri($"/api/employees/{Guid.CreateVersion7()}/documents", UriKind.Relative);

        using var yuklash = await HujjatYuborAsync(client, notanish, PdfYasa(), "a.pdf", "application/pdf", turi.Id);

        yuklash.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await XatoKodiAsync(yuklash)).ShouldBe("xodim.topilmadi");
    }

    [Fact]
    public async Task Nomavjud_turga_yuklash_404_qaytaradi()
    {
        using var client = await AdminMijozAsync();
        var xodim = await XodimYaratAsync(client, "Турсиз");
        var yol = new Uri($"/api/employees/{xodim.Id}/documents", UriKind.Relative);

        using var yuklash = await HujjatYuborAsync(
            client, yol, PdfYasa(), "a.pdf", "application/pdf", Guid.CreateVersion7());

        yuklash.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await XatoKodiAsync(yuklash)).ShouldBe("hujjat.turi_topilmadi");
    }

    // ────────────────────────────────────────────────────────────
    //  Muhit tayyorlash
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal, lekin HAQIQIY PDF imzosi bilan boshlanadigan fayl.
    /// </summary>
    /// <remarks>
    /// To'liq PDF tuzilmasi shart emas: server PDF'ni hech qachon parslamaydi
    /// (<c>FileContentInspector</c> izohiga qarang) — faqat <c>%PDF-</c>
    /// imzosini tekshiradi.
    /// </remarks>
    private static byte[] PdfYasa() => Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\n%%EOF");

    private async Task<(Guid Id, string Name)> TurYaratAsync(string nomOldindanIzohi)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var belgi = Guid.NewGuid().ToString("N")[..6];
        var turi = DocumentType.Create($"{nomOldindanIzohi} {belgi}").Value;

        kontekst.DocumentTypes.Add(turi);
        await kontekst.SaveChangesAsync(bekorQilish);

        return (turi.Id, turi.Name);
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

    /// <summary>Faylni «multipart/form-data» bo'lib, qo'shimcha maydonlar bilan yuboradi.</summary>
    private static async Task<HttpResponseMessage> HujjatYuborAsync(
        HttpClient client,
        Uri yol,
        byte[] baytlar,
        string fileName,
        string tur,
        Guid documentTypeId,
        string? note = null)
    {
        using var forma = new MultipartFormDataContent();
        using var qism = new ByteArrayContent(baytlar);
        qism.Headers.ContentType = new MediaTypeHeaderValue(tur);

        forma.Add(qism, "file", fileName);
        forma.Add(new StringContent(documentTypeId.ToString()), "documentTypeId");

        if (note is not null)
        {
            forma.Add(new StringContent(note), "note");
        }

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
