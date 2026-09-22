using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Catalog.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.WorkSites;

/// <summary>Obyektning tashqi ko'rinishi.</summary>
/// <param name="Id">Identifikator.</param>
/// <param name="Name">Nom — ma'lumotnoma bo'yicha unikal.</param>
/// <param name="Latitude">Markaz kengligi.</param>
/// <param name="Longitude">Markaz uzunligi.</param>
/// <param name="RadiusMeters">To'siq radiusi, metr.</param>
/// <param name="IsActive">Bu yerda belgi qabul qilinadimi.</param>
/// <param name="Note">Izoh.</param>
public sealed record WorkSiteDto(
    Guid Id,
    string Name,
    decimal Latitude,
    decimal Longitude,
    int RadiusMeters,
    bool IsActive,
    string? Note);

/// <summary>Obyektni yaratish yoki tahrirlash maydonlari.</summary>
/// <remarks>
/// ⚠️ Umumiy <c>SaveReferenceCommand</c> ATAYLAB ishlatilmadi. Ikki sabab:
/// (1) u to'qqizta ma'lumotnomaga umumiy va unga koordinata bilan radius
/// qo'shish davomatga xos uchta maydonni boshqa sakkizta ma'lumotnomaning
/// formasiga olib kirardi; (2) obyektlarda YOZISH ruxsati boshqa
/// (<c>attendance.manage</c>, sababi marshrut faylida), umumiy shtamp esa
/// <c>catalog.manage</c> ni qattiq yozib qo'ygan.
/// </remarks>
/// <param name="Name">Nom.</param>
/// <param name="Latitude">Kenglik.</param>
/// <param name="Longitude">Uzunlik.</param>
/// <param name="RadiusMeters">Radius; berilmasa 200.</param>
/// <param name="Note">Izoh.</param>
public sealed record SaveWorkSiteCommand(
    string? Name,
    decimal Latitude,
    decimal Longitude,
    int? RadiusMeters = null,
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
/// arzon tekshiruv (domen — nom, koordinata, radius), keyin bazaga bitta
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

        var created = WorkSite.Create(fields.Name, fields.Latitude, fields.Longitude, fields.RadiusMeters);
        if (created.IsFailure)
        {
            return created.Error;
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
}

/// <summary><see cref="UpdateWorkSiteCommand"/> ni bajaradi.</summary>
/// <remarks>
/// ⚠️ Radiusni o'zgartirish TARIXNI qayta yozmaydi: har bir qabul qilingan
/// belgi o'sha lahzadagi radiusni o'zida muzlatib saqlaydi
/// (<c>MobileCheckIn.SiteRadiusMeters</c>). Shu sababli bu amal o'tmishdagi
/// qarorlarni hukm qilib bo'lmaydigan holga keltirmaydi.
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

        var reconfigured = site.Reconfigure(
            fields.Latitude,
            fields.Longitude,
            fields.RadiusMeters ?? site.RadiusMeters);

        if (reconfigured.IsFailure)
        {
            return reconfigured.Error;
        }

        if (await _sites.NameExistsAsync(site.Name, site.Id, cancellationToken).ConfigureAwait(false))
        {
            return CatalogErrors.NameTaken;
        }

        site.SetNote(fields.Note);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return WorkSiteMapper.ToDto(site);
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

/// <summary>Obyektni tashqi ko'rinishga o'giradi — yagona nusxa.</summary>
internal static class WorkSiteMapper
{
    public static WorkSiteDto ToDto(WorkSite site)
        => new(
            site.Id,
            site.Name,
            site.Latitude,
            site.Longitude,
            site.RadiusMeters,
            site.IsActive,
            site.Note);
}
