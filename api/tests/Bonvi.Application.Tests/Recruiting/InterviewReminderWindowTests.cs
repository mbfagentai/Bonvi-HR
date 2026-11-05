using Bonvi.Application.Recruiting.Candidates;
using Shouldly;

namespace Bonvi.Application.Tests.Recruiting;

/// <summary>The day-before reminder goes by day, in Tashkent time, and not at night.</summary>
public sealed class InterviewReminderWindowTests
{
    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    [Theory]
    [InlineData(9, 59)]
    [InlineData(20, 0)]
    [InlineData(23, 30)]
    [InlineData(3, 0)]
    public void Outside_the_hours_nothing_is_sent(int hour, int minute)
        => InterviewReminderWindow.At(new DateTimeOffset(2026, 11, 5, hour, minute, 0, Tashkent)).ShouldBeNull();

    [Theory]
    [InlineData(10, 0)]
    [InlineData(19, 59)]
    public void Inside_the_hours_the_window_is_tomorrow_in_tashkent(int hour, int minute)
    {
        var window = InterviewReminderWindow.At(new DateTimeOffset(2026, 11, 5, hour, minute, 0, Tashkent));

        window.ShouldNotBeNull();
        window.Value.InterviewFrom.ShouldBe(new DateTimeOffset(2026, 11, 6, 0, 0, 0, Tashkent));
        window.Value.InterviewTo.ShouldBe(new DateTimeOffset(2026, 11, 7, 0, 0, 0, Tashkent));
        window.Value.InvitedBefore.ShouldBe(new DateTimeOffset(2026, 11, 5, 0, 0, 0, Tashkent));
    }

    [Fact]
    public void The_day_is_tashkents_not_utcs()
    {
        // 19:30 UTC is already the 6th, 00:30, in Tashkent: outside the hours, not the 5th's evening.
        InterviewReminderWindow.At(new DateTimeOffset(2026, 11, 5, 19, 30, 0, TimeSpan.Zero)).ShouldBeNull();

        // 05:00 UTC is 10:00 in Tashkent.
        var window = InterviewReminderWindow.At(new DateTimeOffset(2026, 11, 5, 5, 0, 0, TimeSpan.Zero));
        window.ShouldNotBeNull();
        window.Value.InterviewFrom.ShouldBe(new DateTimeOffset(2026, 11, 6, 0, 0, 0, Tashkent));
    }
}
