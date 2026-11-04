using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Processes.Facts;

/// <summary>
/// <c>emp.*</c>, <c>dis.*</c>, <c>prob.*</c> — queries: employments, probations, the employee blanks
/// (F06 F07 F08 F09 F11 in one statement), attendance identities, device enrolments.
/// </summary>
public sealed partial class EfProcessFactSource
{
    /// <summary>An HR-F06/F07 belongs to a probation when created at most this long before its start.</summary>
    private const int ProbationBlankLeadDays = 7;

    private async Task<List<EmploymentRow>> LoadEmploymentsAsync(Guid[] ids, CancellationToken cancellationToken)
        => ids.Length == 0
            ? []
            : await _context.Employments.AsNoTracking()
                .Where(employment => ids.Contains(employment.Id))
                .Select(employment => new EmploymentRow(
                    employment.Id,
                    employment.EmployeeId,
                    employment.Status,
                    employment.HireOrderNo,
                    employment.TerminationDate,
                    employment.CreatedAt,
                    employment.UpdatedAt))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

    private async Task<List<ProbationRow>> LoadProbationsAsync(Guid[] ids, CancellationToken cancellationToken)
        => ids.Length == 0
            ? []
            : await (
                    from probation in _context.Probations.AsNoTracking()
                    where ids.Contains(probation.Id)
                    join employment in _context.Employments.AsNoTracking() on probation.EmploymentId equals employment.Id
                    select new ProbationRow(
                        probation.Id,
                        employment.EmployeeId,
                        probation.StartDate,
                        probation.Status,
                        probation.ResolvedAt,
                        probation.UpdatedAt ?? probation.CreatedAt))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

    /// <summary>
    /// The earliest moment each employee became visible to the terminals: a live (not soft-deleted)
    /// <c>attendance_identities</c> row (<c>created_at</c>) or a <c>device_enrollments</c> row with
    /// status 1 Synced (<c>synced_at</c>, fallback <c>created_at</c>).
    /// </summary>
    private async Task<Dictionary<Guid, DateTimeOffset>> AttendanceSinceAsync(Guid[] employeeIds, CancellationToken cancellationToken)
    {
        var since = new Dictionary<Guid, DateTimeOffset>();
        if (employeeIds.Length == 0)
        {
            return since;
        }

        var identities = await _context.AttendanceIdentities.AsNoTracking()
            .Where(identity => employeeIds.Contains(identity.EmployeeId))
            .GroupBy(identity => identity.EmployeeId)
            .Select(group => new { EmployeeId = group.Key, At = group.Min(identity => identity.CreatedAt) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var enrollments = await _context.DeviceEnrollments.AsNoTracking()
            .Where(row => employeeIds.Contains(row.EmployeeId) && row.Status == DeviceEnrollmentStatus.Synced)
            .GroupBy(row => row.EmployeeId)
            .Select(group => new { EmployeeId = group.Key, At = group.Min(row => row.SyncedAt ?? row.CreatedAt) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in identities.Concat(enrollments))
        {
            since[row.EmployeeId] = since.TryGetValue(row.EmployeeId, out var known) && known <= row.At ? known : row.At;
        }

        return since;
    }

    private static Dictionary<Guid, IReadOnlyDictionary<string, FactValue>> EmploymentFacts(
        List<EmploymentRow> employments,
        List<BlankFactRow> blanks,
        Dictionary<Guid, DateTimeOffset> attendance)
    {
        var result = new Dictionary<Guid, IReadOnlyDictionary<string, FactValue>>();
        var blanksByEmployee = blanks.ToLookup(row => row.EmployeeId!.Value);

        foreach (var employment in employments)
        {
            var facts = NewFacts();

            facts[ProcessFacts.EmpHired] = Holds(employment.CreatedAt, linkedId: employment.Id);

            facts[ProcessFacts.EmpHireOrder] = string.IsNullOrWhiteSpace(employment.HireOrderNo)
                ? FactValue.NotHeld
                : Holds(employment.UpdatedAt ?? employment.CreatedAt, employment.HireOrderNo.Trim(), employment.Id);

            facts[ProcessFacts.EmpAttendance] = attendance.TryGetValue(employment.EmployeeId, out var enrolledAt)
                ? Holds(enrolledAt)
                : FactValue.NotHeld;

            facts[ProcessFacts.DisTerminated] = employment.Status == EmploymentStatus.Terminated
                ? Holds(
                    employment.UpdatedAt ?? employment.CreatedAt,
                    employment.TerminationDate is { } terminated ? IsoDate(terminated) : null,
                    employment.Id)
                : FactValue.NotHeld;

            var ofEmployment = blanksByEmployee[employment.EmployeeId].Where(blank => BelongsTo(blank, employment)).ToList();

            // ── HR-F11 ──
            facts[ProcessFacts.EmpF11] = Latest(ofEmployment, ProcessBlanks.F11) is { } introduction
                ? AllSigned(introduction, ProcessBlanks.F11Steps)
                : FactValue.NotHeld;

            // ── HR-F09, kind = dismissal ──
            var act = Latest(
                ofEmployment.Where(blank => BlankJson.Choice(blank.AnswersJson["kind"]) == ProcessBlanks.F09DismissalKind),
                ProcessBlanks.F09);

            facts[ProcessFacts.DisF09Opened] = act is null ? FactValue.NotHeld : Holds(act.CreatedAt, linkedId: act.Id);
            facts[ProcessFacts.DisF09Approved] = act is { Status: BlankSubmissionStatus.Approved }
                ? Holds(act.CompletedAt, linkedId: act.Id)
                : FactValue.NotHeld;

            if (act is not null)
            {
                AddSignedSteps(facts, act, ProcessFacts.DisF09Step);
            }

            // ── HR-F08 (exit interview; Q13 needs the main K-code) ──
            var exit = Latest(ofEmployment, ProcessBlanks.F08);
            var kCode = exit is null ? null : BlankJson.Choice(exit.AnswersJson["mainReason"]);

            facts[ProcessFacts.DisF08Approved] = exit switch
            {
                { Status: BlankSubmissionStatus.Approved } => Holds(exit.CompletedAt, kCode, exit.Id),
                { Status: BlankSubmissionStatus.InApproval } when kCode is not null => Holds(
                    exit.Steps.Max(step => step.ActedAt) ?? exit.SubmittedAt ?? exit.CreatedAt,
                    kCode,
                    exit.Id),
                _ => FactValue.NotHeld,
            };

            if (exit is not null && BlankJson.Date(exit.AnswersJson["leaveDate"]) is { } leaveDate)
            {
                facts[ProcessFacts.DisF08LeaveDate] = Holds(exit.CreatedAt, leaveDate, exit.Id);
            }

            result[employment.Id] = facts;
        }

        return result;
    }

    private static Dictionary<Guid, IReadOnlyDictionary<string, FactValue>> ProbationFacts(
        List<ProbationRow> probations,
        List<BlankFactRow> blanks)
    {
        var result = new Dictionary<Guid, IReadOnlyDictionary<string, FactValue>>();
        var blanksByEmployee = blanks.ToLookup(row => row.EmployeeId!.Value);

        foreach (var probation in probations)
        {
            var facts = NewFacts();
            var windowStart = TashkentStartOf(probation.StartDate.AddDays(-ProbationBlankLeadDays));
            var ofProbation = blanksByEmployee[probation.EmployeeId].Where(blank => blank.CreatedAt >= windowStart).ToList();

            // ── HR-F06 ──
            if (Latest(ofProbation, ProcessBlanks.F06) is { } plan)
            {
                facts[ProcessFacts.ProbF06] = BlankJson.RefId(plan.AnswersJson["mentor"]) is not null
                    ? Holds(plan.SubmittedAt ?? plan.CreatedAt, linkedId: plan.Id)
                    : FactValue.NotHeld;

                facts[ProcessFacts.ProbF06Group1] = AllSigned(plan, ProcessBlanks.F06Group1Steps);

                AddSignedSteps(facts, plan, ProcessFacts.ProbF06Step, step => step.StepCode switch
                {
                    "day7" => BlankJson.Flag(plan.ComputedJson["day7Pass"]),
                    "day85" => BlankJson.Flag(plan.ComputedJson["day85Pass"]),
                    _ => step.Decision,
                });
            }
            else
            {
                facts[ProcessFacts.ProbF06] = FactValue.NotHeld;
                facts[ProcessFacts.ProbF06Group1] = FactValue.NotHeld;
            }

            // ── HR-F07 ──
            if (Latest(ofProbation, ProcessBlanks.F07) is { } review)
            {
                facts[ProcessFacts.ProbF07Submitted] = review.Status is BlankSubmissionStatus.InApproval or BlankSubmissionStatus.Approved
                    ? Holds(review.SubmittedAt ?? review.CreatedAt, linkedId: review.Id)
                    : FactValue.NotHeld;

                AddSignedSteps(facts, review, ProcessFacts.ProbF07Step);
            }
            else
            {
                facts[ProcessFacts.ProbF07Submitted] = FactValue.NotHeld;
            }

            facts[ProcessFacts.ProbResolved] = probation.Status == ProbationStatus.InProgress
                ? FactValue.NotHeld
                : Holds(
                    probation.ResolvedAt is { } resolved ? TashkentStartOf(resolved) : probation.UpdatedAt,
                    StatusNumber(probation.Status),
                    probation.Id);

            result[probation.Id] = facts;
        }

        return result;
    }

    private static Dictionary<Guid, IReadOnlyDictionary<string, FactValue>> EmployeeFacts(
        Guid[] employeeIds,
        Dictionary<Guid, DateTimeOffset> attendance)
    {
        var result = new Dictionary<Guid, IReadOnlyDictionary<string, FactValue>>();

        foreach (var employeeId in employeeIds)
        {
            var facts = NewFacts();
            facts[ProcessFacts.EmpAttendance] = attendance.TryGetValue(employeeId, out var enrolledAt)
                ? Holds(enrolledAt)
                : FactValue.NotHeld;

            result[employeeId] = facts;
        }

        return result;
    }

    /// <summary>
    /// An employee blank belongs to this employment when it was opened on it
    /// (<c>blank_submissions.employment_id</c>), or — opened with no employment — after it began.
    /// Keeps a rehired person's old F08/F09/F11 off the new employment.
    /// </summary>
    private static bool BelongsTo(BlankFactRow blank, EmploymentRow employment)
        => blank.EmploymentId == employment.Id
            || (blank.EmploymentId is null && blank.CreatedAt >= employment.CreatedAt);

    private sealed record EmploymentRow(
        Guid Id,
        Guid EmployeeId,
        EmploymentStatus Status,
        string? HireOrderNo,
        DateOnly? TerminationDate,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    private sealed record ProbationRow(
        Guid Id,
        Guid EmployeeId,
        DateOnly StartDate,
        ProbationStatus Status,
        DateOnly? ResolvedAt,
        DateTimeOffset UpdatedAt);
}
