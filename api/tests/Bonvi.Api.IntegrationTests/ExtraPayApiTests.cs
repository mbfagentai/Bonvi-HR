using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Qo'shimcha vazifa haqi — okladga QO'SHILIB ko'rsatiladi va ALOHIDA ruxsat
/// (<c>salary.extra.view</c>) bilan ko'rinadi.
/// </summary>
/// <remarks>
/// Egasining qarori (2026-09-27): qo'shimcha haq okladning ustiga yozilmaydi,
/// kartada «oklad + qo'shimcha haq = jami» bo'lib chiqadi; asosiy okladni HR
/// ko'radi, qo'shimcha haqni esa hatto HR menejerlari ham ko'rmasligi mumkin.
/// </remarks>
public sealed class ExtraPayApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public ExtraPayApiTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>
    /// Oklad ruxsati bilan: vazifalar ko'rinadi, lekin summalari va jami YO'Q —
    /// kartada ham, mehnat yo'lida ham, buyruqlar ro'yxatida ham.
    /// </summary>
    [Fact]
    public async Task Qoshimcha_haq_alohida_ruxsatsiz_hech_qayerda_korinmaydi()
    {
        var urug = await UrugAsync();

        using var okladli = await MijozAsync(
            Permissions.EmployeesView, Permissions.EmploymentView, Permissions.SalaryView);

        var karta = await MunosabatAsync(okladli, urug.XodimId);
        karta.GetProperty("salary").GetProperty("amount").GetDecimal().ShouldBe(5_000_000m, "asosiy oklad ko'rinadi");

        var vazifalar = karta.GetProperty("duties").EnumerateArray().ToList();
        vazifalar.Count.ShouldBe(2, "vazifalarning o'zi yashirin emas");
        vazifalar.ShouldAllBe(vazifa => vazifa.GetProperty("extraPay").ValueKind == JsonValueKind.Null);
        karta.GetProperty("totalPay").ValueKind.ShouldBe(
            JsonValueKind.Null, "jamidan okladni ayirib qo'shimcha haqni hisoblab olish mumkin edi");

        (await LentadagiVazifaHaqiAsync(okladli, urug.XodimId, urug.HaqliVazifa))
            .ShouldBeNull("mehnat yo'li qo'shimcha haqni ochmasligi kerak");

        (await BuyruqlardagiVazifaHaqiAsync(okladli, urug.Buyruq))
            .ShouldBeNull("buyruqlar ro'yxati qo'shimcha haqni ochmasligi kerak");
    }

    /// <summary>
    /// Ikkala ruxsat bilan: kartada vazifa haqi va «jami = oklad + faol haqlar».
    /// Oklad O'ZGARMAYDI — qo'shimcha haq uning ustiga yozilmaydi.
    /// </summary>
    [Fact]
    public async Task Ruxsat_bilan_jami_oklad_va_qoshimcha_haqdan_iborat()
    {
        var urug = await UrugAsync();

        using var toliq = await MijozAsync(
            Permissions.EmployeesView, Permissions.EmploymentView, Permissions.SalaryView,
            Permissions.SalaryExtraView);

        var karta = await MunosabatAsync(toliq, urug.XodimId);
        karta.GetProperty("salary").GetProperty("amount").GetDecimal().ShouldBe(5_000_000m, "oklad o'zgarmaydi");
        karta.GetProperty("totalPay").GetProperty("amount").GetDecimal().ShouldBe(6_000_000m);

        var haqli = karta.GetProperty("duties").EnumerateArray()
            .Single(vazifa => vazifa.GetProperty("eventId").GetGuid() == urug.HaqliVazifa);
        haqli.GetProperty("duty").GetString().ShouldBe("Omborga mas'ul");
        haqli.GetProperty("extraPay").GetProperty("amount").GetDecimal().ShouldBe(1_000_000m);

        (await LentadagiVazifaHaqiAsync(toliq, urug.XodimId, urug.HaqliVazifa)).ShouldBe(1_000_000m);
        (await BuyruqlardagiVazifaHaqiAsync(toliq, urug.Buyruq)).ShouldBe(1_000_000m);
    }

    /// <summary>
    /// Tugatilgan vazifa jamiga kirmaydi, yozuvi esa tarixda tugash sanasi
    /// bilan qoladi; <c>null</c> bilan qayta ochiladi.
    /// </summary>
    [Fact]
    public async Task Tugatilgan_vazifa_jamiga_kirmaydi_va_qayta_ochiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var urug = await UrugAsync();

        using var toliq = await MijozAsync(
            Permissions.EmployeesView, Permissions.EmploymentView, Permissions.SalaryView,
            Permissions.SalaryExtraView, Permissions.EmploymentDuty, Permissions.EmploymentSalary);

        var bugun = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        using (var javob = await TugatAsync(toliq, urug, urug.HaqliVazifa, bugun))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            var hodisa = await javob.Content.ReadFromJsonAsync<JsonElement>(bekorQilish);
            hodisa.GetProperty("dutyEndDate").GetString().ShouldBe(bugun.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        var karta = await MunosabatAsync(toliq, urug.XodimId);
        karta.GetProperty("duties").EnumerateArray()
            .ShouldNotContain(vazifa => vazifa.GetProperty("eventId").GetGuid() == urug.HaqliVazifa);
        karta.GetProperty("totalPay").ValueKind.ShouldBe(JsonValueKind.Null, "haqli faol vazifa qolmadi");
        karta.GetProperty("salary").GetProperty("amount").GetDecimal().ShouldBe(5_000_000m);

        using (var javob = await TugatAsync(toliq, urug, urug.HaqliVazifa, null))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

        (await MunosabatAsync(toliq, urug.XodimId)).GetProperty("totalPay").GetProperty("amount").GetDecimal()
            .ShouldBe(6_000_000m, "qayta ochilgan vazifa jamiga qaytadi");
    }

    /// <summary>
    /// Vazifani tugatish — <c>employment.duty</c>; HAQLI vazifani esa faqat
    /// qo'shimcha haqning ruxsatlari bilan. Haq kiritish ham shunday.
    /// </summary>
    [Fact]
    public async Task Haqli_vazifa_qoshimcha_haq_ruxsatlarini_talab_qiladi()
    {
        var bekorQilish = TestContext.Current.CancellationToken;
        var urug = await UrugAsync();
        var sana = DateOnly.FromDateTime(DateTime.UtcNow);

        using (var ruxsatsiz = await MijozAsync(Permissions.EmploymentView))
        using (var javob = await TugatAsync(ruxsatsiz, urug, urug.HaqsizVazifa, sana))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using var vazifachi = await MijozAsync(
            Permissions.EmploymentView, Permissions.EmploymentDuty, Permissions.EmploymentSalary);

        using (var javob = await TugatAsync(vazifachi, urug, urug.HaqliVazifa, sana))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "oklad ruxsati qo'shimcha haqqa yetmaydi");
            (await XatoKodiAsync(javob)).ShouldBe("auth.ruxsat_yoq");
        }

        using (var javob = await TugatAsync(vazifachi, urug, urug.HaqsizVazifa, sana))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        }

        using (var javob = await vazifachi.PostAsJsonAsync(
            new Uri($"/api/employments/{urug.EmploymentId}/additional-duty", UriKind.Relative),
            new { duty = "Navbatchilik", extraSalaryAmount = 300_000m },
            bekorQilish))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "haqli vazifa berish ham alohida ruxsat bilan");
        }

        // Tugash sanasi boshlanishidan oldin bo'lmaydi
        using var toliq = await MijozAsync(
            Permissions.EmploymentView, Permissions.EmploymentDuty, Permissions.EmploymentSalary,
            Permissions.SalaryExtraView);

        using (var javob = await TugatAsync(toliq, urug, urug.HaqliVazifa, new DateOnly(2020, 1, 1)))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await XatoKodiAsync(javob)).ShouldBe("mehnat.vazifa_tugash_sanasi");
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private sealed record Urug(Guid XodimId, Guid EmploymentId, Guid HaqliVazifa, Guid HaqsizVazifa, string Buyruq);

    /// <summary>Oklad 5 mln; haqli (1 mln) va haqsiz ikkita faol vazifa.</summary>
    private async Task<Urug> UrugAsync()
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var belgi = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8];

        var firma = LegalEntity.Create("Ustama MChJ " + belgi).Value;
        var xodim = Employee.Create(FullName.Create("Устамали", "Ходим", null).Value).Value;
        var munosabat = Employment.Hire(xodim.Id, firma.Id, new DateOnly(2024, 1, 15)).Value;

        munosabat.SetSalary(Money.Create(5_000_000m).Value, new DateOnly(2024, 1, 15)).IsSuccess.ShouldBeTrue();
        var buyruq = "QH-" + belgi;
        munosabat.AddAdditionalDuty(new DateOnly(2024, 6, 1), "Omborga mas'ul", Money.Create(1_000_000m).Value, buyruq)
            .IsSuccess.ShouldBeTrue();
        var haqli = munosabat.Events[^1].Id;
        munosabat.AddAdditionalDuty(new DateOnly(2024, 7, 1), "Navbatchilik").IsSuccess.ShouldBeTrue();
        var haqsiz = munosabat.Events[^1].Id;

        kontekst.AddRange(firma, xodim);
        kontekst.Add(munosabat);
        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new Urug(xodim.Id, munosabat.Id, haqli, haqsiz, buyruq);
    }

    /// <summary>Xodim kartasidagi (yagona) mehnat munosabati.</summary>
    private static async Task<JsonElement> MunosabatAsync(HttpClient client, Guid xodimId)
    {
        using var javob = await client.GetAsync(
            new Uri($"/api/employees/{xodimId}", UriKind.Relative), TestContext.Current.CancellationToken);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var karta = await javob.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return karta.GetProperty("employments").EnumerateArray().Single().Clone();
    }

    private static async Task<decimal?> LentadagiVazifaHaqiAsync(HttpClient client, Guid xodimId, Guid vazifaId)
    {
        using var javob = await client.GetAsync(
            new Uri($"/api/employees/{xodimId}/timeline", UriKind.Relative), TestContext.Current.CancellationToken);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var lenta = await javob.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var hodisa = lenta.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("event"))
            .Single(item => item.GetProperty("id").GetGuid() == vazifaId);

        return Summa(hodisa.GetProperty("toSalary"));
    }

    private static async Task<decimal?> BuyruqlardagiVazifaHaqiAsync(HttpClient client, string buyruq)
    {
        using var javob = await client.GetAsync(
            new Uri($"/api/employments/events?search={buyruq}&type=AdditionalDuty", UriKind.Relative),
            TestContext.Current.CancellationToken);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        var sahifa = await javob.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var qator = sahifa.GetProperty("items").EnumerateArray().Single();

        return Summa(qator.GetProperty("toSalary"));
    }

    private static decimal? Summa(JsonElement pul)
        => pul.ValueKind == JsonValueKind.Null ? null : pul.GetProperty("amount").GetDecimal();

    private static async Task<HttpResponseMessage> TugatAsync(HttpClient client, Urug urug, Guid vazifaId, DateOnly? sana)
        => await client.PutAsJsonAsync(
            new Uri($"/api/employments/{urug.EmploymentId}/events/{vazifaId}/duty-end", UriKind.Relative),
            new { endDate = sana },
            TestContext.Current.CancellationToken);

    private async Task<HttpClient> MijozAsync(params string[] ruxsatlar)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAccounts.TokenAsync(_factory, ruxsatlar));
        return client;
    }

    private static async Task<string?> XatoKodiAsync(HttpResponseMessage javob)
    {
        var muammo = await javob.Content.ReadFromJsonAsync<ProblemResponse>(TestContext.Current.CancellationToken);
        return muammo?.Title;
    }

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
