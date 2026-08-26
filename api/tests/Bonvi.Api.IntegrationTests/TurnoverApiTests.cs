using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Oylik qator (test uchun).</summary>
public sealed record TurnoverMonthResponse(
    DateOnly MonthStart,
    DateOnly MonthEnd,
    int Hired,
    int Left,
    int HeadcountStart,
    int HeadcountEnd);

/// <summary>Ketish sababi bo'yicha bo'lak (test uchun).</summary>
public sealed record TurnoverReasonResponse(Guid? ReasonId, string ReasonName, bool? IsVoluntary, int Count);

/// <summary>Kadrlar oqimi hisoboti (test uchun).</summary>
public sealed record TurnoverResponse(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<TurnoverMonthResponse> Months,
    IReadOnlyList<TurnoverReasonResponse> Reasons);

/// <summary>
/// Kadrlar oqimi (turnover) hisobotining uchi-uchiga testlari.
/// </summary>
public sealed class TurnoverApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public TurnoverApiTests(BonviApiFactory factory) => _factory = factory;

    private static string Belgi() => "TO" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    [Fact]
    public async Task Tokensiz_sorov_401_qaytaradi()
    {
        using var client = _factory.CreateClient();

        using var javob = await client.GetAsync(
            new Uri("/api/reports/turnover", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(-1)]
    public async Task Notogri_oylar_soni_400_qaytaradi(int oylar)
    {
        using var client = await AdminMijozAsync();

        using var javob = await client.GetAsync(
            new Uri($"/api/reports/turnover?months={oylar}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));
        (await MatnAsync(javob)).ShouldContain("hisobot.oy_notogri");
    }

    /// <summary>Sukut bo'yicha — so'nggi 12 oy, joriy oy ham kiradi.</summary>
    [Fact]
    public async Task Sukut_boyicha_12_oy_qaytadi()
    {
        using var client = await AdminMijozAsync();

        var hisobot = await TurnoverAsync(client, string.Empty);

        hisobot.Months.Count.ShouldBe(12);
        hisobot.Months[^1].MonthStart.Month.ShouldBe(hisobot.To.Month);
        hisobot.Months[^1].MonthStart.Year.ShouldBe(hisobot.To.Year);
        hisobot.Months[^1].MonthEnd.ShouldBe(hisobot.To);
    }

    /// <summary>
    /// Bir oyda ishga qabul qilingan/bo'shatilgan MUNOSABATLAR (hodisalar)
    /// soni — odam bo'yicha emas (izohi «TurnoverMonth» da).
    /// </summary>
    [Fact]
    public async Task Oylik_kelgan_ketgan_hodisalar_toliq_sanaladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Belgi();

        using var client = await AdminMijozAsync();

        var oldin = await TurnoverAsync(client, "?months=1");
        var joriyOy = oldin.Months[^1];

        var bugun = joriyOy.MonthEnd;
        var qabulSanasi = new DateOnly(joriyOy.MonthStart.Year, joriyOy.MonthStart.Month, 1);

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            var firma = LegalEntity.Create($"{belgi} МЧЖ").Value;

            // Ikkita YANGI qabul — shu oyda.
            var kelgan1 = Employee.Create(FullName.Create("Kelgan", belgi + "1", null).Value).Value;
            var kelgan2 = Employee.Create(FullName.Create("Kelgan", belgi + "2", null).Value).Value;
            var m1 = Employment.Hire(kelgan1.Id, firma.Id, qabulSanasi).Value;
            var m2 = Employment.Hire(kelgan2.Id, firma.Id, qabulSanasi).Value;

            // Bitta odam ESKIDAN ishlab, shu oyda ikki marta (ketib-kelib)
            // bo'shatiladi — ikkalasi ham HODISA sifatida sanalishi kerak.
            var ketuvchi = Employee.Create(FullName.Create("Ketuvchi", belgi, null).Value).Value;
            var eski = Employment.Hire(ketuvchi.Id, firma.Id, qabulSanasi.AddYears(-2)).Value;
            eski.CompleteProbation(qabulSanasi.AddYears(-2)).IsSuccess.ShouldBeTrue();
            eski.Terminate(bugun, reasonId: null).IsSuccess.ShouldBeTrue();

            kontekst.Add(firma);
            kontekst.AddRange(kelgan1, kelgan2, ketuvchi);
            kontekst.AddRange(m1, m2, eski);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        var keyin = await TurnoverAsync(client, "?months=1");
        var keyingiOy = keyin.Months[^1];

        (keyingiOy.Hired - joriyOy.Hired).ShouldBe(2);
        (keyingiOy.Left - joriyOy.Left).ShouldBe(1);
    }

    /// <summary>Ketish sababi bo'yicha taqsimot — mavjud ma'lumotnoma bo'yicha guruhlanadi.</summary>
    [Fact]
    public async Task Ketish_sababi_boyicha_guruhlanadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var belgi = Belgi();

        using var client = await AdminMijozAsync();

        TerminationReason sabab;
        DateOnly ketishSanasi;

        await using (var qamrov = _factory.Services.CreateAsyncScope())
        {
            var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

            sabab = TerminationReason.Create($"{belgi} O'z хохиши билан").Value;

            var firma = LegalEntity.Create($"{belgi} МЧЖ").Value;
            var xodim1 = Employee.Create(FullName.Create("Sababli", belgi + "1", null).Value).Value;
            var xodim2 = Employee.Create(FullName.Create("Sababli", belgi + "2", null).Value).Value;

            ketishSanasi = qamrov.ServiceProvider
                .GetRequiredService<Bonvi.Domain.Abstractions.IClock>().Today;

            var m1 = Employment.Hire(xodim1.Id, firma.Id, ketishSanasi.AddYears(-1)).Value;
            m1.CompleteProbation(ketishSanasi.AddYears(-1)).IsSuccess.ShouldBeTrue();
            m1.Terminate(ketishSanasi, sabab.Id).IsSuccess.ShouldBeTrue();

            var m2 = Employment.Hire(xodim2.Id, firma.Id, ketishSanasi.AddYears(-1)).Value;
            m2.CompleteProbation(ketishSanasi.AddYears(-1)).IsSuccess.ShouldBeTrue();
            m2.Terminate(ketishSanasi, sabab.Id).IsSuccess.ShouldBeTrue();

            kontekst.Add(sabab);
            kontekst.Add(firma);
            kontekst.AddRange(xodim1, xodim2);
            kontekst.AddRange(m1, m2);
            await kontekst.SaveChangesAsync(bekorQilish);
        }

        var hisobot = await TurnoverAsync(client, "?months=1");

        var topilgan = hisobot.Reasons.Single(item => item.ReasonId == sabab.Id);
        topilgan.ReasonName.ShouldBe(sabab.Name);
        topilgan.Count.ShouldBe(2);
        topilgan.IsVoluntary.ShouldBe(true);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private static async Task<string> MatnAsync(HttpResponseMessage javob) =>
        await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    private static async Task<TurnoverResponse> TurnoverAsync(HttpClient client, string sorov)
    {
        using var javob = await client.GetAsync(
            new Uri($"/api/reports/turnover{sorov}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var natija = await javob.Content.ReadFromJsonAsync<TurnoverResponse>(
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
