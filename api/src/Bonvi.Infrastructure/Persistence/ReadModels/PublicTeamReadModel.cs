using Bonvi.Application.Attendance.Computation;
using Bonvi.Application.PublicApi.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary>
/// <see cref="IPublicTeamReadModel"/> — bo'lim davomati kalitining o'tishlar oqimi.
/// </summary>
internal sealed class PublicTeamReadModel : IPublicTeamReadModel
{
    private readonly AppDbContext _context;

    public PublicTeamReadModel(AppDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<PublicPassBatch> PassesReceivedAfterAsync(
        DateTimeOffset after,
        bool inclusive,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // Npgsql `timestamptz` ga faqat UTC parametr yozadi
        var from = after.ToUniversalTime();

        var passes = _context.AttendancePasses
            .AsNoTracking()
            .Where(pass => pass.ResolvedEmployeeId != null);

        passes = inclusive
            ? passes.Where(pass => pass.ReceivedAt >= from)
            : passes.Where(pass => pass.ReceivedAt > from);

        var rows = await Rows(passes, limit + 1, cancellationToken).ConfigureAwait(false);

        if (rows.Count <= limit)
        {
            return new PublicPassBatch(rows, HasMore: false);
        }

        // Oxirgi lahzaning guruhi bo'linmasin (interfeys izohi): chegaradagi lahza keyingi sahifaga
        var edge = rows[limit - 1].ReceivedAt;
        var whole = rows.Take(limit).Where(row => row.ReceivedAt < edge).ToList();

        if (whole.Count == 0)
        {
            // Butun sahifa bitta lahza — shu lahzaning HAMMASI olinadi (chegaradan oshsa ham)
            whole = [.. await Rows(passes.Where(pass => pass.ReceivedAt == edge), int.MaxValue, cancellationToken).ConfigureAwait(false)];
        }

        return new PublicPassBatch(whole, HasMore: true);
    }

    private async Task<IReadOnlyList<PublicPassRow>> Rows(
        IQueryable<AttendancePass> passes,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await (
                from pass in passes
                join employee in _context.Employees.AsNoTracking() on pass.ResolvedEmployeeId equals employee.Id
                orderby pass.ReceivedAt, pass.Id
                select new
                {
                    pass.Id,
                    EmployeeId = employee.Id,
                    employee.FullName.LastName,
                    employee.FullName.FirstName,
                    employee.FullName.MiddleName,
                    pass.OccurredAt,
                    pass.LocalDate,
                    pass.ReceivedAt,
                    pass.Direction,
                    pass.Method,
                })
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new PublicPassRow(
                row.Id,
                row.EmployeeId,
                FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
                row.OccurredAt,
                row.LocalDate,
                row.ReceivedAt,
                row.Direction,
                row.Method)),
        ];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, PassDirection>> EffectiveDirectionsAsync(
        IReadOnlyCollection<(Guid EmployeeId, DateOnly Date)> days,
        CancellationToken cancellationToken = default)
    {
        if (days.Count == 0)
        {
            return new Dictionary<Guid, PassDirection>();
        }

        var keys = days.ToHashSet();
        var employeeIds = keys.Select(key => key.EmployeeId).Distinct().ToList();
        var dates = keys.Select(key => key.Date).Distinct().ToList();

        // Keng olinadi (xodimlar × sanalar), keyin aniq juftlar qoldiriladi — `AttendanceReadModel` dagi naqsh
        var passes = await _context.AttendancePasses
            .AsNoTracking()
            .Where(pass =>
                pass.ResolvedEmployeeId != null &&
                employeeIds.Contains(pass.ResolvedEmployeeId.Value) &&
                dates.Contains(pass.LocalDate))
            .Select(pass => new { pass.Id, pass.ResolvedEmployeeId, pass.OccurredAt, pass.Direction, pass.LocalDate })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new Dictionary<Guid, PassDirection>();

        foreach (var day in passes.GroupBy(pass => (EmployeeId: pass.ResolvedEmployeeId!.Value, pass.LocalDate)))
        {
            if (!keys.Contains(day.Key))
            {
                continue;
            }

            var directions = PassPairing.EffectiveDirections(
                day.Select(pass => new PassReading(pass.Id, pass.ResolvedEmployeeId, pass.OccurredAt, pass.Direction)));

            foreach (var (passId, direction) in directions)
            {
                result[passId] = direction;
            }
        }

        return result;
    }
}
