using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Bonvi.Api.Common;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// «Davomat mas'uli» reads: a user assigned to department A sees A and its child A1, never B.
/// </summary>
/// <remarks>
/// Every test goes through the HTTP API with a real token, and each seeds its own
/// <see cref="World"/>. List assertions read the raw body: an id of department B's people
/// anywhere in the response fails the test, whichever field it arrives in.
/// </remarks>
public sealed class AttendanceScopeReadTests : IClassFixture<BonviApiFactory>
{
    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    private static readonly string[] ScopedPermissions = SystemRoles.Defaults[SystemRoles.AttendanceResponsible].Permissions;

    private static readonly string[] HrManagerPermissions = SystemRoles.Defaults[SystemRoles.HrManager].Permissions;

    private readonly BonviApiFactory _factory;

    public AttendanceScopeReadTests(BonviApiFactory factory) => _factory = factory;

    // ── lists ─────────────────────────────────────────────────────

    [Fact]
    public async Task Scoped_user_lists_show_the_assigned_branch_and_never_the_other_department()
    {
        var world = await SeedWorldAsync();
        using var scoped = await ScopedClientAsync(world.DepartmentA);

        foreach (var url in world.ListUrls())
        {
            var body = await BodyAsync(scoped, url);

            body.ShouldContain(world.InA.ToString(), Case.Insensitive, url);
            body.ShouldContain(world.InA1.ToString(), Case.Insensitive, url);
            body.ShouldNotContain(world.InB.ToString(), Case.Insensitive, url);
        }

        (await JsonAsync(scoped, "/requests/pending-count")).GetProperty("pending").GetInt32()
            .ShouldBe(4, "two absence and two schedule requests, A and A1 only");

        var trend = await JsonAsync(scoped, $"/dashboard/trend?from={Iso(world.PastDay)}&to={Iso(world.PastDay)}");
        trend.GetProperty("days")[0].GetProperty("total").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Scoped_user_sees_former_staff_of_the_branch_only_and_no_clock_warnings()
    {
        var world = await SeedWorldAsync();
        using var scoped = await ScopedClientAsync(world.DepartmentA);

        var board = await BodyAsync(scoped, "/board");
        board.ShouldContain(world.FormerA.ToString(), Case.Insensitive);
        board.ShouldNotContain(world.FormerB.ToString(), Case.Insensitive);

        var alerts = await BodyAsync(scoped, "/former-employee-alerts");
        alerts.ShouldContain(world.FormerA.ToString(), Case.Insensitive);
        alerts.ShouldNotContain(world.FormerB.ToString(), Case.Insensitive);

        (await JsonAsync(scoped, "/board")).GetProperty("clockWarningDeviceCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Filtering_by_an_employee_or_department_outside_the_scope_gives_nothing()
    {
        var world = await SeedWorldAsync();
        using var scoped = await ScopedClientAsync(world.DepartmentA);
        var day = Iso(world.PassDate);

        (await RowCountAsync(scoped, $"/passes?from={day}&to={day}&employeeId={world.InB}")).ShouldBe(0);
        (await RowCountAsync(scoped, $"/passes?from={day}&to={day}&departmentId={world.DepartmentB}")).ShouldBe(0);
        (await RowCountAsync(scoped, $"/requests?employeeId={world.InB}")).ShouldBe(0);
        (await RowCountAsync(scoped, $"/schedule-requests?employeeId={world.InB}")).ShouldBe(0);
    }

    [Fact]
    public async Task Unmatched_passes_are_hidden_from_a_scoped_user()
    {
        var world = await SeedWorldAsync();
        using var scoped = await ScopedClientAsync(world.DepartmentA);
        using var hr = await ClientAsync(HrManagerPermissions);
        var day = Iso(world.PassDate);

        (await RowCountAsync(scoped, $"/passes?from={day}&to={day}&matched=false")).ShouldBe(0);
        (await RowCountAsync(hr, $"/passes?from={day}&to={day}&search={world.UnmatchedRawId}")).ShouldBe(1);

        await ExpectErrorAsync(scoped, $"/passes/{world.UnmatchedPass}", "davomat.otish_topilmadi");
    }

    // ── single items ──────────────────────────────────────────────

    [Fact]
    public async Task Single_items_outside_the_scope_answer_not_found()
    {
        var world = await SeedWorldAsync();
        using var scoped = await ScopedClientAsync(world.DepartmentA);
        var day = Iso(world.PassDate);

        (await BodyAsync(scoped, $"/passes/{world.PassA}")).ShouldContain(world.InA.ToString(), Case.Insensitive);
        await ExpectErrorAsync(scoped, $"/passes/{world.PassB}", "davomat.otish_topilmadi");

        // The photo goes through FindPass: out of scope reads as «no such pass», in scope as «no photo».
        await ExpectErrorAsync(scoped, $"/passes/{world.PassA}/photo", "davomat.surat_yoq");
        await ExpectErrorAsync(scoped, $"/passes/{world.PassB}/photo", "davomat.otish_topilmadi");

        (await BodyAsync(scoped, $"/employees/{world.InA1}/days/{day}/passes")).ShouldContain(world.PassA1.ToString(), Case.Insensitive);
        await ExpectStatusAsync(scoped, $"/employees/{world.InB}/days/{day}/passes", HttpStatusCode.NotFound);

        (await BodyAsync(scoped, $"/schedule-requests/{world.ScheduleRequestA}")).ShouldContain(world.InA.ToString(), Case.Insensitive);
        await ExpectStatusAsync(scoped, $"/schedule-requests/{world.ScheduleRequestB}", HttpStatusCode.NotFound);

        (await BodyAsync(scoped, $"/schedules/resolve?employeeId={world.InA1}&date={day}")).ShouldContain(world.NameMark, Case.Insensitive);
        await ExpectErrorAsync(scoped, $"/schedules/resolve?employeeId={world.InB}&date={day}", "davomat.xodim_topilmadi");
    }

    /// <summary>No GET route reads one absence request, so this asks the read model directly.</summary>
    [Fact]
    public async Task Finding_an_absence_request_outside_the_scope_gives_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await SeedWorldAsync();
        var account = await TestAccounts.CreateAsync(_factory, ScopedPermissions);
        await AssignAsync(account.UserId, world.DepartmentA);

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, account.UserId.ToString()) };
        claims.AddRange(ScopedPermissions.Select(permission => new Claim(CurrentUser.PermissionClaimType, permission)));

        _factory.Services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
        };

        await using var serviceScope = _factory.Services.CreateAsyncScope();
        var readModel = serviceScope.ServiceProvider.GetRequiredService<IAttendanceReadModel>();

        (await readModel.FindAbsenceRequestAsync(world.AbsenceRequestA, ct)).ShouldNotBeNull();
        (await readModel.FindAbsenceRequestAsync(world.AbsenceRequestB, ct)).ShouldBeNull();
    }

    // ── assignments (D9) ──────────────────────────────────────────

    [Fact]
    public async Task Assignments_show_employee_and_department_rows_of_the_branch_only()
    {
        var world = await SeedWorldAsync();
        using var scoped = await ScopedClientAsync(world.DepartmentA);

        var body = await BodyAsync(scoped, "/schedule-assignments");

        foreach (var visible in new[] { world.AssignmentEmployeeA, world.AssignmentEmployeeA1, world.AssignmentDepartmentA, world.AssignmentDepartmentA1 })
        {
            body.ShouldContain(visible.ToString(), Case.Insensitive);
        }

        foreach (var hidden in new[] { world.AssignmentEmployeeB, world.AssignmentDepartmentB, world.AssignmentPosition })
        {
            body.ShouldNotContain(hidden.ToString(), Case.Insensitive);
        }

        foreach (var row in JsonDocument.Parse(body).RootElement.EnumerateArray())
        {
            row.GetProperty("scope").GetString().ShouldBeOneOf(nameof(ScheduleScope.Employee), nameof(ScheduleScope.Department));
        }
    }

    // ── exports ───────────────────────────────────────────────────

    [Fact]
    public async Task Exports_inherit_the_scope()
    {
        var world = await SeedWorldAsync();
        using var scoped = await ScopedClientAsync(world.DepartmentA);
        var day = Iso(world.PassDate);

        foreach (var url in new[] { $"/export/passes?from={day}&to={day}", $"/schedule-requests/export?search={world.NameMark}" })
        {
            var text = await WorkbookTextAsync(scoped, url);

            text.ShouldContain(world.NameMark + "A", Case.Insensitive, url);
            text.ShouldNotContain(world.NameMark + "B", Case.Insensitive, url);
        }
    }

    // ── devices ───────────────────────────────────────────────────

    [Fact]
    public async Task Device_health_never_reaches_a_scoped_user()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = await SeedWorldAsync();
        var deviceName = "Scope turnstile " + Guid.NewGuid().ToString("N")[..8];

        await using (var serviceScope = _factory.Services.CreateAsyncScope())
        {
            var context = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var device = AttendanceDevice.Register(deviceName, RandomNumberGenerator.GetBytes(32), "tst00000", DateTimeOffset.UtcNow).Value;
            device.NoteAgent(DateTimeOffset.UtcNow.AddHours(-1), null);
            context.Add(device);
            await context.SaveChangesAsync(ct);
        }

        var trendPath = $"/dashboard/trend?from={Iso(world.PastDay)}&to={Iso(world.PastDay)}";

        using var viewer = await ClientAsync(Permissions.AttendanceView);
        (await BodyAsync(viewer, "/dashboard")).ShouldContain(deviceName);
        (await BodyAsync(viewer, trendPath)).ShouldContain(deviceName);

        using var scoped = await ScopedClientAsync(world.DepartmentA);
        (await BodyAsync(scoped, "/dashboard")).ShouldNotContain(deviceName);
        (await BodyAsync(scoped, trendPath)).ShouldNotContain(deviceName);

        // Left out rather than empty: the page reads zero connected devices as a warning banner.
        (await JsonAsync(scoped, "/dashboard")).TryGetProperty("deviceHealth", out _).ShouldBeFalse();
        (await JsonAsync(scoped, trendPath)).GetProperty("deviceHealth").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // ── no assignment: fail closed ────────────────────────────────

    [Fact]
    public async Task Without_an_assignment_a_scoped_user_sees_nothing()
    {
        var world = await SeedWorldAsync();
        using var scoped = await ClientAsync(ScopedPermissions);
        var day = Iso(world.PassDate);

        foreach (var url in world.ListUrls())
        {
            var body = await BodyAsync(scoped, url);

            body.ShouldNotContain(world.InA.ToString(), Case.Insensitive, url);
            body.ShouldNotContain(world.InB.ToString(), Case.Insensitive, url);
        }

        (await RowCountAsync(scoped, $"/passes?from={day}&to={day}")).ShouldBe(0);
        (await JsonAsync(scoped, "/schedule-assignments")).GetArrayLength().ShouldBe(0);
        (await JsonAsync(scoped, "/requests/pending-count")).GetProperty("pending").GetInt32().ShouldBe(0);
        (await RowCountAsync(scoped, "/former-employee-alerts")).ShouldBe(0);

        var board = await JsonAsync(scoped, "/board");
        board.GetProperty("items").GetArrayLength().ShouldBe(0);
        board.GetProperty("expectedCount").GetInt32().ShouldBe(0);

        var trend = await JsonAsync(scoped, $"/dashboard/trend?from={Iso(world.PastDay)}&to={Iso(world.PastDay)}");
        trend.GetProperty("days")[0].GetProperty("total").GetInt32().ShouldBe(0);

        await ExpectStatusAsync(scoped, $"/passes/{world.PassA}", HttpStatusCode.NotFound);
        await ExpectStatusAsync(scoped, $"/schedules/resolve?employeeId={world.InA}&date={day}", HttpStatusCode.NotFound);
    }

    // ── search (D8) ───────────────────────────────────────────────

    [Fact]
    public async Task Scoped_search_matches_the_name_and_nothing_else()
    {
        var world = await SeedWorldAsync();
        using var scoped = await ScopedClientAsync(world.DepartmentA);
        using var hr = await ClientAsync(HrManagerPermissions);
        var day = Iso(world.PassDate);

        var month = $"year={world.PastDay.Year}&month={world.PastDay.Month}";
        string[] searches =
        [
            $"/passes?from={day}&to={day}&search=",
            $"/timesheet?{month}&search=",
            "/requests?search=",
            "/schedule-requests?search=",
            $"/dashboard?date={Iso(world.PastDay)}&search=",
        ];

        foreach (var url in searches)
        {
            // In the branch and yet not searchable: these would answer «whose number is this».
            (await RowCountAsync(scoped, url + world.PinflA)).ShouldBe(0, url + " PINFL");
            (await RowCountAsync(scoped, url + world.PhoneDigitsA)).ShouldBe(0, url + " phone");

            // Findable by name inside the branch, by no means outside it.
            (await RowCountAsync(scoped, url + world.NameMark + "A1")).ShouldBeGreaterThan(0, url + " name");
            (await RowCountAsync(scoped, url + world.NameMark + "B")).ShouldBe(0, url + " other department");

            // The same PINFL still works for HR: the narrowing is the scoped user's alone.
            (await RowCountAsync(hr, url + world.PinflA)).ShouldBeGreaterThan(0, url + " HR PINFL");
        }

        (await RowCountAsync(scoped, $"/passes?from={day}&to={day}&search={world.RawIdA}")).ShouldBe(0, "device number");
        (await RowCountAsync(hr, $"/passes?from={day}&to={day}&search={world.RawIdA}")).ShouldBe(1, "device number, HR");

        (await RowCountAsync(scoped, $"/board?search={world.PinflA}")).ShouldBe(0);
    }

    // ── existing roles ────────────────────────────────────────────

    [Fact]
    public Task Hr_manager_still_sees_everything() => SeesEverythingAsync(HrManagerPermissions);

    /// <summary>super_admin holds the marker too; attendance.manage is what keeps it unrestricted.</summary>
    [Fact]
    public Task Holder_of_the_marker_and_manage_still_sees_everything() => SeesEverythingAsync([.. Permissions.All]);

    private async Task SeesEverythingAsync(string[] permissions)
    {
        var world = await SeedWorldAsync();
        using var client = await ClientAsync(permissions);
        var day = Iso(world.PassDate);

        foreach (var url in world.ListUrls())
        {
            var body = await BodyAsync(client, url);

            body.ShouldContain(world.InA.ToString(), Case.Insensitive, url);
            body.ShouldContain(world.InB.ToString(), Case.Insensitive, url);
        }

        (await RowCountAsync(client, $"/passes?from={day}&to={day}&search={world.UnmatchedRawId}")).ShouldBe(1);

        await ExpectStatusAsync(client, $"/passes/{world.PassB}", HttpStatusCode.OK);
        await ExpectStatusAsync(client, $"/passes/{world.UnmatchedPass}", HttpStatusCode.OK);
        await ExpectStatusAsync(client, $"/employees/{world.InB}/days/{day}/passes", HttpStatusCode.OK);
        await ExpectStatusAsync(client, $"/schedule-requests/{world.ScheduleRequestB}", HttpStatusCode.OK);
        await ExpectStatusAsync(client, $"/schedules/resolve?employeeId={world.InB}&date={day}", HttpStatusCode.OK);

        var board = await JsonAsync(client, "/board");
        board.GetProperty("clockWarningDeviceCount").GetInt32().ShouldBeGreaterThan(0);

        var assignments = await BodyAsync(client, "/schedule-assignments");
        assignments.ShouldContain(world.AssignmentEmployeeB.ToString(), Case.Insensitive);
        assignments.ShouldContain(world.AssignmentDepartmentB.ToString(), Case.Insensitive);
        assignments.ShouldContain(world.AssignmentPosition.ToString(), Case.Insensitive);

        (await BodyAsync(client, "/former-employee-alerts")).ShouldContain(world.FormerB.ToString(), Case.Insensitive);
    }

    // ── helpers ───────────────────────────────────────────────────

    private async Task<HttpClient> ScopedClientAsync(Guid assignedDepartment)
    {
        var account = await TestAccounts.CreateAsync(_factory, ScopedPermissions);
        await AssignAsync(account.UserId, assignedDepartment);

        return Client(account.AccessToken);
    }

    private async Task AssignAsync(Guid userId, Guid department)
    {
        await using var serviceScope = _factory.Services.CreateAsyncScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();

        context.UserAttendanceScopes.Add(new UserAttendanceScope(userId, department));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<HttpClient> ClientAsync(params string[] permissions)
        => Client(await TestAccounts.TokenAsync(_factory, permissions));

    private HttpClient Client(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static Uri Path(string path) => new("/api/attendance" + path, UriKind.Relative);

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task<string> BodyAsync(HttpClient client, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync(Path(path), ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"{path}: {body}");

        return body;
    }

    private static async Task<JsonElement> JsonAsync(HttpClient client, string path)
        => JsonDocument.Parse(await BodyAsync(client, path)).RootElement.Clone();

    /// <summary>The rows a list returned. Not «total»: the dashboard's counts the population, search aside.</summary>
    private static async Task<int> RowCountAsync(HttpClient client, string path)
        => (await JsonAsync(client, path)).GetProperty("items").GetArrayLength();

    private static async Task ExpectStatusAsync(HttpClient client, string path, HttpStatusCode status)
    {
        var ct = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync(Path(path), ct);

        response.StatusCode.ShouldBe(status, path);
    }

    private static async Task ExpectErrorAsync(HttpClient client, string path, string code)
    {
        var ct = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync(Path(path), ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, $"{path}: {body}");
        JsonDocument.Parse(body).RootElement.GetProperty("title").GetString().ShouldBe(code, path);
    }

    private static async Task<string> WorkbookTextAsync(HttpClient client, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync(Path(path), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, path);

        using var workbook = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync(ct)));

        return string.Join(
            '\n',
            workbook.Worksheets.SelectMany(sheet => sheet.CellsUsed()).Select(cell => cell.GetFormattedString()));
    }

    private async Task<World> SeedWorldAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var serviceScope = _factory.Services.CreateAsyncScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var mark = "m" + Guid.NewGuid().ToString("N")[..8];

        var firm = LegalEntity.Create("Scope firm " + mark).Value;
        var departmentA = Department.Create("Scope A " + mark).Value;
        var departmentA1 = Department.Create("Scope A1 " + mark, departmentA.Id).Value;
        var departmentB = Department.Create("Scope B " + mark).Value;
        var position = Position.Create("Scope position " + mark).Value;
        context.AddRange(firm, departmentA, departmentA1, departmentB, position);

        // A pass an hour ago is on the board and within today, except just after midnight,
        // when the date below follows it to yesterday.
        var occurredAt = DateTimeOffset.UtcNow.AddHours(-1).ToOffset(Tashkent);
        var passDate = DateOnly.FromDateTime(occurredAt.DateTime);

        // A day no other test writes to, so summaries and exception reports stay short.
        var pastDay = new DateOnly(2015, 1, 1).AddDays(RandomNumberGenerator.GetInt32(3000));
        var birthDate = passDate.AddYears(-30);

        Employee Person(string suffix, string pinfl, string phone, Guid? department, DateOnly? ended = null)
        {
            var person = Employee.Create(FullName.Create("Qamrovov", "Xodim", mark + suffix).Value, birthDate).Value;
            person.SetPinfl(Pinfl.FromTrusted(pinfl)).IsSuccess.ShouldBeTrue();
            person.SetPhones(PhoneNumber.Create(phone).Value);

            var employment = Employment.Hire(person.Id, firm.Id, new DateOnly(2000, 1, 1), department).Value;
            if (ended is { } date)
            {
                employment.Terminate(date, reasonId: null).IsSuccess.ShouldBeTrue();
            }

            context.AddRange(person, employment);

            return person;
        }

        var pinflA = PinflFor(birthDate);
        var phoneDigitsA = "90" + RandomDigits(7);

        var inA = Person("A", pinflA, phoneDigitsA, departmentA.Id);
        var inA1 = Person("A1", PinflFor(birthDate), "90" + RandomDigits(7), departmentA1.Id);
        var inB = Person("B", PinflFor(birthDate), "90" + RandomDigits(7), departmentB.Id);
        var longGone = new DateOnly(2010, 1, 1);
        var formerA = Person("Fa", PinflFor(birthDate), "90" + RandomDigits(7), departmentA.Id, longGone);
        var formerB = Person("Fb", PinflFor(birthDate), "90" + RandomDigits(7), departmentB.Id, longGone);

        var device = AttendanceDevice.Register(
            "Scope device " + mark,
            RandomNumberGenerator.GetBytes(32),
            "tst" + RandomDigits(5),
            DateTimeOffset.UtcNow).Value;
        device.NoteEvent(DateTimeOffset.UtcNow.AddMinutes(-5), "10.0.0.7", skewSeconds: 600);
        context.Add(device);

        AttendancePass Pass(Guid? employee, string rawId)
        {
            var pass = AttendancePass.Record(
                device.Id,
                AttendanceIdentityProvider.Hikvision,
                rawId,
                occurredAt,
                occurredAt.ToUniversalTime(),
                "scope:" + Guid.NewGuid().ToString("N"),
                PassDirection.In,
                DirectionSource.Device,
                PassMethod.Face).Value;

            if (employee is { } id)
            {
                pass.Resolve(id, occurredAt.ToUniversalTime());
            }

            context.Add(pass);

            return pass;
        }

        var rawIdA = "raw" + RandomDigits(10);
        var unmatchedRawId = "raw" + RandomDigits(10);

        var passA = Pass(inA.Id, rawIdA);
        var passA1 = Pass(inA1.Id, "raw" + RandomDigits(10));
        var passB = Pass(inB.Id, "raw" + RandomDigits(10));
        Pass(formerA.Id, "raw" + RandomDigits(10));
        Pass(formerB.Id, "raw" + RandomDigits(10));
        var unmatched = Pass(null, unmatchedRawId);

        var absenceType = AbsenceType.Create("Scope " + mark).Value;
        context.Add(absenceType);

        ScheduleChangeRequest ScheduleRequest(Guid employee)
        {
            var request = ScheduleChangeRequest.Create(
                employee,
                ScheduleChangeKind.Change,
                [new ScheduleChangeDayInput(pastDay, IsWorking: false)]).Value;
            context.Add(request);

            return request;
        }

        var scheduleRequestA = ScheduleRequest(inA.Id);
        ScheduleRequest(inA1.Id);
        var scheduleRequestB = ScheduleRequest(inB.Id);

        AbsenceRequest Absence(Guid employee)
        {
            var request = AbsenceRequest.Create(employee, absenceType.Id, pastDay, pastDay).Value;
            context.Add(request);

            return request;
        }

        var absenceRequestA = Absence(inA.Id);
        Absence(inA1.Id);
        var absenceRequestB = Absence(inB.Id);

        foreach (var employee in new[] { inA, inA1, inB })
        {
            context.Add(AttendanceDay.Compute(
                employee.Id,
                pastDay,
                new AttendanceDayFacts(null, null, null, null, null, null, null, null, null, 0, 0, 0, 0, 0, 0, 0, 0, 0, AttendanceDayStatus.Absent),
                DateTimeOffset.UtcNow));
        }

        var schedule = WorkSchedule.Create(
            "Scope schedule " + mark,
            [.. Enumerable.Range(1, 7).Select(day => new WorkScheduleDayInput(day, true, new TimeOnly(9, 0), new TimeOnly(18, 0)))]).Value;
        context.Add(schedule);

        ScheduleAssignment Assignment(ScheduleScope scope, Guid target)
        {
            var assignment = ScheduleAssignment.Assign(schedule.Id, scope, target, new DateOnly(2000, 1, 1)).Value;
            context.Add(assignment);

            return assignment;
        }

        var world = new World(
            departmentA.Id,
            departmentB.Id,
            inA.Id,
            inA1.Id,
            inB.Id,
            formerA.Id,
            formerB.Id,
            mark,
            pinflA,
            phoneDigitsA,
            rawIdA,
            unmatchedRawId,
            passDate,
            pastDay,
            passA.Id,
            passA1.Id,
            passB.Id,
            unmatched.Id,
            scheduleRequestA.Id,
            scheduleRequestB.Id,
            absenceRequestA.Id,
            absenceRequestB.Id,
            Assignment(ScheduleScope.Employee, inA.Id).Id,
            Assignment(ScheduleScope.Employee, inA1.Id).Id,
            Assignment(ScheduleScope.Employee, inB.Id).Id,
            Assignment(ScheduleScope.Department, departmentA.Id).Id,
            Assignment(ScheduleScope.Department, departmentA1.Id).Id,
            Assignment(ScheduleScope.Department, departmentB.Id).Id,
            Assignment(ScheduleScope.Position, position.Id).Id);

        await context.SaveChangesAsync(ct);

        return world;
    }

    /// <summary>A PINFL whose embedded date agrees with the card, as <c>Employee.SetPinfl</c> requires.</summary>
    private static string PinflFor(DateOnly birthDate)
    {
        var century = birthDate.Year < 2000 ? "3" : "5";

        return century + birthDate.ToString("ddMMyy", CultureInfo.InvariantCulture) + RandomDigits(7);
    }

    private static string RandomDigits(int length)
        => string.Concat(Enumerable.Range(0, length).Select(_ => RandomNumberGenerator.GetInt32(1, 10).ToString(CultureInfo.InvariantCulture)));

    private sealed record World(
        Guid DepartmentA,
        Guid DepartmentB,
        Guid InA,
        Guid InA1,
        Guid InB,
        Guid FormerA,
        Guid FormerB,
        string NameMark,
        string PinflA,
        string PhoneDigitsA,
        string RawIdA,
        string UnmatchedRawId,
        DateOnly PassDate,
        DateOnly PastDay,
        Guid PassA,
        Guid PassA1,
        Guid PassB,
        Guid UnmatchedPass,
        Guid ScheduleRequestA,
        Guid ScheduleRequestB,
        Guid AbsenceRequestA,
        Guid AbsenceRequestB,
        Guid AssignmentEmployeeA,
        Guid AssignmentEmployeeA1,
        Guid AssignmentEmployeeB,
        Guid AssignmentDepartmentA,
        Guid AssignmentDepartmentA1,
        Guid AssignmentDepartmentB,
        Guid AssignmentPosition)
    {
        /// <summary>Every list that names staff. Each is narrowed to this world where the route allows it.</summary>
        public IEnumerable<string> ListUrls()
        {
            var passDay = Iso(PassDate);
            var past = Iso(PastDay);

            yield return "/board";
            yield return $"/passes?from={passDay}&to={passDay}&search={NameMark}";
            yield return $"/timesheet?year={PastDay.Year}&month={PastDay.Month}&search={NameMark}";
            yield return $"/reports/summary?from={past}&to={past}";
            yield return $"/reports/exceptions?kind=absent&from={past}&to={past}";
            yield return $"/requests?search={NameMark}";
            yield return $"/schedule-requests?search={NameMark}";
            yield return $"/dashboard?date={past}&search={NameMark}";
        }
    }
}
