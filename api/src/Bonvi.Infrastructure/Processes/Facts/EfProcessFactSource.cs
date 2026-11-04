using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Processes.Facts;

/// <summary>
/// EF implementation of <see cref="IProcessFactSource"/> (docs/SPEC-mening-ishlarim.md §7.2).
/// </summary>
/// <remarks>
/// <para>
/// One query per fact family per call, never per subject: each family loads the rows of ALL its
/// subjects with <c>ids.Contains(…)</c> and the facts are assembled in memory. The families live in
/// the partial files <c>EfProcessFactSource.*.cs</c>; blank rows and their JSON readers in
/// <see cref="BlankFactRows"/>.
/// </para>
/// <para>
/// Everything is <c>AsNoTracking</c> and read-only. Soft-deleted rows stay hidden by the global
/// filter: a deleted subject simply has no facts (no <c>vr.created</c> / <c>cand.status</c> /
/// <c>emp.hired</c>) — that is how the engine sees «subject deleted».
/// </para>
/// </remarks>
public sealed partial class EfProcessFactSource : IProcessFactSource
{
    private readonly AppDbContext _context;

    public EfProcessFactSource(AppDbContext context) => _context = context;

    public async Task<ProcessFactSnapshot> LoadAsync(ProcessFactQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var vacancyRequests = await VacancyRequestFactsAsync(Distinct(query.VacancyRequestIds), cancellationToken)
            .ConfigureAwait(false);

        var candidates = await CandidateFactsAsync(
                query.Candidates.Distinct().ToList(),
                query.StageBindings,
                cancellationToken)
            .ConfigureAwait(false);

        var employeeScoped = await EmployeeScopedFactsAsync(
                Distinct(query.EmploymentIds),
                Distinct(query.ProbationIds),
                Distinct(query.EmployeeIds),
                cancellationToken)
            .ConfigureAwait(false);

        return new ProcessFactSnapshot(
            vacancyRequests,
            candidates,
            employeeScoped.Employments,
            employeeScoped.Probations,
            employeeScoped.Employees);
    }

    /// <summary>
    /// Employments, probations and employees share one employee-blank query and one attendance
    /// query, so they are loaded together.
    /// </summary>
    private async Task<EmployeeScopedFacts> EmployeeScopedFactsAsync(
        Guid[] employmentIds,
        Guid[] probationIds,
        Guid[] employeeIds,
        CancellationToken cancellationToken)
    {
        var employments = await LoadEmploymentsAsync(employmentIds, cancellationToken).ConfigureAwait(false);
        var probations = await LoadProbationsAsync(probationIds, cancellationToken).ConfigureAwait(false);

        var blankEmployees = employments.Select(row => row.EmployeeId)
            .Concat(probations.Select(row => row.EmployeeId))
            .Distinct()
            .ToArray();

        var blanks = await BlankFactRows.ForEmployeesAsync(
                _context,
                [ProcessBlanks.F06, ProcessBlanks.F07, ProcessBlanks.F08, ProcessBlanks.F09, ProcessBlanks.F11],
                blankEmployees,
                cancellationToken)
            .ConfigureAwait(false);

        var attendanceEmployees = employeeIds.Concat(employments.Select(row => row.EmployeeId)).Distinct().ToArray();
        var attendance = await AttendanceSinceAsync(attendanceEmployees, cancellationToken).ConfigureAwait(false);

        return new EmployeeScopedFacts(
            EmploymentFacts(employments, blanks, attendance),
            ProbationFacts(probations, blanks),
            EmployeeFacts(employeeIds, attendance));
    }

    private sealed record EmployeeScopedFacts(
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, FactValue>> Employments,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, FactValue>> Probations,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, FactValue>> Employees);

    // ────────────────────────────────────────────────────────────
    //  Shared helpers
    // ────────────────────────────────────────────────────────────

    private static Guid[] Distinct(IReadOnlyCollection<Guid> ids) => ids.Where(id => id != Guid.Empty).Distinct().ToArray();

    private static Dictionary<string, FactValue> NewFacts() => new(StringComparer.Ordinal);

    private static FactValue Holds(DateTimeOffset? at, string? value = null, Guid? linkedId = null)
        => new(true, at, value, linkedId);

    /// <summary>Start of a Tashkent calendar day as an instant (UTC offset, safe for SQL parameters).</summary>
    private static DateTimeOffset TashkentStartOf(DateOnly date)
        => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), BonviTime.TashkentOffset).ToUniversalTime();

    private static string IsoDate(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static string StatusNumber<TEnum>(TEnum status)
        where TEnum : struct, Enum
        => Convert.ToInt32(status, System.Globalization.CultureInfo.InvariantCulture)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The latest-created blank of <paramref name="templateCode"/> among <paramref name="rows"/>.</summary>
    private static BlankFactRow? Latest(IEnumerable<BlankFactRow> rows, string templateCode)
        => rows.Where(row => row.TemplateCode == templateCode)
            .OrderByDescending(row => row.CreatedAt)
            .ThenByDescending(row => row.Id)
            .FirstOrDefault();

    /// <summary><c>&lt;prefix&gt;&lt;stepId&gt;</c> for every Signed step of the blank.</summary>
    private static void AddSignedSteps(
        Dictionary<string, FactValue> facts,
        BlankFactRow blank,
        Func<string, string> factName,
        Func<BlankFactStep, string?>? value = null)
    {
        foreach (var step in blank.Steps.Where(step => step.State == BlankStepState.Signed))
        {
            facts[factName(step.StepCode)] = Holds(step.ActedAt, value is null ? step.Decision : value(step), blank.Id);
        }
    }

    /// <summary>
    /// All of <paramref name="stepCodes"/> present on the blank are Signed or Skipped, and at least one
    /// is Signed. At: the last act.
    /// </summary>
    private static FactValue AllSigned(BlankFactRow blank, IReadOnlyList<string> stepCodes)
    {
        var steps = blank.Steps.Where(step => stepCodes.Contains(step.StepCode, StringComparer.Ordinal)).ToList();
        var complete = steps.Exists(step => step.State == BlankStepState.Signed)
            && steps.TrueForAll(step => step.State is BlankStepState.Signed or BlankStepState.Skipped);

        return complete
            ? Holds(steps.Max(step => step.ActedAt), linkedId: blank.Id)
            : FactValue.NotHeld;
    }
}
