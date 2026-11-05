using System.Collections.Concurrent;
using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Organization;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests.Employees;

/// <summary>
/// «Hujjatlari chala» end to end: real HTTP, real JSON, real PostgreSQL. The database is shared by the tests of the
/// class and the catalogue is global, so each test finds its own people by a name of its own and takes the document
/// kinds from the response.
/// </summary>
public sealed class EmployeeCompletenessApiTests : IClassFixture<BonviApiFactory>
{
    private const string Root = "/api/employees/completeness";
    private const string My = "/api/processes/my";
    private const string TartibKey = "employeesWithIncompleteFiles";

    private static readonly string[] OwnCodesOfAnEmptyFile =
        ["pinfl", "passport", "photo", "phone", "address", "orderNo"];

    private readonly BonviApiFactory _factory;
    private readonly CommandLog _log = new();

    public EmployeeCompletenessApiTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Day(int year, int month, int day) => new(year, month, day);

    // ────────────────────────────────────────────────────────────
    //  RBAC
    // ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(Root)]
    [InlineData(Root + "/summary")]
    public async Task Without_a_token_is_401_without_employees_view_is_403(string route)
    {
        using var anonymous = _factory.CreateClient();
        (await anonymous.GetAsync(new Uri(route, UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var salaryOnly = await ClientAsync(Permissions.SalaryView, Permissions.ProcessesView);
        (await salaryOnly.GetAsync(new Uri(route, UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var allowed = await ClientAsync(Permissions.EmployeesView);
        (await allowed.GetAsync(new Uri(route, UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Salary_exists_only_for_a_viewer_with_salary_view()
    {
        var name = Name();
        await HireAsync(name);

        using var plain = await ClientAsync(Permissions.EmployeesView);
        using var finance = await ClientAsync(Permissions.EmployeesView, Permissions.SalaryView);

        var plainRow = (await RowsAsync(plain, name)).ShouldHaveSingleItem();
        var financeRow = (await RowsAsync(finance, name)).ShouldHaveSingleItem();

        Codes(plainRow).ShouldNotContain("salary");
        Codes(financeRow).ShouldContain("salary");
        financeRow.GetProperty("total").GetInt32().ShouldBe(plainRow.GetProperty("total").GetInt32() + 1);
        financeRow.GetProperty("done").GetInt32().ShouldBe(plainRow.GetProperty("done").GetInt32());

        KindCodes(await GetAsync(plain, Root + "/summary")).ShouldNotContain("salary");
        KindCodes(await GetAsync(finance, Root + "/summary")).ShouldContain("salary");

        // Asking for it is the same mistake as asking for a kind that does not exist.
        (await plain.GetAsync(new Uri(Root + "?missing=salary", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await finance.GetAsync(new Uri(Root + "?missing=salary", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ────────────────────────────────────────────────────────────
    //  What counts as missing
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Each_field_is_reported_and_a_filled_one_leaves_the_list()
    {
        var name = Name();
        var employeeId = await HireAsync(name);

        using var client = await ClientAsync(Permissions.EmployeesView);

        var row = (await RowsAsync(client, name)).ShouldHaveSingleItem();
        Codes(row).ShouldBe(OwnCodesOfAnEmptyFile.Concat(Codes(row).Where(code => code.StartsWith("doc:", StringComparison.Ordinal))), ignoreOrder: false);
        row.GetProperty("employeeId").GetGuid().ShouldBe(employeeId);
        row.GetProperty("hasPhoto").GetBoolean().ShouldBeFalse();
        row.GetProperty("done").GetInt32().ShouldBe(row.GetProperty("total").GetInt32() - Codes(row).Count);

        await DbAsync(async db =>
        {
            var employee = await db.Employees.SingleAsync(item => item.Id == employeeId, Ct);
            employee.SetPassport(PassportSeries.Create(RandomPassport()).Value);
            employee.SetPhoto("photos/" + employeeId + ".jpg");
            employee.SetAddress("Toshkent");
            employee.SetPhones(PhoneNumber.Create(RandomPhone()).Value);
            return await db.SaveChangesAsync(Ct);
        });

        var after = (await RowsAsync(client, name)).ShouldHaveSingleItem();
        Codes(after).ShouldNotContain("passport");
        Codes(after).ShouldNotContain("photo");
        Codes(after).ShouldNotContain("address");
        Codes(after).ShouldNotContain("phone");
        Codes(after).ShouldContain("pinfl");
        after.GetProperty("hasPhoto").GetBoolean().ShouldBeTrue();
        after.GetProperty("done").GetInt32().ShouldBe(row.GetProperty("done").GetInt32() + 4);
    }

    [Fact]
    public async Task No_pinfl_or_passport_value_leaves_through_the_worklist()
    {
        var name = Name();
        var pinfl = RandomPinfl();
        var passport = RandomPassport();
        var employeeId = await HireAsync(name);

        await DbAsync(async db =>
        {
            var employee = await db.Employees.SingleAsync(item => item.Id == employeeId, Ct);
            employee.SetPinfl(Pinfl.FromTrusted(pinfl)).IsSuccess.ShouldBeTrue();
            employee.SetPassport(PassportSeries.Create(passport).Value);
            return await db.SaveChangesAsync(Ct);
        });

        using var client = await ClientAsync(Permissions.EmployeesView);

        using var response = await client.GetAsync(new Uri($"{Root}?search={name}", UriKind.Relative), Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);

        text.ShouldContain(name);
        text.ShouldNotContain(pinfl);
        text.ShouldNotContain(passport);
        text.ShouldNotContain("\"pinfl\":", Case.Insensitive);
        text.ShouldNotContain("\"passport\":", Case.Insensitive);

        // Findable by it, as in the employee list, and still only by whether it exists.
        var byPinfl = (await RowsAsync(client, pinfl)).ShouldHaveSingleItem();
        byPinfl.GetProperty("employeeId").GetGuid().ShouldBe(employeeId);
        Codes(byPinfl).ShouldNotContain("pinfl");
        Codes(byPinfl).ShouldNotContain("passport");
    }

    [Fact]
    public async Task Only_active_required_types_are_kinds_and_a_removed_document_stops_counting()
    {
        var name = Name();
        var employeeId = await HireAsync(name);

        var required = await DocumentTypeAsync(required: true);
        var optional = await DocumentTypeAsync(required: false);
        var retired = await DocumentTypeAsync(required: true, active: false);

        using var client = await ClientAsync(Permissions.EmployeesView);

        var requiredCode = "doc:" + required;

        var kinds = KindCodes(await GetAsync(client, Root + "/summary"));
        kinds.ShouldContain(requiredCode);
        kinds.ShouldNotContain("doc:" + optional);
        kinds.ShouldNotContain("doc:" + retired);

        var before = (await RowsAsync(client, name)).ShouldHaveSingleItem();
        Codes(before).ShouldContain(requiredCode);
        Codes(before).ShouldNotContain("doc:" + optional);

        var documentId = await DbAsync(async db =>
        {
            var document = EmployeeDocument.Attach(employeeId, required, "docs/" + name, "a.pdf", "application/pdf", 10).Value;
            db.EmployeeDocuments.Add(document);
            await db.SaveChangesAsync(Ct);
            return document.Id;
        });

        var attached = (await RowsAsync(client, name)).ShouldHaveSingleItem();
        Codes(attached).ShouldNotContain(requiredCode);
        attached.GetProperty("done").GetInt32().ShouldBe(before.GetProperty("done").GetInt32() + 1);

        await DbAsync(async db =>
        {
            db.EmployeeDocuments.Remove(await db.EmployeeDocuments.SingleAsync(item => item.Id == documentId, Ct));
            return await db.SaveChangesAsync(Ct);
        });

        Codes((await RowsAsync(client, name)).ShouldHaveSingleItem()).ShouldContain(requiredCode);
    }

    [Fact]
    public async Task Both_review_flags_count_as_one_kind()
    {
        var onEmployee = Name();
        var onEmployment = Name();
        var neither = Name();

        var employeeId = await HireAsync(onEmployee);
        var employedId = await HireAsync(onEmployment);
        await HireAsync(neither);

        await DbAsync(async db =>
        {
            (await db.Employees.SingleAsync(item => item.Id == employeeId, Ct)).FlagForReview("tekshirish");
            (await db.Employments.SingleAsync(item => item.EmployeeId == employedId, Ct)).FlagForReview("tekshirish");
            return await db.SaveChangesAsync(Ct);
        });

        using var client = await ClientAsync(Permissions.EmployeesView);

        Codes((await RowsAsync(client, onEmployee)).ShouldHaveSingleItem()).ShouldContain("needsReview");
        Codes((await RowsAsync(client, onEmployment)).ShouldHaveSingleItem()).ShouldContain("needsReview");
        Codes((await RowsAsync(client, neither)).ShouldHaveSingleItem()).ShouldNotContain("needsReview");
    }

    [Fact]
    public async Task A_second_job_lacking_an_order_counts_and_the_person_stays_one_row()
    {
        var name = Name();
        var hired = Day(2020, 1, 1);
        var employeeId = await HireAsync(name, hired, orderNo: "ORD-1");

        using var client = await ClientAsync(Permissions.EmployeesView);
        Codes((await RowsAsync(client, name)).ShouldHaveSingleItem()).ShouldNotContain("orderNo");

        await DbAsync(async db =>
        {
            var legalEntity = LegalEntity.Create("Ikkinchi " + name).Value;
            db.LegalEntities.Add(legalEntity);
            var second = Employment.Hire(employeeId, legalEntity.Id, Day(2024, 1, 1), orderNo: null).Value;
            second.SetPrimary(false);
            db.Employments.Add(second);
            return await db.SaveChangesAsync(Ct);
        });

        var row = (await RowsAsync(client, name)).ShouldHaveSingleItem();
        Codes(row).ShouldContain("orderNo");
        row.GetProperty("hireDate").GetString().ShouldBe("2020-01-01", "the primary job is the one shown");
    }

    [Fact]
    public async Task A_terminated_employee_is_not_on_the_list_and_a_complete_one_neither()
    {
        var gone = Name();
        var goneId = await HireAsync(gone);

        await DbAsync(async db =>
        {
            var employment = await db.Employments.SingleAsync(item => item.EmployeeId == goneId, Ct);
            employment.Terminate(employment.HireDate, reasonId: null).IsSuccess.ShouldBeTrue();
            return await db.SaveChangesAsync(Ct);
        });

        var complete = Name();
        var completeId = await CompleteEmployeeAsync(complete);

        using var client = await ClientAsync(Permissions.EmployeesView, Permissions.SalaryView);

        (await RowsAsync(client, gone)).ShouldBeEmpty();
        (await RowsAsync(client, complete)).ShouldBeEmpty();

        // Still counted among the employees checked, and the terminated one nowhere.
        var summary = await GetAsync(client, $"{Root}/summary?search={complete}");
        summary.GetProperty("employees").GetInt32().ShouldBe(1);
        summary.GetProperty("withGaps").GetInt32().ShouldBe(0);

        var goneSummary = await GetAsync(client, $"{Root}/summary?search={gone}");
        goneSummary.GetProperty("employees").GetInt32().ShouldBe(0);

        completeId.ShouldNotBe(Guid.Empty);
    }

    // ────────────────────────────────────────────────────────────
    //  Order, filters, pages, summary
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Most_gaps_come_first_then_the_newest_hire()
    {
        var group = Name();
        var older = await HireAsync(group + "a", Day(2021, 5, 1));
        var newer = await HireAsync(group + "b", Day(2025, 5, 1));
        var worst = await HireAsync(group + "c", Day(2019, 5, 1));

        await DbAsync(async db =>
        {
            foreach (var id in new[] { older, newer })
            {
                (await db.Employees.SingleAsync(item => item.Id == id, Ct)).SetAddress("Toshkent");
            }

            return await db.SaveChangesAsync(Ct);
        });

        using var client = await ClientAsync(Permissions.EmployeesView);

        var rows = await RowsAsync(client, group);

        rows.Select(row => row.GetProperty("employeeId").GetGuid()).ShouldBe([worst, newer, older]);
    }

    [Fact]
    public async Task Search_department_and_kind_filter_both_list_and_counts()
    {
        var group = Name();

        var (parent, child) = await DbAsync(async db =>
        {
            var top = Department.Create("Top " + group).Value;
            db.Departments.Add(top);
            var below = Department.Create("Below " + group, top.Id).Value;
            db.Departments.Add(below);
            await db.SaveChangesAsync(Ct);
            return (top.Id, below.Id);
        });

        var inChild = await HireAsync(group + "a", departmentId: child);
        var elsewhere = await HireAsync(group + "b");

        await DbAsync(async db =>
        {
            (await db.Employees.SingleAsync(item => item.Id == elsewhere, Ct)).SetAddress("Toshkent");
            return await db.SaveChangesAsync(Ct);
        });

        using var client = await ClientAsync(Permissions.EmployeesView);

        (await RowsAsync(client, group)).Count.ShouldBe(2);

        // The department and what is under it.
        var underParent = await GetAsync(client, $"{Root}?search={group}&departmentId={parent}");
        var only = underParent.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        only.GetProperty("employeeId").GetGuid().ShouldBe(inChild);
        only.GetProperty("department").GetString().ShouldBe("Below " + group);

        var byKind = await GetAsync(client, $"{Root}?search={group}&missing=address");
        byKind.GetProperty("total").GetInt32().ShouldBe(1);
        byKind.GetProperty("items").EnumerateArray().ShouldHaveSingleItem()
            .GetProperty("employeeId").GetGuid().ShouldBe(inChild);

        var summary = await GetAsync(client, $"{Root}/summary?search={group}");
        summary.GetProperty("employees").GetInt32().ShouldBe(2);
        summary.GetProperty("withGaps").GetInt32().ShouldBe(2);
        KindCount(summary, "pinfl").ShouldBe(2);
        KindCount(summary, "address").ShouldBe(1);
        KindCount(summary, "needsReview").ShouldBe(0, "a kind at zero is listed all the same");

        var narrowed = await GetAsync(client, $"{Root}/summary?search={group}&departmentId={child}");
        narrowed.GetProperty("employees").GetInt32().ShouldBe(1);
        KindCount(narrowed, "pinfl").ShouldBe(1);
    }

    [Fact]
    public async Task An_unknown_kind_is_refused_and_a_required_document_kind_is_accepted()
    {
        var required = await DocumentTypeAsync(required: true);

        using var client = await ClientAsync(Permissions.EmployeesView);

        (await client.GetAsync(new Uri(Root + "?missing=nonsense", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.GetAsync(new Uri($"{Root}?missing=doc:{Guid.NewGuid()}", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.GetAsync(new Uri($"{Root}?missing=doc:{required}", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Pages_cut_the_list_and_total_stays_whole()
    {
        var group = Name();
        for (var index = 0; index < 3; index++)
        {
            await HireAsync(group + index.ToString(CultureInfo.InvariantCulture));
        }

        using var client = await ClientAsync(Permissions.EmployeesView);

        var first = await GetAsync(client, $"{Root}?search={group}&page=1&pageSize=2");
        var second = await GetAsync(client, $"{Root}?search={group}&page=2&pageSize=2");

        first.GetProperty("total").GetInt32().ShouldBe(3);
        first.GetProperty("pageCount").GetInt32().ShouldBe(2);
        first.GetProperty("items").GetArrayLength().ShouldBe(2);
        second.GetProperty("items").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task The_tartib_card_counts_what_the_tab_lists_under_the_same_permission()
    {
        await HireAsync(Name());

        using var both = await ClientAsync(Permissions.ProcessesView, Permissions.EmployeesView);
        using var processesOnly = await ClientAsync(Permissions.ProcessesView);

        var counts = (await GetAsync(both, My)).GetProperty("tartib").GetProperty("counts");
        var card = counts.EnumerateArray().Single(item => item.GetProperty("key").GetString() == TartibKey);

        var summary = await GetAsync(both, Root + "/summary");
        card.GetProperty("count").GetInt32().ShouldBe(summary.GetProperty("withGaps").GetInt32());
        card.GetProperty("count").GetInt32().ShouldBeGreaterThan(0);

        (await GetAsync(processesOnly, My)).GetProperty("tartib").GetProperty("counts").EnumerateArray()
            .ShouldNotContain(item => item.GetProperty("key").GetString() == TartibKey);
    }

    // ────────────────────────────────────────────────────────────
    //  Query count
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_reads_send_the_same_commands_for_a_few_employees_and_for_many()
    {
        await using var host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(_log))));

        var group = Name();
        var department = await DbAsync(async db =>
        {
            var created = Department.Create("Counted " + group).Value;
            db.Departments.Add(created);
            await db.SaveChangesAsync(Ct);
            return created.Id;
        });

        var viewer = await TestAccounts.CreateAsync(host, Permissions.EmployeesView, Permissions.SalaryView);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", viewer.AccessToken);

        string[] routes =
        [
            $"{Root}?search={group}&pageSize=1000",
            $"{Root}?departmentId={department}&pageSize=1000",
            $"{Root}/summary?search={group}",
            $"{Root}/summary?departmentId={department}",
        ];

        await HireAsync(group + "0", departmentId: department);
        var few = await MeasureAsync(client, routes);

        for (var index = 1; index <= 6; index++)
        {
            await HireAsync(group + index.ToString(CultureInfo.InvariantCulture), departmentId: department);
        }

        var many = await MeasureAsync(client, routes);

        foreach (var route in routes)
        {
            few[route].ShouldBeGreaterThan(0, route);
            many[route].ShouldBe(few[route], route);
            many[route].ShouldBeLessThanOrEqualTo(4, route);
        }

        (await GetAsync(client, routes[0])).GetProperty("items").GetArrayLength().ShouldBe(7);
    }

    private async Task<Dictionary<string, int>> MeasureAsync(HttpClient client, string[] routes)
    {
        var measured = new Dictionary<string, int>();

        foreach (var route in routes)
        {
            await GetAsync(client, route);

            _log.Start();
            await GetAsync(client, route);
            measured[route] = _log.Stop().Count;
        }

        return measured;
    }

    // ────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────

    /// <summary>A name of letters only, unique enough for one test's <c>search</c> to reach only its own people.</summary>
    private static string Name()
    {
        var letters = Guid.NewGuid().ToString("N")[..10].Select(c => (char)('a' + (c % 26)));
        return "Zq" + string.Concat(letters);
    }

    private static string RandomPinfl()
        => "3" + RandomNumberGenerator.GetInt32(100_000, 999_999).ToString(CultureInfo.InvariantCulture)
            + RandomNumberGenerator.GetInt32(1_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture);

    private static string RandomPassport()
        => "AB" + RandomNumberGenerator.GetInt32(1_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture);

    private static string RandomPhone()
        => "90" + RandomNumberGenerator.GetInt32(1_000_000, 9_999_999).ToString(CultureInfo.InvariantCulture);

    private Task<Guid> HireAsync(
        string lastName,
        DateOnly? hired = null,
        Guid? departmentId = null,
        string? orderNo = null) => DbAsync(async db =>
    {
        var legalEntity = LegalEntity.Create("Firma " + lastName).Value;
        db.LegalEntities.Add(legalEntity);

        var employee = Employee.Create(FullName.Create(lastName, "Test", null).Value).Value;
        db.Employees.Add(employee);

        db.Employments.Add(Employment.Hire(
            employee.Id,
            legalEntity.Id,
            hired ?? Day(2024, 3, 1),
            departmentId: departmentId,
            orderNo: orderNo).Value);

        await db.SaveChangesAsync(Ct);
        return employee.Id;
    });

    /// <summary>Everything filled in, every required document attached, whatever the shared catalogue holds by now.</summary>
    private async Task<Guid> CompleteEmployeeAsync(string lastName)
    {
        var employeeId = await HireAsync(lastName, orderNo: "ORD-" + lastName);

        await DbAsync(async db =>
        {
            var employee = await db.Employees.SingleAsync(item => item.Id == employeeId, Ct);
            employee.SetPinfl(Pinfl.FromTrusted(RandomPinfl())).IsSuccess.ShouldBeTrue();
            employee.SetPassport(PassportSeries.Create(RandomPassport()).Value);
            employee.SetPhoto("photos/" + employeeId + ".jpg");
            employee.SetPhones(PhoneNumber.Create(RandomPhone()).Value);
            employee.SetAddress("Toshkent");

            var employment = await db.Employments.SingleAsync(item => item.EmployeeId == employeeId, Ct);
            employment.SetSalary(Money.Create(1_000_000m).Value, employment.HireDate).IsSuccess.ShouldBeTrue();

            var required = await db.DocumentTypes.Where(type => type.IsRequired && type.IsActive).Select(type => type.Id).ToListAsync(Ct);
            foreach (var typeId in required)
            {
                db.EmployeeDocuments.Add(EmployeeDocument.Attach(employeeId, typeId, "docs/" + typeId, "a.pdf", "application/pdf", 10).Value);
            }

            return await db.SaveChangesAsync(Ct);
        });

        return employeeId;
    }

    private Task<Guid> DocumentTypeAsync(bool required, bool active = true) => DbAsync(async db =>
    {
        var type = DocumentType.Create("Tur " + Name()).Value;
        type.SetRequired(required);
        if (!active)
        {
            type.Deactivate();
        }

        db.DocumentTypes.Add(type);
        await db.SaveChangesAsync(Ct);
        return type.Id;
    });

    private async Task<T> DbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<HttpClient> ClientAsync(params string[] permissions)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await TestAccounts.TokenAsync(_factory, permissions));
        return client;
    }

    private static async Task<List<JsonElement>> RowsAsync(HttpClient client, string search)
    {
        var body = await GetAsync(client, $"{Root}?search={search}&pageSize=100");
        return [.. body.GetProperty("items").EnumerateArray()];
    }

    private static List<string> Codes(JsonElement row)
        => [.. row.GetProperty("missing").EnumerateArray().Select(code => code.GetString()!)];

    private static List<string> KindCodes(JsonElement summary)
        => [.. summary.GetProperty("kinds").EnumerateArray().Select(kind => kind.GetProperty("code").GetString()!)];

    private static int KindCount(JsonElement summary, string code)
        => summary.GetProperty("kinds").EnumerateArray()
            .Single(kind => kind.GetProperty("code").GetString() == code)
            .GetProperty("count").GetInt32();

    private static async Task<JsonElement> GetAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(new Uri(url, UriKind.Relative), Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);

        response.IsSuccessStatusCode.ShouldBeTrue(url + " → " + text);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private sealed class CommandLog : DbCommandInterceptor
    {
        private ConcurrentQueue<string>? _recording;

        public void Start() => Volatile.Write(ref _recording, new ConcurrentQueue<string>());

        public IReadOnlyList<string> Stop() => [.. Interlocked.Exchange(ref _recording, null) ?? []];

        public override DbCommand CommandInitialized(CommandEndEventData eventData, DbCommand result)
        {
            Volatile.Read(ref _recording)?.Enqueue(result.CommandText);
            return result;
        }
    }
}
