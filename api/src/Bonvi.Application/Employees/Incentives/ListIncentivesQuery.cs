using Bonvi.Application.Employees.Abstractions;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.Employees.Queries;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.Employees.Incentives;

/// <summary>
/// Barcha rag'bat va jarima yozuvlari — xodimdan qat'i nazar, bitta ro'yxatda.
/// </summary>
/// <remarks>
/// <para>
/// Excel'da bu «91.14 Тақдирланган ходимлар» varag'i edi va u faqat
/// rag'batni bilardi — jarima umuman yozilmasdi, HR uni og'zaki eslab
/// qolardi. Bu yerda ikkalasi bitta oqim: «91.14» esa uning
/// <c>kind=Award</c> bilan filtrlangan ko'rinishi.
/// </para>
/// <para>
/// <b>Nega yig'ma emas.</b> «Xodim — nechta rag'bat, nechta jarima»
/// ko'rinishi ham muhokama qilingan va RAD ETILGAN: u «kimda ko'p» degan
/// savolga javob beradi, HR esa «avgustda nima bo'ldi, qaysi buyruq
/// bilan» deb so'raydi. Har bir yozuv alohida qator bo'lgani sababli
/// bitta xodim ro'yxatda bir necha marta ko'rinadi — bu kutilgan holat.
/// </para>
/// </remarks>
/// <param name="Search">Xodim bo'yicha qidiruv: F.I.Sh., JSHSHIR, pasport yoki telefon.</param>
/// <param name="Kind">Rag'bat yoki jarima. Bo'sh bo'lsa — ikkalasi.</param>
/// <param name="From">Sananing quyi chegarasi.</param>
/// <param name="To">Sananing yuqori chegarasi.</param>
/// <param name="DepartmentId">Bo'lim, ICHKI bo'limlari bilan birga.</param>
/// <param name="LegalEntityId">Yuridik shaxs.</param>
/// <param name="Page">Sahifa raqami, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
/// <param name="Sort">
/// Saralash: <c>date</c> yoki <c>employee</c>. Teskari tartib uchun oldiga
/// <c>-</c> qo'yiladi (<c>-date</c>) yoki oxiriga <c>:desc</c>.
/// </param>
public sealed record ListIncentivesQuery(
    string? Search = null,
    IncentiveKind? Kind = null,
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? DepartmentId = null,
    Guid? LegalEntityId = null,
    int? Page = null,
    int? PageSize = null,
    string? Sort = null);

public sealed class ListIncentivesQueryHandler
{
    private readonly IIncentiveReadModel _readModel;

    public ListIncentivesQueryHandler(IIncentiveReadModel readModel) => _readModel = readModel;

    public async Task<Result<PagedResult<IncentiveListItemDto>>> HandleAsync(
        ListIncentivesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = query.Page is { } requested && requested > 0 ? requested : 1;

        // Sahifa chegaralari xodimlar ro'yxatinikidan olinadi va ATAYLAB
        // takrorlanmaydi — `ListEmploymentEventsQuery` dagi bilan bir xil
        // sabab: uch ro'yxat uch xil «eng katta sahifa» ni qabul qilsa,
        // mijoz qaysi biri qayerda ishlashini eslab qolishi kerak bo'lardi.
        var pageSize = Math.Clamp(
            query.PageSize ?? SearchEmployeesQueryHandler.DefaultPageSize,
            1,
            SearchEmployeesQueryHandler.MaxPageSize);

        var (sort, descending) = ParseSort(query.Sort);

        var filter = new IncentiveListFilter(
            Normalize(query.Search),
            query.Kind,
            query.From,
            query.To,
            query.DepartmentId,
            query.LegalEntityId,
            page,
            pageSize,
            sort,
            descending);

        var found = await _readModel
            .ListIncentivesAsync(filter, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<IncentiveListItemDto> items = [.. found.Items.Select(ToDto)];

        var pageCount = pageSize == 0 ? 0 : (found.Total + pageSize - 1) / pageSize;

        return Result.Success(new PagedResult<IncentiveListItemDto>(
            items,
            page,
            pageSize,
            found.Total,
            pageCount));
    }

    /// <summary>
    /// Saralash matnini xavfsiz enum'ga o'giradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Noma'lum ustun so'ralganda xato QAYTARILMAYDI
    /// (<c>SearchEmployeesQueryHandler.ParseSort</c> dagi bilan bir xil
    /// qoida): jadval sarlavhasini bosgan foydalanuvchi «400 Bad Request»
    /// ko'rishi kerak emas — ro'yxat sukut bo'yicha tartibda ochilaveradi.
    /// </para>
    /// <para>
    /// Sukut bo'yicha tartib xodimlar ro'yxatinikidan FARQ QILADI va bu
    /// ataylab: bu yerda standart yo'nalish KAMAYISH («yangidan eskiga»),
    /// chunki ro'yxat «oxirgi paytda nima bo'ldi» degan savolga javob
    /// beradi. Ya'ni <c>sort</c> berilmasa — <c>-date</c>; <c>date</c>
    /// esa aniq so'ralgan O'SISH tartibi (eng eski yozuvlar tepada).
    /// </para>
    /// </remarks>
    private static (IncentiveSortField Sort, bool Descending) ParseSort(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (IncentiveSortField.Date, true);
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

        return value.ToUpperInvariant() switch
        {
            // «employeeName» ham qabul qilinadi: mijoz jadval ustunini
            // shu nom bilan biladi va ikki xil imlo tufayli saralash
            // jimgina sanaga qaytib ketmasligi kerak.
            "EMPLOYEE" or "EMPLOYEENAME" => (IncentiveSortField.EmployeeName, descending),
            "DATE" => (IncentiveSortField.Date, descending),

            // Noma'lum ustun — sukut bo'yicha tartib TO'LIQ tiklanadi
            // (yo'nalish ham). Aks holda «?sort=allaqachon-yoq» so'rovi
            // sanani o'sish tartibida qaytarib, eng eski yozuvlarni
            // tepaga chiqarardi — foydalanuvchi uchun bu «ro'yxat buzildi»
            // degani.
            _ => (IncentiveSortField.Date, true),
        };
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IncentiveListItemDto ToDto(IncentiveListRow row)
        => new(
            row.Id,
            row.EmployeeId,
            row.EmployeeName,
            row.PhotoKey,
            row.Kind,
            row.TypeId,
            row.TypeName,
            row.Date,
            Amount(row.AmountValue, row.AmountCurrency),
            row.OrderNo,
            row.OrderDate);

    /// <summary>Xom summani DTO'ga o'giradi.</summary>
    /// <remarks>
    /// <para>
    /// <c>salary.view</c> tekshiruvi bu yerda ATAYLAB YO'Q — buyruqlar
    /// ro'yxatidan (<c>ListEmploymentEventsQueryHandler.Salary</c>) farqli.
    /// U yerdagi raqam OKLAD: shartnomadagi doimiy maosh, va uni ko'rish
    /// alohida ruxsat bilan chegaralangan. Bu yerdagi raqam esa bir
    /// martalik mukofot yoki jarima summasi; u <c>incentives.view</c>
    /// ruxsatining O'ZIDA — bu ruxsat aynan «pul mukofoti va intizomiy
    /// chora» uchun berilgan (sababi <c>Permissions.IncentivesView</c>
    /// izohida).
    /// </para>
    /// <para>
    /// ⚠️ Bu qaror o'ylab topilmadi, MAVJUD qoidadan ko'chirildi: xodim
    /// kartasidagi tab (<c>EmployeeMapper.ToDtoAsync(Incentive)</c>)
    /// summani <c>salary.view</c> bilan yashirmaydi. Agar bu yerda
    /// yashirilsa, bitta va shu ma'lumot ikki ekranda ikki xil
    /// ko'rinardi — HR uchun bu tizimga ishonchni yo'qotadigan xato.
    /// Qoidani o'zgartirish kerak bo'lsa, IKKALA joyda birga o'zgaradi.
    /// </para>
    /// </remarks>
    private static MoneyDto? Amount(decimal? amount, string? currency)
    {
        if (amount is not { } value)
        {
            return null;
        }

        var money = Money.FromTrusted(value, currency ?? Money.Uzs);
        return new MoneyDto(money.Amount, money.Currency, money.Formatted);
    }
}
