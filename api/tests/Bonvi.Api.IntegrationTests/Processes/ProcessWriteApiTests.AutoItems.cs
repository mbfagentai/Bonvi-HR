using System.Net;
using System.Text.Json;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Domain.Auditing;
using Bonvi.Domain.Catalog;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Processes;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Api.IntegrationTests.Processes;

/// <summary>Data-backed checklist items through the API: what the read reports and what «Bajarildi» enforces.</summary>
public sealed partial class ProcessWriteApiTests
{
    private const string MalumotYetishmaydi = "jarayon.malumot_yetishmaydi";

    private const string Diploma = "Маълумот тўғрисида ҳужжат (диплом)";

    /// <summary>The r01-2 items that have no place in the system: the client still ticks these.</summary>
    private static readonly string[] PlainDocsItems = ["certificates", "bankCard", "liability"];

    /// <summary>One document type for each r01-2 item that reads an upload.</summary>
    private static readonly string[] DocsStepTypes =
    [
        Diploma,
        "Меҳнат дафтарчаси",
        "Ҳарбий ҳисоб варақаси",
        "Тиббий кўрик хулосаси",
        "Ариза",
        "Анкета + кенгаш хулосаси",
    ];

    private static Dictionary<string, string> PlainDocsTicks() => PlainDocsItems.ToDictionary(item => item, _ => "done");

    private static JsonElement DocsInput(JsonElement step, string inputId = "docs")
        => step.GetProperty("manual").GetProperty("inputs").EnumerateArray().Single(input => input.GetProperty("id").GetString() == inputId);

    private static JsonElement AutoItem(JsonElement input, string value)
        => input.GetProperty("autoItems").EnumerateArray().Single(item => item.GetProperty("value").GetString() == value);

    private static string RandomPassport()
        => "AA" + Random.Shared.Next(1_000_000, 10_000_000).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>What the employee's card holds for r01-2: a passport and a photo. <see cref="FormaliseAsync"/> has set the JSHSHIR.</summary>
    private Task<int> FillCardAsync(Guid employeeId, string passport) => DbAsync(async db =>
    {
        var employee = await db.Employees.SingleAsync(row => row.Id == employeeId, Ct);
        employee.SetPassport(PassportSeries.FromTrusted(passport));
        employee.SetPhoto($"employees/{employeeId}.jpg");
        return await db.SaveChangesAsync(Ct);
    });

    /// <summary>Real uploads, each of a type by that name; a type the database lacks is created as HR would.</summary>
    private Task<int> AttachDocumentsAsync(Guid employeeId, params string[] typeNames) => DbAsync(async db =>
    {
        foreach (var name in typeNames)
        {
            var type = await db.DocumentTypes.FirstOrDefaultAsync(row => row.Name == name, Ct);
            if (type is null)
            {
                type = DocumentType.Create(name).Value;
                db.DocumentTypes.Add(type);
            }

            db.EmployeeDocuments.Add(EmployeeDocument.Attach(employeeId, type.Id, $"employees/{employeeId}/{Suffix()}.pdf", "fayl.pdf", "application/pdf", 10).Value);
        }

        return await db.SaveChangesAsync(Ct);
    });

    private Task<int> SetGenderAsync(Guid employeeId, Gender gender) => DbAsync(async db =>
    {
        var employee = await db.Employees.SingleAsync(row => row.Id == employeeId, Ct);
        employee.SetGender(gender);
        return await db.SaveChangesAsync(Ct);
    });

    private static Task<Reply> DoneWithReasonAsync(HttpClient client, Guid caseId, string step, object? inputs, string missingReason, int version)
        => PostAsync(client, $"{Root}/cases/{caseId}/steps/{step}/done", new { version, inputs, missingReason });

    [Fact]
    public async Task The_detail_says_for_each_item_what_the_records_hold_and_where_to_fill_the_rest()
    {
        var (hire, processCase) = await FormaliseAsync();
        await AttachDocumentsAsync(hire.EmployeeId, "Ариза", Diploma);
        var passport = RandomPassport();
        await FillCardAsync(hire.EmployeeId, passport);
        using var client = await ManagerAsync();

        var reply = await SendAsync(client, "GET", $"{Root}/cases/{processCase.Id}", string.Empty);
        reply.Status.ShouldBe(HttpStatusCode.OK, reply.Raw);
        var input = DocsInput(StepOf(reply.Json, "r01-2"));

        var pinfl = AutoItem(input, "pinfl");
        pinfl.GetProperty("state").GetString().ShouldBe("Known");
        pinfl.GetProperty("autoValue").GetString().ShouldBe("done");
        pinfl.GetProperty("autoSource").GetString().ShouldBe("EmployeeCard");
        pinfl.GetProperty("fill").ValueKind.ShouldBe(JsonValueKind.Null);

        var application = AutoItem(input, "application");
        application.GetProperty("state").GetString().ShouldBe("Known");
        application.GetProperty("autoSource").GetString().ShouldBe("EmployeeDocument");
        application.GetProperty("sourceDetail").GetString().ShouldBe("Ариза");

        var medical = AutoItem(input, "medical");
        medical.GetProperty("state").GetString().ShouldBe("Missing");
        medical.GetProperty("autoValue").ValueKind.ShouldBe(JsonValueKind.Null);
        var fill = medical.GetProperty("fill");
        fill.GetProperty("kind").GetString().ShouldBe("EmployeeDocument");
        fill.GetProperty("employeeId").GetGuid().ShouldBe(hire.EmployeeId);
        fill.GetProperty("documentTypeName").GetString().ShouldBe("Тиббий кўрик хулосаси");

        input.GetProperty("autoItems").EnumerateArray()
            .Select(item => item.GetProperty("value").GetString())
            .ShouldNotContain("certificates", "it has no place in the system: a plain tick");

        reply.Raw.ShouldNotContain(passport, Case.Sensitive, "only that it exists");
    }

    [Fact]
    public async Task The_templates_have_no_auto_items_and_a_closed_step_reports_none()
    {
        var (_, processCase) = await FormaliseAsync();
        using var client = await ManagerAsync();

        var templates = await GetAsync(client, $"{Root}/templates");
        var formalise = templates.EnumerateArray().Single(template => template.GetProperty("code").GetString() == ProcessCodes.HireFormalise);
        var step = formalise.GetProperty("steps").EnumerateArray().Single(row => row.GetProperty("id").GetString() == "r01-2");
        DocsInput(step).GetProperty("autoItems").ValueKind.ShouldBe(JsonValueKind.Null);

        // A step closed before this feature existed: its payload has no sources and is shown as it was.
        await CloseManuallyAsync(processCase.Id, "r01-2", await UserIdAsync(), """{"docs":{"passport":"done"}}""");

        var detail = await DetailAsync(client, processCase.Id);
        var closed = StepOf(detail, "r01-2");
        closed.GetProperty("state").GetString().ShouldBe("Done");
        closed.GetProperty("payload").GetProperty("docs").GetProperty("passport").GetString().ShouldBe("done");
        DocsInput(closed).GetProperty("autoItems").ValueKind.ShouldBe(JsonValueKind.Null);

        var fileItems = DocsInput(StepOf(detail, "r01-7"), "items");
        AutoItem(fileItems, "docCopies").GetProperty("state").GetString().ShouldBe("Known", "that old close counts for r01-7");
        AutoItem(fileItems, "docCopies").GetProperty("autoSource").GetString().ShouldBe("CaseStep");
        var safety = AutoItem(fileItems, "safetyMark");
        safety.GetProperty("state").GetString().ShouldBe("Missing");
        safety.GetProperty("fill").GetProperty("kind").GetString().ShouldBe("CaseStep");
        safety.GetProperty("fill").GetProperty("stepCode").GetString().ShouldBe("mm");
    }

    [Fact]
    public async Task Ticking_everything_on_a_bare_employee_is_refused_with_the_list_and_writes_nothing()
    {
        var (_, processCase) = await FormaliseAsync();
        using var client = await ManagerAsync();
        var before = await DetailAsync(client, processCase.Id);
        var docs = await InputOptionsAsync(processCase.TemplateVersionId, "r01-2", "docs");

        var reply = await DoneAsync(client, processCase.Id, "r01-2", new { docs = docs.ToDictionary(item => item, _ => "done") }, Version(before));

        reply.Status.ShouldBe(HttpStatusCode.Conflict, reply.Raw);
        reply.Title.ShouldBe(MalumotYetishmaydi);
        var missing = reply.Json.GetProperty("missing").EnumerateArray().ToList();
        missing.Select(row => row.GetProperty("item").GetString()).ShouldContain("passport");
        missing.Select(row => row.GetProperty("item").GetString()).ShouldNotContain("pinfl", "the JSHSHIR is on the card");
        missing.Select(row => row.GetProperty("item").GetString()).ShouldNotContain("certificates");
        missing[0].GetProperty("input").GetString().ShouldBe("docs");
        missing[0].GetProperty("label").GetString().ShouldNotBeNullOrWhiteSpace();

        (await CaseByIdAsync(processCase.Id)).FindStep("r01-2")!.State.ShouldBe(ProcessStepState.Active);
    }

    [Fact]
    public async Task Marks_on_known_items_are_recomputed_and_the_sources_stored()
    {
        var (hire, processCase) = await FormaliseAsync();
        await FillCardAsync(hire.EmployeeId, RandomPassport());
        await AttachDocumentsAsync(hire.EmployeeId, DocsStepTypes);
        await SetGenderAsync(hire.EmployeeId, Gender.Female);
        using var client = await ManagerAsync();
        var before = await DetailAsync(client, processCase.Id);
        AutoItem(DocsInput(StepOf(before, "r01-2")), "military").GetProperty("reason").GetString().ShouldBe("Female");

        var marks = PlainDocsTicks();
        marks["passport"] = "na";
        marks["military"] = "done";

        var reply = await DoneAsync(client, processCase.Id, "r01-2", new { docs = marks }, Version(before));

        reply.Status.ShouldBe(HttpStatusCode.OK, reply.Raw);
        var payload = StepOf(reply.Json, "r01-2").GetProperty("payload");
        payload.GetProperty("docs").GetProperty("passport").GetString().ShouldBe("done");
        payload.GetProperty("docs").GetProperty("military").GetString().ShouldBe("na");
        payload.GetProperty("docs").GetProperty("certificates").GetString().ShouldBe("done");
        payload.GetProperty("autoSources").GetProperty("docs").GetProperty("passport").GetString().ShouldBe("employee.passport");
        payload.GetProperty("autoSources").GetProperty("docs").GetProperty("diploma").GetString().ShouldBe("doc:" + Diploma);
        payload.TryGetProperty("missingOverride", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task A_holder_without_manage_cannot_close_past_missing_data_with_a_reason()
    {
        await ClearRolesAsync();
        var (_, processCase) = await FormaliseAsync();
        var holder = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork);
        await AssignRoleAsync(holder.UserId, ProcessRoles.Hr2);
        using var client = ClientWith(holder.AccessToken);
        var before = await DetailAsync(client, processCase.Id);

        var reply = await DoneWithReasonAsync(client, processCase.Id, "r01-2", new { docs = PlainDocsTicks() }, "Keyin keladi", Version(before));

        reply.Status.ShouldBe(HttpStatusCode.Forbidden, reply.Raw);
        reply.Title.ShouldBe(Ruxsat);
        (await CaseByIdAsync(processCase.Id)).FindStep("r01-2")!.State.ShouldBe(ProcessStepState.Active);
    }

    [Fact]
    public async Task A_manager_closes_past_missing_data_with_a_reason_kept_in_the_payload_and_the_audit()
    {
        var (_, processCase) = await FormaliseAsync();
        var manager = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView, Permissions.ProcessesWork, Permissions.ProcessesManage);
        using var client = ClientWith(manager.AccessToken);
        var before = await DetailAsync(client, processCase.Id);
        var reason = "Hujjat keyin keladi " + Suffix();

        var reply = await DoneWithReasonAsync(client, processCase.Id, "r01-2", new { docs = PlainDocsTicks() }, "  " + reason + "  ", Version(before));

        reply.Status.ShouldBe(HttpStatusCode.OK, reply.Raw);
        var closed = StepOf(reply.Json, "r01-2");
        closed.GetProperty("state").GetString().ShouldBe("Done");
        var missingOverride = closed.GetProperty("payload").GetProperty("missingOverride");
        missingOverride.GetProperty("reason").GetString().ShouldBe(reason);
        missingOverride.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("item").GetString()).ShouldContain("passport");
        closed.GetProperty("payload").GetProperty("docs").TryGetProperty("passport", out _).ShouldBeFalse("what is missing gets no mark");

        var row = (await AuditRowsAsync(nameof(ProcessCase), processCase.Id)).Single(audit => audit.NewValues != null && audit.NewValues.Contains(reason));
        row.Action.ShouldBe(AuditAction.Update);
        row.UserId.ShouldBe(manager.UserId);
        row.NewValues.ShouldNotBeNull().ShouldContain("r01-2");
        row.NewValues.ShouldContain("passport");
    }

    [Fact]
    public async Task A_refused_override_leaves_no_audit_and_a_reason_nobody_needed_is_not_kept()
    {
        var (hire, processCase) = await FormaliseAsync();
        using var client = await ManagerAsync();
        var before = await DetailAsync(client, processCase.Id);
        var auditBefore = (await AuditRowsAsync(nameof(ProcessCase), processCase.Id)).Count;

        var tooLong = await DoneWithReasonAsync(client, processCase.Id, "r01-2", new { docs = PlainDocsTicks() }, new string('a', ProcessCase.MaxCancelReasonLength + 1), Version(before));
        tooLong.Status.ShouldBe(HttpStatusCode.BadRequest, tooLong.Raw);
        tooLong.Title.ShouldBe(Kiritish);
        (await AuditRowsAsync(nameof(ProcessCase), processCase.Id)).Count.ShouldBe(auditBefore);

        await FillCardAsync(hire.EmployeeId, RandomPassport());
        await AttachDocumentsAsync(hire.EmployeeId, DocsStepTypes);
        var current = await DetailAsync(client, processCase.Id);

        var reply = await DoneWithReasonAsync(client, processCase.Id, "r01-2", new { docs = PlainDocsTicks() }, "Kerak bo'lmagan sabab", Version(current));

        reply.Status.ShouldBe(HttpStatusCode.OK, reply.Raw);
        StepOf(reply.Json, "r01-2").GetProperty("payload").TryGetProperty("missingOverride", out _).ShouldBeFalse();
        (await AuditRowsAsync(nameof(ProcessCase), processCase.Id)).ShouldNotContain(row => row.NewValues != null && row.NewValues.Contains("Kerak bo'lmagan sabab"));
    }

    [Fact]
    public async Task A_reason_dotnet_cannot_read_is_a_wrong_request_not_a_500()
    {
        var (_, processCase) = await FormaliseAsync();
        using var client = await ManagerAsync();
        var before = await DetailAsync(client, processCase.Id);
        var body = $$"""{"version":{{Version(before)}},"inputs":null,"missingReason":"\ud800"}""";

        var reply = await PostRawAsync(client, $"{Root}/cases/{processCase.Id}/steps/r01-2/done", body);

        reply.Status.ShouldBe(HttpStatusCode.BadRequest, reply.Raw);
    }

    [Fact]
    public async Task A_step_outside_the_map_closes_as_before()
    {
        var (_, processCase) = await FormaliseAsync();
        using var client = await ManagerAsync();
        var before = await DetailAsync(client, processCase.Id);
        DocsInput(StepOf(before, "r01-6"), "enteredOn").GetProperty("autoItems").ValueKind.ShouldBe(JsonValueKind.Null);

        var reply = await DoneAsync(client, processCase.Id, "r01-6", new { enteredOn = Iso(Today) }, Version(before));

        reply.Status.ShouldBe(HttpStatusCode.OK, reply.Raw);
        StepOf(reply.Json, "r01-6").GetProperty("payload").TryGetProperty("autoSources", out _).ShouldBeFalse();
    }
}
