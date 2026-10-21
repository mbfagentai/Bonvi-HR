using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bonvi.Application.Attendance.Agent;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Ingest;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// Qurilma agenti — ro'yxat, shaxs raqamlari, hisobot va qayta yozish
/// (SPEC-agent §G).
/// </summary>
/// <remarks>
/// <para>
/// Agentning O'ZI bu yerda ishlamaydi va terminal ham yo'q: tekshirilayotgan
/// narsa — SERVER tomonining shartnomasi. Terminal bilan bo'lgan qism
/// 2026-09-21 da tirik qurilmada qo'lda o'tkazilgan va uni sinov muhitida
/// takrorlab bo'lmaydi.
/// </para>
/// <para>
/// Tasdiqlash qurilma tokeni bilan, xuddi qabul yo'lidagidek — ya'ni bu
/// testlar bir vaqtning o'zida «agent endpointlari JWT talab qilmaydi,
/// lekin ochiq ham emas» degan shartni mahkamlaydi.
/// </para>
/// </remarks>
public sealed class AttendanceAgentApiTests : IClassFixture<BonviApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly BonviApiFactory _factory;

    public AttendanceAgentApiTests(BonviApiFactory factory) => _factory = factory;

    /// <summary>
    /// Ro'yxatda faqat bugun ishlayotgan VA rasmi bor xodim bo'ladi
    /// (SPEC-agent §D2).
    /// </summary>
    [Fact]
    public async Task Royxatda_faqat_rasmi_bor_ishlayotgan_xodim_boladi()
    {
        var cancel = TestContext.Current.CancellationToken;
        var tag = Tag();

        var (deviceId, token) = await DeviceAsync(tag);
        Guid ready;
        Guid noPhoto;
        Guid departed;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = LegalEntity.Create($"{tag} MChJ").Value;
            db.LegalEntities.Add(entity);

            var withPhoto = Person(tag, "A");
            withPhoto.SetPhoto($"employees/{tag}-a.jpg");
            ready = withPhoto.Id;

            var without = Person(tag, "B");
            noPhoto = without.Id;

            var gone = Person(tag, "C");
            gone.SetPhoto($"employees/{tag}-c.jpg");
            departed = gone.Id;

            db.Employees.AddRange(withPhoto, without, gone);

            db.Employments.Add(Employment.Hire(withPhoto.Id, entity.Id, new DateOnly(2025, 1, 1)).Value);
            db.Employments.Add(Employment.Hire(without.Id, entity.Id, new DateOnly(2025, 1, 1)).Value);

            var goneJob = Employment.Hire(gone.Id, entity.Id, new DateOnly(2024, 1, 1)).Value;
            goneJob.Terminate(new DateOnly(2025, 6, 30), reasonId: null).IsSuccess.ShouldBeTrue();
            db.Employments.Add(goneJob);

            await db.SaveChangesAsync(cancel);
        }

        var roster = await RosterAsync(token);

        roster.DeviceId.ShouldBe(deviceId);

        var ids = roster.Entries.Select(entry => entry.EmployeeId).ToList();
        ids.ShouldContain(ready);
        ids.ShouldNotContain(noPhoto, "rasmsiz odamni yuz terminaliga yozib bo'lmaydi");
        ids.ShouldNotContain(departed, "ishdan ketgan odam ro'yxatda qolmasligi kerak");

        var row = roster.Entries.Single(entry => entry.EmployeeId == ready);
        row.PhotoVersion.ShouldBe($"employees/{tag}-a.jpg");
        // Belgi: rasm, «Qayta yozish» sanog'i va ism izi (SPEC-agent §E, 2026-09-27).
        row.Version.ShouldBe(EnrollmentVersion.Of($"employees/{tag}-a.jpg", 0, row.FullName));
        row.Version.ShouldStartWith($"employees/{tag}-a.jpg:0:");
        row.Hold.ShouldBeFalse();
        row.Force.ShouldBeTrue("hali hech narsa yozilmagan — agent uni yozishi kerak");
    }

    /// <summary>
    /// Shaxs raqami BIR MARTA ajratiladi: ikkinchi so'rovda o'sha raqam
    /// qaytadi va mosliklar jadvalida ikkinchi qator paydo bo'lmaydi
    /// (SPEC-agent §D1).
    /// </summary>
    [Fact]
    public async Task Shaxs_raqami_bir_marta_ajratiladi()
    {
        var cancel = TestContext.Current.CancellationToken;
        var tag = Tag();

        var (_, token) = await DeviceAsync(tag);
        var employeeId = await EmployedWithPhotoAsync(tag);

        var first = await RosterAsync(token);
        var second = await RosterAsync(token);

        var personNo = first.Entries.Single(entry => entry.EmployeeId == employeeId).PersonNo;

        personNo.Length.ShouldBe(9);
        personNo[0].ShouldBe('9');
        personNo[1..].ShouldAllBe(character => char.IsAsciiDigit(character));
        DevicePersonNo.IsManaged(personNo).ShouldBeTrue();

        second.Entries.Single(entry => entry.EmployeeId == employeeId).PersonNo.ShouldBe(personNo);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var identities = await db.AttendanceIdentities
            .AsNoTracking()
            .Where(identity => identity.EmployeeId == employeeId)
            .ToListAsync(cancel);

        identities.ShouldHaveSingleItem().ExternalPersonId.ShouldBe(personNo);
    }

    /// <summary>
    /// Noto'g'ri token — 401, faolsizlantirilgan qurilma — 403. Ikkala holda
    /// ham ro'yxat berilmaydi (qabul yo'lidagi bilan bir xil qoida).
    /// </summary>
    [Fact]
    public async Task Notogri_token_401_faolsiz_qurilma_403()
    {
        var cancel = TestContext.Current.CancellationToken;
        var tag = Tag();

        using var client = _factory.CreateClient();

        using var unknown = await client.SendAsync(Request(HttpMethod.Get, "/api/attendance/agent/roster", "bonvi_yoq"), cancel);
        unknown.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var missing = await client.SendAsync(Request(HttpMethod.Get, "/api/attendance/agent/roster", token: null), cancel);
        missing.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var (_, token) = await DeviceAsync(tag, active: false);

        using var inactive = await client.SendAsync(Request(HttpMethod.Get, "/api/attendance/agent/roster", token), cancel);
        inactive.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Hisobot holatni yozadi: muvaffaqiyat — «yozildi» va belgi, xato —
    /// «xato» va qurilmaning O'Z matni, belgi esa TEGILMAYDI (SPEC-agent §D3).
    /// </summary>
    [Fact]
    public async Task Hisobot_holatni_yozadi_va_xatoda_belgi_tegilmaydi()
    {
        var cancel = TestContext.Current.CancellationToken;
        var tag = Tag();

        var (deviceId, token) = await DeviceAsync(tag);
        var employeeId = await EmployedWithPhotoAsync(tag);

        var roster = await RosterAsync(token);
        var version = roster.Entries.Single(entry => entry.EmployeeId == employeeId).Version;

        await ReportAsync(token, [new AgentReportItemDto(employeeId, null, AgentEnrollmentAction.Upsert, Ok: true, version, null)]);

        var written = await EnrollmentAsync(deviceId, employeeId);
        written.Status.ShouldBe(DeviceEnrollmentStatus.Synced);
        written.AppliedVersion.ShouldBe(version);
        written.SyncedAt.ShouldNotBeNull();

        // Ro'yxat endi uni «yozish kerak» demaydi.
        var after = await RosterAsync(token);
        after.Entries.Single(entry => entry.EmployeeId == employeeId).Force.ShouldBeFalse();

        await ReportAsync(
            token,
            [new AgentReportItemDto(employeeId, null, AgentEnrollmentAction.Upsert, Ok: false, version, "face quality too low")]);

        var failed = await EnrollmentAsync(deviceId, employeeId);
        failed.Status.ShouldBe(DeviceEnrollmentStatus.Failed);
        failed.Error.ShouldBe("face quality too low");
        failed.AppliedVersion.ShouldBe(version, "xato urinish «yozildi» deb sanalmasligi kerak");
    }

    /// <summary>
    /// O'chirish qatori XODIMSIZ keladi — server uni shaxs raqami orqali
    /// topadi (qurilmada boshqa hech narsa yo'q).
    /// </summary>
    [Fact]
    public async Task Ochirish_qatori_shaxs_raqami_boyicha_bogolanadi()
    {
        var tag = Tag();

        var (deviceId, token) = await DeviceAsync(tag);
        var employeeId = await EmployedWithPhotoAsync(tag);

        var roster = await RosterAsync(token);
        var personNo = roster.Entries.Single(entry => entry.EmployeeId == employeeId).PersonNo;

        await ReportAsync(token, [new AgentReportItemDto(Guid.Empty, personNo, AgentEnrollmentAction.Remove, Ok: true, null, null)]);

        var row = await EnrollmentAsync(deviceId, employeeId);
        row.Status.ShouldBe(DeviceEnrollmentStatus.Removed);
        row.AppliedVersion.ShouldBeNull();
    }

    /// <summary>
    /// «Qayta yozish» belgini eskirtiradi va agent xodimni qayta yozadi;
    /// «Hammasini sinxronlash» esa FAQAT xato qatorlarga tegadi
    /// (SPEC-agent §E, egasining qarori).
    /// </summary>
    [Fact]
    public async Task Qayta_yozish_belgini_eskirtiradi_sinxronlash_faqat_xatoga_tegadi()
    {
        var cancel = TestContext.Current.CancellationToken;
        var tag = Tag();

        var (deviceId, token) = await DeviceAsync(tag);
        var employeeId = await EmployedWithPhotoAsync(tag);

        var roster = await RosterAsync(token);
        var version = roster.Entries.Single(entry => entry.EmployeeId == employeeId).Version;

        await ReportAsync(token, [new AgentReportItemDto(employeeId, null, AgentEnrollmentAction.Upsert, Ok: true, version, null)]);

        using var hr = await AdminClientAsync();

        using var deviceResync = await hr.PostAsync(
            new Uri($"/api/attendance/devices/{deviceId}/enrollments/resync", UriKind.Relative),
            content: null,
            cancel);

        deviceResync.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await deviceResync.Content.ReadFromJsonAsync<EnrollmentResyncDto>(Json, cancel))
            .ShouldNotBeNull().Affected.ShouldBe(0, "joyida turgan xodimga tegilmaydi");

        (await RosterAsync(token)).Entries.Single(entry => entry.EmployeeId == employeeId).Force.ShouldBeFalse();

        using var employeeResync = await hr.PostAsync(
            new Uri($"/api/attendance/employees/{employeeId}/enrollments/resync", UriKind.Relative),
            content: null,
            cancel);

        employeeResync.StatusCode.ShouldBe(HttpStatusCode.OK);

        var forced = await RosterAsync(token);
        var entry = forced.Entries.Single(row => row.EmployeeId == employeeId);

        entry.Force.ShouldBeTrue();
        entry.Version.ShouldNotBe(version, "belgi «Qayta yozish» sanog'ini ham o'z ichiga oladi");
        (await EnrollmentAsync(deviceId, employeeId)).ForceVersion.ShouldBe(1);
    }

    /// <summary>
    /// Qurilma soati TO'G'RIDAN-TO'G'RI o'lchanadi: o'lchov hodisalar
    /// navbatiga bog'liq emas va ogohlantirish shundan chiqadi.
    /// </summary>
    [Fact]
    public async Task Qurilma_soati_olchanadi_va_ogohlantirish_shundan_chiqadi()
    {
        var cancel = TestContext.Current.CancellationToken;
        var tag = Tag();

        var (deviceId, token) = await DeviceAsync(tag);

        // O'n daqiqa oldinda yurgan terminal — chegaradan (5 daqiqa) oshadi.
        var ahead = await ClockAsync(token, DateTimeOffset.UtcNow.AddMinutes(10));

        ahead.OffsetSeconds.ShouldBeInRange(570, 630);
        ahead.Warning.ShouldBeTrue();

        // Soat to'g'rilandi — belgi darhol tinadi.
        var fixedClock = await ClockAsync(token, DateTimeOffset.UtcNow);

        Math.Abs(fixedClock.OffsetSeconds).ShouldBeLessThan(60);
        fixedClock.Warning.ShouldBeFalse();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var device = await db.AttendanceDevices
            .AsNoTracking()
            .SingleAsync(row => row.Id == deviceId, cancel);

        device.ClockCheckedAt.ShouldNotBeNull();
        device.LastSkewSeconds.ShouldBeNull("o'tish kelmagan — o'tishdan olinadigan son tegilmasligi kerak");
    }

    // ────────────────────────────────────────────────────────────
    //  Yordamchilar
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Terminalning o'z yozuvi (SPEC-agent §M): bog'langan iVMS raqami ro'yxatda
    /// beriladi, Bonvi raqami baribir ajratiladi, «Covered» hisoboti holatni yozadi
    /// va ekranda o'sha raqam bilan, nosozliklar ro'yxatidan tashqarida ko'rinadi.
    /// </summary>
    [Fact]
    public async Task Terminalning_oz_yozuvi_royxatda_beriladi_va_hisobotda_saqlanadi()
    {
        var cancel = TestContext.Current.CancellationToken;
        var tag = Tag();

        var (deviceId, token) = await DeviceAsync(tag);
        var employeeId = await EmployedWithPhotoAsync(tag);
        var other = await EmployedWithPhotoAsync(tag + "b");
        var alias = $"3{tag}";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AttendanceIdentities.Add(AttendanceIdentity.Link(
                AttendanceIdentityProvider.Verifix, alias, employeeId, IdentitySource.Import, "iVMS ismi").Value);
            await db.SaveChangesAsync(cancel);
        }

        var roster = await RosterAsync(token);
        var entry = roster.Entries.Single(row => row.EmployeeId == employeeId);

        entry.AliasNumbers.ShouldBe([alias]);
        DevicePersonNo.IsManaged(entry.PersonNo).ShouldBeTrue("raqam baribir ajratiladi — boshqa terminallar uchun");
        entry.Force.ShouldBeTrue();
        roster.Entries.Single(row => row.EmployeeId == other).AliasNumbers.ShouldBeNull();

        await ReportAsync(token, [new AgentReportItemDto(employeeId, alias, AgentEnrollmentAction.Covered, Ok: true, entry.Version, null)]);

        var written = await EnrollmentAsync(deviceId, employeeId);
        written.Status.ShouldBe(DeviceEnrollmentStatus.Covered);
        written.AppliedVersion.ShouldBe(entry.Version);

        (await RosterAsync(token)).Entries.Single(row => row.EmployeeId == employeeId).Force
            .ShouldBeFalse("server endi biladi — agent har daqiqada qayta aytmaydi");

        using var hr = await AdminClientAsync();

        var page = await hr.GetFromJsonAsync<DeviceEnrollmentPageDto>($"/api/attendance/devices/{deviceId}/enrollments", Json, cancel);
        page!.Summary.Covered.ShouldBe(1);
        page.Rows.ShouldNotContain(row => row.EmployeeId == employeeId, "ishlayotgan xodimda bu nosozlik emas");

        var card = await hr.GetFromJsonAsync<List<DeviceEnrollmentRowDto>>($"/api/attendance/employees/{employeeId}/enrollments", Json, cancel);
        var mine = card!.Single(row => row.DeviceId == deviceId);
        mine.State.ShouldBe(DeviceEnrollmentState.Covered);
        mine.PersonNo.ShouldBe(alias, "HR iVMS'dagi raqamni qidiradi — terminalda Bonvi raqami yo'q");
    }

    private static string Tag() => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8];

    private static Employee Person(string tag, string suffix)
        => Employee.Create(FullName.Create($"{tag}{suffix}", "Test", null).Value).Value;

    private static HttpRequestMessage Request(HttpMethod method, string path, string? token)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));

        if (token is not null)
        {
            request.Headers.Add("X-Bonvi-Device-Token", token);
        }

        return request;
    }

    private async Task<(Guid Id, string Token)> DeviceAsync(string tag, bool active = true)
    {
        var token = AttendanceDeviceToken.Generate();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var device = AttendanceDevice.Register(
            $"{tag} terminal",
            AttendanceDeviceToken.Hash(token),
            AttendanceDeviceToken.PrefixOf(token),
            DateTimeOffset.UtcNow).Value;

        if (!active)
        {
            device.Deactivate();
        }

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

        var employee = Person(tag, "A");
        employee.SetPhoto($"employees/{tag}.jpg");
        db.Employees.Add(employee);

        db.Employments.Add(Employment.Hire(employee.Id, entity.Id, new DateOnly(2025, 1, 1)).Value);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return employee.Id;
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var cancel = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = BonviApiFactory.TestAdminLogin, password = BonviApiFactory.TestAdminPassword },
            cancel);

        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(cancel);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    private async Task<AgentRosterDto> RosterAsync(string token)
    {
        var cancel = TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();
        using var response = await client.SendAsync(Request(HttpMethod.Get, "/api/attendance/agent/roster", token), cancel);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(cancel));

        return (await response.Content.ReadFromJsonAsync<AgentRosterDto>(Json, cancel)).ShouldNotBeNull();
    }

    private async Task ReportAsync(string token, IReadOnlyList<AgentReportItemDto> items)
    {
        var cancel = TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();
        using var request = Request(HttpMethod.Post, "/api/attendance/agent/report", token);
        request.Content = JsonContent.Create(items, options: Json);

        using var response = await client.SendAsync(request, cancel);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(cancel));
    }

    private async Task<DeviceClockDto> ClockAsync(string token, DateTimeOffset deviceTime)
    {
        var cancel = TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();
        using var request = Request(HttpMethod.Post, "/api/attendance/agent/clock", token);
        request.Content = JsonContent.Create(new { deviceTime }, options: Json);

        using var response = await client.SendAsync(request, cancel);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(cancel));

        return (await response.Content.ReadFromJsonAsync<DeviceClockDto>(Json, cancel)).ShouldNotBeNull();
    }

    private async Task<DeviceEnrollment> EnrollmentAsync(Guid deviceId, Guid employeeId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.DeviceEnrollments
            .AsNoTracking()
            .SingleAsync(
                row => row.DeviceId == deviceId && row.EmployeeId == employeeId,
                TestContext.Current.CancellationToken);
    }
}
