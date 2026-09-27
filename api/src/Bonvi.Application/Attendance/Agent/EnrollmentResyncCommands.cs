using Bonvi.Application.Attendance.Common;
using Bonvi.Application.Attendance.Dtos;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Common;

namespace Bonvi.Application.Attendance.Agent;

/// <summary>Xodimni barcha qurilmalarga qayta yozish — kartadagi tugma.</summary>
/// <param name="EmployeeId">Xodim.</param>
public sealed record ResyncEmployeeEnrollmentsCommand(Guid EmployeeId);

/// <summary>
/// Xodimning har bir faol qurilmadagi belgisini «eskirgan» qiladi.
/// </summary>
/// <remarks>
/// <para>
/// Qurilmaga TO'G'RIDAN-TO'G'RI murojaat yo'q: server terminalga yeta
/// olmaydi (SPEC-agent §C). Tugma faqat belgini o'zgartiradi, yozishni esa
/// agent keyingi tekshiruvida (60 soniyagacha) bajaradi — VA O'SHA
/// RAQAM OSTIDA, o'chirib qayta yozmasdan (§E).
/// </para>
/// <para>
/// Qatori yo'q xodim uchun qator YARATILADI: aks holda hech qachon
/// yozilmagan odamda tugma ishlamasdi, holbuki aynan o'shanda u eng
/// kerak.
/// </para>
/// </remarks>
public sealed class ResyncEmployeeEnrollmentsCommandHandler
{
    private readonly IEmployeeRepository _employees;
    private readonly IAttendanceDeviceRepository _devices;
    private readonly IDeviceEnrollmentRepository _enrollments;
    private readonly IUnitOfWork _unitOfWork;

    public ResyncEmployeeEnrollmentsCommandHandler(
        IEmployeeRepository employees,
        IAttendanceDeviceRepository devices,
        IDeviceEnrollmentRepository enrollments,
        IUnitOfWork unitOfWork)
    {
        _employees = employees;
        _devices = devices;
        _enrollments = enrollments;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EnrollmentResyncDto>> HandleAsync(
        ResyncEmployeeEnrollmentsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await _employees.ExistsAsync(command.EmployeeId, cancellationToken).ConfigureAwait(false))
        {
            return AttendanceErrors.EmployeeNotFound;
        }

        var devices = await _devices.ListAsync(includeInactive: false, cancellationToken).ConfigureAwait(false);

        var existing = await _enrollments
            .ListByEmployeeAsync(command.EmployeeId, cancellationToken)
            .ConfigureAwait(false);

        var byDevice = existing.ToDictionary(row => row.DeviceId);
        var affected = 0;

        foreach (var device in devices)
        {
            if (!byDevice.TryGetValue(device.Id, out var row))
            {
                row = DeviceEnrollment.Start(device.Id, command.EmployeeId);
                _enrollments.Add(row);
            }

            row.Force();
            affected++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new EnrollmentResyncDto(affected);
    }
}

/// <summary>Qurilmani butun ro'yxat bilan solishtirish — «Hammasini sinxronlash».</summary>
/// <param name="DeviceId">Qurilma.</param>
public sealed record ResyncDeviceEnrollmentsCommand(Guid DeviceId);

/// <summary>
/// Qurilmadagi XATO qatorlarni qayta urinishga qo'yadi.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Bu «hammasini qaytadan yozish» EMAS</b> (egasi, 2026-09-21:
/// «bunda qurilmaga qayta ustiga yozib qo'ymiydimi?»). Agent baribir har
/// tekshiruvda kerakli ro'yxatni qurilmadagi haqiqiy ro'yxat bilan
/// solishtiradi va farqini tuzatadi — ya'ni yetishmagan odam tugmasiz ham
/// yoziladi. Tugmaning yagona ishi — <b>rad etilgan</b> qatorlarni qayta
/// urinishga qo'yish.
/// </para>
/// <para>
/// Nega bu kerak (2026-09-27 dan): qurilma AYNAN bir belgini qat'iy rad
/// etsa (yuz sifati past, rasm juda katta), server uni eslab qoladi
/// (<see cref="DeviceEnrollment.RejectedVersion"/>) va ro'yxat shu odamni
/// «ushlab tur» deb beradi — aks holda agent har 60 soniyada, kuniga ~1440
/// marta, o'sha xatoni olib turardi. Rasm yoki ism o'zgarsa, qator o'zi
/// qayta urinishga tushadi; tugma esa belgini (<see cref="DeviceEnrollment.ForceVersion"/>)
/// o'zgartirib, rasmi o'zgarmagan qatorni ham QAYTA urinishga qo'yadi —
/// masalan terminal sozlamasi tuzatilgandan keyin. Vaqtinchalik xato
/// (tarmoq) bilan tugagan qator ushlab turilmaydi va tugmasiz ham qayta
/// urinadi.
/// </para>
/// <para>
/// Joyida turgan xodimga TEGILMAYDI: ikkinchi marta bosilgan tugma hech
/// narsa qilmaydi va hech kimning eshigi bir lahzaga ham yopilmaydi.
/// </para>
/// </remarks>
public sealed class ResyncDeviceEnrollmentsCommandHandler
{
    private readonly IAttendanceDeviceRepository _devices;
    private readonly IDeviceEnrollmentRepository _enrollments;
    private readonly IUnitOfWork _unitOfWork;

    public ResyncDeviceEnrollmentsCommandHandler(
        IAttendanceDeviceRepository devices,
        IDeviceEnrollmentRepository enrollments,
        IUnitOfWork unitOfWork)
    {
        _devices = devices;
        _enrollments = enrollments;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EnrollmentResyncDto>> HandleAsync(
        ResyncDeviceEnrollmentsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await _devices.ExistsAsync(command.DeviceId, cancellationToken).ConfigureAwait(false))
        {
            return AttendanceErrors.DeviceNotFound;
        }

        var rows = await _enrollments.ListByDeviceAsync(command.DeviceId, cancellationToken).ConfigureAwait(false);

        var affected = 0;

        foreach (var row in rows.Where(row => row.Status == DeviceEnrollmentStatus.Failed))
        {
            row.Force();
            affected++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new EnrollmentResyncDto(affected);
    }
}
