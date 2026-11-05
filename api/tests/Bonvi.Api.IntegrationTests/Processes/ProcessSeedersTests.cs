using System.Text.Json;
using System.Text.Json.Nodes;
using Bonvi.Application.Processes.Calendar;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Processes;
using Bonvi.Domain.Settings;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Processes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests.Processes;

/// <summary>
/// <see cref="ProcessTemplateSeeder"/>, <see cref="ProcessHolidaySeeder"/> and the calendar they feed, against real
/// PostgreSQL (docs/SPEC-mening-ishlarim.md §3.1, §6.1, §6.6, §6.10).
/// </summary>
/// <remarks>
/// The factory start-up already ran both seeders, as a real start would. Version changes use <c>probe-*</c> codes so
/// the real templates stay published as they are; the database is shared by the tests of the class, so each test
/// uses its own code and its own dates.
/// </remarks>
public sealed class ProcessSeedersTests : IClassFixture<BonviApiFactory>
{
    private static readonly string[] Codes = ["dismissal", "hire-candidate", "hire-formalise", "hire-request", "probation"];

    private static readonly ProcessTemplateSeedOptions Quiet = new(FailOnInvalid: false, WriteStartedOn: false);

    private readonly BonviApiFactory _factory;

    public ProcessSeedersTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ────────────────────────────────────────────────────────────
    //  Templates
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Startup_publishes_the_five_templates()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var templates = await context.ProcessTemplates.AsNoTracking()
            .Where(row => Codes.Contains(row.Code))
            .ToListAsync(Ct);

        templates.Select(row => row.Code).ShouldBe(Codes, ignoreOrder: true);
        templates.ShouldAllBe(row => row.IsActive);
        templates.Single(row => row.Code == "hire-request").SubjectKind.ShouldBe(ProcessSubjectKind.VacancyRequest);
        templates.Single(row => row.Code == "hire-candidate").SubjectKind.ShouldBe(ProcessSubjectKind.Candidate);
        templates.Single(row => row.Code == "dismissal").SubjectKind.ShouldBe(ProcessSubjectKind.Employment);

        var ids = templates.Select(row => row.Id).ToList();
        var versions = await context.ProcessTemplateVersions.AsNoTracking()
            .Where(row => ids.Contains(row.TemplateId))
            .ToListAsync(Ct);

        versions.Count.ShouldBe(Codes.Length);
        versions.ShouldAllBe(row => row.Status == ProcessTemplateVersionStatus.Published && row.PublishedAt != null);

        // Back from jsonb: still valid, and its hash still matches the canonical form
        foreach (var version in versions)
        {
            ProcessDefinitionValidator.Load(version.Definition).Version.ShouldBe(version.Version);
            ProcessTemplateSeeder.Sha256Hex(ProcessTemplateSeeder.Canonicalize(version.Definition))
                .ShouldBe(version.DefinitionSha256);
        }
    }

    [Fact]
    public async Task Restart_changes_no_row_of_the_real_templates()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<ProcessTemplateSeeder>();

        var before = await SnapshotAsync(context);

        var result = await seeder.SeedAsync(Quiet, Ct);

        result.ShouldBe(new ProcessTemplateSeedResult(0, 0, Codes.Length, 0, 0));

        context.ChangeTracker.Clear();
        (await SnapshotAsync(context)).ShouldBe(before);
    }

    [Fact]
    public async Task Version_change_and_same_version_with_another_hash()
    {
        const string code = "probe-cycle";
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<ProcessTemplateSeeder>();

        // 1. New template → created and published; reformatted → unchanged
        (await seeder.SeedAsync([Probe(code, "1.0", "Probe")], Quiet, Ct))
            .ShouldBe(new ProcessTemplateSeedResult(1, 1, 0, 0, 0));
        (await seeder.SeedAsync([Probe(code, "1.0", "Probe", indented: true)], Quiet, Ct))
            .ShouldBe(new ProcessTemplateSeedResult(0, 0, 1, 0, 0), "formatting does not change the hash");

        var stored = (await VersionsAsync(context, code)).Single();

        // 2. Same version, other content → conflict, the stored row is NOT overwritten
        (await seeder.SeedAsync([Probe(code, "1.0", "Other title")], Quiet, Ct))
            .ShouldBe(new ProcessTemplateSeedResult(0, 0, 0, 1, 0));

        context.ChangeTracker.Clear();
        var afterConflict = (await VersionsAsync(context, code)).Single();
        afterConflict.DefinitionSha256.ShouldBe(stored.DefinitionSha256);
        afterConflict.Definition.ShouldBe(stored.Definition);
        afterConflict.Status.ShouldBe(ProcessTemplateVersionStatus.Published);
        (await TemplateAsync(context, code)).Title.ShouldBe("Probe");

        // 3. New version → published, the previous one retired, the title follows
        (await seeder.SeedAsync([Probe(code, "1.1", "Probe new")], Quiet, Ct))
            .ShouldBe(new ProcessTemplateSeedResult(0, 1, 0, 0, 0));
        (await seeder.SeedAsync([Probe(code, "1.1", "Probe new")], Quiet, Ct))
            .ShouldBe(new ProcessTemplateSeedResult(0, 0, 1, 0, 0));

        context.ChangeTracker.Clear();
        var versions = await VersionsAsync(context, code);
        versions.Count.ShouldBe(2);
        versions.Single(row => row.Version == "1.0").Status.ShouldBe(ProcessTemplateVersionStatus.Retired);
        versions.Single(row => row.Version == "1.1").Status.ShouldBe(ProcessTemplateVersionStatus.Published);
        (await TemplateAsync(context, code)).Title.ShouldBe("Probe new");

        // 4. Older than the Published one and not stored → not seeded
        (await seeder.SeedAsync([Probe(code, "0.9", "Old")], Quiet, Ct))
            .ShouldBe(new ProcessTemplateSeedResult(0, 0, 0, 1, 0));
        (await VersionsAsync(context, code)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Deactivated_template_stays_inactive_through_restart_and_new_version()
    {
        const string code = "probe-inactive";
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<ProcessTemplateSeeder>();

        await seeder.SeedAsync([Probe(code, "1.0", "Probe")], Quiet, Ct);

        var template = await context.ProcessTemplates.SingleAsync(row => row.Code == code, Ct);
        template.Deactivate();
        await context.SaveChangesAsync(Ct);

        await seeder.SeedAsync([Probe(code, "1.0", "Probe")], Quiet, Ct);
        await seeder.SeedAsync([Probe(code, "1.1", "Probe")], Quiet, Ct);

        context.ChangeTracker.Clear();
        (await TemplateAsync(context, code)).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Bad_template_is_logged_and_skipped_the_good_one_is_still_seeded()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<ProcessTemplateSeeder>();

        var result = await seeder.SeedAsync(
            [Probe("probe-good", "1.0", "Good"), Broken("probe-bad"), new ProcessTemplateFile("probe-notjson", "{ not json")],
            Quiet,
            Ct);

        result.ShouldBe(new ProcessTemplateSeedResult(1, 1, 0, 0, 2));

        (await context.ProcessTemplates.CountAsync(row => row.Code == "probe-good", Ct)).ShouldBe(1);
        (await context.ProcessTemplates.CountAsync(row => row.Code == "probe-bad" || row.Code == "probe-notjson", Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task File_name_must_match_the_code()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<ProcessTemplateSeeder>();

        var result = await seeder.SeedAsync(
            [new ProcessTemplateFile("probe-other", Probe("probe-named", "1.0", "Probe").Json)], Quiet, Ct);

        result.ShouldBe(new ProcessTemplateSeedResult(0, 0, 0, 0, 1));
    }

    [Fact]
    public async Task Title_over_the_column_limit_is_invalid_and_does_not_stop_startup()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<ProcessTemplateSeeder>();

        var result = await seeder.SeedAsync(
            [Probe("probe-long", "1.0", new string('x', ProcessTemplate.MaxTitleLength + 1))], Quiet, Ct);

        result.ShouldBe(new ProcessTemplateSeedResult(0, 0, 0, 0, 1));
    }

    [Fact]
    public async Task In_development_a_bad_template_stops_startup_before_any_write()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<ProcessTemplateSeeder>();

        var error = await Should.ThrowAsync<InvalidOperationException>(() => seeder.SeedAsync(
            [Probe("probe-early", "1.0", "Early"), Broken("probe-strict"), Broken("probe-strict2")],
            new ProcessTemplateSeedOptions(FailOnInvalid: true, WriteStartedOn: false),
            Ct));

        error.Message.ShouldContain("probe-strict");
        error.Message.ShouldContain("probe-strict2", customMessage: "every bad file is named at once");
        (await context.ProcessTemplates.CountAsync(row => row.Code == "probe-early", Ct))
            .ShouldBe(0, "the valid file sorted first must not have been written");
    }

    [Fact]
    public async Task Start_date_is_absent_in_testing_written_once_and_not_on_a_broken_release()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<ProcessTemplateSeeder>();
        var writing = new ProcessTemplateSeedOptions(FailOnInvalid: false, WriteStartedOn: true);

        // 1. The factory runs as Testing: the engine must be dormant after startup
        (await StartedOnAsync(context)).ShouldBeNull();

        // 2. A release with a broken template keeps it dormant
        await seeder.SeedAsync([Broken("probe-dormant")], writing, Ct);
        (await StartedOnAsync(context)).ShouldBeNull();

        // 3. Written once, Tashkent today
        await seeder.SeedAsync([], writing, Ct);
        var today = scope.ServiceProvider.GetRequiredService<IClock>().Today;
        (await StartedOnAsync(context)).ShouldBe(today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

        // 4. A later start keeps the first date
        var row = await context.SystemSettings.SingleAsync(setting => setting.Key == SystemSettingKeys.ProcessesStartedOn, Ct);
        row.SetServerValue("2020-01-01").IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync(Ct);

        await seeder.SeedAsync([], writing, Ct);

        context.ChangeTracker.Clear();
        (await StartedOnAsync(context)).ShouldBe("2020-01-01");

        // The class database must not stay out of dormant mode for the other tests
        context.SystemSettings.Remove(await context.SystemSettings.SingleAsync(setting => setting.Key == SystemSettingKeys.ProcessesStartedOn, Ct));
        await context.SaveChangesAsync(Ct);
    }

    // ────────────────────────────────────────────────────────────
    //  Holidays and the calendar
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Holiday_seed_fills_two_years_once_and_leaves_hr_edits_alone()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<ProcessHolidaySeeder>();

        // 1. Startup: 8 fixed dates × 2026, 2027
        var seeded = await HolidaysAsync(context, 2026, 2027);
        seeded.Count.ShouldBe(16);
        seeded.ShouldAllBe(row => row.Kind == ProcessHolidayKind.Holiday);
        seeded.ShouldContain(row => row.Date == new DateOnly(2026, 9, 1) && row.Title == "Mustaqillik kuni");
        seeded.ShouldContain(row => row.Date == new DateOnly(2027, 12, 31));

        // 2. Restart → nothing
        (await seeder.SeedAsync(Ct)).ShouldBe(new ProcessHolidaySeedResult(0, 2));
        (await HolidaysAsync(context, 2026, 2027)).Count.ShouldBe(16);

        // 3. HR edits a title, removes a date, adds a moving holiday → a restart keeps all three
        var edited = await context.ProcessHolidays.SingleAsync(row => row.Date == new DateOnly(2026, 10, 1), Ct);
        edited.Update(ProcessHolidayKind.Holiday, "HR title").IsSuccess.ShouldBeTrue();
        context.ProcessHolidays.Remove(await context.ProcessHolidays.SingleAsync(row => row.Date == new DateOnly(2026, 12, 8), Ct));
        context.ProcessHolidays.Add(ProcessHoliday.Create(new DateOnly(2027, 3, 10), ProcessHolidayKind.Holiday, "Ramazon hayiti").Value);
        await context.SaveChangesAsync(Ct);

        (await seeder.SeedAsync(Ct)).ShouldBe(new ProcessHolidaySeedResult(0, 2));

        context.ChangeTracker.Clear();
        var kept = await HolidaysAsync(context, 2026, 2027);
        kept.Count.ShouldBe(16);
        kept.Single(row => row.Date == new DateOnly(2026, 10, 1)).Title.ShouldBe("HR title");
        kept.ShouldNotContain(row => row.Date == new DateOnly(2026, 12, 8));
        kept.ShouldContain(row => row.Date == new DateOnly(2027, 3, 10));

        // 4. A year emptied completely counts as not seeded
        context.ProcessHolidays.RemoveRange(await context.ProcessHolidays
            .Where(row => row.Date >= new DateOnly(2027, 1, 1) && row.Date <= new DateOnly(2027, 12, 31))
            .ToListAsync(Ct));
        await context.SaveChangesAsync(Ct);

        (await seeder.SeedAsync(Ct)).ShouldBe(new ProcessHolidaySeedResult(8, 1));
    }

    [Fact]
    public async Task Calendar_reads_holidays_and_working_saturdays_from_the_table()
    {
        // 2031: outside the seeded years
        var saturday = new DateOnly(2031, 3, 1);
        var friday = saturday.AddDays(-1);
        var monday = saturday.AddDays(2);
        var tuesday = saturday.AddDays(3);
        var wednesday = saturday.AddDays(4);
        saturday.DayOfWeek.ShouldBe(DayOfWeek.Saturday);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var source = scope.ServiceProvider.GetRequiredService<IWorkingDayCalendarSource>();

        var plain = await source.LoadAsync(Ct);
        plain.IsWorkingDay(saturday).ShouldBeFalse();
        plain.AddWorkingDays(friday, 1).ShouldBe(monday);

        context.ProcessHolidays.Add(ProcessHoliday.Create(saturday, ProcessHolidayKind.WorkingDay, "Ish kuni (ko'chirilgan)").Value);
        context.ProcessHolidays.Add(ProcessHoliday.Create(tuesday, ProcessHolidayKind.Holiday, "Hayit").Value);
        await context.SaveChangesAsync(Ct);

        var calendar = await source.LoadAsync(Ct);

        calendar.IsWorkingDay(saturday).ShouldBeTrue();
        calendar.IsWorkingDay(tuesday).ShouldBeFalse();
        calendar.AddWorkingDays(friday, 1).ShouldBe(saturday);
        calendar.AddWorkingDays(friday, 2).ShouldBe(monday);
        calendar.AddWorkingDays(friday, 3).ShouldBe(wednesday);
        calendar.AddWorkingDays(wednesday, -1).ShouldBe(monday);
        calendar.NextCouncilDay(friday, DayOfWeek.Tuesday).ShouldBe(wednesday, "a council day on a holiday moves to the next working day");
    }

    [Fact]
    public async Task Calendar_counts_a_seeded_holiday()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var calendar = await scope.ServiceProvider.GetRequiredService<IWorkingDayCalendarSource>().LoadAsync(Ct);

        // No other test edits this date
        var holiday = new DateOnly(2026, 9, 1);
        holiday.DayOfWeek.ShouldBe(DayOfWeek.Tuesday);
        calendar.IsWorkingDay(holiday).ShouldBeFalse();
        calendar.AddWorkingDays(holiday.AddDays(-1), 1).ShouldBe(holiday.AddDays(1));
    }

    // ────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────

    /// <summary>Valid minimal definition.</summary>
    private static ProcessTemplateFile Probe(string code, string version, string title, bool indented = false)
    {
        var root = new JsonObject
        {
            ["schema"] = 1,
            ["code"] = code,
            ["version"] = version,
            ["title"] = title,
            ["source"] = "HR-R02",
            ["subjectKind"] = "candidate",
            ["board"] = new JsonArray(new JsonObject { ["key"] = "one", ["title"] = "One" }),
            ["steps"] = new JsonArray(new JsonObject
            {
                ["id"] = "a",
                ["title"] = "A",
                ["column"] = "one",
                ["role"] = "hr4",
                ["kind"] = "fact",
                ["completion"] = new JsonObject { ["fact"] = "cand.contacted" },
                ["link"] = new JsonObject { ["screen"] = "candidate" },
            }),
        };

        return new ProcessTemplateFile(code, root.ToJsonString(new JsonSerializerOptions { WriteIndented = indented }));
    }

    /// <summary>An otherwise valid definition with an unknown step kind.</summary>
    private static ProcessTemplateFile Broken(string code)
    {
        var root = JsonNode.Parse(Probe(code, "1.0", "Broken").Json)!.AsObject();
        root["steps"]![0]!["kind"] = "nonsense";
        return new ProcessTemplateFile(code, root.ToJsonString());
    }

    private static Task<ProcessTemplate> TemplateAsync(AppDbContext context, string code)
        => context.ProcessTemplates.AsNoTracking().SingleAsync(row => row.Code == code, Ct);

    private static async Task<List<ProcessTemplateVersion>> VersionsAsync(AppDbContext context, string code)
    {
        var template = await TemplateAsync(context, code);

        return await context.ProcessTemplateVersions.AsNoTracking()
            .Where(row => row.TemplateId == template.Id)
            .ToListAsync(Ct);
    }

    private static async Task<List<string>> SnapshotAsync(AppDbContext context)
    {
        var templates = await context.ProcessTemplates.AsNoTracking()
            .Where(row => Codes.Contains(row.Code))
            .ToListAsync(Ct);
        var ids = templates.Select(row => row.Id).ToList();
        var versions = await context.ProcessTemplateVersions.AsNoTracking()
            .Where(row => ids.Contains(row.TemplateId))
            .ToListAsync(Ct);

        return templates
            .Select(row => $"t|{row.Id}|{row.Title}|{row.Source}|{row.IsActive}|{row.UpdatedAt:O}")
            .Concat(versions.Select(row => $"v|{row.Id}|{row.Status}|{row.DefinitionSha256}|{row.PublishedAt:O}|{row.UpdatedAt:O}"))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static Task<string?> StartedOnAsync(AppDbContext context)
        => context.SystemSettings.AsNoTracking()
            .Where(row => row.Key == SystemSettingKeys.ProcessesStartedOn)
            .Select(row => row.Value)
            .SingleOrDefaultAsync(Ct);

    private static Task<List<ProcessHoliday>> HolidaysAsync(AppDbContext context, int firstYear, int lastYear)
        => context.ProcessHolidays.AsNoTracking()
            .Where(row => row.Date >= new DateOnly(firstYear, 1, 1) && row.Date <= new DateOnly(lastYear, 12, 31))
            .OrderBy(row => row.Date)
            .ToListAsync(Ct);
}
