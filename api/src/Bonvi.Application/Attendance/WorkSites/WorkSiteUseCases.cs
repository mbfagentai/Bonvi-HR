using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Catalog.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.WorkSites;

/// <summary>Ko'pburchakning bitta uchi — <c>{ "lat": …, "lng": … }</c>.</summary>
/// <remarks>
/// <para>
/// Loyihadagi qolgan DTO'lardek pozitsion yozuv EMAS va bu ataylab — ikki
/// teshikni bog'lashning o'zida yopish uchun:
/// </para>
/// <list type="bullet">
///   <item><b>Struktura</b> — JSON massividagi <c>null</c> element 400 bo'lib
///         rad etiladi, ishlovchiga <c>NullReferenceException</c> bo'lib
///         yetib kelmaydi.</item>
///   <item><b><c>required</c></b> — <c>lat</c> yoki <c>lng</c> tushib qolsa
///         ham 400. Aks holda u jimgina 0 bo'lardi, (0, 0) esa Gvineya
///         ko'rfazidagi mutlaqo haqiqiy nuqta. OpenAPI ham ikkalasini
///         majburiy deb ko'rsatadi.</item>
/// </list>
/// </remarks>
public readonly record struct WorkSiteVertexDto
{
    /// <summary>Kenglik, −90…90.</summary>
    public required decimal Lat { get; init; }

    /// <summary>Uzunlik, −180…180.</summary>
    public required decimal Lng { get; init; }
}

/// <summary>Obyektning tashqi ko'rinishi.</summary>
/// <param name="Id">Identifikator.</param>
/// <param name="Name">Nom — ma'lumotnoma bo'yicha unikal.</param>
/// <param name="Address">Erkin matnli manzil.</param>
/// <param name="Shape">To'siq shakli.</param>
/// <param name="Latitude">Doirada — markaz; ko'pburchakda — uchlarning o'rtacha nuqtasi (server hisoblaydi).</param>
/// <param name="Longitude">Doirada — markaz; ko'pburchakda — uchlarning o'rtacha nuqtasi.</param>
/// <param name="RadiusMeters">Doira radiusi, metr. Ko'pburchakda ishlatilmaydi.</param>
/// <param name="Polygon">Ko'pburchak uchlari; doirada <c>null</c>.</param>
/// <param name="EdgeBufferMeters">Ko'pburchak chetidan qabul zaxirasi, metr. Doirada ishlatilmaydi.</param>
/// <param name="IsActive">Bu yerda belgi qabul qilinadimi.</param>
/// <param name="Note">Izoh.</param>
public sealed record WorkSiteDto(
    Guid Id,
    string Name,
    string? Address,
    WorkSiteShape Shape,
    decimal Latitude,
    decimal Longitude,
    int RadiusMeters,
    IReadOnlyList<WorkSiteVertexDto>? Polygon,
    int EdgeBufferMeters,
    bool IsActive,
    string? Note);

/// <summary>Obyektni yaratish yoki tahrirlash maydonlari.</summary>
/// <remarks>
/// <para>
/// ⚠️ Umumiy <c>SaveReferenceCommand</c> ATAYLAB ishlatilmadi. Ikki sabab:
/// (1) u to'qqizta ma'lumotnomaga umumiy va unga koordinata bilan radius
/// qo'shish davomatga xos uchta maydonni boshqa sakkizta ma'lumotnomaning
/// formasiga olib kirardi; (2) obyektlarda YOZISH ruxsati boshqa
/// (<c>attendance.manage</c>, sababi marshrut faylida), umumiy shtamp esa
/// <c>catalog.manage</c> ni qattiq yozib qo'ygan.
/// </para>
/// <para>
/// Qaysi maydonlar o'qilishini <see cref="Shape"/> hal qiladi
/// (SPEC-joylashuvlar §C): doirada — markaz va radius, ko'pburchakda —
/// uchlar va zaxira. Boshqa shaklning maydonlari yuborilsa ham e'tiborga
/// olinmaydi.
/// </para>
/// </remarks>
/// <param name="Name">Nom.</param>
/// <param name="Shape">Shakl — majburiy.</param>
/// <param name="Address">Manzil, ≤ 300 belgi.</param>
/// <param name="Latitude">Doira markazining kengligi — doirada majburiy.</param>
/// <param name="Longitude">Doira markazining uzunligi — doirada majburiy.</param>
/// <param name="RadiusMeters">Doira radiusi — doirada majburiy.</param>
/// <param name="Polygon">Ko'pburchak uchlari — ko'pburchakda majburiy.</param>
/// <param name="EdgeBufferMeters">
/// Ko'pburchak chet zaxirasi. Berilmasa: yangi obyektda 50, tahrirda — hozirgi qiymati.
/// </param>
/// <param name="Note">Izoh.</param>
public sealed record SaveWorkSiteCommand(
    string? Name,
    WorkSiteShape Shape,
    string? Address = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    int? RadiusMeters = null,
    IReadOnlyList<WorkSiteVertexDto>? Polygon = null,
    int? EdgeBufferMeters = null,
    string? Note = null);

/// <summary>Obyektlar ro'yxati.</summary>
/// <param name="OnlyActive">Faqat faollari (tanlash ro'yxatlari uchun).</param>
public sealed record ListWorkSitesQuery(bool OnlyActive = false);

/// <summary>Bitta obyekt.</summary>
/// <param name="Id">Identifikator.</param>
public sealed record GetWorkSiteQuery(Guid Id);

/// <summary>Obyektni yaratadi.</summary>
/// <param name="Command">Maydonlar.</param>
public sealed record CreateWorkSiteCommand(SaveWorkSiteCommand Command);

/// <summary>Obyektni tahrirlaydi.</summary>
/// <param name="Id">Obyekt.</param>
/// <param name="Command">Yangi maydonlar.</param>
public sealed record UpdateWorkSiteCommand(Guid Id, SaveWorkSiteCommand Command);

/// <summary>Obyektni yoqadi yoki o'chiradi.</summary>
/// <param name="Id">Obyekt.</param>
/// <param name="Active">Yoqilsinmi.</param>
/// <param name="Reason">Nima uchun — izohga yoziladi.</param>
public sealed record SetWorkSiteActiveCommand(Guid Id, bool Active, string? Reason = null);

/// <summary><see cref="ListWorkSitesQuery"/> ni bajaradi.</summary>
public sealed class ListWorkSitesQueryHandler
{
    private readonly IReferenceRepository<WorkSite> _sites;

    public ListWorkSitesQueryHandler(IReferenceRepository<WorkSite> sites) => _sites = sites;

    public async Task<Result<IReadOnlyList<WorkSiteDto>>> HandleAsync(
        ListWorkSitesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var items = await _sites.ListAsync(query.OnlyActive, cancellationToken).ConfigureAwait(false);

        // Tartib alifbo bo'yicha va MADANIYAT solishtiruvi bilan — kirill
        // va lotin aralash ro'yxatda ordinal tartib odamga tasodifiy
        // ko'rinardi (`ListReferenceQueryHandler` dagi bilan bir xil qaror).
        IReadOnlyList<WorkSiteDto> result =
        [
            .. items.OrderBy(site => site.Name, StringComparer.CurrentCulture).Select(WorkSiteMapper.ToDto),
        ];

        return Result.Success(result);
    }
}

/// <summary><see cref="GetWorkSiteQuery"/> ni bajaradi.</summary>
public sealed class GetWorkSiteQueryHandler
{
    private readonly IReferenceRepository<WorkSite> _sites;

    public GetWorkSiteQueryHandler(IReferenceRepository<WorkSite> sites) => _sites = sites;

    public async Task<Result<WorkSiteDto>> HandleAsync(
        GetWorkSiteQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var site = await _sites.GetByIdAsync(query.Id, cancellationToken).ConfigureAwait(false);

        return site is null
            ? AttendanceErrors.WorkSiteNotFound
            : Result.Success(WorkSiteMapper.ToDto(site));
    }
}

/// <summary>
/// <see cref="CreateWorkSiteCommand"/> ni bajaradi.
/// </summary>
/// <remarks>
/// Qadamlar tartibi <c>CreateReferenceCommandHandler</c> dagidek: avval
/// arzon tekshiruv (domen — nom, shakl, manzil), keyin bazaga bitta
/// so'rov (nom bandmi). Nom bandligi tekshiruvi bazadagi
/// <c>ux_work_sites_name</c> ni ALMASHTIRMAYDI — maqsad odatiy holatda
/// 500 emas, tushunarli 409 berish.
/// </remarks>
public sealed class CreateWorkSiteCommandHandler
{
    private readonly IReferenceRepository<WorkSite> _sites;
    private readonly IUnitOfWork _unitOfWork;

    public CreateWorkSiteCommandHandler(IReferenceRepository<WorkSite> sites, IUnitOfWork unitOfWork)
    {
        _sites = sites;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<WorkSiteDto>> HandleAsync(
        CreateWorkSiteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var fields = command.Command;

        var created = fields.Shape switch
        {
            WorkSiteShape.Circle => CreateCircle(fields),
            WorkSiteShape.Polygon => WorkSite.CreatePolygon(
                fields.Name,
                WorkSiteMapper.ToVertices(fields.Polygon),
                fields.EdgeBufferMeters),
            _ => AttendanceErrors.WorkSiteShapeInvalid,
        };

        if (created.IsFailure)
        {
            return created.Error;
        }

        var addressed = created.Value.SetAddress(fields.Address);
        if (addressed.IsFailure)
        {
            return addressed.Error;
        }

        if (await _sites.NameExistsAsync(created.Value.Name, null, cancellationToken).ConfigureAwait(false))
        {
            return CatalogErrors.NameTaken;
        }

        created.Value.SetNote(fields.Note);

        _sites.Add(created.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return WorkSiteMapper.ToDto(created.Value);
    }

    /// <summary>
    /// Doira: markaz ham, radius ham MAJBURIY (SPEC-joylashuvlar §C).
    /// </summary>
    /// <remarks>
    /// Radius ilgari berilmasa 200 bo'lardi. Endi so'raladi: HR tahrirlagichda
    /// radiusni har doim ko'radi va tanlaydi, jimgina qo'yilgan sukut esa
    /// to'siqni hech kim tanlamagan o'lchamda qoldirardi.
    /// </remarks>
    private static Result<WorkSite> CreateCircle(SaveWorkSiteCommand fields)
    {
        if (fields.Latitude is not { } latitude)
        {
            return AttendanceErrors.WorkSiteLatitudeMissing;
        }

        if (fields.Longitude is not { } longitude)
        {
            return AttendanceErrors.WorkSiteLongitudeMissing;
        }

        if (fields.RadiusMeters is not { } radius)
        {
            return AttendanceErrors.WorkSiteRadiusMissing;
        }

        return WorkSite.Create(fields.Name, latitude, longitude, radius);
    }
}

/// <summary><see cref="UpdateWorkSiteCommand"/> ni bajaradi.</summary>
/// <remarks>
/// <para>
/// ⚠️ Radius, zaxira yoki SHAKLNI o'zgartirish TARIXNI qayta yozmaydi: har
/// bir qabul qilingan belgi o'sha lahzadagi masofa, ruxsat va shaklni
/// o'zida muzlatib saqlaydi (<c>MobileCheckIn.DistanceMeters</c>,
/// <c>SiteRadiusMeters</c>, <c>SiteShape</c>). Shu sababli bu amal
/// o'tmishdagi qarorlarni hukm qilib bo'lmaydigan holga keltirmaydi.
/// </para>
/// <para>
/// Ko'pburchakda zaxira berilmasa HOZIRGI qiymati qoladi, 50 ga
/// qaytarilmaydi: HR ataylab 0 qo'ygan obyektni uchlarini tuzatish bilan
/// jimgina 50 metrga kengaytirish noto'g'ri bo'lardi.
/// </para>
/// </remarks>
public sealed class UpdateWorkSiteCommandHandler
{
    private readonly IReferenceRepository<WorkSite> _sites;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateWorkSiteCommandHandler(IReferenceRepository<WorkSite> sites, IUnitOfWork unitOfWork)
    {
        _sites = sites;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<WorkSiteDto>> HandleAsync(
        UpdateWorkSiteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var site = await _sites.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (site is null)
        {
            return AttendanceErrors.WorkSiteNotFound;
        }

        var fields = command.Command;

        var renamed = site.Rename(fields.Name);
        if (renamed.IsFailure)
        {
            return renamed.Error;
        }

        var reconfigured = fields.Shape switch
        {
            WorkSiteShape.Circle => ReconfigureCircle(site, fields),
            WorkSiteShape.Polygon => site.ReconfigurePolygon(
                WorkSiteMapper.ToVertices(fields.Polygon),
                fields.EdgeBufferMeters ?? site.EdgeBufferMeters),
            _ => AttendanceErrors.WorkSiteShapeInvalid,
        };

        if (reconfigured.IsFailure)
        {
            return reconfigured.Error;
        }

        var addressed = site.SetAddress(fields.Address);
        if (addressed.IsFailure)
        {
            return addressed.Error;
        }

        if (await _sites.NameExistsAsync(site.Name, site.Id, cancellationToken).ConfigureAwait(false))
        {
            return CatalogErrors.NameTaken;
        }

        site.SetNote(fields.Note);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return WorkSiteMapper.ToDto(site);
    }

    /// <summary>Doira: markaz ham, radius ham majburiy — yaratishdagi bilan bir xil sabab.</summary>
    /// <remarks>
    /// Ko'pburchakdan doiraga o'tkazilayotgan obyektda hozirgi radius —
    /// hech kim tanlamagan to'ldiruvchi qiymat (sukut 200). Uni jimgina
    /// meros qilib olish to'siqni tasodifiy o'lchamda qoldirardi.
    /// </remarks>
    private static Result ReconfigureCircle(WorkSite site, SaveWorkSiteCommand fields)
    {
        if (fields.Latitude is not { } latitude)
        {
            return AttendanceErrors.WorkSiteLatitudeMissing;
        }

        if (fields.Longitude is not { } longitude)
        {
            return AttendanceErrors.WorkSiteLongitudeMissing;
        }

        if (fields.RadiusMeters is not { } radius)
        {
            return AttendanceErrors.WorkSiteRadiusMissing;
        }

        return site.Reconfigure(latitude, longitude, radius);
    }
}

/// <summary>
/// <see cref="SetWorkSiteActiveCommand"/> ni bajaradi.
/// </summary>
/// <remarks>
/// Faolsizlantirilgan obyekt O'CHMAYDI: tarixiy belgilar unga ishora qilib
/// turadi (<c>mobile_check_ins.work_site_id</c> — <c>RESTRICT</c>) va
/// «u qayerda belgilagan edi» degan savol javobsiz qolmasligi kerak.
/// </remarks>
public sealed class SetWorkSiteActiveCommandHandler
{
    private readonly IReferenceRepository<WorkSite> _sites;
    private readonly IUnitOfWork _unitOfWork;

    public SetWorkSiteActiveCommandHandler(IReferenceRepository<WorkSite> sites, IUnitOfWork unitOfWork)
    {
        _sites = sites;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        SetWorkSiteActiveCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var site = await _sites.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (site is null)
        {
            return AttendanceErrors.WorkSiteNotFound;
        }

        if (command.Active)
        {
            site.Activate();
        }
        else
        {
            site.Deactivate(command.Reason);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary>Obyektni tashqi ko'rinishga o'giradi va qaytaradi — yagona nusxa.</summary>
internal static class WorkSiteMapper
{
    public static WorkSiteDto ToDto(WorkSite site)
        => new(
            site.Id,
            site.Name,
            site.Address,
            site.Shape,
            site.Latitude,
            site.Longitude,
            site.RadiusMeters,
            site.Polygon?
                .Select(vertex => new WorkSiteVertexDto { Lat = vertex.Latitude, Lng = vertex.Longitude })
                .ToArray(),
            site.EdgeBufferMeters,
            site.IsActive,
            site.Note);

    /// <summary>So'rovdagi uchlar → domen nuqtalari; <c>null</c> o'zgarmaydi (domen uni rad etadi).</summary>
    public static GeoPoint[]? ToVertices(IReadOnlyList<WorkSiteVertexDto>? polygon)
        => polygon?.Select(vertex => new GeoPoint(vertex.Lat, vertex.Lng)).ToArray();
}
