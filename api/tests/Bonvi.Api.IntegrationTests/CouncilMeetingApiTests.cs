using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bonvi.Api.IntegrationTests;

/// <summary>The council's minutes: who may do what, and how a decision reaches the candidate's card.</summary>
public sealed class CouncilMeetingApiTests : IClassFixture<BonviApiFactory>
{
    private const string Yol = "/api/recruiting/council-meetings";

    private static int _kun;
    private static int _raqam;

    private readonly BonviApiFactory _factory;

    public CouncilMeetingApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ruxsatsiz_kirib_bolmaydi_korish_ruxsati_yozmaydi()
    {
        var sana = KeyingiKun();
        var kerakli = new { decision = "Accept" };

        using var tokensiz = _factory.CreateClient();
        using (var javob = await tokensiz.GetAsync(new Uri($"{Yol}/{sana:yyyy-MM-dd}", UriKind.Relative), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var begona = await MijozAsync("employees.view");
        using (var javob = await begona.GetAsync(new Uri(Yol, UriKind.Relative), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using var koruvchi = await MijozAsync("recruiting.view");
        using (var javob = await koruvchi.GetAsync(new Uri($"{Yol}/{sana:yyyy-MM-dd}", UriKind.Relative), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (var javob = await koruvchi.PutAsJsonAsync(
            new Uri($"{Yol}/{sana:yyyy-MM-dd}", UriKind.Relative), new { members = Array.Empty<object>() }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var javob = await koruvchi.PutAsJsonAsync(
            new Uri($"{Yol}/{sana:yyyy-MM-dd}/candidates/{Guid.NewGuid()}", UriKind.Relative), kerakli, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Chaqirilgan_nomzod_royxatda_qaror_kartaga_bitta_yozuv_bolib_tushadi()
    {
        var sana = KeyingiKun();
        using var admin = await MijozAsync("recruiting.view", "recruiting.manage", "salary.view");
        var nomzod = await ChaqirilganNomzodAsync(admin, sana);

        var majlis = await MajlisAsync(admin, sana);
        majlis.GetProperty("number").ValueKind.ShouldBe(JsonValueKind.Null);
        var qator = Qator(majlis, nomzod);
        qator.GetProperty("decided").GetBoolean().ShouldBeFalse();
        qator.GetProperty("isActive").GetBoolean().ShouldBeTrue();

        await QarorAsync(admin, sana, nomzod, new
        {
            decision = "Conditional",
            practical = "Passed",
            offeredSalary = 5_500_000m,
            startDate = sana.AddDays(5),
            condition = "Ustoz bilan",
            conclusion = "Omborda sinab ko'rsa bo'ladi",
        });

        majlis = await MajlisAsync(admin, sana);
        majlis.GetProperty("number").GetInt32().ShouldBeGreaterThan(0);
        qator = Qator(majlis, nomzod);
        qator.GetProperty("decided").GetBoolean().ShouldBeTrue();
        qator.GetProperty("decision").GetString().ShouldBe("Conditional");
        qator.GetProperty("practical").GetString().ShouldBe("Passed");
        qator.GetProperty("offeredSalary").GetDecimal().ShouldBe(5_500_000m);
        qator.GetProperty("startDate").GetString().ShouldBe(sana.AddDays(5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        var (holat, yozuvlar) = await KengashYozuvlariAsync(admin, nomzod, sana);
        holat.ShouldBe("InProgress");
        yozuvlar.ShouldHaveSingleItem();
        yozuvlar[0].ShouldBe("Shartli qabul — surishtiruvdan keyin\nOmborda sinab ko'rsa bo'ladi\nShart: Ustoz bilan");

        await QarorAsync(admin, sana, nomzod, new { decision = "Reserve", conclusion = "Keyinroq" });

        (_, yozuvlar) = await KengashYozuvlariAsync(admin, nomzod, sana);
        yozuvlar.ShouldHaveSingleItem();
        yozuvlar[0].ShouldBe("Zaxira\nKeyinroq");
    }

    [Fact]
    public async Task Oldin_yozilgan_kengash_xulosasi_taklif_qilinadi_va_shu_yozuv_tuzatiladi()
    {
        var sana = KeyingiKun();
        using var admin = await MijozAsync("recruiting.view", "recruiting.manage");
        var nomzod = await ChaqirilganNomzodAsync(admin, sana);
        var kengash = await KengashBosqichiAsync(admin);

        using (var javob = await admin.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod}/stages", UriKind.Relative),
            new { stageId = kengash, date = sana, conclusion = "HR yozgan matn" },
            Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var qator = Qator(await MajlisAsync(admin, sana), nomzod);
        qator.GetProperty("decided").GetBoolean().ShouldBeFalse();
        qator.GetProperty("conclusion").GetString().ShouldBe("HR yozgan matn");

        await QarorAsync(admin, sana, nomzod, new { decision = "Accept", conclusion = "HR yozgan matn" });

        var (_, yozuvlar) = await KengashYozuvlariAsync(admin, nomzod, sana);
        yozuvlar.ShouldHaveSingleItem();
        yozuvlar[0].ShouldBe("Qabul\nHR yozgan matn");
    }

    [Fact]
    public async Task Oklad_ruxsatsiz_korinmaydi_va_ochirilmaydi()
    {
        var sana = KeyingiKun();
        using var toliq = await MijozAsync("recruiting.view", "recruiting.manage", "salary.view");
        using var okladsiz = await MijozAsync("recruiting.view", "recruiting.manage");
        var nomzod = await ChaqirilganNomzodAsync(toliq, sana);

        await QarorAsync(toliq, sana, nomzod, new { decision = "Accept", offeredSalary = 7_000_000m });

        var majlis = await MajlisAsync(okladsiz, sana);
        majlis.GetProperty("canSeeSalary").GetBoolean().ShouldBeFalse();
        Qator(majlis, nomzod).GetProperty("offeredSalary").ValueKind.ShouldBe(JsonValueKind.Null);

        await QarorAsync(okladsiz, sana, nomzod, new { decision = "Reject", offeredSalary = 1m });

        var qator = Qator(await MajlisAsync(toliq, sana), nomzod);
        qator.GetProperty("decision").GetString().ShouldBe("Reject");
        qator.GetProperty("offeredSalary").GetDecimal().ShouldBe(7_000_000m);
    }

    [Fact]
    public async Task Azolar_saqlanadi_keyingi_majlisga_belgisiz_otadi()
    {
        var sana = KeyingiKun();
        var keyingi = KeyingiKun();
        using var admin = await MijozAsync("recruiting.view", "recruiting.manage");

        using (var javob = await admin.PutAsJsonAsync(
            new Uri($"{Yol}/{sana:yyyy-MM-dd}", UriKind.Relative),
            new
            {
                members = new object[]
                {
                    new { fullName = "  Rais Ismi ", role = "Rais", present = true },
                    new { fullName = "   ", role = "Bo'sh qator", present = true },
                },
                followUp = new { resultsDeliveredOn = sana.AddDays(1) },
            },
            Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NoContent, await javob.Content.ReadAsStringAsync(Ct));
        }

        var majlis = await MajlisAsync(admin, sana);
        var azo = majlis.GetProperty("members").EnumerateArray().ShouldHaveSingleItem();
        azo.GetProperty("fullName").GetString().ShouldBe("Rais Ismi");
        azo.GetProperty("present").GetBoolean().ShouldBeTrue();
        majlis.GetProperty("followUp").GetProperty("resultsDeliveredOn").GetString()
            .ShouldBe(sana.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        var yangi = await MajlisAsync(admin, keyingi);
        yangi.GetProperty("members").EnumerateArray().ShouldContain(row =>
            row.GetProperty("fullName").GetString() == "Rais Ismi" && !row.GetProperty("present").GetBoolean());
    }

    [Fact]
    public async Task Qarorsiz_saqlanmaydi_notanish_nomzod_topilmaydi()
    {
        var sana = KeyingiKun();
        using var admin = await MijozAsync("recruiting.view", "recruiting.manage");
        var nomzod = await ChaqirilganNomzodAsync(admin, sana);

        using (var javob = await admin.PutAsJsonAsync(
            new Uri($"{Yol}/{sana:yyyy-MM-dd}/candidates/{nomzod}", UriKind.Relative), new { conclusion = "Matn" }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using (var javob = await admin.PutAsJsonAsync(
            new Uri($"{Yol}/{sana:yyyy-MM-dd}/candidates/{Guid.NewGuid()}", UriKind.Relative), new { decision = "Accept" }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        Qator(await MajlisAsync(admin, sana), nomzod).GetProperty("decided").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Voronkadan_chiqqan_nomzod_bayonnomada_qoladi_yangi_bosqich_yozilmaydi()
    {
        var sana = KeyingiKun();
        using var admin = await MijozAsync("recruiting.view", "recruiting.manage");
        var nomzod = await ChaqirilganNomzodAsync(admin, sana);

        using (var javob = await admin.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{nomzod}/reject", UriKind.Relative), new { reason = "Yo'q" }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await QarorAsync(admin, sana, nomzod, new { decision = "Reject" });

        var qator = Qator(await MajlisAsync(admin, sana), nomzod);
        qator.GetProperty("decided").GetBoolean().ShouldBeTrue();
        qator.GetProperty("isActive").GetBoolean().ShouldBeFalse();

        var (holat, yozuvlar) = await KengashYozuvlariAsync(admin, nomzod, sana);
        holat.ShouldBe("Rejected");
        yozuvlar.ShouldBeEmpty();
    }

    [Fact]
    public async Task Kunlar_royxatida_chaqirilganlar_va_qarorlar_sanaladi()
    {
        var sana = KeyingiKun();
        using var admin = await MijozAsync("recruiting.view", "recruiting.manage");
        var birinchi = await ChaqirilganNomzodAsync(admin, sana);
        await ChaqirilganNomzodAsync(admin, sana);
        await QarorAsync(admin, sana, birinchi, new { decision = "Accept" });

        var kunlar = await admin.GetFromJsonAsync<JsonElement>(new Uri(Yol, UriKind.Relative), Ct);
        var kun = kunlar.EnumerateArray().Single(row => row.GetProperty("date").GetString() == sana.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        kun.GetProperty("invited").GetInt32().ShouldBe(2);
        kun.GetProperty("decided").GetInt32().ShouldBe(1);
    }

    private static DateOnly KeyingiKun()
        => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30 + Interlocked.Increment(ref _kun));

    private static JsonElement Qator(JsonElement majlis, Guid nomzod)
        => majlis.GetProperty("candidates").EnumerateArray()
            .Single(row => row.GetProperty("candidateId").GetGuid() == nomzod);

    private static async Task<JsonElement> MajlisAsync(HttpClient mijoz, DateOnly sana)
        => await mijoz.GetFromJsonAsync<JsonElement>(new Uri($"{Yol}/{sana:yyyy-MM-dd}", UriKind.Relative), Ct);

    private static async Task QarorAsync(HttpClient mijoz, DateOnly sana, Guid nomzod, object tana)
    {
        using var javob = await mijoz.PutAsJsonAsync(
            new Uri($"{Yol}/{sana:yyyy-MM-dd}/candidates/{nomzod}", UriKind.Relative), tana, Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.NoContent, await javob.Content.ReadAsStringAsync(Ct));
    }

    private static async Task<Guid> KengashBosqichiAsync(HttpClient mijoz)
    {
        var bosqichlar = await mijoz.GetFromJsonAsync<JsonElement>(
            new Uri("/api/recruiting/stages?onlyActive=true", UriKind.Relative), Ct);
        return bosqichlar.EnumerateArray()
            .Single(row => row.GetProperty("name").GetString() == "Кенгаш")
            .GetProperty("id").GetGuid();
    }

    private static async Task<(string Holat, List<string?> Yozuvlar)> KengashYozuvlariAsync(
        HttpClient mijoz, Guid nomzod, DateOnly sana)
    {
        var kengash = await KengashBosqichiAsync(mijoz);
        var karta = await mijoz.GetFromJsonAsync<JsonElement>(
            new Uri($"/api/recruiting/candidates/{nomzod}", UriKind.Relative), Ct);

        return (
            karta.GetProperty("status").GetString()!,
            karta.GetProperty("stages").EnumerateArray()
                .Where(row => row.GetProperty("stageId").GetGuid() == kengash
                    && row.GetProperty("date").GetString() == sana.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Select(row => row.GetProperty("conclusion").GetString())
                .ToList());
    }

    private static async Task<Guid> ChaqirilganNomzodAsync(HttpClient mijoz, DateOnly sana)
    {
        var belgi = Interlocked.Increment(ref _raqam);

        using var vakansiya = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/vacancies", UriKind.Relative),
            new { title = $"Кенгаш синови {Guid.NewGuid():N}", openings = 1, openedAt = new DateOnly(2026, 3, 1) },
            Ct);
        vakansiya.StatusCode.ShouldBe(HttpStatusCode.OK, await vakansiya.Content.ReadAsStringAsync(Ct));
        var vakansiyaId = (await vakansiya.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();

        using var nomzod = await mijoz.PostAsJsonAsync(
            new Uri("/api/recruiting/candidates", UriKind.Relative),
            new
            {
                vacancyId = vakansiyaId,
                lastName = "Кенгашов" + belgi,
                firstName = "Азиз",
                phone = "93" + Random.Shared.Next(1_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture),
                birthDate = new DateOnly(1995, 5, 20),
                source = "Manual",
                referralChannelId = ReferralSeeds.Telegram,
            },
            Ct);
        nomzod.StatusCode.ShouldBe(HttpStatusCode.OK, await nomzod.Content.ReadAsStringAsync(Ct));
        var id = (await nomzod.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();

        using var taklif = await mijoz.PostAsJsonAsync(
            new Uri($"/api/recruiting/candidates/{id}/interview-invite", UriKind.Relative),
            new { interviewAt = new DateTimeOffset(sana.ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(5)) },
            Ct);
        taklif.StatusCode.ShouldBe(HttpStatusCode.OK, await taklif.Content.ReadAsStringAsync(Ct));

        return id;
    }

    private async Task<HttpClient> MijozAsync(params string[] ruxsatlar)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await TestAccounts.TokenAsync(_factory, ruxsatlar));
        return client;
    }
}
