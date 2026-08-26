using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Qobiliyatlar profili javobi (test uchun).</summary>
public sealed record CompetencyResponse(
    Guid Id,
    Guid EmployeeId,
    string? Languages,
    int? ComputerLiteracyPercent,
    string? Specialization,
    string? OtherAbilities,
    string? Interest,
    string? EducationPlace);

/// <summary>
/// Qobiliyatlar profili — uchi-uchiga: bo'sh holat, saqlash, qayta saqlash (upsert), tekshiruv.
/// </summary>
/// <remarks>
/// Bu yerda tekshiriladigan narsa mantiq emas (uni <c>EmployeeCompetencyTests</c> va
/// <c>CompetencyTests</c> allaqachon qoplagan), balki ULANISH: so'rov tanasi
/// handler'gacha yetib boradimi va marshrut/ruxsat to'g'ri qo'yilganmi.
/// </remarks>
public sealed class EmployeeCompetencyApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public EmployeeCompetencyApiTests(BonviApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Profil_hali_yoq_bolganda_bosh_javob_qaytadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var xodim = await XodimYaratAsync(mijoz);

        using var javob = await mijoz.GetAsync(
            new Uri($"/api/employees/{xodim.Id}/competency", UriKind.Relative), bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        // Minimal API `Results.Ok(null)` uchun tana yo bo'sh, yo so'zma-so'z
        // «null» — ikkalasi ham «profil yo'q» ma'nosida, farqi muhim emas.
        var tana = (await MatnAsync(javob)).Trim();
        (tana.Length == 0 || tana == "null").ShouldBeTrue($"kutilmagan tana: «{tana}»");
    }

    [Fact]
    public async Task Saqlangan_profil_qayta_sorovda_togri_qaytadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var xodim = await XodimYaratAsync(mijoz);

        using var saqlash = await mijoz.PutAsJsonAsync(
            new Uri($"/api/employees/{xodim.Id}/competency", UriKind.Relative),
            new
            {
                languages = "Инглиз ва рус тили",
                computerLiteracyPercent = 80,
                specialization = "Ихтисодиёт",
                interest = "Дизайн",
                educationPlace = "ТДТУ",
            },
            bekorQilish);

        saqlash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(saqlash));

        var natija = await saqlash.Content.ReadFromJsonAsync<CompetencyResponse>(bekorQilish);
        natija!.EmployeeId.ShouldBe(xodim.Id);
        natija.ComputerLiteracyPercent.ShouldBe(80);

        using var olish = await mijoz.GetAsync(
            new Uri($"/api/employees/{xodim.Id}/competency", UriKind.Relative), bekorQilish);

        var qayta = await olish.Content.ReadFromJsonAsync<CompetencyResponse>(bekorQilish);
        qayta!.Id.ShouldBe(natija.Id);
        qayta.Languages.ShouldBe("Инглиз ва рус тили");
        qayta.EducationPlace.ShouldBe("ТДТУ");
    }

    [Fact]
    public async Task Ikkinchi_saqlashda_yangi_yozuv_emas_ustiga_yoziladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var xodim = await XodimYaratAsync(mijoz);

        using var birinchi = await mijoz.PutAsJsonAsync(
            new Uri($"/api/employees/{xodim.Id}/competency", UriKind.Relative),
            new { languages = "Rus tili" },
            bekorQilish);

        var birinchiNatija = await birinchi.Content.ReadFromJsonAsync<CompetencyResponse>(bekorQilish);

        using var ikkinchi = await mijoz.PutAsJsonAsync(
            new Uri($"/api/employees/{xodim.Id}/competency", UriKind.Relative),
            new { languages = "Ingliz tili", computerLiteracyPercent = 60 },
            bekorQilish);

        ikkinchi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(ikkinchi));

        var ikkinchiNatija = await ikkinchi.Content.ReadFromJsonAsync<CompetencyResponse>(bekorQilish);
        ikkinchiNatija!.Id.ShouldBe(birinchiNatija!.Id, "profil bitta — ustiga yoziladi, yangisi ochilmaydi");
        ikkinchiNatija.Languages.ShouldBe("Ingliz tili");
        ikkinchiNatija.ComputerLiteracyPercent.ShouldBe(60);
    }

    [Fact]
    public async Task Notogri_foiz_400_qaytaradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var xodim = await XodimYaratAsync(mijoz);

        using var javob = await mijoz.PutAsJsonAsync(
            new Uri($"/api/employees/{xodim.Id}/competency", UriKind.Relative),
            new { computerLiteracyPercent = 150 },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));
        (await MatnAsync(javob)).ShouldContain("qobiliyat.kompyuter_foiz");
    }

    [Fact]
    public async Task Nomavjud_xodimga_saqlash_404_qaytaradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        using var javob = await mijoz.PutAsJsonAsync(
            new Uri($"/api/employees/{Guid.NewGuid()}/competency", UriKind.Relative),
            new { languages = "Rus tili" },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.NotFound, await MatnAsync(javob));
    }

    private static async Task<EmployeeResponse> XodimYaratAsync(HttpClient client)
    {
        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/employees", UriKind.Relative),
            new
            {
                lastName = "Қобилятли " + Guid.NewGuid().ToString("N")[..6],
                firstName = "Ходим",
            },
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var xodim = await javob.Content.ReadFromJsonAsync<EmployeeResponse>(
            TestContext.Current.CancellationToken);

        xodim.ShouldNotBeNull();
        return xodim;
    }

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

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
