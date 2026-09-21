using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Agent;

/// <summary>
/// Ekranda ko'rinadigan holatni hisoblaydi (SPEC-agent §F).
/// </summary>
/// <remarks>
/// Qoida BITTA joyda: qurilma sahifasi va xodim kartasi bir xil qatorni
/// bir xil nomlashi shart, aks holda HR ikki ekranda ikki javob ko'rardi.
/// </remarks>
public static class EnrollmentState
{
    /// <summary>Qatorning hozirgi holati.</summary>
    /// <remarks>
    /// Tartib muhim: rasm yo'qligi hamma narsadan ustun (yozib bo'lmaydi),
    /// keyin ro'yxatdan chiqqanlar, keyin xato, va oxirida belgilar
    /// solishtiriladi.
    /// </remarks>
    public static DeviceEnrollmentState Of(EnrollmentRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!row.OnRoster)
        {
            // Ro'yxatdan chiqqan: agent olib tashlagan bo'lsa «o'chirildi»,
            // aks holda hali navbatda.
            return row.Status == DeviceEnrollmentStatus.Removed
                ? DeviceEnrollmentState.Removed
                : DeviceEnrollmentState.Pending;
        }

        if (row.PhotoKey is null)
        {
            return DeviceEnrollmentState.PhotoMissing;
        }

        if (row.Status == DeviceEnrollmentStatus.Failed)
        {
            return DeviceEnrollmentState.Failed;
        }

        if (row.Status != DeviceEnrollmentStatus.Synced)
        {
            return DeviceEnrollmentState.Pending;
        }

        // Belgi ichida «Qayta yozish» sanog'i ham bor, shu sababli
        // tugma bosilgan zahoti qator yana «kutilmoqda» bo'ladi.
        return string.Equals(row.AppliedVersion, EnrollmentVersion.Of(row.PhotoKey, row.ForceVersion), StringComparison.Ordinal)
            ? DeviceEnrollmentState.Synced
            : DeviceEnrollmentState.Pending;
    }

    /// <summary>Qatorni ekran DTO siga o'giradi.</summary>
    public static DeviceEnrollmentRowDto ToDto(EnrollmentRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new DeviceEnrollmentRowDto(
            row.DeviceId,
            row.DeviceName,
            row.EmployeeId,
            row.EmployeeName,
            row.PersonNo,
            Of(row),
            row.Error,
            row.SyncedAt);
    }
}

/// <summary>Bitta qurilmaning yozish holati — qurilmalar sahifasi uchun.</summary>
/// <param name="DeviceId">Qurilma.</param>
public sealed record GetDeviceEnrollmentsQuery(Guid DeviceId);

/// <summary>
/// Qurilma bo'yicha umumiy sanoq va e'tibor talab qiladigan qatorlar.
/// </summary>
/// <remarks>
/// ⚠️ Qatorlarning HAMMASI qaytmaydi — faqat «Yozildi» BO'LMAGANLARI.
/// 950 kishilik ro'yxatning 940 tasi joyida bo'lganda HR ga o'sha 940
/// emas, qolgan 10 tasi kerak. Umumiy manzara sanoqlarda ko'rinadi.
/// </remarks>
public sealed class GetDeviceEnrollmentsQueryHandler
{
    private readonly IAttendanceDeviceRepository _devices;
    private readonly IEnrollmentReadModel _enrollments;
    private readonly IClock _clock;

    public GetDeviceEnrollmentsQueryHandler(
        IAttendanceDeviceRepository devices,
        IEnrollmentReadModel enrollments,
        IClock clock)
    {
        _devices = devices;
        _enrollments = enrollments;
        _clock = clock;
    }

    public async Task<Result<DeviceEnrollmentPageDto>> HandleAsync(
        GetDeviceEnrollmentsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var device = await _devices.GetByIdAsync(query.DeviceId, cancellationToken).ConfigureAwait(false);
        if (device is null)
        {
            return AttendanceErrors.DeviceNotFound;
        }

        var today = _clock.UtcNow.TashkentDate();
        var rows = await _enrollments.DeviceRowsAsync(device.Id, today, cancellationToken).ConfigureAwait(false);

        var states = rows.Select(row => (Row: row, State: EnrollmentState.Of(row))).ToList();

        var summary = new DeviceEnrollmentSummaryDto(
            device.Id,
            device.Name,
            Total: states.Count(entry => entry.Row.OnRoster),
            Synced: states.Count(entry => entry.State == DeviceEnrollmentState.Synced),
            Pending: states.Count(entry => entry.State == DeviceEnrollmentState.Pending),
            Failed: states.Count(entry => entry.State == DeviceEnrollmentState.Failed),
            PhotoMissing: states.Count(entry => entry.State == DeviceEnrollmentState.PhotoMissing),
            device.LastSeenAt);

        var problems = states
            .Where(entry => entry.State != DeviceEnrollmentState.Synced && entry.State != DeviceEnrollmentState.Removed)
            .OrderBy(entry => entry.State)
            .ThenBy(entry => entry.Row.EmployeeName, StringComparer.CurrentCulture)
            .Select(entry => EnrollmentState.ToDto(entry.Row))
            .ToList();

        return new DeviceEnrollmentPageDto(summary, problems);
    }
}

/// <summary>Xodimning har bir qurilmadagi holati — karta uchun.</summary>
/// <param name="EmployeeId">Xodim.</param>
public sealed record GetEmployeeEnrollmentsQuery(Guid EmployeeId);

/// <summary>Xodim kartasining «Qurilmalar» qatori.</summary>
public sealed class GetEmployeeEnrollmentsQueryHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly IEnrollmentReadModel _enrollments;
    private readonly IClock _clock;

    public GetEmployeeEnrollmentsQueryHandler(
        IEmployeeRepository employees,
        IEnrollmentReadModel enrollments,
        IClock clock)
    {
        _employees = employees;
        _enrollments = enrollments;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<DeviceEnrollmentRowDto>>> HandleAsync(
        GetEmployeeEnrollmentsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!await _employees.ExistsAsync(query.EmployeeId, cancellationToken).ConfigureAwait(false))
        {
            return AttendanceErrors.EmployeeNotFound;
        }

        var today = _clock.UtcNow.TashkentDate();

        var rows = await _enrollments
            .EmployeeRowsAsync(query.EmployeeId, today, cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(EnrollmentState.ToDto).ToList();
    }
}
