using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Bonvi.Application.Common.Photos;
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
/// «Davomat mas'uli» writes and employee routes: a user assigned to department A works on A
/// and its child A1, and every attempt on B reads as «no such thing» and writes nothing.
/// </summary>
/// <remarks>
/// Every test goes through the HTTP API with a real token and seeds its own <see cref="World"/>.
/// Row counts come straight from the database, so a 404 that had already written is caught.
/// </remarks>
public sealed class AttendanceScopeWriteTests : IClassFixture<BonviApiFactory>
{
    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    private static readonly string[] ScopedPermissions = SystemRoles.Defaults[SystemRoles.AttendanceResponsible].Permissions;

    private static readonly string[] HrManagerPermissions = SystemRoles.Defaults[SystemRoles.HrManager].Permissions;

    private readonly BonviApiFactory _factory;

    public AttendanceScopeWriteTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ── adding a manual pass ──────────────────────────────────────

    [Fact]
    public async Task Scoped_user_adds_and_deletes_a_manual_pass_in_the_assigned_branch()
    {
        var world = await SeedWorldAsync();
        using var scoped = await ScopedClientAsync(world.DepartmentA);

        using var added = await AddPassAsync(scoped, world.InA1);
        added.StatusCode.ShouldBe(HttpStatusCode.OK, await TextAsync(added));

        var pass = await JsonAsync(added);
        pass.ValueKind.ShouldBe(JsonValueKind.Object, "the response must be the new pass, not a null success");
        pass.GetProperty("manual").GetProperty("note").GetString().ShouldBe("Terminal did not read");

        var rows = await CountRowsAsync(world.InA1);
        rows.Passes.ShouldBe(1);
        rows.ManualPasses.ShouldBe(1);
        rows.Days.ShouldBeGreaterThan(0, "the days are recomputed under the scoped user too");

        using var deleted = await scoped.DeleteAsync(Path($"/manual-passes/{pass.GetProperty("id").GetGuid()}"), Ct);
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent, await TextAsync(deleted));

        (await CountRowsAsync(world.InA1)).Passes.ShouldBe(0);
        (await CountRowsAsync(world.InA1)).ManualPasses.ShouldBe(0);
    }

    [Fact]
    public async Task Adding_a_manual_pass_outside_the_scope_gives_not_found_and_writes_nothing()
    {
        var world = await SeedWorldAsync();
        using var scoped = await ScopedClientAsync(world.DepartmentA);

        using var added = await AddPassAsync(scoped, world.InB);
        await ExpectErrorAsync(added, HttpStatusCode.NotFound, "davomat.xodim_topilmadi");

        // A date rule answering first would confirm that the person exists.
        using var future = await AddPassAsync(scoped, world.InB, Today().AddDays(5));
        await ExpectErrorAsync(future, HttpStatusCode.NotFound, "davomat.xodim_topilmadi");

        (await CountRowsAsync(world.InB)).ShouldBe(new Rows(0, 0, 0));
    }

    [Fact]
    public async Task Without_an_assignment_a_scoped_user_adds_nothing()
    {
        var world = await SeedWorldAsync();
        using var scoped = Client((await TestAccounts.CreateAsync(_factory, ScopedPermissions)).AccessToken);

        using var added = await AddPassAsync(scoped, world.InA1);
        await ExpectErrorAsync(added, HttpStatusCode.NotFound, "davomat.xodim_topilmadi");

        (await CountRowsAsync(world.InA1)).ShouldBe(new Rows(0, 0, 0));
    }

    // ── deleting a manual pass ────────────────────────────────────

    [Fact]
    public async Task Deleting_a_manual_pass_outside_the_scope_gives_not_found_and_it_remains()
    {
        var world = await SeedWorldAsync();
        using var scoped = await ScopedClientAsync(world.DepartmentA);
        using var hr = await ClientAsync(HrManagerPermissions);

        using var added = await AddPassAsync(hr, world.InB);
        added.StatusCode.ShouldBe(HttpStatusCode.OK, await TextAsync(added));
        var passId = (await JsonAsync(added)).GetProperty("id").GetGuid();
        var before = await CountRowsAsync(world.InB);

        using var deleted = await scoped.DeleteAsync(Path($"/manual-passes/{passId}"), Ct);
        await ExpectErrorAsync(deleted, HttpStatusCode.NotFound, "davomat.otish_topilmadi");

        (await CountRowsAsync(world.InB)).ShouldBe(before);
        before.Passes.ShouldBe(1);
        before.ManualPasses.ShouldBe(1);
    }

    [Fact]
    public async Task The_not_manual_error_tells_a_scoped_user_nothing_about_passes_outside_the_scope()
    {
        var world = await SeedWorldAsync();
        using var scoped = await ScopedClientAsync(world.DepartmentA);
        using var hr = await ClientAsync(HrManagerPermissions);

        var terminalB = await TerminalPassAsync(world.InB);
        var unmatched = await TerminalPassAsync(employee: null);
        var terminalA1 = await TerminalPassAsync(world.InA1);

        using (var response = await scoped.DeleteAsync(Path($"/manual-passes/{terminalB}"), Ct))
        {
            await ExpectErrorAsync(response, HttpStatusCode.NotFound, "davomat.otish_topilmadi");
        }

        using (var response = await scoped.DeleteAsync(Path($"/manual-passes/{unmatched}"), Ct))
        {
            await ExpectErrorAsync(response, HttpStatusCode.NotFound, "davomat.otish_topilmadi");
        }

        // In scope, and for HR, the rule is unchanged.
        using (var response = await scoped.DeleteAsync(Path($"/manual-passes/{terminalA1}"), Ct))
        {
            await ExpectErrorAsync(response, HttpStatusCode.BadRequest, "davomat.belgi_qolda_emas");
        }

        foreach (var pass in new[] { terminalB, unmatched })
        {
            using var response = await hr.DeleteAsync(Path($"/manual-passes/{pass}"), Ct);
            await ExpectErrorAsync(response, HttpStatusCode.BadRequest, "davomat.belgi_qolda_emas");
        }
    }

    // ── lookup ────────────────────────────────────────────────────

    [Fact]
    public async Task Lookup_returns_active_people_in_scope_with_the_agreed_fields_only()
    {
        var world = await SeedWorldAsync();
        using var scoped = await ScopedClientAsync(world.DepartmentA);

        var rows = await LookupAsync(scoped, $"search={world.Mark}");

        rows.Select(row => row.GetProperty("id").GetGuid()).ShouldBe([world.InA, world.InA1], ignoreOrder: true);

        var row = rows.Single(item => item.GetProperty("id").GetGuid() == world.InA1);
        row.EnumerateObject().Select(property => property.Name)
            .ShouldBe(["id", "fullName", "departmentName", "positionName", "hasPhoto"], ignoreOrder: true);
        row.GetProperty("fullName").GetString().ShouldBe($"Qamrovov Xodim {world.Mark}A1");
        row.GetProperty("departmentName").GetString().ShouldBe($"Scope A1 {world.Mark}");
        row.GetProperty("positionName").GetString().ShouldBe($"Scope position {world.Mark}");
        row.GetProperty("hasPhoto").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Lookup_matches_the_name_alone()
    {
        var world = await SeedWorldAsync();
        using var scoped = await ScopedClientAsync(world.DepartmentA);
        using var hr = await ClientAsync(HrManagerPermissions);

        foreach (var client in new[] { scoped, hr })
        {
            (await LookupAsync(client, $"search={world.PinflA1}")).ShouldBeEmpty("PINFL");
            (await LookupAsync(client, $"search={world.PhoneA1}")).ShouldBeEmpty("phone");
            (await LookupAsync(client, $"search={world.Mark}A1")).Count.ShouldBe(1, "name");
        }
    }

    [Fact]
    public async Task Lookup_caps_the_limit()
    {
        var world = await SeedWorldAsync(extraInA: 55);
        using var scoped = await ScopedClientAsync(world.DepartmentA);
        var search = $"search={world.Mark}";

        (await LookupAsync(scoped, search)).Count.ShouldBe(20, "default");
        (await LookupAsync(scoped, $"{search}&limit=3")).Count.ShouldBe(3);
        (await LookupAsync(scoped, $"{search}&limit=100000")).Count.ShouldBe(50, "cap");
        (await LookupAsync(scoped, $"{search}&limit=0")).Count.ShouldBe(1, "floor");
    }

    [Fact]
    public async Task Without_an_assignment_the_lookup_is_empty()
    {
        var world = await SeedWorldAsync();
        using var scoped = Client((await TestAccounts.CreateAsync(_factory, ScopedPermissions)).AccessToken);

        (await LookupAsync(scoped, $"search={world.Mark}")).ShouldBeEmpty();
        (await LookupAsync(scoped, string.Empty)).ShouldBeEmpty();
    }

    // ── photo ─────────────────────────────────────────────────────

    [Fact]
    public async Task Photo_is_given_in_scope_and_reads_as_not_found_outside_it()
    {
        var world = await SeedWorldAsync();
        using var scoped = await ScopedClientAsync(world.DepartmentA);
        using var editor = await ClientAsync(Permissions.EmployeesEdit);

        // Before the upload: the existing no-photo error, in scope only.
        using (var response = await scoped.GetAsync(Path($"/employees/{world.InA1}/photo"), Ct))
        {
            await ExpectErrorAsync(response, HttpStatusCode.NotFound, "xodim.rasm_yoq");
        }

        using (var response = await scoped.GetAsync(Path($"/employees/{world.InB}/photo"), Ct))
        {
            await ExpectErrorAsync(response, HttpStatusCode.NotFound, "xodim.topilmadi");
        }

        await UploadPhotoAsync(editor, world.InA1);
        await UploadPhotoAsync(editor, world.InB);

        string version;

        using (var response = await scoped.GetAsync(Path($"/employees/{world.InA1}/photo"), Ct))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await TextAsync(response));
            response.Content.Headers.ContentType!.MediaType.ShouldBe("image/jpeg");
            response.Headers.CacheControl!.Private.ShouldBeTrue();
            response.Headers.CacheControl.NoCache.ShouldBeTrue("a browser must ask again once the scope shrinks");
            version = response.Headers.ETag!.Tag;
        }

        using (var request = new HttpRequestMessage(HttpMethod.Get, Path($"/employees/{world.InA1}/photo")))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", version);
            using var response = await scoped.SendAsync(request, Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        }

        // Having a photo changes nothing outside the scope, and neither does a matching ETag.
        using (var request = new HttpRequestMessage(HttpMethod.Get, Path($"/employees/{world.InB}/photo")))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", version);
            using var response = await scoped.SendAsync(request, Ct);
            await ExpectErrorAsync(response, HttpStatusCode.NotFound, "xodim.topilmadi");
        }

        using (var response = await scoped.GetAsync(Path($"/employees/{Guid.NewGuid()}/photo"), Ct))
        {
            await ExpectErrorAsync(response, HttpStatusCode.NotFound, "xodim.topilmadi");
        }

        (await LookupAsync(scoped, $"search={world.Mark}A1")).Single().GetProperty("hasPhoto").GetBoolean().ShouldBeTrue();
    }

    // ── pass photo ────────────────────────────────────────────────

    [Fact]
    public async Task Pass_photo_is_revalidated_for_a_scoped_user_and_cached_for_hr()
    {
        var world = await SeedWorldAsync();
        var passId = await TerminalPassAsync(world.InA1);
        var key = "scope-write/" + Guid.NewGuid().ToString("N") + ".jpg";

        await using (var serviceScope = _factory.Services.CreateAsyncScope())
        {
            var context = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pass = await context.Set<AttendancePass>().SingleAsync(row => row.Id == passId, Ct);
            pass.AttachPhoto(key).ShouldBeTrue();
            await context.SaveChangesAsync(Ct);
        }

        // A matching version answers 304 from the key alone, with the headers of a 200.
        async Task<CacheControlHeaderValue> CacheControlAsync(HttpClient client)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Path($"/passes/{passId}/photo"));
            request.Headers.TryAddWithoutValidation("If-None-Match", $"\"{PhotoLimits.VersionOf(key)}\"");

            using var response = await client.SendAsync(request, Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.NotModified, await TextAsync(response));

            return response.Headers.CacheControl.ShouldNotBeNull();
        }

        using var scoped = await ScopedClientAsync(world.DepartmentA);
        var forScoped = await CacheControlAsync(scoped);
        forScoped.Private.ShouldBeTrue();
        forScoped.NoCache.ShouldBeTrue("a browser must ask again once the scope shrinks");
        forScoped.MaxAge.ShouldBeNull();

        using var hr = await ClientAsync(HrManagerPermissions);
        var forHr = await CacheControlAsync(hr);
        forHr.Private.ShouldBeTrue();
        forHr.NoCache.ShouldBeFalse();
        forHr.MaxAge.ShouldBe(TimeSpan.FromDays(1));
    }

    // ── the attendance-only employee page ─────────────────────────

    [Fact]
    public async Task Employee_header_gives_the_agreed_fields_in_scope_and_not_found_outside_it()
    {
        var world = await SeedWorldAsync();
        using var scoped = await ScopedClientAsync(world.DepartmentA);

        using var inside = await scoped.GetAsync(Path($"/employees/{world.InA1}/header"), Ct);
        inside.StatusCode.ShouldBe(HttpStatusCode.OK, await TextAsync(inside));

        var header = await JsonAsync(inside);
        header.EnumerateObject().Select(property => property.Name)
            .ShouldBe(["id", "fullName", "departmentName", "positionName", "hasPhoto"], ignoreOrder: true);
        header.GetProperty("fullName").GetString().ShouldBe($"Qamrovov Xodim {world.Mark}A1");
        header.GetProperty("departmentName").GetString().ShouldBe($"Scope A1 {world.Mark}");
        header.GetProperty("positionName").GetString().ShouldBe($"Scope position {world.Mark}");

        using (var outside = await scoped.GetAsync(Path($"/employees/{world.InB}/header"), Ct))
        {
            await ExpectErrorAsync(outside, HttpStatusCode.NotFound, "xodim.topilmadi");
        }

        using (var unknown = await scoped.GetAsync(Path($"/employees/{Guid.NewGuid()}/header"), Ct))
        {
            await ExpectErrorAsync(unknown, HttpStatusCode.NotFound, "xodim.topilmadi");
        }
    }

    [Fact]
    public async Task Employee_header_is_empty_handed_without_an_assignment_and_open_to_hr_everywhere()
    {
        var world = await SeedWorldAsync();

        using (var unassigned = Client((await TestAccounts.CreateAsync(_factory, ScopedPermissions)).AccessToken))
        using (var response = await unassigned.GetAsync(Path($"/employees/{world.InA1}/header"), Ct))
        {
            await ExpectErrorAsync(response, HttpStatusCode.NotFound, "xodim.topilmadi");
        }

        using var hr = await ClientAsync(HrManagerPermissions);
        using var open = await hr.GetAsync(Path($"/employees/{world.InB}/header"), Ct);
        open.StatusCode.ShouldBe(HttpStatusCode.OK, await TextAsync(open));
    }

    [Fact]
    public async Task Employee_header_requires_attendance_view_and_a_staff_right()
    {
        var world = await SeedWorldAsync();

        using var withoutAttendance = await ClientAsync(Permissions.EmployeesView);
        using (var response = await withoutAttendance.GetAsync(Path($"/employees/{world.InA1}/header"), Ct))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using var withoutStaff = await ClientAsync(Permissions.AttendanceView);
        using (var response = await withoutStaff.GetAsync(Path($"/employees/{world.InA1}/header"), Ct))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using var anonymous = _factory.CreateClient();
        using var unauthorized = await anonymous.GetAsync(Path($"/employees/{world.InA1}/header"), Ct);
        unauthorized.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/employees/{0}")]
    [InlineData("/api/employees/{0}/documents")]
    [InlineData("/api/employees/{0}/timeline")]
    [InlineData("/api/employees/{0}/app-access")]
    public async Task Personal_data_stays_closed_even_for_someone_in_the_scope(string template)
    {
        var world = await SeedWorldAsync();
        using var scoped = await ScopedClientAsync(world.DepartmentA);

        var url = string.Format(CultureInfo.InvariantCulture, template, world.InA1);
        using var response = await scoped.GetAsync(new Uri(url, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, url);
    }

    [Fact]
    public async Task Attendance_data_of_the_page_holds_no_pinfl_or_phone()
    {
        var world = await SeedWorldAsync();
        using var scoped = await ScopedClientAsync(world.DepartmentA);

        var today = DateTimeOffset.UtcNow.ToOffset(Tashkent);
        var day = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        string[] urls =
        [
            $"/employees/{world.InA1}/header",
            $"/timesheet?year={today.Year}&month={today.Month}&employeeId={world.InA1}&page=1&pageSize=1",
            $"/passes?from={day}&to={day}&employeeId={world.InA1}&page=1&pageSize=50",
            $"/employees/{world.InA1}/days/{day}/passes",
        ];

        foreach (var url in urls)
        {
            using var response = await scoped.GetAsync(Path(url), Ct);
            var body = await TextAsync(response);

            response.StatusCode.ShouldBe(HttpStatusCode.OK, $"{url}: {body}");
            body.ShouldNotContain(world.PinflA1, Case.Sensitive, url);
            body.ShouldNotContain(world.PhoneA1, Case.Sensitive, url);
        }
    }

    // ── other roles ───────────────────────────────────────────────

    [Fact]
    public async Task Hr_manager_is_unaffected()
    {
        var world = await SeedWorldAsync();
        using var hr = await ClientAsync(HrManagerPermissions);
        using var editor = await ClientAsync(Permissions.EmployeesEdit);

        using var added = await AddPassAsync(hr, world.InB);
        added.StatusCode.ShouldBe(HttpStatusCode.OK, await TextAsync(added));

        using var deleted = await hr.DeleteAsync(Path($"/manual-passes/{(await JsonAsync(added)).GetProperty("id").GetGuid()}"), Ct);
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent, await TextAsync(deleted));

        (await LookupAsync(hr, $"search={world.Mark}")).Select(row => row.GetProperty("id").GetGuid())
            .ShouldBe([world.InA, world.InA1, world.InB], ignoreOrder: true);

        await UploadPhotoAsync(editor, world.InB);

        using var photo = await hr.GetAsync(Path($"/employees/{world.InB}/photo"), Ct);
        photo.StatusCode.ShouldBe(HttpStatusCode.OK, await TextAsync(photo));
    }

    [Fact]
    public async Task Employee_routes_require_attendance_view()
    {
        var world = await SeedWorldAsync();
        using var other = await ClientAsync(Permissions.EmployeesView);

        using var lookup = await other.GetAsync(Path("/employees/lookup"), Ct);
        lookup.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var photo = await other.GetAsync(Path($"/employees/{world.InA1}/photo"), Ct);
        photo.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var anonymous = _factory.CreateClient();
        using var unauthorized = await anonymous.GetAsync(Path("/employees/lookup"), Ct);
        unauthorized.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Employee_routes_are_closed_to_attendance_view_without_staff_access()
    {
        var world = await SeedWorldAsync();

        using var viewerOnly = await ClientAsync(Permissions.AttendanceView);

        using (var lookup = await viewerOnly.GetAsync(Path("/employees/lookup"), Ct))
        {
            await ExpectErrorAsync(lookup, HttpStatusCode.Forbidden, "auth.ruxsat_yoq");
        }

        using (var photo = await viewerOnly.GetAsync(Path($"/employees/{world.InA1}/photo"), Ct))
        {
            await ExpectErrorAsync(photo, HttpStatusCode.Forbidden, "auth.ruxsat_yoq");
        }

        using var staffReader = await ClientAsync(Permissions.AttendanceView, Permissions.EmployeesView);

        using (var lookup = await staffReader.GetAsync(Path("/employees/lookup"), Ct))
        {
            lookup.StatusCode.ShouldBe(HttpStatusCode.OK, await TextAsync(lookup));
        }

        using (var photo = await staffReader.GetAsync(Path($"/employees/{world.InA1}/photo"), Ct))
        {
            await ExpectErrorAsync(photo, HttpStatusCode.NotFound, "xodim.rasm_yoq");
        }

        using var scoped = await ScopedClientAsync(world.DepartmentA);

        using (var lookup = await scoped.GetAsync(Path("/employees/lookup"), Ct))
        {
            lookup.StatusCode.ShouldBe(HttpStatusCode.OK, await TextAsync(lookup));
        }
    }

    // ── own record ────────────────────────────────────────────────

    [Fact]
    public async Task Scoped_user_cannot_add_or_delete_passes_on_their_own_employee_record()
    {
        var world = await SeedWorldAsync();
        var account = await TestAccounts.CreateAsync(_factory, ScopedPermissions);
        await AssignAsync(account.UserId, world.DepartmentA);
        await LinkToEmployeeAsync(account.UserId, world.InA1);

        using var scoped = Client(account.AccessToken);
        using var hr = await ClientAsync(HrManagerPermissions);

        using (var added = await AddPassAsync(scoped, world.InA1))
        {
            await ExpectErrorAsync(added, HttpStatusCode.Forbidden, "auth.ruxsat_yoq");
        }

        (await CountRowsAsync(world.InA1)).Passes.ShouldBe(0);

        // Colleagues in the same branch are the point of the role.
        using (var colleague = await AddPassAsync(scoped, world.InA))
        {
            colleague.StatusCode.ShouldBe(HttpStatusCode.OK, await TextAsync(colleague));
        }

        // A pass HR put on their record is theirs to live with, too.
        using var byHr = await AddPassAsync(hr, world.InA1);
        byHr.StatusCode.ShouldBe(HttpStatusCode.OK, await TextAsync(byHr));
        var passId = (await JsonAsync(byHr)).GetProperty("id").GetGuid();

        using (var deleted = await scoped.DeleteAsync(Path($"/manual-passes/{passId}"), Ct))
        {
            await ExpectErrorAsync(deleted, HttpStatusCode.Forbidden, "auth.ruxsat_yoq");
        }

        (await CountRowsAsync(world.InA1)).ManualPasses.ShouldBe(1);
    }

    [Fact]
    public async Task Hr_may_add_passes_on_their_own_employee_record()
    {
        var world = await SeedWorldAsync();
        var account = await TestAccounts.CreateAsync(_factory, HrManagerPermissions);
        await LinkToEmployeeAsync(account.UserId, world.InA1);

        using var hr = Client(account.AccessToken);
        using var added = await AddPassAsync(hr, world.InA1);

        added.StatusCode.ShouldBe(HttpStatusCode.OK, await TextAsync(added));
    }

    // ── department on the date ────────────────────────────────────

    [Fact]
    public async Task Adding_a_pass_needs_the_department_on_that_date_to_be_in_scope()
    {
        var world = await SeedWorldAsync();
        var movedOn = Today().AddDays(-10);
        var moved = await MovedIntoDepartmentAAsync(world, movedOn);

        using var scoped = await ScopedClientAsync(world.DepartmentA);
        using var hr = await ClientAsync(HrManagerPermissions);

        using (var before = await AddPassAsync(scoped, moved, movedOn.AddDays(-1)))
        {
            await ExpectErrorAsync(before, HttpStatusCode.NotFound, "davomat.xodim_topilmadi");
        }

        (await CountRowsAsync(moved)).Passes.ShouldBe(0);

        using (var after = await AddPassAsync(scoped, moved, movedOn))
        {
            after.StatusCode.ShouldBe(HttpStatusCode.OK, await TextAsync(after));
        }

        using var byHr = await AddPassAsync(hr, moved, movedOn.AddDays(-1));
        byHr.StatusCode.ShouldBe(HttpStatusCode.OK, await TextAsync(byHr));
    }

    // ── age ───────────────────────────────────────────────────────

    [Fact]
    public async Task Scoped_user_deletes_within_the_window_of_adding_and_hr_has_no_limit()
    {
        var world = await SeedWorldAsync();
        using var scoped = await ScopedClientAsync(world.DepartmentA);
        using var hr = await ClientAsync(HrManagerPermissions);

        var tooOld = await ManualPassAsync(world.InA1, Today().AddDays(-AttendanceRules.MaxRecomputeDays - 2));
        var oldest = await ManualPassAsync(world.InA1, Today().AddDays(-AttendanceRules.MaxRecomputeDays + 1));

        using (var refused = await scoped.DeleteAsync(Path($"/manual-passes/{tooOld}"), Ct))
        {
            await ExpectErrorAsync(refused, HttpStatusCode.BadRequest, "davomat.belgi_juda_eski");
        }

        (await CountRowsAsync(world.InA1)).ManualPasses.ShouldBe(2);

        using (var allowed = await scoped.DeleteAsync(Path($"/manual-passes/{oldest}"), Ct))
        {
            allowed.StatusCode.ShouldBe(HttpStatusCode.NoContent, await TextAsync(allowed));
        }

        using (var byHr = await hr.DeleteAsync(Path($"/manual-passes/{tooOld}"), Ct))
        {
            byHr.StatusCode.ShouldBe(HttpStatusCode.NoContent, await TextAsync(byHr));
        }

        (await CountRowsAsync(world.InA1)).ManualPasses.ShouldBe(0);
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
        await context.SaveChangesAsync(Ct);
    }

    private async Task LinkToEmployeeAsync(Guid userId, Guid employeeId)
    {
        await using var serviceScope = _factory.Services.CreateAsyncScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await context.Users.SingleAsync(row => row.Id == userId, Ct);
        user.LinkToEmployee(employeeId);
        await context.SaveChangesAsync(Ct);
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

    private static DateOnly Today() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(Tashkent).DateTime);

    private static Task<HttpResponseMessage> AddPassAsync(HttpClient client, Guid employee, DateOnly? date = null)
        => client.PostAsJsonAsync(
            Path($"/employees/{employee}/manual-passes"),
            new
            {
                date = (date ?? Today().AddDays(-2)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                time = "18:00:00",
                direction = "Out",
                note = "Terminal did not read",
            },
            Ct);

    private static async Task<List<JsonElement>> LookupAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync(Path($"/employees/lookup?{query}"), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await TextAsync(response));

        return [.. (await JsonAsync(response)).EnumerateArray()];
    }

    private static async Task UploadPhotoAsync(HttpClient editor, Guid employee)
    {
        using var form = new MultipartFormDataContent();
        using var part = new ByteArrayContent(TestImages.PngYasa(40, 40));
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(part, "file", "photo.png");

        using var response = await editor.PostAsync(new Uri($"/api/employees/{employee}/photo", UriKind.Relative), form, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await TextAsync(response));
    }

    /// <summary>A pass from the mobile line, i.e. not HR manual. <c>null</c> leaves it unmatched.</summary>
    /// <summary>Worked in department B until the day before <paramref name="movedOn"/>, in A since.</summary>
    private async Task<Guid> MovedIntoDepartmentAAsync(World world, DateOnly movedOn)
    {
        await using var serviceScope = _factory.Services.CreateAsyncScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var inB = await context.Set<Employment>().AsNoTracking().SingleAsync(row => row.EmployeeId == world.InB, Ct);

        var person = Employee.Create(FullName.Create("Qamrovov", "Xodim", world.Mark + "Moved").Value, Today().AddYears(-30)).Value;

        var before = Employment.Hire(person.Id, inB.LegalEntityId, new DateOnly(2000, 1, 1), inB.DepartmentId, inB.PositionId).Value;
        before.Terminate(movedOn.AddDays(-1), reasonId: null).IsSuccess.ShouldBeTrue();

        var after = Employment.Hire(person.Id, inB.LegalEntityId, movedOn, world.DepartmentA, inB.PositionId).Value;

        context.AddRange(person, before, after);
        await context.SaveChangesAsync(Ct);

        return person.Id;
    }

    /// <summary>Written directly: the add route refuses the dates the age test needs.</summary>
    private async Task<Guid> ManualPassAsync(Guid employee, DateOnly date)
    {
        await using var serviceScope = _factory.Services.CreateAsyncScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var occurredAt = new DateTimeOffset(date.ToDateTime(new TimeOnly(9, 0)), Tashkent);
        var pass = AttendancePass.RecordByHr(employee, occurredAt, DateTimeOffset.UtcNow, PassDirection.In).Value;
        var evidence = AttendanceManualPass.Create(pass.Id, employee, pass.OccurredAt, pass.Direction, "Seeded").Value;

        context.AddRange(pass, evidence);
        await context.SaveChangesAsync(Ct);

        return pass.Id;
    }

    private async Task<Guid> TerminalPassAsync(Guid? employee)
    {
        await using var serviceScope = _factory.Services.CreateAsyncScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var occurredAt = new DateTimeOffset(Today().AddDays(-2).ToDateTime(new TimeOnly(9, 0)), Tashkent);
        var pass = AttendancePass.Record(
            AttendanceDevice.MobileAppId,
            AttendanceIdentityProvider.Mobile,
            (employee ?? Guid.NewGuid()).ToString(),
            occurredAt,
            occurredAt.ToUniversalTime(),
            "scope-write:" + Guid.NewGuid().ToString("N"),
            PassDirection.In,
            DirectionSource.Employee,
            PassMethod.Mobile).Value;

        if (employee is { } id)
        {
            pass.Resolve(id, occurredAt.ToUniversalTime());
        }

        context.Add(pass);
        await context.SaveChangesAsync(Ct);

        return pass.Id;
    }

    private async Task<Rows> CountRowsAsync(Guid employee)
    {
        await using var serviceScope = _factory.Services.CreateAsyncScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();

        return new Rows(
            await context.AttendancePasses.CountAsync(pass => pass.ResolvedEmployeeId == employee, Ct),
            await context.Set<AttendanceManualPass>().CountAsync(pass => pass.EmployeeId == employee, Ct),
            await context.AttendanceDays.CountAsync(day => day.EmployeeId == employee, Ct));
    }

    private static async Task ExpectErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await TextAsync(response);

        response.StatusCode.ShouldBe(status, body);
        JsonDocument.Parse(body).RootElement.GetProperty("title").GetString().ShouldBe(code, body);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await TextAsync(response)).RootElement.Clone();

    private static Task<string> TextAsync(HttpResponseMessage response) => response.Content.ReadAsStringAsync(Ct);

    private async Task<World> SeedWorldAsync(int extraInA = 0)
    {
        await using var serviceScope = _factory.Services.CreateAsyncScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var mark = "m" + Guid.NewGuid().ToString("N")[..8];

        var firm = LegalEntity.Create("Scope firm " + mark).Value;
        var departmentA = Department.Create("Scope A " + mark).Value;
        var departmentA1 = Department.Create("Scope A1 " + mark, departmentA.Id).Value;
        var departmentB = Department.Create("Scope B " + mark).Value;
        var position = Position.Create("Scope position " + mark).Value;
        context.AddRange(firm, departmentA, departmentA1, departmentB, position);

        var birthDate = Today().AddYears(-30);

        Employee Person(string suffix, string pinfl, string phone, Guid department, DateOnly? ended = null)
        {
            var person = Employee.Create(FullName.Create("Qamrovov", "Xodim", mark + suffix).Value, birthDate).Value;
            person.SetPinfl(Pinfl.FromTrusted(pinfl)).IsSuccess.ShouldBeTrue();
            person.SetPhones(PhoneNumber.Create(phone).Value);

            var employment = Employment.Hire(person.Id, firm.Id, new DateOnly(2000, 1, 1), department, position.Id).Value;
            if (ended is { } date)
            {
                employment.Terminate(date, reasonId: null).IsSuccess.ShouldBeTrue();
            }

            context.AddRange(person, employment);

            return person;
        }

        var pinflA1 = PinflFor(birthDate);
        var phoneA1 = "90" + RandomDigits(7);

        var inA = Person("A", PinflFor(birthDate), "90" + RandomDigits(7), departmentA.Id);
        var inA1 = Person("A1", pinflA1, phoneA1, departmentA1.Id);
        var inB = Person("B", PinflFor(birthDate), "90" + RandomDigits(7), departmentB.Id);

        // On past timesheets, but nobody a picker should offer.
        Person("Former", PinflFor(birthDate), "90" + RandomDigits(7), departmentA.Id, new DateOnly(2010, 1, 1));

        for (var i = 0; i < extraInA; i++)
        {
            Person("Extra" + i.ToString(CultureInfo.InvariantCulture), PinflFor(birthDate), "90" + RandomDigits(7), departmentA.Id);
        }

        await context.SaveChangesAsync(Ct);

        return new World(mark, departmentA.Id, inA.Id, inA1.Id, inB.Id, pinflA1, phoneA1);
    }

    private static string PinflFor(DateOnly birthDate)
    {
        var century = birthDate.Year < 2000 ? "3" : "5";

        return century + birthDate.ToString("ddMMyy", CultureInfo.InvariantCulture) + RandomDigits(7);
    }

    private static string RandomDigits(int length)
        => string.Concat(Enumerable.Range(0, length).Select(_ => RandomNumberGenerator.GetInt32(1, 10).ToString(CultureInfo.InvariantCulture)));

    private sealed record Rows(int Passes, int ManualPasses, int Days);

    private sealed record World(
        string Mark,
        Guid DepartmentA,
        Guid InA,
        Guid InA1,
        Guid InB,
        string PinflA1,
        string PhoneA1);
}
