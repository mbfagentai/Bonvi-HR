using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Bonvi.Api.Common;
using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests;

/// <summary>
/// «Davomat mas'uli»: the guard, the assignment endpoints and <see cref="IAttendanceScope"/>.
/// </summary>
/// <remarks>
/// The route sweep walks the real endpoint table, so a route added tomorrow is covered by
/// this test without anyone remembering it. Whether a route SHOULD be open is written here a
/// second time on purpose: changing the guard's allowlist has to fail this file.
/// </remarks>
public sealed class AttendanceScopeApiTests : IClassFixture<BonviApiFactory>
{
    private const string ManualPassCreateRoute = "/api/attendance/employees/{employeeId:guid}/manual-passes";
    private const string ManualPassDeleteRoute = "/api/attendance/manual-passes/{passId:guid}";

    private static readonly string[] ScopedPermissions = SystemRoles.Defaults[SystemRoles.AttendanceResponsible].Permissions;

    private readonly BonviApiFactory _factory;

    public AttendanceScopeApiTests(BonviApiFactory factory) => _factory = factory;

    // ── guard ─────────────────────────────────────────────────────

    [Fact]
    public async Task Scoped_user_gets_403_on_every_route_outside_the_allowlist()
    {
        var ct = TestContext.Current.CancellationToken;
        var scoped = await ClientAsync(ScopedPermissions);

        var routes = AllRoutes();

        // A broken enumeration must not pass as «nothing was refused».
        routes.Count.ShouldBeGreaterThan(300);
        routes.ShouldContain(("POST", ManualPassCreateRoute), "the allowlist names a route that no longer exists");
        routes.ShouldContain(("DELETE", ManualPassDeleteRoute), "the allowlist names a route that no longer exists");

        var failures = new List<string>();
        var checkedRoutes = 0;
        var attendanceWrites = 0;

        foreach (var (method, route) in routes)
        {
            var path = Expand(route);

            if (IsOpenToScopedUser(method, route))
            {
                continue;
            }

            checkedRoutes++;
            attendanceWrites += route.StartsWith("/api/attendance", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            var response = await scoped.SendAsync(request, ct);

            // Strict limiter policies (exports: 5 a minute per user) answer ahead of the guard.
            // A new account has its own budget, so the guard's own answer is still what is checked.
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                response.Dispose();
                scoped.Dispose();
                scoped = await ClientAsync(ScopedPermissions);

                using var retry = new HttpRequestMessage(new HttpMethod(method), path);
                response = await scoped.SendAsync(retry, ct);
            }

            using var answer = response;

            // With MCP switched off its own middleware answers first; the user is out either way.
            if (route.StartsWith("/mcp", StringComparison.OrdinalIgnoreCase)
                && response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                continue;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            if (response.StatusCode != HttpStatusCode.Forbidden || !body.Contains("auth.ruxsat_yoq", StringComparison.Ordinal))
            {
                failures.Add($"{method} {route} → {(int)response.StatusCode}");
            }
        }

        scoped.Dispose();

        failures.ShouldBeEmpty();
        checkedRoutes.ShouldBeGreaterThan(250);
        attendanceWrites.ShouldBeGreaterThan(25, "absence and schedule request writes must be among the refused");
    }

    [Theory]
    [InlineData("POST", "/api/attendance/requests")]
    [InlineData("POST", "/api/attendance/schedule-requests")]
    [InlineData("POST", "/api/attendance/recompute")]
    [InlineData("post", "/API/ATTENDANCE/RECOMPUTE")]
    [InlineData("POST", "/api/attendance/no-such-route")]
    [InlineData("GET", "/api/attendance/work-sites")]
    [InlineData("GET", "/API/ATTENDANCE/WORK-SITES/")]
    [InlineData("GET", "/API/USERS")]
    [InlineData("GET", "/api/users/")]
    [InlineData("GET", "/api/authors")]
    [InlineData("GET", "/no-such-route")]
    [InlineData("GET", "/api/auth%2F..%2Fusers")]
    public async Task Scoped_user_cannot_slip_past_the_guard(string method, string path)
    {
        using var scoped = await ClientAsync(ScopedPermissions);
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));

        using var response = await scoped.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Refusal_is_recorded_in_the_audit_journal()
    {
        var ct = TestContext.Current.CancellationToken;
        var account = await TestAccounts.CreateAsync(_factory, ScopedPermissions);
        using var scoped = Client(account.AccessToken);

        (await scoped.GetAsync(new Uri("/api/employees", UriKind.Relative), ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await using var serviceScope = _factory.Services.CreateAsyncScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rows = await context.AuditLogs.AsNoTracking()
            .Where(row => row.Action == AuditAction.PermissionDenied && row.UserId == account.UserId)
            .ToListAsync(ct);

        rows.ShouldHaveSingleItem().NewValues.ShouldNotBeNull().ShouldContain("/api/employees");
    }

    [Fact]
    public async Task Scoped_user_reaches_attendance_reads_auth_and_notifications()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scoped = await ClientAsync(ScopedPermissions);

        (await scoped.GetAsync(new Uri("/api/attendance/schedules", UriKind.Relative), ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await scoped.GetAsync(new Uri("/api/notifications", UriKind.Relative), ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var permissions = await MyPermissionsAsync(scoped, ct);
        permissions.ShouldContain(Permissions.AttendanceScoped, "the web client decides its navigation by this");
    }

    [Fact]
    public async Task Scoped_user_may_add_and_delete_manual_passes()
    {
        var ct = TestContext.Current.CancellationToken;
        var account = await TestAccounts.CreateAsync(_factory, ScopedPermissions);
        using var scoped = Client(account.AccessToken);

        var department = await DepartmentAsync(null);
        await AssignAsync(account.UserId, department);
        var employee = await EmployeeAsync(new EmploymentRow(department, new DateOnly(2025, 1, 1), null));

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(-1)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        using var created = await scoped.PostAsJsonAsync(
            new Uri($"/api/attendance/employees/{employee}/manual-passes", UriKind.Relative),
            new { date, time = "09:00:00", direction = "In", note = "Terminal was down" },
            ct);
        created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync(ct));

        var passId = JsonDocument.Parse(await created.Content.ReadAsStringAsync(ct)).RootElement.GetProperty("id").GetGuid();

        using var deleted = await scoped.DeleteAsync(new Uri($"/api/attendance/manual-passes/{passId}", UriKind.Relative), ct);
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Notifications_are_empty_for_a_scoped_user_even_if_the_role_could_read_them()
    {
        var ct = TestContext.Current.CancellationToken;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
        await EmployeeAsync(new EmploymentRow(null, new DateOnly(2025, 1, 1), null), birthDate: new DateOnly(1990, today.Month, today.Day == 29 && today.Month == 2 ? 28 : today.Day));

        // The control proves the list has something in it for a user with the same permission.
        using var control = await ClientAsync(Permissions.EmployeesView);
        (await NotificationCountAsync(control, ct)).ShouldBeGreaterThan(0);

        using var role = await ClientAsync(ScopedPermissions);
        (await NotificationCountAsync(role, ct)).ShouldBe(0);

        using var widened = await ClientAsync([.. ScopedPermissions, Permissions.EmployeesView]);
        (await NotificationCountAsync(widened, ct)).ShouldBe(0);
    }

    [Fact]
    public async Task Super_admin_is_not_blocked_and_is_not_told_about_the_marker()
    {
        var ct = TestContext.Current.CancellationToken;

        using var anonymous = _factory.CreateClient();
        using var login = await anonymous.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { login = BonviApiFactory.TestAdminLogin, password = BonviApiFactory.TestAdminPassword },
            ct);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync(ct)).RootElement;
        using var admin = Client(body.GetProperty("accessToken").GetString()!);

        await using (var serviceScope = _factory.Services.CreateAsyncScope())
        {
            var context = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var role = await context.Roles.AsNoTracking().SingleAsync(r => r.Code == SystemRoles.SuperAdmin, ct);
            role.Permissions.ShouldContain(Permissions.AttendanceScoped, "the seeder keeps the owner equal to Permissions.All");
        }

        body.GetRawText().ShouldNotContain(Permissions.AttendanceScoped);

        var permissions = await MyPermissionsAsync(admin, ct);
        permissions.ShouldContain(Permissions.AttendanceManage);
        permissions.ShouldNotContain(Permissions.AttendanceScoped);

        (await admin.GetAsync(new Uri("/api/users", UriKind.Relative), ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.GetAsync(new Uri("/api/employees", UriKind.Relative), ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Hr_manager_is_not_blocked_and_marker_with_manage_is_not_scoped()
    {
        var ct = TestContext.Current.CancellationToken;

        using var hr = await ClientAsync(SystemRoles.Defaults[SystemRoles.HrManager].Permissions);
        (await hr.GetAsync(new Uri("/api/employees", UriKind.Relative), ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Reaches the handler: an empty body is a validation error, not a refusal.
        using var write = await hr.PostAsJsonAsync(new Uri("/api/attendance/requests", UriKind.Relative), new { }, ct);
        write.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);

        using var both = await ClientAsync(Permissions.AttendanceScoped, Permissions.AttendanceManage, Permissions.EmployeesView);
        (await both.GetAsync(new Uri("/api/employees", UriKind.Relative), ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await MyPermissionsAsync(both, ct)).ShouldNotContain(Permissions.AttendanceScoped);
    }

    // ── assignment endpoints ──────────────────────────────────────

    [Fact]
    public async Task Attendance_scope_routes_require_users_manage()
    {
        var ct = TestContext.Current.CancellationToken;
        var target = await TestAccounts.CreateAsync(_factory, ScopedPermissions);
        var uri = new Uri($"/api/users/{target.UserId}/attendance-scope", UriKind.Relative);
        var body = new { departmentIds = Array.Empty<Guid>() };

        using var anonymous = _factory.CreateClient();
        (await anonymous.GetAsync(uri, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PutAsJsonAsync(uri, body, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var withoutPermission = await ClientAsync(Permissions.EmployeesView, Permissions.AttendanceManage);
        (await withoutPermission.GetAsync(uri, ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await withoutPermission.PutAsJsonAsync(uri, body, ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var manager = await ClientAsync(ManagerPermissions);
        (await manager.GetAsync(uri, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Put_replaces_the_list_and_audits_one_row_per_real_change()
    {
        var ct = TestContext.Current.CancellationToken;
        var target = await TestAccounts.CreateAsync(_factory, ScopedPermissions);
        using var manager = await ClientAsync(ManagerPermissions);
        var uri = new Uri($"/api/users/{target.UserId}/attendance-scope", UriKind.Relative);

        var first = await DepartmentAsync(null);
        var second = await DepartmentAsync(null);

        (await ReadScopeAsync(manager, uri, ct)).ShouldBeEmpty();

        // Duplicates collapse.
        using (var put = await manager.PutAsJsonAsync(uri, new { departmentIds = new[] { first, first, second } }, ct))
        {
            put.StatusCode.ShouldBe(HttpStatusCode.OK, await put.Content.ReadAsStringAsync(ct));
        }

        (await ReadScopeAsync(manager, uri, ct)).ShouldBe([first, second], ignoreOrder: true);
        (await ScopeAuditAsync(target.UserId, ct)).Count.ShouldBe(1);

        // The same list again is not a change.
        (await manager.PutAsJsonAsync(uri, new { departmentIds = new[] { second, first } }, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ScopeAuditAsync(target.UserId, ct)).Count.ShouldBe(1);

        (await manager.PutAsJsonAsync(uri, new { departmentIds = new[] { second } }, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadScopeAsync(manager, uri, ct)).ShouldBe([second]);

        var rows = await ScopeAuditAsync(target.UserId, ct);
        rows.Count.ShouldBe(2);

        var latest = rows.OrderBy(row => row.OccurredAt).Last();
        latest.OldValues.ShouldNotBeNull().ShouldContain(first.ToString());
        latest.NewValues.ShouldNotBeNull().ShouldNotContain(first.ToString());
        latest.NewValues.ShouldContain(second.ToString());

        // Fails closed from here: an empty list is allowed and means nothing visible.
        (await manager.PutAsJsonAsync(uri, new { departmentIds = Array.Empty<Guid>() }, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadScopeAsync(manager, uri, ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Put_rejects_unknown_account_unknown_department_and_a_target_out_of_reach()
    {
        var ct = TestContext.Current.CancellationToken;
        var target = await TestAccounts.CreateAsync(_factory, ScopedPermissions);
        using var manager = await ClientAsync(ManagerPermissions);
        var department = await DepartmentAsync(null);

        await ExpectErrorAsync(
            await manager.GetAsync(new Uri($"/api/users/{Guid.NewGuid()}/attendance-scope", UriKind.Relative), ct),
            HttpStatusCode.NotFound,
            "foydalanuvchi.topilmadi");

        await ExpectErrorAsync(
            await manager.PutAsJsonAsync(
                new Uri($"/api/users/{Guid.NewGuid()}/attendance-scope", UriKind.Relative),
                new { departmentIds = new[] { department } },
                ct),
            HttpStatusCode.NotFound,
            "foydalanuvchi.topilmadi");

        await ExpectErrorAsync(
            await manager.PutAsJsonAsync(
                new Uri($"/api/users/{target.UserId}/attendance-scope", UriKind.Relative),
                new { departmentIds = new[] { department, Guid.NewGuid() } },
                ct),
            HttpStatusCode.NotFound,
            "bolim.topilmadi");

        // Nothing partial was saved.
        await using (var serviceScope = _factory.Services.CreateAsyncScope())
        {
            var context = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await context.UserAttendanceScopes.CountAsync(row => row.UserId == target.UserId, ct)).ShouldBe(0);
        }

        // A role broader than the manager's own is out of reach, as for any account change.
        var broader = await TestAccounts.CreateAsync(_factory, [.. ManagerPermissions, Permissions.AttendanceManage]);
        await ExpectErrorAsync(
            await manager.PutAsJsonAsync(
                new Uri($"/api/users/{broader.UserId}/attendance-scope", UriKind.Relative),
                new { departmentIds = new[] { department } },
                ct),
            HttpStatusCode.Forbidden,
            "foydalanuvchi.rolga_haqqi_yoq");
    }

    [Fact]
    public async Task Manager_whose_role_predates_the_marker_can_assign_the_seeded_role()
    {
        var ct = TestContext.Current.CancellationToken;
        using var manager = await ClientAsync([Permissions.UsersManage, .. ScopedPermissions.Where(code => code != Permissions.AttendanceScoped)]);

        using var created = await manager.PostAsJsonAsync(
            new Uri("/api/users", UriKind.Relative),
            new
            {
                login = "97" + Random.Shared.Next(1_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture),
                lastName = "Mas'ulov",
                firstName = "Zavod",
                password = TestAccounts.Password,
                roleCode = SystemRoles.AttendanceResponsible,
            },
            ct);

        created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task Role_change_away_from_the_marker_clears_the_assignment_and_audits_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var target = await TestAccounts.CreateAsync(_factory, ScopedPermissions);
        var otherScoped = await TestAccounts.CreateAsync(_factory, ScopedPermissions);
        var plain = await TestAccounts.CreateAsync(_factory, Permissions.AttendanceView);
        using var manager = await ClientAsync(ManagerPermissions);
        var scopeUri = new Uri($"/api/users/{target.UserId}/attendance-scope", UriKind.Relative);

        var department = await DepartmentAsync(null);
        await AssignAsync(target.UserId, department);

        async Task SetRoleAsync(string roleCode)
        {
            using var response = await manager.PutAsJsonAsync(
                new Uri($"/api/users/{target.UserId}", UriKind.Relative),
                new { login = target.Login, lastName = "Синов", firstName = "Ҳисоб", roleCode },
                ct);

            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(ct));
        }

        async Task<List<AuditLog>> ScopeLinesAsync()
            => [.. (await ScopeAuditAsync(target.UserId, ct)).Where(row => row.OldValues?.Contains("AttendanceScopeDepartmentIds", StringComparison.Ordinal) == true)];

        // A name edit under the same role, and a move to another scoped role, keep it.
        await SetRoleAsync(target.RoleCode);
        (await ReadScopeAsync(manager, scopeUri, ct)).ShouldBe([department]);

        await SetRoleAsync(otherScoped.RoleCode);
        (await ReadScopeAsync(manager, scopeUri, ct)).ShouldBe([department]);
        (await ScopeLinesAsync()).ShouldBeEmpty();

        await SetRoleAsync(plain.RoleCode);
        (await ReadScopeAsync(manager, scopeUri, ct)).ShouldBeEmpty();

        var line = (await ScopeLinesAsync()).ShouldHaveSingleItem();
        line.OldValues.ShouldNotBeNull().ShouldContain(department.ToString());
        line.NewValues.ShouldNotBeNull().ShouldNotContain(department.ToString());

        // Being given the role again does not bring the old departments back.
        await SetRoleAsync(target.RoleCode);
        (await ReadScopeAsync(manager, scopeUri, ct)).ShouldBeEmpty();
        (await ScopeLinesAsync()).Count.ShouldBe(1);
    }

    // ── IAttendanceScope ──────────────────────────────────────────

    [Fact]
    public async Task No_http_context_and_marker_with_manage_resolve_to_unrestricted()
    {
        var ct = TestContext.Current.CancellationToken;

        await using (var job = _factory.Services.CreateAsyncScope())
        {
            var scope = job.ServiceProvider.GetRequiredService<IAttendanceScope>();

            scope.IsScoped.ShouldBeFalse();
            (await scope.AllowedDepartmentIdsAsync(ct)).ShouldBeNull();
            (await scope.EmployeeIdsInScopeAsync(ct)).ShouldBeNull();
            (await scope.CanAccessEmployeeAsync(Guid.NewGuid(), ct)).ShouldBeTrue();
        }

        await using var serviceScope = PrincipalScope(Guid.NewGuid(), Permissions.AttendanceScoped, Permissions.AttendanceManage);
        var manager = serviceScope.ServiceProvider.GetRequiredService<IAttendanceScope>();

        manager.IsScoped.ShouldBeFalse();
        (await manager.EmployeeIdsInScopeAsync(ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Scoped_user_without_assignment_sees_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var employee = await EmployeeAsync(new EmploymentRow(await DepartmentAsync(null), new DateOnly(2025, 1, 1), null));

        await using var serviceScope = PrincipalScope(Guid.NewGuid(), ScopedPermissions);
        var scope = serviceScope.ServiceProvider.GetRequiredService<IAttendanceScope>();

        scope.IsScoped.ShouldBeTrue();
        (await scope.AllowedDepartmentIdsAsync(ct)).ShouldNotBeNull().ShouldBeEmpty();
        (await scope.EmployeeIdsInScopeAsync(ct)).ShouldNotBeNull().ShouldBeEmpty();
        (await scope.CanAccessEmployeeAsync(employee, ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Scope_covers_descendants_and_follows_the_employee_department_today()
    {
        var ct = TestContext.Current.CancellationToken;
        var account = await TestAccounts.CreateAsync(_factory, ScopedPermissions);

        var factory = await DepartmentAsync(null);
        var workshop = await DepartmentAsync(factory);
        var elsewhere = await DepartmentAsync(null);
        await AssignAsync(account.UserId, factory);

        var old = new DateOnly(2020, 1, 1);
        var ended = new DateOnly(2021, 1, 1);
        var later = new DateOnly(2023, 1, 1);

        var inRoot = await EmployeeAsync(new EmploymentRow(factory, old, null));
        var inChild = await EmployeeAsync(new EmploymentRow(workshop, old, null));
        var outside = await EmployeeAsync(new EmploymentRow(elsewhere, old, null));
        var noDepartment = await EmployeeAsync(new EmploymentRow(null, old, null));
        var transferredOut = await EmployeeAsync(new EmploymentRow(factory, old, ended), new EmploymentRow(elsewhere, later, null));
        var transferredIn = await EmployeeAsync(new EmploymentRow(elsewhere, old, ended), new EmploymentRow(workshop, later, null));
        var former = await EmployeeAsync(new EmploymentRow(workshop, old, ended));
        var formerLeftElsewhere = await EmployeeAsync(new EmploymentRow(factory, old, ended), new EmploymentRow(elsewhere, later, later.AddYears(1)));

        await using var serviceScope = PrincipalScope(account.UserId, ScopedPermissions);
        var scope = serviceScope.ServiceProvider.GetRequiredService<IAttendanceScope>();

        (await scope.AllowedDepartmentIdsAsync(ct)).ShouldNotBeNull().ShouldBe([factory, workshop], ignoreOrder: true);

        var employees = await scope.EmployeeIdsInScopeAsync(ct);
        employees.ShouldNotBeNull();

        employees.ShouldContain(inRoot);
        employees.ShouldContain(inChild);
        employees.ShouldContain(transferredIn);
        employees.ShouldContain(former, "a former employee stays on past timesheets");
        employees.ShouldNotContain(outside);
        employees.ShouldNotContain(noDepartment);
        employees.ShouldNotContain(transferredOut, "the employment that counts is today's");
        employees.ShouldNotContain(formerLeftElsewhere, "the employment that counts is the latest ended one");

        (await scope.CanAccessEmployeeAsync(inChild, ct)).ShouldBeTrue();
        (await scope.CanAccessEmployeeAsync(outside, ct)).ShouldBeFalse();
    }

    // ── helpers ───────────────────────────────────────────────────

    /// <summary>Can manage users and is broader than the scoped role, without holding the marker.</summary>
    private static string[] ManagerPermissions =>
        [Permissions.UsersManage, Permissions.AttendanceView, Permissions.AttendanceRequestsManage, Permissions.ReportsExport];

    /// <summary>Every mapped method and route, in the form the guard compares against.</summary>
    private List<(string Method, string Route)> AllRoutes()
    {
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;

        return
        [
            .. endpoints
                .OfType<RouteEndpoint>()
                .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                    .Select(method => (Method: method, Route: "/" + endpoint.RoutePattern.RawText!.Trim('/'))))
                .Distinct(),
        ];
    }

    private static bool IsOpenToScopedUser(string method, string route)
    {
        if (route.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase)
            || route.StartsWith("/api/notifications", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!route.StartsWith("/api/attendance/", StringComparison.OrdinalIgnoreCase)
            || route.StartsWith("/api/attendance/work-sites", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return method is "GET" or "HEAD"
            || (method == "POST" && route == ManualPassCreateRoute)
            || (method == "DELETE" && route == ManualPassDeleteRoute);
    }

    private static Uri Expand(string route)
    {
        var pattern = RoutePatternFactory.Parse(route);

        var segments = pattern.PathSegments.Select(segment => string.Concat(segment.Parts.Select(part => part switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternSeparatorPart separator => separator.Content,
            RoutePatternParameterPart parameter => SampleValue(parameter),
            _ => string.Empty,
        })));

        return new Uri("/" + string.Join('/', segments), UriKind.Relative);
    }

    private static string SampleValue(RoutePatternParameterPart parameter)
    {
        var constraints = parameter.ParameterPolicies.Select(policy => policy.Content ?? string.Empty).ToList();

        if (constraints.Exists(constraint => constraint.StartsWith("guid", StringComparison.OrdinalIgnoreCase)))
        {
            return Guid.NewGuid().ToString();
        }

        return constraints.Exists(constraint => constraint.StartsWith("int", StringComparison.OrdinalIgnoreCase)
                || constraint.StartsWith("long", StringComparison.OrdinalIgnoreCase))
            ? "1"
            : "x";
    }

    private async Task<HttpClient> ClientAsync(params string[] permissions)
        => Client(await TestAccounts.TokenAsync(_factory, permissions));

    private HttpClient Client(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<List<string>> MyPermissionsAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(new Uri("/api/auth/me", UriKind.Relative), ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement;

        return [.. body.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()!)];
    }

    private static async Task<int> NotificationCountAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(new Uri("/api/notifications", UriKind.Relative), ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement;

        return body.GetProperty("items").GetArrayLength();
    }

    private static async Task<List<Guid>> ReadScopeAsync(HttpClient client, Uri uri, CancellationToken ct)
    {
        using var response = await client.GetAsync(uri, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement;

        return [.. body.GetProperty("departmentIds").EnumerateArray().Select(item => item.GetGuid())];
    }

    private static async Task ExpectErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(status, text);
            JsonDocument.Parse(text).RootElement.GetProperty("title").GetString().ShouldBe(code, text);
        }
    }

    private async Task<List<AuditLog>> ScopeAuditAsync(Guid userId, CancellationToken ct)
    {
        await using var serviceScope = _factory.Services.CreateAsyncScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = userId.ToString();

        return await context.AuditLogs.AsNoTracking()
            .Where(row => row.Action == AuditAction.Update && row.EntityName == nameof(User) && row.EntityId == id)
            .ToListAsync(ct);
    }

    private async Task<Guid> DepartmentAsync(Guid? parent)
    {
        await using var serviceScope = _factory.Services.CreateAsyncScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var department = Department.Create("Qamrov " + Guid.NewGuid().ToString("N")[..8], parent).Value;
        context.Add(department);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return department.Id;
    }

    private async Task AssignAsync(Guid userId, Guid departmentId)
    {
        await using var serviceScope = _factory.Services.CreateAsyncScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();

        context.UserAttendanceScopes.Add(new UserAttendanceScope(userId, departmentId));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private Task<Guid> EmployeeAsync(params EmploymentRow[] employments) => EmployeeAsync(null, employments);

    private async Task<Guid> EmployeeAsync(EmploymentRow employment, DateOnly birthDate)
        => await EmployeeAsync(birthDate, [employment]);

    private async Task<Guid> EmployeeAsync(DateOnly? birthDate, EmploymentRow[] employments)
    {
        var ct = TestContext.Current.CancellationToken;

        await using var serviceScope = _factory.Services.CreateAsyncScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mark = Guid.NewGuid().ToString("N")[..8];

        var firm = LegalEntity.Create("Qamrov MCHJ " + mark).Value;
        var employee = Employee.Create(FullName.Create("Qamrovov", "Xodim", mark).Value, birthDate).Value;
        context.AddRange(firm, employee);

        foreach (var row in employments)
        {
            var employment = Employment.Hire(employee.Id, firm.Id, row.Hired, row.Department).Value;

            if (row.Ended is { } ended)
            {
                employment.Terminate(ended, reasonId: null).IsSuccess.ShouldBeTrue();
            }

            context.Add(employment);
        }

        await context.SaveChangesAsync(ct);

        return employee.Id;
    }

    /// <summary>A service scope whose <c>ICurrentUser</c> sees the given claims, as a request's would.</summary>
    private AsyncServiceScope PrincipalScope(Guid userId, params string[] permissions)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        claims.AddRange(permissions.Select(permission => new Claim(CurrentUser.PermissionClaimType, permission)));

        _factory.Services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
        };

        return _factory.Services.CreateAsyncScope();
    }

    private sealed record EmploymentRow(Guid? Department, DateOnly Hired, DateOnly? Ended);
}
