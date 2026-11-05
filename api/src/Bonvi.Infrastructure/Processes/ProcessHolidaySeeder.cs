using Bonvi.Domain.Common;
using Bonvi.Domain.Processes;
using Bonvi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bonvi.Infrastructure.Processes;

/// <summary>Result of one holiday seed run (log and tests).</summary>
/// <param name="Created">Rows added.</param>
/// <param name="YearsSkipped">Seed years left alone because they already have rows.</param>
public sealed record ProcessHolidaySeedResult(int Created, int YearsSkipped);

/// <summary>
/// Seeds the fixed-date Uzbek holidays into <c>process_holidays</c> (SPEC-mening-ishlarim §6.10). Moving holidays
/// (Ramazon / Qurbon hayit) and government transfers are entered by HR-1.
/// </summary>
/// <remarks>
/// <para>
/// A year that already has ANY row is left alone, so HR owns it from the first seed on: a removed holiday does
/// not come back on restart and an edited title stays (same rule as <c>CatalogSeeder</c>, per year so that a
/// later release can add a new year). The one exception: a year HR emptied completely is seeded again.
/// </para>
/// <para>
/// Holidays that fall on a weekend are stored too: they change nothing in the calendar, and HR sees the same
/// list of dates every year.
/// </para>
/// </remarks>
public sealed partial class ProcessHolidaySeeder
{
    private static readonly int[] SeededYears = [2026, 2027];

    private static readonly (int Month, int Day, string Title)[] FixedHolidays =
    [
        (1, 1, "Yangi yil"),
        (3, 8, "Xalqaro xotin-qizlar kuni"),
        (3, 21, "Navro'z bayrami"),
        (5, 9, "Xotira va qadrlash kuni"),
        (9, 1, "Mustaqillik kuni"),
        (10, 1, "O'qituvchi va murabbiylar kuni"),
        (12, 8, "Konstitutsiya kuni"),
        (12, 31, "Yangi yil arafasi"),
    ];

    private readonly AppDbContext _context;
    private readonly ILogger<ProcessHolidaySeeder> _logger;

    public ProcessHolidaySeeder(AppDbContext context, ILogger<ProcessHolidaySeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    [LoggerMessage(
        EventId = 2506,
        Level = LogLevel.Information,
        Message = "Process holidays: {Created} added, {YearsSkipped} years already filled")]
    private partial void LogTotal(int created, int yearsSkipped);

    public async Task<ProcessHolidaySeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        int created = 0, yearsSkipped = 0;

        foreach (var year in SeededYears)
        {
            var first = new DateOnly(year, 1, 1);
            var last = new DateOnly(year, 12, 31);

            var filled = await _context.ProcessHolidays
                .AnyAsync(row => row.Date >= first && row.Date <= last, cancellationToken)
                .ConfigureAwait(false);

            if (filled)
            {
                yearsSkipped++;
                continue;
            }

            foreach (var (month, day, title) in FixedHolidays)
            {
                _context.ProcessHolidays.Add(
                    Ensure(ProcessHoliday.Create(new DateOnly(year, month, day), ProcessHolidayKind.Holiday, title)));
                created++;
            }
        }

        // One save: all the years of a run land together or not at all
        if (created > 0)
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        LogTotal(created, yearsSkipped);
        return new ProcessHolidaySeedResult(created, yearsSkipped);
    }

    /// <summary>The list above is constant: a rejection is a programming error, not bad input.</summary>
    private static ProcessHoliday Ensure(Result<ProcessHoliday> result)
        => result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException($"Process holiday seed rejected: {result.Error.Code}");
}
