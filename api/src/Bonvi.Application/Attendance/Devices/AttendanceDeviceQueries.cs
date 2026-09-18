using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Devices;

/// <summary>Qurilmalar reyestri (<c>GET /devices</c>).</summary>
/// <param name="IncludeInactive">Faolsizlantirilganlari ham kerakmi.</param>
public sealed record ListDevicesQuery(bool IncludeInactive);

/// <summary><see cref="ListDevicesQuery"/> ni bajaradi.</summary>
public sealed class ListDevicesQueryHandler
{
    private readonly IAttendanceDeviceRepository _devices;
    private readonly AttendanceDeviceMapper _mapper;

    public ListDevicesQueryHandler(IAttendanceDeviceRepository devices, AttendanceDeviceMapper mapper)
    {
        _devices = devices;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<AttendanceDeviceDto>>> HandleAsync(
        ListDevicesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var devices = await _devices.ListAsync(query.IncludeInactive, cancellationToken).ConfigureAwait(false);

        return Result.Success(await _mapper.ToDtosAsync(devices, cancellationToken).ConfigureAwait(false));
    }
}

/// <summary>Bitta qurilma (<c>GET /devices/{id}</c>).</summary>
/// <param name="DeviceId">Qurilma.</param>
public sealed record GetDeviceQuery(Guid DeviceId);

/// <summary><see cref="GetDeviceQuery"/> ni bajaradi.</summary>
public sealed class GetDeviceQueryHandler
{
    private readonly IAttendanceDeviceRepository _devices;
    private readonly AttendanceDeviceMapper _mapper;

    public GetDeviceQueryHandler(IAttendanceDeviceRepository devices, AttendanceDeviceMapper mapper)
    {
        _devices = devices;
        _mapper = mapper;
    }

    public async Task<Result<AttendanceDeviceDto>> HandleAsync(
        GetDeviceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var device = await _devices.GetByIdAsync(query.DeviceId, cancellationToken).ConfigureAwait(false);
        if (device is null)
        {
            return AttendanceErrors.DeviceNotFound;
        }

        return await _mapper.ToDtoAsync(device, cancellationToken).ConfigureAwait(false);
    }
}
