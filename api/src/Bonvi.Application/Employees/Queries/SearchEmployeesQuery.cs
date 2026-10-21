using Bonvi.Application.Abstractions;
using Bonvi.Application.Common.Lists;
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
/// Mehnat munosabati holati: <c>Probation</c> | <c>Active</c> |
/// <c>Terminated</c>, yoki <c>Employed</c> — SINOVDAGILAR va DOIMIYLAR
/// birga (ishdan ketganlarsiz).
/// <para>
/// ⭐ <c>Employed</c> — ro'yxatning kundalik ko'rinishi va mijozning
/// standart filtri (foydalanuvchi talabi, 2026-09-16): sinovdagi odam
/// ham xodim va u umumiy ro'yxatda ko'rinishi kerak. Bu qiymat sanoqda
/// YO'Q — u ikkita holatning birlashmasi, sababi
/// <c>EmployeeSearchFilter.HideTerminated</c> izohida.
/// </para>
/// <para>
/// Bo'sh bo'lsa — arxiv bilan hammasi.
/// </para>
/// </param>
/// <param name="NeedsReview">Faqat tuzatish kerak bo'lgan yozuvlar.</param>
/// <param name="Page">Sahifa raqami, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi.</param>
/// <param name="Sort">
/// Saralash: <c>name</c>, <c>hireDate</c>, <c>department</c>, <c>position</c>,
/// <c>status</c>, <c>birthDate</c>. Teskari tartib uchun oldiga <c>-</c>
/// qo'yiladi (<c>-hireDate</c>) yoki oxiriga <c>:desc</c>.
/// </param>
/// <param name="From">
/// Sana oralig'ining boshi (kiradi). Qaysi sana — <paramref name="Status"/> ga
/// bog'liq: <c>Terminated</c> («Ishdan ketganlar») — ishdan ketish sanasi,
/// qolgan hammasi («Xodimlar») — ro'yxatda ko'rinadigan munosabatning ishga
/// kirish sanasi (egasi, 2026-10-20; batafsil <see cref="SearchEmployeesQueryHandler.BuildFilter"/>).
/// </param>
/// <param name="To">Sana oralig'ining oxiri (kiradi).</param>
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
    string? Sort = null,
    DateOnly? From = null,
    DateOnly? To = null);

public sealed class SearchEmployeesQueryHandler
{
    /// <summary>Sukut bo'yicha sahifa hajmi — jadval ekranga shuncha sig'adi.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>
    /// Eng katta sahifa hajmi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Chegara ataylab: cheksiz <c>pageSize</c> so'rovi butun bazani bitta
    /// javobga solib, serverni ham brauzerni ham to'xtatib qo'yardi.
    /// </para>
    /// <para>
    /// 2026-10-15 dan 10 000 (ilgari 200): egasi ro'yxat ostida «20 / 50 /
    /// 100 / Barchasi» tanlovini so'radi, «Barchasi» esa aynan shu sonni
    /// yuboradi (`web/src/shared/lib/use-page-size.ts`, <c>PAGE_SIZE_ALL</c>).
    /// Bu son bazadagi eng katta ro'yxatdan (~5 100 nomzod, ~2 000 xodim)
    /// ancha katta, lekin baribir chegara — <c>pageSize=10000000</c> ham
    /// shu yerda to'xtaydi. Brauzer ming-minglab qatorni virtual chizadi
    /// (`DataTable fill`), javob esa siqilgan holda ketadi (Caddy zstd/gzip).
    /// </para>
    /// <para>
    /// Rag'bat va buyruqlar ro'yxatlari ham shu chegarani oladi
    /// (<c>ListIncentivesQuery</c>, <c>ListEmploymentEventsQuery</c>).
    /// </para>
    /// </remarks>
    public const int MaxPageSize = 10_000;

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

        var page = query.Page is { } requested && requested > 0 ? requested : 1;
        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);

        var filterResult = BuildFilter(query, page, pageSize);
        if (filterResult.IsFailure)
        {
            return filterResult.Error;
        }

        var filter = filterResult.Value;

        var found = await _readModel.SearchEmployeesAsync(filter, cancellationToken).ConfigureAwait(false);

        var canSeeSalary = _currentUser.CanSeeSalary();
        IReadOnlyList<EmployeeListItemDto> items = [.. found.Items.Select(row => ToDto(row, canSeeSalary))];

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
    /// <summary>Faqat ishlayotganlar so'ralganmi (<c>Employed</c>).</summary>
    private const string EmployedFilter = "employed";

    private static Result<EmploymentStatus?> ParseStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result.Success<EmploymentStatus?>(null);
        }

        // «Ishlayotganlar» — sanoq qiymati EMAS, ikkita holatning
        // birlashmasi. Bu yerda `null` qaytadi va filtr bayrog'i
        // `HideTerminated` orqali qo'yiladi (izohi filtr rekordida).
        if (string.Equals(raw.Trim(), EmployedFilter, StringComparison.OrdinalIgnoreCase))
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
                "Holat noto'g'ri. Ruxsat etilgan qiymatlar: Employed, Probation, Active, Terminated.");
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

    /// <summary>
    /// So'rov parametrlarini <see cref="EmployeeSearchFilter"/> ga aylantiradi.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>internal</c> — Excel eksporti (<c>ExportEmployeesQueryHandler</c>)
    /// AYNAN shu metodni chaqiradi, o'z nusxasini yozmaydi. Ekrandagi ro'yxat
    /// va eksport bir xil <see cref="SearchEmployeesQuery"/> ni bir xil
    /// qoidalar bilan (qidiruv normalizatsiyasi, holat va saralash tekshiruvi)
    /// filtrga aylantirishi SHART — aks holda ikkalasi asta-sekin ajralib
    /// ketardi va HR ekranda ko'rgani bilan Excel'dagi qatorlar boshqacha
    /// bo'lib chiqardi.
    /// </para>
    /// <para>
    /// <c>page</c>/<c>pageSize</c> tashqaridan keladi: ekran ularni
    /// <see cref="MaxPageSize"/> bilan cheklaydi, eksport esa o'zining chegarasi bilan —
    /// ikkalasi ham shu bitta metodni chaqiradi, faqat sonlar boshqa.
    /// </para>
    /// </remarks>
    internal static Result<EmployeeSearchFilter> BuildFilter(SearchEmployeesQuery query, int page, int pageSize)
    {
        var (sort, descending) = ParseSort(query.Sort);

        var status = ParseStatus(query.Status);
        if (status.IsFailure)
        {
            return status.Error;
        }

        var range = ListDateRange.Validate(query.From, query.To);
        if (range.IsFailure)
        {
            return range.Error;
        }

        // ⭐ Sana oralig'i qaysi sanaga qo'llanadi (egasi, 2026-10-20): «Ishdan
        // ketganlar» bo'limi (`status=Terminated`) — ketish sanasi, qolgan har
        // qanday holat («Xodimlar»: employed, sinovda, doimiy, bo'sh) — ishga
        // kirish sanasi. Ikkalasi ham ro'yxatda KO'RINADIGAN munosabatniki
        // (asosiysi, bo'lmasa oxirgisi): HR filtrlab ko'rgan qatordagi sana
        // doim oraliq ichida bo'lishi kerak.
        var byTermination = status.Value == EmploymentStatus.Terminated;

        return new EmployeeSearchFilter(
            Normalize(query.Search),
            query.DepartmentId,
            query.PositionId,
            query.DirectionId,
            query.LegalEntityId,
            status.Value,
            string.Equals(query.Status?.Trim(), EmployedFilter, StringComparison.OrdinalIgnoreCase),
            query.NeedsReview,
            page,
            pageSize,
            sort,
            descending,
            HiredFrom: byTermination ? null : query.From,
            HiredTo: byTermination ? null : query.To,
            TerminatedFrom: byTermination ? query.From : null,
            TerminatedTo: byTermination ? query.To : null);
    }

    /// <summary>
    /// Bitta qatorni ekranga/faylga chiqadigan DTO'ga aylantiradi.
    /// </summary>
    /// <remarks>
    /// <c>internal static</c> — eksport ham AYNAN shu mapping'dan foydalanadi:
    /// oklad ustuni ikkalasida ham bir xil qoida bilan (<c>canSeeSalary</c>)
    /// ko'rinadi yoki yashiriladi, alohida yozilgan ikkinchi xarita
    /// ertami-kechmi bittasida yangilanib, ikkinchisida eskirib qolardi.
    /// </remarks>
    internal static EmployeeListItemDto ToDto(EmployeeSearchRow row, bool canSeeSalary)
    {
        var fullName = FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName);

        // Oklad ustuni ro'yxatda ham «salary.view» ga bog'liq: HR aynan shu
        // jadvalni ekranga chiqarib boshqalarga ko'rsatadi.
        MoneyDto? salary = null;
        if (canSeeSalary && row.SalaryAmount is { } amount)
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
