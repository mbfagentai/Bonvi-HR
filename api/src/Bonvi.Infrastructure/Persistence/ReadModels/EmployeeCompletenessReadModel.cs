using Bonvi.Application.Employees.Completeness;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary>The «Hujjatlari chala» worklist: one composable SQL statement that works out each employee's gaps.</summary>
/// <remarks>
/// <para>
/// <b>Why raw SQL.</b> The sort is by how many things are missing, and one of the things is «each required document
/// type», a list the catalogue decides. In LINQ that is a set difference per employee inside an order-by. Here it is an
/// array built per row, and filter, count and sort read that array.
/// </para>
/// <para>
/// <b>One person, one row</b>, as in the employee list. A person with two current employments shows the primary one (or
/// the latest hired), but a hire order, a salary or a review flag missing on either of them counts: the card lists them
/// all, and hiding the second would leave it unfinished forever.
/// </para>
/// <para>
/// Raw SQL skips the global soft-delete filter, so every table states <c>deleted_at IS NULL</c> itself.
/// </para>
/// </remarks>
internal sealed class EmployeeCompletenessReadModel : IEmployeeCompletenessReadModel
{
    private readonly AppDbContext _context;
    private readonly IDepartmentRepository _departments;

    public EmployeeCompletenessReadModel(AppDbContext context, IDepartmentRepository departments)
    {
        _context = context;
        _departments = departments;
    }

    public async Task<IReadOnlyList<RequiredDocumentKind>> ListRequiredDocumentTypesAsync(CancellationToken cancellationToken = default)
        => await _context.DocumentTypes
            .AsNoTracking()
            .Where(type => type.IsActive && type.IsRequired)
            .OrderBy(type => type.Name)
            .ThenBy(type => type.Id)
            .Select(type => new RequiredDocumentKind(type.Id, type.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<EmployeeCompletenessPage> ListAsync(
        EmployeeCompletenessFilter filter,
        string? missing,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var rows = (await RowsAsync(filter, cancellationToken).ConfigureAwait(false))
            .Where(row => row.Missing.Length > 0);

        if (missing is not null)
        {
            rows = rows.Where(row => row.Missing.Contains(missing));
        }

        var total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await rows
            .OrderByDescending(row => row.Missing.Length)
            .ThenByDescending(row => row.HireDate)
            .ThenBy(row => row.LastName)
            .ThenBy(row => row.FirstName)
            .ThenBy(row => row.EmployeeId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new EmployeeCompletenessPage(
            [
                .. items.Select(row => new EmployeeCompletenessRow(
                    row.EmployeeId,
                    row.LastName,
                    row.FirstName,
                    row.MiddleName,
                    row.Department,
                    row.Position,
                    row.HireDate,
                    row.HasPhoto,
                    row.Missing)),
            ],
            total);
    }

    public async Task<EmployeeCompletenessCounts> CountAsync(
        EmployeeCompletenessFilter filter,
        CancellationToken cancellationToken = default)
    {
        var rows = await RowsAsync(filter, cancellationToken).ConfigureAwait(false);

        var totals = await rows
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Employees = group.Count(),
                WithGaps = group.Count(row => row.Missing.Length > 0),
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var byKind = await rows
            .SelectMany(row => row.Missing)
            .GroupBy(code => code)
            .Select(group => new { Code = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Code, item => item.Count, cancellationToken)
            .ConfigureAwait(false);

        return new EmployeeCompletenessCounts(totals?.Employees ?? 0, totals?.WithGaps ?? 0, byKind);
    }

    public async Task<int> CountWithGapsAsync(bool includeSalary, CancellationToken cancellationToken = default)
    {
        var rows = await RowsAsync(new EmployeeCompletenessFilter(null, null, includeSalary), cancellationToken).ConfigureAwait(false);

        return await rows.CountAsync(row => row.Missing.Length > 0, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IQueryable<GapRow>> RowsAsync(EmployeeCompletenessFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var hasBranch = filter.DepartmentId is not null;
        var branch = Array.Empty<Guid>();

        if (filter.DepartmentId is { } departmentId)
        {
            var descendants = await _departments.GetDescendantIdsAsync(departmentId, cancellationToken).ConfigureAwait(false);
            branch = [departmentId, .. descendants];
        }

        var terminated = (int)EmploymentStatus.Terminated;
        var hire = (int)EmploymentEventType.Hire;
        var includeSalary = filter.IncludeSalary;

        // The literals in the array are the codes of EmployeeCompletenessKinds; EmployeeCompletenessApiTests pins them.
        // An order number counts on the employment or on its hire record: the record is where HR can still enter one
        // (the employment's own column is written only at hire), and 25 imported people already have it only there.
        // A department filter narrows which employment is shown, as in the list: «Sotuv» shows the Sotuv job of someone
        // whose primary one is elsewhere.
        var rows = _context.Database.SqlQuery<GapRow>($"""
            WITH current_jobs AS (
                SELECT m.employee_id,
                       bool_or(m.needs_review) AS review,
                       bool_or((m.hire_order_no IS NULL OR btrim(m.hire_order_no) = '')
                           AND NOT EXISTS (
                               SELECT 1
                               FROM employment_events h
                               WHERE h.employment_id = m.id AND h.type = {hire}
                                 AND h.order_no IS NOT NULL AND btrim(h.order_no) <> '')) AS no_order,
                       bool_or(m.salary_amount IS NULL) AS no_salary
                FROM employments m
                WHERE m.deleted_at IS NULL AND m.status <> {terminated}
                GROUP BY m.employee_id
            ),
            shown AS (
                SELECT DISTINCT ON (m.employee_id) m.employee_id, m.department_id, m.position_id, m.hire_date
                FROM employments m
                WHERE m.deleted_at IS NULL
                  AND m.status <> {terminated}
                  AND (NOT {hasBranch} OR m.department_id = ANY({branch}))
                ORDER BY m.employee_id, m.is_primary DESC, m.hire_date DESC, m.id DESC
            ),
            required AS (
                SELECT t.id, t.name
                FROM document_types t
                WHERE t.deleted_at IS NULL AND t.is_active AND t.is_required
            )
            SELECT e.id AS employee_id,
                   e.last_name AS last_name,
                   e.first_name AS first_name,
                   e.middle_name AS middle_name,
                   d.name AS department,
                   p.name AS position,
                   s.hire_date AS hire_date,
                   (e.photo_key IS NOT NULL AND e.photo_key <> '') AS has_photo,
                   array_remove(ARRAY[
                       CASE WHEN e.pinfl IS NULL OR btrim(e.pinfl) = '' THEN 'pinfl' END,
                       CASE WHEN e.passport IS NULL OR btrim(e.passport) = '' THEN 'passport' END,
                       CASE WHEN e.photo_key IS NULL OR e.photo_key = '' THEN 'photo' END,
                       CASE WHEN e.phone IS NULL OR btrim(e.phone) = '' THEN 'phone' END,
                       CASE WHEN e.address IS NULL OR btrim(e.address) = '' THEN 'address' END,
                       CASE WHEN c.no_order THEN 'orderNo' END,
                       CASE WHEN e.needs_review OR c.review THEN 'needsReview' END,
                       CASE WHEN {includeSalary} AND c.no_salary THEN 'salary' END
                   ]::text[], NULL)
                   || ARRAY(
                       SELECT 'doc:' || r.id::text
                       FROM required r
                       WHERE NOT EXISTS (
                           SELECT 1
                           FROM employee_documents x
                           WHERE x.employee_id = e.id AND x.document_type_id = r.id AND x.deleted_at IS NULL)
                       ORDER BY r.name, r.id
                   )::text[] AS missing
            FROM employees e
            JOIN current_jobs c ON c.employee_id = e.id
            JOIN shown s ON s.employee_id = e.id
            LEFT JOIN departments d ON d.id = s.department_id AND d.deleted_at IS NULL
            LEFT JOIN positions p ON p.id = s.position_id AND p.deleted_at IS NULL
            WHERE e.deleted_at IS NULL
            """);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var matching = EmployeeSearchQuery.MatchingIds(_context, filter.Search);
            rows = rows.Where(row => matching.Contains(row.EmployeeId));
        }

        return rows;
    }

    /// <summary>The shape of the statement above. Its aliases are these names in the project's snake_case convention: EF composes over them by the mapped column name.</summary>
    private sealed class GapRow
    {
        public Guid EmployeeId { get; init; }

        public string LastName { get; init; } = string.Empty;

        public string FirstName { get; init; } = string.Empty;

        public string? MiddleName { get; init; }

        public string? Department { get; init; }

        public string? Position { get; init; }

        public DateOnly HireDate { get; init; }

        public bool HasPhoto { get; init; }

        public string[] Missing { get; init; } = [];
    }
}
