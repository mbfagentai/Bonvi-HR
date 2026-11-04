using Bonvi.Application.Processes.Facts;
using Bonvi.Domain.Employees;
using Bonvi.Domain.Recruiting;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Processes.Facts;

/// <summary><c>vr.*</c> — two queries: the requests (with their vacancy), the vacancies' candidates.</summary>
public sealed partial class EfProcessFactSource
{
    private async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, FactValue>>> VacancyRequestFactsAsync(
        Guid[] ids,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, IReadOnlyDictionary<string, FactValue>>();
        if (ids.Length == 0)
        {
            return result;
        }

        var requests = await (
                from request in _context.VacancyRequests.AsNoTracking()
                where ids.Contains(request.Id)
                join vacancy in _context.Vacancies.AsNoTracking() on request.VacancyId equals vacancy.Id into vacancies
                from vacancy in vacancies.DefaultIfEmpty()
                select new
                {
                    request.Id,
                    request.CreatedAt,
                    request.Status,
                    request.DecidedAt,
                    request.CancelledAt,
                    request.VacancyId,
                    request.Openings,
                    VacancyStatus = vacancy == null ? (VacancyStatus?)null : vacancy.Status,
                    VacancyClosedOn = vacancy == null ? null : vacancy.ClosedAt,
                    VacancyUpdatedAt = vacancy == null ? null : vacancy.UpdatedAt,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var vacancyIds = requests.Where(row => row.VacancyId != null).Select(row => row.VacancyId!.Value).Distinct().ToArray();

        // A hire is the Hire/Rehire event of the candidate; a candidate marked Hired without an
        // event (imports) counts from its last update.
        var candidates = new List<VacancyCandidateRow>();
        if (vacancyIds.Length > 0)
        {
            candidates = await _context.Candidates.AsNoTracking()
                .Where(candidate => vacancyIds.Contains(candidate.VacancyId))
                .Select(candidate => new VacancyCandidateRow(
                    candidate.VacancyId,
                    candidate.ReappliedAt ?? candidate.CreatedAt,
                    candidate.Status,
                    candidate.UpdatedAt ?? candidate.CreatedAt,
                    _context.EmploymentEvents
                        .Where(row => row.CandidateId == candidate.Id
                            && (row.Type == EmploymentEventType.Hire || row.Type == EmploymentEventType.Rehire))
                        .Max(row => (DateTimeOffset?)row.RecordedAt)))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var byVacancy = candidates.ToLookup(row => row.VacancyId);

        foreach (var request in requests)
        {
            var facts = NewFacts();
            facts[ProcessFacts.VrCreated] = Holds(request.CreatedAt, linkedId: request.Id);

            var decided = request.Status switch
            {
                VacancyRequestStatus.Approved => "approved",
                VacancyRequestStatus.Rejected => "rejected",
                VacancyRequestStatus.Cancelled => "cancelled",
                _ => null,
            };

            var decidedAt = request.Status == VacancyRequestStatus.Cancelled
                ? request.CancelledAt ?? request.DecidedAt
                : request.DecidedAt;

            facts[ProcessFacts.VrDecided] = decided is null
                ? FactValue.NotHeld
                : Holds(decidedAt, decided);

            var ofVacancy = request.VacancyId is { } vacancyId ? byVacancy[vacancyId] : [];

            var firstCandidate = decidedAt is { } after
                ? ofVacancy.Where(row => row.AppliedAt > after).Min(row => (DateTimeOffset?)row.AppliedAt)
                : null;
            facts[ProcessFacts.VrFirstCandidate] = firstCandidate is null ? FactValue.NotHeld : Holds(firstCandidate);

            // Hires since this request (a vacancy may be older than the request and already have hires).
            var hires = ofVacancy
                .Where(row => row.Status == CandidateStatus.Hired)
                .Select(row => row.HiredAt ?? row.UpdatedAt)
                .Where(at => at >= request.CreatedAt)
                .Order()
                .ToList();

            DateTimeOffset? filledAt = request.Openings > 0 && hires.Count >= request.Openings
                ? hires[request.Openings - 1]
                : null;

            if (request.VacancyStatus == VacancyStatus.Closed)
            {
                var closedAt = request.VacancyUpdatedAt
                    ?? (request.VacancyClosedOn is { } closedOn ? TashkentStartOf(closedOn) : request.CreatedAt);
                filledAt = filledAt is { } byHires && byHires < closedAt ? byHires : closedAt;
            }

            facts[ProcessFacts.VrFilled] = filledAt is null ? FactValue.NotHeld : Holds(filledAt, linkedId: request.VacancyId);

            result[request.Id] = facts;
        }

        return result;
    }

    private sealed record VacancyCandidateRow(
        Guid VacancyId,
        DateTimeOffset AppliedAt,
        CandidateStatus Status,
        DateTimeOffset UpdatedAt,
        DateTimeOffset? HiredAt);
}
