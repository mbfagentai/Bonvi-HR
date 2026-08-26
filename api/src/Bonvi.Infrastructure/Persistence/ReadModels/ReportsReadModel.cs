using Bonvi.Application.Reports.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Bonvi.Infrastructure.Persistence.ReadModels;

/// <summary>
/// <see cref="IReportsReadModel"/> ning EF Core orqali amalga oshirilishi.
/// </summary>
public sealed class ReportsReadModel : IReportsReadModel
{
    private readonly AppDbContext _context;

    public ReportsReadModel(AppDbContext context) => _context = context;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// To'rt qadam: (1) oylik chegaralar C# da tuziladi (kalendar oylar,
    /// oxirgisi so'ralgan davr bilan kesiladi); (2) kelgan/ketgan hodisalar
    /// bittadan <c>GROUP BY</c> so'rovi bilan yil-oy bo'yicha yig'iladi;
    /// (3) har bir oy CHEGARASI uchun shtat soni alohida <c>COUNT</c> bilan
    /// olinadi (izohi <c>HeadcountAtAsync</c> da); (4) ketish sabablari
    /// bittadan so'rov bilan, keyin C# da guruhlanadi.
    /// </para>
    /// </remarks>
    public async Task<TurnoverReport> GetTurnoverAsync(
        TurnoverRange range,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(range);

        var boundaries = MonthBoundaries(range.From, range.To);

        // ── Oylik kelgan/ketgan — MUNOSABAT (hodisa) bo'yicha ───────────
        //
        // Ataylab bitta `GROUP BY` so'rovi, oyma-oy alohida `COUNT` emas:
        // bu yerda dedup shart emas (izohi `TurnoverMonth` da), shu sababli
        // eng oddiy va eng arzon yo'l — bazadagi guruhlash.
        var hiredByMonth = await _context.Employments
            .AsNoTracking()
            .Where(employment => employment.HireDate >= range.From && employment.HireDate <= range.To)
            .GroupBy(employment => new { employment.HireDate.Year, employment.HireDate.Month })
            .Select(group => new { group.Key.Year, group.Key.Month, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var leftByMonth = await _context.Employments
            .AsNoTracking()
            .Where(employment =>
                employment.TerminationDate != null
                && employment.TerminationDate >= range.From
                && employment.TerminationDate <= range.To)
            .GroupBy(employment => new
            {
                Year = employment.TerminationDate!.Value.Year,
                Month = employment.TerminationDate.Value.Month,
            })
            .Select(group => new { group.Key.Year, group.Key.Month, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hiredLookup = hiredByMonth.ToDictionary(row => (row.Year, row.Month), row => row.Count);
        var leftLookup = leftByMonth.ToDictionary(row => (row.Year, row.Month), row => row.Count);

        // ── Har bir chegara sanasi uchun shtat soni ──────────────────────
        //
        // Kerakli sanalar oldindan bittalab yig'ib olinadi (oy boshi va oy
        // oxiri takrorlanishi mumkin — masalan ikkita ketma-ket oyning
        // chegarasi), so'ng har biriga BITTA marta so'rov yuboriladi.
        var neededDates = new HashSet<DateOnly>();
        foreach (var (start, end) in boundaries)
        {
            neededDates.Add(start);
            neededDates.Add(end);
        }

        var headcountByDate = new Dictionary<DateOnly, int>();
        foreach (var date in neededDates)
        {
            headcountByDate[date] = await HeadcountAtAsync(date, cancellationToken).ConfigureAwait(false);
        }

        List<TurnoverMonth> months =
        [
            .. boundaries.Select(boundary => new TurnoverMonth(
                boundary.Start,
                boundary.End,
                hiredLookup.GetValueOrDefault((boundary.Start.Year, boundary.Start.Month)),
                leftLookup.GetValueOrDefault((boundary.Start.Year, boundary.Start.Month)),
                headcountByDate[boundary.Start],
                headcountByDate[boundary.End])),
        ];

        // ── Ketish sabablari ──────────────────────────────────────────
        var terminated = await (
                from employment in _context.Employments.AsNoTracking()
                where employment.TerminationDate != null
                    && employment.TerminationDate >= range.From
                    && employment.TerminationDate <= range.To
                join reason in _context.TerminationReasons
                    on employment.TerminationReasonId equals (Guid?)reason.Id into reasonMatch
                from reason in reasonMatch.DefaultIfEmpty()
                select new
                {
                    employment.TerminationReasonId,
                    ReasonName = (string?)reason!.Name,
                    IsVoluntary = (bool?)reason!.IsVoluntary,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<TurnoverReasonBucket> reasons =
        [
            .. terminated
                .GroupBy(row => row.TerminationReasonId)
                .Select(group =>
                {
                    var first = group.First();
                    return new TurnoverReasonBucket(
                        group.Key,
                        group.Key is null ? "Ko'rsatilmagan" : first.ReasonName ?? "Noma'lum sabab",
                        first.IsVoluntary,
                        group.Count());
                })
                .OrderByDescending(bucket => bucket.Count)
                .ThenBy(bucket => bucket.ReasonName, StringComparer.Ordinal),
        ];

        return new TurnoverReport(months, reasons);
    }

    /// <summary>
    /// Berilgan sanada ishlayotgan (hali ketmagan) xodimlar soni — ODAM bo'yicha.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Joriy <c>Status</c> ustuniga emas, <c>HireDate</c>/<c>TerminationDate</c>
    /// sanalariga tayanadi: bu ustunlar TARIXIY nuqtada ham to'g'ri javob
    /// beradi (masalan bir yil oldingi holat), <c>Status</c> esa faqat
    /// HOZIRGI holatni bildiradi. Boshqa hech qanday holat jurnali yo'q —
    /// bu ikki sana yagona ishonchli manba.
    /// </para>
    /// <para>
    /// Ikki tomondan yopiq: <c>HireDate &lt;= sana</c> va (<c>TerminationDate</c>
    /// bo'sh yoki <c>&gt;= sana</c>) — ya'ni bo'shatilgan kunning O'ZIDA odam
    /// hali ishlagan hisoblanadi.
    /// </para>
    /// </remarks>
    private Task<int> HeadcountAtAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var mos = _context.Employments.Where(employment =>
            employment.HireDate <= date
            && (employment.TerminationDate == null || employment.TerminationDate >= date));

        return _context.Employees
            .AsNoTracking()
            .CountAsync(
                employee => mos.Any(employment => employment.EmployeeId == employee.Id),
                cancellationToken);
    }

    /// <summary>
    /// <paramref name="from"/> dan <paramref name="to"/> gacha bo'lgan kalendar
    /// oylarning chegaralari.
    /// </summary>
    /// <remarks>
    /// Har bir oy 1-sanadan boshlanadi va oxirgi kunigacha davom etadi —
    /// OXIRGI oydan tashqari: u <paramref name="to"/> bilan kesiladi (joriy,
    /// hali tugamagan oy uchun — <c>MonthEnd</c> «bugun» bo'lib qoladi).
    /// </remarks>
    private static List<(DateOnly Start, DateOnly End)> MonthBoundaries(DateOnly from, DateOnly to)
    {
        List<(DateOnly Start, DateOnly End)> boundaries = [];

        var cursor = new DateOnly(from.Year, from.Month, 1);

        while (cursor <= to)
        {
            var monthEnd = cursor.AddMonths(1).AddDays(-1);
            var end = monthEnd < to ? monthEnd : to;

            boundaries.Add((cursor, end));

            cursor = cursor.AddMonths(1);
        }

        return boundaries;
    }
}
