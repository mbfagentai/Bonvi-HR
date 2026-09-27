using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Application.Employees.Common;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Attendance.Devices;

/// <summary><see cref="AttendanceDevice"/> ni DTO ga o'giradi — firma/bo'lim nomi va 24 soatlik sanoq bilan.</summary>
/// <remarks>
/// <para>
/// Ro'yxat va bitta qurilma BITTA yo'ldan o'tadi: yaratish, tahrir, token
/// almashtirish va yoqish/o'chirish javobi ro'yxatdagi qator bilan aynan
/// bir xil bo'ladi.
/// </para>
/// <para>
/// Nomlar <see cref="CatalogNameResolver"/> dan — u so'rov davomida eslab
/// qoladi, qurilmalar esa o'nlab va bir nechta bo'limda turadi. Sanoq —
/// barcha qurilmalar uchun bitta guruhlangan so'rov.
/// </para>
/// </remarks>
public sealed class AttendanceDeviceMapper
{
    /// <summary>«So'nggi sutka» oynasi — qurilmalar sahifasidagi sanoq uchun (SPEC §6.2).</summary>
    public static readonly TimeSpan RecentWindow = TimeSpan.FromHours(24);

    private readonly IAttendanceReadModel _readModel;
    private readonly CatalogNameResolver _names;
    private readonly IClock _clock;

    public AttendanceDeviceMapper(IAttendanceReadModel readModel, CatalogNameResolver names, IClock clock)
    {
        _readModel = readModel;
        _names = names;
        _clock = clock;
    }

    /// <summary>Bitta qurilma.</summary>
    /// <param name="device">Qurilma.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<AttendanceDeviceDto> ToDtoAsync(
        AttendanceDevice device,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        var items = await ToDtosAsync([device], cancellationToken).ConfigureAwait(false);
        return items[0];
    }

    /// <summary>Bir nechta qurilma — kiritilgan tartibda.</summary>
    /// <param name="devices">Qurilmalar.</param>
    /// <param name="cancellationToken">Bekor qilish belgisi.</param>
    public async Task<IReadOnlyList<AttendanceDeviceDto>> ToDtosAsync(
        IReadOnlyList<AttendanceDevice> devices,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(devices);

        if (devices.Count == 0)
        {
            return [];
        }

        var counts = await _readModel
            .CountRecentPassesAsync([.. devices.Select(device => device.Id)], _clock.UtcNow - RecentWindow, cancellationToken)
            .ConfigureAwait(false);

        var items = new List<AttendanceDeviceDto>(devices.Count);
        var now = _clock.UtcNow;

        foreach (var device in devices)
        {
            var (linkState, linkSince) = device.LinkAt(now);

            var legalEntityName = await _names.LegalEntityAsync(device.LegalEntityId, cancellationToken).ConfigureAwait(false);
            var departmentName = await _names.DepartmentAsync(device.DepartmentId, cancellationToken).ConfigureAwait(false);

            items.Add(new AttendanceDeviceDto(
                device.Id,
                device.Name,
                device.SerialNumber,
                device.Model,
                device.Provider,
                device.IdentityProvider,
                device.DirectionRole,
                device.LegalEntityId,
                legalEntityName,
                device.DepartmentId,
                departmentName,
                device.LocationNote,
                device.AuthMode,
                device.HmacSecret is not null,
                device.AllowedIps,
                device.TokenPrefix,
                device.TokenIssuedAt,
                device.IsActive,
                device.LastSeenAt,
                device.LastEventAt,
                device.LastSeenIp,
                device.LastSkewSeconds,
                device.ClockOffsetSeconds,
                device.ClockCheckedAt,
                counts.GetValueOrDefault(device.Id),
                device.HasClockDrift,
                device.AgentSeenAt,
                device.TerminalSeenAt,
                device.TerminalSerial,
                device.EventsRejected,
                linkState,
                linkSince));
        }

        return items;
    }
}
