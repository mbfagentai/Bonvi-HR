using Bonvi.Application.Common.Lists;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Blanks;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Blanks.Submissions;

/// <summary>Blankalar ro'yxati filtri (§7 #6).</summary>
public sealed record ListBlankSubmissionsQuery(
    string? TemplateCode = null,
    string? Status = null,
    Guid? EmployeeId = null,
    Guid? CandidateId = null,
    Guid? DepartmentId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Search = null,
    bool WaitingForMe = false,
    bool Incomplete = false,
    bool Overdue = false,
    int? Page = null,
    int? PageSize = null);

public sealed class ListBlankSubmissionsQueryHandler
{
    /// <summary>Sukut sahifa hajmi (loyiha qoidasi: 20/50/100/Barchasi).</summary>
    public const int DefaultPageSize = 50;

    /// <summary>Eng katta sahifa — «Barchasi».</summary>
    public const int MaxPageSize = 10_000;

    private readonly IBlankReadModel _readModel;
    private readonly IDepartmentRepository _departments;
    private readonly BlankActorResolver _actors;
    private readonly IClock _clock;

    public ListBlankSubmissionsQueryHandler(
        IBlankReadModel readModel,
        IDepartmentRepository departments,
        BlankActorResolver actors,
        IClock clock)
    {
        _readModel = readModel;
        _departments = departments;
        _actors = actors;
        _clock = clock;
    }

    public async Task<Result<PagedResult<BlankSubmissionListItemDto>>> HandleAsync(
        ListBlankSubmissionsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        BlankSubmissionStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<BlankSubmissionStatus>(query.Status.Trim(), ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed)
                || int.TryParse(query.Status, out _))
            {
                return BlankErrors.Invalid;
            }

            status = parsed;
        }

        var range = ListDateRange.Validate(query.From, query.To);
        if (range.IsFailure)
        {
            return range.Error;
        }

        IReadOnlyCollection<Guid>? departmentIds = null;
        if (query.DepartmentId is { } departmentId)
        {
            var descendants = await _departments.GetDescendantIdsAsync(departmentId, cancellationToken).ConfigureAwait(false);
            departmentIds = [departmentId, .. descendants];
        }

        BlankWaitingFor? waitingFor = null;
        if (query.WaitingForMe)
        {
            var caller = await _actors.CallerAsync(cancellationToken).ConfigureAwait(false);
            waitingFor = new BlankWaitingFor(
                caller.EmployeeId,
                await _actors.OfficeRolesAsync(caller, cancellationToken).ConfigureAwait(false));
        }

        var page = query.Page is > 0 ? query.Page.Value : 1;
        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);

        var found = await _readModel
            .SearchAsync(
                new BlankSubmissionFilter(
                    string.IsNullOrWhiteSpace(query.TemplateCode) ? null : query.TemplateCode.Trim(),
                    status,
                    query.EmployeeId,
                    query.CandidateId,
                    departmentIds,
                    ListDateRange.StartUtc(query.From),
                    ListDateRange.EndUtcExclusive(query.To),
                    string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
                    waitingFor,
                    query.Incomplete,
                    query.Overdue ? _clock.Today : null,
                    page,
                    pageSize),
                cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<BlankSubmissionListItemDto>(
            found.Items,
            page,
            pageSize,
            found.Total,
            (found.Total + pageSize - 1) / pageSize);
    }
}

public sealed class GetBlankSubmissionQueryHandler
{
    private readonly BlankSubmissionDetails _details;

    public GetBlankSubmissionQueryHandler(BlankSubmissionDetails details) => _details = details;

    public Task<Result<BlankSubmissionDetailDto>> HandleAsync(Guid id, CancellationToken cancellationToken = default)
        => _details.GetAsync(id, cancellationToken);
}
