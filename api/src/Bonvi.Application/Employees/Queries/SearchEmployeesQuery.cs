using Bonvi.Application.Abstractions;
using Bonvi.Application.Employees.Abstractions;
using Bonvi.Application.Employees.Common;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Employees.Queries;

/// <summary>
/// Xodimlar ro'yxati — tizimning asosiy ekrani.
/// </summary>
/// <param name="Search">FIO / JSHSHIR / telefon / pasport.</param>
/// <param name="DepartmentId">Bo'lim bo'yicha filtr.</param>
/// <param name="PositionId">Lavozim bo'yicha filtr.</param>
/// <param name="DirectionId">Yo'nalish bo'yicha filtr.</param>
/// <param name="LegalEntityId">Yuridik shaxs bo'yicha filtr.</param>
/// <param name="Status">
/// Mehnat munosabati holati: <c>Probation</c> | <c>Active</c> | <c>Terminated</c>.
/// Bo'sh bo'lsa — arxiv bilan hammasi.
/// </param>
/// <param name="NeedsReview">Faqat tuzatish kerak bo'lgan yozuvlar.</param>
/// <param name="Page">Sahifa raqami, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
/// <param name="Sort">
/// Saralash: <c>name</c>, <c>hireDate</c>, <c>department</c>, <c>position</c>,
/// <c>status</c>, <c>birthDate</c>. Teskari tartib uchun oldiga <c>-</c>
/// qo'yiladi (<c>-hireDate</c>) yoki oxiriga <c>:desc</c>.
/// </param>
public sealed record SearchEmployeesQuery(
    string? Search = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    Guid? DirectionId = null,
    Guid? LegalEntityId = null,
    string? Status = null,
    bool? NeedsReview = null,
    int? Page = null,
    int? PageSize = null,
    string? Sort = null);

public sealed class SearchEmployeesQueryHandler
{
    /// <summary>Sukut bo'yicha sahifa hajmi — jadval ekranga shuncha sig'adi.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>
    /// Eng katta sahifa hajmi.
    /// </summary>
    /// <remarks>
    /// Chegara ataylab: <c>pageSize=100000</c> so'rovi butun bazani bitta
    /// javobga solib, serverni ham brauzerni ham to'xtatib qo'yardi.
    /// Eksport uchun alohida yo'l bo'ladi, ro'yxat uchun emas.
    /// </remarks>
    public const int MaxPageSize = 200;

    private readonly IEmployeeReadModel _readModel;
    private readonly ICurrentUser _currentUser;

    public SearchEmployeesQueryHandler(IEmployeeReadModel readModel, ICurrentUser currentUser)
    {
        _readModel = readModel;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<EmployeeListItemDto>>> HandleAsync(
        SearchEmployeesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (sort, descending) = ParseSort(query.Sort);

        var status = ParseStatus(query.Status);
        if (status.IsFailure)
        {
            return status.Error;
        }

        var page = query.Page is { } requested && requested > 0 ? requested : 1;
        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);

        var filter = new EmployeeSearchFilter(
            Normalize(query.Search),
            query.DepartmentId,
            query.PositionId,
            query.DirectionId,
            query.LegalEntityId,
            status.Value,
            query.NeedsReview,
            page,
            pageSize,
            sort,
            descending);

        var found = await _readModel.SearchEmployeesAsync(filter, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<EmployeeListItemDto> items = [.. found.Items.Select(ToDto)];

        var pageCount = pageSize == 0 ? 0 : (found.Total + pageSize - 1) / pageSize;

        return Result.Success(new PagedResult<EmployeeListItemDto>(
            items,
            page,
            pageSize,
            found.Total,
            pageCount));
    }

    /// <summary>
    /// Holat matnini enum'ga o'giradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nega satr, sanoq emas.</b> Minimal API sanoqni REGISTRGA SEZGIR
    /// bog'laydi va bo'sh qiymatni umuman tushunmaydi: <c>?status=active</c>
    /// ham, <c>?status=</c> ham 400 bo'lib qaytardi. Ikkalasi ham oddiy
    /// hol — birinchisi qo'lda yozilgan havola, ikkinchisi esa «filtr
    /// tozalandi» degan mijoz so'rovi.
    /// </para>
    /// <para>
    /// <b>Bo'sh — «hammasi», noma'lum — xato.</b> Saralashdan farqli
    /// (u jimgina sukutga tushadi), bu yerda noto'g'ri qiymat aytiladi:
    /// saralash natijaning MAZMUNINI o'zgartirmaydi, filtr esa
    /// o'zgartiradi. «terminatd» deb yozilgan havola jimgina butun
    /// bazani ko'rsatsa, HR buni sezmasdi.
    /// </para>
    /// </remarks>
    private static Result<EmploymentStatus?> ParseStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result.Success<EmploymentStatus?>(null);
        }

        // `Enum.TryParse` RAQAMNI ham qabul qiladi: «99» muvaffaqiyatli
        // bog'lanadi va sanoqda mavjud bo'lmagan qiymat hosil bo'ladi.
        // U so'rovga tushsa, natija bo'sh ro'yxat bo'lardi — shu sababli
        // `IsDefined` ham kerak.
        if (!Enum.TryParse<EmploymentStatus>(raw.Trim(), ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
        {
            return Error.Validation(
                "xodim.holat_notogri",
                "Holat noto'g'ri. Ruxsat etilgan qiymatlar: Probation, Active, Terminated.");
        }

        return Result.Success<EmploymentStatus?>(parsed);
    }

    /// <summary>
    /// Saralash matnini xavfsiz enum'ga o'giradi.
    /// </summary>
    /// <remarks>
    /// Noma'lum ustun so'ralganda xato QAYTARILMAYDI: jadval sarlavhasini
    /// bosgan foydalanuvchi «400 Bad Request» ko'rishi kerak emas —
    /// ro'yxat sukut bo'yicha tartibda ochilaveradi.
    /// </remarks>
    private static (EmployeeSortField Sort, bool Descending) ParseSort(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (EmployeeSortField.Name, false);
        }

        var value = raw.Trim();
        var descending = false;

        if (value.StartsWith('-'))
        {
            descending = true;
            value = value[1..];
        }

        var separator = value.IndexOf(':', StringComparison.Ordinal);
        if (separator >= 0)
        {
            descending = value[(separator + 1)..].Equals("desc", StringComparison.OrdinalIgnoreCase);
            value = value[..separator];
        }

        var field = value.ToUpperInvariant() switch
        {
            "HIREDATE" => EmployeeSortField.HireDate,
            "DEPARTMENT" => EmployeeSortField.Department,
            "POSITION" => EmployeeSortField.Position,
            "STATUS" => EmployeeSortField.Status,
            "BIRTHDATE" => EmployeeSortField.BirthDate,
            _ => EmployeeSortField.Name,
        };

        return (field, descending);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private EmployeeListItemDto ToDto(EmployeeSearchRow row)
    {
        var fullName = FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName);

        // Oklad ustuni ro'yxatda ham «salary.view» ga bog'liq: HR aynan shu
        // jadvalni ekranga chiqarib boshqalarga ko'rsatadi.
        MoneyDto? salary = null;
        if (_currentUser.CanSeeSalary() && row.SalaryAmount is { } amount)
        {
            var money = Money.FromTrusted(amount, row.SalaryCurrency ?? Money.Uzs);
            salary = new MoneyDto(money.Amount, money.Currency, money.Formatted);
        }

        return new EmployeeListItemDto(
            row.Id,
            fullName.Full,
            fullName.ShortForm,
            row.BirthDate,
            row.Pinfl,
            row.Passport,
            row.Phone,
            row.PhotoKey,
            row.NeedsReview,
            row.EmploymentId,
            row.LegalEntityId,
            row.LegalEntityName,
            row.DepartmentId,
            row.DepartmentName,
            row.PositionId,
            row.PositionName,
            row.Status,
            row.HireDate,
            row.Rate,
            salary,
            row.ActiveFlags,
            [.. row.Directions.Select(direction => new EmployeeDirectionDto(direction.Id, direction.Name))]);
    }
}
