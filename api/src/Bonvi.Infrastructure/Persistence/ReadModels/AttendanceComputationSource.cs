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
    /// <para>
    /// Global filtr qoladi: o'chirilgan munosabat (kiritish xatosi)
    /// <c>PrimaryEmployment</c> tomonidan baribir e'tiborga olinmaydi.
    /// O'qish modelining <c>EmploymentsAsync</c> i ham aynan shunday.
    /// </para>
    /// <para>
    /// ⚠️ Mehnat yo'li hodisalari BILAN (<c>Include(Events)</c>): sanadagi bo'lim va
    /// lavozim ko'chirish hodisalaridan o'qiladi (<c>EmploymentPlacement</c>). Ularsiz
    /// hisoblash jimgina joriy qiymatga qaytib, ko'chirishdan oldingi kunni yangi
    /// lavozimning jadvali bilan qurardi. <c>AsSplitQuery</c> — munosabat qatori har
    /// hodisa uchun takrorlanmasin.
    /// </para>
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
            .Include(employment => employment.Events)
            .AsSplitQuery()
            .Where(employment => ids.Contains(employment.EmployeeId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return employments
            .GroupBy(employment => employment.EmployeeId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<Employment>)[.. group]);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Ikki so'rov: davrda munosabati bo'lgan xodimlar (ichki so'rov) va ularning
    /// hamma munosabatlari hodisalari bilan — <see cref="LoadEmploymentsAsync"/> bilan
    /// bir xil shakl va bir xil global filtr.
    /// </remarks>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Employment>>> LoadEmploymentsCoveringAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var employed = _context.Employments
            .Where(employment =>
                employment.HireDate <= to &&
                (employment.TerminationDate == null || employment.TerminationDate >= from))
            .Select(employment => employment.EmployeeId);

        var employments = await _context.Employments
            .AsNoTracking()
            .Include(employment => employment.Events)
            .AsSplitQuery()
            .Where(employment => employed.Contains(employment.EmployeeId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return employments
            .GroupBy(employment => employment.EmployeeId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<Employment>)[.. group]);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Ikki so'rov: hodisalar (yangi yozilgani — <c>recorded_at</c>, tahrirlangani —
    /// <c>updated_at</c>) va munosabat qatorlarining o'zi (yaratilgan, o'zgargan yoki
    /// o'chirilgan). Munosabatlar <c>IgnoreQueryFilters</c> bilan: o'chirilgan munosabat
    /// ham kunlarni o'zgartiradi.
    /// </para>
    /// <para>
    /// Ikkala jadval ham kichik (munosabatlar ~ming, hodisalar o'n minglar) va vaqt
    /// ustunlarida indeks yo'q — so'rov har 15 daqiqada bir marta ketma-ket o'qiydi,
    /// bu millisekundlar. Indeks uchun migratsiya shu sababli qo'shilmadi.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<EmploymentChange>> ListEmploymentChangesAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        // `timestamptz` parametri faqat UTC lahzani qabul qiladi.
        var from = since.ToUniversalTime();

        var events = await _context.EmploymentEvents
            .AsNoTracking()
            .Where(item => item.RecordedAt >= from || (item.UpdatedAt != null && item.UpdatedAt >= from))
            .Join(
                _context.Employments.IgnoreQueryFilters(),
                item => item.EmploymentId,
                employment => employment.Id,
                (item, employment) => new
                {
                    employment.EmployeeId,
                    employment.HireDate,
                    item.EffectiveDate,
                    Edited = item.UpdatedAt != null && item.UpdatedAt >= from,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await _context.Employments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(employment =>
                employment.CreatedAt >= from ||
                (employment.UpdatedAt != null && employment.UpdatedAt >= from) ||
                (employment.DeletedAt != null && employment.DeletedAt >= from))
            .Select(employment => new { employment.EmployeeId, employment.HireDate })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Tahrirlangan hodisaning ESKI sanasi saqlanmaydi — u qatorning o'zgarishi kabi
        // «ishga kirgan kundan» deb olinadi (sana `EmploymentChange` izohida).
        return
        [
            .. events.Select(item => new EmploymentChange(
                item.EmployeeId,
                item.HireDate,
                item.Edited ? null : item.EffectiveDate)),
            .. rows.Select(row => new EmploymentChange(row.EmployeeId, row.HireDate, null)),
        ];
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
