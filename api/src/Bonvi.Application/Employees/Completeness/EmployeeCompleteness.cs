using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Employees.Queries;
using Bonvi.Domain.Common;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Employees.Completeness;

/// <summary>
/// The codes of what an employee's file can lack. A required document type has a code of its own,
/// <c>doc:{document_types.id}</c>, so the list of documents stays what the catalogue says it is.
/// </summary>
public static class EmployeeCompletenessKinds
{
    public const string Pinfl = "pinfl";
    public const string Passport = "passport";
    public const string Photo = "photo";
    public const string Phone = "phone";
    public const string Address = "address";
    public const string OrderNo = "orderNo";
    public const string NeedsReview = "needsReview";
    public const string Salary = "salary";

    public const string DocumentPrefix = "doc:";

    /// <summary>In the order they are shown. <see cref="Salary"/> is apart: only some viewers have it.</summary>
    public static readonly IReadOnlyList<string> Fixed =
        [Pinfl, Passport, Photo, Phone, Address, OrderNo, NeedsReview];

    public static string ForDocument(Guid documentTypeId) => DocumentPrefix + documentTypeId.ToString("D");
}

/// <summary>One active document type every employee is required to have.</summary>
public sealed record RequiredDocumentKind(Guid Id, string Name);

/// <param name="Search">Name, PINFL, phone or passport: the rule of the employee list.</param>
/// <param name="DepartmentId">A department and everything under it.</param>
/// <param name="IncludeSalary">Whether salary counts as a kind at all: it does only for a viewer who may see salary.</param>
public sealed record EmployeeCompletenessFilter(string? Search, Guid? DepartmentId, bool IncludeSalary);

/// <summary>One person; department, position and hire date are of the employment shown: the primary one, or the latest hired.</summary>
public sealed record EmployeeCompletenessRow(
    Guid EmployeeId,
    string LastName,
    string FirstName,
    string? MiddleName,
    string? Department,
    string? Position,
    DateOnly HireDate,
    bool HasPhoto,
    IReadOnlyList<string> Missing);

public sealed record EmployeeCompletenessPage(IReadOnlyList<EmployeeCompletenessRow> Items, int Total);

/// <param name="Employees">Current employees the filter reaches.</param>
/// <param name="WithGaps">Those of them who lack at least one kind.</param>
/// <param name="ByKind">Employees lacking each kind. A kind nobody lacks is absent.</param>
public sealed record EmployeeCompletenessCounts(
    int Employees,
    int WithGaps,
    IReadOnlyDictionary<string, int> ByKind);

/// <summary>
/// The worklist's reads. Everything happens in SQL over current employees only: filtering, the count of gaps and the
/// sort by it cannot be done on a loaded page.
/// </summary>
public interface IEmployeeCompletenessReadModel
{
    Task<IReadOnlyList<RequiredDocumentKind>> ListRequiredDocumentTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>Employees with at least one gap: most gaps first, then the newest hire, then by name.</summary>
    Task<EmployeeCompletenessPage> ListAsync(
        EmployeeCompletenessFilter filter,
        string? missing,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<EmployeeCompletenessCounts> CountAsync(
        EmployeeCompletenessFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>The «Tartib» card: just the people with a gap, without the per-kind counts.</summary>
    Task<int> CountWithGapsAsync(bool includeSalary, CancellationToken cancellationToken = default);
}

/// <summary>
/// One employee on the worklist. It says whether things exist, never what they are: no PINFL or passport value leaves
/// through here. <c>Missing</c> holds kind codes in the order the summary lists them; <c>Total</c> is the kinds this
/// viewer counts (salary is one only with <c>salary.view</c>) and <c>Done</c> those this employee has.
/// </summary>
public sealed record EmployeeCompletenessItemDto(
    Guid EmployeeId,
    string FullName,
    string? Department,
    string? Position,
    DateOnly HireDate,
    bool HasPhoto,
    IReadOnlyList<string> Missing,
    int Done,
    int Total);

/// <param name="Code">The value to pass as <c>missing</c>.</param>
/// <param name="Name">The catalogue's name of a document kind; the client words the fixed kinds itself.</param>
/// <param name="Count">Employees lacking it. Zero is listed: the kinds are the list of what is checked.</param>
public sealed record EmployeeCompletenessKindDto(string Code, string? Name, int Count);

public sealed record EmployeeCompletenessSummaryDto(
    int Employees,
    int WithGaps,
    IReadOnlyList<EmployeeCompletenessKindDto> Kinds);

public sealed record GetEmployeeCompletenessQuery(
    string? Search = null,
    Guid? DepartmentId = null,
    string? Missing = null,
    int? Page = null,
    int? PageSize = null);

public sealed record GetEmployeeCompletenessSummaryQuery(string? Search = null, Guid? DepartmentId = null);

public sealed class GetEmployeeCompletenessQueryHandler
{
    private readonly IEmployeeCompletenessReadModel _readModel;
    private readonly ICurrentUser _currentUser;

    public GetEmployeeCompletenessQueryHandler(IEmployeeCompletenessReadModel readModel, ICurrentUser currentUser)
    {
        _readModel = readModel;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<EmployeeCompletenessItemDto>>> HandleAsync(
        GetEmployeeCompletenessQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = query.Page is > 0 ? query.Page.Value : 1;
        var pageSize = Math.Clamp(
            query.PageSize ?? SearchEmployeesQueryHandler.DefaultPageSize,
            1,
            SearchEmployeesQueryHandler.MaxPageSize);
        var includeSalary = _currentUser.CanSeeSalary();

        var documents = await _readModel.ListRequiredDocumentTypesAsync(cancellationToken).ConfigureAwait(false);

        var missing = string.IsNullOrWhiteSpace(query.Missing) ? null : query.Missing.Trim();

        // A wrong filter is said out loud: a mistyped link quietly listing everyone would read as «all of these lack it».
        // Salary is simply not a kind for a viewer without salary.view, so asking for it is the same mistake.
        if (missing is not null && !IsKind(missing, documents, includeSalary))
        {
            return Error.Validation(
                "xodim.hujjat_turi_notogri",
                "«missing» qiymati noto'g'ri: bunday tekshiruv turi yo'q.");
        }

        var found = await _readModel
            .ListAsync(new EmployeeCompletenessFilter(query.Search, query.DepartmentId, includeSalary), missing, page, pageSize, cancellationToken)
            .ConfigureAwait(false);

        var total = EmployeeCompletenessKinds.Fixed.Count + documents.Count + (includeSalary ? 1 : 0);

        IReadOnlyList<EmployeeCompletenessItemDto> items =
        [
            .. found.Items.Select(row => new EmployeeCompletenessItemDto(
                row.EmployeeId,
                FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName).Full,
                row.Department,
                row.Position,
                row.HireDate,
                row.HasPhoto,
                row.Missing,
                total - row.Missing.Count,
                total)),
        ];

        return Result.Success(new PagedResult<EmployeeCompletenessItemDto>(
            items,
            page,
            pageSize,
            found.Total,
            (found.Total + pageSize - 1) / pageSize));
    }

    private static bool IsKind(string code, IReadOnlyList<RequiredDocumentKind> documents, bool includeSalary)
        => EmployeeCompletenessKinds.Fixed.Contains(code)
            || (includeSalary && code == EmployeeCompletenessKinds.Salary)
            || documents.Any(document => EmployeeCompletenessKinds.ForDocument(document.Id) == code);
}

public sealed class GetEmployeeCompletenessSummaryQueryHandler
{
    private readonly IEmployeeCompletenessReadModel _readModel;
    private readonly ICurrentUser _currentUser;

    public GetEmployeeCompletenessSummaryQueryHandler(IEmployeeCompletenessReadModel readModel, ICurrentUser currentUser)
    {
        _readModel = readModel;
        _currentUser = currentUser;
    }

    public async Task<Result<EmployeeCompletenessSummaryDto>> HandleAsync(
        GetEmployeeCompletenessSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var includeSalary = _currentUser.CanSeeSalary();

        var documents = await _readModel.ListRequiredDocumentTypesAsync(cancellationToken).ConfigureAwait(false);
        var counts = await _readModel
            .CountAsync(new EmployeeCompletenessFilter(query.Search, query.DepartmentId, includeSalary), cancellationToken)
            .ConfigureAwait(false);

        var kinds = new List<EmployeeCompletenessKindDto>();

        foreach (var code in EmployeeCompletenessKinds.Fixed)
        {
            kinds.Add(new EmployeeCompletenessKindDto(code, null, counts.ByKind.GetValueOrDefault(code)));
        }

        if (includeSalary)
        {
            kinds.Add(new EmployeeCompletenessKindDto(
                EmployeeCompletenessKinds.Salary,
                null,
                counts.ByKind.GetValueOrDefault(EmployeeCompletenessKinds.Salary)));
        }

        foreach (var document in documents)
        {
            var code = EmployeeCompletenessKinds.ForDocument(document.Id);
            kinds.Add(new EmployeeCompletenessKindDto(code, document.Name, counts.ByKind.GetValueOrDefault(code)));
        }

        return Result.Success(new EmployeeCompletenessSummaryDto(counts.Employees, counts.WithGaps, kinds));
    }
}
