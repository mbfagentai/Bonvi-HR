using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Ma'lumotnoma yozuvi (test uchun).</summary>
public sealed record CatalogItemResponse(
    Guid Id,
    string Name,
    string? Code,
    int SortOrder,
    bool IsActive,
    string? Note,
    string? TaxId,
    string? FullName);

/// <summary>Bo'lim (test uchun).</summary>
public sealed record DepartmentResponse(
    Guid Id,
    string Name,
    Guid? ParentId,
    int Level,
    string? Path);

/// <summary>Bo'limlar daraxtining tuguni (test uchun).</summary>
public sealed record DepartmentNodeResponse(
    Guid Id,
    string Name,
    Guid? ParentId,
    int Level,
    string? Path,
    IReadOnlyList<DepartmentNodeResponse> Children);

/// <summary>Lavozim vazifasi (test uchun).</summary>
public sealed record DutyResponse(Guid Id, string Text, int SortOrder);

/// <summary>Lavozim, vazifalari bilan (test uchun).</summary>
public sealed record PositionResponse(
    Guid Id,
    string Name,
    int Grade,
    int? ProbationDays,
    Guid? DepartmentId,
    IReadOnlyList<DutyResponse> Duties);

/// <summary>
/// Ma'lumotnomalar HTTP qatlamining uchi-uchiga testlari.
/// </summary>
/// <remarks>
/// <para>
/// Haqiqiy PostgreSQL ustida ishlaydi. Aynan shu yerda ko'rinadigan narsalar:
/// <c>lower(name)</c> bo'yicha unikal indeks, rekursiv CTE bilan qurilgan
/// ierarxiya va tashqi kalit cheklovlari — ularning birortasi ham mock
/// repozitoriyda tekshirilmaydi.
/// </para>
/// <para>
/// Har bir testda nomlar NOYOB qilib olinadi: baza test sinfi uchun bitta
/// va urug'lantirilgan ma'lumotnomalar ham unda bo'lishi mumkin.
/// </para>
/// </remarks>
public sealed class CatalogApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public CatalogApiTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>Test doirasida takrorlanmaydigan nom.</summary>
    private static string NoyobNom(string asos) => asos + " " + Guid.NewGuid().ToString("N")[..8];

    // ────────────────────────────────────────────────────────────
    //  Oddiy ma'lumotnoma — to'liq CRUD oqimi
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Malumotnomaning_toliq_hayot_sikli_ishlaydi()
    {
        using var client = await AdminMijozAsync();
        var nom = NoyobNom("VIKOR MChJ");

        // 1. Yaratish
        using var yaratish = await client.PostAsJsonAsync(
            new Uri("/api/catalog/legal-entities", UriKind.Relative),
            new { name = nom, code = "VKR", taxId = "123456789", sortOrder = 5 },
            TestContext.Current.CancellationToken);

        yaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yaratish));

        var yaratilgan = await yaratish.Content.ReadFromJsonAsync<CatalogItemResponse>(
            TestContext.Current.CancellationToken);

        yaratilgan.ShouldNotBeNull();
        yaratilgan.Name.ShouldBe(nom);
        yaratilgan.TaxId.ShouldBe("123456789");
        yaratilgan.IsActive.ShouldBeTrue();

        // 2. Bittasini o'qish
        var yol = new Uri($"/api/catalog/legal-entities/{yaratilgan.Id}", UriKind.Relative);

        using var oqish = await client.GetAsync(yol, TestContext.Current.CancellationToken);
        oqish.StatusCode.ShouldBe(HttpStatusCode.OK);

        // 3. Tahrirlash
        var yangiNom = NoyobNom("VIKOR plus");

        using var tahrir = await client.PutAsJsonAsync(
            yol,
            new { name = yangiNom, code = "VKR", fullName = "«VIKOR» MChJ" },
            TestContext.Current.CancellationToken);

        tahrir.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tahrir));

        var tahrirlangan = await tahrir.Content.ReadFromJsonAsync<CatalogItemResponse>(
            TestContext.Current.CancellationToken);

        tahrirlangan.ShouldNotBeNull();
        tahrirlangan.Name.ShouldBe(yangiNom);
        tahrirlangan.FullName.ShouldBe("«VIKOR» MChJ");

        // 4. Ro'yxatda ko'rinishi
        using var royxat = await client.GetAsync(
            new Uri("/api/catalog/legal-entities", UriKind.Relative),
            TestContext.Current.CancellationToken);

        var hammasi = await royxat.Content.ReadFromJsonAsync<List<CatalogItemResponse>>(
            TestContext.Current.CancellationToken);

        hammasi.ShouldNotBeNull();
        hammasi.ShouldContain(x => x.Id == yaratilgan.Id);

        // 5. Faolsizlantirish — yozuv o'chirilmaydi, yashiriladi
        using var faolsiz = await client.PostAsync(
            new Uri(
                $"/api/catalog/legal-entities/{yaratilgan.Id}/deactivate?reason={Uri.EscapeDataString("Firma tugatildi")}",
                UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        faolsiz.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(faolsiz));

        using var faolsizdan = await client.GetAsync(yol, TestContext.Current.CancellationToken);
        var yashirin = await faolsizdan.Content.ReadFromJsonAsync<CatalogItemResponse>(
            TestContext.Current.CancellationToken);

        yashirin.ShouldNotBeNull();
        yashirin.IsActive.ShouldBeFalse();
        yashirin.Note.ShouldBe("Firma tugatildi");

        // Faqat faol yozuvlar so'ralganda ro'yxatga tushmasligi kerak
        using var faollar = await client.GetAsync(
            new Uri("/api/catalog/legal-entities?onlyActive=true", UriKind.Relative),
            TestContext.Current.CancellationToken);

        var faolRoyxat = await faollar.Content.ReadFromJsonAsync<List<CatalogItemResponse>>(
            TestContext.Current.CancellationToken);

        faolRoyxat.ShouldNotBeNull();
        faolRoyxat.ShouldNotContain(x => x.Id == yaratilgan.Id);

        // 6. Qayta yoqish
        using var faollash = await client.PostAsync(
            new Uri($"/api/catalog/legal-entities/{yaratilgan.Id}/activate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        faollash.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Takroriy_nom_409_va_nom_band_kodini_beradi()
    {
        using var client = await AdminMijozAsync();
        var nom = NoyobNom("Velo union");

        using var birinchi = await client.PostAsJsonAsync(
            new Uri("/api/catalog/directions", UriKind.Relative),
            new { name = nom },
            TestContext.Current.CancellationToken);

        birinchi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(birinchi));

        // Registr va ortiqcha bo'shliqlar farq qilmaydi — bu bitta va o'sha nom
        using var ikkinchi = await client.PostAsJsonAsync(
            new Uri("/api/catalog/directions", UriKind.Relative),
            new { name = "  " + nom.ToUpperInvariant() + "  " },
            TestContext.Current.CancellationToken);

        ikkinchi.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await MatnAsync(ikkinchi)).ShouldContain("malumotnoma.nom_band");
    }

    [Fact]
    public async Task Bosh_nom_400_va_kod_qaytaradi()
    {
        using var client = await AdminMijozAsync();

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/catalog/award-types", UriKind.Relative),
            new { name = "   " },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await MatnAsync(javob)).ShouldContain("malumotnoma.nom_bosh");
    }

    [Fact]
    public async Task Notogri_daraja_lavozim_kodi_bilan_rad_etiladi()
    {
        using var client = await AdminMijozAsync();

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/catalog/positions", UriKind.Relative),
            new { name = NoyobNom("Qorovul"), grade = 9 },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await MatnAsync(javob)).ShouldContain("lavozim.daraja");
    }

    // ────────────────────────────────────────────────────────────
    //  Bo'limlar daraxti
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Bolimlar_daraxti_ichma_ich_qaytadi()
    {
        using var client = await AdminMijozAsync();

        var ota = await BolimYaratAsync(client, NoyobNom("7-Departament"), null);
        var bola = await BolimYaratAsync(client, NoyobNom("Metan zavod"), ota.Id);

        using var javob = await client.GetAsync(
            new Uri("/api/catalog/departments/tree", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var daraxt = await javob.Content.ReadFromJsonAsync<List<DepartmentNodeResponse>>(
            TestContext.Current.CancellationToken);

        daraxt.ShouldNotBeNull();

        var otaTugun = daraxt.FirstOrDefault(node => node.Id == ota.Id);
        otaTugun.ShouldNotBeNull("ota bo'lim daraxtning ildizida bo'lishi kerak");
        otaTugun.Level.ShouldBe(0);

        var bolaTugun = otaTugun.Children.FirstOrDefault(node => node.Id == bola.Id);
        bolaTugun.ShouldNotBeNull("bola bo'lim ota tugunning ichida bo'lishi kerak");

        // Ierarxiya yozgandan keyin qayta hisoblanadi
        bolaTugun.Level.ShouldBe(1);
        bolaTugun.Path.ShouldNotBeNullOrWhiteSpace();
        bolaTugun.Path.ShouldContain(ota.Name);
    }

    [Fact]
    public async Task Bolimni_oz_avlodiga_kochirish_409_halqa_beradi()
    {
        using var client = await AdminMijozAsync();

        var ota = await BolimYaratAsync(client, NoyobNom("Departament"), null);
        var bola = await BolimYaratAsync(client, NoyobNom("Zavod"), ota.Id);
        var nabira = await BolimYaratAsync(client, NoyobNom("Sex"), bola.Id);

        using var javob = await client.PostAsJsonAsync(
            new Uri($"/api/catalog/departments/{ota.Id}/move", UriKind.Relative),
            new { newParentId = nabira.Id },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.Conflict, await MatnAsync(javob));
        (await MatnAsync(javob)).ShouldContain("bolim.halqa");
    }

    [Fact]
    public async Task Bolimni_yuqori_darajaga_chiqarish_yolni_yangilaydi()
    {
        using var client = await AdminMijozAsync();

        var ota = await BolimYaratAsync(client, NoyobNom("Departament"), null);
        var bola = await BolimYaratAsync(client, NoyobNom("Zavod"), ota.Id);

        using var javob = await client.PostAsJsonAsync(
            new Uri($"/api/catalog/departments/{bola.Id}/move", UriKind.Relative),
            new { newParentId = (Guid?)null },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var kochirilgan = await javob.Content.ReadFromJsonAsync<DepartmentResponse>(
            TestContext.Current.CancellationToken);

        kochirilgan.ShouldNotBeNull();
        kochirilgan.ParentId.ShouldBeNull();
        kochirilgan.Level.ShouldBe(0);
        kochirilgan.Path.ShouldBe(bola.Name);
    }

    // ────────────────────────────────────────────────────────────
    //  Lavozim vazifalari
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Lavozim_vazifalari_qoshiladi_tartiblanadi_va_ochiriladi()
    {
        using var client = await AdminMijozAsync();

        using var yaratish = await client.PostAsJsonAsync(
            new Uri("/api/catalog/positions", UriKind.Relative),
            new { name = NoyobNom("Qorovul"), grade = 1, probationDays = 30 },
            TestContext.Current.CancellationToken);

        yaratish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yaratish));

        var lavozim = await yaratish.Content.ReadFromJsonAsync<PositionResponse>(
            TestContext.Current.CancellationToken);

        lavozim.ShouldNotBeNull();
        lavozim.ProbationDays.ShouldBe(30);
        lavozim.Duties.ShouldBeEmpty();

        var vazifalarYoli = new Uri($"/api/catalog/positions/{lavozim.Id}/duties", UriKind.Relative);

        await VazifaQoshAsync(client, vazifalarYoli, "Hududni kuzatish");
        var vazifalar = await VazifaQoshAsync(client, vazifalarYoli, "Kirish-chiqishni qayd etish");

        vazifalar.Count.ShouldBe(2);

        // ⭐ HR talabi: lavozim tanlanganda vazifalar avtomatik yuklanadi
        using var oqish = await client.GetAsync(vazifalarYoli, TestContext.Current.CancellationToken);
        oqish.StatusCode.ShouldBe(HttpStatusCode.OK);

        var oqilgan = await oqish.Content.ReadFromJsonAsync<List<DutyResponse>>(
            TestContext.Current.CancellationToken);

        oqilgan.ShouldNotBeNull();
        oqilgan.Count.ShouldBe(2);
        oqilgan[0].Text.ShouldBe("Hududni kuzatish");

        // Tartibni teskarisiga o'zgartirish
        using var tartib = await client.PutAsJsonAsync(
            new Uri($"/api/catalog/positions/{lavozim.Id}/duties/order", UriKind.Relative),
            new { orderedIds = oqilgan.Select(duty => duty.Id).Reverse().ToArray() },
            TestContext.Current.CancellationToken);

        tartib.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tartib));

        var tartiblangan = await tartib.Content.ReadFromJsonAsync<List<DutyResponse>>(
            TestContext.Current.CancellationToken);

        tartiblangan.ShouldNotBeNull();
        tartiblangan[0].Text.ShouldBe("Kirish-chiqishni qayd etish");

        // O'chirish
        using var ochirish = await client.DeleteAsync(
            new Uri($"/api/catalog/positions/{lavozim.Id}/duties/{tartiblangan[0].Id}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        ochirish.StatusCode.ShouldBe(HttpStatusCode.OK);

        var qolgan = await ochirish.Content.ReadFromJsonAsync<List<DutyResponse>>(
            TestContext.Current.CancellationToken);

        qolgan.ShouldNotBeNull();
        qolgan.Count.ShouldBe(1);
        qolgan[0].Text.ShouldBe("Hududni kuzatish");
    }

    // ────────────────────────────────────────────────────────────
    //  Ruxsatlar
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tokensiz_malumotnoma_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(
            new Uri("/api/catalog/legal-entities", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Catalog_ruxsati_yoq_foydalanuvchi_403_oladi()
    {
        // Token haqiqiy va imzosi to'g'ri — faqat ichida `catalog.view` yo'q.
        // Aynan shu holat eng xavflisi: autentifikatsiya o'tadi, avtorizatsiya
        // esa endpoint darajasida to'xtatishi kerak.
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await RuxsatsizTokenAsync());

        using var oqish = await client.GetAsync(
            new Uri("/api/catalog/legal-entities", UriKind.Relative),
            TestContext.Current.CancellationToken);

        oqish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var yozish = await client.PostAsJsonAsync(
            new Uri("/api/catalog/legal-entities", UriKind.Relative),
            new { name = NoyobNom("Ruxsatsiz") },
            TestContext.Current.CancellationToken);

        yozish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private async Task<HttpClient> AdminMijozAsync()
    {
        var client = _factory.CreateClient();

        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = BonviApiFactory.TestAdminLogin, password = BonviApiFactory.TestAdminPassword },
            TestContext.Current.CancellationToken);

        javob.EnsureSuccessStatusCode();

        var auth = await javob.Content.ReadFromJsonAsync<AuthResponse>(
            TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    /// <summary>
    /// Haqiqiy, lekin ruxsatlarsiz access token yasaydi.
    /// </summary>
    /// <remarks>
    /// Bazada faqat administrator bor va unda barcha ruxsatlar mavjud.
    /// Shu sababli token to'g'ridan-to'g'ri token servisidan, bo'sh ruxsat
    /// to'plami bilan olinadi — bu «ruxsati kam foydalanuvchi» holatini
    /// yangi rol yaratmasdan aniq takrorlaydi.
    /// </remarks>
    private async Task<string> RuxsatsizTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();

        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var login = PhoneNumber.Create(BonviApiFactory.TestAdminLogin).Value;
        var user = await users.GetByLoginAsync(login, TestContext.Current.CancellationToken);
        user.ShouldNotBeNull();

        var pair = tokens.CreateTokens(user, [], new HashSet<string>(StringComparer.Ordinal));

        return pair.AccessToken;
    }

    private static async Task<DepartmentResponse> BolimYaratAsync(HttpClient client, string nom, Guid? otaId)
    {
        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/catalog/departments", UriKind.Relative),
            new { name = nom, parentId = otaId },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var bolim = await javob.Content.ReadFromJsonAsync<DepartmentResponse>(
            TestContext.Current.CancellationToken);

        return bolim!;
    }

    /// <summary>
    /// Vazifa qo'shadi.
    /// </summary>
    /// <remarks>
    /// Bu yerda yiqilish odatda EF Core'ning kuzatuv qoidasini bildiradi:
    /// tayyor kalitli (client-generated GUID) bola yozuv kuzatilayotgan
    /// agregatning to'plamiga qo'shilganda EF uni <c>Added</c> emas,
    /// <c>Modified</c> deb belgilaydi va INSERT o'rniga UPDATE yuboradi.
    /// Yechim Infrastructure qatlamida — batafsil hisobotda.
    /// </remarks>
    private static async Task<List<DutyResponse>> VazifaQoshAsync(HttpClient client, Uri yol, string matn)
    {
        using var javob = await client.PostAsJsonAsync(
            yol,
            new { text = matn },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            "Vazifa qo'shilmadi. Javob: " + await MatnAsync(javob));

        var vazifalar = await javob.Content.ReadFromJsonAsync<List<DutyResponse>>(
            TestContext.Current.CancellationToken);

        return vazifalar!;
    }

    /// <summary>Xato javobini o'qish — tekshiruv yiqilganda sababi ko'rinishi uchun.</summary>
    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
