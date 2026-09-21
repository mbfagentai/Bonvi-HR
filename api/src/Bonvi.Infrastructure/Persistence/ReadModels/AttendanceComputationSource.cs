using Bonvi.Application.Attendance.Abstractions;
using Bonvi.Application.Attendance.Computation;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary><see cref="IAttendanceComputationSource"/> ning EF Core orqali amalga oshirilishi.</summary>
/// <remarks>
/// Hamma so'rov <c>AsNoTracking</c>: bu yerdan o'qilgan hech narsa
/// o'zgartirilmaydi, va kuzatuv qayta hisoblashning o'zi yozadigan
/// <c>AttendanceDay</c> qatorlari bilan bitta kontekstda xotirani behuda
/// to'ldirardi. So'rovlar soni xodimlar soniga bog'liq EMAS — har bir
/// metod bitta so'rov.
/// </remarks>
internal sealed class AttendanceComputationSource : IAttendanceComputationSource
{
    private readonly AppDbContext _context;

    public AttendanceComputationSource(AppDbContext context) => _context = context;

    /// <inheritdoc />
    /// <remarks>
    /// Biriktirmalar global filtr bilan (o'chirilgani — tarix, amaldagi qaror
    /// emas). Jadval va bo'limlar esa <c>IgnoreQueryFilters</c> bilan — sabab
    /// port izohida.
    /// </remarks>
    public async Task<ScheduleCatalog> LoadScheduleCatalogAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var assignments = await _context.ScheduleAssignments
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var schedules = await _context.WorkSchedules
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(schedule => schedule.Days)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var departments = await _context.Departments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        /*
          Yillik to'r kataklari — faqat SO'RALGAN oraliq uchun. Qo'shni
          kunlar ham kerak: tungi smenaning chiqishi ertangi sanada
          o'qiladi (`AttendanceDayBuilder.PassDatesNeeded` bilan bir xil
          mulohaza), shu sababli oraliq ikki tomonga bir kundan
          kengaytiriladi.
        */
        var dateFrom = from.AddDays(-1);
        var dateTo = to.AddDays(1);

        var dates = await _context.WorkScheduleDates
            .AsNoTracking()
            .Where(date => date.Date >= dateFrom && date.Date <= dateTo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ScheduleCatalog(assignments, schedules, departments, dates);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Global filtr qoladi: o'chirilgan munosabat (kiritish xatosi)
    /// <c>PrimaryEmployment</c> tomonidan baribir e'tiborga olinmaydi.
    /// O'qish modelining <c>EmploymentsAsync</c> i ham aynan shunday.
    /// </remarks>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Employment>>> LoadEmploymentsAsync(
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employeeIds);

        if (employeeIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Employment>>();
        }

        var ids = employeeIds.ToList();

        var employments = await _context.Employments
            .AsNoTracking()
            .Where(employment => ids.Contains(employment.EmployeeId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return employments
            .GroupBy(employment => employment.EmployeeId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<Employment>)[.. group]);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>ix_attendance_passes_employee_time</c> (xodim bo'yicha) va
    /// <c>ix_attendance_passes_local_date</c> — rejalashtiruvchi qaysi biri
    /// torroq bo'lsa, o'shani tanlaydi.
    /// </remarks>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<PassReading>>> LoadPassesAsync(
        IReadOnlyCollection<Guid> employeeIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employeeIds);

        if (employeeIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<PassReading>>();
        }

        var ids = employeeIds.ToList();

        var passes = await _context.AttendancePasses
            .AsNoTracking()
            .Where(pass =>
                pass.ResolvedEmployeeId != null &&
                ids.Contains(pass.ResolvedEmployeeId.Value) &&
                pass.LocalDate >= from &&
                pass.LocalDate <= to)
            .Select(pass => new PassReading(pass.Id, pass.ResolvedEmployeeId, pass.OccurredAt, pass.Direction))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return passes
            .GroupBy(pass => pass.EmployeeId!.Value)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PassReading>)[.. group]);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListEmployeesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var (passFrom, passTo) = AttendanceDayBuilder.PassDatesNeeded(from, to);

        var employed = _context.Employments
            .Where(employment =>
                employment.HireDate <= to &&
                (employment.TerminationDate == null || employment.TerminationDate >= from))
            .Select(employment => employment.EmployeeId);

        var passed = _context.AttendancePasses
            .Where(pass =>
                pass.ResolvedEmployeeId != null &&
                pass.LocalDate >= passFrom &&
                pass.LocalDate <= passTo)
            .Select(pass => pass.ResolvedEmployeeId!.Value);

        var stored = _context.AttendanceDays
            .Where(day => day.LocalDate >= from && day.LocalDate <= to)
            .Select(day => day.EmployeeId);

        // Bitta so'rov: UNION bazada bajariladi va takrorlarni o'zi olib tashlaydi.
        return await employed
            .Union(passed)
            .Union(stored)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>Shartning O'ZI <see cref="AbsenceQuery"/> da — tabel bilan bitta nusxa.</remarks>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlySet<DateOnly>>> LoadExcusedDatesAsync(
        IReadOnlyCollection<Guid> employeeIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
        => await AbsenceQuery
            .ExcusedDatesAsync(_context, employeeIds, from, to, cancellationToken)
            .ConfigureAwait(false);
}
