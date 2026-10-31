using Bonvi.Application.Recruiting.Abstractions;
using Bonvi.Application.Recruiting.Dtos;
using Bonvi.Domain.Recruiting;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary>«Vakansiya so'rovlari» ro'yxati (docs/SPEC-vakansiya-sorovi.md).</summary>
/// <remarks>
/// So'rovlar oyiga o'nlab bo'ladi — bitta sahifa qatorlari uchun nomlar va nomzod sonlari
/// alohida kichik so'rovlar bilan yig'iladi (bog'liq ostki so'rovlar emas): har biri faqat
/// sahifadagi identifikatorlarni oladi.
/// </remarks>
public sealed class VacancyRequestReadModel : IVacancyRequestReadModel
{
    private readonly AppDbContext _context;

    public VacancyRequestReadModel(AppDbContext context) => _context = context;

    public async Task<VacancyRequestPage> SearchAsync(
        VacancyRequestFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = _context.VacancyRequests.AsNoTracking();

        if (filter.Status is { } status)
        {
            query = query.Where(request => request.Status == status);
        }

        if (filter.DepartmentIds is { } departmentIds)
        {
            var scoped = departmentIds.ToList();
            query = query.Where(request => scoped.Contains(request.DepartmentId));
        }

        if (filter.CreatedFrom is { } from)
        {
            query = query.Where(request => request.CreatedAt >= from);
        }

        if (filter.CreatedBefore is { } before)
        {
            query = query.Where(request => request.CreatedAt < before);
        }

        // Qidiruv: lavozim, bo'lim nomi yoki so'rovchi (xodim qidiruvining o'zi — lotin/kirill farqsiz).
        if (filter.Search is { } search)
        {
            var pattern = SearchPattern.For(search);
            var people = EmployeeSearchQuery.MatchingIds(_context, search);
            var departments = _context.Departments
                .Where(department => EF.Functions.ILike(department.Name, pattern, SearchPattern.Escape))
                .Select(department => department.Id);

            query = query.Where(request =>
                EF.Functions.ILike(request.Title, pattern, SearchPattern.Escape)
                || people.Contains(request.RequestedByEmployeeId)
                || departments.Contains(request.DepartmentId));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await query
            .OrderByDescending(request => request.CreatedAt)
            .ThenByDescending(request => request.Id)
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new VacancyRequestPage(await ToDtosAsync(rows, cancellationToken).ConfigureAwait(false), total);
    }

    public async Task<VacancyRequestDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _context.VacancyRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(request => request.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : (await ToDtosAsync([row], cancellationToken).ConfigureAwait(false))[0];
    }

    private async Task<IReadOnlyList<VacancyRequestDto>> ToDtosAsync(
        List<VacancyRequest> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var departmentIds = rows.Select(row => row.DepartmentId).Distinct().ToList();
        var employeeIds = rows.Select(row => row.RequestedByEmployeeId).Distinct().ToList();
        var userIds = rows.Where(row => row.DecidedBy is not null).Select(row => row.DecidedBy!.Value).Distinct().ToList();
        var vacancyIds = rows.Where(row => row.VacancyId is not null).Select(row => row.VacancyId!.Value).Distinct().ToList();

        // O'chirilgan bo'lim, xodim yoki foydalanuvchi ham nomi bilan ko'rinadi — so'rov tarix yozuvi.
        var departments = await _context.Departments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(department => departmentIds.Contains(department.Id))
            .Select(department => new { department.Id, department.Name, department.Path })
            .ToDictionaryAsync(department => department.Id, cancellationToken)
            .ConfigureAwait(false);

        var employees = await _context.Employees
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(employee => employeeIds.Contains(employee.Id))
            .Select(employee => new
            {
                employee.Id,
                employee.FullName.LastName,
                employee.FullName.FirstName,
                employee.FullName.MiddleName,
            })
            .ToDictionaryAsync(employee => employee.Id, cancellationToken)
            .ConfigureAwait(false);

        var users = userIds.Count == 0
            ? []
            : await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(user => userIds.Contains(user.Id))
                .Select(user => new { user.Id, user.FullName.LastName, user.FullName.FirstName, user.FullName.MiddleName })
                .ToDictionaryAsync(
                    user => user.Id,
                    user => FullName.FromTrusted(user.LastName, user.FirstName, user.MiddleName).Full,
                    cancellationToken)
                .ConfigureAwait(false);

        var vacancies = vacancyIds.Count == 0
            ? []
            : await _context.Vacancies
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(vacancy => vacancyIds.Contains(vacancy.Id))
                .Select(vacancy => new
                {
                    vacancy.Id,
                    vacancy.Title,
                    Candidates = _context.Candidates.Count(candidate => candidate.VacancyId == vacancy.Id),
                    Hired = _context.Candidates.Count(candidate =>
                        candidate.VacancyId == vacancy.Id && candidate.Status == CandidateStatus.Hired),
                })
                .ToDictionaryAsync(vacancy => vacancy.Id, cancellationToken)
                .ConfigureAwait(false);

        return
        [
            .. rows.Select(row =>
            {
                var department = departments.GetValueOrDefault(row.DepartmentId);
                var employee = employees.GetValueOrDefault(row.RequestedByEmployeeId);
                var vacancy = row.VacancyId is { } vacancyId ? vacancies.GetValueOrDefault(vacancyId) : null;

                return new VacancyRequestDto(
                    row.Id,
                    row.DepartmentId,
                    department?.Name ?? string.Empty,
                    department?.Path,
                    row.RequestedByEmployeeId,
                    employee is null
                        ? string.Empty
                        : FullName.FromTrusted(employee.LastName, employee.FirstName, employee.MiddleName).Full,
                    row.Title,
                    row.Openings,
                    row.NeededBy,
                    row.Note,
                    row.Status,
                    row.CreatedAt,
                    row.DecidedAt,
                    row.DecidedBy is { } decidedBy ? users.GetValueOrDefault(decidedBy) : null,
                    row.DecisionNote,
                    row.VacancyId,
                    vacancy?.Title,
                    vacancy?.Candidates ?? 0,
                    vacancy?.Hired ?? 0);
            }),
        ];
    }
}
