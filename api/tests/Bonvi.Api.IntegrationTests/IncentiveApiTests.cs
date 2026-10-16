using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bonvi.Domain.Catalog;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>Rag'bat/jarima summasi (test uchun).</summary>
public sealed record IncentiveMoneyResponse(decimal Amount, string Currency, string Formatted);

/// <summary>Rag'bat/jarima javobi (test uchun).</summary>
public sealed record IncentiveResponse(
    Guid Id,
    Guid EmployeeId,
    string Kind,
    Guid TypeId,
    string? TypeName,
    DateOnly Date,
    IncentiveMoneyResponse? Amount,
    decimal? Percent,
    string? GiftDescription,
    string? Reason,
    string? OrderNo,
    DateOnly? OrderDate);

/// <summary>
/// Rag'bat va jarima — uchi-uchiga: berish, ro'yxat, tuzatish, o'chirish.
/// </summary>
/// <remarks>
/// Bu yerda tekshiriladigan narsa mantiq emas (uni <c>IncentiveTests</c> va
/// <c>IncentiveHandlerTests</c> allaqachon qoplagan), balki ULANISH: so'rov
/// tanasi handler'gacha yetib boradimi, ruxsat va marshrut to'g'ri
/// qo'yilganmi.
/// </remarks>
public sealed class IncentiveApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public IncentiveApiTests(BonviApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Ragbat_berish_royxatda_va_tur_nomi_bilan_chiqadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var xodim = await XodimYaratAsync(mijoz);
        var turi = await TurYaratAsync(hasAmount: true);

        using var berish = await mijoz.PostAsJsonAsync(
            new Uri($"/api/employees/{xodim.Id}/incentives", UriKind.Relative),
            new
            {
                kind = "Award",
                typeId = turi.Id,
                date = new DateOnly(2026, 3, 1),
                amount = 500_000m,
                orderNo = "12-b",
            },
            bekorQilish);

        berish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(berish));

        var natija = await berish.Content.ReadFromJsonAsync<IncentiveResponse>(bekorQilish);
        natija!.TypeName.ShouldBe(turi.Name);
        natija.Amount!.Amount.ShouldBe(500_000m);

        using var royxat = await mijoz.GetAsync(
            new Uri($"/api/employees/{xodim.Id}/incentives", UriKind.Relative), bekorQilish);

        var qatorlar = await royxat.Content.ReadFromJsonAsync<IReadOnlyList<IncentiveResponse>>(bekorQilish);
        qatorlar!.ShouldContain(q => q.Id == natija.Id);
    }

    [Fact]
    public async Task Jarima_sababsiz_400_qaytaradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var xodim = await XodimYaratAsync(mijoz);
        var turi = await TurYaratAsync(hasAmount: false, kind: IncentiveKindSeed.Penalty);

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri($"/api/employees/{xodim.Id}/incentives", UriKind.Relative),
            new { kind = "Penalty", typeId = turi.Id, date = new DateOnly(2026, 3, 1) },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));
        (await MatnAsync(javob)).ShouldContain("ragbat_jarima.sabab_bosh");
    }

    [Fact]
    public async Task Jarima_foiz_bilan_beriladi_va_ikkala_royxatda_foiz_chiqadi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var xodim = await XodimYaratAsync(mijoz);
        var turi = await TurYaratAsync(hasAmount: true, kind: IncentiveKindSeed.Penalty);

        using var berish = await mijoz.PostAsJsonAsync(
            new Uri($"/api/employees/{xodim.Id}/incentives", UriKind.Relative),
            new { kind = "Penalty", typeId = turi.Id, date = new DateOnly(2026, 3, 1), reason = "Kechikish", percent = 2.5m },
            bekorQilish);

        berish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(berish));

        var natija = await berish.Content.ReadFromJsonAsync<IncentiveResponse>(bekorQilish);
        natija!.Percent.ShouldBe(2.5m);
        natija.Amount.ShouldBeNull();
        natija.OrderNo.ShouldBeNull();

        // Xodim kartasidagi tab.
        using var tab = await mijoz.GetAsync(
            new Uri($"/api/employees/{xodim.Id}/incentives", UriKind.Relative), bekorQilish);
        var qatorlar = await tab.Content.ReadFromJsonAsync<IReadOnlyList<IncentiveResponse>>(bekorQilish);
        qatorlar!.Single(q => q.Id == natija.Id).Percent.ShouldBe(2.5m);

        // Umumiy ro'yxat.
        using var royxat = await mijoz.GetAsync(
            new Uri($"/api/incentives?kind=Penalty&search={Uri.EscapeDataString(xodim.FullName)}", UriKind.Relative),
            bekorQilish);
        royxat.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(royxat));
        var sahifa = await royxat.Content.ReadFromJsonAsync<IncentiveListPageResponse>(bekorQilish);
        var qator = sahifa!.Items.Single(q => q.Id == natija.Id);
        qator.Percent.ShouldBe(2.5m);
        qator.Amount.ShouldBeNull();
    }

    [Fact]
    public async Task Jarima_tuzatishda_summadan_foizga_va_qaytib_otadi()
    {
        // Bitta UPDATE da summa (EF ixtiyoriy murakkab xossasi) null'ga, foiz
        // qiymatga o'tadi — haqiqiy bazadagi `ck_incentives_amount_or_percent`
        // bilan. Domen va handler testlari bu yo'lni bazasiz tekshiradi.
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var xodim = await XodimYaratAsync(mijoz);
        var turi = await TurYaratAsync(hasAmount: true, kind: IncentiveKindSeed.Penalty);
        var sana = new DateOnly(2026, 3, 1);

        using var berish = await mijoz.PostAsJsonAsync(
            new Uri($"/api/employees/{xodim.Id}/incentives", UriKind.Relative),
            new { kind = "Penalty", typeId = turi.Id, date = sana, reason = "Kechikish", amount = 300_000m },
            bekorQilish);
        berish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(berish));
        var yaratilgan = await berish.Content.ReadFromJsonAsync<IncentiveResponse>(bekorQilish);

        var manzil = new Uri($"/api/employees/{xodim.Id}/incentives/{yaratilgan!.Id}", UriKind.Relative);

        using var foizga = await mijoz.PutAsJsonAsync(
            manzil, new { typeId = turi.Id, date = sana, reason = "Kechikish", percent = 4m }, bekorQilish);
        foizga.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(foizga));
        var foizli = await foizga.Content.ReadFromJsonAsync<IncentiveResponse>(bekorQilish);
        foizli!.Percent.ShouldBe(4m);
        foizli.Amount.ShouldBeNull();

        using var summaga = await mijoz.PutAsJsonAsync(
            manzil, new { typeId = turi.Id, date = sana, reason = "Kechikish", amount = 250_000m }, bekorQilish);
        summaga.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(summaga));

        // Bazadan qayta o'qiladi — javob emas, saqlangan holat tekshiriladi.
        using var royxat = await mijoz.GetAsync(
            new Uri($"/api/employees/{xodim.Id}/incentives", UriKind.Relative), bekorQilish);
        var qatorlar = await royxat.Content.ReadFromJsonAsync<IReadOnlyList<IncentiveResponse>>(bekorQilish);
        var qator = qatorlar!.Single(q => q.Id == yaratilgan.Id);
        qator.Amount!.Amount.ShouldBe(250_000m);
        qator.Percent.ShouldBeNull();
    }

    [Fact]
    public async Task Jarimaga_buyruq_bilan_berish_400_qaytaradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var xodim = await XodimYaratAsync(mijoz);
        var turi = await TurYaratAsync(hasAmount: false, kind: IncentiveKindSeed.Penalty);

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri($"/api/employees/{xodim.Id}/incentives", UriKind.Relative),
            new
            {
                kind = "Penalty",
                typeId = turi.Id,
                date = new DateOnly(2026, 3, 1),
                reason = "Kechikish",
                orderNo = "45-b",
                orderDate = new DateOnly(2026, 3, 1),
            },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));
        (await MatnAsync(javob)).ShouldContain("ragbat_jarima.buyruq_jarimada");
    }

    [Fact]
    public async Task Jarimada_summa_va_foiz_birga_400_qaytaradi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var xodim = await XodimYaratAsync(mijoz);
        var turi = await TurYaratAsync(hasAmount: true, kind: IncentiveKindSeed.Penalty);

        using var javob = await mijoz.PostAsJsonAsync(
            new Uri($"/api/employees/{xodim.Id}/incentives", UriKind.Relative),
            new
            {
                kind = "Penalty",
                typeId = turi.Id,
                date = new DateOnly(2026, 3, 1),
                reason = "Kechikish",
                amount = 300_000m,
                percent = 4m,
            },
            bekorQilish);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await MatnAsync(javob));
        (await MatnAsync(javob)).ShouldContain("ragbat_jarima.summa_va_foiz");
    }

    [Fact]
    public async Task Tuzatish_va_ochirish_ishlaydi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        using var mijoz = await AdminMijozAsync();

        var xodim = await XodimYaratAsync(mijoz);
        var turi = await TurYaratAsync(hasAmount: false);

        using var berish = await mijoz.PostAsJsonAsync(
            new Uri($"/api/employees/{xodim.Id}/incentives", UriKind.Relative),
            new { kind = "Award", typeId = turi.Id, date = new DateOnly(2026, 3, 1) },
            bekorQilish);

        var yaratilgan = await berish.Content.ReadFromJsonAsync<IncentiveResponse>(bekorQilish);

        using var tuzatish = await mijoz.PutAsJsonAsync(
            new Uri($"/api/employees/{xodim.Id}/incentives/{yaratilgan!.Id}", UriKind.Relative),
            new { typeId = turi.Id, date = new DateOnly(2026, 3, 5), reason = "Tuzatildi" },
            bekorQilish);

        tuzatish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tuzatish));
        var tuzatilgan = await tuzatish.Content.ReadFromJsonAsync<IncentiveResponse>(bekorQilish);
        tuzatilgan!.Date.ShouldBe(new DateOnly(2026, 3, 5));
        tuzatilgan.Reason.ShouldBe("Tuzatildi");

        using var ochirish = await mijoz.DeleteAsync(
            new Uri($"/api/employees/{xodim.Id}/incentives/{yaratilgan.Id}", UriKind.Relative), bekorQilish);

        ochirish.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(ochirish));

        using var royxat = await mijoz.GetAsync(
            new Uri($"/api/employees/{xodim.Id}/incentives", UriKind.Relative), bekorQilish);

        var qatorlar = await royxat.Content.ReadFromJsonAsync<IReadOnlyList<IncentiveResponse>>(bekorQilish);
        qatorlar!.ShouldNotContain(q => q.Id == yaratilgan.Id, "yumshoq o'chirilgan yozuv ro'yxatda chiqmasligi kerak");
    }

    // ────────────────────────────────────────────────────────────
    //  Muhit tayyorlash
    // ────────────────────────────────────────────────────────────

    private enum IncentiveKindSeed
    {
        Award,
        Penalty,
    }

    private async Task<(Guid Id, string Name)> TurYaratAsync(
        bool hasAmount,
        IncentiveKindSeed kind = IncentiveKindSeed.Award)
    {
        var bekorQilish = TestContext.Current.CancellationToken;

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var belgi = Guid.NewGuid().ToString("N")[..6];

        if (kind == IncentiveKindSeed.Award)
        {
            var turi = AwardType.Create($"Test rag'bat {belgi}").Value;
            turi.SetHasAmount(hasAmount);
            kontekst.AwardTypes.Add(turi);
            await kontekst.SaveChangesAsync(bekorQilish);
            return (turi.Id, turi.Name);
        }

        var jarima = PenaltyType.Create($"Test jarima {belgi}").Value;
        jarima.SetHasAmount(hasAmount);
        kontekst.PenaltyTypes.Add(jarima);
        await kontekst.SaveChangesAsync(bekorQilish);
        return (jarima.Id, jarima.Name);
    }

    private static async Task<EmployeeResponse> XodimYaratAsync(HttpClient client)
    {
        using var javob = await client.PostAsJsonAsync(
            new Uri("/api/employees", UriKind.Relative),
            new
            {
                lastName = "Рағбатли " + Guid.NewGuid().ToString("N")[..6],
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
