using Bonvi.Application.Notifications;
using Bonvi.Application.Notifications.Abstractions;
using Bonvi.Application.Notifications.Dtos;
using Bonvi.Domain.Notifications;

namespace Bonvi.Application.Tests.Notifications;

/// <summary>
/// Bo'lim boshlig'ining vakansiya so'rovi HR qarorini kutmoqda (docs/SPEC-vakansiya-sorovi.md §3).
/// Qaysi so'rov «kutmoqda» ekanini baza kesadi (<c>VacancyRequestApiTests</c>); bu yerda qatordan
/// yasalgan yozuv.
/// </summary>
public sealed class VacancyRequestNotificationTests
{
    private readonly NotificationTestContext _context = new();

    [Fact]
    public async Task Yozuv_sorovlar_royxatiga_olib_boradi_sariq_darajada()
    {
        var qator = new PendingVacancyRequestRow(
            Guid.NewGuid(),
            "Payvandchi",
            2,
            "Skuter yo'nalishi",
            "Nasritdinov Ilhomjon",
            new DateTimeOffset(2026, 8, 28, 19, 30, 0, TimeSpan.Zero));
        _context.Source.VacancyRequests.Add(qator);

        var natija = await _context.Collector.CollectAsync(TestContext.Current.CancellationToken);

        var item = natija.ShouldHaveSingleItem();
        item.Type.ShouldBe(NotificationType.VacancyRequested);
        item.Severity.ShouldBe(NotificationSeverity.Warning, "so'rov HR harakatini kutadi");
        item.Date.ShouldBe(new DateOnly(2026, 8, 29), "kun Toshkent kalendarida");
        item.Link.Route.ShouldBe(NotificationRoute.VacancyRequests);
        item.Link.Id.ShouldBe(qator.RequestId);

        item.Params[NotificationParams.Title].ShouldBe("Payvandchi");
        item.Params[NotificationParams.Openings].ShouldBe("2");
        item.Params[NotificationParams.DepartmentName].ShouldBe("Skuter yo'nalishi");
        item.Params[NotificationParams.EmployeeName].ShouldBe("Nasritdinov Ilhomjon");
    }

    [Fact]
    public void Turi_rekruting_korish_ruxsati_ostida()
        => NotificationAccess.RequiredPermission(NotificationType.VacancyRequested)
            .ShouldBe(Bonvi.Domain.Identity.Permissions.RecruitingView);
}
