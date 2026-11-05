using Bonvi.Application.Processes.Calendar;
using Bonvi.Domain.Abstractions;
using Bonvi.Domain.Processes;
using NSubstitute;

namespace Bonvi.Application.Tests.Processes;

/// <summary>The calendar built from <c>process_holidays</c> rows (SPEC-mening-ishlarim §6.10).</summary>
public sealed class HolidayWorkingDayCalendarSourceTests
{
    private static readonly DateOnly Monday = new(2026, 11, 9);
    private static readonly DateOnly Tuesday = new(2026, 11, 10);
    private static readonly DateOnly Wednesday = new(2026, 11, 11);
    private static readonly DateOnly Friday = new(2026, 11, 13);
    private static readonly DateOnly Saturday = new(2026, 11, 14);
    private static readonly DateOnly NextSaturday = new(2026, 11, 21);

    private readonly IProcessHolidayRepository _holidays = Substitute.For<IProcessHolidayRepository>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ProcessHoliday Row(DateOnly date, ProcessHolidayKind kind)
        => ProcessHoliday.Create(date, kind, "Test").Value;

    private void Stored(params ProcessHoliday[] rows)
        => _holidays.ListAllAsync(Arg.Any<CancellationToken>()).Returns(rows);

    [Fact]
    public async Task Holiday_row_is_a_day_off_and_is_skipped()
    {
        Stored(Row(Tuesday, ProcessHolidayKind.Holiday));

        var calendar = await new HolidayWorkingDayCalendarSource(_holidays).LoadAsync(Ct);

        calendar.IsWorkingDay(Tuesday).ShouldBeFalse();
        calendar.AddWorkingDays(Monday, 1).ShouldBe(Wednesday);
    }

    [Fact]
    public async Task Working_day_row_makes_a_saturday_count()
    {
        Stored(Row(Saturday, ProcessHolidayKind.WorkingDay));

        var calendar = await new HolidayWorkingDayCalendarSource(_holidays).LoadAsync(Ct);

        calendar.IsWorkingDay(Saturday).ShouldBeTrue();
        calendar.IsWorkingDay(NextSaturday).ShouldBeFalse();
        calendar.AddWorkingDays(Friday, 1).ShouldBe(Saturday);
    }

    [Fact]
    public async Task No_rows_means_monday_to_friday()
    {
        Stored();

        var calendar = await new HolidayWorkingDayCalendarSource(_holidays).LoadAsync(Ct);

        calendar.IsWorkingDay(Tuesday).ShouldBeTrue();
        calendar.IsWorkingDay(Saturday).ShouldBeFalse();
    }

    [Fact]
    public async Task Every_load_reads_the_table_and_a_loaded_calendar_stays_as_it_was()
    {
        var source = new HolidayWorkingDayCalendarSource(_holidays);

        Stored();
        var before = await source.LoadAsync(Ct);

        Stored(Row(Tuesday, ProcessHolidayKind.Holiday));
        var after = await source.LoadAsync(Ct);

        before.IsWorkingDay(Tuesday).ShouldBeTrue();
        after.IsWorkingDay(Tuesday).ShouldBeFalse();
    }
}
