using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Processes.Facts;

/// <summary>
/// <c>cand.*</c> — seven queries: candidates, bound stage entries, forms, hire events, candidate
/// blanks (F03, F05), HR-F04 councils, decisions on the council minutes page.
/// </summary>
/// <remarks>
/// Every candidate fact belongs to ONE application: only rows on/after its <c>appliedAt</c> count
/// (forms from one day before — the kiosk form may precede <c>reapplied_at</c>), so a reapplied
/// candidate never inherits the previous application's council, offer or hire.
/// </remarks>
public sealed partial class EfProcessFactSource
{
    private static readonly TimeSpan FormGrace = TimeSpan.FromDays(1);

    private async Task<IReadOnlyDictionary<CandidateFactSubject, IReadOnlyDictionary<string, FactValue>>> CandidateFactsAsync(
        List<CandidateFactSubject> subjects,
        IReadOnlyDictionary<string, Guid?> stageBindings,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<CandidateFactSubject, IReadOnlyDictionary<string, FactValue>>();
        var ids = subjects.Select(subject => subject.CandidateId).Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return result;
        }

        var candidates = await _context.Candidates.AsNoTracking()
            .Where(candidate => ids.Contains(candidate.Id))
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Status,
                AppliedAt = candidate.ReappliedAt ?? candidate.CreatedAt,
                UpdatedAt = candidate.UpdatedAt ?? candidate.CreatedAt,
            })
            .ToDictionaryAsync(row => row.Id, cancellationToken)
            .ConfigureAwait(false);

        // Subjects resolved to their application; a missing (deleted) candidate gets no facts.
        var applications = subjects
            .Where(subject => candidates.ContainsKey(subject.CandidateId))
            .Select(subject => (Subject: subject, AppliedAt: subject.AppliedAt ?? candidates[subject.CandidateId].AppliedAt))
            .ToList();

        if (applications.Count == 0)
        {
            return result;
        }

        var foundIds = applications.Select(row => row.Subject.CandidateId).Distinct().ToArray();
        var earliestApplication = applications.Min(row => row.AppliedAt);

        var bound = stageBindings
            .Where(binding => binding.Value is { } stage && stage != Guid.Empty)
            .ToDictionary(binding => binding.Key, binding => binding.Value!.Value, StringComparer.Ordinal);
        var boundStageIds = bound.Values.Distinct().ToArray();

        List<StageEntryRow> stageEntries = boundStageIds.Length == 0
            ? []
            : await _context.CandidateStageEntries.AsNoTracking()
                .Where(entry => foundIds.Contains(entry.CandidateId) && boundStageIds.Contains(entry.StageId))
                .Select(entry => new StageEntryRow(entry.CandidateId, entry.StageId, entry.RecordedAt))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var forms = await _context.CandidateForms.AsNoTracking()
            .Where(form => foundIds.Contains(form.CandidateId))
            .Select(form => new FormRow(form.CandidateId, form.Id, form.SubmittedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hires = await (
                from row in _context.EmploymentEvents.AsNoTracking()
                where row.CandidateId != null
                    && foundIds.Contains(row.CandidateId.Value)
                    && (row.Type == EmploymentEventType.Hire || row.Type == EmploymentEventType.Rehire)
                join employment in _context.Employments.AsNoTracking() on row.EmploymentId equals employment.Id
                select new HireRow(row.CandidateId!.Value, row.EmploymentId, employment.EmployeeId, row.RecordedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var candidateBlanks = await BlankFactRows.ForCandidatesAsync(
                _context,
                [ProcessBlanks.F03, ProcessBlanks.F05],
                foundIds,
                cancellationToken)
            .ConfigureAwait(false);

        var councils = await BlankFactRows.SubjectlessSinceAsync(_context, ProcessBlanks.F04, earliestApplication, cancellationToken)
            .ConfigureAwait(false);
        var councilRows = CouncilRows(councils, foundIds);

        // The minutes page (`/kengash-xulosasi`, debt D7 in SPEC-hujjat-manbai) holds the decision until HRMentor serves HR-F04.
        var pageDecisions = await _context.CouncilDecisions.AsNoTracking()
            .Where(row => foundIds.Contains(row.CandidateId))
            .Select(row => new PageDecisionRow(row.CandidateId, row.Id, row.Decision, row.CreatedAt, row.UpdatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var stagesByCandidate = stageEntries.ToLookup(row => row.CandidateId);
        var formsByCandidate = forms.ToLookup(row => row.CandidateId);
        var hiresByCandidate = hires.ToLookup(row => row.CandidateId);
        var blanksByCandidate = candidateBlanks.ToLookup(row => row.CandidateId!.Value);
        var councilsByCandidate = councilRows.ToLookup(row => row.CandidateId);
        var pageDecisionsByCandidate = pageDecisions.ToLookup(row => row.CandidateId);

        foreach (var (subject, appliedAt) in applications)
        {
            var id = subject.CandidateId;
            var candidate = candidates[id];
            var facts = NewFacts();

            facts[ProcessFacts.CandStatus] = Holds(candidate.UpdatedAt, StatusNumber(candidate.Status));

            // ── Stage bindings: cand.stage:<code>; the «contacted» binding is also cand.contacted ──
            foreach (var (code, stageId) in bound)
            {
                var recordedAt = stagesByCandidate[id]
                    .Where(row => row.StageId == stageId && row.RecordedAt >= appliedAt)
                    .Min(row => (DateTimeOffset?)row.RecordedAt);

                facts[ProcessFacts.CandStage(code)] = recordedAt is null ? FactValue.NotHeld : Holds(recordedAt);
            }

            facts[ProcessFacts.CandContacted] = facts.GetValueOrDefault(ProcessFacts.CandStage(StageBindings.Contacted))
                ?? FactValue.NotHeld;

            // ── HR-F02 (candidate_forms) ──
            var form = formsByCandidate[id]
                .Where(row => row.SubmittedAt >= appliedAt - FormGrace)
                .OrderBy(row => row.SubmittedAt)
                .FirstOrDefault();
            facts[ProcessFacts.CandF02] = form is null ? FactValue.NotHeld : Holds(form.SubmittedAt, linkedId: form.FormId);

            // ── Hire ──
            var hire = hiresByCandidate[id]
                .Where(row => row.RecordedAt >= appliedAt)
                .OrderBy(row => row.RecordedAt)
                .FirstOrDefault();
            facts[ProcessFacts.CandHired] = hire is null
                ? FactValue.NotHeld
                : new FactValue(true, hire.RecordedAt, LinkedId: hire.EmploymentId, LinkedEmployeeId: hire.EmployeeId);

            // ── HR-F04 council ──
            var councilsOfApplication = councilsByCandidate[id].Where(row => row.Blank.CreatedAt >= appliedAt).ToList();
            var listed = councilsOfApplication
                .OrderByDescending(row => row.Blank.CreatedAt)
                .ThenByDescending(row => row.Blank.Id)
                .FirstOrDefault();
            var pageDecision = pageDecisionsByCandidate[id]
                .Where(row => row.CreatedAt >= appliedAt)
                .OrderByDescending(row => row.CreatedAt)
                .FirstOrDefault();
            facts[ProcessFacts.CandInF04] = (listed, pageDecision) switch
            {
                ({ } blank, _) => Holds(blank.Blank.CreatedAt, blank.Decision, blank.Blank.Id),
                (null, { } page) => Holds(page.CreatedAt, page.Value, page.Id),
                _ => FactValue.NotHeld,
            };

            var decided = councilsOfApplication
                .Where(row => row.Blank.Status == BlankSubmissionStatus.Approved)
                .OrderByDescending(row => row.Blank.CompletedAt)
                .ThenByDescending(row => row.Blank.Id)
                .FirstOrDefault();
            facts[ProcessFacts.CandF04Decision] = (decided, pageDecision) switch
            {
                ({ } blank, _) => Holds(blank.Blank.CompletedAt, blank.Decision, blank.Blank.Id),
                (null, { } page) => Holds(page.UpdatedAt ?? page.CreatedAt, page.Value, page.Id),
                _ => FactValue.NotHeld,
            };

            // ── HR-F03 / HR-F05 (candidate-subject blanks) ──
            var ofApplication = blanksByCandidate[id].Where(row => row.CreatedAt >= appliedAt).ToList();

            if (Latest(ofApplication, ProcessBlanks.F03) is { } check && check.Status == BlankSubmissionStatus.Approved)
            {
                facts[ProcessFacts.CandF03] = Holds(check.CompletedAt, check.Step(ProcessBlanks.F03DecisionStep)?.Decision, check.Id);
            }
            else
            {
                facts[ProcessFacts.CandF03] = FactValue.NotHeld;
            }

            if (Latest(ofApplication, ProcessBlanks.F05) is { } offer)
            {
                AddSignedSteps(facts, offer, ProcessFacts.CandF05Step);

                if (BlankJson.Date(offer.AnswersJson["startDate"]) is { } startDate)
                {
                    facts[ProcessFacts.CandF05StartDate] = Holds(offer.CreatedAt, startDate, offer.Id);
                }
            }

            result[subject] = facts;
        }

        return result;
    }

    /// <summary>
    /// Every (HR-F04, candidate row) pair for the wanted candidates: §5.2 <c>candidates</c> table,
    /// column <c>candidate</c> (ref) and <c>decision</c> (choice).
    /// </summary>
    private static List<CouncilRow> CouncilRows(List<BlankFactRow> councils, Guid[] candidateIds)
    {
        var wanted = candidateIds.ToHashSet();
        var rows = new List<CouncilRow>();

        foreach (var council in councils)
        {
            foreach (var row in BlankJson.Rows(council.AnswersJson["candidates"]))
            {
                if (BlankJson.RefId(row["candidate"]) is { } candidateId && wanted.Contains(candidateId))
                {
                    rows.Add(new CouncilRow(candidateId, council, BlankJson.Choice(row["decision"])));
                }
            }
        }

        return rows;
    }

    private sealed record StageEntryRow(Guid CandidateId, Guid StageId, DateTimeOffset RecordedAt);

    private sealed record FormRow(Guid CandidateId, Guid FormId, DateTimeOffset SubmittedAt);

    private sealed record HireRow(Guid CandidateId, Guid EmploymentId, Guid EmployeeId, DateTimeOffset RecordedAt);

    private sealed record CouncilRow(Guid CandidateId, BlankFactRow Blank, string? Decision);
    /// <summary>A decision saved on the minutes page; <see cref="Value"/> is the fact's value.</summary>
    private sealed record PageDecisionRow(
        Guid CandidateId,
        Guid Id,
        CouncilDecisionKind Decision,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt)
    {
        public string Value => Decision.ToString().ToLowerInvariant();
    }
}
