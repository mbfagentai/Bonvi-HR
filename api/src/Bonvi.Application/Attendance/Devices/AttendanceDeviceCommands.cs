using Bonvi.Application.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Attendance.Ingest;
using Bonvi.Application.Catalog.Common;
using Bonvi.Application.Employees.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;
using Bonvi.Domain.Organization;

namespace Bonvi.Application.Attendance.Devices;

/// <summary>Qurilmaning sozlanadigan maydonlari — ro'yxatga olish va tahrir uchun bir xil.</summary>
/// <param name="Name">Nom. Majburiy va takrorlanmas.</param>
/// <param name="SerialNumber">Seriya raqami — berilsa, takrorlanmas.</param>
/// <param name="Model">Model (faqat hujjat uchun).</param>
/// <param name="Provider">Hodisani qaysi tahlilchi o'qiydi.</param>
/// <param name="IdentityProvider">Shaxs raqamlari qaysi fazoda.</param>
/// <param name="DirectionRole">Yo'nalish qurilmaning o'zidanmi yoki rolidanmi.</param>
/// <param name="LegalEntityId">Firma.</param>
/// <param name="DepartmentId">Bo'lim.</param>
/// <param name="LocationNote">Joylashuvi.</param>
/// <param name="AuthMode">Tasdiqlash usuli.</param>
/// <param name="AllowedIps">Ruxsat etilgan manbalar; bo'sh — cheklovsiz.</param>
/// <param name="HmacSecret">
/// Imzo siri, OCHIQ holda — serverda shifrlanadi. Tahrirda: <c>null</c> — o'zgarmaydi,
/// <c>""</c> — o'chiriladi (SPEC §6.2). Hech qachon javobda qaytmaydi.
/// </param>
public sealed record AttendanceDeviceSettings(
    string? Name,
    string? SerialNumber,
    string? Model,
    AttendanceProvider Provider,
    AttendanceIdentityProvider IdentityProvider,
    DeviceDirectionRole DirectionRole,
    Guid? LegalEntityId,
    Guid? DepartmentId,
    string? LocationNote,
    DeviceAuthMode AuthMode,
    string? AllowedIps,
    string? HmacSecret);

/// <summary>Yangi terminalni ro'yxatga oladi va unga token beradi.</summary>
/// <param name="Settings">Qurilma maydonlari.</param>
public sealed record RegisterDeviceCommand(AttendanceDeviceSettings Settings);

/// <summary>Qurilma sozlamalarini qayta yozadi — token o'zgarmaydi.</summary>
/// <param name="DeviceId">Qurilma.</param>
/// <param name="Settings">Yangi maydonlar.</param>
public sealed record UpdateDeviceCommand(Guid DeviceId, AttendanceDeviceSettings Settings);

/// <summary>Tokenni almashtiradi — eskisi shu lahzada o'ladi.</summary>
/// <param name="DeviceId">Qurilma.</param>
public sealed record RotateDeviceTokenCommand(Guid DeviceId);

/// <summary>Qurilmani yoqadi yoki o'chiradi.</summary>
/// <param name="DeviceId">Qurilma.</param>
/// <param name="Active"><c>true</c> — faollashtirish, <c>false</c> — faolsizlantirish.</param>
public sealed record SetDeviceActiveCommand(Guid DeviceId, bool Active);

/// <summary>Qurilmani yumshoq o'chiradi — faqat birorta ham o'tish yozmagan bo'lsa.</summary>
/// <param name="DeviceId">Qurilma.</param>
public sealed record DeleteDeviceCommand(Guid DeviceId);

/// <summary>
/// Qurilma yaratish va tahririning umumiy tekshiruvlari.
/// </summary>
/// <remarks>
/// Ikki handler bir xil qoidani ikki marta yozmasin: IP ro'yxati, nom va
/// seriya takrori, firma va bo'lim mavjudligi. Domen qoidalari (bo'sh nom,
/// imzo siri) domenning o'zida.
/// </remarks>
public sealed class AttendanceDeviceRules
{
    private readonly IAttendanceDeviceRepository _devices;
    private readonly IReferenceRepository<LegalEntity> _legalEntities;
    private readonly IDepartmentRepository _departments;

    public AttendanceDeviceRules(
        IAttendanceDeviceRepository devices,
        IReferenceRepository<LegalEntity> legalEntities,
        IDepartmentRepository departments)
    {
        _devices = devices;
        _legalEntities = legalEntities;
        _departments = departments;
    }

    /// <summary>Domen tekshiruvidan KEYIN chaqiriladi: bazaga murojaat qiladiganlari.</summary>
    /// <param name="settings">Maydonlar.</param>
    /// <param name="exceptId">Tahrirlanayotgan qurilma — o'zi bilan takror sanalmaydi.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<Result> CheckAsync(
        AttendanceDeviceSettings settings,
        Guid? exceptId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        /*
          «Mobil ilova» provayderi formadan KELMASLIGI kerak.

          ⚠️ Bu server tomonidagi haqiqiy to'siq, ro'yxatdagi yashirish esa
          faqat xushmuomalalik. Shu provayder bilan ro'yxatga olingan
          HAQIQIY terminal qabul yo'lida `_parsers.Single(…)` ga urilib
          500 berardi — HR uni «terminal ishlamayapti» deb o'qirdi va
          sababini hech qachon topmasdi.
        */
        if (settings.Provider == AttendanceProvider.Mobile)
        {
            return AttendanceErrors.DeviceProviderMobile;
        }

        if (!AllowedIpList.IsValid(settings.AllowedIps))
        {
            return AttendanceErrors.AllowedIpsInvalid;
        }

        if (await _devices.NameExistsAsync(settings.Name!, exceptId, cancellationToken).ConfigureAwait(false))
        {
            return AttendanceErrors.DeviceNameTaken;
        }

        if (!string.IsNullOrWhiteSpace(settings.SerialNumber)
            && await _devices.SerialExistsAsync(settings.SerialNumber, exceptId, cancellationToken).ConfigureAwait(false))
        {
            return AttendanceErrors.DeviceSerialTaken;
        }

        if (settings.LegalEntityId is { } legalEntityId
            && !await _legalEntities.ExistsAsync(legalEntityId, cancellationToken).ConfigureAwait(false))
        {
            return EmployeeErrors.LegalEntityNotFound;
        }

        if (settings.DepartmentId is { } departmentId
            && !await _departments.ExistsAsync(departmentId, cancellationToken).ConfigureAwait(false))
        {
            return CatalogErrors.DepartmentNotFound;
        }

        return Result.Success();
    }
}

/// <summary><see cref="RegisterDeviceCommand"/> ni bajaradi.</summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Token FAQAT <see cref="AttendanceDeviceToken"/> orqali</b> yasaladi va
/// xeshlanadi — qabul yo'li aynan shu sinf bilan tekshiradi. Boshqacha xeshlash
/// (masalan base64 dan ochib) hech bir qurilmani o'tkazmasdi va xato faqat
/// «401» bo'lib ko'rinardi (B3).
/// </para>
/// <para>
/// Imzo siri <see cref="ISecretProtector"/> bilan shifrlanib saqlanadi —
/// <c>bot.token</c> sozlamasidagi qoida.
/// </para>
/// </remarks>
public sealed class RegisterDeviceCommandHandler
{
    private readonly IAttendanceDeviceRepository _devices;
    private readonly AttendanceDeviceRules _rules;
    private readonly AttendanceDeviceMapper _mapper;
    private readonly ISecretProtector _protector;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterDeviceCommandHandler(
        IAttendanceDeviceRepository devices,
        AttendanceDeviceRules rules,
        AttendanceDeviceMapper mapper,
        ISecretProtector protector,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _devices = devices;
        _rules = rules;
        _mapper = mapper;
        _protector = protector;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AttendanceDeviceCreatedDto>> HandleAsync(
        RegisterDeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var settings = command.Settings;
        var token = AttendanceDeviceToken.Generate();

        var registered = AttendanceDevice.Register(
            settings.Name,
            AttendanceDeviceToken.Hash(token),
            AttendanceDeviceToken.PrefixOf(token),
            _clock.UtcNow,
            _currentUser.UserId,
            settings.Provider,
            settings.IdentityProvider,
            settings.DirectionRole,
            settings.AuthMode,
            settings.SerialNumber,
            settings.Model,
            settings.LegalEntityId,
            settings.DepartmentId,
            settings.LocationNote,
            string.IsNullOrWhiteSpace(settings.HmacSecret) ? null : _protector.Protect(settings.HmacSecret.Trim()),
            settings.AllowedIps);

        if (registered.IsFailure)
        {
            return registered.Error;
        }

        var checkedRules = await _rules.CheckAsync(settings, exceptId: null, cancellationToken).ConfigureAwait(false);
        if (checkedRules.IsFailure)
        {
            return checkedRules.Error;
        }

        _devices.Add(registered.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var device = await _mapper.ToDtoAsync(registered.Value, cancellationToken).ConfigureAwait(false);

        return new AttendanceDeviceCreatedDto(device, token);
    }
}

/// <summary><see cref="UpdateDeviceCommand"/> ni bajaradi.</summary>
/// <remarks>
/// Imzo siri uch holatli (SPEC §6.2): <c>null</c> — eski shifrlangan qiymat
/// joyida qoladi (forma sirni hech qachon ko'rmaydi, ya'ni uni qayta yubora
/// olmaydi); <c>""</c> — o'chiriladi; boshqa qiymat — shifrlanib almashtiriladi.
/// </remarks>
public sealed class UpdateDeviceCommandHandler
{
    private readonly IAttendanceDeviceRepository _devices;
    private readonly AttendanceDeviceRules _rules;
    private readonly AttendanceDeviceMapper _mapper;
    private readonly ISecretProtector _protector;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDeviceCommandHandler(
        IAttendanceDeviceRepository devices,
        AttendanceDeviceRules rules,
        AttendanceDeviceMapper mapper,
        ISecretProtector protector,
        IUnitOfWork unitOfWork)
    {
        _devices = devices;
        _rules = rules;
        _mapper = mapper;
        _protector = protector;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AttendanceDeviceDto>> HandleAsync(
        UpdateDeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.DeviceId == AttendanceDevice.MobileAppId)
        {
            return AttendanceErrors.MobileAppDeviceProtected;
        }

        var device = await _devices.GetByIdAsync(command.DeviceId, cancellationToken).ConfigureAwait(false);
        if (device is null)
        {
            return AttendanceErrors.DeviceNotFound;
        }

        var settings = command.Settings;

        var hmacSecret = settings.HmacSecret switch
        {
            null => device.HmacSecret,
            { } plain when string.IsNullOrWhiteSpace(plain) => null,
            { } plain => _protector.Protect(plain.Trim()),
        };

        // Domen tekshiruvi (bo'sh nom, imzo siri) bazaga murojaatdan OLDIN:
        // noto'g'ri formada bitta ham SELECT yuborilmaydi.
        var renamed = device.Rename(settings.Name);
        if (renamed.IsFailure)
        {
            return renamed.Error;
        }

        var reconfigured = device.Reconfigure(
            settings.Provider,
            settings.IdentityProvider,
            settings.DirectionRole,
            settings.AuthMode,
            settings.SerialNumber,
            settings.Model,
            settings.LegalEntityId,
            settings.DepartmentId,
            settings.LocationNote,
            hmacSecret,
            settings.AllowedIps);

        if (reconfigured.IsFailure)
        {
            return reconfigured.Error;
        }

        var checkedRules = await _rules.CheckAsync(settings, device.Id, cancellationToken).ConfigureAwait(false);
        if (checkedRules.IsFailure)
        {
            return checkedRules.Error;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(device, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary><see cref="RotateDeviceTokenCommand"/> ni bajaradi.</summary>
/// <remarks>
/// Eski token SHU so'rov saqlangan lahzada o'ladi: qabul yo'li tokenni har
/// so'rovda xesh bo'yicha qidiradi va keshlamaydi. Terminal yangi tokenni
/// olmaguncha yuborgan hamma narsasi 401 — ekran buni oldindan aytadi.
/// </remarks>
public sealed class RotateDeviceTokenCommandHandler
{
    private readonly IAttendanceDeviceRepository _devices;
    private readonly AttendanceDeviceMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RotateDeviceTokenCommandHandler(
        IAttendanceDeviceRepository devices,
        AttendanceDeviceMapper mapper,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _devices = devices;
        _mapper = mapper;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AttendanceDeviceCreatedDto>> HandleAsync(
        RotateDeviceTokenCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Tokenni aylantirish ilovaga ISHLAYDIGAN qabul tokeni berib
        // qo'yardi — SPEC §D5 aynan shuni taqiqlaydi.
        if (command.DeviceId == AttendanceDevice.MobileAppId)
        {
            return AttendanceErrors.MobileAppDeviceProtected;
        }

        var device = await _devices.GetByIdAsync(command.DeviceId, cancellationToken).ConfigureAwait(false);
        if (device is null)
        {
            return AttendanceErrors.DeviceNotFound;
        }

        var token = AttendanceDeviceToken.Generate();

        device.RotateToken(
            AttendanceDeviceToken.Hash(token),
            AttendanceDeviceToken.PrefixOf(token),
            _clock.UtcNow,
            _currentUser.UserId);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = await _mapper.ToDtoAsync(device, cancellationToken).ConfigureAwait(false);

        return new AttendanceDeviceCreatedDto(dto, token);
    }
}

/// <summary><see cref="SetDeviceActiveCommand"/> ni bajaradi.</summary>
/// <remarks>
/// Faolsiz qurilmaning tokeni haqiqiy qoladi, lekin qabul 403 qaytaradi va
/// hech narsa yozmaydi. Qayta yoqish tokenni almashtirmaydi — terminalni
/// qayta sozlash shart emas.
/// </remarks>
public sealed class SetDeviceActiveCommandHandler
{
    private readonly IAttendanceDeviceRepository _devices;
    private readonly AttendanceDeviceMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public SetDeviceActiveCommandHandler(
        IAttendanceDeviceRepository devices,
        AttendanceDeviceMapper mapper,
        IUnitOfWork unitOfWork)
    {
        _devices = devices;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AttendanceDeviceDto>> HandleAsync(
        SetDeviceActiveCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Faollashtirish ikki teshikni birdan ochardi: qabul endpointi
        // (tasdiqlovchi faolsiz qurilmani birinchi bo'lib rad etadi) va
        // xodim kartasi — u har bir xodimga FAOL qurilmalar ro'yxatini
        // chizadi, ya'ni 950 ta kartada hech qachon bajarilmaydigan
        // «Mobil ilova — Rasm yo'q» qatori paydo bo'lardi.
        if (command.DeviceId == AttendanceDevice.MobileAppId)
        {
            return AttendanceErrors.MobileAppDeviceProtected;
        }

        var device = await _devices.GetByIdAsync(command.DeviceId, cancellationToken).ConfigureAwait(false);
        if (device is null)
        {
            return AttendanceErrors.DeviceNotFound;
        }

        if (command.Active)
        {
            device.Activate();
        }
        else
        {
            device.Deactivate();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await _mapper.ToDtoAsync(device, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary><see cref="DeleteDeviceCommand"/> ni bajaradi.</summary>
/// <remarks>
/// O'chirilgan qurilmaning tokeni ham O'LIK bo'ladi: token xeshining unikal
/// indeksida yumshoq o'chirish filtri yo'q va qidiruv global filtr bilan
/// o'chirilganni topmaydi (<c>IAttendanceDeviceRepository.FindByTokenHashAsync</c>).
/// </remarks>
public sealed class DeleteDeviceCommandHandler
{
    private readonly IAttendanceDeviceRepository _devices;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteDeviceCommandHandler(IAttendanceDeviceRepository devices, IUnitOfWork unitOfWork)
    {
        _devices = devices;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        DeleteDeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Pastdagi «o'tishlari bor» tekshiruvi buning o'rnini BOSMAYDI:
        // birinchi kuni sintetik qatorda hali bitta ham o'tish yo'q va
        // uni o'chirish telefondan belgilashni jimgina o'ldirib qo'yardi.
        if (command.DeviceId == AttendanceDevice.MobileAppId)
        {
            return AttendanceErrors.MobileAppDeviceProtected;
        }

        var device = await _devices.GetByIdAsync(command.DeviceId, cancellationToken).ConfigureAwait(false);
        if (device is null)
        {
            return AttendanceErrors.DeviceNotFound;
        }

        if (await _devices.HasPassesAsync(device.Id, cancellationToken).ConfigureAwait(false))
        {
            return AttendanceErrors.DeviceHasPasses;
        }

        _devices.Remove(device);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
