using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Boshliq vakansiya so'rovini o'zi yuboradi — «Jamoa» tabidan va rahbar botidan (egasi, 2026-10-31).
/// </summary>
/// <remarks>
/// Eng qimmat xatolar: boshliq yoki kalit o'z qamrovidan tashqaridagi bo'limga so'rov yuborishi yoki
/// boshqa bo'limning so'rovlarini ko'rishi/bekor qilishi, va so'rov HR ro'yxatiga tushmasligi.
/// </remarks>
public sealed class TeamVacancyRequestApiTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public TeamVacancyRequestApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Boshliq_ilovadan_sorov_yuboradi_HR_royxatiga_tushadi_va_qaytarib_oladi()
    {
        var dunyo = await DunyoAsync();
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        using var ilova = await IlovaMijoziAsync(admin, dunyo.Boshliq.Id);

        // Bitta bo'limli boshliq bo'limni ko'rsatmaydi — zavodga tushadi, so'rovchi o'zi
        using var yuborish = await ilova.PostAsJsonAsync(
            Yol("/api/me/team/vacancy-requests"),
            new { title = "Payvandchi " + dunyo.Belgi, openings = 2, note = "Yangi liniya" },
            Ct);
        yuborish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuborish));
        var sorov = await JsonAsync(yuborish);
        sorov.GetProperty("departmentId").GetGuid().ShouldBe(dunyo.Zavod);
        sorov.GetProperty("requestedByEmployeeId").GetGuid().ShouldBe(dunyo.Boshliq.Id);
        sorov.GetProperty("status").GetString().ShouldBe("Pending");
        var id = sorov.GetProperty("id").GetGuid();

        // Ichki bo'limga ham bo'ladi
        using (var sexga = await ilova.PostAsJsonAsync(
                   Yol("/api/me/team/vacancy-requests"),
                   new { departmentId = dunyo.Sex, title = "Tokar " + dunyo.Belgi },
                   Ct))
        {
            sexga.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(sexga));
        }

        // HR menyusida ko'rinadi
        var hr = await admin.GetFromJsonAsync<JsonElement>(Yol($"/api/recruiting/vacancy-requests?status=Pending&search={dunyo.Belgi}"), Ct);
        hr.GetProperty("items").EnumerateArray().ShouldContain(item => item.GetProperty("id").GetGuid() == id);

        // Qo'shni bo'limga — «yo'q» bilan bir xil javob
        using (var qoshniga = await ilova.PostAsJsonAsync(
                   Yol("/api/me/team/vacancy-requests"),
                   new { departmentId = dunyo.Qoshni, title = "Begona " + dunyo.Belgi },
                   Ct))
        {
            await XatoKutAsync(qoshniga, HttpStatusCode.NotFound, "jamoa.bolim_topilmadi");
        }

        // Ro'yxatda qo'shni bo'limning so'rovi yo'q, o'zinikilari bor
        var begonaId = await HrSorovAsync(admin, dunyo.Qoshni, dunyo.Begona.Id, "Qoshni " + dunyo.Belgi);
        var royxat = await ilova.GetFromJsonAsync<JsonElement>(Yol("/api/me/team/vacancy-requests"), Ct);
        var idlar = royxat.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();
        idlar.ShouldContain(id);
        idlar.ShouldNotContain(begonaId);
        royxat.GetProperty("total").GetInt32().ShouldBe(2);

        // Begona so'rovni bekor qilib bo'lmaydi
        using (var begona = await ilova.PostAsync(Yol($"/api/me/team/vacancy-requests/{begonaId}/cancel"), null, Ct))
        {
            await XatoKutAsync(begona, HttpStatusCode.NotFound, "vakansiya_sorovi.topilmadi");
        }

        // O'zinikini qaytarib oladi, ikkinchi marta — 409
        using (var bekor = await ilova.PostAsync(Yol($"/api/me/team/vacancy-requests/{id}/cancel"), null, Ct))
        {
            bekor.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(bekor));
            (await JsonAsync(bekor)).GetProperty("status").GetString().ShouldBe("Cancelled");
        }

        using (var yana = await ilova.PostAsync(Yol($"/api/me/team/vacancy-requests/{id}/cancel"), null, Ct))
        {
            await XatoKutAsync(yana, HttpStatusCode.Conflict, "vakansiya_sorovi.qaror_qilingan");
        }
    }

    [Fact]
    public async Task Oddiy_xodim_sorov_yubora_olmaydi()
    {
        var dunyo = await DunyoAsync();
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        using var ilova = await IlovaMijoziAsync(admin, dunyo.Azo.Id);

        using var javob = await ilova.PostAsJsonAsync(
            Yol("/api/me/team/vacancy-requests"),
            new { departmentId = dunyo.Sex, title = "Tokar " + dunyo.Belgi },
            Ct);
        await XatoKutAsync(javob, HttpStatusCode.Forbidden, "jamoa.boshliq_emas");

        using var royxat = await ilova.GetAsync(Yol("/api/me/team/vacancy-requests"), Ct);
        await XatoKutAsync(royxat, HttpStatusCode.Forbidden, "jamoa.boshliq_emas");
    }

    [Fact]
    public async Task Bot_bolim_kaliti_bilan_sorov_yuboradi_soraguvchi_boshliq()
    {
        var dunyo = await DunyoAsync();
        using var admin = await PublicApiHarness.AdminMijozAsync(_factory);
        using var bot = PublicApiHarness.KalitMijozi(_factory, await KalitAsync(admin, dunyo.Zavod));

        // Sexda boshliq yo'q — so'rovchi zavod boshlig'i
        using var yuborish = await bot.PostAsJsonAsync(
            Yol("/api/public/v1/team/vacancy-requests"),
            new { departmentId = dunyo.Sex, title = "Elektrik " + dunyo.Belgi, openings = 1, neededBy = "2026-12-01" },
            Ct);
        yuborish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(yuborish));
        var sorov = await JsonAsync(yuborish);
        sorov.GetProperty("departmentId").GetGuid().ShouldBe(dunyo.Sex);
        sorov.GetProperty("requestedByEmployeeId").GetGuid().ShouldBe(dunyo.Boshliq.Id);
        sorov.GetProperty("neededBy").GetString().ShouldBe("2026-12-01");
        var id = sorov.GetProperty("id").GetGuid();

        var hr = await admin.GetFromJsonAsync<JsonElement>(Yol($"/api/recruiting/vacancy-requests?search={dunyo.Belgi}"), Ct);
        hr.GetProperty("items").EnumerateArray().ShouldContain(item => item.GetProperty("id").GetGuid() == id);

        // Qamrovdan tashqari bo'lim va uning so'rovi
        using (var qoshniga = await bot.PostAsJsonAsync(
                   Yol("/api/public/v1/team/vacancy-requests"),
                   new { departmentId = dunyo.Qoshni, title = "Begona " + dunyo.Belgi },
                   Ct))
        {
            await XatoKutAsync(qoshniga, HttpStatusCode.NotFound, "tashqi.bolim_topilmadi");
        }

        var begonaId = await HrSorovAsync(admin, dunyo.Qoshni, dunyo.Begona.Id, "Qoshni " + dunyo.Belgi);
        var royxat = await bot.GetFromJsonAsync<JsonElement>(Yol("/api/public/v1/team/vacancy-requests?status=Pending"), Ct);
        var idlar = royxat.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();
        idlar.ShouldBe([id]);

        using (var begona = await bot.PostAsync(Yol($"/api/public/v1/team/vacancy-requests/{begonaId}/cancel"), null, Ct))
        {
            await XatoKutAsync(begona, HttpStatusCode.NotFound, "vakansiya_sorovi.topilmadi");
        }

        using (var bekor = await bot.PostAsync(Yol($"/api/public/v1/team/vacancy-requests/{id}/cancel"), null, Ct))
        {
            bekor.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(bekor));
        }

        // Boshliqsiz bo'lim kaliti — so'rovchi yo'q
        using var boshliqsiz = PublicApiHarness.KalitMijozi(_factory, await KalitAsync(admin, dunyo.Qoshni));
        using (var javob = await boshliqsiz.PostAsJsonAsync(
                   Yol("/api/public/v1/team/vacancy-requests"),
                   new { title = "Tokar " + dunyo.Belgi },
                   Ct))
        {
            await XatoKutAsync(javob, HttpStatusCode.BadRequest, "tashqi.boshliq_yoq");
        }

        // Oddiy kalit bu yo'lga kirmaydi
        var oddiy = await PublicApiHarness.KalitYaratAsync(admin, PublicApiHarness.NoyobNom("Oddiy"));
        using var holding = PublicApiHarness.KalitMijozi(_factory, oddiy.Key);
        using var yopiq = await holding.PostAsJsonAsync(
            Yol("/api/public/v1/team/vacancy-requests"),
            new { title = "Tokar " + dunyo.Belgi },
            Ct);
        yopiq.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    private sealed record Odam(Guid Id, string FullName);

    /// <summary>Zavod (boshliq — Boshliq) → Sex (Azo, boshliqsiz); Qoshni (Begona, boshliqsiz).</summary>
    private sealed record Dunyo(string Belgi, Guid Zavod, Guid Sex, Guid Qoshni, Odam Boshliq, Odam Azo, Odam Begona);

    private async Task<Dunyo> DunyoAsync()
    {
        var belgi = "VS" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("So'rov MCHJ " + belgi).Value;
        var zavod = Department.Create("Zavod " + belgi).Value;
        var sex = Department.Create("Sex " + belgi, zavod.Id).Value;
        var qoshni = Department.Create("Qoshni " + belgi).Value;
        kontekst.AddRange(firma, zavod, sex, qoshni);

        Odam Ishga(string familiya, Department bolim)
        {
            var ism = FullName.Create(familiya + belgi, "Sorov", null).Value;
            var xodim = Employee.Create(ism).Value;
            xodim.SetPhones(PhoneNumber.Create("+99891" + RandomNumberGenerator.GetInt32(1_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture)).Value);
            kontekst.AddRange(xodim, Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1), bolim.Id).Value);
            return new Odam(xodim.Id, ism.Full);
        }

        var boshliq = Ishga("Boshliq", zavod);
        var azo = Ishga("Azo", sex);
        var begona = Ishga("Begona", qoshni);
        zavod.AssignHead(boshliq.Id);

        await kontekst.SaveChangesAsync(Ct);
        await qamrov.ServiceProvider.GetRequiredService<IDepartmentRepository>().RebuildHierarchyAsync(Ct);

        return new Dunyo(belgi, zavod.Id, sex.Id, qoshni.Id, boshliq, azo, begona);
    }

    private static async Task<Guid> HrSorovAsync(HttpClient admin, Guid bolim, Guid soraguvchi, string nom)
    {
        using var javob = await admin.PostAsJsonAsync(
            Yol("/api/recruiting/vacancy-requests"),
            new { departmentId = bolim, requestedByEmployeeId = soraguvchi, title = nom },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        return (await JsonAsync(javob)).GetProperty("id").GetGuid();
    }

    private static async Task<string> KalitAsync(HttpClient admin, Guid bolim)
    {
        using var javob = await admin.PostAsJsonAsync(
            Yol("/api/api-keys"),
            new { name = PublicApiHarness.NoyobNom("Rahbar boti"), scope = "DepartmentAttendance", departmentId = bolim },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
        return (await JsonAsync(javob)).GetProperty("key").GetString()!;
    }

    /// <summary>HR «Ilova» bo'limini ochadi (hisob kafolatlanadi), parolni ko'radi va kiradi.</summary>
    private async Task<HttpClient> IlovaMijoziAsync(HttpClient admin, Guid xodimId)
    {
        string login;

        using (var bolim = await admin.GetAsync(Yol($"/api/employees/{xodimId}/app-access"), Ct))
        {
            bolim.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(bolim));
            login = (await JsonAsync(bolim)).GetProperty("login").GetString()!;
        }

        string parol;

        using (var javob = await admin.GetAsync(Yol($"/api/employees/{xodimId}/app-access/password"), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            parol = (await JsonAsync(javob)).GetProperty("password").GetString()!;
        }

        using var mijoz = _factory.CreateClient();
        using var kirish = await mijoz.PostAsJsonAsync(Yol("/api/employee-auth/login"), new { login, password = parol }, Ct);
        kirish.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(kirish));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            (await JsonAsync(kirish)).GetProperty("accessToken").GetString()!);

        return client;
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
        => await javob.Content.ReadAsStringAsync(Ct);

    private static Uri Yol(string yol) => new(yol, UriKind.Relative);
}
