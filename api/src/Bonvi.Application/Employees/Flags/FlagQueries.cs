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
/// <param name="Search">
/// Xodim bo'yicha qidiruv: F.I.Sh., JSHSHIR, pasport yoki telefon —
/// xodimlar ro'yxatidagi bilan aynan bir xil qoida.
/// <para>
/// Qidiruv SERVERDA bajariladi, garchi ro'yxat sahifalanmasa ham: qora
/// ro'yxat yuzlab yozuvga yetishi mumkin va klientda kesish loyihaning
/// Excel muammosini takrorlash bo'lardi. Muhimrog'i — bu ro'yxatda «bu
/// odam bormi?» degan savolga JSHSHIR bilan javob berish kerak, ismning
/// yozilishi esa manbadan manbaga farq qiladi.
/// </para>
/// </param>
public sealed record ListFlaggedQuery(
    EmployeeFlagType Type,
    bool OnlyActive = true,
    string? Search = null);

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

        // Bo'sh matn `null` ga aylantiriladi: «?search=» — bu «hech narsa
        // topilmasin» emas, «qidiruvsiz» degani.
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        var rows = await _readModel
            .ListFlagsAsync(query.Type, query.OnlyActive, today, search, cancellationToken)
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
                    row.StartDate <= today && (row.EndDate is null || row.EndDate >= today))),
        ];

        return Result.Success(items);
    }
}

/// <summary>Bitta xodimning barcha belgilari — kartadagi nishonlar va tarix uchun.</summary>
/// <remarks>
/// Filtr YO'Q: kartaga baribir butun tarix kerak («Belgilar» tabi uni
/// sanalari bilan ko'rsatadi), nishonlar esa mijozda kesiladi
/// (<c>flagInForce</c>) — sabab <c>IEmployeeFlagRepository.GetByEmployeeAsync</c>
/// izohida.
/// </remarks>
public sealed record GetEmployeeFlagsQuery(Guid EmployeeId);

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
            .GetByEmployeeAsync(query.EmployeeId, cancellationToken)
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
