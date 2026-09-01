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
/// <param name="Search">
/// Xodim bo'yicha qidiruv: F.I.Sh., JSHSHIR, pasport yoki telefon.
/// Ro'yxat sahifalanmaydi (bir vaqtda sinovda o'nlab, ko'pi bilan bir
/// necha yuz odam bo'ladi), lekin qidiruv baribir SERVERDA bajariladi —
/// klientda <c>filter()</c> qilish loyihaning Excel muammosini takrorlash
/// bo'lardi: bir kun ro'yxat kattalashadi va kesish jimgina noto'g'ri
/// natija berardi.
/// </param>
/// <param name="Page">Sahifa raqami, 1 dan (sukut: 1).</param>
/// <param name="PageSize">Sahifa hajmi (sukut 25, eng ko'pi 200).</param>
public sealed record ListProbationsQuery(
    ProbationStatus? Status = null,
    int? EndingWithinDays = null,
    string? Search = null,
    int? Page = null,
    int? PageSize = null);

public sealed class ListProbationsQueryHandler
{
    private readonly IEmployeeReadModel _readModel;
    private readonly IClock _clock;

    public ListProbationsQueryHandler(IEmployeeReadModel readModel, IClock clock)
    {
        _readModel = readModel;
        _clock = clock;
    }

    /// <summary>Sahifa hajmi — xodimlar ro'yxatidagi bilan bir xil.</summary>
    /// <remarks>
    /// Ikkala chegara ham <c>SearchEmployeesQuery</c> dan ko'chirilgan:
    /// bir xil ekran naqshi bir xil his qilishi kerak va HR ikkita
    /// ro'yxatda ikki xil sahifa hajmini ko'rmasligi lozim.
    /// </remarks>
    public const int DefaultPageSize = 25;

    /// <summary>Bir so'rovda beriladigan eng katta sahifa.</summary>
    /// <remarks>
    /// Chegara ATAYLAB bor: sahifalashdan oldin bu ro'yxat 1342 qatorni
    /// bitta javobda (926 KB) yuborardi va u ma'lumot o'sishi bilan
    /// cheksiz kattalashardi.
    /// </remarks>
    public const int MaxPageSize = 200;

    public async Task<Result<PagedResult<ProbationListItemDto>>> HandleAsync(
        ListProbationsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var today = _clock.Today;

        var page = Math.Max(query.Page ?? 1, 1);
        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);

        var found = await _readModel
            .ListProbationsAsync(
                // Bo'sh matn `null` ga aylantiriladi: «?search=» — bu
                // «hech narsa topilmasin» emas, «qidiruvsiz» degani
                // (`ListEmploymentEventsQueryHandler` dagi bilan bir xil
                // qoida — bir xil parametr hamma ekranda bir xil ishlaydi).
                new ProbationListFilter(
                    query.Status,
                    query.EndingWithinDays,
                    today,
                    Normalize(query.Search),
                    page,
                    pageSize),
                cancellationToken)
            .ConfigureAwait(false);

        var pageCount = (found.Total + pageSize - 1) / pageSize;

        return Result.Success(new PagedResult<ProbationListItemDto>(
            ToDtos(found.Items, today),
            page,
            pageSize,
            found.Total,
            pageCount));
    }

    /// <summary>Bo'sh yoki faqat bo'shliqdan iborat matn — «qidiruvsiz».</summary>
    internal static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
                    row.DepartmentId,
                    row.PositionId,
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

        // Bu ro'yxat SAHIFALANMAYDI va bu ataylab: u bosh sahifadagi
        // «diqqat» bloki hamda kunlik eslatma uchun va 7 kun ichida
        // tugaydiganlar soni tabiatan kichik. Sahifa hajmi baribir
        // yuqori chegara bilan beriladi — chegarasiz so'rov ma'lumot
        // o'sganda jimgina og'irlashardi.
        var found = await _readModel
            .ListProbationsAsync(
                new ProbationListFilter(
                    ProbationStatus.InProgress,
                    within,
                    today,
                    Search: null,
                    Page: 1,
                    PageSize: ListProbationsQueryHandler.MaxPageSize),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(ListProbationsQueryHandler.ToDtos(found.Items, today));
    }
}
