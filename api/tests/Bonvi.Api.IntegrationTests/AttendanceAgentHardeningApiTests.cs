using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Application.Attendance.Agent;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Ingest;
using Bonvi.Application.Identity.Abstractions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Agent va qurilmalar — 2026-09-27 audit topilmalarining server tomoni.
/// </summary>
/// <remarks>
/// Har bir test bitta topilmani qadaydi (raqami test izohida). Agentning
/// o'zi (terminal bilan qism) <c>Bonvi.DeviceAgent.Tests</c> da.
/// </remarks>
public sealed class AttendanceAgentHardeningApiTests : IClassFixture<BonviApiFactory>
{
    private const string Serial = "DS-K1T341AMF20210915V030700ENG12345678";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly BonviApiFactory _factory;

    public AttendanceAgentHardeningApiTests(BonviApiFactory factory) => _factory = factory;

    // ────────────────────────────────────────────────────────────
    //  Topilma 8 — token jismoniy terminalga bog'lanadi
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Birinchi seriya yoziladi; boshqa seriya yoki seriyasiz so'rov — 409,
    /// va rad etilgan so'rov qurilmaning hech narsasini yozmaydi.
    /// </summary>
    [Fact]
    public async Task Token_birinchi_terminalga_bogolanadi_boshqasi_rad_etiladi()
    {
        var cancel = TestContext.Current.CancellationToken;
        var tag = Tag();
        var (deviceId, token) = await DeviceAsync(tag);

        (await RosterAsync(token, Serial)).DeviceId.ShouldBe(deviceId);

        (await DeviceRowAsync(deviceId)).TerminalSerial.ShouldBe(Serial);

        await ExpectProblemAsync(Send(HttpMethod.Get, "/api/attendance/agent/roster", token, "BOSHQA-TERMINAL"), HttpStatusCode.Conflict, "davomat.terminal_mos_emas");
        await ExpectProblemAsync(Send(HttpMethod.Get, "/api/attendance/agent/roster", token, serial: null), HttpStatusCode.Conflict, "davomat.terminal_mos_emas");

        var report = Send(HttpMethod.Post, "/api/attendance/agent/report", token, "BOSHQA-TERMINAL");
        report.Content = JsonContent.Create(Array.Empty<AgentReportItemDto>(), options: Json);
        await ExpectProblemAsync(report, HttpStatusCode.Conflict, "davomat.terminal_mos_emas");

        var clock = Send(HttpMethod.Post, "/api/attendance/agent/clock", token, "BOSHQA-TERMINAL");
        clock.Content = JsonContent.Create(new { deviceTime = DateTimeOffset.UtcNow }, options: Json);
        await ExpectProblemAsync(clock, HttpStatusCode.Conflict, "davomat.terminal_mos_emas");

        var ingest = Send(HttpMethod.Post, "/api/attendance/ingest", token, "BOSHQA-TERMINAL");
        ingest.Content = new StringContent("{\"events\":[]}", Encoding.UTF8, "application/json");
        await ExpectProblemAsync(ingest, HttpStatusCode.Conflict, "davomat.terminal_mos_emas");

        var device = await DeviceRowAsync(deviceId);
        device.TerminalSerial.ShouldBe(Serial, "rad etilgan so'rov bog'lanishni o'zgartirmaydi");
        device.ClockCheckedAt.ShouldBeNull("boshqa terminalning soati yozilmaydi");
    }

    /// <summary>O'zi push qiladigan terminal seriya yubormaydi — bog'lanmagan qurilmada bu normal.</summary>
    [Fact]
    public async Task Seriyasiz_qabul_boglanmagan_qurilmada_otadi()
    {
        var (_, token) = await DeviceAsync(Tag(), generic: true);

        using var client = _factory.CreateClient();
        using var request = Send(HttpMethod.Post, "/api/attendance/ingest", token, serial: null);
        request.Content = new StringContent("{\"events\":[]}", Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ────────────────────────────────────────────────────────────
    //  Topilma 8 — o'chirish ro'yxati va ommaviy o'chirish
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Ro'yxat O'CHIRILADIGAN raqamlarni ham beradi — faqat shu server
    /// ajratganlarini; ishlayotgan xodimning raqami unda yo'q.
    /// </summary>
    [Fact]
    public async Task Royxat_ishdan_ketganning_raqamini_ochirishga_beradi()
    {
        var tag = Tag();
        var (_, token) = await DeviceAsync(tag);
        var stays = await EmployedWithPhotoAsync(tag + "a");
        var leaves = await EmployedWithPhotoAsync(tag + "b");

        var first = await RosterAsync(token, Serial);
        var stayingNo = first.Entries.Single(entry => entry.EmployeeId == stays).PersonNo;
        var leavingNo = first.Entries.Single(entry => entry.EmployeeId == leaves).PersonNo;

        first.RemoveNumbers.ShouldNotContain(stayingNo);
        first.RemoveNumbers.ShouldNotContain(leavingNo);

        await TerminateAsync(leaves);

        var second = await RosterAsync(token, Serial);

        second.Entries.ShouldNotContain(entry => entry.EmployeeId == leaves);
        second.RemoveNumbers.ShouldContain(leavingNo);
        second.RemoveNumbers.ShouldNotContain(stayingNo);
        second.RemoveNumbers.ShouldAllBe(number => DevicePersonNo.IsManaged(number));
    }

    /// <summary>
    /// Agent hisobot bergan to'siq va begona odamlar kartada ko'rinadi; HR
    /// tasdiqlasa, ro'yxat ruxsat beradi (10 daqiqaga).
    /// </summary>
    [Fact]
    public async Task Ushlab_qolingan_ochirish_kartada_korinadi_va_HR_tasdiqlaydi()
    {
        var cancel = TestContext.Current.CancellationToken;
        var tag = Tag();
        var (deviceId, token) = await DeviceAsync(tag);

        (await RosterAsync(token, Serial)).AllowMassRemoval.ShouldBeFalse();

        using (var client = _factory.CreateClient())
        using (var status = Send(HttpMethod.Post, "/api/attendance/agent/status", token, Serial))
        {
            status.Content = JsonContent.Create(new { heldRemovals = 150, foreignPersons = 3 }, options: Json);
            using var response = await client.SendAsync(status, cancel);
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(cancel));
        }

        using var hr = await ClientAsync(Permissions.AttendanceManage);

        var page = await hr.GetFromJsonAsync<DeviceEnrollmentPageDto>($"/api/attendance/devices/{deviceId}/enrollments", Json, cancel);
        page!.Summary.HeldRemovals.ShouldBe(150);
        page.Summary.ForeignPersons.ShouldBe(3);
        page.Summary.ReconciledAt.ShouldNotBeNull();

        using (var approve = await hr.PostAsync(new Uri($"/api/attendance/devices/{deviceId}/enrollments/approve-removals", UriKind.Relative), null, cancel))
        {
            approve.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await RosterAsync(token, Serial)).AllowMassRemoval.ShouldBeTrue();

        var approved = await DeviceRowAsync(deviceId);
        approved.RemovalsApprovedUntil!.Value.ShouldBeInRange(
            DateTimeOffset.UtcNow.AddMinutes(9),
            DateTimeOffset.UtcNow.AddMinutes(11));
    }

    // ────────────────────────────────────────────────────────────
    //  Topilma 5, 6 — xato turlari
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Qat'iy rad etilgan belgi qayta berilmaydi («ushlab tur»); «Hammasini
    /// sinxronlash» uni qayta urinishga qo'yadi. Vaqtinchalik xato esa
    /// ushlanmaydi (topilma 6).
    /// </summary>
    [Fact]
    public async Task Rad_etilgan_belgi_ushlab_turiladi_sinxronlash_qayta_qoyadi()
    {
        var cancel = TestContext.Current.CancellationToken;
        var tag = Tag();
        var (deviceId, token) = await DeviceAsync(tag);
        var employeeId = await EmployedWithPhotoAsync(tag);

        var version = (await RosterAsync(token, Serial)).Entries.Single(entry => entry.EmployeeId == employeeId).Version;

        // Vaqtinchalik xato — keyingi safar yana urinadi.
        await ReportAsync(token, [new AgentReportItemDto(employeeId, null, AgentEnrollmentAction.Upsert, Ok: false, version, "Muddat tugadi.")]);

        var retry = (await RosterAsync(token, Serial)).Entries.Single(entry => entry.EmployeeId == employeeId);
        retry.Force.ShouldBeTrue();
        retry.Hold.ShouldBeFalse();

        // Qat'iy rad — ushlab turiladi.
        await ReportAsync(token, [new AgentReportItemDto(employeeId, null, AgentEnrollmentAction.Upsert, Ok: false, version, "face quality too low", Rejected: true)]);

        var held = (await RosterAsync(token, Serial)).Entries.Single(entry => entry.EmployeeId == employeeId);
        held.Hold.ShouldBeTrue();
        held.Force.ShouldBeFalse();

        using var hr = await ClientAsync(Permissions.AttendanceManage);
        using (var resync = await hr.PostAsync(new Uri($"/api/attendance/devices/{deviceId}/enrollments/resync", UriKind.Relative), null, cancel))
        {
            (await resync.Content.ReadFromJsonAsync<EnrollmentResyncDto>(Json, cancel))!.Affected.ShouldBe(1);
        }

        var rearmed = (await RosterAsync(token, Serial)).Entries.Single(entry => entry.EmployeeId == employeeId);
        rearmed.Hold.ShouldBeFalse();
        rearmed.Force.ShouldBeTrue();
        rearmed.Version.ShouldNotBe(version);
    }

    /// <summary>
    /// O'chirib bo'lmagan ishdan ketgan xodim — «Xato», «O'chirilgan» ham,
    /// «Navbatda» ham EMAS (topilma 5).
    /// </summary>
    [Fact]
    public async Task Ochirib_bolmagan_ishdan_ketgan_xato_bolib_korinadi()
    {
        var cancel = TestContext.Current.CancellationToken;
        var tag = Tag();
        var (deviceId, token) = await DeviceAsync(tag);
        var employeeId = await EmployedWithPhotoAsync(tag);

        var entry = (await RosterAsync(token, Serial)).Entries.Single(row => row.EmployeeId == employeeId);
        await ReportAsync(token, [new AgentReportItemDto(employeeId, null, AgentEnrollmentAction.Upsert, Ok: true, entry.Version, null)]);

        await TerminateAsync(employeeId);

        await ReportAsync(token, [new AgentReportItemDto(Guid.Empty, entry.PersonNo, AgentEnrollmentAction.Remove, Ok: false, null, "deviceBusy")]);

        using var hr = await ClientAsync(Permissions.AttendanceManage);
        var page = await hr.GetFromJsonAsync<DeviceEnrollmentPageDto>($"/api/attendance/devices/{deviceId}/enrollments", Json, cancel);

        var row = page!.Rows.Single(item => item.EmployeeId == employeeId);
        row.State.ShouldBe(DeviceEnrollmentState.Failed);
        row.Error.ShouldBe("deviceBusy");
        row.EmployeePhotoKey.ShouldBe($"employees/{tag}.jpg", "ro'yxatda haqiqiy rasm ko'rinishi uchun");

        await ReportAsync(token, [new AgentReportItemDto(Guid.Empty, entry.PersonNo, AgentEnrollmentAction.Remove, Ok: true, null, null)]);

        var removed = await hr.GetFromJsonAsync<DeviceEnrollmentPageDto>($"/api/attendance/devices/{deviceId}/enrollments", Json, cancel);
        removed!.Rows.ShouldNotContain(item => item.EmployeeId == employeeId);
    }

    // ────────────────────────────────────────────────────────────
    //  Topilma 12, 15 — rasm o'chirilishi va ism o'zgarishi
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Terminalga yozilgan xodimning rasmi o'chirilsa, u ro'yxatda QOLADI —
    /// qayta yozilmaydi va terminaldan olib tashlanmaydi (topilma 12).
    /// </summary>
    [Fact]
    public async Task Rasmi_ochirilgan_yozilgan_xodim_royxatda_qoladi()
    {
        var tag = Tag();
        var (_, token) = await DeviceAsync(tag);
        var employeeId = await EmployedWithPhotoAsync(tag);
        var never = await EmployedWithPhotoAsync(tag + "n");

        var entry = (await RosterAsync(token, Serial)).Entries.Single(row => row.EmployeeId == employeeId);
        await ReportAsync(token, [new AgentReportItemDto(employeeId, null, AgentEnrollmentAction.Upsert, Ok: true, entry.Version, null)]);

        await ChangeEmployeeAsync(employeeId, employee => employee.SetPhoto(null));
        await ChangeEmployeeAsync(never, employee => employee.SetPhoto(null));

        var roster = await RosterAsync(token, Serial);

        var kept = roster.Entries.Single(row => row.EmployeeId == employeeId);
        kept.PersonNo.ShouldBe(entry.PersonNo);
        kept.Version.ShouldBe(entry.Version);
        kept.Force.ShouldBeFalse();
        kept.PhotoVersion.ShouldBeNull();
        roster.RemoveNumbers.ShouldNotContain(entry.PersonNo);

        roster.Entries.ShouldNotContain(row => row.EmployeeId == never, "hech qachon yozilmagan rasmsiz xodim ro'yxatga tushmaydi");
    }

    /// <summary>Ism o'zgarsa — xodim qayta yoziladi (topilma 15).</summary>
    [Fact]
    public async Task Ism_ozgarsa_xodim_qayta_yoziladi()
    {
        var tag = Tag();
        var (_, token) = await DeviceAsync(tag);
        var employeeId = await EmployedWithPhotoAsync(tag);

        var entry = (await RosterAsync(token, Serial)).Entries.Single(row => row.EmployeeId == employeeId);
        await ReportAsync(token, [new AgentReportItemDto(employeeId, null, AgentEnrollmentAction.Upsert, Ok: true, entry.Version, null)]);

        (await RosterAsync(token, Serial)).Entries.Single(row => row.EmployeeId == employeeId).Force.ShouldBeFalse();

        await ChangeEmployeeAsync(
            employeeId,
            employee => employee.Rename(FullName.Create($"{tag}Yangi", "Test", null).Value).IsSuccess.ShouldBeTrue());

        var renamed = (await RosterAsync(token, Serial)).Entries.Single(row => row.EmployeeId == employeeId);
        renamed.Force.ShouldBeTrue();
        renamed.PersonNo.ShouldBe(entry.PersonNo, "raqam o'zgarmaydi — joyida yangilanadi");
        renamed.FullName.ShouldContain("Yangi");
    }

    // ────────────────────────────────────────────────────────────
    //  Topilma 11 — raqam o'zgarmaydi
    // ────────────────────────────────────────────────────────────

    /// <summary>Tizim yozgan raqamni uzib bo'lmaydi va xodimga ikkinchisini qo'shib bo'lmaydi.</summary>
    [Fact]
    public async Task Tizim_raqamini_uzib_ham_ikkinchisini_qoshib_ham_bolmaydi()
    {
        var cancel = TestContext.Current.CancellationToken;
        var tag = Tag();
        var (_, token) = await DeviceAsync(tag);
        var employeeId = await EmployedWithPhotoAsync(tag);

        var personNo = (await RosterAsync(token, Serial)).Entries.Single(row => row.EmployeeId == employeeId).PersonNo;

        Guid identityId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            identityId = await db.AttendanceIdentities
                .Where(identity => identity.ExternalPersonId == personNo)
                .Select(identity => identity.Id)
                .SingleAsync(cancel);
        }

        using var hr = await ClientAsync(Permissions.AttendanceManage);

        await ExpectProblemAsync(hr, new HttpRequestMessage(HttpMethod.Delete, new Uri($"/api/attendance/identities/{identityId}", UriKind.Relative)), HttpStatusCode.Conflict, "davomat.moslik_bonvi_raqami");

        var second = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/attendance/identities", UriKind.Relative))
        {
            Content = JsonContent.Create(new { provider = "Hikvision", externalPersonId = "912345678", employeeId }, options: Json),
        };
        await ExpectProblemAsync(hr, second, HttpStatusCode.Conflict, "davomat.moslik_bonvi_raqami_bor");
    }

    /// <summary>
    /// Bir vaqtda kelgan ro'yxat so'rovlari xodimga IKKI raqam bermaydi —
    /// ikkinchisi 409 oladi va qayta so'raganda birinchisining raqamini ko'radi.
    /// </summary>
    [Fact]
    public async Task Parallel_royxat_sorovlari_xodimga_ikki_raqam_bermaydi()
    {
        var cancel = TestContext.Current.CancellationToken;
        var tag = Tag();
        var (_, token) = await DeviceAsync(tag);

        var employees = new List<Guid>();
        for (var index = 0; index < 5; index++)
        {
            employees.Add(await EmployedWithPhotoAsync($"{tag}{index}"));
        }

        using var client = _factory.CreateClient();

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            using var request = Send(HttpMethod.Get, "/api/attendance/agent/roster", token, Serial);
            using var response = await client.SendAsync(request, cancel);
            return (response.StatusCode, Body: await response.Content.ReadAsStringAsync(cancel));
        }));

        responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.OK
            || (response.StatusCode == HttpStatusCode.Conflict && response.Body.Contains("davomat.agent_royxat_band")));

        await RosterAsync(token, Serial);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var counts = await db.AttendanceIdentities
            .Where(identity => employees.Contains(identity.EmployeeId))
            .GroupBy(identity => identity.EmployeeId)
            .Select(group => group.Count())
            .ToListAsync(cancel);

        counts.Count.ShouldBe(5);
        counts.ShouldAllBe(count => count == 1);
    }

    // ────────────────────────────────────────────────────────────
    //  Topilma 2, 4, 18 — qabul yo'li
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Agent soatni o'lchagan terminalning internet uzilishidan keyin kelgan eski
    /// o'tishi «soat adashgan» EMAS; o'lchovsiz terminalniki esa — eski qoida
    /// bo'yicha (topilma 18).
    /// </summary>
    [Fact]
    public async Task Uzilishdan_keyingi_otish_soat_adashgan_deb_belgilanmaydi()
    {
        var cancel = TestContext.Current.CancellationToken;
        var tag = Tag();
        var (agentDevice, agentToken) = await DeviceAsync(tag, generic: true);
        var (pushDevice, pushToken) = await DeviceAsync(tag + "p", generic: true);

        await ClockAsync(agentToken, DateTimeOffset.UtcNow);

        var twentyMinutesAgo = DateTimeOffset.UtcNow.AddMinutes(-20).ToOffset(TimeSpan.FromHours(5));

        await IngestAsync(agentToken, $"a-{tag}", twentyMinutesAgo);
        await IngestAsync(pushToken, $"p-{tag}", twentyMinutesAgo);

        using var hr = await ClientAsync(Permissions.AttendanceView);

        var agentPass = await PassAsync(hr, agentDevice);
        agentPass.GetProperty("skewWarning").GetBoolean().ShouldBeFalse();
        Math.Abs(agentPass.GetProperty("clockSkewSeconds").GetInt32()).ShouldBeLessThan(60);

        var pushPass = await PassAsync(hr, pushDevice);
        pushPass.GetProperty("skewWarning").GetBoolean().ShouldBeTrue("o'lchov yo'q — qabul ayirmasi ishlatiladi");

        (await DeviceRowAsync(agentDevice)).HasClockDrift.ShouldBeFalse();
    }

    /// <summary>Rad etilgan urinish (ruxsat yo'q) server tomonida ham o'tish sifatida yozilmaydi (topilma 4).</summary>
    [Fact]
    public async Task Rad_etilgan_kichik_tur_yozilmaydi()
    {
        var cancel = TestContext.Current.CancellationToken;
        var tag = Tag();
        var (_, token) = await DeviceAsync(tag, generic: true);

        var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(5)).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
        var body = $$"""
            {"events":[
              {"personId":"9{{tag[..8]}}","occurredAt":"{{now}}","eventId":"d-{{tag}}","minor":6},
              {"personId":"9{{tag[..8]}}","occurredAt":"{{now}}","eventId":"o-{{tag}}","minor":75}
            ]}
            """;

        using var client = _factory.CreateClient();
        using var request = Send(HttpMethod.Post, "/api/attendance/ingest", token, serial: null);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancel);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(cancel));

        (await response.Content.ReadFromJsonAsync<AttendanceIngestResultDto>(Json, cancel))!.Stored.ShouldBe(1);
    }

    /// <summary>
    /// «Hodisalari rad etilyapti» belgisi AYNAN rad etilgan so'rovdan chiqadi,
    /// jimlikdan taxmin qilinmaydi; keyingi qabul qilingan o'tish uni o'chiradi (topilma 2).
    /// </summary>
    [Fact]
    public async Task Rad_etilgan_sorov_belgisi_keyingi_otish_bilan_ochadi()
    {
        var cancel = TestContext.Current.CancellationToken;
        var tag = Tag();
        var (deviceId, token) = await DeviceAsync(tag, generic: true);

        using (var client = _factory.CreateClient())
        using (var request = Send(HttpMethod.Post, "/api/attendance/ingest", token, serial: null))
        {
            request.Content = new StringContent("{\"events\":[{\"personId\":\"1\",\"occurredAt\":\"buzuq\"}]}", Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request, cancel);
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        var rejected = await DeviceRowAsync(deviceId);
        rejected.EventsRejected.ShouldBeTrue();
        rejected.TerminalSeenAt.ShouldNotBeNull("terminal gapiryapti — faqat hodisasi rad etildi");

        await IngestAsync(token, $"ok-{tag}", DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(5)));

        (await DeviceRowAsync(deviceId)).EventsRejected.ShouldBeFalse();
    }

    // ────────────────────────────────────────────────────────────
    //  Topilma 2 + egasining so'rovi — agent/terminal uzilishi
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Agent ro'yxat so'rab turibdi-yu terminal javob bermayapti — qurilma
    /// «hozirgina» emas, «terminal uzilgan»; Statistika va qurilmalar
    /// sahifasi buni qizil bilan ko'rsatadi.
    /// </summary>
    [Fact]
    public async Task Agent_tirik_terminal_uzilgan_bolsa_ogohlantirish_chiqadi()
    {
        var cancel = TestContext.Current.CancellationToken;
        var tag = Tag();
        var (deviceId, token) = await DeviceAsync(tag);

        // Terminal 30 daqiqa oldin oxirgi marta javob bergan.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var device = await db.AttendanceDevices.SingleAsync(row => row.Id == deviceId, cancel);
            device.NoteClock(0, DateTimeOffset.UtcNow.AddMinutes(-30));
            await db.SaveChangesAsync(cancel);
        }

        // Agent hozir ham ro'yxat so'rayapti.
        await RosterAsync(token, Serial);

        using var manager = await ClientAsync(Permissions.AttendanceManage);
        var devices = await manager.GetFromJsonAsync<List<AttendanceDeviceDto>>("/api/attendance/devices", Json, cancel);
        var row = devices!.Single(device => device.Id == deviceId);

        row.LinkState.ShouldBe(DeviceLinkState.TerminalUnreachable);
        row.LinkSince!.Value.ShouldBeInRange(DateTimeOffset.UtcNow.AddMinutes(-31), DateTimeOffset.UtcNow.AddMinutes(-29));
        row.AgentSeenAt.ShouldNotBeNull();

        using var viewer = await ClientAsync(Permissions.AttendanceView);
        var dashboard = await viewer.GetFromJsonAsync<AttendanceDashboardDto>("/api/attendance/dashboard", Json, cancel);

        dashboard!.DeviceHealth.Outages.ShouldContain(outage =>
            outage.DeviceId == deviceId && outage.State == DeviceLinkState.TerminalUnreachable);
    }

    /// <summary>Agent o'n daqiqadan beri jim — «agent uzilgan», qachondan beri bilan.</summary>
    [Fact]
    public async Task Agent_jim_qolsa_ogohlantirish_chiqadi()
    {
        var cancel = TestContext.Current.CancellationToken;
        var tag = Tag();
        var (deviceId, _) = await DeviceAsync(tag);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var device = await db.AttendanceDevices.SingleAsync(row => row.Id == deviceId, cancel);
            device.NoteClock(0, DateTimeOffset.UtcNow.AddMinutes(-25));
            await db.SaveChangesAsync(cancel);
        }

        using var viewer = await ClientAsync(Permissions.AttendanceView);
        var dashboard = await viewer.GetFromJsonAsync<AttendanceDashboardDto>("/api/attendance/dashboard", Json, cancel);

        var outage = dashboard!.DeviceHealth.Outages.Single(item => item.DeviceId == deviceId);
        outage.State.ShouldBe(DeviceLinkState.AgentOffline);
        outage.DeviceName.ShouldBe($"{tag} terminal");
        dashboard.DeviceHealth.ActiveDevices.ShouldBeGreaterThanOrEqualTo(1);
    }

    // ────────────────────────────────────────────────────────────
    //  Topilma 13 — RBAC
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Xodim kartasidagi qurilmalar qatori va ushlab qolingan o'chirishni
    /// tasdiqlash — faqat <c>attendance.manage</c>: faqat ko'ruvchi 403 oladi.
    /// </summary>
    [Fact]
    public async Task Yozish_holati_va_tasdiqlash_faqat_boshqaruvchiga_ochiq()
    {
        var cancel = TestContext.Current.CancellationToken;
        var tag = Tag();
        var (deviceId, _) = await DeviceAsync(tag);
        var employeeId = await EmployedWithPhotoAsync(tag);

        using var viewer = await ClientAsync(Permissions.AttendanceView);

        using (var enrollments = await viewer.GetAsync(new Uri($"/api/attendance/employees/{employeeId}/enrollments", UriKind.Relative), cancel))
        {
            enrollments.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "javobda terminal raqami bor — SPEC-agent §D5");
        }

        using (var approve = await viewer.PostAsync(new Uri($"/api/attendance/devices/{deviceId}/enrollments/approve-removals", UriKind.Relative), null, cancel))
        {
            approve.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using var anonymous = _factory.CreateClient();
        using (var approve = await anonymous.PostAsync(new Uri($"/api/attendance/devices/{deviceId}/enrollments/approve-removals", UriKind.Relative), null, cancel))
        {
            approve.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var manager = await ClientAsync(Permissions.AttendanceManage);

        using (var enrollments = await manager.GetAsync(new Uri($"/api/attendance/employees/{employeeId}/enrollments", UriKind.Relative), cancel))
        {
            enrollments.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    /// <summary>Agentning holat hisoboti ham qurilma tokeni bilan himoyalangan.</summary>
    [Fact]
    public async Task Holat_hisoboti_tokensiz_401()
    {
        using var client = _factory.CreateClient();
        using var request = Send(HttpMethod.Post, "/api/attendance/agent/status", "bonvi_yoq", Serial);
        request.Content = JsonContent.Create(new { heldRemovals = 1, foreignPersons = 0 }, options: Json);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    private static string Tag() => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8];

    private static HttpRequestMessage Send(HttpMethod method, string path, string? token, string? serial)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));

        if (token is not null)
        {
            request.Headers.Add("X-Bonvi-Device-Token", token);
        }

        if (serial is not null)
        {
            request.Headers.Add("X-Bonvi-Terminal-Serial", serial);
        }

        return request;
    }

    private async Task ExpectProblemAsync(HttpRequestMessage request, HttpStatusCode status, string code)
    {
        using var client = _factory.CreateClient();
        await ExpectProblemAsync(client, request, status, code);
    }

    private static async Task ExpectProblemAsync(HttpClient client, HttpRequestMessage request, HttpStatusCode status, string code)
    {
        var cancel = TestContext.Current.CancellationToken;

        using (request)
        using (var response = await client.SendAsync(request, cancel))
        {
            var text = await response.Content.ReadAsStringAsync(cancel);
            response.StatusCode.ShouldBe(status, text);

            using var json = JsonDocument.Parse(text);
            json.RootElement.GetProperty("title").GetString().ShouldBe(code);
        }
    }

    private async Task<(Guid Id, string Token)> DeviceAsync(string tag, bool generic = false)
    {
        var token = AttendanceDeviceToken.Generate();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var device = AttendanceDevice.Register(
            $"{tag} terminal",
            AttendanceDeviceToken.Hash(token),
            AttendanceDeviceToken.PrefixOf(token),
            DateTimeOffset.UtcNow,
            provider: generic ? AttendanceProvider.Generic : AttendanceProvider.Hikvision,
            identityProvider: AttendanceIdentityProvider.Hikvision).Value;

        db.AttendanceDevices.Add(device);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (device.Id, token);
    }

    private async Task<Guid> EmployedWithPhotoAsync(string tag)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entity = LegalEntity.Create($"{tag} MChJ").Value;
        db.LegalEntities.Add(entity);

        var employee = Employee.Create(FullName.Create($"{tag}A", "Test", null).Value).Value;
        employee.SetPhoto($"employees/{tag}.jpg");
        db.Employees.Add(employee);

        db.Employments.Add(Employment.Hire(employee.Id, entity.Id, new DateOnly(2025, 1, 1)).Value);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return employee.Id;
    }

    private async Task TerminateAsync(Guid employeeId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var employment = await db.Employments.SingleAsync(row => row.EmployeeId == employeeId, TestContext.Current.CancellationToken);
        employment.Terminate(new DateOnly(2025, 6, 30), reasonId: null).IsSuccess.ShouldBeTrue();

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task ChangeEmployeeAsync(Guid employeeId, Action<Employee> change)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var employee = await db.Employees.SingleAsync(row => row.Id == employeeId, TestContext.Current.CancellationToken);
        change(employee);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<AttendanceDevice> DeviceRowAsync(Guid deviceId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.AttendanceDevices.AsNoTracking().SingleAsync(row => row.Id == deviceId, TestContext.Current.CancellationToken);
    }

    private async Task<AgentRosterDto> RosterAsync(string token, string? serial)
    {
        var cancel = TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();
        using var request = Send(HttpMethod.Get, "/api/attendance/agent/roster", token, serial);
        using var response = await client.SendAsync(request, cancel);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(cancel));

        return (await response.Content.ReadFromJsonAsync<AgentRosterDto>(Json, cancel)).ShouldNotBeNull();
    }

    private async Task ReportAsync(string token, IReadOnlyList<AgentReportItemDto> items)
    {
        var cancel = TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();
        using var request = Send(HttpMethod.Post, "/api/attendance/agent/report", token, Serial);
        request.Content = JsonContent.Create(items, options: Json);

        using var response = await client.SendAsync(request, cancel);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(cancel));
    }

    private async Task ClockAsync(string token, DateTimeOffset deviceTime)
    {
        var cancel = TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();
        using var request = Send(HttpMethod.Post, "/api/attendance/agent/clock", token, Serial);
        request.Content = JsonContent.Create(new { deviceTime }, options: Json);

        using var response = await client.SendAsync(request, cancel);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(cancel));
    }

    private async Task IngestAsync(string token, string eventId, DateTimeOffset occurredAt)
    {
        var cancel = TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();
        using var request = Send(HttpMethod.Post, "/api/attendance/ingest", token, serial: null);
        request.Content = JsonContent.Create(
            new
            {
                events = new[]
                {
                    new
                    {
                        personId = "977777777",
                        occurredAt = occurredAt.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
                        eventId,
                    },
                },
            },
            options: Json);

        using var response = await client.SendAsync(request, cancel);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(cancel));
    }

    private static async Task<JsonElement> PassAsync(HttpClient client, Guid deviceId)
    {
        var cancel = TestContext.Current.CancellationToken;

        // Toshkent yarim tuni atrofida ham topilsin — kechadan bugungacha.
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(5)).DateTime);
        var range = string.Create(CultureInfo.InvariantCulture, $"from={today.AddDays(-1):yyyy-MM-dd}&to={today:yyyy-MM-dd}");

        using var response = await client.GetAsync(new Uri($"/api/attendance/passes?deviceId={deviceId}&{range}", UriKind.Relative), cancel);
        var text = await response.Content.ReadAsStringAsync(cancel);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);

        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("items").EnumerateArray().Single().Clone();
    }

    // Ruxsatlar endi har so'rovda BAZADAN o'qiladi (2026-09-26, `TestAccounts` izohi):
    // administrator tokeniga yozilgan ro'yxat e'tiborsiz qolardi. Har chaqiruv o'z
    // rolini va hisobini yaratadi.
    private async Task<HttpClient> ClientAsync(params string[] permissions)
    {
        var mijoz = _factory.CreateClient();
        mijoz.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await TestAccounts.TokenAsync(_factory, permissions));

        return mijoz;
    }
}
