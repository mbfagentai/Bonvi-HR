using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Erta kelish va kech ketish chegarasi uchi-uchiga — egasining qarori (2026-10-28).
/// </summary>
/// <remarks>
/// Hisob qoidalari <c>ArrivalDepartureThresholdTests</c> da (bazasiz). Bu yerda — SHARTNOMA:
/// ikki maydon so'rov tanasidan bazagacha, bazadan javobgacha va saqlangan kungacha yetadimi,
/// xato kodi to'g'rimi. Yangi endpoint ham, yangi ruxsat ham yo'q.
/// </remarks>
public sealed class ScheduleThresholdApiTests : IClassFixture<BonviApiFactory>
{
    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    private readonly BonviApiFactory _factory;

    public ScheduleThresholdApiTests(BonviApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Chegaralar_saqlanadi_oqiladi_tahrirlanadi_chegaradan_tashqarisi_400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();

        Guid jadvalId;
        using (var javob = await admin.PostAsJsonAsync(Yol("/schedules"), JadvalTanasi("Chegarali " + belgi, 30, 60), ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            var jadval = await JsonAsync(javob);
            jadvalId = jadval.GetProperty("id").GetGuid();
            jadval.GetProperty("earlyArrivalThresholdMinutes").GetInt32().ShouldBe(30);
            jadval.GetProperty("lateDepartureThresholdMinutes").GetInt32().ShouldBe(60);
        }

        var oqish = await JsonAsync(await admin.GetAsync(Yol($"/schedules/{jadvalId}"), ct));
        oqish.GetProperty("earlyArrivalThresholdMinutes").GetInt32().ShouldBe(30);
        oqish.GetProperty("lateDepartureThresholdMinutes").GetInt32().ShouldBe(60);

        using (var tahrir = await admin.PutAsJsonAsync(Yol($"/schedules/{jadvalId}"), JadvalTanasi("Chegarali " + belgi, 15, 0), ct))
        {
            tahrir.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(tahrir));
            var jadval = await JsonAsync(tahrir);
            jadval.GetProperty("earlyArrivalThresholdMinutes").GetInt32().ShouldBe(15);
            jadval.GetProperty("lateDepartureThresholdMinutes").GetInt32().ShouldBe(0);
        }

        using (var kop = await admin.PutAsJsonAsync(Yol($"/schedules/{jadvalId}"), JadvalTanasi("Chegarali " + belgi, 0, 241), ct))
        {
            await XatoKutAsync(kop, HttpStatusCode.BadRequest, "davomat.jadval_chegara");
        }

        // Eski mijoz (maydonlarsiz tana) — ikkalasi 0, ya'ni avvalgi hisob.
        using (var eski = await admin.PostAsJsonAsync(Yol("/schedules"), JadvalTanasi("Chegarasiz " + belgi), ct))
        {
            eski.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(eski));
            var jadval = await JsonAsync(eski);
            jadval.GetProperty("earlyArrivalThresholdMinutes").GetInt32().ShouldBe(0);
            jadval.GetProperty("lateDepartureThresholdMinutes").GetInt32().ShouldBe(0);
        }
    }

    /// <summary>
    /// Egasining misoli bazada: 09:00–18:00, chegaralar 30 va 60, 08:00 → 19:30 — saqlangan kunda
    /// ishlangan 600 (08:00–08:30, 09:00–18:00, 19:00–19:30), ortiqcha 60.
    /// </summary>
    [Fact]
    public async Task Saqlangan_kunda_chegara_ichidagi_vaqt_sanalmaydi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var xodimId = await XodimAsync(belgi);
        await XodimgaJadvalAsync(admin, xodimId, JadvalTanasi("Chegarali " + belgi, 30, 60));
        var sana = BugunToshkentda().AddDays(-3);

        await OtishlarAsync(admin, xodimId, "ch-" + belgi, (Lahza(sana, 8, 0), "In"), (Lahza(sana, 19, 30), "Out"));

        var saqlangan = await SaqlanganKunAsync(xodimId, sana, ct);
        saqlangan.WorkedMinutes.ShouldBe(600);
        saqlangan.OvertimeMinutes.ShouldBe(60);
        saqlangan.LateMinutes.ShouldBe(0);
    }

    // ────────────────────────────────────────────────────────────
    //  Muhit tayyorlash — FlexibleScheduleApiTests dagi yo'l
    // ────────────────────────────────────────────────────────────

    private sealed record SaqlanganKun(int WorkedMinutes, int OvertimeMinutes, int LateMinutes);

    /// <summary>Har kuni 09:00–18:00, tanaffussiz; chegaralar berilmasa tanada umuman yo'q.</summary>
    private static object JadvalTanasi(string nom, int? erta = null, int? kech = null)
    {
        var kunlar = Enumerable.Range(1, 7)
            .Select(kun => new { dayIndex = kun, isWorking = true, startTime = "09:00", endTime = "18:00", breakMinutes = 0 })
            .ToArray();

        return erta is null && kech is null
            ? new { name = nom, graceInMinutes = 10, graceOutMinutes = 10, note = (string?)null, days = kunlar }
            : new
            {
                name = nom,
                graceInMinutes = 10,
                graceOutMinutes = 10,
                note = (string?)null,
                days = kunlar,
                earlyArrivalThresholdMinutes = erta ?? 0,
                lateDepartureThresholdMinutes = kech ?? 0,
            };
    }

    private static async Task XodimgaJadvalAsync(HttpClient admin, Guid xodimId, object tana)
    {
        var ct = TestContext.Current.CancellationToken;

        using var jadval = await admin.PostAsJsonAsync(Yol("/schedules"), tana, ct);
        jadval.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(jadval));
        var jadvalId = (await JsonAsync(jadval)).GetProperty("id").GetGuid();

        using var biriktirma = await admin.PostAsJsonAsync(
            Yol("/schedule-assignments"),
            new { workScheduleId = jadvalId, scope = "Employee", employeeId = xodimId, effectiveFrom = "2025-01-01" },
            ct);
        biriktirma.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(biriktirma));
    }

    /// <summary>Terminal o'tishlari: qurilma → hodisalar → moslik (moslik kunni qayta hisoblaydi).</summary>
    private async Task OtishlarAsync(HttpClient admin, Guid xodimId, string shaxs, params (DateTimeOffset Lahza, string Yonalish)[] otishlar)
    {
        var ct = TestContext.Current.CancellationToken;

        using var qurilma = await admin.PostAsJsonAsync(Yol("/devices"), new { name = "Chegara turniketi " + shaxs, provider = "Generic" }, ct);
        qurilma.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qurilma));
        var token = (await JsonAsync(qurilma)).GetProperty("token").GetString()!;

        var tana = JsonSerializer.Serialize(new
        {
            events = otishlar.Select((otish, i) => new
            {
                personId = shaxs,
                occurredAt = otish.Lahza.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
                direction = otish.Yonalish,
                eventId = $"{shaxs}-{otish.Lahza.ToUnixTimeSeconds()}-{i}",
            }),
        });

        using (var mijoz = _factory.CreateClient())
        using (var sorov = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/attendance/ingest", UriKind.Relative)))
        {
            sorov.Content = new StringContent(tana, Encoding.UTF8, "application/json");
            sorov.Headers.Add("X-Bonvi-Device-Token", token);

            using var qabul = await mijoz.SendAsync(sorov, ct);
            qabul.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(qabul));
        }

        using var moslik = await admin.PostAsJsonAsync(Yol("/identities"), new { externalPersonId = shaxs, employeeId = xodimId }, ct);
        moslik.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(moslik));
    }

    private async Task<Guid> XodimAsync(string belgi)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Chegara MCHJ " + belgi).Value;
        var xodim = Employee.Create(FullName.Create("Chegarov", "Xodim", belgi).Value).Value;

        kontekst.AddRange(firma, xodim);
        kontekst.Add(Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1)).Value);
        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        return xodim.Id;
    }

    private async Task<SaqlanganKun> SaqlanganKunAsync(Guid xodimId, DateOnly sana, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();

        return await qamrov.ServiceProvider.GetRequiredService<AppDbContext>().AttendanceDays.AsNoTracking()
            .Where(day => day.EmployeeId == xodimId && day.LocalDate == sana)
            .Select(day => new SaqlanganKun(day.WorkedMinutes, day.OvertimeMinutes, day.LateMinutes))
            .SingleAsync(ct);
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

    private static DateOnly BugunToshkentda() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(Tashkent).DateTime);

    private static DateTimeOffset Lahza(DateOnly sana, int soat, int daqiqa)
        => new(sana.ToDateTime(new TimeOnly(soat, daqiqa)), Tashkent);

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
