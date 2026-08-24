using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Abstractions;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Employees.Queries;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Employees.Employments;

/// <summary>
/// Barcha buyruqlar — xodimdan qat'i nazar, bitta ro'yxatda.
/// </summary>
/// <remarks>
/// <para>
/// Excel'da bu savollar to'rtta alohida varaq edi: «91.2 янги ишга
/// келганлар», «91.3 лавозими ўзгарганлар», «91.4 қўшимча вазифалар»,
/// «91.6 ишдан кетганлар». Har bir o'zgarish ularning bir nechtasiga qo'lda
/// ko'chirilardi va aynan shu tarqoqlik muammoning o'zi edi.
/// </para>
/// <para>
/// Bu yerda varaq yo'q: bitta hodisalar oqimi bor, «91.2» esa uning
/// <c>type=Hire</c> bilan filtrlangan ko'rinishi.
/// </para>
/// </remarks>
/// <param name="Search">Buyruq raqami bo'yicha qidiruv (registrga sezgir emas).</param>
/// <param name="Type">Hodisa turi. Bo'sh bo'lsa — barcha turlar.</param>
/// <param name="From">Kuchga kirish sanasining quyi chegarasi.</param>
/// <param name="To">Kuchga kirish sanasining yuqori chegarasi.</param>
/// <param name="DepartmentId">
/// Bo'lim, ICHKI bo'limlari bilan birga. Hodisada bo'lim ko'rsatilmagan
/// bo'lsa (oklad, bo'shatish, qo'shimcha vazifa) — munosabatning joriy
/// bo'limi hisobga olinadi.
/// </param>
/// <param name="LegalEntityId">Munosabat tegishli bo'lgan yuridik shaxs.</param>
/// <param name="Page">Sahifa raqami, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
public sealed record ListEmploymentEventsQuery(
    string? Search = null,
    EmploymentEventType? Type = null,
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? DepartmentId = null,
    Guid? LegalEntityId = null,
    int? Page = null,
    int? PageSize = null);

public sealed class ListEmploymentEventsQueryHandler
{
    private readonly IEmployeeReadModel _readModel;
    private readonly ICurrentUser _currentUser;

    public ListEmploymentEventsQueryHandler(IEmployeeReadModel readModel, ICurrentUser currentUser)
    {
        _readModel = readModel;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<EmploymentEventListItemDto>>> HandleAsync(
        ListEmploymentEventsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = query.Page is { } requested && requested > 0 ? requested : 1;

        // Sahifa chegaralari xodimlar ro'yxatinikidan olinadi va ATAYLAB
        // takrorlanmaydi: ikkita ro'yxat ikki xil «eng katta sahifa» ni
        // qabul qilsa, mijoz qaysi biri qayerda ishlashini eslab qolishi
        // kerak bo'lardi va chegara vaqt o'tib ajralib ketardi.
        var pageSize = Math.Clamp(
            query.PageSize ?? SearchEmployeesQueryHandler.DefaultPageSize,
            1,
            SearchEmployeesQueryHandler.MaxPageSize);

        var filter = new EmploymentEventFilter(
            Normalize(query.Search),
            query.Type,
            query.From,
            query.To,
            query.DepartmentId,
            query.LegalEntityId,
            page,
            pageSize);

        var found = await _readModel
            .ListEmploymentEventsAsync(filter, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<EmploymentEventListItemDto> items = [.. found.Items.Select(ToDto)];

        var pageCount = pageSize == 0 ? 0 : (found.Total + pageSize - 1) / pageSize;

        return Result.Success(new PagedResult<EmploymentEventListItemDto>(
            items,
            page,
            pageSize,
            found.Total,
            pageCount));
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private EmploymentEventListItemDto ToDto(EmploymentEventRow row)
        => new(
            row.Id,
            row.EmploymentId,
            row.EmployeeId,
            row.EmployeeName,
            row.PhotoKey,
            row.Type,
            row.EffectiveDate,
            row.OrderNo,
            row.OrderDate,
            row.FromDepartment,
            row.ToDepartment,
            row.FromPosition,
            row.ToPosition,
            Salary(row.FromSalaryAmount, row.FromSalaryCurrency),
            Salary(row.ToSalaryAmount, row.ToSalaryCurrency),
            row.Note,
            row.IsImported);

    /// <summary>Xom summani DTO'ga o'giradi — ruxsat bo'lmasa <c>null</c>.</summary>
    /// <remarks>
    /// Oklad tarixi aynan shu ro'yxatda ochiq ko'rinadi («4 500 000 → 5 200 000»),
    /// shu sababli bu yerda ham <c>salary.view</c> tekshiruvi bor: buyruqlar
    /// ro'yxati oklad ruxsatini chetlab o'tadigan yo'l bo'lib qolmasligi kerak.
    /// </remarks>
    private MoneyDto? Salary(decimal? amount, string? currency)
    {
        if (!_currentUser.CanSeeSalary() || amount is not { } value)
        {
            return null;
        }

        var money = Money.FromTrusted(value, currency ?? Money.Uzs);
        return new MoneyDto(money.Amount, money.Currency, money.Formatted);
    }
}
