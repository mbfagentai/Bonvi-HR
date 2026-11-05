using Bonvi.Application.Processes.Inputs;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary>
/// At most five queries: the employee, the employment, the document types held, the active types, the candidate. The
/// projections ask only whether a value exists, so a PINFL or passport number never reaches this process's memory.
/// </summary>
public sealed class ProcessSubjectDataReader : IProcessSubjectDataReader
{
    private readonly AppDbContext _context;

    public ProcessSubjectDataReader(AppDbContext context) => _context = context;

    public async Task<ProcessSubjectData> LoadAsync(Guid? employeeId, Guid? employmentId, Guid? candidateId, CancellationToken cancellationToken = default)
    {
        var data = new ProcessSubjectData { CandidateId = candidateId };

        if (candidateId is { } candidate)
        {
            data = data with
            {
                CandidateHasPhoto = await _context.Candidates.AsNoTracking()
                    .AnyAsync(row => row.Id == candidate && row.PhotoKey != null, cancellationToken).ConfigureAwait(false),
                CandidateHasForm = await _context.CandidateForms.AsNoTracking()
                    .AnyAsync(row => row.CandidateId == candidate, cancellationToken).ConfigureAwait(false),
            };
        }

        if (employeeId is not { } employee)
        {
            return data;
        }

        var card = await _context.Employees.AsNoTracking()
            .Where(row => row.Id == employee)
            .Select(row => new
            {
                HasPassport = row.Passport != null,
                HasPinfl = row.Pinfl != null,
                HasPhoto = row.PhotoKey != null,
                HasMehnatUzUrl = row.MehnatUzUrl != null,
                row.Gender,
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (card is null)
        {
            return data;
        }

        var hasHireOrder = employmentId is { } employment
            && await _context.Employments.AsNoTracking()
                .AnyAsync(row => row.Id == employment && row.HireOrderNo != null, cancellationToken).ConfigureAwait(false);

        var held = await _context.EmployeeDocuments.AsNoTracking()
            .Where(document => document.EmployeeId == employee)
            .Join(_context.DocumentTypes.IgnoreQueryFilters(), document => document.DocumentTypeId, type => type.Id, (_, type) => type.Name)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var active = await _context.DocumentTypes.AsNoTracking()
            .Where(type => type.IsActive)
            .Select(type => new { type.Id, type.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return data with
        {
            EmployeeId = employee,
            HasPassport = card.HasPassport,
            HasPinfl = card.HasPinfl,
            HasPhoto = card.HasPhoto,
            HasMehnatUzUrl = card.HasMehnatUzUrl,
            Gender = card.Gender,
            HasHireOrder = hasHireOrder,
            DocumentTypes = held.Select(ProcessInputSources.NormaliseTypeName).ToHashSet(StringComparer.Ordinal),
            ActiveDocumentTypes = active
                .GroupBy(type => ProcessInputSources.NormaliseTypeName(type.Name), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal),
        };
    }
}
