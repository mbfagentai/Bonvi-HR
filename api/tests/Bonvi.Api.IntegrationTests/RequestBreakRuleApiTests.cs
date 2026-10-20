using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Settings;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Jadval so'rovidagi tanaffus qoidasi — SPEC-verifikatsiya-sorov-tahriri §2.2, §4, §8.4.
/// </summary>
/// <remarks>
/// Qoidaning o'zi (B1–B19) domen testlarida. Bu yerda — SHARTNOMA: o'qish endpointi va uning ruxsati,
/// sozlamalar orqali o'zgartirish (kalitlararo qoida), yaratish yangi sonlarga bo'ysunishi va
/// <c>GET /api/me</c> dagi nusxa.
/// </remarks>
public sealed class RequestBreakRuleApiTests : IClassFixture<BonviApiFactory>
{
    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    private static readonly Uri SozlamalarYoli = new("/api/settings", UriKind.Relative);

    private readonly BonviApiFactory _factory;

    public RequestBreakRuleApiTests(BonviApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Qoida_korish_ruxsati_bilan_200_ruxsatsiz_403_anonim_401()
    {
        var ct = TestContext.Current.CancellationToken;

        using var korish = await MijozAsync(Permissions.AttendanceView);
        using (var javob = await korish.GetAsync(Yol("/schedule-requests/break-rule"), ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            var qoida = await JsonAsync(javob);
            qoida.GetProperty("from").GetString().ShouldBe("12:00:00");
            qoida.GetProperty("to").GetString().ShouldBe("15:00:00");
            qoida.GetProperty("minutes").GetInt32().ShouldBe(60);
            qoida.GetProperty("fridayMaxMinutes").GetInt32().ShouldBe(120);
        }

        using var ruxsatsiz = await MijozAsync(Permissions.ReportsExport);
        (await ruxsatsiz.GetAsync(Yol("/schedule-requests/break-rule"), ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var anonim = _factory.CreateClient();
        (await anonim.GetAsync(Yol("/schedule-requests/break-rule"), ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// To'rt kalit <c>settings.manage</c> ostida; zid qiymat — 400 <c>davomat.tanaffus_qoidasi_nomuvofiq</c>;
    /// o'zgargan qoidani yaratish darhol qo'llaydi, o'qish endpointi va <c>GET /api/me</c> ham ko'rsatadi.
    /// </summary>
    [Fact]
    public async Task Qoida_sozlamada_ozgaradi_yaratish_yangi_sonlarga_boysunadi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();

        using (var ruxsatsiz = await MijozAsync(Permissions.AttendanceView, Permissions.AttendanceRequestsManage))
        {
            (await ruxsatsiz.PutAsJsonAsync(SozlamalarYoli, Sozlamalar(("attendance.requestBreakMinutes", "45")), ct))
                .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var zid = await admin.PutAsJsonAsync(
                   SozlamalarYoli,
                   Sozlamalar((SystemSettingKeys.RequestBreakFrom, "900"), (SystemSettingKeys.RequestBreakTo, "720")),
                   ct))
        {
            await XatoKutAsync(zid, HttpStatusCode.BadRequest, "davomat.tanaffus_qoidasi_nomuvofiq");
        }

        try
        {
            // 13:00–14:00, 30 daqiqa, jumada 45 gacha (SPEC §4.4 B19).
            using (var saqlash = await admin.PutAsJsonAsync(
                       SozlamalarYoli,
                       Sozlamalar(
                           (SystemSettingKeys.RequestBreakFrom, "780"),
                           (SystemSettingKeys.RequestBreakTo, "840"),
                           (SystemSettingKeys.RequestBreakMinutes, "30"),
                           (SystemSettingKeys.RequestBreakFridayMaxMinutes, "45")),
                       ct))
            {
                saqlash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(saqlash));
            }

            var qoida = await JsonAsync(await admin.GetAsync(Yol("/schedule-requests/break-rule"), ct));
            qoida.GetProperty("from").GetString().ShouldBe("13:00:00");
            qoida.GetProperty("minutes").GetInt32().ShouldBe(30);
            qoida.GetProperty("fridayMaxMinutes").GetInt32().ShouldBe(45);

            var belgi = Belgi();
            var xodim = await XodimAsync(belgi);
            var dushanba = Keyingi(BugunToshkentda().AddDays(1), DayOfWeek.Monday);
            var juma = Keyingi(BugunToshkentda().AddDays(1), DayOfWeek.Friday);

            // Avvalgi sukut (12:00–13:00, 60) endi o'tmaydi — HR tomonida ham.
            using (var eski = await admin.PostAsJsonAsync(Yol("/schedule-requests"), SorovTanasi(xodim, dushanba, "12:00:00", "13:00:00"), ct))
            {
                await XatoKutAsync(eski, HttpStatusCode.BadRequest, "davomat.jadval_sorovi_tanaffus_oraligi");
            }

            using (var dushanbada = await admin.PostAsJsonAsync(Yol("/schedule-requests"), SorovTanasi(xodim, dushanba, "13:00:00", "13:45:00"), ct))
            {
                await XatoKutAsync(dushanbada, HttpStatusCode.BadRequest, "davomat.jadval_sorovi_tanaffus_davomiyligi");
            }

            using (var jumada = await admin.PostAsJsonAsync(Yol("/schedule-requests"), SorovTanasi(xodim, juma, "13:00:00", "13:45:00"), ct))
            {
                jumada.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(jumada));
            }

            // Xodim ilovasi qoidani `GET /api/me` dan oladi.
            using var ilova = await IlovaMijoziAsync(admin, xodim);
            var men = await JsonAsync(await ilova.GetAsync(new Uri("/api/me", UriKind.Relative), ct));
            var ilovadagi = men.GetProperty("requestBreakRule");
            ilovadagi.GetProperty("from").GetString().ShouldBe("13:00:00");
            ilovadagi.GetProperty("to").GetString().ShouldBe("14:00:00");
            ilovadagi.GetProperty("minutes").GetInt32().ShouldBe(30);
            ilovadagi.GetProperty("fridayMaxMinutes").GetInt32().ShouldBe(45);

            using (var ilovadan = await ilova.PostAsJsonAsync(new Uri("/api/me/schedule-requests", UriKind.Relative), SorovTanasi(null, dushanba, "13:00:00", "13:30:00"), ct))
            {
                ilovadan.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(ilovadan));
            }
        }
        finally
        {
            // Sinf bazasidagi boshqa testlar sukut qoidani kutadi.
            using var tiklash = await admin.PutAsJsonAsync(
                SozlamalarYoli,
                Sozlamalar(
                    (SystemSettingKeys.RequestBreakFrom, "720"),
                    (SystemSettingKeys.RequestBreakTo, "900"),
                    (SystemSettingKeys.RequestBreakMinutes, "60"),
                    (SystemSettingKeys.RequestBreakFridayMaxMinutes, "120")),
                ct);
            tiklash.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tiklash));
        }
    }

    [Fact]
    public async Task Men_javobida_qoida_va_turlarning_ish_vaqti_bayrogi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();

        Guid ishTuri;
        using (var tur = await admin.PostAsJsonAsync(
                   new Uri("/api/catalog/absence-types", UriKind.Relative),
                   new { name = "Ish bilan " + belgi, countsAsWorkTime = true },
                   ct))
        {
            tur.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tur));
            ishTuri = (await JsonAsync(tur)).GetProperty("id").GetGuid();
        }

        Guid shaxsiyTuri;
        using (var tur = await admin.PostAsJsonAsync(
                   new Uri("/api/catalog/absence-types", UriKind.Relative),
                   new { name = "Shaxsiy " + belgi, countsAsWorkTime = false },
                   ct))
        {
            tur.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tur));
            shaxsiyTuri = (await JsonAsync(tur)).GetProperty("id").GetGuid();
        }

        var xodim = await XodimAsync(belgi);
        using var ilova = await IlovaMijoziAsync(admin, xodim);

        var men = await JsonAsync(await ilova.GetAsync(new Uri("/api/me", UriKind.Relative), ct));
        var turlar = men.GetProperty("absenceTypes").EnumerateArray().ToList();

        turlar.Single(item => item.GetProperty("id").GetGuid() == ishTuri).GetProperty("countsAsWorkTime").GetBoolean().ShouldBeTrue();
        turlar.Single(item => item.GetProperty("id").GetGuid() == shaxsiyTuri).GetProperty("countsAsWorkTime").GetBoolean().ShouldBeFalse();

        var qoida = men.GetProperty("requestBreakRule");
        qoida.GetProperty("minutes").GetInt32().ShouldBe(60);
        qoida.GetProperty("fridayMaxMinutes").GetInt32().ShouldBe(120);
    }

    // ────────────────────────────────────────────────────────────
    //  Muhit tayyorlash
    // ────────────────────────────────────────────────────────────

    private static object Sozlamalar(params (string Key, string Value)[] items)
        => new { items = items.Select(item => new { key = item.Key, value = item.Value }) };

    /// <summary>«Kunni o'zgartirish» — 09:00–18:00 ish kuni, berilgan tanaffus oynasi bilan.</summary>
    private static object SorovTanasi(Guid? xodim, DateOnly sana, string tanaffusBoshi, string tanaffusOxiri)
        => new
        {
            employeeId = xodim,
            kind = "Change",
            days = new[]
            {
                new { date = Iso(sana), isWorking = true, startTime = "09:00:00", endTime = "18:00:00", breakStart = tanaffusBoshi, breakEnd = tanaffusOxiri },
            },
        };

    private async Task<HttpClient> MijozAsync(params string[] ruxsatlar)
    {
        var mijoz = _factory.CreateClient();
        mijoz.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await TestAccounts.TokenAsync(_factory, ruxsatlar));

        return mijoz;
    }

    /// <summary>Xodim — o'z firmasi, mehnat munosabati va takrorlanmas telefoni (ilova logini) bilan.</summary>
    private async Task<Guid> XodimAsync(string belgi)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Tanaffus MCHJ " + belgi).Value;
        var xodim = Employee.Create(FullName.Create("Tanaffusov", "Xodim", belgi).Value).Value;
        var telefon = "9" + Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);
        xodim.SetPhones(PhoneNumber.Create(telefon).Value);

        kontekst.AddRange(firma, xodim);
        kontekst.Add(Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1)).Value);
        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        return xodim.Id;
    }

    /// <summary>HR «Ilova» bo'limini ochadi, parolni ko'radi va xodim sifatida kiradi.</summary>
    private async Task<HttpClient> IlovaMijoziAsync(HttpClient admin, Guid xodimId)
    {
        var ct = TestContext.Current.CancellationToken;

        using var bolim = await admin.GetAsync(new Uri($"/api/employees/{xodimId}/app-access", UriKind.Relative), ct);
        bolim.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(bolim));
        var login = (await JsonAsync(bolim)).GetProperty("login").GetString()!;

        using var parolJavobi = await admin.GetAsync(new Uri($"/api/employees/{xodimId}/app-access/password", UriKind.Relative), ct);
        parolJavobi.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(parolJavobi));
        var parol = (await JsonAsync(parolJavobi)).GetProperty("password").GetString()!;

        using var kirish = await _factory.CreateClient().PostAsJsonAsync(
            new Uri("/api/employee-auth/login", UriKind.Relative),
            new { login, password = parol },
            ct);
        kirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(kirish));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            (await JsonAsync(kirish)).GetProperty("accessToken").GetString()!);

        return client;
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

    private static Uri Yol(string yol) => new("/api/attendance" + yol, UriKind.Relative);

    private static string Belgi() => Guid.NewGuid().ToString("N")[..8];

    private static string Iso(DateOnly sana) => sana.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly BugunToshkentda() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(Tashkent).DateTime);

    /// <summary><paramref name="dan"/> yoki undan keyingi berilgan hafta kuni.</summary>
    private static DateOnly Keyingi(DateOnly dan, DayOfWeek kun)
    {
        var sana = dan;
        while (sana.DayOfWeek != kun)
        {
            sana = sana.AddDays(1);
        }

        return sana;
    }

    private static async Task XatoKutAsync(HttpResponseMessage javob, HttpStatusCode holat, string kod)
    {
        var matn = await MatnAsync(javob);
        javob.StatusCode.ShouldBe(holat, matn);
        JsonDocument.Parse(matn).RootElement.GetProperty("title").GetString().ShouldBe(kod, matn);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage javob)
        => JsonDocument.Parse(await MatnAsync(javob)).RootElement.Clone();

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
