using System.Diagnostics.CodeAnalysis;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Repositories;

/// <summary><see cref="IAttendanceDeviceRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class AttendanceDeviceRepository : Repository<AttendanceDevice>, IAttendanceDeviceRepository
{
    public AttendanceDeviceRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// ⚠️ <c>AsNoTracking</c> ATAYLAB YO'Q. Bu metod qabul qilish yo'lida
    /// chaqiriladi va topilgan qurilma darhol o'zgaradi
    /// (<c>NoteSeen</c> / <c>NoteEvent</c> kuzatuv ustunlarini yozadi) —
    /// kuzatilmagan obyektdagi o'zgarish jimgina yo'qolardi.
    /// </para>
    /// <para>
    /// Taqqoslash <c>bytea</c> ustunidagi tenglik: unikal indeks bo'yicha
    /// aniq zarba, ko'zdan kechirish yo'q.
    /// </para>
    /// </remarks>
    public Task<AttendanceDevice?> FindByTokenHashAsync(
        byte[] hash,
        CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(device => device.TokenHash == hash, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Tartib nom bo'yicha: reyestrda o'nlab qator bo'ladi va HR uni har
    /// ochganda bir xil ko'rishi kerak. Sahifalash kerak emas — aynan
    /// shu sababli ro'yxat o'qish modelidan emas, shu portdan keladi.
    /// </remarks>
    public async Task<IReadOnlyList<AttendanceDevice>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(device => device.IsActive);
        }

        return await query
            .OrderBy(device => device.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>lower(name)</c> — migratsiyadagi <c>ux_attendance_devices_name</c>
    /// indeksining AYNAN o'sha ifodasi, so'rov indeksga tushadi. Nom domen
    /// tomonidan faqat chetlaridan qirqiladi, shu sababli bu yerda ham shunday.
    /// </remarks>
    [SuppressMessage(
        "Globalization",
        "CA1304:Specify CultureInfo",
        Justification = "Ifoda daraxti SQL'ga tarjima qilinadi, .NET'da bajarilmaydi.")]
    [SuppressMessage(
        "Globalization",
        "CA1311:Specify a culture or use an invariant version",
        Justification = "Ifoda daraxti SQL'ga tarjima qilinadi, .NET'da bajarilmaydi.")]
    [SuppressMessage(
        "Performance",
        "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons",
        Justification = "EF Core StringComparison variantini SQL'ga tarjima qila olmaydi.")]
    public Task<bool> NameExistsAsync(
        string name,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalized = name.Trim().ToLowerInvariant();

        return Set
            .Where(device => device.Name.ToLower() == normalized && (exceptId == null || device.Id != exceptId))
            .AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>ux_attendance_devices_serial</c> bilan bir xil ifoda. Seriya raqamsiz
    /// qurilmalar bir-biriga takror sanalmaydi — indeks ham ularni chetlaydi.
    /// </remarks>
    [SuppressMessage(
        "Globalization",
        "CA1304:Specify CultureInfo",
        Justification = "Ifoda daraxti SQL'ga tarjima qilinadi, .NET'da bajarilmaydi.")]
    [SuppressMessage(
        "Globalization",
        "CA1311:Specify a culture or use an invariant version",
        Justification = "Ifoda daraxti SQL'ga tarjima qilinadi, .NET'da bajarilmaydi.")]
    [SuppressMessage(
        "Performance",
        "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons",
        Justification = "EF Core StringComparison variantini SQL'ga tarjima qila olmaydi.")]
    public Task<bool> SerialExistsAsync(
        string serialNumber,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serialNumber);

        var normalized = serialNumber.Trim().ToLowerInvariant();

        return Set
            .Where(device =>
                device.SerialNumber != null &&
                device.SerialNumber.ToLower() == normalized &&
                (exceptId == null || device.Id != exceptId))
            .AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks><c>ix_attendance_passes_device_time</c> ning chap ustuniga tushadi.</remarks>
    public Task<bool> HasPassesAsync(Guid deviceId, CancellationToken cancellationToken = default)
        => Context.AttendancePasses.AnyAsync(pass => pass.DeviceId == deviceId, cancellationToken);
}

/// <summary><see cref="IAttendanceIdentityRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class AttendanceIdentityRepository : Repository<AttendanceIdentity>, IAttendanceIdentityRepository
{
    public AttendanceIdentityRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    /// <remarks>
    /// <c>AsNoTracking</c>: chaqiruvchiga faqat <c>EmployeeId</c> kerak va
    /// yozuvning o'zi o'zgarmaydi. O'zgartirish kerak bo'lganda
    /// <c>GetByIdAsync</c> ishlatiladi.
    /// </remarks>
    public Task<AttendanceIdentity?> FindAsync(
        AttendanceIdentityProvider provider,
        string externalPersonId,
        CancellationToken cancellationToken = default)
        => Set
            .AsNoTracking()
            .FirstOrDefaultAsync(
                identity => identity.Provider == provider && identity.ExternalPersonId == externalPersonId,
                cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceIdentity>> ListByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(identity => identity.EmployeeId == employeeId)
            .OrderBy(identity => identity.Provider)
            .ThenBy(identity => identity.ExternalPersonId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    /// <remarks>
    /// <c>= ANY(@ids)</c> — bitta massiv parametri; 5 000 ta raqam ham bitta
    /// so'rov va <c>ux_attendance_identities_person</c> indeksiga tushadi.
    /// </remarks>
    public async Task<IReadOnlyList<AttendanceIdentity>> FindManyAsync(
        AttendanceIdentityProvider provider,
        IReadOnlyCollection<string> externalPersonIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(externalPersonIds);

        if (externalPersonIds.Count == 0)
        {
            return [];
        }

        var ids = externalPersonIds.Distinct(StringComparer.Ordinal).ToList();

        return await Set
            .AsNoTracking()
            .Where(identity => identity.Provider == provider && ids.Contains(identity.ExternalPersonId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary><see cref="IAttendancePassRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class AttendancePassRepository : Repository<AttendancePass>, IAttendancePassRepository
{
    public AttendancePassRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Ikki qadam: avval bog'lanmaganlarning identifikator va sanasi
    /// (<c>ix_attendance_passes_unmatched</c> — qisman indeks aynan shu so'rov
    /// uchun), keyin shu identifikatorlar bo'yicha <c>ExecuteUpdate</c>.
    /// <c>UPDATE … RETURNING</c> bitta so'rov bo'lardi, lekin EF uni
    /// qaytariladigan turga bog'lab o'qiy olmaydi va xom SQL ustun
    /// nomlari konvensiyasini qo'lda takrorlashni talab qilardi.
    /// </para>
    /// <para>
    /// Yangilashdagi <c>resolved_employee_id IS NULL</c> sharti ikki qadam
    /// orasidagi poygani yopadi: parallel vazifa bog'lab ulgurgan o'tish qayta
    /// yozilmaydi. Qaytarilgan ro'yxat unda ham to'g'ri — moslik unikal, ya'ni
    /// o'sha o'tish aynan shu xodimga bog'langan va uning kuni qayta
    /// hisoblanishi zararsiz.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<AttachedPass>> AttachUnmatchedAsync(
        AttendanceIdentityProvider provider,
        string rawPersonId,
        Guid employeeId,
        DateTimeOffset resolvedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPersonId);

        var personId = rawPersonId.Trim();

        var unmatched = await Set
            .AsNoTracking()
            .Where(pass =>
                pass.IdentityProvider == provider &&
                pass.RawPersonId == personId &&
                pass.ResolvedEmployeeId == null)
            .Select(pass => new AttachedPass(pass.Id, pass.LocalDate))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (unmatched.Count == 0)
        {
            return [];
        }

        var ids = unmatched.ConvertAll(pass => pass.Id);

        await Set
            .Where(pass => ids.Contains(pass.Id) && pass.ResolvedEmployeeId == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(pass => pass.ResolvedEmployeeId, employeeId)
                    .SetProperty(pass => pass.ResolvedAt, resolvedAt)
                    .SetProperty(pass => pass.ProcessedAt, (DateTimeOffset?)null),
                cancellationToken)
            .ConfigureAwait(false);

        return unmatched;
    }

    /// <inheritdoc />
    public async Task MarkProcessedAsync(
        IReadOnlyCollection<Guid> passIds,
        DateTimeOffset processedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(passIds);

        if (passIds.Count == 0)
        {
            return;
        }

        var ids = passIds.ToList();

        await Set
            .Where(pass => ids.Contains(pass.Id) && pass.ProcessedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(pass => pass.ProcessedAt, processedAt),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>DISTINCT</c> — bitta raqamning ko'p o'tishi bitta qator beradi; tartib
    /// faqat natija barqaror bo'lishi uchun (chegara ichida qaysilari olinishi
    /// har yurishda bir xil).
    /// </remarks>
    public async Task<IReadOnlyList<AttachableIdentity>> ListAttachableUnmatchedAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        var rows = await Set
            .AsNoTracking()
            .Where(pass => pass.ResolvedEmployeeId == null)
            .Join(
                Context.AttendanceIdentities,
                pass => new { Provider = pass.IdentityProvider, PersonId = pass.RawPersonId },
                identity => new { identity.Provider, PersonId = identity.ExternalPersonId },
                (pass, identity) => new { identity.Provider, identity.ExternalPersonId, identity.EmployeeId })
            .Distinct()
            .OrderBy(row => row.Provider)
            .ThenBy(row => row.ExternalPersonId)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ConvertAll(row => new AttachableIdentity(row.Provider, row.ExternalPersonId, row.EmployeeId));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Bitta so'rov: ichki qism kutayotgan o'tishlarni (xodim, sana) bo'yicha
    /// guruhlab eng eski <c>received_at</c> tartibida kesadi, tashqi qism o'sha
    /// juftliklarning kutayotgan o'tishlarini qaytaradi. Ikkalasi ham
    /// <c>ix_attendance_passes_pending</c> qisman indeksidan o'qiydi — navbat
    /// odatda bo'sh yoki bir necha yuz qator.
    /// </remarks>
    public async Task<IReadOnlyList<PendingPass>> ListPendingAsync(
        int maxDays,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDays);

        var pending = Set
            .AsNoTracking()
            .Where(pass => pass.ProcessedAt == null && pass.ResolvedEmployeeId != null);

        var oldestDays = pending
            .GroupBy(pass => new { pass.ResolvedEmployeeId, pass.LocalDate })
            .Select(day => new
            {
                day.Key.ResolvedEmployeeId,
                day.Key.LocalDate,
                FirstReceivedAt = day.Min(pass => pass.ReceivedAt),
            })
            .OrderBy(day => day.FirstReceivedAt)
            .ThenBy(day => day.ResolvedEmployeeId)
            .ThenBy(day => day.LocalDate)
            .Take(maxDays);

        var rows = await pending
            .Join(
                oldestDays,
                pass => new { pass.ResolvedEmployeeId, pass.LocalDate },
                day => new { day.ResolvedEmployeeId, day.LocalDate },
                (pass, day) => new { pass.Id, pass.ResolvedEmployeeId, pass.LocalDate })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ConvertAll(row => new PendingPass(row.Id, row.ResolvedEmployeeId!.Value, row.LocalDate));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExpiredPass>> ListExpiredPhotosAsync(
        DateTimeOffset receivedBefore,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        return await Set
            .AsNoTracking()
            .Where(pass => pass.PhotoKey != null && pass.ReceivedAt < receivedBefore)
            .OrderBy(pass => pass.ReceivedAt)
            .Take(take)
            .Select(pass => new ExpiredPass(pass.Id, pass.PhotoKey))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> MarkPhotosPurgedAsync(
        IReadOnlyCollection<Guid> passIds,
        DateTimeOffset purgedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(passIds);

        if (passIds.Count == 0)
        {
            return 0;
        }

        var ids = passIds.ToList();

        return await Set
            .Where(pass => ids.Contains(pass.Id) && pass.PhotoKey != null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(pass => pass.PhotoKey, (string?)null)
                    .SetProperty(pass => pass.RawEvent, (string?)null)
                    .SetProperty(pass => pass.PhotoPurgedAt, purgedAt),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <c>ix_attendance_passes_local_date</c> bo'yicha oraliq o'qish. Faqat
    /// <c>local_date</c> bo'yicha tartib — indeks tartibining o'zi, qo'shimcha
    /// saralashsiz. Holat sharti <see cref="DeleteExpiredAsync"/> dagi bilan AYNAN
    /// bir xil — ikkalasi ajralib ketsa, ro'yxatdagi qator o'chmay, to'plamni
    /// har yurishda to'sib turardi.
    /// </remarks>
    public async Task<IReadOnlyList<ExpiredPass>> ListExpiredPassesAsync(
        DateOnly localDateBefore,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        return await Set
            .AsNoTracking()
            .Where(pass =>
                pass.LocalDate < localDateBefore &&
                (pass.ProcessedAt != null || pass.ResolvedEmployeeId == null))
            .OrderBy(pass => pass.LocalDate)
            .Take(take)
            .Select(pass => new ExpiredPass(pass.Id, pass.PhotoKey))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> DeleteExpiredAsync(
        IReadOnlyCollection<Guid> passIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(passIds);

        if (passIds.Count == 0)
        {
            return 0;
        }

        var ids = passIds.ToList();

        return await Set
            .Where(pass => ids.Contains(pass.Id) && (pass.ProcessedAt != null || pass.ResolvedEmployeeId == null))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<int> CountExpiredPendingAsync(
        DateOnly localDateBefore,
        CancellationToken cancellationToken = default)
        => Set
            .AsNoTracking()
            .CountAsync(
                pass =>
                    pass.ProcessedAt == null &&
                    pass.ResolvedEmployeeId != null &&
                    pass.LocalDate < localDateBefore,
                cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <c>ix_attendance_passes_employee_time</c> ga aniq tushadi: birinchi
    /// ustun bo'yicha tenglik, ikkinchisi bo'yicha teskari yurish, bitta
    /// qator. Chegara ochiq (<c>&gt;</c>): «roppa-rosa 60 soniya oldingi»
    /// belgi endi to'smaydi va bu takror qoidasining ta'rifi bilan mos.
    /// </para>
    /// <para>
    /// ⚠️ Yo'nalish HISOBGA OLINMAYDI: takror qoidasi «shu odam yaqinda
    /// belgiladimi» degan savolga javob beradi, «shu tomonga belgiladimi»
    /// emas. Kirishdan keyin darhol bosilgan chiqish ham xatolik, va uni
    /// o'tkazib yuborish kunga ikkita ma'nosiz o'tish qo'shardi.
    /// </para>
    /// </remarks>
    public async Task<DateTimeOffset?> LastPassAtAsync(
        Guid employeeId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        // `Max` emas, tartiblab birinchisi: `Max` bo'sh to'plamda
        // `DateTimeOffset` uchun istisno tashlardi, nullable ga
        // o'rash esa so'rovni o'qishni qiyinlashtirardi.
        var rows = await Set
            .AsNoTracking()
            .Where(pass => pass.ResolvedEmployeeId == employeeId && pass.OccurredAt > since)
            .OrderByDescending(pass => pass.OccurredAt)
            .Select(pass => pass.OccurredAt)
            .Take(1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Count == 0 ? null : rows[0];
    }
}

/// <summary><see cref="IWorkScheduleRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class WorkScheduleRepository : Repository<WorkSchedule>, IWorkScheduleRepository
{
    public WorkScheduleRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Kunlar HAR DOIM birga yuklanadi: kunsiz jadval hech qanday
    /// savolga javob bermaydi (na tahrir ekrani, na hisoblash). Alohida
    /// yo'l ochilganida kimdir uni chaqirishni unutib, bo'sh haftani
    /// ko'rsatardi — <c>CandidateRepository.GetWithStagesAsync</c> dagi
    /// bilan bir xil mulohaza.
    /// </para>
    /// <para>
    /// Tartib <c>Include</c> ichida ATAYLAB: usiz PostgreSQL yettita
    /// kunni ixtiyoriy tartibda qaytaradi va tahrir ekranida hafta har
    /// ochilganda boshqacha ko'rinardi. So'rov
    /// <c>ux_work_schedule_days_day</c> indeksiga to'liq tushadi.
    /// </para>
    /// </remarks>
    public override Task<WorkSchedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Set
            .Include(schedule => schedule.Days.OrderBy(day => day.DayIndex))
            .FirstOrDefaultAsync(schedule => schedule.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkSchedule>> ListAsync(CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Include(schedule => schedule.Days.OrderBy(day => day.DayIndex))
            .OrderBy(schedule => schedule.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    /// <remarks><c>ux_work_schedules_name</c> indeksining ifodasi — <c>lower(name)</c>.</remarks>
    [SuppressMessage(
        "Globalization",
        "CA1304:Specify CultureInfo",
        Justification = "Ifoda daraxti SQL'ga tarjima qilinadi, .NET'da bajarilmaydi.")]
    [SuppressMessage(
        "Globalization",
        "CA1311:Specify a culture or use an invariant version",
        Justification = "Ifoda daraxti SQL'ga tarjima qilinadi, .NET'da bajarilmaydi.")]
    [SuppressMessage(
        "Performance",
        "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons",
        Justification = "EF Core StringComparison variantini SQL'ga tarjima qila olmaydi.")]
    public Task<bool> NameExistsAsync(
        string name,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalized = name.Trim().ToLowerInvariant();

        return Set
            .Where(schedule => schedule.Name.ToLower() == normalized && (exceptId == null || schedule.Id != exceptId))
            .AnyAsync(cancellationToken);
    }

    /// <summary>Jadvalni YUMSHOQ o'chiradi — kunlariga tegmasdan.</summary>
    /// <remarks>
    /// <para>
    /// ⚠️ Oddiy <c>Set.Remove</c> bu yerda YETMAYDI. Kunlar agregat ichida
    /// <c>Cascade</c> bilan bog'langan va EF ota yozuv «o'chirildi» deb
    /// belgilangan lahzadayoq kuzatilayotgan kunlarni ham «o'chirildi» qiladi.
    /// Keyin <c>AuditableInterceptor</c> jadvalni yumshoq o'chirishga
    /// aylantiradi — lekin <c>WorkScheduleDay</c> <c>ISoftDeletable</c> emas va
    /// yettala kun bazadan HAQIQATAN o'chib ketardi.
    /// </para>
    /// <para>
    /// Oqibati jimgina bo'lardi: o'chirilgan jadval <c>attendance_days.work_schedule_id</c>
    /// orqali tarixda qoladi va o'qish modeli uni kunlari bilan yuklaydi
    /// (<c>IgnoreQueryFilters</c>) — kunsiz jadval hisoblashda istisno otadi.
    /// Shu sababli kunlar qaytadan «o'zgarmagan» holatiga qo'yiladi.
    /// </para>
    /// </remarks>
    /// <param name="aggregate">O'chiriladigan jadval (kunlari bilan yuklangan).</param>
    public override void Remove(WorkSchedule aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        Set.Remove(aggregate);

        foreach (var day in aggregate.Days)
        {
            Context.Entry(day).State = EntityState.Unchanged;
        }
    }
}

/// <summary><see cref="IScheduleAssignmentRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class ScheduleAssignmentRepository : Repository<ScheduleAssignment>, IScheduleAssignmentRepository
{
    public ScheduleAssignmentRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    public Task<bool> AnyForScheduleAsync(Guid workScheduleId, CancellationToken cancellationToken = default)
        => Set.AnyAsync(assignment => assignment.WorkScheduleId == workScheduleId, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Uchala obyekt ustuni pog'onaga qarab tanlanadi — bazadagi
    /// <c>NULLS NOT DISTINCT</c> indeksi aynan shu besh ustunni solishtiradi.
    /// </remarks>
    public Task<bool> SameStartExistsAsync(
        ScheduleScope scope,
        Guid? targetId,
        DateOnly effectiveFrom,
        CancellationToken cancellationToken = default)
    {
        var employeeId = scope == ScheduleScope.Employee ? targetId : null;
        var positionId = scope == ScheduleScope.Position ? targetId : null;
        var departmentId = scope == ScheduleScope.Department ? targetId : null;

        return Set.AnyAsync(
            assignment =>
                assignment.Scope == scope &&
                assignment.EmployeeId == employeeId &&
                assignment.PositionId == positionId &&
                assignment.DepartmentId == departmentId &&
                assignment.EffectiveFrom == effectiveFrom,
            cancellationToken);
    }
}

/// <summary><see cref="IAttendanceDayRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class AttendanceDayRepository : Repository<AttendanceDay>, IAttendanceDayRepository
{
    public AttendanceDayRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    /// <remarks>
    /// <c>ux_attendance_days_employee_date</c> ga tushadi. <c>AsNoTracking</c>
    /// ATAYLAB yo'q — qatorlar shu zahoti <c>Recompute</c> bilan o'zgaradi.
    /// </remarks>
    public async Task<IReadOnlyList<AttendanceDay>> ListForUpdateAsync(
        IReadOnlyCollection<Guid> employeeIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employeeIds);

        if (employeeIds.Count == 0)
        {
            return [];
        }

        var ids = employeeIds.ToList();

        return await Set
            .Where(day => ids.Contains(day.EmployeeId) && day.LocalDate >= from && day.LocalDate <= to)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Faqat <see cref="AttendanceDay"/> yozuvlari uziladi: shu kontekstdagi
    /// boshqa narsa (masalan sinxron navbatda so'rov ALLAQACHON saqlagan
    /// moslik) tegilmaydi.
    /// </remarks>
    public void ForgetTracked()
    {
        foreach (var entry in Context.ChangeTracker.Entries<AttendanceDay>().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}

/// <summary><see cref="IAbsenceRequestRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class AbsenceRequestRepository : Repository<AbsenceRequest>, IAbsenceRequestRepository
{
    public AbsenceRequestRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc />
    public Task<bool> OverlapsAsync(
        Guid employeeId,
        DateOnly from,
        DateOnly to,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default)
        => Set
            .AsNoTracking()
            .AnyAsync(
                request =>
                    request.EmployeeId == employeeId &&
                    (exceptId == null || request.Id != exceptId) &&
                    (request.Status == AbsenceRequestStatus.Pending ||
                     request.Status == AbsenceRequestStatus.Approved) &&
                    request.FromDate <= to &&
                    request.ToDate >= from,
                cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AbsenceRequest>> ListApprovedAsync(
        IReadOnlyCollection<Guid> employeeIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employeeIds);

        if (employeeIds.Count == 0)
        {
            return [];
        }

        var ids = employeeIds.ToList();

        return await Set
            .AsNoTracking()
            .Where(request =>
                ids.Contains(request.EmployeeId) &&
                request.Status == AbsenceRequestStatus.Approved &&
                request.FromDate <= to &&
                request.ToDate >= from)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary><see cref="IWorkScheduleDateRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
public sealed class WorkScheduleDateRepository : IWorkScheduleDateRepository
{
    private readonly AppDbContext _context;

    public WorkScheduleDateRepository(AppDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkScheduleDate>> ListAsync(
        Guid workScheduleId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
        => await _context.WorkScheduleDates
            .AsNoTracking()
            .Where(date => date.WorkScheduleId == workScheduleId && date.Date >= from && date.Date <= to)
            .OrderBy(date => date.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    /// <remarks>
    /// ⚠️ <c>AsNoTracking</c> ATAYLAB YO'Q: topilgan katak darhol
    /// o'zgartiriladi yoki o'chiriladi (`WorkScheduleDateRepository`
    /// yozish yo'li), kuzatilmagan obyektdagi o'zgarish esa jimgina
    /// yo'qolardi.
    /// </remarks>
    public async Task<IReadOnlyList<WorkScheduleDate>> ListForUpdateAsync(
        Guid workScheduleId,
        IReadOnlyCollection<DateOnly> dates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dates);

        if (dates.Count == 0)
        {
            return [];
        }

        var wanted = dates.ToList();

        return await _context.WorkScheduleDates
            .Where(date => date.WorkScheduleId == workScheduleId && wanted.Contains(date.Date))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Add(WorkScheduleDate date) => _context.WorkScheduleDates.Add(date);

    /// <inheritdoc />
    public void Remove(WorkScheduleDate date) => _context.WorkScheduleDates.Remove(date);
}

/// <summary><see cref="IDeviceEnrollmentRepository"/> ning EF Core orqali amalga oshirilishi.</summary>
/// <remarks>
/// ⚠️ <c>AsNoTracking</c> ATAYLAB YO'Q: bu portdan o'qilgan qatorlar darhol
/// o'zgaradi (agent hisoboti, «Qayta yozish»). O'qish uchun alohida yo'l bor
/// — <c>EnrollmentReadModel</c>.
/// </remarks>
public sealed class DeviceEnrollmentRepository : IDeviceEnrollmentRepository
{
    private readonly AppDbContext _context;

    public DeviceEnrollmentRepository(AppDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<IReadOnlyList<DeviceEnrollment>> ListByDeviceAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
        => await _context.DeviceEnrollments
            .Where(row => row.DeviceId == deviceId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<DeviceEnrollment>> ListByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
        => await _context.DeviceEnrollments
            .Where(row => row.EmployeeId == employeeId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public void Add(DeviceEnrollment enrollment) => _context.DeviceEnrollments.Add(enrollment);
}

/// <summary><see cref="IDevicePersonNumbers"/> — PostgreSQL ketma-ketligi.</summary>
/// <remarks>
/// <para>
/// <c>nextval</c> tranzaksiyadan TASHQARIDA ishlaydi: qaytarib olinmaydi va
/// hech qachon ikki marta bir xil son bermaydi. Ya'ni tranzaksiya
/// bekor qilinsa raqamda «teshik» qoladi — bu mutlaqo zararsiz, chunki
/// raqam ketma-ket bo'lishi shart emas, faqat unikal bo'lishi kerak.
/// </para>
/// <para>
/// Chegara ketma-ketlikning O'ZIDA (<c>MAXVALUE</c>): 99 999 999 dan
/// keyin baza xato beradi va raqam jimgina takrorlanib ketmaydi.
/// </para>
/// </remarks>
public sealed class DevicePersonNumbers : IDevicePersonNumbers
{
    /// <summary>Ketma-ketlikning bazadagi nomi.</summary>
    public const string SequenceName = "device_person_no_seq";

    private readonly AppDbContext _context;

    public DevicePersonNumbers(AppDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<long> NextAsync(CancellationToken cancellationToken = default)
    {
        var values = await _context.Database
            .SqlQuery<long>($"SELECT nextval('device_person_no_seq')")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return values[0];
    }
}
