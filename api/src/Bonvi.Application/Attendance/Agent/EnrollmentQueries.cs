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
    /// <para>
    /// Tartib muhim: avval ro'yxatdan chiqqanlar, keyin rasm yo'qligi (yozib
    /// bo'lmaydi), keyin xato, va oxirida belgilar solishtiriladi.
    /// </para>
    /// <para>
    /// ⚠️ Ro'yxatdan chiqqan odamni o'chirib BO'LMAGAN bo'lsa — «Xato», «Navbatda»
    /// emas: ishdan ketgan odam hali terminalda va eshikni ocha oladi. Ilgari
    /// agent muvaffaqiyatsiz o'chirishni ham «bajarildi» deb yuborardi, server
    /// esa uni «O'chirilgan» deb ko'rsatardi.
    /// </para>
    /// </remarks>
    public static DeviceEnrollmentState Of(EnrollmentRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!row.OnRoster)
        {
            return row.Status switch
            {
                DeviceEnrollmentStatus.Removed => DeviceEnrollmentState.Removed,
                DeviceEnrollmentStatus.RemoveFailed => DeviceEnrollmentState.Failed,

                // Terminalga hech qachon yozilmagan (belgisi yo'q) — olib
                // tashlaydigan narsa yo'q, «navbatda» abadiy turib qolardi.
                _ when row.AppliedVersion is null => DeviceEnrollmentState.Removed,

                // Yozilgan — agent keyingi tekshiruvda olib tashlaydi.
                _ => DeviceEnrollmentState.Pending,
            };
        }

        if (row.PhotoKey is null)
        {
            return DeviceEnrollmentState.PhotoMissing;
        }

        // Belgi ichida «Qayta yozish» sanog'i va ism ham bor, shu sababli
        // tugma bosilgan yoki ism o'zgargan zahoti qator yana «kutilmoqda» bo'ladi.
        var version = EnrollmentVersion.Of(row.PhotoKey, row.ForceVersion, row.EmployeeName);

        if (row.Status == DeviceEnrollmentStatus.Failed)
        {
            // Rad etilgan belgi endi eskirgan (yangi rasm, ism, «Qayta
            // yozish») — agent qayta urinmoqda.
            return row.RejectedVersion is { } rejected && !string.Equals(rejected, version, StringComparison.Ordinal)
                ? DeviceEnrollmentState.Pending
                : DeviceEnrollmentState.Failed;
        }

        // O'chirib bo'lmagan-u, qayta ro'yxatga tushgan odam terminalda o'sha
        // belgi bilan turibdi — «yozilgan» kabi solishtiriladi.
        if (row.Status is not (DeviceEnrollmentStatus.Synced or DeviceEnrollmentStatus.RemoveFailed))
        {
            return DeviceEnrollmentState.Pending;
        }

        return string.Equals(row.AppliedVersion, version, StringComparison.Ordinal)
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
            row.SyncedAt,
            row.PhotoKey);
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
            device.AgentSeenAt ?? device.LastSeenAt,
            device.ReconciledAt,
            device.HeldRemovals,
            device.ForeignPersons,
            device.RemovalsApprovedUntil);

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
