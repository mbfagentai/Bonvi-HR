using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// «Vakansiya so'rovlari» menyusi — uchi-uchiga (docs/SPEC-vakansiya-sorovi.md).
/// </summary>
/// <remarks>
/// Har bir test o'z belgisi bilan bo'lim va lavozim yaratadi va ro'yxatni <c>search</c> bilan shu
/// belgiga toraytiradi — bazada boshqa testlarning so'rovlari ham bor.
/// </remarks>
public sealed class VacancyRequestApiTests : IClassFixture<BonviApiFactory>
{
    private const string Yol = "/api/recruiting/vacancy-requests";

    private readonly BonviApiFactory _factory;

    public VacancyRequestApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ────────────────────────────────────────────────────────────
    //  Ruxsat
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tokensiz_401_rekruting_korishsiz_403()
    {
        using var tokensiz = _factory.CreateClient();
        using (var javob = await tokensiz.GetAsync(new Uri(Yol, UriKind.Relative), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var begona = await MijozAsync("employees.view");
        using (var javob = await begona.GetAsync(new Uri(Yol, UriKind.Relative), Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
    }

    /// <summary>
    /// «recruiting.view» — ko'rish va KIRITISH; qaror (vakansiya ochish, rad etish, bekor qilish) —
    /// faqat «recruiting.manage».
    /// </summary>
    [Fact]
    public async Task Korish_ruxsati_bilan_kiritiladi_lekin_qaror_qilinmaydi()
    {
        var dunyo = await DunyoAsync();
        using var koruvchi = await MijozAsync("recruiting.view");

        var sorov = await KiritAsync(koruvchi, dunyo.Bolim, "Omborchi " + dunyo.Belgi);

        foreach (var (amal, tana) in new (string, object)[]
                 {
                     ("approve", new { title = "Omborchi " + dunyo.Belgi, openings = 1 }),
                     ("reject", new { reason = "Yo'q" }),
                     ("cancel", new { }),
                 })
        {
            using var javob = await koruvchi.PostAsJsonAsync(new Uri($"{Yol}/{sorov.Id}/{amal}", UriKind.Relative), tana, Ct);
            javob.StatusCode.ShouldBe(HttpStatusCode.Forbidden, amal);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Oqim
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sorovchi_korsatilmasa_bolim_boshligi_yoziladi_va_royxatda_kutmoqda()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("recruiting.view", "recruiting.manage");

        var sorov = await KiritAsync(hr, dunyo.Bolim, "Payvandchi " + dunyo.Belgi, openings: 3);

        sorov.Status.ShouldBe("Pending");
        sorov.RequestedByEmployeeId.ShouldBe(dunyo.Boshliq);
        sorov.RequestedByName.ShouldContain("Boshliq" + dunyo.Belgi);
        sorov.DepartmentName.ShouldBe("Zavod " + dunyo.Belgi);
        sorov.Openings.ShouldBe(3);

        var royxat = await RoyxatAsync(hr, $"?status=Pending&search={dunyo.Belgi}");
        royxat.Items.ShouldHaveSingleItem().Id.ShouldBe(sorov.Id);

        // Qidiruv so'rovchining ismi bo'yicha ham topadi
        (await RoyxatAsync(hr, $"?search=Boshliq{dunyo.Belgi}")).Items.Select(qator => qator.Id).ShouldContain(sorov.Id);
    }

    [Fact]
    public async Task Boshliqsiz_bolimda_sorovchi_majburiy()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("recruiting.view", "recruiting.manage");

        using var javob = await hr.PostAsJsonAsync(
            new Uri(Yol, UriKind.Relative),
            new { departmentId = dunyo.BoshliqsizBolim, title = "Haydovchi " + dunyo.Belgi, openings = 1 },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await javob.Content.ReadAsStringAsync(Ct)).ShouldContain("vakansiya_sorovi.boshliq_yoq");

        // Xodim tanlansa — qabul qilinadi
        var sorov = await KiritAsync(hr, dunyo.BoshliqsizBolim, "Haydovchi " + dunyo.Belgi, sorovchi: dunyo.Boshliq);
        sorov.RequestedByEmployeeId.ShouldBe(dunyo.Boshliq);
    }

    [Fact]
    public async Task Tasdiqlash_vakansiya_ochadi_va_sorovga_boglaydi()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("recruiting.view", "recruiting.manage");
        var sorov = await KiritAsync(hr, dunyo.Bolim, "Kassir " + dunyo.Belgi, openings: 2);

        var nom = "Kassir (Zavod) " + dunyo.Belgi;
        using var javob = await hr.PostAsJsonAsync(
            new Uri($"{Yol}/{sorov.Id}/approve", UriKind.Relative),
            new { title = nom, openings = 2, note = "So'rov asosida" },
            Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));

        var tasdiqlangan = (await javob.Content.ReadFromJsonAsync<SorovJavobi>(Ct))!;
        tasdiqlangan.Status.ShouldBe("Approved");
        tasdiqlangan.VacancyId.ShouldNotBeNull();
        tasdiqlangan.VacancyTitle.ShouldBe(nom);
        tasdiqlangan.DecidedByName.ShouldNotBeNullOrWhiteSpace();

        // Vakansiya haqiqatan ochiq — bot uni ko'radi
        var vakansiya = await hr.GetFromJsonAsync<JsonElement>(
            new Uri($"/api/recruiting/vacancies/{tasdiqlangan.VacancyId}", UriKind.Relative), Ct);
        vakansiya.GetProperty("status").GetString().ShouldBe("Open");
        vakansiya.GetProperty("openings").GetInt32().ShouldBe(2);

        // Qaror bir marta
        using var takror = await hr.PostAsJsonAsync(
            new Uri($"{Yol}/{sorov.Id}/approve", UriKind.Relative),
            new { title = nom + " 2", openings = 1 },
            Ct);
        takror.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Band_nom_bilan_tasdiqlansa_hech_narsa_yozilmaydi()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("recruiting.view", "recruiting.manage");
        var birinchi = await KiritAsync(hr, dunyo.Bolim, "Usta " + dunyo.Belgi);
        var ikkinchi = await KiritAsync(hr, dunyo.Bolim, "Usta " + dunyo.Belgi);

        using (var javob = await hr.PostAsJsonAsync(
                   new Uri($"{Yol}/{birinchi.Id}/approve", UriKind.Relative), new { title = "Usta " + dunyo.Belgi }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (var javob = await hr.PostAsJsonAsync(
                   new Uri($"{Yol}/{ikkinchi.Id}/approve", UriKind.Relative), new { title = "Usta " + dunyo.Belgi }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await javob.Content.ReadAsStringAsync(Ct)).ShouldContain("vakansiya.nom_band");
        }

        // Ikkinchi so'rov kutishda qoladi — vakansiya ham, qaror ham yozilmagan
        (await RoyxatAsync(hr, $"?status=Pending&search={dunyo.Belgi}")).Items.ShouldHaveSingleItem().Id.ShouldBe(ikkinchi.Id);
    }

    [Fact]
    public async Task Rad_etish_sababsiz_bolmaydi_keyin_bekor_qilinmaydi()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("recruiting.view", "recruiting.manage");
        var sorov = await KiritAsync(hr, dunyo.Bolim, "Qorovul " + dunyo.Belgi);

        using (var javob = await hr.PostAsJsonAsync(new Uri($"{Yol}/{sorov.Id}/reject", UriKind.Relative), new { reason = " " }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using (var javob = await hr.PostAsJsonAsync(new Uri($"{Yol}/{sorov.Id}/reject", UriKind.Relative), new { reason = "Shtat yo'q" }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK);
            var rad = (await javob.Content.ReadFromJsonAsync<SorovJavobi>(Ct))!;
            rad.Status.ShouldBe("Rejected");
            rad.DecisionNote.ShouldBe("Shtat yo'q");
        }

        using (var javob = await hr.PostAsJsonAsync(new Uri($"{Yol}/{sorov.Id}/cancel", UriKind.Relative), new { }, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }
    }

    [Fact]
    public async Task Eksport_fayl_qaytaradi()
    {
        var dunyo = await DunyoAsync();
        using var hr = await MijozAsync("recruiting.view", "recruiting.manage", "reports.export");
        await KiritAsync(hr, dunyo.Bolim, "Elektrik " + dunyo.Belgi);

        using var javob = await hr.GetAsync(new Uri($"{Yol}/export?search={dunyo.Belgi}", UriKind.Relative), Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK);
        javob.Content.Headers.ContentType!.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private sealed record Dunyo(string Belgi, Guid Bolim, Guid BoshliqsizBolim, Guid Boshliq);

    private sealed record SorovJavobi(
        Guid Id,
        Guid DepartmentId,
        string DepartmentName,
        Guid RequestedByEmployeeId,
        string RequestedByName,
        string Title,
        int Openings,
        string Status,
        string? DecidedByName,
        string? DecisionNote,
        Guid? VacancyId,
        string? VacancyTitle);

    private sealed record SahifaJavobi(List<SorovJavobi> Items, int Total);

    /// <summary>Boshliqli bo'lim va boshliqsiz bo'lim — har test uchun yangi belgi bilan.</summary>
    private async Task<Dunyo> DunyoAsync()
    {
        var belgi = "VS" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var zavod = Department.Create("Zavod " + belgi).Value;
        var boshliqsiz = Department.Create("Ombor " + belgi).Value;
        var boshliq = Employee.Create(FullName.Create("Boshliq" + belgi, "Ali", null).Value).Value;

        zavod.AssignHead(boshliq.Id);
        kontekst.AddRange(zavod, boshliqsiz, boshliq);
        await kontekst.SaveChangesAsync(Ct);

        return new Dunyo(belgi, zavod.Id, boshliqsiz.Id, boshliq.Id);
    }

    private static async Task<SorovJavobi> KiritAsync(
        HttpClient mijoz,
        Guid bolim,
        string lavozim,
        int openings = 1,
        Guid? sorovchi = null)
    {
        using var javob = await mijoz.PostAsJsonAsync(
            new Uri(Yol, UriKind.Relative),
            new
            {
                departmentId = bolim,
                requestedByEmployeeId = sorovchi,
                title = lavozim,
                openings,
                neededBy = new DateOnly(2026, 12, 1),
                note = "Yangi liniya ochiladi",
            },
            Ct);

        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
        return (await javob.Content.ReadFromJsonAsync<SorovJavobi>(Ct))!;
    }

    private static async Task<SahifaJavobi> RoyxatAsync(HttpClient mijoz, string sorov)
        => (await mijoz.GetFromJsonAsync<SahifaJavobi>(new Uri(Yol + sorov, UriKind.Relative), Ct))!;

    private async Task<HttpClient> MijozAsync(params string[] ruxsatlar)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await TestAccounts.TokenAsync(_factory, ruxsatlar));
        return client;
    }
}
