using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Agent;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary><see cref="IEnrollmentReadModel"/> ning EF Core orqali amalga oshirilishi.</summary>
/// <remarks>
/// <para>
/// Hamma so'rov <c>AsNoTracking</c>: bu yerdan o'qilgan hech narsa
/// o'zgarmaydi. Qatorlar bazada BIRLASHTIRILMAYDI — bir nechta tor so'rov
/// olinib, xotirada qo'shiladi. Sabab: kerakli ro'yxat (munosabatlar),
/// haqiqiy holat (<c>device_enrollments</c>) va raqamlar (mosliklar) uch
/// xil o'lchamdagi to'plam, va ularning to'la <c>LEFT JOIN</c> i
/// rejalashtiruvchini har doim eng kattasidan boshlashga majburlardi.
/// </para>
/// </remarks>
internal sealed class EnrollmentReadModel : IEnrollmentReadModel
{
    private readonly AppDbContext _context;

    public EnrollmentReadModel(AppDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<IReadOnlyList<RosterCandidate>> RosterAsync(
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var ids = await EmployedOn(today).ToListAsync(cancellationToken).ConfigureAwait(false);

        return await PeopleAsync(ids, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> PersonNumbersAsync(
        AttendanceIdentityProvider provider,
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employeeIds);

        if (employeeIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<string>>();
        }

        var ids = employeeIds.ToList();

        // Yaratilish tartibida — `DevicePersonNo.Prefer` birinchisini oladi va u
        // har so'rovda bir xil bo'lishi shart (interfeys izohi). Tartibsiz
        // `SELECT` da PostgreSQL qatorlarni istalgan tartibda qaytarishi mumkin.
        var rows = await _context.AttendanceIdentities
            .AsNoTracking()
            .Where(identity => identity.Provider == provider && ids.Contains(identity.EmployeeId))
            .OrderBy(identity => identity.CreatedAt)
            .ThenBy(identity => identity.ExternalPersonId)
            .Select(identity => new { identity.EmployeeId, identity.ExternalPersonId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(row => row.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)[.. group.Select(row => row.ExternalPersonId)]);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Faza sharti <c>DevicePersonNo.IsManaged</c> bilan AYNAN bir xil: 9 belgi,
    /// birinchisi <c>9</c>. Bazada tekshiriladi — ishdan ketganlarning
    /// raqamlari yillar davomida yig'iladi va hammasini xotiraga olib
    /// saralashning ma'nosi yo'q.
    /// </remarks>
    public async Task<IReadOnlyList<string>> ManagedNumbersAsync(
        AttendanceIdentityProvider provider,
        CancellationToken cancellationToken = default)
        => await _context.AttendanceIdentities
            .AsNoTracking()
            .Where(identity =>
                identity.Provider == provider &&
                identity.ExternalPersonId.Length == DevicePersonNo.Length &&
                EF.Functions.Like(identity.ExternalPersonId, DevicePersonNo.Prefix + "%"))
            .Select(identity => identity.ExternalPersonId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<EnrollmentRow>> DeviceRowsAsync(
        Guid deviceId,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var device = await _context.AttendanceDevices
            .AsNoTracking()
            .Where(row => row.Id == deviceId)
            .Select(row => new { row.Id, row.Name, row.IdentityProvider })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (device is null)
        {
            return [];
        }

        var rosterIds = await EmployedOn(today).ToListAsync(cancellationToken).ConfigureAwait(false);

        var enrollments = await _context.DeviceEnrollments
            .AsNoTracking()
            .Where(row => row.DeviceId == deviceId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        /*
          Ro'yxatdan chiqqan, lekin qurilmada qolgan odam ham kerak: uni
          agent olib tashlaguncha HR ko'rib turishi shart (SPEC-agent §A3).
        */
        var ids = rosterIds.Concat(enrollments.Select(row => row.EmployeeId)).Distinct().ToList();

        var people = await PeopleAsync(ids, cancellationToken).ConfigureAwait(false);
        var numbers = await PersonNumbersAsync(device.IdentityProvider, ids, cancellationToken).ConfigureAwait(false);

        var roster = rosterIds.ToHashSet();
        var byEmployee = enrollments.ToDictionary(row => row.EmployeeId);

        return people
            .Select(person => Compose(
                device.Id,
                device.Name,
                person,
                roster.Contains(person.EmployeeId),
                numbers,
                byEmployee.GetValueOrDefault(person.EmployeeId)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EnrollmentRow>> EmployeeRowsAsync(
        Guid employeeId,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var people = await PeopleAsync([employeeId], cancellationToken).ConfigureAwait(false);
        if (people.Count == 0)
        {
            return [];
        }

        var person = people[0];

        var devices = await _context.AttendanceDevices
            .AsNoTracking()
            .Where(row => row.IsActive)
            .OrderBy(row => row.Name)
            .Select(row => new { row.Id, row.Name, row.IdentityProvider })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (devices.Count == 0)
        {
            return [];
        }

        var onRoster = await EmployedOn(today)
            .AnyAsync(id => id == employeeId, cancellationToken)
            .ConfigureAwait(false);

        var enrollments = await _context.DeviceEnrollments
            .AsNoTracking()
            .Where(row => row.EmployeeId == employeeId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byDevice = enrollments.ToDictionary(row => row.DeviceId);

        // Raqamlar fazasi bo'yicha bitta so'rov: qurilmalar odatda bitta
        // fazada bo'ladi va har biri uchun alohida so'rov ortiqcha bo'lardi.
        var numbersByProvider = new Dictionary<AttendanceIdentityProvider, IReadOnlyDictionary<Guid, IReadOnlyList<string>>>();

        foreach (var provider in devices.Select(device => device.IdentityProvider).Distinct())
        {
            numbersByProvider[provider] = await PersonNumbersAsync(provider, [employeeId], cancellationToken)
                .ConfigureAwait(false);
        }

        var rows = new List<EnrollmentRow>(devices.Count);

        foreach (var device in devices)
        {
            var numbers = numbersByProvider[device.IdentityProvider];

            rows.Add(Compose(
                device.Id,
                device.Name,
                person,
                onRoster,
                numbers,
                byDevice.GetValueOrDefault(device.Id)));
        }

        return rows;
    }

    /// <summary>Bugunni qamrab oluvchi munosabati bor xodimlar — TAKRORSIZ.</summary>
    /// <remarks>
    /// Global filtr qoladi: o'chirilgan munosabat (kiritish xatosi) odamni
    /// qurilmaga olib chiqmasligi kerak. Shart
    /// <c>AttendanceComputationSource.ListEmployeesAsync</c> dagi bilan bir xil.
    /// </remarks>
    private IQueryable<Guid> EmployedOn(DateOnly today)
        => _context.Employments
            .AsNoTracking()
            .Where(employment =>
                employment.HireDate <= today &&
                (employment.TerminationDate == null || employment.TerminationDate >= today))
            .Select(employment => employment.EmployeeId)
            .Distinct();

    /// <summary>Xodimlarning ismi va rasm kaliti.</summary>
    private async Task<IReadOnlyList<RosterCandidate>> PeopleAsync(
        List<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var people = await _context.Employees
            .AsNoTracking()
            .Where(employee => ids.Contains(employee.Id))
            .Select(employee => new
            {
                employee.Id,
                employee.FullName.LastName,
                employee.FullName.FirstName,
                employee.FullName.MiddleName,
                employee.PhotoKey,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return people
            .Select(person => new RosterCandidate(
                person.Id,
                FullName.FromTrusted(person.LastName, person.FirstName, person.MiddleName).Full,
                person.PhotoKey))
            .OrderBy(person => person.FullName, StringComparer.InvariantCulture)
            .ToList();
    }

    /// <summary>Uch manbadan bitta qator yig'adi.</summary>
    /// <summary>Qurilmadagi raqam — HR ekranida ko'rinadigani.</summary>
    /// <remarks>
    /// Terminalning o'z yozuvi bo'lsa (SPEC-agent §M) — o'sha raqam: terminalda
    /// Bonvi raqami yo'q va HR aynan iVMS'dagi raqamni qidiradi.
    /// </remarks>
    private static string? PersonNoOf(IReadOnlyList<string>? numbers, DeviceEnrollmentStatus? status)
        => status == DeviceEnrollmentStatus.Covered
            ? DevicePersonNo.Aliases(numbers)?[0] ?? DevicePersonNo.Prefer(numbers)
            : DevicePersonNo.Prefer(numbers);

    private static EnrollmentRow Compose(
        Guid deviceId,
        string deviceName,
        RosterCandidate person,
        bool onRoster,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> numbers,
        DeviceEnrollment? enrollment)
        => new(
            deviceId,
            deviceName,
            person.EmployeeId,
            person.FullName,
            person.PhotoKey,
            onRoster,
            PersonNoOf(numbers.GetValueOrDefault(person.EmployeeId), enrollment?.Status),
            enrollment?.Status,
            enrollment?.AppliedVersion,
            enrollment?.ForceVersion ?? 0,
            enrollment?.Error,
            enrollment?.SyncedAt,
            enrollment?.RejectedVersion);
}
