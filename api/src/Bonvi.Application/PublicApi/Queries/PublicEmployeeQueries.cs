using Bonvi.Application.Common.Photos;
using Bonvi.Application.Employees.Dtos;
using Bonvi.Application.PublicApi.Abstractions;
using Bonvi.Application.PublicApi.Common;
using Bonvi.Application.PublicApi.Dtos;
using Bonvi.Domain.Common;
using Bonvi.Domain.Employees;
using Bonvi.Domain.ValueObjects;

namespace Bonvi.Application.PublicApi.Queries;

/// <summary>
/// Ommaviy xodimlar ro'yxati.
/// </summary>
/// <remarks>
/// Parametrlar ATAYLAB xom turlar (satr va <c>Guid?</c>): ular so'rov
/// qatoridan keladi va sanoqni minimal API registrga sezgir bog'laydi
/// (sabab <c>SearchEmployeesQuery.ParseStatus</c> izohida). Bu yerda
/// yana bitta sabab bor — OpenAPI: nullable bo'lmagan sukut qiymat
/// hujjatni yiqitadi (SPEC §8 dagi 2026-08-29 tuzog'i).
/// </remarks>
/// <param name="Search">F.I.Sh. / JSHSHIR / telefon / pasport bo'yicha qidiruv.</param>
/// <param name="LegalEntityId">Yuridik shaxs.</param>
/// <param name="DepartmentId">Bo'lim — ichki bo'limlari bilan birga.</param>
/// <param name="PositionId">Lavozim.</param>
/// <param name="DirectionId">Yo'nalish.</param>
/// <param name="Status">Holat: <c>Probation</c> | <c>Active</c> | <c>Terminated</c>.</param>
/// <param name="UpdatedSince">Shu lahzadan keyin o'zgargan yozuvlar.</param>
/// <param name="Page">Sahifa raqami, 1 dan.</param>
/// <param name="PageSize">Sahifa hajmi: sukut 50, eng ko'pi 200.</param>
public sealed record ListPublicEmployeesQuery(
    string? Search = null,
    Guid? LegalEntityId = null,
    Guid? DepartmentId = null,
    Guid? PositionId = null,
    Guid? DirectionId = null,
    string? Status = null,
    DateTimeOffset? UpdatedSince = null,
    int? Page = null,
    int? PageSize = null);

/// <summary>
/// Ommaviy ro'yxatni yig'adi.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sahifa hajmi KESILADI, xato qaytarmaydi</b> (<c>pageSize=100000</c>
/// → 200): iste'molchining sinxronlash skripti yarim yo'lda 400 olib
/// to'xtab qolmasligi kerak, u shunchaki kichikroq sahifalar bilan
/// davom etadi. Ichki ro'yxatda ham AYNAN shu qoida.
/// </para>
/// <para>
/// <b>Holat esa aytiladi.</b> Saralashdan farqli (u jimgina sukutga
/// tushadi), noto'g'ri filtr natijaning MAZMUNINI o'zgartiradi:
/// «terminatd» deb yozilgan so'rov jimgina butun ro'yxatni bersa,
/// iste'molchi buni sezmasdi.
/// </para>
/// </remarks>
public sealed class ListPublicEmployeesQueryHandler
{
    /// <summary>Sukut bo'yicha sahifa hajmi (SPEC §4).</summary>
    public const int DefaultPageSize = 50;

    /// <summary>Eng katta sahifa hajmi (SPEC §4).</summary>
    public const int MaxPageSize = 200;

    private readonly IPublicApiReadModel _readModel;

    public ListPublicEmployeesQueryHandler(IPublicApiReadModel readModel) => _readModel = readModel;

    public async Task<Result<PagedResult<PublicEmployeeDto>>> HandleAsync(
        ListPublicEmployeesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var status = PublicEmploymentStatusFilter.Parse(query.Status);
        if (status.IsFailure)
        {
            return status.Error;
        }

        var page = query.Page is { } requested && requested > 0 ? requested : 1;
        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);

        var found = await _readModel
            .SearchAsync(
                new PublicEmployeeFilter(
                    query.Search,
                    query.LegalEntityId,
                    query.DepartmentId,
                    query.PositionId,
                    query.DirectionId,
                    status.Value,
                    query.UpdatedSince,
                    page,
                    pageSize),
                cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PublicEmployeeDto> items = [.. found.Items.Select(PublicEmployeeMapper.ToDto)];

        var pageCount = pageSize == 0 ? 0 : (found.Total + pageSize - 1) / pageSize;

        return Result.Success(new PagedResult<PublicEmployeeDto>(
            items,
            page,
            pageSize,
            found.Total,
            pageCount));
    }
}

/// <summary>Bitta xodim — ommaviy shakl.</summary>
public sealed record GetPublicEmployeeQuery(Guid Id);

/// <summary>Xodimni beradi yoki <c>tashqi.xodim_topilmadi</c> qaytaradi.</summary>
public sealed class GetPublicEmployeeQueryHandler
{
    private readonly IPublicApiReadModel _readModel;

    public GetPublicEmployeeQueryHandler(IPublicApiReadModel readModel) => _readModel = readModel;

    public async Task<Result<PublicEmployeeDto>> HandleAsync(
        GetPublicEmployeeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var row = await _readModel.FindAsync(query.Id, cancellationToken).ConfigureAwait(false);

        return row is null
            ? PublicApiErrors.EmployeeNotFound
            : Result.Success(PublicEmployeeMapper.ToDto(row));
    }
}

/// <summary>O'chirilgan xodimlar ro'yxati.</summary>
/// <param name="Since">Shu lahzadan keyin o'chirilganlar. <c>null</c> — hammasi.</param>
public sealed record ListPublicDeletionsQuery(DateTimeOffset? Since = null);

/// <summary>
/// Yumshoq o'chirilgan xodimlarni beradi.
/// </summary>
/// <remarks>
/// ⭐ Bu endpoint bor bo'lgani uchun <c>/employees</c> o'chirilganlarni
/// UMUMAN qaytarmaydi. Aks holda iste'molchi «yozuv yo'qoldi» va «yozuv
/// hali kelmadi» ni ajrata olmasdi: birinchisida u o'z nusxasini
/// tozalashi kerak, ikkinchisida — kutishi.
/// </remarks>
public sealed class ListPublicDeletionsQueryHandler
{
    private readonly IPublicApiReadModel _readModel;

    public ListPublicDeletionsQueryHandler(IPublicApiReadModel readModel) => _readModel = readModel;

    public async Task<Result<IReadOnlyList<PublicDeletionDto>>> HandleAsync(
        ListPublicDeletionsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rows = await _readModel
            .ListDeletionsAsync(query.Since, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PublicDeletionDto> items =
            [.. rows.Select(row => new PublicDeletionDto(row.Id, row.DeletedAt))];

        return Result.Success(items);
    }
}

/// <summary>Ma'lumotnoma ro'yxati.</summary>
/// <param name="Kind">Qaysi ma'lumotnoma.</param>
public sealed record ListPublicReferenceQuery(PublicReferenceKind Kind);

/// <summary>
/// Ma'lumotnoma yozuvlarini beradi: <c>{ id, name }</c>.
/// </summary>
/// <remarks>
/// Faolsizlantirilgan yozuvlar ham qaytadi: xodimning munosabatida
/// tarixan o'sha bo'lim turgan bo'lishi mumkin va iste'molchi uning
/// nomini topa olmasa, javobdagi <c>departmentId</c> ma'nosiz raqam
/// bo'lib qolardi.
/// </remarks>
public sealed class ListPublicReferenceQueryHandler
{
    private readonly IPublicApiReadModel _readModel;

    public ListPublicReferenceQueryHandler(IPublicApiReadModel readModel) => _readModel = readModel;

    public async Task<Result<IReadOnlyList<PublicReferenceDto>>> HandleAsync(
        ListPublicReferenceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rows = await _readModel
            .ListReferenceAsync(query.Kind, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PublicReferenceDto> items =
            [.. rows.Select(row => new PublicReferenceDto(row.Id, row.Name, row.ParentId))];

        return Result.Success(items);
    }
}

/// <summary>Xodim rasmi — ommaviy marshrut.</summary>
/// <param name="EmployeeId">Xodim.</param>
/// <param name="KnownVersion">Mijozdagi nusxaning belgisi (<c>If-None-Match</c>).</param>
public sealed record GetPublicEmployeePhotoQuery(Guid EmployeeId, string? KnownVersion = null);

/// <summary>
/// Rasm baytlarini beradi.
/// </summary>
/// <remarks>
/// Quvur ichki rasm endpoint'i bilan AYNAN bir xil
/// (<c>SquarePhotoStore</c>): bir xil <c>ETag</c>, bir xil 304, bir xil
/// kesh siyosati. Ikkinchi quvur yozilsa, biri o'zgarganda ikkinchisi
/// eskirib qolardi.
/// <para>
/// Xato kodlari esa ommaviy: xodim yo'q — <c>tashqi.xodim_topilmadi</c>,
/// rasm yo'q — <c>tashqi.surat_yoq</c>. Ikkalasi ham 404.
/// </para>
/// </remarks>
public sealed class GetPublicEmployeePhotoQueryHandler
{
    private readonly IPublicApiReadModel _readModel;
    private readonly SquarePhotoStore _photos;

    public GetPublicEmployeePhotoQueryHandler(IPublicApiReadModel readModel, SquarePhotoStore photos)
    {
        _readModel = readModel;
        _photos = photos;
    }

    public async Task<Result<PhotoContent>> HandleAsync(
        GetPublicEmployeePhotoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var key = await _readModel
            .FindPhotoKeyAsync(query.EmployeeId, cancellationToken)
            .ConfigureAwait(false);

        if (key is null)
        {
            // «Xodim yo'q» va «rasmi yo'q» bu yerda BIRLASHADI va ikkalasi
            // ham `tashqi.surat_yoq` bo'ladi. Ataylab: ommaviy yuzada
            // «xodim bor, lekin rasmi yo'q» degan javob xodimning
            // mavjudligini tasdiqlardi — ya'ni identifikatorlarni tanlab
            // ko'rish uchun arzon tekshirgich bo'lib qolardi. Xodimning
            // borligini bilish uchun `/employees/{id}` bor.
            return PublicApiErrors.PhotoNotFound;
        }

        var content = await _photos
            .OpenAsync(key, query.KnownVersion, cancellationToken)
            .ConfigureAwait(false);

        return content is null
            ? PublicApiErrors.PhotoNotFound
            : Result.Success(content);
    }
}

/// <summary>
/// <c>?status=</c> parametrini sanoqqa o'giradi.
/// </summary>
/// <remarks>
/// <c>Employed</c> qiymati bu yerda YO'Q — ichki ro'yxatdan farqli. U
/// ikkita holatning birlashmasi va faqat ekran filtri uchun yaratilgan
/// (<c>EmployeeSearchFilter.HideTerminated</c> izohi); tashqi
/// iste'molchiga esa aniq sanoq qiymatlari kerak, aks holda uning kodida
/// «employed nima?» degan ikkinchi ta'rif paydo bo'lardi.
/// </remarks>
internal static class PublicEmploymentStatusFilter
{
    public static Result<EmploymentStatus?> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result.Success<EmploymentStatus?>(null);
        }

        // `Enum.TryParse` RAQAMNI ham qabul qiladi («99» muvaffaqiyatli
        // bog'lanadi), shu sababli `IsDefined` ham kerak — ichki ro'yxatdagi
        // bilan bir xil sabab.
        if (!Enum.TryParse<EmploymentStatus>(raw.Trim(), ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
        {
            return Error.Validation(
                "tashqi.holat_notogri",
                "Holat noto'g'ri. Ruxsat etilgan qiymatlar: Probation, Active, Terminated.");
        }

        return Result.Success<EmploymentStatus?>(parsed);
    }
}

/// <summary>
/// O'qish modelining qatorini ommaviy DTO'ga aylantiradi.
/// </summary>
/// <remarks>
/// Ro'yxat va bitta xodim AYNAN shu xaritadan o'tadi: ikkinchi nusxa
/// yozilsa, ertami-kechmi bittasiga maydon qo'shilib, ikkinchisi
/// eskirib qolardi — va aynan shunday holatda «maydon sizib chiqishi»
/// testi faqat bitta yo'lni qo'riqlardi.
/// </remarks>
internal static class PublicEmployeeMapper
{
    public static PublicEmployeeDto ToDto(PublicEmployeeRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var fullName = FullName.FromTrusted(row.LastName, row.FirstName, row.MiddleName);

        return new PublicEmployeeDto(
            row.Id,
            fullName.Full,
            fullName.ShortForm,
            row.LastName,
            row.FirstName,
            row.MiddleName,
            row.BirthDate,
            row.Gender,
            row.Phone,
            row.TelegramUrl,
            row.PhotoKey,
            row.Status,
            row.HireDate,
            row.TerminationDate,
            [.. row.Employments.Select(ToDto)],
            [.. row.Directions.Select(direction => new PublicRefDto(direction.Id, direction.Name))],
            row.UpdatedAt);
    }

    private static PublicEmploymentDto ToDto(PublicEmploymentRow row) => new(
        row.Id,
        Ref(row.LegalEntityId, row.LegalEntityName),
        Ref(row.DepartmentId, row.DepartmentName),
        Ref(row.PositionId, row.PositionName),
        row.Rate,
        row.Status,
        row.HireDate,
        row.TerminationDate,
        row.IsPrimary);

    /// <summary>
    /// Identifikator va nomdan havola yasaydi.
    /// </summary>
    /// <remarks>
    /// Nom topilmasa (ma'lumotnoma yozuvi o'chirilgan) butun havola
    /// <c>null</c> bo'ladi, nomi bo'sh havola emas: iste'molchi tomonda
    /// «nomi yo'q bo'lim» degan yozuv faqat chalkashlik berardi.
    /// </remarks>
    private static PublicRefDto? Ref(Guid? id, string? name)
        => id is { } value && name is not null ? new PublicRefDto(value, name) : null;
}
