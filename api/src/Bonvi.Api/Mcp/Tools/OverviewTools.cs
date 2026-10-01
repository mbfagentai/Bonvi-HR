using System.ComponentModel;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Identity;
using Bonvi.Domain.Recruiting;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace Bonvi.Api.Mcp.Tools;

/// <summary>Umumiy ko'rinish — bitta chaqiruvda butun tizimning holati.</summary>
[McpServerToolType]
public sealed class OverviewTools(McpToolContext t)
{
    [McpServerTool(Name = "overview", Title = "Umumiy ko'rinish",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Kompaniya bo'yicha qisqa holat: faol xodimlar soni (yuridik shaxs va bo'lim bo'yicha), sinov muddatidagilar, "
        + "shu oy ishga kirgan va ketganlar, ochiq vakansiyalar va jarayondagi nomzodlar; davomat ruxsati bo'lsa — bugungi davomat. "
        + "Suhbatni shu vositadan boshlash qulay. Company-wide snapshot: headcount, hires/terminations this month, open vacancies, today's attendance.")]
    public async Task<string> Overview(CancellationToken cancellationToken = default)
    {
        t.Require(Permissions.EmployeesView, "Xodimlar");

        var db = t.Db;
        var today = McpToolContext.Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var employments = await db.Employments
            .Where(employment => employment.Status != EmploymentStatus.Terminated)
            .Select(employment => new { employment.EmployeeId, employment.LegalEntityId, employment.DepartmentId, employment.Status })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var legalEntities = await db.LegalEntities
            .Select(entity => new { entity.Id, entity.Name })
            .ToDictionaryAsync(entity => entity.Id, entity => entity.Name, cancellationToken)
            .ConfigureAwait(false);

        var departments = await db.Departments
            .Select(department => new { department.Id, department.Name })
            .ToDictionaryAsync(department => department.Id, department => department.Name, cancellationToken)
            .ConfigureAwait(false);

        var hiredThisMonth = await db.Employments
            .CountAsync(employment => employment.HireDate >= monthStart && employment.HireDate <= today, cancellationToken)
            .ConfigureAwait(false);

        var terminatedThisMonth = await db.Employments
            .CountAsync(employment => employment.TerminationDate >= monthStart && employment.TerminationDate <= today, cancellationToken)
            .ConfigureAwait(false);

        object? recruiting = null;

        if (t.Has(Permissions.RecruitingView))
        {
            var openVacancies = await db.Vacancies
                .Where(vacancy => vacancy.Status == VacancyStatus.Open)
                .Select(vacancy => new { vacancy.Title, vacancy.Openings })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var candidates = await db.Candidates
                .GroupBy(candidate => candidate.Status)
                .Select(group => new { Status = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            recruiting = new
            {
                openVacancies = openVacancies.Count,
                openPositions = openVacancies.Sum(vacancy => vacancy.Openings),
                candidatesByStatus = candidates.ToDictionary(item => item.Status.ToString(), item => item.Count),
            };
        }

        object? attendance = null;

        if (t.Has(Permissions.AttendanceView))
        {
            var days = await db.AttendanceDays
                .Where(day => day.LocalDate == today)
                .GroupBy(day => day.Status)
                .Select(group => new { Status = group.Key, Count = group.Count(), Late = group.Count(day => day.LateMinutes > 0) })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            attendance = new
            {
                date = McpToolContext.Iso(today),
                byStatus = days.ToDictionary(item => item.Status.ToString(), item => item.Count),
                late = days.Sum(item => item.Late),
                note = days.Count == 0 ? "Bugun uchun davomat hali hisoblanmagan." : null,
            };
        }

        var activeEmployees = employments.Select(employment => employment.EmployeeId).Distinct().Count();

        return t.Json(
            new
            {
                today = McpToolContext.Iso(today),
                activeEmployees,
                onProbation = employments.Where(employment => employment.Status == EmploymentStatus.Probation)
                    .Select(employment => employment.EmployeeId).Distinct().Count(),
                byLegalEntity = employments
                    .GroupBy(employment => legalEntities.GetValueOrDefault(employment.LegalEntityId, "?"))
                    .OrderByDescending(group => group.Count())
                    .Select(group => new { legalEntity = group.Key, employees = group.Select(item => item.EmployeeId).Distinct().Count() }),
                byDepartment = employments
                    .GroupBy(employment => employment.DepartmentId is { } id ? departments.GetValueOrDefault(id, "?") : "(bo'limsiz)")
                    .OrderByDescending(group => group.Count())
                    .Select(group => new { department = group.Key, employees = group.Select(item => item.EmployeeId).Distinct().Count() }),
                thisMonth = new { from = McpToolContext.Iso(monthStart), hired = hiredThisMonth, terminated = terminatedThisMonth },
                recruiting,
                attendance,
            },
            activeEmployees);
    }
}
