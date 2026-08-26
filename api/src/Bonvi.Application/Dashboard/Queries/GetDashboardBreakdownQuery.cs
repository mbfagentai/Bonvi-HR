using Bonvi.Application.Dashboard.Abstractions;
using Bonvi.Application.Dashboard.Dtos;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Dashboard.Queries;

/// <summary>Bosh sahifadagi bo'lim/yo'nalish taqsimotini so'rash — parametrsiz.</summary>
public sealed record GetDashboardBreakdownQuery;

/// <summary>
/// <see cref="GetDashboardBreakdownQuery"/> ni bajaradi.
/// </summary>
/// <remarks>
/// Bu handler faqat qatlamlar orasidagi chegarani saqlash uchun bor:
/// <c>Api</c> faqat <c>Application</c> ga bog'lanadi, <c>Infrastructure</c>
/// ga to'g'ridan-to'g'ri emas. Biznes qoida yo'q — sanashning o'zi
/// <see cref="IDashboardReadModel"/> da, bazada bajariladi.
/// </remarks>
public sealed class GetDashboardBreakdownQueryHandler
{
    private readonly IDashboardReadModel _dashboard;

    public GetDashboardBreakdownQueryHandler(IDashboardReadModel dashboard) => _dashboard = dashboard;

    public async Task<Result<DashboardBreakdownDto>> HandleAsync(
        GetDashboardBreakdownQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var breakdown = await _dashboard.GetBreakdownAsync(cancellationToken).ConfigureAwait(false);

        return new DashboardBreakdownDto(
            [.. breakdown.Departments.Select(ToDto)],
            [.. breakdown.Directions.Select(ToDto)]);
    }

    private static DashboardBreakdownItemDto ToDto(DashboardBreakdownItem item)
        => new(item.Id, item.Name, item.Count);
}
