using Bonvi.Application.Blanks.Definitions;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Blanks;

/// <summary>
/// Oldindan to'ldirish manbasi (SPEC-blankalar §4.6) — xodim kartasi, ish joyi, bo'lim boshlig'i,
/// sinov muddati va HR-F13 tarixi. Qoralama yaratilganda BIR MARTA chaqiriladi; ma'lumot yo'q kalit
/// natijaga kirmaydi (maydon bo'sh qoladi).
/// </summary>
public sealed class BlankPrefillSource : IBlankPrefillSource
{
    /// <summary>«Avvalgi tushuntirish xatlari» hisobi shu shablon bo'yicha (§4.6).</summary>
    public const string ExplanationTemplateCode = "HR-F13";

    private readonly AppDbContext _context;

    public BlankPrefillSource(AppDbContext context) => _context = context;

    public async Task<IReadOnlyDictionary<string, BlankPrefillValue>> ResolveAsync(
        BlankPrefillContext context,
        IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(keys);

        var wanted = keys.ToHashSet(StringComparer.Ordinal);
        var values = new Dictionary<string, BlankPrefillValue>(StringComparer.Ordinal);

        if (wanted.Contains(BlankPrefillKeys.Today))
        {
            values[BlankPrefillKeys.Today] = new BlankPrefillValue(Date: context.Today);
        }

        if (wanted.Contains(BlankPrefillKeys.CurrentUserEmployeeId)
            && context.CurrentUserEmployeeId is { } me
            && await EmployeeNameAsync(me, cancellationToken).ConfigureAwait(false) is { } myName)
        {
            values[BlankPrefillKeys.CurrentUserEmployeeId] = new BlankPrefillValue(myName, me);
        }

        if (context.SubjectEmployeeId is { } employeeId)
        {
            await EmployeeAsync(employeeId, context.Today, wanted, values, cancellationToken).ConfigureAwait(false);
        }
        else if (context.SubjectCandidateId is { } candidateId && wanted.Contains(BlankPrefillKeys.SubjectFullName))
        {
            var candidate = await _context.Candidates
                .AsNoTracking()
                .Where(row => row.Id == candidateId)
                .Select(row => new { row.FullName.LastName, row.FullName.FirstName, row.FullName.MiddleName })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (candidate is not null)
            {
                values[BlankPrefillKeys.SubjectFullName] = new BlankPrefillValue(
                    FullName.FromTrusted(candidate.LastName, candidate.FirstName, candidate.MiddleName).Full,
                    candidateId);
            }
        }

        return values;
    }

    /// <summary>Faol ish joyi (bir nechta bo'lsa asosiysi, keyin eng yangisi); faoli yo'q bo'lsa — oxirgisi (§3.3).</summary>
    internal static Task<Employment?> CurrentEmploymentAsync(AppDbContext context, Guid employeeId, CancellationToken cancellationToken)
        => context.Employments
            .AsNoTracking()
            .Where(employment => employment.EmployeeId == employeeId)
            .OrderBy(employment => employment.Status == EmploymentStatus.Terminated ? 1 : 0)
            .ThenByDescending(employment => employment.IsPrimary)
            .ThenByDescending(employment => employment.HireDate)
            .ThenByDescending(employment => employment.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task EmployeeAsync(
        Guid employeeId,
        DateOnly today,
        HashSet<string> wanted,
        Dictionary<string, BlankPrefillValue> values,
        CancellationToken cancellationToken)
    {
        var employee = await _context.Employees
            .AsNoTracking()
            .Where(row => row.Id == employeeId)
            .Select(row => new { row.FullName.LastName, row.FullName.FirstName, row.FullName.MiddleName, row.DirectionIds })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (employee is null)
        {
            return;
        }

        values[BlankPrefillKeys.SubjectFullName] =
            new BlankPrefillValue(FullName.FromTrusted(employee.LastName, employee.FirstName, employee.MiddleName).Full, employeeId);

        if (wanted.Contains(BlankPrefillKeys.SubjectDirectionName) && employee.DirectionIds.Count > 0)
        {
            var directionIds = employee.DirectionIds.ToList();
            var names = await _context.Directions
                .AsNoTracking()
                .Where(direction => directionIds.Contains(direction.Id))
                .OrderBy(direction => direction.Name)
                .Select(direction => direction.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (names.Count > 0)
            {
                values[BlankPrefillKeys.SubjectDirectionName] = new BlankPrefillValue(string.Join(", ", names));
            }
        }

        if (wanted.Contains(BlankPrefillKeys.SubjectPriorF13Count12m))
        {
            var since = new DateTimeOffset(today.AddMonths(-12).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(5));
            var count = await _context.BlankSubmissions
                .AsNoTracking()
                .CountAsync(
                    submission => submission.TemplateCode == ExplanationTemplateCode
                        && submission.EmployeeId == employeeId
                        && submission.Status == BlankSubmissionStatus.Approved
                        && submission.CompletedAt >= since,
                    cancellationToken)
                .ConfigureAwait(false);
            values[BlankPrefillKeys.SubjectPriorF13Count12m] = new BlankPrefillValue(Number: count);
        }

        var employment = await CurrentEmploymentAsync(_context, employeeId, cancellationToken).ConfigureAwait(false);
        if (employment is null)
        {
            return;
        }

        values[BlankPrefillKeys.SubjectHireDate] = new BlankPrefillValue(Date: employment.HireDate);
        if (employment.TerminationDate is { } terminated)
        {
            values[BlankPrefillKeys.SubjectTerminationDate] = new BlankPrefillValue(Date: terminated);
        }

        if (employment.PositionId is { } positionId
            && await _context.Positions.IgnoreQueryFilters().AsNoTracking()
                .Where(position => position.Id == positionId)
                .Select(position => position.Name)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false) is { } positionName)
        {
            values[BlankPrefillKeys.SubjectPositionName] = new BlankPrefillValue(positionName);
        }

        if (employment.DepartmentId is { } departmentId)
        {
            var department = await _context.Departments.IgnoreQueryFilters().AsNoTracking()
                .Where(row => row.Id == departmentId)
                .Select(row => new { row.Name, row.HeadEmployeeId })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (department is not null)
            {
                values[BlankPrefillKeys.SubjectDepartmentName] = new BlankPrefillValue(department.Name);
                if (department.HeadEmployeeId is { } headId
                    && await EmployeeNameAsync(headId, cancellationToken).ConfigureAwait(false) is { } headName)
                {
                    values[BlankPrefillKeys.SubjectDepartmentHeadId] = new BlankPrefillValue(headName, headId);
                }
            }
        }

        if (wanted.Contains(BlankPrefillKeys.SubjectProbationEndDate))
        {
            var probationEnd = await _context.Probations
                .AsNoTracking()
                .Where(probation => probation.EmploymentId == employment.Id)
                .OrderByDescending(probation => probation.EndDate)
                .Select(probation => (DateOnly?)probation.EndDate)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (probationEnd is { } end)
            {
                values[BlankPrefillKeys.SubjectProbationEndDate] = new BlankPrefillValue(Date: end);
            }
        }
    }

    private async Task<string?> EmployeeNameAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var name = await _context.Employees
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => row.Id == employeeId)
            .Select(row => new { row.FullName.LastName, row.FullName.FirstName, row.FullName.MiddleName })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return name is null ? null : FullName.FromTrusted(name.LastName, name.FirstName, name.MiddleName).Full;
    }
}
