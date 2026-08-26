using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Taqsimotdagi bitta bo'lak (test uchun).</summary>
public sealed record BreakdownItemResponse(Guid? Id, string Name, int Count);

/// <summary>Bo'lim/yo'nalish taqsimoti (test uchun).</summary>
public sealed record DashboardBreakdownResponse(
    IReadOnlyList<BreakdownItemResponse> Departments,
    IReadOnlyList<BreakdownItemResponse> Directions);

/// <summary>
/// Bosh sahifadagi bo'lim/yo'nalish kesimining uchi-uchiga testlari.
/// </summary>
/// <remarks>
/// Har bir test o'zining YANGI (tasodifiy GUID) bo'lim/yo'nalishini yaratadi
/// va topilgan bo'lakni aynan shu identifikator bo'yicha izlaydi — shu
/// sababli bir xil bazani baham ko'radigan boshqa testlar «Belgilanmagan»
/// kabi umumiy bo'laklarga tegmasa, natija hech qachon boshqa testdan
/// pollyutsiyaga uchramaydi.
/// </remarks>
public sealed class DashboardBreakdownApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public DashboardBreakdownApiTests(BonviApiFactory factory) => _factory = factory;

    private static string Belgi() => "BD" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    [Fact]
    public async Task Tokensiz_sorov_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(
            new Uri("/api/dashboard/breakdown", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Bo'lim daraxtining barcha darajalari (ildiz, farzand, nevara) BITTA
    /// ildiz bo'lagiga yig'iladi.
    /// </summary>
    [Fact]
    public async Task Bolim_daraxti_ildizga_yigiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Belgi();

        using var client = await AdminMijozAsync();

        Department ildiz;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var firma = LegalEntity.Create($"{belgi} МЧЖ").Value;
            ildiz = Department.Create($"{belgi} Ildiz").Value;
            var farzand = Department.Create($"{belgi} Farzand", ildiz.Id).Value;
            var nevara = Department.Create($"{belgi} Nevara", farzand.Id).Value;

            // Ildizning o'zida ham, farzandda ham, nevarada ham — jami 3 kishi.
            var ildizdagi = Employee.Create(FullName.Create("Ildizov", belgi, null).Value).Value;
            var farzanddagi = Employee.Create(FullName.Create("Farzandov", belgi, null).Value).Value;
            var nevaradagi = Employee.Create(FullName.Create("Nevarov", belgi, null).Value).Value;

            var m1 = Doimiy(ildizdagi.Id, firma.Id, new DateOnly(2022, 1, 10), ildiz.Id);
            var m2 = Doimiy(farzanddagi.Id, firma.Id, new DateOnly(2022, 2, 10), farzand.Id);
            var m3 = Doimiy(nevaradagi.Id, firma.Id, new DateOnly(2022, 3, 10), nevara.Id);

            kontekst.Add(firma);
            kontekst.AddRange(ildiz, farzand, nevara);
            kontekst.AddRange(ildizdagi, farzanddagi, nevaradagi);
            kontekst.AddRange(m1, m2, m3);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        var breakdown = await BreakdownAsync(client);

        var topilgan = breakdown.Departments.SingleOrDefault(item => item.Id == ildiz.Id);
        topilgan.ShouldNotBeNull();
        topilgan.Name.ShouldBe(ildiz.Name);
        topilgan.Count.ShouldBe(3);
    }

    /// <summary>
    /// Bo'limi ko'rsatilmagan faol xodim «Belgilanmagan» bo'lagiga tushadi —
    /// jimgina yo'qolmaydi.
    /// </summary>
    [Fact]
    public async Task Bolim_korsatilmagan_xodim_belgilanmagan_bolagiga_tushadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Belgi();

        using var client = await AdminMijozAsync();

        var oldin = await BreakdownAsync(client);
        var oldingiSon = oldin.Departments.FirstOrDefault(item => item.Id is null)?.Count ?? 0;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var firma = LegalEntity.Create($"{belgi} МЧЖ").Value;
            var xodim = Employee.Create(FullName.Create("Bolimsiz", belgi, null).Value).Value;
            var munosabat = Doimiy(xodim.Id, firma.Id, new DateOnly(2022, 4, 10), departmentId: null);

            kontekst.Add(firma);
            kontekst.Add(xodim);
            kontekst.Add(munosabat);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        var keyin = await BreakdownAsync(client);
        var keyingiSon = keyin.Departments.FirstOrDefault(item => item.Id is null)?.Count ?? 0;

        (keyingiSon - oldingiSon).ShouldBe(1);
    }

    /// <summary>
    /// Bo'lim bo'yicha taqsimotning yig'indisi «ActiveEmployees» soniga TENG —
    /// ikkalasi bir-birini tekshiradi (izohi «IDashboardReadModel» da).
    /// </summary>
    [Fact]
    public async Task Bolim_taqsimoti_yigindisi_ActiveEmployees_soniga_teng()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Belgi();

        using var client = await AdminMijozAsync();

        var oldingiStats = await StatsAsync(client);
        var oldingiBreakdown = await BreakdownAsync(client);
        var oldingiYigindi = oldingiBreakdown.Departments.Sum(item => item.Count);

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var firma = LegalEntity.Create($"{belgi} МЧЖ").Value;
            var bolim = Department.Create($"{belgi} Bolim").Value;
            var xodim = Employee.Create(FullName.Create("Yigindiov", belgi, null).Value).Value;
            var munosabat = Doimiy(xodim.Id, firma.Id, new DateOnly(2022, 5, 10), bolim.Id);

            kontekst.Add(firma);
            kontekst.Add(bolim);
            kontekst.Add(xodim);
            kontekst.Add(munosabat);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        var keyingiStats = await StatsAsync(client);
        var keyingiBreakdown = await BreakdownAsync(client);
        var keyingiYigindi = keyingiBreakdown.Departments.Sum(item => item.Count);

        (keyingiStats.ActiveEmployees - oldingiStats.ActiveEmployees).ShouldBe(1);
        (keyingiYigindi - oldingiYigindi).ShouldBe(1);
    }

    /// <summary>
    /// Bir nechta yo'nalishga tegishli xodim HAR BIR yo'nalish bo'lagida
    /// alohida sanaladi — bo'lim bilan farqi shu (ko'p-ga-ko'p bog'lanish).
    /// </summary>
    [Fact]
    public async Task Bitta_xodim_ikkita_yonalishda_ikkalasida_ham_sanaladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Belgi();

        using var client = await AdminMijozAsync();

        Direction birinchi;
        Direction ikkinchi;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var firma = LegalEntity.Create($"{belgi} МЧЖ").Value;
            birinchi = Direction.Create($"{belgi} Birinchi").Value;
            ikkinchi = Direction.Create($"{belgi} Ikkinchi").Value;

            var xodim = Employee.Create(FullName.Create("Yonalishli", belgi, null).Value).Value;
            xodim.AssignDirection(birinchi.Id);
            xodim.AssignDirection(ikkinchi.Id);

            var munosabat = Doimiy(xodim.Id, firma.Id, new DateOnly(2022, 6, 10), departmentId: null);

            kontekst.Add(firma);
            kontekst.AddRange(birinchi, ikkinchi);
            kontekst.Add(xodim);
            kontekst.Add(munosabat);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        var breakdown = await BreakdownAsync(client);

        breakdown.Directions.Single(item => item.Id == birinchi.Id).Count.ShouldBe(1);
        breakdown.Directions.Single(item => item.Id == ikkinchi.Id).Count.ShouldBe(1);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private static Employment Doimiy(Guid xodimId, Guid firmaId, DateOnly sana, Guid? departmentId)
    {
        var munosabat = Employment.Hire(xodimId, firmaId, sana, departmentId).Value;
        munosabat.CompleteProbation(sana).IsSuccess.ShouldBeTrue();
        return munosabat;
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob) =>
        await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    private static async Task<DashboardBreakdownResponse> BreakdownAsync(HttpClient client)
    {
        using var javob = await client.GetAsync(
            new Uri("/api/dashboard/breakdown", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var natija = await javob.Content.ReadFromJsonAsync<DashboardBreakdownResponse>(
            TestContext.Current.CancellationToken);

        natija.ShouldNotBeNull();
        return natija;
    }

    private static async Task<DashboardStatsResponse> StatsAsync(HttpClient client)
    {
        using var javob = await client.GetAsync(
            new Uri("/api/dashboard/stats?period=month", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var natija = await javob.Content.ReadFromJsonAsync<DashboardStatsResponse>(
            TestContext.Current.CancellationToken);

        natija.ShouldNotBeNull();
        return natija;
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
}
