using Bonvi.Application.Processes.Engine;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Organization;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.Settings;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bonvi.Api.IntegrationTests.Processes;

/// <summary>
/// §7.6: a database as the Testing environment starts it has no <c>processes.startedOn</c>. This class's database
/// never gets one, so it stands for every other integration class: the existing hire and terminate tests, which
/// hire without a candidate, stay outside the engine however it is entered.
/// </summary>
public sealed class ProcessDormantInTestingTests : IClassFixture<BonviApiFactory>
{
    private readonly BonviApiFactory _factory;

    public ProcessDormantInTestingTests(BonviApiFactory factory) => _factory = factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_way_into_the_engine_ends_dormant_and_writes_nothing()
    {
        await HireWithoutACandidateAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();

        (await db.ProcessTemplates.AnyAsync(Ct)).ShouldBeTrue("dormant because of the missing date, not missing templates");
        (await db.SystemSettings.AnyAsync(row => row.Key == SystemSettingKeys.ProcessesStartedOn, Ct)).ShouldBeFalse();

        (await services.GetRequiredService<ProcessReconciler>().RunAsync(Ct)).Status.ShouldBe(ProcessReconcileStatus.Dormant);
        await services.GetRequiredService<ProcessReconcileJob>().RunAsync(Ct);

        var inline = _factory.Services.GetRequiredService<ProcessInlineReconcile>();
        await inline.BeforeReadAsync(Ct);
        await inline.BeforeCaseReadAsync(Guid.NewGuid(), Ct);

        (await db.ProcessCases.AnyAsync(Ct)).ShouldBeFalse();
        (await db.SystemSettings.AnyAsync(row => row.Key == SystemSettingKeys.ProcessesReconcileWatermark, Ct)).ShouldBeFalse();
    }

    private async Task HireWithoutACandidateAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().Today;

        var legalEntity = LegalEntity.Create("Dormant MChJ").Value;
        var employee = Employee.Create(FullName.Create("Dormantov", "Test", null).Value).Value;
        db.LegalEntities.Add(legalEntity);
        db.Employees.Add(employee);
        db.Employments.Add(Employment.Hire(employee.Id, legalEntity.Id, today).Value);
        db.Vacancies.Add(Vacancy.Open("Dormant vacancy", today).Value);

        await db.SaveChangesAsync(Ct);
    }
}
