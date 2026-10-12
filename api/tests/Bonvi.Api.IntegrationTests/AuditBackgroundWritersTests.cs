using System.Globalization;
using System.Net;
using Bonvi.Application.Attendance.Ingest;
using Bonvi.Application.Attendance.Recompute;
using Bonvi.Application.Attendance.Retention;
using Bonvi.Application.Identity.EmployeeAccounts;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.PublicApi;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// ⭐ Fon yozuvchilari jurnalni to'ldirib yubormaydi: bo'sh (o'zgarishsiz) yurish — NOL audit qatori.
/// </summary>
/// <remarks>
/// <para>
/// Ishlab chiqarishda to'xtovsiz yozadiganlar bor: terminal agenti har daqiqada «oxirgi
/// aloqa», soat va solishtirishni yozadi; davomat vazifalari har 15 daqiqada kursor
/// sozlamasini va kun yozuvlarini qayta yozadi; hisob ochish vazifasi har 15 daqiqada
/// yuradi; bot har 15 daqiqada qayta kiradi; tashqi API kalitining «oxirgi ishlatilishi»
/// har chaqiruvda yangilanadi. Ularning har biri audit qatori yasasa, jadval kuniga
/// o'n minglab bema'ni qator bilan to'lardi va haqiqiy o'zgarish ko'rinmay qolardi.
/// </para>
/// <para>
/// Bu sinf O'Z bazasida ishlaydi (alohida <see cref="BonviApiFactory"/>), testlari esa
/// ketma-ket — shu sababli butun jadvalning sanog'i solishtiriladi: qaysidir yangi fon
/// yozuvchisi qator yasasa, aynan shu test yiqiladi.
/// </para>
/// </remarks>
public sealed class AuditBackgroundWritersTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public AuditBackgroundWritersTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// Har bir takroriy yozuvchining odatiy yurishi — jurnal sanog'i o'zgarmaydi.
    /// </summary>
    [Fact]
    public async Task Fon_yozuvchilarining_odatiy_yurishi_jurnalga_hech_narsa_yozmaydi()
    {
        // ---------- Tayyorlov (yaratishlarning o'zi yoziladi — bu to'g'ri) ----------
        var (qurilmaId, token) = await QurilmaAsync("Фон");
        var kalitId = await KalitAsync();
        var xodimId = await XodimAsync("Фон");

        await VazifaAsync<EmployeeAccountProvisioningJob>(job => job.RunAsync(Ct));
        await VazifaAsync<AttendanceEmploymentChangesJob>(job => job.RunAsync(Ct));

        var hisobId = await BazadaAsync(db => db.EmployeeAccounts
            .Where(row => row.EmployeeId == xodimId)
            .Select(row => row.Id)
            .SingleAsync(Ct));

        var oldin = await SanoqAsync();

        // ---------- Terminal agenti: soat va ro'yxat (HTTP, qurilma tokeni) ----------
        for (var marta = 0; marta < 2; marta++)
        {
            await AgentAsync(HttpMethod.Post, "/api/attendance/agent/clock", token, """{"deviceTime":"2026-10-12T05:00:00Z"}""");
            await AgentAsync(HttpMethod.Get, "/api/attendance/agent/roster", token, body: null);
        }

        // ---------- Qurilma, kalit, hisoblarning har daqiqalik maydonlari ----------
        await BazadaAsync(async db =>
        {
            var hozir = DateTimeOffset.UtcNow;

            var qurilma = await db.AttendanceDevices.SingleAsync(row => row.Id == qurilmaId, Ct);
            qurilma.NoteSeen(hozir, "10.0.0.5");
            qurilma.NoteEvent(hozir, "10.0.0.5", skewSeconds: 3);
            qurilma.NoteAgent(hozir, "10.0.0.6");
            qurilma.NoteClock(offsetSeconds: -2, hozir);
            qurilma.NoteReconcile(hozir, "10.0.0.6", heldRemovals: 1, foreignPersons: 2);
            qurilma.NoteRejected(hozir);

            (await db.ApiKeys.SingleAsync(row => row.Id == kalitId, Ct)).MarkUsed(hozir, "10.0.0.7");

            var admin = await db.Users.FirstAsync(Ct);
            admin.RecordFailedLogin(hozir);
            admin.RecordSuccessfulLogin(hozir);

            (await db.EmployeeAccounts.SingleAsync(row => row.Id == hisobId, Ct)).RecordSuccessfulLogin(hozir);

            await db.SaveChangesAsync(Ct);
            return 0;
        });

        // ---------- Takroriy vazifalar (Hangfire jadvalidagilar) ----------
        await VazifaAsync<EmployeeAccountProvisioningJob>(job => job.RunAsync(Ct));
        await VazifaAsync<AttendanceEmploymentChangesJob>(job => job.RunAsync(Ct));
        await VazifaAsync<AttendanceDirtyRecomputeJob>(job => job.RunAsync(Ct));
        await VazifaAsync<AttendanceStartedShiftsJob>(job => job.RunAsync(Ct));
        await VazifaAsync<AttendanceRetention>(retention => retention.FreezeThroughAsync(new DateOnly(2025, 1, 1), Ct));

        // ---------- Qayta ishga tushish: urug'lar mavjud bazaga yozmaydi ----------
        await VazifaAsync<IdentitySeeder>(seeder => seeder.SeedAsync(Ct));
        await VazifaAsync<CatalogSeeder>(seeder => seeder.SeedAsync(Ct));

        (await SanoqAsync()).ShouldBe(oldin, "fon yozuvchilarining odatiy yurishi audit qatori yasamasligi kerak");
    }

    /// <summary>
    /// Qurilma yozgan MA'NOLI o'zgarish (terminal seriyasining birinchi bog'lanishi) — yoziladi,
    /// bajaruvchi «qurilma», amal nomi — agent endpointi.
    /// </summary>
    [Fact]
    public async Task Terminal_seriyasining_bog_lanishi_qurilma_nomidan_yoziladi()
    {
        var (qurilmaId, token) = await QurilmaAsync("Серия");

        using var client = _factory.CreateClient();
        using var sorov = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/attendance/agent/roster", UriKind.Relative));
        sorov.Headers.Add("X-Bonvi-Device-Token", token);
        sorov.Headers.Add("X-Bonvi-Terminal-Serial", "DS-AUDIT-" + Belgi());

        using (var javob = await client.SendAsync(sorov, Ct))
        {
            javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
        }

        var qatorlar = await JurnalAsync(qurilmaId);

        qatorlar.Select(log => log.Action).ShouldBe([AuditAction.Create, AuditAction.Update]);

        var boglanish = qatorlar[1];
        boglanish.ChangedFields.ShouldBe(["TerminalSerial"], "aloqa vaqtlari tashlab ketiladi, faqat seriya qoladi");
        boglanish.ActorKind.ShouldBe(AuditActorKind.Device);
        boglanish.UserId.ShouldBeNull();
        boglanish.Operation.ShouldBe("GetAttendanceAgentRoster");

        // Yaratishda token va uning prefiksi — yashirin
        var yaratish = qatorlar[0].NewValues.ShouldNotBeNull();
        yaratish.ShouldContain("\"TokenHash\":\"***\"");
        yaratish.ShouldContain("\"TokenPrefix\":\"***\"");
        yaratish.ShouldNotContain(AttendanceDeviceToken.PrefixOf(token));
    }

    /// <summary>
    /// Hisob ochish vazifasi — hisob BIR MARTA yoziladi («tizim», vazifa nomi bilan, parol
    /// maydonlari yashirin); keyingi yurish hech narsa yozmaydi.
    /// </summary>
    [Fact]
    public async Task Hisob_ochish_vazifasi_bir_marta_tizim_nomidan_yozadi()
    {
        var xodimId = await XodimAsync("Ҳисоб");

        await VazifaAsync<EmployeeAccountProvisioningJob>(job => job.RunAsync(Ct));

        var hisobId = await BazadaAsync(db => db.EmployeeAccounts
            .Where(row => row.EmployeeId == xodimId)
            .Select(row => row.Id)
            .SingleAsync(Ct));

        var qator = (await JurnalAsync(hisobId)).ShouldHaveSingleItem();

        qator.Action.ShouldBe(AuditAction.Create);
        qator.ActorKind.ShouldBe(AuditActorKind.System);
        qator.Operation.ShouldBe(EmployeeAccountProvisioningJob.AuditOperation);
        qator.SubjectEmployeeId.ShouldBe(xodimId);
        var yangi = qator.NewValues.ShouldNotBeNull();
        yangi.ShouldContain("\"PasswordHash\":\"***\"");
        yangi.ShouldContain("\"PasswordProtected\":\"***\"");

        var oldin = await SanoqAsync();

        await VazifaAsync<EmployeeAccountProvisioningJob>(job => job.RunAsync(Ct));

        (await SanoqAsync()).ShouldBe(oldin);
    }

    // ════════════════════════════════════════════════════════════
    //  Yordamchilar
    // ════════════════════════════════════════════════════════════

    private static string Belgi() => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8];

    private async Task<(Guid Id, string Token)> QurilmaAsync(string tag)
    {
        var token = AttendanceDeviceToken.Generate();

        var id = await BazadaAsync(async db =>
        {
            var qurilma = AttendanceDevice.Register(
                $"{tag} терминал {Belgi()}",
                AttendanceDeviceToken.Hash(token),
                AttendanceDeviceToken.PrefixOf(token),
                DateTimeOffset.UtcNow).Value;

            db.AttendanceDevices.Add(qurilma);
            await db.SaveChangesAsync(Ct);
            return qurilma.Id;
        });

        return (id, token);
    }

    private Task<Guid> KalitAsync()
        => BazadaAsync(async db =>
        {
            var kalit = ApiKey.Issue("Фон калити " + Belgi(), ApiKeyFormat.Generate()).Value;
            db.ApiKeys.Add(kalit);
            await db.SaveChangesAsync(Ct);
            return kalit.Id;
        });

    private Task<Guid> XodimAsync(string tag)
        => BazadaAsync(async db =>
        {
            var belgi = Belgi();
            var firma = LegalEntity.Create($"{tag} МЧЖ {belgi}").Value;
            var xodim = Employee.Create(FullName.Create("Фонов", tag, belgi).Value).Value;

            db.AddRange(firma, xodim);
            db.Add(Employment.Hire(xodim.Id, firma.Id, new DateOnly(2025, 1, 1)).Value);
            await db.SaveChangesAsync(Ct);
            return xodim.Id;
        });

    private async Task AgentAsync(HttpMethod usul, string yol, string token, string? body)
    {
        using var client = _factory.CreateClient();
        using var sorov = new HttpRequestMessage(usul, new Uri(yol, UriKind.Relative));
        sorov.Headers.Add("X-Bonvi-Device-Token", token);

        if (body is not null)
        {
            sorov.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        }

        using var javob = await client.SendAsync(sorov, Ct);
        javob.StatusCode.ShouldBe(HttpStatusCode.OK, await javob.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>Vazifani o'z DI qamrovida — Hangfire qanday chaqirsa, shunday (HTTP'siz).</summary>
    private async Task VazifaAsync<T>(Func<T, Task> ish)
        where T : notnull
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        await ish(qamrov.ServiceProvider.GetRequiredService<T>());
    }

    private async Task<T> BazadaAsync<T>(Func<AppDbContext, Task<T>> ish)
    {
        await using var qamrov = _factory.Services.CreateAsyncScope();
        return await ish(qamrov.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<List<AuditLog>> JurnalAsync(Guid entityId)
        => BazadaAsync(db => db.AuditLogs.AsNoTracking()
            .Where(log => log.EntityId == entityId.ToString())
            .OrderBy(log => log.OccurredAt)
            .ThenBy(log => log.Id)
            .ToListAsync(Ct));

    private async Task<long> SanoqAsync()
    {
        await using var ulanish = new NpgsqlConnection(_factory.ConnectionString);
        await ulanish.OpenAsync(Ct);

        await using var buyruq = new NpgsqlCommand("SELECT count(*) FROM audit_logs;", ulanish);
        return (long)(await buyruq.ExecuteScalarAsync(Ct))!;
    }
}
