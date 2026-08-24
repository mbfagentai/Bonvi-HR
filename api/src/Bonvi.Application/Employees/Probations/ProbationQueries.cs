using Bonvi.Application.Employees.Abstractions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Probations;

/// <summary>
/// Sinov muddatlari ro'yxati.
/// </summary>
/// <param name="Status">Holat bo'yicha filtr. <c>null</c> — hammasi.</param>
/// <param name="EndingWithinDays">Shuncha kun ichida tugaydiganlar.</param>
public sealed record ListProbationsQuery(
    ProbationStatus? Status = null,
    int? EndingWithinDays = null);

public sealed class ListProbationsQueryHandler
{
    private readonly IEmployeeReadModel _readModel;
    private readonly IClock _clock;

    public ListProbationsQueryHandler(IEmployeeReadModel readModel, IClock clock)
    {
        _readModel = readModel;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<ProbationListItemDto>>> HandleAsync(
        ListProbationsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var today = _clock.Today;

        var rows = await _readModel
            .ListProbationsAsync(
                new ProbationListFilter(query.Status, query.EndingWithinDays, today),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(ToDtos(rows, today));
    }

    internal static IReadOnlyList<ProbationListItemDto> ToDtos(IReadOnlyList<ProbationRow> rows, DateOnly today)
        =>
        [
            .. rows
                // Tugash sanasi bo'yicha: eng yaqinlashgani tepada — bu ro'yxat
                // «kimga bugun ulgurish kerak» degan savolga javob beradi.
                .OrderBy(row => row.EndDate)
                .ThenBy(row => row.EmployeeName, StringComparer.CurrentCulture)
                .Select(row => new ProbationListItemDto(
                    row.Id,
                    row.EmploymentId,
                    row.EmployeeId,
                    row.EmployeeName,
                    row.PhotoKey,
                    row.DepartmentName,
                    row.PositionName,
                    row.HireDate,
                    row.StartDate,
                    row.EndDate,
                    row.Status,
                    row.ResolvedAt,
                    row.Conclusion,
                    row.EndDate.DayNumber - today.DayNumber,
                    row.Status == ProbationStatus.InProgress
                        && row.EndDate.DayNumber - today.DayNumber <= Probation.WarningDays)),
        ];
}

/// <summary>
/// Yaqin kunlarda tugaydigan sinov muddatlari.
/// </summary>
/// <remarks>
/// Alohida use-case, chunki uni ikki joy chaqiradi: bosh sahifadagi
/// «diqqat» bloki va kunlik eslatma vazifasi. Ikkalasi ham bir xil
/// chegarani ishlatishi kerak — u <see cref="Probation.WarningDays"/> da.
/// </remarks>
public sealed record GetProbationsEndingSoonQuery(int? WithinDays = null);

public sealed class GetProbationsEndingSoonQueryHandler
{
    private readonly IEmployeeReadModel _readModel;
    private readonly IClock _clock;

    public GetProbationsEndingSoonQueryHandler(IEmployeeReadModel readModel, IClock clock)
    {
        _readModel = readModel;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<ProbationListItemDto>>> HandleAsync(
        GetProbationsEndingSoonQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var today = _clock.Today;
        var within = query.WithinDays is { } days && days > 0 ? days : Probation.WarningDays;

        var rows = await _readModel
            .ListProbationsAsync(
                new ProbationListFilter(ProbationStatus.InProgress, within, today),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(ListProbationsQueryHandler.ToDtos(rows, today));
    }
}
