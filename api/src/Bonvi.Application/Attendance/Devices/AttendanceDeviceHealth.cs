using Bonvi.Application.Attendance.Dtos;
using Bonvi.Domain.Attendance;

namespace Bonvi.Application.Attendance.Devices;

/// <summary>
/// Qurilmalar sog'lig'i — Statistika sahifasi va taxta uchun BITTA hisob.
/// </summary>
/// <remarks>
/// <para>
/// Qoidalar domenda (<see cref="AttendanceDevice.LinkAt"/>,
/// <see cref="AttendanceDevice.ClockWarningAt"/>); bu yerda faqat yig'ish.
/// Qurilmalar o'ntadan oshmaydi, ya'ni hammasini o'qib xotirada sanash
/// SQL'da qoidani ikkinchi marta yozishdan arzon va xavfsizroq.
/// </para>
/// <para>
/// «Mobil ilova» qatori (<see cref="AttendanceDevice.IsSynthetic"/>) hech
/// qaysi sanoqqa KIRMAYDI — u terminal emas va hech qachon «ulanmaydi».
/// </para>
/// </remarks>
public static class AttendanceDeviceHealth
{
    /// <summary>Faol qurilmalardan sog'lik xulosasini tuzadi.</summary>
    /// <param name="devices">Qurilmalar — faolsizlari ham berilishi mumkin, ular tashlanadi.</param>
    /// <param name="now">Hozirgi lahza (UTC).</param>
    public static AttendanceDeviceHealthDto Of(IEnumerable<AttendanceDevice> devices, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(devices);

        var terminals = devices.Where(device => device.IsActive && !device.IsSynthetic).ToList();

        var outages = new List<DeviceOutageDto>();

        foreach (var device in terminals.OrderBy(device => device.Name, StringComparer.CurrentCulture))
        {
            var (state, since) = device.LinkAt(now);

            if (state is DeviceLinkState.AgentOffline or DeviceLinkState.TerminalUnreachable)
            {
                outages.Add(new DeviceOutageDto(device.Id, device.Name, state, since));
            }
        }

        return new AttendanceDeviceHealthDto(
            ActiveDevices: terminals.Count,
            ConnectedDevices: terminals.Count(device => device.LastSeenAt is not null),
            ClockWarningDevices: terminals.Count(device => device.ClockWarningAt(now)),
            Outages: outages);
    }
}
