using Bonvi.Application.Processes.Calendar;

namespace Bonvi.Application.Tests.Processes;

/// <summary>Process working-day calendar (SPEC-mening-ishlarim §6.10) and <c>nextCouncilDay</c> (§3.1).</summary>
public sealed class WorkingDayCalendarTests
{
    // 2026-11-02 Mon … 2026-11-06 Fri, 07 Sat, 08 Sun, 09 Mon, 12 Thu, 13 Fri
    private static DateOnly Nov(int day) => new(2026, 11, day);

    private static WorkingDayCalendar With(params ProcessCalendarDay[] days) => new(days);

    private static ProcessCalendarDay Holiday(DateOnly date) => new(date, ProcessCalendarDayKind.Holiday);

    private static ProcessCalendarDay WorkingDay(DateOnly date) => new(date, ProcessCalendarDayKind.WorkingDay);

    [Theory]
    [InlineData(2, true)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    [InlineData(8, false)]
    public void Monday_to_Friday_by_default(int day, bool working)
        => WorkingDayCalendar.MondayToFriday.IsWorkingDay(Nov(day)).ShouldBe(working);

    [Theory]
    [InlineData(6, 1, 9)]   // Fri +1 → Mon (weekend skipped)
    [InlineData(5, 2, 9)]   // Thu +2 → Mon
    [InlineData(2, 5, 9)]   // Mon +5 → next Mon
    [InlineData(7, 1, 9)]   // Sat +1 → Mon (start day not counted)
    public void Weekend_is_skipped(int from, int workingDays, int expected)
        => WorkingDayCalendar.MondayToFriday.AddWorkingDays(Nov(from), workingDays).ShouldBe(Nov(expected));

    [Fact]
    public void Holiday_is_skipped()
    {
        var calendar = With(Holiday(Nov(9)));

        calendar.IsWorkingDay(Nov(9)).ShouldBeFalse();
        calendar.AddWorkingDays(Nov(6), 1).ShouldBe(Nov(10));
    }

    [Fact]
    public void Working_Saturday_counts()
    {
        var calendar = With(WorkingDay(Nov(7)));

        calendar.IsWorkingDay(Nov(7)).ShouldBeTrue();
        calendar.IsWorkingDay(Nov(8)).ShouldBeFalse();
        calendar.AddWorkingDays(Nov(6), 1).ShouldBe(Nov(7));
        calendar.AddWorkingDays(Nov(6), 2).ShouldBe(Nov(9));
    }

    [Fact]
    public void Holiday_on_a_weekend_and_working_day_on_a_weekday_change_nothing()
    {
        var calendar = With(Holiday(Nov(7)), WorkingDay(Nov(4)));

        calendar.IsWorkingDay(Nov(7)).ShouldBeFalse();
        calendar.IsWorkingDay(Nov(4)).ShouldBeTrue();
        calendar.AddWorkingDays(Nov(6), 1).ShouldBe(Nov(9));
    }

    [Theory]
    [InlineData(9, -1, 6)]  // Mon −1 → Fri
    [InlineData(9, -5, 2)]  // Mon −5 → previous Mon
    [InlineData(8, -1, 6)]  // Sun −1 → Fri
    public void Negative_offset_moves_back(int from, int workingDays, int expected)
        => WorkingDayCalendar.MondayToFriday.AddWorkingDays(Nov(from), workingDays).ShouldBe(Nov(expected));

    [Fact]
    public void Negative_offset_skips_a_holiday()
        => With(Holiday(Nov(6))).AddWorkingDays(Nov(9), -1).ShouldBe(Nov(5));

    [Theory]
    [InlineData(4)]
    [InlineData(7)]  // Saturday stays Saturday
    [InlineData(9)]  // holiday below stays as given
    public void Zero_offset_returns_the_date_unchanged(int day)
        => With(Holiday(Nov(9))).AddWorkingDays(Nov(day), 0).ShouldBe(Nov(day));

    [Fact]
    public void Broken_holiday_list_fails_loudly()
    {
        var everyDay = Enumerable.Range(0, 4000).Select(offset => Holiday(Nov(1).AddDays(offset)));

        Should.Throw<InvalidOperationException>(() => new WorkingDayCalendar(everyDay).AddWorkingDays(Nov(2), 1));
    }

    [Theory]
    [InlineData(2, 5)]   // Mon → same week Thu
    [InlineData(5, 12)]  // Thu → strictly after → next Thu
    [InlineData(7, 12)]  // Sat → next Thu
    [InlineData(6, 12)]  // Fri → next Thu
    public void Next_council_day_is_strictly_after_the_anchor(int anchor, int expected)
        => WorkingDayCalendar.MondayToFriday.NextCouncilDay(Nov(anchor), DayOfWeek.Thursday).ShouldBe(Nov(expected));

    [Fact]
    public void Council_Thursday_on_a_holiday_moves_to_the_next_working_day()
        => With(Holiday(Nov(12))).NextCouncilDay(Nov(9), DayOfWeek.Thursday).ShouldBe(Nov(13));

    [Fact]
    public void Council_holiday_before_a_weekend_moves_past_it()
        => With(Holiday(new DateOnly(2026, 12, 31)), Holiday(new DateOnly(2027, 1, 1)))
            .NextCouncilDay(new DateOnly(2026, 12, 28), DayOfWeek.Thursday)
            .ShouldBe(new DateOnly(2027, 1, 4));

    [Fact]
    public void Council_day_follows_the_setting()
        => WorkingDayCalendar.MondayToFriday.NextCouncilDay(Nov(2), DayOfWeek.Tuesday).ShouldBe(Nov(3));

    [Theory]
    [InlineData(1, DayOfWeek.Monday)]
    [InlineData(4, DayOfWeek.Thursday)]
    [InlineData(6, DayOfWeek.Saturday)]
    [InlineData(7, DayOfWeek.Sunday)]
    public void From_iso_weekday(int iso, DayOfWeek expected)
        => WorkingDayCalendar.FromIsoWeekday(iso).ShouldBe(expected);

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(-1)]
    public void From_iso_weekday_out_of_range(int iso)
        => Should.Throw<ArgumentOutOfRangeException>(() => WorkingDayCalendar.FromIsoWeekday(iso));
}
