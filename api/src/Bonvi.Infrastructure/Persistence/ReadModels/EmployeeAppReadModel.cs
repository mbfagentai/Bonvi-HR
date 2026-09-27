using Bonvi.Application.Attendance.Computation;
using Bonvi.Application.Me;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Attendance;
using Bonvi.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary><see cref="IEmployeeAppReadModel"/> ning EF Core orqali amalga oshirilishi.</summary>
/// <remarks>
/// <para>
/// Hamma so'rov <c>AsNoTracking</c>: bu yerdan o'qilgan hech narsa
/// o'zgarmaydi.
/// </para>
/// <para>
/// ⚠️ Ikkala metod ham xodim identifikatorini MAJBURIY argument sifatida
/// oladi va «hammasi» degan variantni taklif qilmaydi — bu o'qish
/// modelining butun xavfsizlik shartnomasi (sababi interfeys izohida).
/// </para>
/// </remarks>
internal sealed class EmployeeAppReadModel : IEmployeeAppReadModel
{
    private readonly AppDbContext _context;

    public EmployeeAppReadModel(AppDbContext context) => _context = context;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Bo'lim va lavozim BUGUNGI munosabatdan olinadi, oxirgi kun
    /// yozuvining suratidan emas: xodimga o'zining HOZIRGI joyi
    /// ko'rsatiladi. (Tabel esa teskarisini qiladi va to'g'ri qiladi —
    /// hisobot ko'chirishdan keyin ham o'zgarmasligi kerak.)
    /// </para>
    /// <para>
    /// Bir nechta amaldagi munosabatda ASOSIYsi tanlanadi
    /// (<c>is_primary</c>), u ham bo'lmasa — eng kechroq qabul qilingani.
    /// Ikki firmada ishlaydigan odamga ikkita bo'lim ko'rsatishning
    /// ma'nosi yo'q: ilovada bu bitta qator sarlavha, ro'yxat emas.
    /// </para>
    /// </remarks>
    public async Task<MyProfileRow?> ProfileAsync(
        Guid employeeId,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var person = await _context.Employees
            .AsNoTracking()
            .Where(employee => employee.Id == employeeId)
            .Select(employee => new
            {
                employee.FullName.LastName,
                employee.FullName.FirstName,
                employee.FullName.MiddleName,
                employee.PhotoKey,
                employee.MobileCheckInAllowed,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (person is null)
        {
            return null;
        }

        var employment = await _context.Employments
            .AsNoTracking()
            .Where(row =>
                row.EmployeeId == employeeId &&
                row.HireDate <= today &&
                (row.TerminationDate == null || row.TerminationDate >= today))
            .OrderByDescending(row => row.IsPrimary)
            .ThenByDescending(row => row.HireDate)
            .Select(row => new { row.DepartmentId, row.PositionId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var department = employment?.DepartmentId is { } departmentId
            ? await _context.Departments
                .AsNoTracking()
                .Where(row => row.Id == departmentId)
                .Select(row => row.Name)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
            : null;

        var position = employment?.PositionId is { } positionId
            ? await _context.Positions
                .AsNoTracking()
                .Where(row => row.Id == positionId)
                .Select(row => row.Name)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
            : null;

        var fullName = FullName.FromTrusted(person.LastName, person.FirstName, person.MiddleName);

        return new MyProfileRow(
            fullName.Full,
            fullName.ShortForm,
            person.PhotoKey,
            department,
            position,
            person.MobileCheckInAllowed);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Uchta so'rov, kun bo'yicha aylanish YO'Q: oyning (± bir kun) o'tishlari
    /// (<c>ix_attendance_passes_employee_time</c>), FAQAT telefon belgilarining
    /// obyekt nomlari (<c>ux_mobile_check_ins_pass</c> bo'yicha
    /// <c>WHERE pass_id = ANY(…)</c>) va shu xodimning kun yozuvlaridagi reja
    /// suratlari. 31 ta so'rov o'rniga 3 ta.
    /// </para>
    /// <para>
    /// O'tishlar davrdan bir kun KENG o'qiladi: oyning birinchi kuni ertalabki
    /// chiqish o'tgan oyning oxirgi katagiga, oxirgi kun kechki kirishi esa shu
    /// oyga tegishli bo'lishi mumkin — qaysi biri qaysi katakka tushishini
    /// faqat kun oynasi aytadi.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyDictionary<DateOnly, IReadOnlyList<MyMarkDto>>> MarksAsync(
        Guid employeeId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var marks = await AttributedAsync(
                employeeId,
                from.AddDays(-1),
                to.AddDays(1),
                since: null,
                cancellationToken)
            .ConfigureAwait(false);

        return marks
            .Where(mark => mark.Date >= from && mark.Date <= to)
            .GroupBy(mark => mark.Date)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<MyMarkDto>)[.. group.Select(mark => mark.Mark)]);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MyAttributedMark>> RecentMarksAsync(
        Guid employeeId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
        => AttributedAsync(employeeId, since.TashkentDate(), passTo: null, since, cancellationToken);

    /// <summary>
    /// O'tishlar (kalendar sanasi oralig'ida) — har biri tabel qoidasi bo'yicha katagi bilan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Katak kun yozuvidagi REJA SURATIDAN (<c>plan_start_at</c>,
    /// <c>plan_end_at</c>) va <c>PassAttribution</c> ning o'zi bilan aniqlanadi:
    /// hisoblash qaysi oynani ishlatgan bo'lsa, belgi aynan o'sha katakka
    /// tushadi — jadval keyin o'zgargan bo'lsa ham. Jadvallarni qayta yuklab,
    /// rejani o'zi hisoblash esa ikkinchi, ajralib keta oladigan yo'l bo'lardi.
    /// </para>
    /// <para>
    /// ⚠️ <c>IgnoreQueryFilters</c> obyektlarda ATAYLAB: obyekt keyinchalik
    /// yumshoq o'chirilgan bo'lsa ham, o'sha paytdagi belgi qayerda qabul
    /// qilinganini aytishda davom etishi kerak — aks holda tarix jimgina
    /// «obyektsiz» bo'lib qolardi.
    /// </para>
    /// </remarks>
    private async Task<IReadOnlyList<MyAttributedMark>> AttributedAsync(
        Guid employeeId,
        DateOnly passFrom,
        DateOnly? passTo,
        DateTimeOffset? since,
        CancellationToken cancellationToken)
    {
        var query = _context.AttendancePasses
            .AsNoTracking()
            .Where(pass => pass.ResolvedEmployeeId == employeeId && pass.LocalDate >= passFrom);

        if (passTo is { } upper)
        {
            query = query.Where(pass => pass.LocalDate <= upper);
        }

        if (since is { } lower)
        {
            var lowerUtc = lower.ToUniversalTime();
            query = query.Where(pass => pass.OccurredAt >= lowerUtc);
        }

        var passes = await query
            .OrderBy(pass => pass.OccurredAt)
            .ThenBy(pass => pass.Id)
            .Select(pass => new
            {
                pass.Id,
                pass.LocalDate,
                pass.OccurredAt,
                pass.Direction,
                pass.Method,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (passes.Count == 0)
        {
            return [];
        }

        var mobileIds = passes
            .Where(pass => pass.Method == PassMethod.Mobile)
            .Select(pass => pass.Id)
            .ToList();

        var siteNames = mobileIds.Count == 0
            ? []
            : await _context.MobileCheckIns
                .AsNoTracking()
                .Where(checkIn => mobileIds.Contains(checkIn.PassId))
                .Join(
                    _context.WorkSites.AsNoTracking().IgnoreQueryFilters(),
                    checkIn => checkIn.WorkSiteId,
                    site => site.Id,
                    (checkIn, site) => new { checkIn.PassId, site.Name })
                .ToDictionaryAsync(row => row.PassId, row => row.Name, cancellationToken)
                .ConfigureAwait(false);

        // Nomzod kunlar o'tishning o'z sanasi ± 1 — reja suratlari shu
        // oraliq uchun bitta so'rov bilan.
        var planFrom = passes[0].LocalDate.AddDays(-1);
        var planTo = passes[^1].LocalDate.AddDays(1);

        var plans = await _context.AttendanceDays
            .AsNoTracking()
            .Where(day =>
                day.EmployeeId == employeeId &&
                day.LocalDate >= planFrom &&
                day.LocalDate <= planTo)
            .Select(day => new { day.LocalDate, day.PlanStartAt, day.PlanEndAt, day.LastOutAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var windows = plans.ToDictionary(
            day => day.LocalDate,
            day => PassAttribution.WindowOf(day.PlanStartAt, day.PlanEndAt));

        /*
          Kechagi smenaning KECH CHIQISHI (egasining qarori, 2026-09-27):
          hech bir oynaga tushmagan o'tishni tabel kechagi kunga beradi —
          faqat kecha ochiq kirish bilan tugagan bo'lsa (PassAttribution.
          LateExitDateFor). Bu shartni bu yerda qayta hisoblash o'rniga
          tabelning SAQLANGAN qarori o'qiladi: kechagi kunning oxirgi
          chiqishi aynan shu o'tish bo'lsa, belgi o'sha katakka tushadi.
          Aks holda ilova 00:20 dagi chiqishni seshanbada, tabel esa
          dushanbada ko'rsatardi.
        */
        var lastOuts = plans
            .Where(day => day.LastOutAt is not null)
            .ToDictionary(day => day.LocalDate, day => day.LastOutAt!.Value);

        DateOnly CellOf(DateOnly localDate, DateTimeOffset occurredAt)
        {
            if (PassAttribution.WindowDateFor(localDate, occurredAt, date => windows.GetValueOrDefault(date)) is { } windowDate)
            {
                return windowDate;
            }

            var previous = localDate.AddDays(-1);

            return lastOuts.TryGetValue(previous, out var lastOut) && lastOut == occurredAt
                ? previous
                : localDate;
        }

        return
        [
            .. passes.Select(pass => new MyAttributedMark(
                CellOf(pass.LocalDate, pass.OccurredAt),
                new MyMarkDto(
                    pass.OccurredAt,
                    pass.Direction,
                    pass.Method == PassMethod.Mobile ? MarkSource.Mobile : MarkSource.Terminal,
                    pass.Method == PassMethod.Mobile ? siteNames.GetValueOrDefault(pass.Id) : null))),
        ];
    }
}
