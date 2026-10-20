using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Shaxsiy vaqt uchi-uchiga — SPEC-verifikatsiya-sorov-tahriri §5, §6.2, §8.4.
/// </summary>
/// <remarks>
/// <para>
/// Hisob qoidalari <c>PersonalTimeTests</c> da (bazasiz, E1–E21). Bu yerda — SHARTNOMA: tasdiqlangan
/// shaxsiy so'rov qarordan keyin kunni qayta hisoblatadi, <c>personal_minutes</c> bazaga tushadi va
/// tabel, ilova, xulosa, Excel va panelgacha yetadi; bekor qilish va tur bayrog'i ham qayta hisoblatadi.
/// </para>
/// <para>
/// Sahna — egasining misoli (E1): reja 08:30–17:30, tanaffus 60 daqiqa 12:00–15:00; 08:25 kirish,
/// 17:35 chiqish; shaxsiy 14:00–16:00 → ishlangan 360, shaxsiy 120.
/// </para>
/// </remarks>
public sealed class PersonalTimeApiTests : IClassFixture<BonviApiFactory>
{
    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    private readonly BonviApiFactory _factory;

    public PersonalTimeApiTests(BonviApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Tasdiqlangan_shaxsiy_sorov_ishlangan_vaqtdan_ayriladi_va_hamma_joyda_korinadi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var xodim = await XodimAsync(belgi);
        await XodimgaJadvalAsync(admin, xodim, "Shaxsiy sinov " + belgi);
        var tur = await YoqlikTuriAsync(admin, "Shaxsiy " + belgi, ishVaqti: false);
        var firma = await FirmaIdAsync(xodim, ct);

        var sana = BugunToshkentda().AddDays(-3);
        await OtishlarAsync(admin, xodim, "sh-" + belgi, (Lahza(sana, 8, 25), "In"), (Lahza(sana, 17, 35), "Out"));

        (await SaqlanganKunAsync(xodim, sana, ct)).ShouldBe(new SaqlanganKun(AttendanceDayStatus.Present, 490, 0), "sinov sharti: so'rovsiz kun");

        var sorov = await SorovAsync(admin, xodim, tur, sana, "14:00:00", "16:00:00");
        await TasdiqlaAsync(admin, sorov);

        // ── Saqlangan kun ──
        (await SaqlanganKunAsync(xodim, sana, ct)).ShouldBe(new SaqlanganKun(AttendanceDayStatus.Present, 360, 120));

        // ── Tabel katagi va jami ──
        var tabel = await JsonAsync(await admin.GetAsync(Yol($"/timesheet?year={sana.Year}&month={sana.Month}&employeeId={xodim}"), ct));
        var qator = tabel.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        var katak = Katak(qator, sana);
        katak.GetProperty("personalMinutes").GetInt32().ShouldBe(120);
        katak.GetProperty("workedMinutes").GetInt32().ShouldBe(360);
        katak.GetProperty("lateMinutes").GetInt32().ShouldBe(0);
        katak.GetProperty("earlyLeaveMinutes").GetInt32().ShouldBe(0);
        qator.GetProperty("totals").GetProperty("personalMinutes").GetInt32().ShouldBe(120);

        // ── Xodim ilovasi — HR katagining o'zi ──
        using (var ilova = await IlovaMijoziAsync(admin, xodim))
        {
            var oy = await JsonAsync(await ilova.GetAsync(new Uri($"/api/me/attendance?year={sana.Year}&month={sana.Month}", UriKind.Relative), ct));
            oy.GetProperty("days").EnumerateArray()
                .Select(kun => kun.GetProperty("day"))
                .Single(kun => kun.GetProperty("date").GetString() == Iso(sana))
                .GetProperty("personalMinutes").GetInt32().ShouldBe(120);
            oy.GetProperty("totals").GetProperty("personalMinutes").GetInt32().ShouldBe(120);
        }

        // ── Xulosa ──
        var davr = $"from={Iso(sana)}&to={Iso(sana)}";
        var xulosa = await JsonAsync(await admin.GetAsync(Yol($"/reports/summary?{davr}&legalEntityId={firma}"), ct));
        var xulosaQatori = xulosa.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        xulosaQatori.GetProperty("personalMinutes").GetInt32().ShouldBe(120);
        xulosaQatori.GetProperty("workedMinutes").GetInt32().ShouldBe(360);
        xulosaQatori.GetProperty("overtimeMinutes").GetInt32().ShouldBe(0);

        // ── Excel: ikki yangi ustun OXIRIDA, mavjudlari joyida ──
        using (var fayl = await ExcelAsync(admin, $"/export/timesheet?year={sana.Year}&month={sana.Month}&employeeId={xodim}", ct))
        {
            var kunlar = fayl.Worksheet("Kunlar");
            kunlar.Cell(1, 8).GetString().ShouldBe("Ishlangan (daq.)");
            kunlar.Cell(1, 16).GetString().ShouldBe("Erkin jadval");
            kunlar.Cell(1, 18).GetString().ShouldBe("Kam ishlangan (soat:daq)");
            kunlar.Cell(1, 19).GetString().ShouldBe("Shaxsiy vaqt (daq.)");
            kunlar.Cell(1, 20).GetString().ShouldBe("Shaxsiy vaqt (soat:daq)");
            kunlar.Cell(1, 21).GetString().ShouldBeEmpty("shaxsiy vaqt — oxirgi ustun");

            var qatorRaqami = 1 + sana.Day;   // bitta xodim: 2-qatordan oyning 1-kuni
            kunlar.Cell(qatorRaqami, 4).GetDateTime().ShouldBe(sana.ToDateTime(TimeOnly.MinValue));
            kunlar.Cell(qatorRaqami, 8).GetDouble().ShouldBe(360);
            kunlar.Cell(qatorRaqami, 19).GetDouble().ShouldBe(120);
            kunlar.Cell(qatorRaqami, 20).GetString().ShouldBe("2:00");

            var tabelVaraq = fayl.Worksheet("Tabel");
            var jami = 4 + DateTime.DaysInMonth(sana.Year, sana.Month);
            tabelVaraq.Cell(1, jami).GetString().ShouldBe("Ishlangan (daq.)");
            tabelVaraq.Cell(1, jami + 11).GetString().ShouldBe("Kam ishlangan (soat:daq)");
            tabelVaraq.Cell(1, jami + 12).GetString().ShouldBe("Shaxsiy vaqt (daq.)");
            tabelVaraq.Cell(1, jami + 13).GetString().ShouldBe("Shaxsiy vaqt (soat:daq)");
            tabelVaraq.Cell(1, jami + 14).GetString().ShouldBeEmpty();
            tabelVaraq.Cell(2, jami + 12).GetDouble().ShouldBe(120);
        }

        using (var fayl = await ExcelAsync(admin, $"/export/summary?{davr}&legalEntityId={firma}", ct))
        {
            var varaq = fayl.Worksheet("Xulosa");
            varaq.Cell(1, 18).GetString().ShouldBe("Ortiqcha ish (soat:daq)");
            varaq.Cell(1, 20).GetString().ShouldBe("Kam ishlangan (soat:daq)");
            varaq.Cell(1, 21).GetString().ShouldBe("Shaxsiy vaqt (daq.)");
            varaq.Cell(1, 22).GetString().ShouldBe("Shaxsiy vaqt (soat:daq)");
            varaq.Cell(1, 23).GetString().ShouldBeEmpty();
            varaq.Cell(2, 21).GetDouble().ShouldBe(120);
        }

        // ── HR bekor qiladi — kun shu yerning o'zida qayta hisoblanadi (E16) ──
        using (var bekor = await admin.PostAsync(Yol($"/requests/{sorov}/cancel"), null, ct))
        {
            bekor.StatusCode.ShouldBe(HttpStatusCode.NoContent, await MatnAsync(bekor));
        }

        (await SaqlanganKunAsync(xodim, sana, ct)).ShouldBe(new SaqlanganKun(AttendanceDayStatus.Present, 490, 0));
    }

    /// <summary>
    /// Turning «ish vaqti» bayrog'i almashsa, tasdiqlangan so'rovlar kunlari qayta hisoblanadi — endi
    /// ikkala yo'nalishda ham ma'noli: shaxsiy vaqt ↔ ish vaqti.
    /// </summary>
    [Fact]
    public async Task Tur_bayrogi_almashsa_kun_qayta_hisoblanadi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var xodim = await XodimAsync(belgi);
        await XodimgaJadvalAsync(admin, xodim, "Bayroq sinov " + belgi);
        var nom = "Bayroqli " + belgi;
        var tur = await YoqlikTuriAsync(admin, nom, ishVaqti: false);

        var sana = BugunToshkentda().AddDays(-4);
        await OtishlarAsync(admin, xodim, "bq-" + belgi, (Lahza(sana, 8, 25), "In"), (Lahza(sana, 17, 35), "Out"));

        await TasdiqlaAsync(admin, await SorovAsync(admin, xodim, tur, sana, "14:00:00", "16:00:00"));
        (await SaqlanganKunAsync(xodim, sana, ct)).ShouldBe(new SaqlanganKun(AttendanceDayStatus.Present, 360, 120));

        await TurBayrogiAsync(admin, tur, nom, ishVaqti: true);
        (await SaqlanganKunAsync(xodim, sana, ct)).ShouldBe(
            new SaqlanganKun(AttendanceDayStatus.Present, 490, 0),
            "ish vaqti turi — binoda bo'lgan vaqtga kredit hech narsa qo'shmaydi");

        await TurBayrogiAsync(admin, tur, nom, ishVaqti: false);
        (await SaqlanganKunAsync(xodim, sana, ct)).ShouldBe(new SaqlanganKun(AttendanceDayStatus.Present, 360, 120));
    }

    /// <summary>E9: kelmagan kun, ikki soatlik shaxsiy so'rov — «Kelmadi» (avval «Sababli» edi), tabelda ham, panelda ham.</summary>
    [Fact]
    public async Task Qisman_shaxsiy_vaqtli_kelmagan_kun_tabelda_va_panelda_Kelmadi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var admin = await AdminMijozAsync();
        var belgi = Belgi();
        var xodim = await XodimAsync(belgi);
        await XodimgaJadvalAsync(admin, xodim, "Kelmadi sinov " + belgi);
        var tur = await YoqlikTuriAsync(admin, "Shaxsiy E9 " + belgi, ishVaqti: false);
        var firma = await FirmaIdAsync(xodim, ct);

        var sana = BugunToshkentda().AddDays(-5);
        await TasdiqlaAsync(admin, await SorovAsync(admin, xodim, tur, sana, "14:00:00", "16:00:00"));

        (await SaqlanganKunAsync(xodim, sana, ct)).ShouldBe(new SaqlanganKun(AttendanceDayStatus.Absent, 0, 120));

        var tabel = await JsonAsync(await admin.GetAsync(Yol($"/timesheet?year={sana.Year}&month={sana.Month}&employeeId={xodim}"), ct));
        var katak = Katak(tabel.GetProperty("items").EnumerateArray().ShouldHaveSingleItem(), sana);
        katak.GetProperty("status").GetString().ShouldBe("Absent");
        katak.GetProperty("personalMinutes").GetInt32().ShouldBe(120);

        var panel = await JsonAsync(await admin.GetAsync(Yol($"/dashboard?date={Iso(sana)}&legalEntityId={firma}"), ct));
        panel.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("employeeId").GetGuid() == xodim)
            .GetProperty("status").GetString().ShouldBe("Absent");

        // Butun kunlik shaxsiy so'rov esa avvalgidek «Sababli».
        var butun = sana.AddDays(-1);
        await TasdiqlaAsync(admin, await SorovAsync(admin, xodim, tur, butun, null, null));
        (await SaqlanganKunAsync(xodim, butun, ct)).Status.ShouldBe(AttendanceDayStatus.Excused);
    }

    // ────────────────────────────────────────────────────────────
    //  Muhit tayyorlash
    // ────────────────────────────────────────────────────────────

    private sealed record SaqlanganKun(AttendanceDayStatus Status, int WorkedMinutes, int PersonalMinutes);

    private static JsonElement Katak(JsonElement qator, DateOnly sana)
        => qator.GetProperty("cells").EnumerateArray().Single(cell => cell.GetProperty("date").GetString() == Iso(sana));

    private static async Task<Guid> SorovAsync(HttpClient admin, Guid xodim, Guid tur, DateOnly sana, string? dan, string? gacha)
    {
        using var javob = await admin.PostAsJsonAsync(
            Yol("/requests"),
            new { employeeId = xodim, absenceTypeId = tur, fromDate = Iso(sana), toDate = Iso(sana), fromTime = dan, toTime = gacha },
            TestContext.Current.CancellationToken);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return (await JsonAsync(javob)).GetProperty("id").GetGuid();
    }

    private static async Task TasdiqlaAsync(HttpClient admin, Guid sorov)
    {
        using var javob = await admin.PostAsJsonAsync(
            Yol($"/requests/{sorov}/decide"),
            new { approve = true },
            TestContext.Current.CancellationToken);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
    }

    private static async Task<Guid> YoqlikTuriAsync(HttpClient admin, string nom, bool ishVaqti)
    {
        using var javob = await admin.PostAsJsonAsync(
            new Uri("/api/catalog/absence-types", UriKind.Relative),
            new { name = nom, countsAsWorkTime = ishVaqti },
            TestContext.Current.CancellationToken);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return (await JsonAsync(javob)).GetProperty("id").GetGuid();
    }

    private static async Task TurBayrogiAsync(HttpClient admin, Guid tur, string nom, bool ishVaqti)
    {
        using var javob = await admin.PutAsJsonAsync(
            new Uri($"/api/catalog/absence-types/{tur}", UriKind.Relative),
            new { name = nom, countsAsWorkTime = ishVaqti },
            TestContext.Current.CancellationToken);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
    }

    /// <summary>Hamma kun 08:30–17:30, tanaffus 60 daqiqa 12:00–15:00 (§5.8 sukut sahnasi) — XODIM pog'onasida.</summary>
    private static async Task XodimgaJadvalAsync(HttpClient admin, Guid xodimId, string nom)
    {
        var ct = TestContext.Current.CancellationToken;

        using var jadval = await admin.PostAsJsonAsync(
            Yol("/schedules"),
            new
            {
                name = nom,
                graceInMinutes = 10,
                graceOutMinutes = 10,
                note = (string?)null,
                days = Enumerable.Range(1, 7).Select(kun => new
                {
                    dayIndex = kun,
                    isWorking = true,
                    startTime = "08:30",
                    endTime = "17:30",
                    breakMinutes = 60,
                    breakStart = "12:00",
                    breakEnd = "15:00",
                }),
            },
            ct);
        jadval.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(jadval));
        var jadvalId = (await JsonAsync(jadval)).GetProperty("id").GetGuid();

        using var biriktirma = await admin.PostAsJsonAsync(
            Yol("/schedule-assignments"),
            new { workScheduleId = jadvalId, scope = "Employee", employeeId = xodimId, effectiveFrom = "2025-01-01" },
            ct);
        biriktirma.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(biriktirma));
    }

    /// <summary>
    /// Terminal o'tishlari: qurilma → hodisalar → moslik. Moslik eski o'tishlarni bog'laydi va kunni
    /// qayta hisoblaydi (testda navbat sinxron) — <c>FlexibleScheduleApiTests</c> dagi yo'l.
    /// </summary>
    private async Task OtishlarAsync(HttpClient admin, Guid xodimId, string shaxs, params (DateTimeOffset Lahza, string Yonalish)[] otishlar)
    {
        var ct = TestContext.Current.CancellationToken;

        using var qurilma = await admin.PostAsJsonAsync(Yol("/devices"), new { name = "Shaxsiy turniket " + shaxs, provider = "Generic" }, ct);
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

    /// <summary>Xodim — o'z firmasi, mehnat munosabati va takrorlanmas telefoni (ilova logini) bilan.</summary>
    private async Task<Guid> XodimAsync(string belgi)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var firma = LegalEntity.Create("Shaxsiy MCHJ " + belgi).Value;
        var xodim = Employee.Create(FullName.Create("Shaxsiyev", "Xodim", belgi).Value).Value;
        var telefon = "9" + Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);
        xodim.SetPhones(PhoneNumber.Create(telefon).Value);

        kontekst.AddRange(firma, xodim);
        kontekst.Add(Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1)).Value);
        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        return xodim.Id;
    }

    private async Task<Guid> FirmaIdAsync(Guid xodimId, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();

        return await qamrov.ServiceProvider.GetRequiredService<AppDbContext>().Employments.AsNoTracking()
            .Where(munosabat => munosabat.EmployeeId == xodimId)
            .Select(munosabat => munosabat.LegalEntityId)
            .FirstAsync(ct);
    }

    private async Task<SaqlanganKun> SaqlanganKunAsync(Guid xodimId, DateOnly sana, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();

        return await qamrov.ServiceProvider.GetRequiredService<AppDbContext>().AttendanceDays.AsNoTracking()
            .Where(day => day.EmployeeId == xodimId && day.LocalDate == sana)
            .Select(day => new SaqlanganKun(day.Status, day.WorkedMinutes, day.PersonalMinutes))
            .SingleAsync(ct);
    }

    private static async Task<XLWorkbook> ExcelAsync(HttpClient admin, string yol, CancellationToken ct)
    {
        using var javob = await admin.GetAsync(Yol(yol), ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));

        return new XLWorkbook(new MemoryStream(await javob.Content.ReadAsByteArrayAsync(ct)));
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

    private static DateTimeOffset Lahza(DateOnly sana, int soat, int daqiqa)
        => new(sana.ToDateTime(new TimeOnly(soat, daqiqa)), Tashkent);

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage javob)
        => JsonDocument.Parse(await MatnAsync(javob)).RootElement.Clone();

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
