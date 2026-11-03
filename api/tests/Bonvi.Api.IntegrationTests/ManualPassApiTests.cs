using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// HR qo'lda belgi qo'shadi va kun oynasi — SPEC-qolda-belgi §B.
/// </summary>
/// <remarks>
/// Terminal o'tishi hech qachon o'zgarmaydi: tuzatish faqat yangi belgi bilan, o'chirish faqat
/// HR qo'shgan belgiga. Har bir test o'z xodimi bilan ishlaydi.
/// </remarks>
public sealed class ManualPassApiTests : IClassFixture<BonviApiFactory>
{
    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    private readonly BonviApiFactory _factory;

    public ManualPassApiTests(BonviApiFactory factory) => _factory = factory;

    [Fact]
    public async Task HR_belgi_qoshadi_kun_qayta_hisoblanadi_oynada_korinadi_va_audit_yoziladi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var hr = await MijozAsync(Permissions.AttendanceView, Permissions.AttendanceRequestsManage);
        var xodim = await XodimAsync();
        var sana = BugunToshkentda().AddDays(-2);

        // Terminal faqat kirishni yozgan — chiqish yo'q.
        await TerminalOtishiAsync(xodim, sana, "09:00", PassDirection.In);

        using (var javob = await hr.PostAsJsonAsync(
                   Yol($"/employees/{xodim}/manual-passes"),
                   new { date = Iso(sana), time = "18:00:00", direction = "Out", note = "Terminal ishlamadi" },
                   ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await MatnAsync(javob));
            var otish = await JsonAsync(javob);
            otish.GetProperty("method").GetString().ShouldBe("Manual");
            otish.GetProperty("direction").GetString().ShouldBe("Out");
            otish.GetProperty("skewWarning").GetBoolean().ShouldBeFalse("o'tgan kunga yozilgan belgi «soat og'gan» emas");
            otish.GetProperty("manual").GetProperty("note").GetString().ShouldBe("Terminal ishlamadi");
        }

        // Kun SHU YERDA qayta hisoblangan: 09:00–18:00 = 540 daqiqa.
        var kun = await KunAsync(xodim, sana, ct);
        kun.WorkedMinutes.ShouldBe(540);
        kun.LastOutAt.ShouldBe(new DateTimeOffset(sana.ToDateTime(new TimeOnly(18, 0)), Tashkent));

        var oyna = await JsonAsync(await hr.GetAsync(Yol($"/employees/{xodim}/days/{Iso(sana)}/passes"), ct));
        var qatorlar = oyna.GetProperty("items").EnumerateArray().ToList();
        qatorlar.Count.ShouldBe(2);
        qatorlar.ShouldAllBe(qator => qator.GetProperty("counted").GetBoolean());
        qatorlar[1].GetProperty("pass").GetProperty("manual").GetProperty("note").GetString().ShouldBe("Terminal ishlamadi");

        var id = qatorlar[1].GetProperty("pass").GetProperty("id").GetGuid();
        (await AuditAsync(id, ct)).ShouldContain(AuditAction.Create);

        // Takror — 409, ikkinchi qator yozilmaydi.
        using (var takror = await hr.PostAsJsonAsync(
                   Yol($"/employees/{xodim}/manual-passes"),
                   new { date = Iso(sana), time = "18:00:00", direction = "Out", note = "Yana" },
                   ct))
        {
            await XatoKutAsync(takror, HttpStatusCode.Conflict, "davomat.belgi_takror");
        }

        // O'chirish — kun yana ochiq.
        (await hr.DeleteAsync(Yol($"/manual-passes/{id}"), ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await KunAsync(xodim, sana, ct)).WorkedMinutes.ShouldBe(0);
        (await AuditAsync(id, ct)).ShouldContain(AuditAction.Delete);
    }

    [Fact]
    public async Task Terminal_otishi_ochirilmaydi_va_notogri_sorovlar_rad_etiladi()
    {
        var ct = TestContext.Current.CancellationToken;
        using var hr = await MijozAsync(Permissions.AttendanceView, Permissions.AttendanceRequestsManage);
        var xodim = await XodimAsync();
        var sana = BugunToshkentda().AddDays(-1);

        var terminal = await TerminalOtishiAsync(xodim, sana, "08:00", PassDirection.In);

        await XatoKutAsync(
            await hr.DeleteAsync(Yol($"/manual-passes/{terminal}"), ct),
            HttpStatusCode.BadRequest,
            "davomat.belgi_qolda_emas");

        await XatoKutAsync(
            await hr.PostAsJsonAsync(
                Yol($"/employees/{xodim}/manual-passes"),
                new { date = Iso(sana), time = "18:00:00", direction = "Out", note = "  " },
                ct),
            HttpStatusCode.BadRequest,
            "davomat.belgi_izoh_bosh");

        await XatoKutAsync(
            await hr.PostAsJsonAsync(
                Yol($"/employees/{xodim}/manual-passes"),
                new { date = Iso(BugunToshkentda().AddDays(2)), time = "09:00:00", direction = "In", note = "Kelajak" },
                ct),
            HttpStatusCode.BadRequest,
            "davomat.belgi_kelajakda");

        await XatoKutAsync(
            await hr.PostAsJsonAsync(
                Yol($"/employees/{xodim}/manual-passes"),
                new { date = Iso(BugunToshkentda().AddDays(-120)), time = "09:00:00", direction = "In", note = "Eski" },
                ct),
            HttpStatusCode.BadRequest,
            "davomat.belgi_juda_eski");

        await XatoKutAsync(
            await hr.PostAsJsonAsync(
                Yol($"/employees/{xodim}/manual-passes"),
                new { date = Iso(sana), time = "09:00:00", direction = "Unknown", note = "Noma'lum" },
                ct),
            HttpStatusCode.BadRequest,
            "davomat.yonalish_notogri");

        await XatoKutAsync(
            await hr.PostAsJsonAsync(
                Yol($"/employees/{Guid.NewGuid()}/manual-passes"),
                new { date = Iso(sana), time = "09:00:00", direction = "In", note = "Yo'q xodim" },
                ct),
            HttpStatusCode.NotFound,
            "davomat.xodim_topilmadi");

        // Terminal o'tishi joyida.
        var oyna = await JsonAsync(await hr.GetAsync(Yol($"/employees/{xodim}/days/{Iso(sana)}/passes"), ct));
        oyna.GetProperty("items").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Ruxsatsiz_403_anonim_401()
    {
        var ct = TestContext.Current.CancellationToken;
        var xodim = await XodimAsync();
        var sana = BugunToshkentda().AddDays(-1);
        var tana = new { date = Iso(sana), time = "09:00:00", direction = "In", note = "Ruxsat" };

        // Faqat ko'rish — oynani o'qiydi, lekin qo'sha ham, o'chira ham olmaydi.
        using var korish = await MijozAsync(Permissions.AttendanceView);
        (await korish.GetAsync(Yol($"/employees/{xodim}/days/{Iso(sana)}/passes"), ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await korish.PostAsJsonAsync(Yol($"/employees/{xodim}/manual-passes"), tana, ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await korish.DeleteAsync(Yol($"/manual-passes/{Guid.NewGuid()}"), ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Davomatga umuman ruxsati yo'q — oyna ham yopiq.
        using var begona = await MijozAsync(Permissions.EmployeesView);
        (await begona.GetAsync(Yol($"/employees/{xodim}/days/{Iso(sana)}/passes"), ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var anonim = _factory.CreateClient();
        (await anonim.GetAsync(Yol($"/employees/{xodim}/days/{Iso(sana)}/passes"), ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonim.PostAsJsonAsync(Yol($"/employees/{xodim}/manual-passes"), tana, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── yordamchilar ──────────────────────────────────────────────

    private async Task<Guid> XodimAsync()
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();
        var belgi = Guid.NewGuid().ToString("N")[..8];

        var firma = LegalEntity.Create("Belgi MCHJ " + belgi).Value;
        var xodim = Employee.Create(FullName.Create("Belgiyev", "Xodim", belgi).Value).Value;

        kontekst.AddRange(firma, xodim);
        kontekst.Add(Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1)).Value);
        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        return xodim.Id;
    }

    /// <summary>Terminal o'tishi o'rnida — «Mobil ilova» qatoridagi bog'langan o'tish (qo'lda EMAS).</summary>
    private async Task<Guid> TerminalOtishiAsync(Guid xodim, DateOnly sana, string vaqt, PassDirection yonalish)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kontekst = qamrov.ServiceProvider.GetRequiredService<AppDbContext>();

        var lahza = new DateTimeOffset(sana.ToDateTime(TimeOnly.Parse(vaqt, CultureInfo.InvariantCulture)), Tashkent);
        var otish = AttendancePass.Record(
            AttendanceDevice.MobileAppId,
            AttendanceIdentityProvider.Mobile,
            xodim.ToString(),
            lahza,
            lahza.ToUniversalTime(),
            "test:" + Guid.NewGuid().ToString("N"),
            yonalish,
            DirectionSource.Employee,
            PassMethod.Mobile).Value;
        otish.Resolve(xodim, lahza.ToUniversalTime());

        kontekst.Add(otish);
        await kontekst.SaveChangesAsync(TestContext.Current.CancellationToken);

        return otish.Id;
    }

    private async Task<AttendanceDay> KunAsync(Guid xodim, DateOnly sana, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();

        return await qamrov.ServiceProvider.GetRequiredService<AppDbContext>().AttendanceDays.AsNoTracking()
            .SingleAsync(day => day.EmployeeId == xodim && day.LocalDate == sana, ct);
    }

    private async Task<List<AuditAction>> AuditAsync(Guid id, CancellationToken ct)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        var kalit = id.ToString();

        return await qamrov.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.AsNoTracking()
            .Where(log => log.EntityName == nameof(AttendanceManualPass) && log.EntityId == kalit)
            .Select(log => log.Action)
            .ToListAsync(ct);
    }

    private async Task<HttpClient> MijozAsync(params string[] ruxsatlar)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await TestAccounts.TokenAsync(_factory, ruxsatlar));
        return client;
    }

    private static Uri Yol(string yol) => new("/api/attendance" + yol, UriKind.Relative);

    private static string Iso(DateOnly sana) => sana.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly BugunToshkentda() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(Tashkent).DateTime);

    private static async Task XatoKutAsync(HttpResponseMessage javob, HttpStatusCode holat, string kod)
    {
        using (javob)
        {
            var matn = await MatnAsync(javob);
            javob.StatusCode.ShouldBe(holat, matn);
            JsonDocument.Parse(matn).RootElement.GetProperty("title").GetString().ShouldBe(kod, matn);
        }
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage javob)
        => JsonDocument.Parse(await MatnAsync(javob)).RootElement.Clone();

    private static async Task<string> MatnAsync(HttpResponseMessage javob)
        => await javob.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
}
