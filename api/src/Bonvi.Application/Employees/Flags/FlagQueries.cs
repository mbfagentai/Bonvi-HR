using Bonvi.Application.Employees.Abstractions;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;

namespace Bonvi.Application.Employees.Flags;

/// <summary>
/// Belgi qo'yilgan xodimlar ro'yxati.
/// </summary>
/// <remarks>
/// To'rt tur — to'rt sahifa: rezerv, qora ro'yxat, nazorat, qobiliyatli.
/// Ular bir xil jadval, shu sababli bitta use-case va bitta endpoint.
/// </remarks>
/// <param name="Type">Belgi turi.</param>
/// <param name="OnlyActive">Faqat bugun kuchda bo'lganlar.</param>
public sealed record ListFlaggedQuery(EmployeeFlagType Type, bool OnlyActive = true);

public sealed class ListFlaggedQueryHandler
{
    private readonly IEmployeeReadModel _readModel;
    private readonly IClock _clock;

    public ListFlaggedQueryHandler(IEmployeeReadModel readModel, IClock clock)
    {
        _readModel = readModel;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<FlaggedEmployeeDto>>> HandleAsync(
        ListFlaggedQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var today = _clock.Today;

        var rows = await _readModel
            .ListFlagsAsync(query.Type, query.OnlyActive, today, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<FlaggedEmployeeDto> items =
        [
            .. rows
                .OrderByDescending(row => row.StartDate)
                .ThenBy(row => row.EmployeeName, StringComparer.CurrentCulture)
                .Select(row => new FlaggedEmployeeDto(
                    row.Id,
                    row.EmployeeId,
                    row.EmployeeName,
                    row.PhotoKey,
                    row.Type,
                    row.StartDate,
                    row.EndDate,
                    row.Reason,
                    row.ResponsibleEmployeeId,
                    row.ResponsibleName,
                    row.StartDate <= today && (row.EndDate is null || row.EndDate >= today))),
        ];

        return Result.Success(items);
    }
}

/// <summary>Bitta xodimning barcha belgilari — kartadagi qizil/sariq nishonlar uchun.</summary>
public sealed record GetEmployeeFlagsQuery(Guid EmployeeId, bool OnlyActive = false);

public sealed class GetEmployeeFlagsQueryHandler
{
    private readonly IEmployeeFlagRepository _flags;
    private readonly IEmployeeRepository _employees;
    private readonly IClock _clock;

    public GetEmployeeFlagsQueryHandler(
        IEmployeeFlagRepository flags,
        IEmployeeRepository employees,
        IClock clock)
    {
        _flags = flags;
        _employees = employees;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<EmployeeFlagDto>>> HandleAsync(
        GetEmployeeFlagsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Xodim bor-yo'qligi tekshiriladi: bo'sh ro'yxat «belgisi yo'q» va
        // «bunday xodim yo'q» holatlarini farqlamas edi.
        var exists = await _employees.ExistsAsync(query.EmployeeId, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            return EmployeeErrors.NotFound;
        }

        var flags = await _flags
            .GetByEmployeeAsync(query.EmployeeId, query.OnlyActive, cancellationToken)
            .ConfigureAwait(false);

        var today = _clock.Today;

        IReadOnlyList<EmployeeFlagDto> items =
        [
            .. flags
                .OrderByDescending(flag => flag.StartDate)
                .Select(flag => EmployeeMapper.ToDto(flag, today)),
        ];

        return Result.Success(items);
    }
}
