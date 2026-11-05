using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bonvi.Application.Processes.Definitions;
using Bonvi.Application.Processes.Engine;
using Bonvi.Application.Processes.Queries;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Processes;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Bonvi.Infrastructure.Processes.Engine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProcessGateCodes = Bonvi.Application.Processes.Definitions.ProcessGateCodes;

namespace Bonvi.Api.IntegrationTests.Processes;

/// <summary>
/// <c>/my</c> cuts each section, and nothing else may notice: the totals, the counts and the badge stay over everything.
/// Its own database, because the numbers are exact and a pass sweeps everything.
/// </summary>
public sealed class ProcessMyWorkCapTests : IClassFixture<BonviApiFactory>
{
    private const string Root = "/api/processes";

    /// <summary>Over the cap, so a cut shows; one case holds one step of each section.</summary>
    private const int Cases = ProcessWorkSections.MaxSectionRows + 3;

    private readonly BonviApiFactory _factory;

    public ProcessMyWorkCapTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sections_are_cut_at_the_cap_and_the_totals_and_the_badge_are_not()
    {
        await SeedAsync();

        var viewer = await TestAccounts.CreateAsync(_factory, Permissions.ProcessesView);
        await DbAsync(db =>
        {
            db.BlankRoleAssignments.Add(BlankRoleAssignment.Create(ProcessRoles.Hr4, viewer.UserId).Value);
            return db.SaveChangesAsync(Ct);
        });

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", viewer.AccessToken);

        var my = await GetAsync(client, Root + "/my");

        // One step is Blocked on Q03, the viewer's own to clear: urgent, and it sorts last as it has no due date.
        const int UrgentTotal = Cases + 1;
        const int Count = UrgentTotal + Cases;

        SectionShouldBe(my, "urgent", "urgentTotal", UrgentTotal);
        SectionShouldBe(my, "later", "laterTotal", Cases);
        SectionShouldBe(my, "held", "heldTotal", Cases);
        SectionShouldBe(my, "waiting", "waitingTotal", Cases);

        my.GetProperty("count").GetInt32().ShouldBe(Count);
        my.GetProperty("lateCount").GetInt32().ShouldBe(Cases, "over every late step, also those past the cut");

        (await GetAsync(client, Root + "/my/count")).GetProperty("count").GetInt32().ShouldBe(Count, "the badge and the tab agree");
    }

    private static void SectionShouldBe(JsonElement my, string section, string total, int expectedTotal)
    {
        my.GetProperty(section).GetArrayLength().ShouldBe(ProcessWorkSections.MaxSectionRows, section);
        my.GetProperty(total).GetInt32().ShouldBe(expectedTotal, total);
    }

    /// <summary>
    /// Cases from the engine, so they are whole; then one step of each per section by hand, and the engine dormant again
    /// so that the read's inline pass does not put them back.
    /// </summary>
    private async Task SeedAsync()
    {
        var today = _factory.Services.GetRequiredService<IClock>().Today;

        await SetStartedOnAsync(today.ToString(SystemSettingKeys.ProcessesStartedOnFormat, CultureInfo.InvariantCulture));

        await DbAsync(db =>
        {
            var vacancy = Vacancy.Open("Cap vacancy", today).Value;
            db.Vacancies.Add(vacancy);

            for (var i = 0; i < Cases; i++)
            {
                db.Candidates.Add(Candidate.Register(FullName.Create("Cap" + i.ToString(CultureInfo.InvariantCulture), "Test", null).Value, vacancy.Id).Value);
            }

            return db.SaveChangesAsync(Ct);
        });

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<ProcessReconciler>().RunAsync(Ct);
            result.Status.ShouldNotBe(ProcessReconcileStatus.Dormant);
        }

        await SetStartedOnAsync(string.Empty);

        var active = (short)ProcessStepState.Active;
        var blocked = (short)ProcessStepState.Blocked;
        var waiting = (short)ProcessStepState.Waiting;
        var (late, soon) = (today.AddDays(-1), today.AddDays(1));

        await SqlAsync($"UPDATE process_case_steps SET state = {active}, holder_role = {ProcessRoles.Hr4}, holder_employee_id = NULL, due_date = {late} WHERE step_code = 'r02-5'");
        await SqlAsync($"UPDATE process_case_steps SET state = {active}, holder_role = {ProcessRoles.Hr4}, holder_employee_id = NULL, due_date = {today.AddDays(30)} WHERE step_code = 'r02-6'");
        await SqlAsync($"UPDATE process_case_steps SET state = {blocked}, holder_role = {ProcessRoles.Hr4}, holder_employee_id = NULL, blocked_gate_code = {ProcessGateCodes.Q04} WHERE step_code = 'r02-9'");
        await SqlAsync($"UPDATE process_case_steps SET state = {waiting}, holder_role = {ProcessRoles.Hr4}, holder_employee_id = NULL, due_date = {soon} WHERE step_code = 'r02-10'");
        await SqlAsync($"""
            UPDATE process_case_steps SET state = {blocked}, holder_role = {ProcessRoles.Hr4}, holder_employee_id = NULL, due_date = NULL, blocked_gate_code = {ProcessGateCodes.Q03}
            WHERE step_code = 'r02-11' AND case_id = (SELECT min(case_id::text)::uuid FROM process_case_steps WHERE step_code = 'r02-11')
            """);
    }

    private Task<int> SetStartedOnAsync(string value) => DbAsync(async db =>
    {
        var row = await db.SystemSettings.SingleOrDefaultAsync(setting => setting.Key == SystemSettingKeys.ProcessesStartedOn, Ct);

        if (row is null)
        {
            db.SystemSettings.Add(SystemSetting.CreateServerValue(SystemSettingKeys.ProcessesStartedOn, value).Value);
        }
        else
        {
            row.SetServerValue(value).IsSuccess.ShouldBeTrue();
        }

        return await db.SaveChangesAsync(Ct);
    });

    private Task<int> SqlAsync(FormattableString sql) => DbAsync(db => db.Database.ExecuteSqlInterpolatedAsync(sql, Ct));

    private async Task<T> DbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(new Uri(url, UriKind.Relative), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }
}
